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

    [Header("Stâncă la înălțime")]
    public int rockLayerIndex = 2;       // indexul layer-ului de textură "stâncă" (al 3-lea layer pe Terrain)
    public float rockStartHeight = 24f;  // înălțimea de la care începe să apară stânca
    public float rockBlendRange = 6f;    // pe câți metri face trecerea lină din iarbă în stâncă

    [Header("Margini hartă (Doar Stânci 3D & Barieră)")]
    public GameObject[] borderRockPrefabs;        // Pune aici prefab-urile 3D de stânci mari
    public float borderRockSpacing = 10f;         // Distanța între stânci (mai mică = mai dese, fără goluri)
    public Vector2 borderRockScale = new Vector2(4f, 7f);
    public float borderRockSinkDepth = 2f;        // Cât de adânc intră baza stâncii în pământ (ca să nu plutească)
    public bool addInvisibleBoundaryWalls = true; // Barieră invizibilă de siguranță în spatele/pe linia stâncilor

    [Header("Lacuri")]
    public GameObject waterPrefab;       // Un Plane/Quad cu material de apă
    public float waterLevel = 6f;        // Nivelul apei
    [Range(1, 3)] public int lakeCount = 2;             // Câte lacuri mari pe toată harta (1 sau 2)
    public Vector2 lakeRadius = new Vector2(22f, 35f);  // Raza lacului în metri (diametru 44-70m)
    public float lakeDepth = 4.5f;                      // Adâncimea maximă sub nivelul apei
    public float lakeShoreBlend = 14f;                  // Lățimea malului lin (unde cresc sălciile)

    [Header("Vegetatie")]
    public GameObject[] treePrefabs;          // Pune aici DOAR copacii normali (fără salcie)
    public int treeCount = 600;               // Copaci împrăștiați în pădure
    public int trailSideTrees = 250;          // Copaci extra așezați mai aproape de poteci
    public float minTreeDistFromTrail = 2.6f; // Distanța minimă față de centrul potecii
    public float maxTrailSideDist = 6.5f;     // Cât de aproape de potecă stau copacii de margine

    public GameObject grassPrefab;            // Prefab-ul 3D de iarbă (Grass_Patch)
    public int grassCount = 15000;            // Câte tufe de iarbă 3D pe hartă
    public Vector2 grassScale = new Vector2(1.5f, 2.5f); // Mărimea tufelor
    public float grassYOffset = -0.15f;       // Valoare negativă ca să stea lipită de sol

    private Mesh grassMesh;
    private Material grassMat;
    private List<Matrix4x4[]> grassBatches = new List<Matrix4x4[]>();

    [Header("Tufisuri")]
    public GameObject[] bushPrefabs;              // Pune aici 1 sau mai multe prefab-uri de tufe
    public int bushCount = 350;                   // Câte tufe să apară pe hartă
    public Vector2 bushScale = new Vector2(0.8f, 1.4f);
    public float bushYOffset = -0.1f;             // Le coboară ușor în sol ca să nu plutească pe dealuri
    public float minBushDistFromTrail = 2.0f;     // Distanța minimă față de centrul potecii

    [Header("Salcii (langa apa)")]
    public GameObject willowPrefab;                  // Pune aici prefab-ul de salcie
    public int maxWillows = 20;                      // Numărul maxim de salcii pe hartă
    public float willowMaxHeightAboveWater = 2.5f;   // Cât de sus pe mal pot apărea
    [Range(0f, 1f)] public float willowChanceNearWater = 0.85f;

    [Header("Zona de Start")]
    public Transform playerRig;          // trage aici XR Origin
    public Transform[] moveToStart;      // obiectele de la cabană

    public Transform firstCheckpointCabin;
    [Min(0f)] public float firstCheckpointDistance = 4f;

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
            CarveLakes();
            CarveTrails();
            PaintTrails();

            PlaceWater();
            PlantTrees();
            GrowGrass();
            PlantBushes();
            PlaceBorderRocks();
        }
        BuildBoundaryWalls();
        PlaceStartArea();
        SpawnProps();
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

        float minLandNorm = Mathf.Clamp((waterLevel + 2.8f) / maxHeight, 0.05f, 0.5f);

        for (int z = 0; z < r; z++)
            for (int x = 0; x < r; x++)
            {
                float wx = x / (float)(r - 1) * terrainSize, wz = z / (float)(r - 1) * terrainSize;
                float n = Mathf.PerlinNoise(ox + wx * noiseScale, oz + wz * noiseScale) * 0.7f
                        + Mathf.PerlinNoise(ox + wx * noiseScale * 3, oz + wz * noiseScale * 3) * 0.3f;
                hts[z, x] = Mathf.Lerp(minLandNorm, 0.65f, n);
            }
        td.SetHeights(0, 0, hts);
    }

    void CarveLakes()
    {
        var td = terrain.terrainData;
        int r = td.heightmapResolution;
        var hts = td.GetHeights(0, 0, r, r);
        float cell = terrainSize / (r - 1);

        float waterNorm = waterLevel / maxHeight;
        float bottomNorm = Mathf.Max(0.005f, (waterLevel - lakeDepth) / maxHeight);

        var placedCenters = new List<Vector3>();
        var placedRadii = new List<float>();

        for (int l = 0; l < lakeCount; l++)
        {
            float rad = R(lakeRadius.x, lakeRadius.y);
            float margin = rad + lakeShoreBlend + 8f;
            float limit = Mathf.Max(10f, areaSize / 2f - margin);

            bool found = false;
            Vector3 chosenLocal = Vector3.zero;

            for (int attempt = 0; attempt < 150; attempt++)
            {
                Vector3 candidate = new Vector3(R(-limit, limit), 0f, R(-limit, limit));

                if (Vector3.Distance(candidate, keyPoints[0]) < startClearingRadius + rad + 10f)
                    continue;

                bool nearKey = false;
                for (int k = 1; k < keyPoints.Count; k++)
                    if (Vector3.Distance(candidate, keyPoints[k]) < rad + 12f) { nearKey = true; break; }
                if (nearKey) continue;

                bool nearLake = false;
                for (int j = 0; j < placedCenters.Count; j++)
                    if (Vector3.Distance(candidate, placedCenters[j]) < rad + placedRadii[j] + 20f) { nearLake = true; break; }
                if (nearLake) continue;

                float trailPad = attempt < 100 ? 6f : 1.5f;
                bool nearTrail = false;
                foreach (var t in trails)
                {
                    foreach (var p in t.pts)
                    {
                        if (Vector3.Distance(candidate, p) < rad + trailWidth + trailPad)
                        {
                            nearTrail = true;
                            break;
                        }
                    }
                    if (nearTrail) break;
                }
                if (nearTrail) continue;

                chosenLocal = candidate;
                found = true;
                break;
            }

            if (!found) continue;

            placedCenters.Add(chosenLocal);
            placedRadii.Add(rad);

            float ph1 = R(0, Mathf.PI * 2f);
            float ph2 = R(0, Mathf.PI * 2f);
            float ph3 = R(0, Mathf.PI * 2f);

            Vector3 w = transform.TransformPoint(chosenLocal) - terrain.transform.position;
            int cx = Mathf.Clamp(Mathf.RoundToInt(w.x / cell), 0, r - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt(w.z / cell), 0, r - 1);
            float maxExtent = rad * 1.35f + lakeShoreBlend;
            int cellRad = Mathf.CeilToInt(maxExtent / cell);

            for (int dz = -cellRad; dz <= cellRad; dz++)
            {
                for (int dx = -cellRad; dx <= cellRad; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= r || z >= r) continue;

                    float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                    float ang = Mathf.Atan2(dz, dx);

                    float shape = 1f
                        + 0.16f * Mathf.Sin(ang * 2f + ph1)
                        + 0.11f * Mathf.Cos(ang * 3f + ph2)
                        + 0.06f * Mathf.Sin(ang * 5f + ph3);

                    float effRad = rad * shape;
                    float outerRad = effRad + lakeShoreBlend;
                    if (d > outerRad) continue;

                    if (d <= effRad)
                    {
                        float t = d / effRad;
                        float lakeH = Mathf.Lerp(bottomNorm, waterNorm, Mathf.SmoothStep(0f, 1f, t * t));
                        hts[z, x] = Mathf.Min(hts[z, x], lakeH);
                    }
                    else
                    {
                        float t = (d - effRad) / lakeShoreBlend;
                        float shoreH = Mathf.Lerp(waterNorm, hts[z, x], Mathf.SmoothStep(0f, 1f, t));
                        hts[z, x] = Mathf.Min(hts[z, x], shoreH);
                    }
                }
            }
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
        float minTrailH = (waterLevel + 1.2f) / maxHeight;

        foreach (var t in trails)
            foreach (var p in t.pts)
            {
                Vector3 w = transform.TransformPoint(p) - terrain.transform.position;
                int cx = Mathf.Clamp(Mathf.RoundToInt(w.x / cell), 0, r - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(w.z / cell), 0, r - 1);
                float target = Mathf.Max(hts[cz, cx], minTrailH);
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
        float flatH = Mathf.Max(hts[sz, sx], minTrailH);
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

    // Pereți invizibili de siguranță pe linia stâncilor ca jucătorul să nu poată trece printre ele sau cădea din hartă
    void BuildBoundaryWalls()
    {
        if (!addInvisibleBoundaryWalls) return;

        GameObject holder = new GameObject("MapBoundaries");
        holder.transform.SetParent(transform, false);

        float halfPlayable = areaSize / 2f + 2.5f;
        float wallLength = (halfPlayable + 6f) * 2f;
        float wallHeight = Mathf.Max(100f, maxHeight * 2f);
        float wallThickness = 4f;

        CreateBoundaryWall(holder.transform, "Wall_North", new Vector3(0, wallHeight * 0.4f, halfPlayable), new Vector3(wallLength, wallHeight, wallThickness));
        CreateBoundaryWall(holder.transform, "Wall_South", new Vector3(0, wallHeight * 0.4f, -halfPlayable), new Vector3(wallLength, wallHeight, wallThickness));
        CreateBoundaryWall(holder.transform, "Wall_East", new Vector3(halfPlayable, wallHeight * 0.4f, 0), new Vector3(wallThickness, wallHeight, wallLength));
        CreateBoundaryWall(holder.transform, "Wall_West", new Vector3(-halfPlayable, wallHeight * 0.4f, 0), new Vector3(wallThickness, wallHeight, wallLength));
    }

    void CreateBoundaryWall(Transform parent, string wallName, Vector3 localPos, Vector3 size)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPos;
        var col = wall.AddComponent<BoxCollider>();
        col.size = size;
    }

    // Plasează stâncile 3D pe marginile hărții, îngropate bine în sol și cu collider solid
    void PlaceBorderRocks()
    {
        if (borderRockPrefabs == null || borderRockPrefabs.Length == 0) return;

        GameObject holder = new GameObject("BorderRocks");
        holder.transform.SetParent(transform, false);

        float half = areaSize / 2f + 4.5f;
        float step = Mathf.Max(3f, borderRockSpacing);

        for (float p = -half; p <= half; p += step)
        {
            SpawnBorderRock(holder.transform, new Vector3(p, 0, half));
            SpawnBorderRock(holder.transform, new Vector3(p, 0, -half));
            SpawnBorderRock(holder.transform, new Vector3(half, 0, p));
            SpawnBorderRock(holder.transform, new Vector3(-half, 0, p));
        }
    }

    void SpawnBorderRock(Transform parent, Vector3 localPos)
    {
        GameObject prefab = borderRockPrefabs[rng.Next(borderRockPrefabs.Length)];
        if (!prefab) return;

        Vector3 jitter = new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f));
        Vector3 worldPos = Ground(localPos + jitter);

        var rock = Instantiate(prefab, worldPos, Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f)), parent);
        float s = R(borderRockScale.x, borderRockScale.y);
        rock.transform.localScale = new Vector3(s, s * R(1.1f, 1.5f), s);

        // Garantăm că fiecare stâncă are un Collider solid (nu Trigger), ca să nu poată trece jucătorul prin ea
        var colliders = rock.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            var mfs = rock.GetComponentsInChildren<MeshFilter>();
            if (mfs.Length > 0)
            {
                foreach (var mf in mfs)
                    if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>();
            }
            else
            {
                rock.AddComponent<BoxCollider>();
            }
        }
        else
        {
            foreach (var col in colliders) col.isTrigger = false;
        }

        // Coborâm stânca sub cel mai jos punct al terenului de sub ea ca să nu plutească niciodată pe pante
        var renderers = rock.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float minGroundY = SampleMinGroundHeight(b);
            float deltaY = (minGroundY - borderRockSinkDepth) - b.min.y;
            rock.transform.position += new Vector3(0f, deltaY, 0f);
        }
        else
        {
            rock.transform.position -= new Vector3(0f, borderRockSinkDepth, 0f);
        }
    }

    float SampleMinGroundHeight(Bounds b)
    {
        if (!terrain) return b.min.y;
        float ty = terrain.transform.position.y;
        float minH = terrain.SampleHeight(new Vector3(b.center.x, 0f, b.center.z)) + ty;
        Vector3[] corners = {
            new Vector3(b.min.x, 0f, b.min.z),
            new Vector3(b.min.x, 0f, b.max.z),
            new Vector3(b.max.x, 0f, b.min.z),
            new Vector3(b.max.x, 0f, b.max.z)
        };
        foreach (var c in corners)
        {
            float h = terrain.SampleHeight(c) + ty;
            if (h < minH) minH = h;
        }
        return minH;
    }

    void PaintTrails()
    {
        var td = terrain.terrainData;
        int ar = td.alphamapResolution, layers = td.alphamapLayers;
        if (layers <= pathLayerIndex) { Debug.LogWarning("Adaugă un Terrain Layer pentru potecă."); return; }

        bool hasRock = rockLayerIndex >= 0 && rockLayerIndex < layers && rockLayerIndex != pathLayerIndex;
        if (!hasRock && rockLayerIndex >= layers)
            Debug.LogWarning("Adaugă un Terrain Layer pentru stâncă (index " + rockLayerIndex + ") pe Terrain.");

        var a = td.GetAlphamaps(0, 0, ar, ar);

        for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                for (int l = 0; l < layers; l++) a[z, x, l] = 0f;

                if (hasRock)
                {
                    float nx = x / (float)(ar - 1);
                    float nz = z / (float)(ar - 1);
                    float h = td.GetInterpolatedHeight(nx, nz);
                    float rockW = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((h - rockStartHeight) / Mathf.Max(0.01f, rockBlendRange)));
                    a[z, x, 0] = 1f - rockW;
                    a[z, x, rockLayerIndex] = rockW;
                }
                else
                {
                    a[z, x, 0] = 1f;
                }
            }

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

    // ---------- 3. APĂ & VEGETAȚIE ----------

    void PlaceWater()
    {
        if (!waterPrefab) return;

        Vector3 center = terrain.transform.position + new Vector3(terrainSize / 2f, waterLevel, terrainSize / 2f);
        GameObject water = Instantiate(waterPrefab, center, Quaternion.identity, transform);
        water.name = "Lac";
        water.transform.localScale = new Vector3(terrainSize / 10f, 1f, terrainSize / 10f);
    }

    // Distanța exactă (2D) până la cea mai apropiată potecă, ca să nu apară niciun copac pe drum
    float DistanceToNearestTrail(Vector3 localPos)
    {
        Vector2 p = new Vector2(localPos.x, localPos.z);
        float bestSq = float.MaxValue;

        foreach (var t in trails)
        {
            for (int i = 1; i < t.pts.Count; i++)
            {
                Vector2 a = new Vector2(t.pts[i - 1].x, t.pts[i - 1].z);
                Vector2 b = new Vector2(t.pts[i].x, t.pts[i].z);
                float dSq = DistSqPointToSegment2D(p, a, b);
                if (dSq < bestSq) bestSq = dSq;
            }
        }
        return Mathf.Sqrt(bestSq);
    }

    float DistSqPointToSegment2D(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 proj = a + ab * t;
        return (p - proj).sqrMagnitude;
    }

    void PlantTrees()
    {
        var td = terrain.terrainData;

        var protoList = new List<TreePrototype>();
        if (treePrefabs != null)
        {
            for (int i = 0; i < treePrefabs.Length; i++)
                if (treePrefabs[i] != null)
                    protoList.Add(new TreePrototype { prefab = treePrefabs[i] });
        }

        int normalTreeCount = protoList.Count;
        int willowProtoIndex = -1;
        if (willowPrefab != null)
        {
            willowProtoIndex = protoList.Count;
            protoList.Add(new TreePrototype { prefab = willowPrefab });
        }

        if (protoList.Count == 0) return;
        td.treePrototypes = protoList.ToArray();

        int ar = td.alphamapResolution;
        float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);

        var instances = new List<TreeInstance>();
        int willowsPlaced = 0;

        float safeTrailDist = Mathf.Max(minTreeDistFromTrail, trailWidth + 1.0f);
        float maxBorderCoord = areaSize / 2f + 1.5f;
        Vector3 startGround = Ground(keyPoints[0]);

        // ETAPA 1: Copaci mai aproape de poteci (de o parte și de alta, niciodată pe potecă)
        if (normalTreeCount > 0 && trails.Count > 0 && trailSideTrees > 0)
        {
            int attempts = trailSideTrees * 3;
            int placedSide = 0;

            for (int i = 0; i < attempts && placedSide < trailSideTrees; i++)
            {
                Trail tr = trails[rng.Next(trails.Count)];
                if (tr.pts.Count < 2) continue;

                int segIdx = rng.Next(tr.pts.Count - 1);
                Vector3 a = tr.pts[segIdx];
                Vector3 b = tr.pts[segIdx + 1];
                Vector3 dir = (b - a).normalized;
                Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

                float side = rng.Next(2) == 0 ? 1f : -1f;
                float dist = R(safeTrailDist, Mathf.Max(safeTrailDist + 0.5f, maxTrailSideDist));
                Vector3 localPos = Vector3.Lerp(a, b, R(0f, 1f)) + perp * (side * dist);

                if (Mathf.Abs(localPos.x) > maxBorderCoord || Mathf.Abs(localPos.z) > maxBorderCoord) continue;
                if (DistanceToNearestTrail(localPos) < safeTrailDist) continue;

                Vector3 worldOffset = transform.TransformPoint(localPos) - terrain.transform.position;
                float nx = worldOffset.x / terrainSize;
                float nz = worldOffset.z / terrainSize;
                if (nx <= 0.01f || nx >= 0.99f || nz <= 0.01f || nz >= 0.99f) continue;

                float h = td.GetInterpolatedHeight(nx, nz);
                if (h < waterLevel + 1f) continue;

                int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);
                if (alpha[az, ax, pathLayerIndex] > 0.05f) continue;

                Vector3 worldPos = terrain.transform.position + new Vector3(nx * terrainSize, h, nz * terrainSize);
                if (Vector3.Distance(worldPos, startGround) < startClearingRadius) continue;

                instances.Add(new TreeInstance
                {
                    position = new Vector3(nx, 0, nz),
                    prototypeIndex = rng.Next(normalTreeCount),
                    widthScale = R(0.75f, 1.25f),
                    heightScale = R(0.75f, 1.25f),
                    color = Color.white,
                    lightmapColor = Color.white
                });
                placedSide++;
            }
        }

        // ETAPA 2: Restul pădurii + salcii lângă lacuri
        for (int i = 0; i < treeCount; i++)
        {
            float nx = R(0f, 1f);
            float nz = R(0f, 1f);

            float h = td.GetInterpolatedHeight(nx, nz);
            float heightAboveWater = h - waterLevel;
            if (heightAboveWater < 0.3f) continue;

            Vector3 worldPos = terrain.transform.position + new Vector3(nx * terrainSize, h, nz * terrainSize);
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            if (Mathf.Abs(localPos.x) > maxBorderCoord || Mathf.Abs(localPos.z) > maxBorderCoord) continue;
            if (DistanceToNearestTrail(localPos) < safeTrailDist) continue;

            int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);
            if (alpha[az, ax, pathLayerIndex] > 0.05f) continue;

            if (Vector3.Distance(worldPos, startGround) < startClearingRadius) continue;

            int chosenIndex;
            bool isNearWater = heightAboveWater <= willowMaxHeightAboveWater;

            if (willowProtoIndex != -1 && isNearWater && willowsPlaced < maxWillows && R(0f, 1f) < willowChanceNearWater)
            {
                chosenIndex = willowProtoIndex;
                willowsPlaced++;
            }
            else
            {
                if (normalTreeCount == 0 || heightAboveWater < 1f) continue;
                chosenIndex = rng.Next(normalTreeCount);
            }

            instances.Add(new TreeInstance
            {
                position = new Vector3(nx, 0, nz),
                prototypeIndex = chosenIndex,
                widthScale = R(0.7f, 1.3f),
                heightScale = R(0.7f, 1.3f),
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        td.SetTreeInstances(instances.ToArray(), true);
    }

    void GrowGrass()
    {
        grassBatches.Clear();
        grassMesh = null;
        grassMat = null;

        if (!grassPrefab || grassCount <= 0) return;

        var mf = grassPrefab.GetComponentInChildren<MeshFilter>();
        var mr = mf ? mf.GetComponent<MeshRenderer>() : null;
        if (!mf || !mr || !mf.sharedMesh || !mr.sharedMaterial)
        {
            Debug.LogWarning("Grass Prefab nu are MeshFilter / MeshRenderer valid!");
            return;
        }

        grassMesh = mf.sharedMesh;
        grassMat = mr.sharedMaterial;
        grassMat.enableInstancing = true;

        var td = terrain.terrainData;
        td.detailPrototypes = new DetailPrototype[0];

        int ar = td.alphamapResolution;
        int layers = td.alphamapLayers;
        float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);
        bool hasRock = rockLayerIndex >= 0 && rockLayerIndex < layers;

        var matrices = new List<Matrix4x4>(grassCount);
        int attempts = grassCount * 2;
        float maxBorderCoord = areaSize / 2f + 1f;

        for (int i = 0; i < attempts && matrices.Count < grassCount; i++)
        {
            float nx = R(0.02f, 0.98f);
            float nz = R(0.02f, 0.98f);

            float h = td.GetInterpolatedHeight(nx, nz);
            if (h < waterLevel + 0.6f) continue;

            Vector3 worldPos = terrain.transform.position + new Vector3(nx * terrainSize, h, nz * terrainSize);
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            if (Mathf.Abs(localPos.x) > maxBorderCoord || Mathf.Abs(localPos.z) > maxBorderCoord) continue;

            int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);

            if (alpha[az, ax, pathLayerIndex] > 0.15f) continue;
            if (hasRock && alpha[az, ax, rockLayerIndex] > 0.5f) continue;

            Vector3 normal = td.GetInterpolatedNormal(nx, nz);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, R(0f, 360f), 0f);
            float s = R(grassScale.x, grassScale.y);

            worldPos += normal * (grassYOffset * s);
            matrices.Add(Matrix4x4.TRS(worldPos, rot, new Vector3(s, s, s)));
        }

        for (int i = 0; i < matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, matrices.Count - i);
            grassBatches.Add(matrices.GetRange(i, count).ToArray());
        }

        Debug.Log($"Iarbă 3D generată: {matrices.Count} tufe în {grassBatches.Count} batch-uri.");
    }

    void PlantBushes()
    {
        if (bushPrefabs == null || bushPrefabs.Length == 0 || bushCount <= 0) return;

        var validPrefabs = new List<GameObject>();
        for (int i = 0; i < bushPrefabs.Length; i++)
            if (bushPrefabs[i] != null) validPrefabs.Add(bushPrefabs[i]);

        if (validPrefabs.Count == 0) return;

        var td = terrain.terrainData;
        int ar = td.alphamapResolution;
        int layers = td.alphamapLayers;
        float[,,] alpha = td.GetAlphamaps(0, 0, ar, ar);
        bool hasRock = rockLayerIndex >= 0 && rockLayerIndex < layers;

        GameObject holder = new GameObject("BushHolder");
        holder.transform.SetParent(transform, false);

        float safeTrailDist = Mathf.Max(minBushDistFromTrail, trailWidth + 0.6f);
        float maxBorderCoord = areaSize / 2f + 1f;
        Vector3 startGround = Ground(keyPoints[0]);

        int attempts = bushCount * 3;
        int placed = 0;

        for (int i = 0; i < attempts && placed < bushCount; i++)
        {
            float nx = R(0.02f, 0.98f);
            float nz = R(0.02f, 0.98f);

            float h = td.GetInterpolatedHeight(nx, nz);
            if (h < waterLevel + 0.6f) continue;

            Vector3 worldPos = terrain.transform.position + new Vector3(nx * terrainSize, h, nz * terrainSize);
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            if (Mathf.Abs(localPos.x) > maxBorderCoord || Mathf.Abs(localPos.z) > maxBorderCoord) continue;
            if (Vector3.Distance(worldPos, startGround) < startClearingRadius) continue;
            if (DistanceToNearestTrail(localPos) < safeTrailDist) continue;

            int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (ar - 1)), 0, ar - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(nz * (ar - 1)), 0, ar - 1);
            if (alpha[az, ax, pathLayerIndex] > 0.1f) continue;
            if (hasRock && alpha[az, ax, rockLayerIndex] > 0.5f) continue;

            Vector3 normal = td.GetInterpolatedNormal(nx, nz);
            Quaternion slopeRot = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), 0.5f);
            Quaternion rot = slopeRot * Quaternion.Euler(0f, R(0f, 360f), 0f);

            float s = R(bushScale.x, bushScale.y);
            worldPos += normal * (bushYOffset * s);

            GameObject prefab = validPrefabs[rng.Next(validPrefabs.Count)];
            var bush = Instantiate(prefab, worldPos, rot, holder.transform);
            bush.transform.localScale = new Vector3(s, s, s);
            placed++;
        }
    }

    void Update()
    {
        if (!grassMesh || !grassMat || grassBatches.Count == 0) return;

        for (int i = 0; i < grassBatches.Count; i++)
        {
            Graphics.DrawMeshInstanced(
                grassMesh,
                0,
                grassMat,
                grassBatches[i],
                grassBatches[i].Length,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                true
            );
        }
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
        Vector3 firstCheckpoint = keyPoints[1];
        Vector3 firstCheckpointForward = Vector3.forward;
        if (firstCheckpointCabin)
        {
            Vector3 cabinPosition = firstCheckpointCabin.position;
            Vector3 towardStart = Ground(keyPoints[0]) - cabinPosition;
            towardStart.y = 0f;
            if (towardStart.sqrMagnitude > 0.001f)
                firstCheckpointForward = towardStart.normalized;

            Vector3 worldPosition = cabinPosition + firstCheckpointForward * firstCheckpointDistance;
            firstCheckpoint = transform.InverseTransformPoint(worldPosition);
        }
        Spawn(checkpointPrefab, firstCheckpoint, firstCheckpointForward, "CP1");
        for (int i = 2; i <= 3; i++) Spawn(checkpointPrefab, keyPoints[i], Vector3.forward, "CP" + i);
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