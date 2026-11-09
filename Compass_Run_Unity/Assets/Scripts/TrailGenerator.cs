using System.Collections.Generic;
using UnityEngine;

// Tipuri de drum. Main = traseul corect Start->CP1->CP2->CP3->Finish.
public enum TrailKind { Main, Shortcut, WrongLoop, DeadEnd, Danger }

public class Trail
{
    public TrailKind kind;
    public List<Vector3> pts = new List<Vector3>(); // coordonate locale (relativ la generator)
}

public class TrailGenerator : MonoBehaviour
{
    [Header("Seed & dimensiuni")]
    public int seed = 0;                 // 0 = aleator la fiecare rulare
    public float areaSize = 250f;        // zona jucabilă (m)
    public float stepLen = 6f;           // distanța dintre punctele potecii
    [Range(0f, 0.6f)] public float wander = 0.3f; // cât de șerpuită e poteca

    [Header("Ramificații")]
    public int branchesPerSegment = 2;   // câte răspântii pe fiecare tronson
    public Vector2 branchLength = new Vector2(40f, 90f);
    public float minTrailGap = 12f;      // distanța minimă între drumuri diferite
    [Header("Ponderi tipuri de ramificații")]
    public float wShortcut = 0.25f, wWrongLoop = 0.2f, wDeadEnd = 0.3f, wDanger = 0.25f;

    [Header("Terrain (opțional)")]
    public Terrain terrain;
    public float terrainSize = 300f;
    public float maxHeight = 60f;
    public float noiseScale = 0.012f;
    public float trailWidth = 1.5f;      // jumătate din lățimea potecii
    public int pathLayerIndex = 1;       // indexul layer-ului de textură "pământ/potecă"
    public float startClearingRadius = 15f; // poiana plată de la Start

    [Header("Vegetație & Apă")]
    public GameObject waterPrefab;       // Un Plane/Quad cu material de apă
    public float waterLevel = 6f;        // Tot ce e sub această înălțime pe teren devine lac
    public GameObject[] treePrefabs;     // Pune aici 1-2 prefab-uri de copaci
    public int treeCount = 600;          // Câți copaci să genereze
    public Texture2D grassTexture;       // O imagine PNG cu un fir/mănunchi de iarbă
    [Range(0, 10)] public int grassDensity = 4; // Densitatea ierbii

    [Header("Zona de Start")]
    public Transform playerRig;          // trage aici XR Origin
    public Transform[] moveToStart;      // obiectele de la cabană

    [Header("Prefab-uri (opțional)")]
    public GameObject startGatePrefab;
    public GameObject checkpointPrefab;   
    public GameObject finishPrefab;
    public GameObject deadEndBlockerPrefab; 
    public GameObject hazardZonePrefab;     

    public List<Trail> trails = new List<Trail>();
    public List<Vector3> keyPoints = new List<Vector3>(); 

    System.Random rng;
    float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

    void Start() { Generate(); }

    [ContextMenu("Generate")]
    public void Generate()
    {
        // curăț ce s-a generat înainte
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);

        rng = new System.Random(seed == 0 ? System.Environment.TickCount : seed);

        BuildTerrain();

        bool ok = false;
        for (int attempt = 0; attempt < 50 && !ok; attempt++) ok = BuildGraph();
        if (!ok) { Debug.LogWarning("Nu am putut genera o hartă validă. Mărește areaSize sau schimbă seed-ul."); return; }

