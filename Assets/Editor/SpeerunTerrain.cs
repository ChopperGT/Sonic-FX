using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// "SpeerunMadeByCOCO": Green-Hill style level on a procedurally sculpted Terrain.
// A winding main path (turns, hills, dives, chasms) + shortcut branches that cut hairpins (dive & climb) + a high road.
// Run via Tools/Speerun/Build Level. Writes bot routes to Temp/speerun_route_*.txt.
public static class SpeerunTerrain
{
    const string P = "Assets/BumperEngineV1/ObjectPrefabs/";
    const string SD = "Assets/Structure/décore/";
    const string MatDir = "Assets/Level/SpeerunMadeByCOCO_Mats";
    const float Step = 3f, G = 90f;

    class Route { public string name; public float halfW; public List<Vector3> p = new List<Vector3>(); public List<Vector2> dir = new List<Vector2>(); }

    static Transform root;
    static Terrain terrain;
    static Route main;
    static readonly List<Route> branches = new List<Route>();
    static readonly StringBuilder log = new StringBuilder();
    static System.Random rng;

    // ---------- layout ----------
    // (length, turn in degrees (+ = right), slope dy/ds). Turns sit on flat / gentle sections, crests open onto plateaus.
    static readonly float[,] Sections = {
        {300, 0, 0}, {160, 0, -0.45f}, {140, 0, -0.05f}, {200, 0, 0.25f}, {250, 0, 0},
        {300, 70, -0.12f}, {180, 0, -0.45f}, {150, 0, -0.05f}, {250, -110, -0.05f}, {220, 0, 0.25f}, {250, 0, 0},
        {400, 150, -0.12f}, {200, 0, -0.45f}, {200, 0, -0.05f}, {250, -60, 0.15f}, {250, 0, 0},
        {300, 90, -0.12f}, {200, 0, -0.45f}, {150, 0, -0.05f}, {220, 0, 0.25f}, {250, 0, 0},
        {350, -70, -0.15f}, {250, 0, -0.30f}, {250, 0, -0.05f}
    };

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

    static Route BuildMain()
    {
        var r = new Route { name = "main", halfW = 26 };
        var slope = new List<float>();
        float psi = 0, x = 0, z = 0;
        for (int l = 0; l < Sections.GetLength(0); l++)
        {
            int n = Mathf.RoundToInt(Sections[l, 0] / Step);
            float dpsi = Sections[l, 1] * Mathf.Deg2Rad / n;
            for (int i = 0; i < n; i++)
            {
                r.p.Add(new Vector3(x, 0, z));
                r.dir.Add(new Vector2(Mathf.Sin(psi), Mathf.Cos(psi)));
                slope.Add(Sections[l, 2]);
                psi += dpsi;
                x += Mathf.Sin(psi) * Step; z += Mathf.Cos(psi) * Step;
            }
        }
        r.p.Add(new Vector3(x, 0, z)); r.dir.Add(r.dir[r.dir.Count - 1]); slope.Add(slope[slope.Count - 1]);

        // slope profile low-passed (+-30 samples) so hills and dives are rounded
        int N = r.p.Count;
        var sm = new float[N];
        for (int i = 0; i < N; i++)
        {
            float sum = 0; int c = 0;
            for (int j = Mathf.Max(0, i - 30); j <= Mathf.Min(N - 1, i + 30); j++) { sum += slope[j]; c++; }
            sm[i] = sum / c;
        }
        float y = 0, minY = 0;
        var ys = new float[N];
        for (int i = 0; i < N; i++) { ys[i] = y; minY = Mathf.Min(minY, y); y += sm[i] * Step; }
        float shift = 110 - minY;
        for (int i = 0; i < N; i++) r.p[i] = new Vector3(r.p[i].x, ys[i] + shift, r.p[i].z);
        return r;
    }

    static Vector2 Right(Vector2 d) => new Vector2(d.y, -d.x);

