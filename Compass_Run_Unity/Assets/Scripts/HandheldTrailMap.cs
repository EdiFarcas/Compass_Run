using System;
using System.Collections.Generic;
using UnityEngine;
using CompassRun.Checkpoints;

/// <summary>A north-up paper map of the current generated terrain. No live player/GPS marker.</summary>
[DisallowMultipleComponent]
public sealed class HandheldTrailMap : MonoBehaviour
{
    [SerializeField] TrailGenerator generator;
    [SerializeField, Range(256, 1536)] int resolution = 1024;
    Texture2D texture;
    MeshFilter faceFilter;
    Mesh originalMesh, mappedMesh;
    Renderer faceRenderer;
    int materialIndex;
    bool started;
    MaterialPropertyBlock previousProperties;
    Color32[] pixels, terrainPixels;
    int width, height, mapLeft, mapBottom, mapSize;
    Vector3 terrainOrigin, terrainSize;
    readonly List<CheckpointObjective> checkpoints = new List<CheckpointObjective>();
    public Texture2D MapTexture => texture;
    public int GenerationCount { get; private set; }

    void OnEnable()
    {
        if (!generator) generator = FindAnyObjectByType<TrailGenerator>();
        if (generator) generator.Generated += Rebuild;
        if (started) Rebuild();
    }
    void Start() { started = true; if (!texture && generator && generator.keyPoints.Count == 5) Rebuild(); }
    void OnDisable()
    {
        if (generator) generator.Generated -= Rebuild;
        Unsubscribe();
        if (faceFilter && mappedMesh) faceFilter.sharedMesh = originalMesh;
        if (faceRenderer && previousProperties != null) faceRenderer.SetPropertyBlock(previousProperties, materialIndex);
        if (mappedMesh) Destroy(mappedMesh);
        if (texture) Destroy(texture);
        mappedMesh = null; texture = null;
    }
    void Unsubscribe()
    {
        foreach (var cp in checkpoints) if (cp) cp.OnValidated.RemoveListener(DrawMarkers);
        checkpoints.Clear();
    }
    public void Rebuild()
    {
        if (!generator || !generator.terrain || generator.keyPoints.Count != 5) return;
        if (!mappedMesh && !PrepareFace()) return;
        Unsubscribe();
        foreach (var cp in generator.GetComponentsInChildren<CheckpointObjective>())
        { checkpoints.Add(cp); cp.OnValidated.AddListener(DrawMarkers); }
        var td = generator.terrain.terrainData;
        terrainOrigin = generator.terrain.transform.position;
        terrainSize = td.size;
        var heights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
        int n = td.heightmapResolution;
        pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(242, 234, 207, 255);
        mapSize = Mathf.Min(width - 64, height - 155); mapLeft = (width - mapSize) / 2; mapBottom = 83;
        for (int y = 0; y < mapSize; y++) for (int x = 0; x < mapSize; x++)
        {
            float u = x / (float)(mapSize - 1), v = y / (float)(mapSize - 1);
            int hx = Mathf.RoundToInt(u * (n - 1)), hz = Mathf.RoundToInt(v * (n - 1));
            float elevation = heights[hz, hx] * td.size.y;
            float neighbor = heights[Mathf.Min(hz + 1, n - 1), Mathf.Min(hx + 1, n - 1)] * td.size.y;
            var color = Color.Lerp(new Color(.78f, .85f, .64f), new Color(.92f, .87f, .69f), elevation / Mathf.Max(1, generator.maxHeight));
            if (elevation < generator.waterLevel && generator.waterPrefab) color = new Color(.39f, .71f, .83f);
            else if (Mathf.FloorToInt(elevation / 5) != Mathf.FloorToInt(neighbor / 5)) color = new Color(.59f, .54f, .37f);
            pixels[(mapBottom + y) * width + mapLeft + x] = color;
        }
        var pathColor = new Color32(119, 85, 48, 255);
        foreach (var trail in generator.trails) for (int i = 1; i < trail.pts.Count; i++)
            Line(WorldToPixel(generator.transform.TransformPoint(trail.pts[i - 1])), WorldToPixel(generator.transform.TransformPoint(trail.pts[i])), pathColor, 2);
        var ink = new Color32(35, 49, 34, 255);
        for (int i = 0; i <= 6; i++)
        {
            int k = i * (mapSize - 1) / 6;
            Line(new Vector2Int(mapLeft + k, mapBottom), new Vector2Int(mapLeft + k, mapBottom + mapSize - 1), new Color32(154, 166, 131, 255), 0, true);
            Line(new Vector2Int(mapLeft, mapBottom + k), new Vector2Int(mapLeft + mapSize - 1, mapBottom + k), new Color32(154, 166, 131, 255), 0, true);
        }
        Label("COMPASS RUN", 32, height - 35, ink, 3);
        Label("N", width - 60, height - 35, ink, 3);
        Line(new Vector2Int(width - 52, height - 93), new Vector2Int(width - 52, height - 65), ink, 2);
        Line(new Vector2Int(width - 52, height - 65), new Vector2Int(width - 58, height - 75), ink, 2);
        Label("START   CP1 CP2 CP3", 32, 47, ink, 2);
        Label("50 M", mapLeft, 19, ink, 2);
        int scalePixels = Mathf.RoundToInt(mapSize * 50f / terrainSize.x);
        Line(new Vector2Int(mapLeft + 75, 20), new Vector2Int(mapLeft + 75 + scalePixels, 20), ink, 2);
        terrainPixels = (Color32[])pixels.Clone();
        if (texture) Destroy(texture);
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Current Trail Paper Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var properties = new MaterialPropertyBlock(); faceRenderer.GetPropertyBlock(properties, materialIndex);
        properties.SetTexture("_BaseMap", texture); properties.SetTexture("_MainTex", texture); properties.SetColor("_BaseColor", Color.white);
        faceRenderer.SetPropertyBlock(properties, materialIndex);
        GenerationCount++;
        DrawMarkers();
    }
    bool PrepareFace()
    {
        foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
        {
            int slot = Array.FindIndex(renderer.sharedMaterials, m => m && m.name.StartsWith("Mat_Mapface", StringComparison.Ordinal));
            if (slot < 0) continue;
            faceFilter = renderer.GetComponent<MeshFilter>(); if (!faceFilter || !faceFilter.sharedMesh) continue;
            originalMesh = faceFilter.sharedMesh;
            if (!originalMesh.isReadable) { Debug.LogError("Enable Read/Write on Harta.fbx for the generated paper map.", this); return false; }
            var vertices = originalMesh.vertices; var indices = originalMesh.GetTriangles(slot);
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < indices.Length; i += 3) normal += Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
            normal.Normalize();
            Vector3 up = Vector3.ProjectOnPlane(faceFilter.transform.InverseTransformDirection(Vector3.forward), normal).normalized;
            if (up.sqrMagnitude < .1f) up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            Vector3 right = Vector3.ProjectOnPlane(faceFilter.transform.InverseTransformDirection(Vector3.right), normal);
            right = Vector3.ProjectOnPlane(right, up).normalized;
            if (right.sqrMagnitude < .1f) right = Vector3.Cross(up, normal).normalized;
            float minX = float.PositiveInfinity, minY = minX, maxX = float.NegativeInfinity, maxY = maxX;
            foreach (int i in indices) { float x = Vector3.Dot(vertices[i], right), y = Vector3.Dot(vertices[i], up); minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            float w = (maxX - minX) * faceFilter.transform.TransformVector(right).magnitude, h = (maxY - minY) * faceFilter.transform.TransformVector(up).magnitude;
            width = Mathf.Max(256, Mathf.RoundToInt(resolution * w / Mathf.Max(w, h))); height = Mathf.Max(256, Mathf.RoundToInt(resolution * h / Mathf.Max(w, h)));
            var uv = originalMesh.uv;
            foreach (int i in indices) uv[i] = new Vector2(Mathf.InverseLerp(minX, maxX, Vector3.Dot(vertices[i], right)), Mathf.InverseLerp(minY, maxY, Vector3.Dot(vertices[i], up)));
            mappedMesh = Instantiate(originalMesh); mappedMesh.name = originalMesh.name + " (Map UV)"; mappedMesh.uv = uv; faceFilter.sharedMesh = mappedMesh;
            faceRenderer = renderer; materialIndex = slot; previousProperties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(previousProperties, slot);
            return true;
        }
        Debug.LogError("No Mat_Mapface surface found on handheld map.", this); return false;
    }
    public Vector2Int WorldToPixel(Vector3 world) => new Vector2Int(mapLeft + Mathf.RoundToInt((world.x - terrainOrigin.x) / terrainSize.x * (mapSize - 1)), mapBottom + Mathf.RoundToInt((world.z - terrainOrigin.z) / terrainSize.z * (mapSize - 1)));
    void DrawMarkers()
    {
        if (!texture || terrainPixels == null) return;
        Array.Copy(terrainPixels, pixels, pixels.Length);
        var start = WorldToPixel(generator.transform.TransformPoint(generator.keyPoints[0])); var red = new Color32(170, 35, 60, 255);
        Line(start + new Vector2Int(0, 14), start + new Vector2Int(-12, -10), red, 3); Line(start + new Vector2Int(-12, -10), start + new Vector2Int(12, -10), red, 3); Line(start + new Vector2Int(12, -10), start + new Vector2Int(0, 14), red, 3);
        Label("START", start.x - 70, start.y - 22, red, 2);
        foreach (var cp in checkpoints)
        {
            if (!cp) continue;
            var point = WorldToPixel(cp.transform.position); var color = cp.IsCompleted ? new Color32(23, 110, 55, 255) : new Color32(155, 36, 123, 255);
            for (int a = 0; a < 360; a++) { float r = a * Mathf.Deg2Rad; Dot(point.x + Mathf.RoundToInt(16 * Mathf.Cos(r)), point.y + Mathf.RoundToInt(16 * Mathf.Sin(r)), color, 2); }
            Label(cp.CheckpointId, point.x + 20, point.y + 14, color, 2);
        }
        texture.SetPixels32(pixels); texture.Apply(false);
    }
    void Dot(int x, int y, Color32 c, int radius = 0) { for (int j = -radius; j <= radius; j++) for (int i = -radius; i <= radius; i++) { int px = x + i, py = y + j; if (px >= 0 && px < width && py >= 0 && py < height) pixels[py * width + px] = c; } }
    void Line(Vector2Int a, Vector2Int b, Color32 c, int radius, bool dashed = false)
    { int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)); for (int i = 0; i <= steps; i++) { if (dashed && i % 18 > 2) continue; var p = Vector2.Lerp(a, b, steps == 0 ? 0 : i / (float)steps); Dot(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), c, radius); } }
    static readonly Dictionary<char, string> glyphs = new Dictionary<char, string> {
        {'A',"01110100011000111111100011000110001"},{'C',"01111100001000010000100001000001111"},{'M',"10001110111010110101100011000110001"},{'N',"10001110011010110011100011000110001"},{'O',"01110100011000110001100011000101110"},{'P',"11110100011000111110100001000010000"},{'R',"11110100011000111110101001001010001"},{'S',"01111100001000001110000010000111110"},{'T',"11111001000010000100001000010000100"},{'U',"10001100011000110001100011000101110"},{'1',"00100011000010000100001000010001110"},{'2',"01110100010000100010001000100011111"},{'3',"11110000010000101110000010000111110"},{'5',"11111100001000011110000010000111110"},{'0',"01110100011001110101110011000101110"}};
    void Label(string text, int x, int y, Color32 c, int scale)
    { x = Mathf.Clamp(x, 3, Mathf.Max(3, width - text.Length * 6 * scale)); y = Mathf.Clamp(y, 7 * scale, height - 3); foreach (char ch in text) { if (glyphs.TryGetValue(ch, out var glyph)) for (int row = 0; row < 7; row++) for (int col = 0; col < 5; col++) if (glyph[row * 5 + col] == '1') for (int j = 0; j < scale; j++) for (int i = 0; i < scale; i++) Dot(x + col * scale + i, y - row * scale - j, c); x += 6 * scale; } }
}