        if (terrain) 
        { 
            CarveTrails(); 
            PaintTrails(); 
            
            // Adăugăm vegetația și apa DUPĂ ce am vopsit potecile (ca să știm unde NU punem iarbă)
            PlaceWater();
            PlantTrees();
            GrowGrass();
        }
        SpawnProps();
        PlaceStartArea();
    }

    void PlaceStartArea()
    {
        if (!playerRig) return;
        Vector3 offset = Ground(keyPoints[0]) - playerRig.position;
        offset.y += 0.05f;
        playerRig.position += offset;
        if (moveToStart == null) return;
        foreach (var t in moveToStart)
            if (t && t != playerRig) t.position += offset;
    }

    bool BuildGraph()
    {
        trails.Clear();
        if (!PlaceKeyPoints()) return false;
        for (int i = 0; i < 4; i++)
        {
            var t = new Trail { kind = TrailKind.Main };
            t.pts = Wander(keyPoints[i], keyPoints[i + 1]);
            trails.Add(t);
        }
        var mains = trails.FindAll(x => x.kind == TrailKind.Main);
        foreach (var seg in mains)
        {
            int added = 0, tries = 0;
            while (added < branchesPerSegment && tries++ < 25)
                if (TryAddBranch(seg)) added++;
        }
        return true;
    }

    bool PlaceKeyPoints()
    {
        keyPoints.Clear();
        float h = areaSize / 2f - 20f;
        keyPoints.Add(new Vector3(R(-h, -h * 0.5f), 0, R(-h, -h * 0.5f)));
        for (int i = 1; i < 5; i++)
        {
            bool placed = false;
            for (int t = 0; t < 100 && !placed; t++)
            {
                float ang = R(0, Mathf.PI * 2), d = R(70, 110);
                Vector3 p = keyPoints[i - 1] + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * d;
                if (Mathf.Abs(p.x) > h || Mathf.Abs(p.z) > h) continue;
                bool far = true;
                foreach (var q in keyPoints) if (Vector3.Distance(p, q) < 60f) far = false;
                if (far) { keyPoints.Add(p); placed = true; }
            }
            if (!placed) return false;
        }
        return true;
    }

    bool TryAddBranch(Trail seg)
    {
        int n = seg.pts.Count;
        if (n < 12) return false;
        int i = rng.Next(n / 4, n * 3 / 4);
        Vector3 j = seg.pts[i];
        Vector3 fwd = (seg.pts[i + 1] - seg.pts[i - 1]).normalized;
        Vector3 heading = Quaternion.Euler(0, (rng.Next(2) == 0 ? 1 : -1) * R(45, 100), 0) * fwd;
        float len = R(branchLength.x, branchLength.y);
        TrailKind kind = PickKind();
        var b = new Trail { kind = kind };

        if (kind == TrailKind.Shortcut)
        {
            int k = Mathf.Min(i + rng.Next(8, 14), n - 2);
            if (k - i < 6) return false;
            b.pts = Wander(j, seg.pts[k]);
        }
        else if (kind == TrailKind.WrongLoop)
        {
            int k = Mathf.Max(2, i - rng.Next(8, 14));
            Vector3 mid = j + heading * len;
            b.pts = Wander(j, mid);
            var back = Wander(mid, seg.pts[k]);
            back.RemoveAt(0); 
            b.pts.AddRange(back);
        }
        else
        {
            Vector3 end = j + heading * len;
            b.pts = Wander(j, end);
        }

        if (!BranchIsValid(b)) return false;
        trails.Add(b);
        return true;
    }

    TrailKind PickKind()
    {
        float total = wShortcut + wWrongLoop + wDeadEnd + wDanger;
        float r = R(0, total);
        if ((r -= wShortcut) < 0) return TrailKind.Shortcut;
        if ((r -= wWrongLoop) < 0) return TrailKind.WrongLoop;
        if ((r -= wDeadEnd) < 0) return TrailKind.DeadEnd;
        return TrailKind.Danger;
    }

    bool BranchIsValid(Trail b)
    {
        float h = areaSize / 2f;
        for (int a = 0; a < b.pts.Count; a++)
        {
            Vector3 p = b.pts[a];
            if (Mathf.Abs(p.x) > h || Mathf.Abs(p.z) > h) return false;
            bool nearEnd = a < 4 || (b.kind != TrailKind.DeadEnd && b.kind != TrailKind.Danger && a > b.pts.Count - 5);
            if (nearEnd) continue;
            foreach (var t in trails)
                foreach (var q in t.pts)
                    if (Vector3.Distance(p, q) < minTrailGap) return false;
        }
        return true;
    }

    List<Vector3> Wander(Vector3 a, Vector3 b)
    {
        var pts = new List<Vector3> { a };
        Vector3 dir = b - a;
        float total = dir.magnitude;
        dir /= total;
        Vector3 perp = new Vector3(-dir.z, 0, dir.x);
        int n = Mathf.Max(2, Mathf.RoundToInt(total / stepLen));
        float ph = R(0, 100);
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            float off = (Mathf.PerlinNoise(ph + t * 3f, 0.5f) - 0.5f) * 2f * wander * total * Mathf.Sin(t * Mathf.PI);
            pts.Add(a + dir * total * t + perp * off);
        }
        pts.Add(b);
        return pts;
    }

    void BuildTerrain()
    {
        if (!terrain) return;
        var td = terrain.terrainData;
        td.heightmapResolution = 257;
        td.size = new Vector3(terrainSize, maxHeight, terrainSize);
        terrain.transform.position = transform.position - new Vector3(terrainSize / 2f, 0, terrainSize / 2f);
        int r = td.heightmapResolution;
        var hts = new float[r, r];
        float ox = R(0, 1000), oz = R(0, 1000);
        for (int z = 0; z < r; z++)
            for (int x = 0; x < r; x++)
            {
                float wx = x / (float)(r - 1) * terrainSize, wz = z / (float)(r - 1) * terrainSize;
                float n = Mathf.PerlinNoise(ox + wx * noiseScale, oz + wz * noiseScale) * 0.7f
                        + Mathf.PerlinNoise(ox + wx * noiseScale * 3, oz + wz * noiseScale * 3) * 0.3f;
                hts[z, x] = n * 0.6f;
            }
        td.SetHeights(0, 0, hts);
    }

    void CarveTrails()
    {
        var td = terrain.terrainData;
        int r = td.heightmapResolution;
        var hts = td.GetHeights(0, 0, r, r);
        float cell = terrainSize / (r - 1);
        float radius = trailWidth * 2f;
        int rad = Mathf.CeilToInt(radius / cell);
        foreach (var t in trails)
            foreach (var p in t.pts)
            {
                Vector3 w = transform.TransformPoint(p) - terrain.transform.position;
                int cx = Mathf.Clamp(Mathf.RoundToInt(w.x / cell), 0, r - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(w.z / cell), 0, r - 1);
                float target = hts[cz, cx];
                for (int dz = -rad; dz <= rad; dz++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= r || z >= r) continue;
                        float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                        if (d > radius) continue;
                        hts[z, x] = Mathf.Lerp(hts[z, x], target, Mathf.SmoothStep(0, 1, 1 - d / radius));
                    }
            }

        Vector3 sw = transform.TransformPoint(keyPoints[0]) - terrain.transform.position;
        int sx = Mathf.Clamp(Mathf.RoundToInt(sw.x / cell), 0, r - 1);
        int sz = Mathf.Clamp(Mathf.RoundToInt(sw.z / cell), 0, r - 1);
        float flatH = hts[sz, sx];
        float outer = startClearingRadius + 10f;
        int srad = Mathf.CeilToInt(outer / cell);
        for (int dz = -srad; dz <= srad; dz++)
            for (int dx = -srad; dx <= srad; dx++)
            {
                int x = sx + dx, z = sz + dz;
                if (x < 0 || z < 0 || x >= r || z >= r) continue;
                float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                if (d > outer) continue;
                float k = d <= startClearingRadius ? 1f : 1f - (d - startClearingRadius) / 10f;
                hts[z, x] = Mathf.Lerp(hts[z, x], flatH, Mathf.SmoothStep(0, 1, k));
            }
        td.SetHeights(0, 0, hts);
    }

    void PaintTrails()
    {
        var td = terrain.terrainData;
        int ar = td.alphamapResolution, layers = td.alphamapLayers;
        if (layers <= pathLayerIndex) { Debug.LogWarning("Adaugă un Terrain Layer pentru potecă."); return; }
        var a = td.GetAlphamaps(0, 0, ar, ar);

        for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
                for (int l = 0; l < layers; l++) a[z, x, l] = (l == 0) ? 1f : 0f;

        float cell = terrainSize / ar;
        foreach (var t in trails)
            for (int i = 1; i < t.pts.Count; i++)
            {
                Vector3 p0 = transform.TransformPoint(t.pts[i - 1]) - terrain.transform.position;
                Vector3 p1 = transform.TransformPoint(t.pts[i]) - terrain.transform.position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p0, p1) / (cell * 0.5f)));
                for (int s = 0; s <= steps; s++)
                    PaintDisc(a, ar, layers, cell, Vector3.Lerp(p0, p1, s / (float)steps));
            }
        td.SetAlphamaps(0, 0, a);
    }

    void PaintDisc(float[,,] a, int ar, int layers, float cell, Vector3 w)
    {
        int cx = Mathf.RoundToInt(w.x / cell), cz = Mathf.RoundToInt(w.z / cell);
        int rad = Mathf.CeilToInt(trailWidth / cell) + 1;
        for (int dz = -rad; dz <= rad; dz++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= ar || z >= ar) continue;
                float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                float wgt = Mathf.Clamp01((trailWidth - d) / (trailWidth * 0.5f)); 
                if (wgt <= 0f) continue;
                float p = Mathf.Max(a[z, x, pathLayerIndex], wgt);
                float others = 0f;
                for (int l = 0; l < layers; l++) if (l != pathLayerIndex) others += a[z, x, l];
                for (int l = 0; l < layers; l++)
                {
                    if (l == pathLayerIndex) a[z, x, l] = p;
                    else a[z, x, l] = others > 0f ? a[z, x, l] / others * (1f - p) : (l == 0 ? 1f - p : 0f);
                }
            }
    }

    // ---------- 3. APĂ & VEGETAȚIE (NOU) ----------

    void PlaceWater()
    {
        if (!waterPrefab) return;
        
        // Creăm planul de apă exact la înălțimea waterLevel
        Vector3 center = terrain.transform.position + new Vector3(terrainSize / 2f, waterLevel, terrainSize / 2f);
        GameObject water = Instantiate(waterPrefab, center, Quaternion.identity, transform);
        water.name = "Lac";
        
        // Scalăm planul ca să acopere tot terenul (pentru un Plane standard în Unity, scara e / 10)
        // Dacă folosești un plan generat de tine, s-ar putea să ai nevoie de o scară diferită.
        water.transform.localScale = new Vector3(terrainSize / 10f, 1f, terrainSize / 10f);
    }

    void PlantTrees()
    {
        if (treePrefabs == null || treePrefabs.Length == 0) return;
        var td = terrain.terrainData;
        
        // Pregătim prototipurile de copaci
        var protos = new TreePrototype[treePrefabs.Length];
        for (int i = 0; i < treePrefabs.Length; i++) protos[i] = new TreePrototype { prefab = treePrefabs[i] };
        td.treePrototypes = protos;

        // Luăm harta vopsită pentru a nu planta pe potecă
        int ar = td.alphamapResolution;
        float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);

        var instances = new List<TreeInstance>();
        for (int i = 0; i < treeCount; i++)
        {
            float nx = R(0f, 1f); // coordonate relative 0-1
            float nz = R(0f, 1f);
            
            // Verificăm înălțimea: fără copaci sub apă
            float h = td.GetInterpolatedHeight(nx, nz);
            if (h < waterLevel + 1f) continue; 

            // Verificăm alphamap-ul: fără copaci pe potecă (layer-ul de pământ e prea puternic)
            int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);
            if (alpha[az, ax, pathLayerIndex] > 0.1f) continue;

            // Verificăm să nu fie chiar lângă zona de start
            Vector3 worldPos = terrain.transform.position + new Vector3(nx * terrainSize, h, nz * terrainSize);
            if (Vector3.Distance(worldPos, Ground(keyPoints[0])) < startClearingRadius) continue;

            instances.Add(new TreeInstance
            {
                position = new Vector3(nx, 0, nz), // Unity cere coordonate locale (0..1)
                prototypeIndex = rng.Next(treePrefabs.Length),
                widthScale = R(0.7f, 1.3f), // copaci de dimensiuni diferite
                heightScale = R(0.7f, 1.3f),
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        td.SetTreeInstances(instances.ToArray(), true);
    }

    void GrowGrass()
    {
        if (!grassTexture) return;
        var td = terrain.terrainData;

        // Setup iarbă
        td.detailPrototypes = new DetailPrototype[] {
            new DetailPrototype {
                prototypeTexture = grassTexture,
                renderMode = DetailRenderMode.GrassBillboard, // Mereu orientată spre cameră
                healthyColor = Color.white,
                dryColor = new Color(0.8f, 0.9f, 0.5f), // Variație de culoare
                minWidth = 1f, maxWidth = 1.5f,
                minHeight = 0.8f, maxHeight = 1.2f,
                noiseSpread = 0.1f
            }
        };

        int dr = td.detailResolution;
        int[,] details = new int[dr, dr];
        
        // Luăm harta vopsită pentru a nu pune iarbă pe potecă
        int ar = td.alphamapResolution;
        float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);

        for (int z = 0; z < dr; z++)
        {
            for (int x = 0; x < dr; x++)
            {
                float nx = x / (float)(dr - 1);
                float nz = z / (float)(dr - 1);
                
                // Fără iarbă sub apă
                float h = td.GetInterpolatedHeight(nx, nz);
                if (h < waterLevel + 0.5f) continue; 

                // Fără iarbă pe potecă (mapăm coordonatele ierbii la coordonatele texturii)
                int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);
                if (alpha[az, ax, pathLayerIndex] > 0.2f) continue;

                // Plantăm iarbă random pentru a părea naturală
                if (rng.NextDouble() > 0.3)
                    details[z, x] = rng.Next(1, grassDensity + 1);
            }
        }
        td.SetDetailLayer(0, 0, 0, details);
    }

    // ---------- 4. OBIECTE ----------

    Vector3 Ground(Vector3 local)
    {
        Vector3 w = transform.TransformPoint(local);
        if (terrain) w.y = terrain.SampleHeight(w) + terrain.transform.position.y;
        return w;
    }

    void Spawn(GameObject prefab, Vector3 local, Vector3 forward, string name)
    {
        if (!prefab) return;
        var go = Instantiate(prefab, Ground(local), Quaternion.LookRotation(forward == Vector3.zero ? Vector3.forward : forward), transform);
        go.name = name;
    }

    void SpawnProps()
    {
        Spawn(startGatePrefab, keyPoints[0], (keyPoints[1] - keyPoints[0]).normalized, "StartGate");
        for (int i = 1; i <= 3; i++) Spawn(checkpointPrefab, keyPoints[i], Vector3.forward, "CP" + i);
        Spawn(finishPrefab, keyPoints[4], Vector3.forward, "Finish");

        foreach (var t in trails)
        {
            if (t.kind != TrailKind.DeadEnd && t.kind != TrailKind.Danger) continue;
            Vector3 end = t.pts[t.pts.Count - 1];
            Vector3 dir = (end - t.pts[t.pts.Count - 2]).normalized;
            Spawn(t.kind == TrailKind.DeadEnd ? deadEndBlockerPrefab : hazardZonePrefab, end, dir, t.kind.ToString());
        }
    }

    void OnDrawGizmos()
    {
        foreach (var t in trails)
        {
            switch (t.kind)
            {
                case TrailKind.Main: Gizmos.color = Color.green; break;
                case TrailKind.Shortcut: Gizmos.color = Color.cyan; break;
                case TrailKind.WrongLoop: Gizmos.color = Color.yellow; break;
                case TrailKind.DeadEnd: Gizmos.color = Color.gray; break;
                default: Gizmos.color = Color.red; break;
            }
            for (int i = 1; i < t.pts.Count; i++)
                Gizmos.DrawLine(transform.TransformPoint(t.pts[i - 1]) + Vector3.up, transform.TransformPoint(t.pts[i]) + Vector3.up);
        }
        Gizmos.color = Color.white;
        foreach (var k in keyPoints) Gizmos.DrawSphere(transform.TransformPoint(k) + Vector3.up, 3f);
    }
}