    // cubic Bezier shortcut between main indices a..b; lat bulges sideways, bump raises (+) or dips (-) the middle
    static Route Branch(string name, int a, int b, float lat, float bump, float halfW)
    {
        var r = new Route { name = name, halfW = halfW };
        Vector3 p0 = main.p[a], p3 = main.p[b];
        Vector2 t0 = main.dir[a], t3 = main.dir[b];
        float chord = new Vector2(p3.x - p0.x, p3.z - p0.z).magnitude;
        float k = 0.33f * chord;
        Vector2 n0 = Right(t0) * lat, n3 = Right(t3) * lat;
        Vector2 a0 = new Vector2(p0.x, p0.z), a3 = new Vector2(p3.x, p3.z);
        Vector2 c1 = a0 + t0 * k + n0, c2 = a3 - t3 * k + n3;
        int n = Mathf.Max(8, Mathf.RoundToInt(chord * 1.15f / Step));
        Vector2 prev = a0;
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n, w = 1 - u;
            Vector2 q = w * w * w * a0 + 3 * w * w * u * c1 + 3 * w * u * u * c2 + u * u * u * a3;
            float yy = Mathf.Lerp(p0.y, p3.y, u) + bump * Mathf.Sin(Mathf.PI * u);
            r.p.Add(new Vector3(q.x, yy, q.y));
            r.dir.Add(i == 0 ? t0 : (q - prev).normalized);
            prev = q;
        }
        r.dir[0] = t0; r.dir[r.dir.Count - 1] = t3;
        return r;
    }

    // ---------- terrain ----------
    static void BuildTerrain(float[] gaps)
    {
        var all = new List<Route> { main };
        all.AddRange(branches);
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f, maxY = 0;
        foreach (var r in all) foreach (var q in r.p)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minZ = Mathf.Min(minZ, q.z); maxZ = Mathf.Max(maxZ, q.z); maxY = Mathf.Max(maxY, q.y);
        }
        const float margin = 260f;
        float E = Mathf.Max(maxX - minX, maxZ - minZ) + 2 * margin;
        float ox = (minX + maxX) / 2 - E / 2, oz = (minZ + maxZ) / 2 - E / 2;
        int res = E > 2600 ? 2049 : 1025;
        float cs = E / (res - 1);
        log.AppendLine($"terrain E={E:F0} res={res} cell={cs:F2} maxY={maxY:F0}");

        int cells = res * res;
        var minDp = new float[cells]; var phNear = new float[cells]; var sNear = new float[cells]; var isMain = new bool[cells];
        var wsum = new float[cells]; var hsum = new float[cells];
        for (int i = 0; i < cells; i++) minDp[i] = 1e9f;
        const float R = 240f, sigma = 85f;
        int rc = Mathf.CeilToInt(R / cs);
        foreach (var r in all)
        {
            // stamp every SEGMENT (not just its points): the floor height is interpolated continuously along the path,
            // otherwise the nearest-point y makes a staircase on slopes (bumpy floor, camera jitter, lost speed)
            for (int pi = 0; pi < r.p.Count - 1; pi++)
            {
                Vector3 A = r.p[pi], B = r.p[pi + 1];
                Vector2 a2 = new Vector2(A.x, A.z), ab = new Vector2(B.x - A.x, B.z - A.z);
                float abl = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                int cx = Mathf.RoundToInt((A.x - ox) / cs), cz = Mathf.RoundToInt((A.z - oz) / cs);
                for (int zz = Mathf.Max(0, cz - rc); zz <= Mathf.Min(res - 1, cz + rc); zz++)
                    for (int xx = Mathf.Max(0, cx - rc); xx <= Mathf.Min(res - 1, cx + rc); xx++)
                    {
                        Vector2 c = new Vector2(ox + xx * cs, oz + zz * cs);
                        float t = Mathf.Clamp01(Vector2.Dot(c - a2, ab) / abl);
                        Vector2 q = a2 + ab * t;
                        float d2 = (c - q).sqrMagnitude;
                        if (d2 > R * R) continue;
                        int id = zz * res + xx;
                        float yq = Mathf.Lerp(A.y, B.y, t);
                        float d = Mathf.Sqrt(d2) - r.halfW;
                        if (d < minDp[id]) { minDp[id] = d; phNear[id] = yq; isMain[id] = r == main; sNear[id] = (pi + t) * Step; }
                        float w = Mathf.Exp(-d2 / (sigma * sigma));
                        wsum[id] += w; hsum[id] += w * yq;
                    }
            }
        }

        var h = new float[res, res];
        float hMax = 0;
        for (int zz = 0; zz < res; zz++)
            for (int xx = 0; xx < res; xx++)
            {
                int id = zz * res + xx;
                float x = ox + xx * cs, z = oz + zz * cs, v;
                if (minDp[id] > 1e8f) v = float.NaN;
                else
                {
                    float d = Mathf.Max(minDp[id], 0);
                    float wavg = wsum[id] > 1e-6f ? hsum[id] / wsum[id] : phNear[id];
                    float baseH = Mathf.Lerp(phNear[id], wavg, Smooth(d / 70f));
                    float bank = 22f * Smooth(d / 50f) + 45f * Smooth((d - 50f) / 40f);
                    float extra = Mathf.Clamp((d - 90f) * 0.15f, 0, 140f);
                    float noise = (Mathf.PerlinNoise(x * 0.008f + 13, z * 0.008f + 7) - 0.5f) * 2 * 30f * Smooth((d - 30f) / 120f);
                    v = baseH + bank + extra + noise;
                    if (isMain[id])
                    {
                        // chasms: sheer drop on entry, gentle exit ramp
                        float s = sNear[id], dip = 0;
                        for (int g = 0; g < gaps.Length / 2; g++)
                        {
                            float g0 = gaps[g * 2], g1 = gaps[g * 2 + 1];
                            if (s >= g0 && s <= g1) dip = 1;
                            else if (s > g1 && s < g1 + 150f) dip = 1 - (s - g1) / 150f;
                        }
                        v -= 34f * dip * (1 - Smooth(d / 12f));
                    }
                }
                h[zz, xx] = v;
            }
        // unstamped cells (far corners): copy the nearest stamped height (two raster sweeps), then blur them
        for (int zz = 0; zz < res; zz++) for (int xx = 0; xx < res; xx++)
            if (float.IsNaN(h[zz, xx])) { if (xx > 0 && !float.IsNaN(h[zz, xx - 1])) h[zz, xx] = h[zz, xx - 1]; else if (zz > 0 && !float.IsNaN(h[zz - 1, xx])) h[zz, xx] = h[zz - 1, xx]; }
        for (int zz = res - 1; zz >= 0; zz--) for (int xx = res - 1; xx >= 0; xx--)
            if (float.IsNaN(h[zz, xx])) { if (xx < res - 1 && !float.IsNaN(h[zz, xx + 1])) h[zz, xx] = h[zz, xx + 1]; else if (zz < res - 1 && !float.IsNaN(h[zz + 1, xx])) h[zz, xx] = h[zz + 1, xx]; }
        for (int it = 0; it < 30; it++)
            for (int zz = 1; zz < res - 1; zz++) for (int xx = 1; xx < res - 1; xx++)
                if (minDp[zz * res + xx] > 1e8f) h[zz, xx] = (h[zz, xx - 1] + h[zz, xx + 1] + h[zz - 1, xx] + h[zz + 1, xx]) * 0.25f;
        for (int zz = 0; zz < res; zz++) for (int xx = 0; xx < res; xx++) { if (float.IsNaN(h[zz, xx])) h[zz, xx] = maxY; hMax = Mathf.Max(hMax, h[zz, xx]); }
        float H = Mathf.Ceil((hMax + 100) / 100f) * 100f;
        var n = new float[res, res];
        for (int zz = 0; zz < res; zz++) for (int xx = 0; xx < res; xx++) n[zz, xx] = Mathf.Clamp01(h[zz, xx] / H);

        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Level", "SpeerunMadeByCOCO_Mats");
        string tdPath = MatDir + "/SpeerunTerrainData.asset";
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath);
        if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, tdPath); }
        td.heightmapResolution = res;
        td.size = new Vector3(E, H, E);
        td.SetHeights(0, 0, n);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "SpeerunTerrain";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(ox, 0, oz);
        terrain = go.GetComponent<Terrain>();
        terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");
        terrain.drawInstanced = false;
        terrain.heightmapPixelError = 1;
    }

    // ---------- placement helpers ----------
    static float Ground(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;

    static Vector3 At(Route r, float s, float lat, float up = 0)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(s / Step), 0, r.p.Count - 1);
        Vector2 rt = Right(r.dir[i]);
        float x = r.p[i].x + rt.x * lat, z = r.p[i].z + rt.y * lat;
        return new Vector3(x, Ground(x, z) + up, z);
    }

    static Vector3 Fwd(Route r, float s)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(s / Step), 0, r.p.Count - 1);
        int j = Mathf.Min(i + 3, r.p.Count - 1);
        int k = Mathf.Max(i - 3, 0);
        Vector3 d = r.p[j] - r.p[k];
        return d.normalized;
    }

    static GameObject Place(string path, Vector3 pos, Quaternion rot)
    {
        var a = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (a == null) { log.AppendLine("MISSING " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(a, root);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    static void RingsAlong(Route r, float s0, float s1, float lat, float spacing, float up = 3f)
    {
        for (float s = s0; s <= s1; s += spacing) Place(P + "Ring.prefab", At(r, s, lat, up), Quaternion.identity);
    }

    static void RingArc(Route r, float s0, float s1, float lat, float peak)
    {
        int n = Mathf.Max(3, Mathf.RoundToInt((s1 - s0) / 10f));
        for (int i = 0; i <= n; i++)
        {
            float u = i / (float)n;
            Place(P + "Ring.prefab", At(r, Mathf.Lerp(s0, s1, u), lat, 4 + peak * Mathf.Sin(Mathf.PI * u)), Quaternion.identity);
        }
    }

    static void Pad(Route r, float s, float speed = 200f)
    {
        Vector3 f = Fwd(r, s);
        var g = Place(P + "Speed Pad.prefab", At(r, s, 0, 0.3f), Quaternion.LookRotation(f, Vector3.up));
        if (g) g.GetComponent<SpeedPadData>().Speed = speed;
    }

    static bool NoEnemies => EditorPrefs.GetBool("SpeerunNoEnemies", false);

    static void Bug(Route r, float s, float lat)
    {
        if (NoEnemies) return;
        Place("Assets/Ennemy/[Enemy] - Motobug Variant.prefab", At(r, s, lat, 2f), Quaternion.LookRotation(-Fwd(r, s), Vector3.up));
    }

    static void Crab(Route r, float s, float lat)
    {
        if (NoEnemies) return;
        Place("Assets/Ennemy/Crab_Ennemi Variant.prefab", At(r, s, lat, 2f), Quaternion.LookRotation(-Fwd(r, s), Vector3.up));
    }

    static void Checkpoint(Route r, float s)
    {
        Place(P + "CheckPoint.prefab", At(r, s, r.halfW - 4f, 0), Quaternion.LookRotation(Fwd(r, s), Vector3.up));
    }

    // spring whose ballistic arc lands on `target` (yaw taken from pos->target), enemies + rings along the arc
    static void SpringArc(Vector3 pos, Vector3 target, float tilt, int bugs)
    {
        Vector3 d = target - pos;
        Vector2 hd = new Vector2(d.x, d.z);
        float D = hd.magnitude, th = tilt * Mathf.Deg2Rad;
        float den = Mathf.Sin(th) * Mathf.Sin(th) * (D / Mathf.Tan(th) - d.y);
        if (den <= 0.01f) { log.AppendLine("SpringArc impossible at " + pos); return; }
        float f = Mathf.Sqrt(0.5f * G * D * D / den);
        Quaternion yaw = Quaternion.LookRotation(new Vector3(hd.x, 0, hd.y), Vector3.up);
        var g = Place(P + "Spring.prefab", pos, yaw * Quaternion.Euler(tilt, 0, 0));
        if (g == null) return;
        g.GetComponent<Spring_Proprieties>().SpringForce = f;
        float T = D / (f * Mathf.Sin(th));
        log.AppendLine($"Spring@{pos.x:F0},{pos.z:F0} force {f:F0} flight {T:F1}s");
        Vector3 hdir = new Vector3(hd.x, 0, hd.y).normalized;
        for (int i = 1; i <= bugs; i++)
        {
            float t = T * i / (bugs + 1f);
            Vector3 q = pos + hdir * (f * Mathf.Sin(th) * t) + Vector3.up * (f * Mathf.Cos(th) * t - 0.5f * G * t * t);
            if (!NoEnemies) Place("Assets/Ennemy/[Enemy] - Motobug Variant.prefab", q + Vector3.down * 3f, Quaternion.identity);
            for (int k = -1; k <= 1; k++)
            {
                float t2 = t + k * 0.12f;
                Vector3 q2 = pos + hdir * (f * Mathf.Sin(th) * t2) + Vector3.up * (f * Mathf.Cos(th) * t2 - 0.5f * G * t2 * t2);
                Place(P + "Ring.prefab", q2 + Vector3.up * 8f, Quaternion.identity);
            }
        }
    }

    static void Scatter()
    {
        string[] trees = { "Tree/palm_A", "Tree/palm_B", "Tree/palm_C" };
        string[] small = { "sun_flower", "violet", "small_rock" };
        var all = new List<Route> { main };
        all.AddRange(branches);
        foreach (var r in all)
        {
            float len = (r.p.Count - 1) * Step;
            for (float s = 20; s < len - 20; s += r == main ? 26 : 34)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rng.NextDouble() < 0.45)
                    {
                        float lat = side * (r.halfW + 50 + (float)rng.NextDouble() * 70);
                        Place(SD + trees[rng.Next(trees.Length)] + ".prefab", At(r, s + (float)rng.NextDouble() * 20, lat), Quaternion.Euler(0, rng.Next(360), 0));
                    }
                    if (rng.NextDouble() < 0.6)
                    {
                        float lat = side * (r.halfW + 4 + (float)rng.NextDouble() * 26);
                        Place(SD + small[rng.Next(small.Length)] + ".prefab", At(r, s + (float)rng.NextDouble() * 20, lat), Quaternion.Euler(0, rng.Next(360), 0));
                    }
                }
                if (r == main && rng.NextDouble() < 0.06)
                    Place(SD + "totem.prefab", At(r, s, (rng.NextDouble() < 0.5 ? -1 : 1) * (r.halfW + 12)), Quaternion.Euler(0, rng.Next(360), 0));
            }
        }
    }

    static void WriteRoute(string name, List<Vector3> pts)
    {
        var sb = new StringBuilder();
        foreach (var q in pts) sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F1} {1:F1} {2:F1}", q.x, q.y, q.z));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/speerun_route_" + name + ".txt"), sb.ToString());
    }

    // ---------- build ----------
    [MenuItem("Tools/Speerun/Build Level")]
    public static void Build()
    {
        var old = GameObject.Find("SpeerunLevel");
        if (old) Object.DestroyImmediate(old);
        root = new GameObject("SpeerunLevel").transform;
        log.Clear();
        rng = new System.Random(1234);
        branches.Clear();

        main = BuildMain();
        int N = main.p.Count;
        float L = (N - 1) * Step;
        log.AppendLine($"main length {L:F0}, start y {main.p[0].y:F0}, end y {main.p[N - 1].y:F0}");

        // chasms sit on the plateaus after hill crests (start, end); exit ramp follows
        float[] gs = { 880, 935, 2230, 2285, 3530, 3585, 4650, 4705 };

        AddBranches();
        BuildTerrain(gs);

        // sky + light like Act 1-1
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Skyboxes/Skybox/Cubemaps/Materials/daytime.mat");
        if (sky) RenderSettings.skybox = sky;
        var sun = GameObject.Find("Directional Light");
        if (sun)
        {
            sun.transform.rotation = Quaternion.Euler(50, 245, 0);
            var lt = sun.GetComponent<Light>(); lt.color = new Color(1f, 0.957f, 0.839f); lt.shadows = LightShadows.Soft;
        }

        PlaceContent(L, gs);
        Scatter();
        Walls();

        // bot routes: main, and main with each branch spliced in
        WriteRoute("main", main.p);
        foreach (var b in branches)
        {
            int a = 0, e = 0;
            float bestA = 1e9f, bestE = 1e9f;
            for (int i = 0; i < N; i++)
            {
                float da = (main.p[i] - b.p[0]).sqrMagnitude, de = (main.p[i] - b.p[b.p.Count - 1]).sqrMagnitude;
                if (da < bestA) { bestA = da; a = i; }
                if (de < bestE) { bestE = de; e = i; }
            }
            var route = new List<Vector3>();
            for (int i = 0; i < a; i++) route.Add(main.p[i]);
            route.AddRange(b.p);
            for (int i = e + 1; i < N; i++) route.Add(main.p[i]);
            WriteRoute(b.name, route);
            log.AppendLine($"{b.name}: {b.p.Count * Step:F0} vs main {(e - a) * Step:F0}");
        }

        // overlap check: path parts far apart in s but close in space
        float minGap = 1e9f; int mi = 0, mj = 0;
        for (int i = 0; i < N; i += 4) for (int j = i + 100; j < N; j += 4)
        {
            float d = new Vector2(main.p[i].x - main.p[j].x, main.p[i].z - main.p[j].z).magnitude;
            if (d < minGap) { minGap = d; mi = i; mj = j; }
        }
        log.AppendLine($"closest non-adjacent main parts: {minGap:F0} units (s={mi * Step:F0} vs {mj * Step:F0})");

        // floor smoothness along the main line: max |ground - path y| and max second difference (bumps) outside chasms
        float maxDev = 0, maxBump = 0; int bumpAt = 0;
        for (int i = 1; i < N - 1; i++)
        {
            float g0 = Ground(main.p[i - 1].x, main.p[i - 1].z), g1 = Ground(main.p[i].x, main.p[i].z), g2 = Ground(main.p[i + 1].x, main.p[i + 1].z);
            bool inGap = false;
            for (int g = 0; g < gs.Length / 2; g++) if (i * Step > gs[g * 2] - 12 && i * Step < gs[g * 2 + 1] + 12) inGap = true;
            if (inGap) continue;
            maxDev = Mathf.Max(maxDev, Mathf.Abs(g1 - main.p[i].y));
            float b = Mathf.Abs(g0 - 2 * g1 + g2);
            if (b > maxBump) { maxBump = b; bumpAt = i; }
        }
        log.AppendLine($"floor: max deviation from path {maxDev:F2}, max bump {maxBump:F2} at s={bumpAt * Step}");

        // player start
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == "Ground" || go.name == "GoalRing") Object.DestroyImmediate(go);
            else if (go.name == "PO_Mania")
                go.transform.position += At(main, 12, 0, 2f) - go.transform.position;
        }
        Debug.Log("SpeerunTerrain\n" + log);
    }

    // shortcuts: dive-and-climb chords across turns, and a high road (indices are main samples, s = idx*3)
    static void AddBranches()
    {
        branches.Add(Branch("B0_cutSweeper", 330, 470, 0, -20, 20));
        branches.Add(Branch("B1_cutLeft", 540, 663, 0, -25, 20));
        branches.Add(Branch("B2_cutHairpin", 780, 953, 0, -32, 20));
        branches.Add(Branch("B3_highRoad", 643, 793, 110, 40, 18));
        branches.Add(Branch("B4_cutLeft2", 1047, 1170, 0, -22, 20));
        branches.Add(Branch("B5_cutRight", 1213, 1353, 0, -25, 20));
        branches.Add(Branch("B6_cutLeft3", 1587, 1743, 0, -22, 20));
    }

    // invisible side walls along every route, WallOff from its centre line; a wall post is dropped when it lies inside
    // another route's corridor (branch junctions, hairpins) so shortcuts stay open
    const float WallOff = 36f, WallClear = 32f, WallH = 45f;

    static void Walls()
    {
        var old = root.Find("SpeerunWalls");
        if (old) Object.DestroyImmediate(old.gameObject);
        var wr = new GameObject("SpeerunWalls").transform;
        wr.SetParent(root, false);
        var all = new List<Route> { main };
        all.AddRange(branches);
        int count = 0;
        foreach (var r in all)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3? prev = null;
                for (int i = 0; i < r.p.Count; i += 4)
                {
                    Vector2 rt = Right(r.dir[i]) * side * (r.halfW + WallOff);
                    float x = r.p[i].x + rt.x, z = r.p[i].z + rt.y;
                    bool open = false;
                    foreach (var o in all)
                    {
                        float lim = (o.halfW + WallClear) * (o.halfW + WallClear);
                        foreach (var q in o.p) if ((q.x - x) * (q.x - x) + (q.z - z) * (q.z - z) < lim) { open = true; break; }
                        if (open) break;
                    }
                    if (open) { prev = null; continue; }
                    var cur = new Vector3(x, Ground(x, z), z);
                    if (prev.HasValue)
                    {
                        Vector3 a = prev.Value, d = cur - a;
                        var w = new GameObject("Wall");
                        w.transform.SetParent(wr, false);
                        w.transform.SetPositionAndRotation((a + cur) / 2 + Vector3.up * (WallH / 2 - 10), Quaternion.LookRotation(new Vector3(d.x, 0, d.z), Vector3.up));
                        w.AddComponent<BoxCollider>().size = new Vector3(2f, WallH, new Vector2(d.x, d.z).magnitude + 2f);
                        count++;
                    }
                    prev = cur;
                }
            }
        log.AppendLine($"walls: {count} segments");
    }

    [MenuItem("Tools/Speerun/Rebuild Walls Only")]
    public static void RebuildWalls()
    {
        var lvl = GameObject.Find("SpeerunLevel");
        if (!lvl) { Debug.LogError("SpeerunLevel not found"); return; }
        root = lvl.transform;
        terrain = root.Find("SpeerunTerrain").GetComponent<Terrain>();
        log.Clear();
        branches.Clear();
        main = BuildMain();
        AddBranches();
        Walls();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lvl.scene);
        Debug.Log("SpeerunTerrain\n" + log);
    }

    static void PlaceContent(float L, float[] gs)
    {
        foreach (float s in new float[] { 1000, 2350, 3620, 4760 }) Checkpoint(main, s);

        // dives -> speed pads
        for (int g = 0; g < gs.Length / 2; g++) Pad(main, gs[g * 2] - 90);
        Pad(main, 5190);

        // ring trails and arcs
        RingsAlong(main, 60, 260, 0, 24);
        RingsAlong(main, 330, 560, 8, 24);
        RingsAlong(main, 1080, 1300, -10, 26);
        RingsAlong(main, 1390, 1600, 10, 26);
        RingsAlong(main, 2450, 2750, 0, 26);
        RingsAlong(main, 2830, 3000, 0, 26);
        RingsAlong(main, 3720, 3950, 0, 26);
        RingsAlong(main, 4050, 4250, 0, 26);
        RingsAlong(main, 4850, 5100, 0, 26);
        RingsAlong(main, 5200, 5400, 0, 26);
        for (int g = 0; g < gs.Length / 2; g++) RingArc(main, gs[g * 2] - 30, gs[g * 2 + 1] + 40, 0, 16);

        // main path enemies (Motobugs) and crabs
        foreach (float s in new float[] { 140, 620, 760, 1150, 1450, 1750, 2000, 2100, 2500, 2650, 2950, 3300, 3450, 3850, 4100, 4300, 4500, 4950, 5300, 5550 })
            Bug(main, s, ((int)(s / 7) % 3 - 1) * 9);
        foreach (float s in new float[] { 820, 2200, 3100, 4600 }) Crab(main, s, 0);

        // shortcuts: rings + enemies inside
        foreach (var b in branches)
        {
            float len = (b.p.Count - 1) * Step;
            RingsAlong(b, 25, len - 25, 0, 20);
            if (!b.name.StartsWith("B3")) Bug(b, len * 0.5f, 0);
        }

        // high road: spring from the main path up onto the ledge, enemies on the ledge
        var hr = branches[3];
        float hl = (hr.p.Count - 1) * Step;
        SpringArc(At(main, 1920, 8, 1f), At(hr, hl * 0.4f, 0, 6f), 62f, 1);
        Bug(hr, hl * 0.6f, 0);
        Bug(hr, hl * 0.8f, 0);

        // chasm spring: launches over the chasm with two Motobugs to chain (slower players simply drop in and climb out)
        for (int g = 0; g < gs.Length / 2; g++)
        {
            float s0 = gs[g * 2] - 70, s1 = gs[g * 2 + 1] + 160;
            SpringArc(At(main, s0, -14, 1f), At(main, s1, -14, 4f), 40f, 2);
        }

        // goal
        Place(P + "GoalRing.prefab", At(main, L - 40, 0, 9f), Quaternion.LookRotation(Fwd(main, L - 40), Vector3.up));
    }
}
