using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
using static NeoRingKit;

// Construction du niveau "Neo Ring Zone - Secteur 07" : menu Tools/Neo Ring, une entree par etape.
// Le projet tourne en Built-in RP (pas d'asset URP assigne) : materiaux Standard + emission, pas de Volume/Bloom.
public static class NeoRingBuilder
{
    const string Tex = Root + "Textures/", Mats = Root + "Materials/", ScenePath = "Assets/Level/NeoRing.unity";
    const string Prefabs = "Assets/BumperEngineV1/ObjectPrefabs/";
    static readonly Color Cyan = Hex("19e3ff"), Magenta = Hex("ff2bd6"), Yellow = Hex("ffd60a"), Violet = Hex("7b5cff"), Fog = Hex("120a2e");
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    static Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + n + ".png");
    static Transform Section(string name) { var root = GameObject.Find("NeoRing") ?? Group("NeoRing"); var old = root.transform.Find(name); if (old) Object.DestroyImmediate(old.gameObject); return Group(name, root.transform).transform; }

    // ---------------------------------------------------------------- 1. Setup
    [MenuItem("Tools/Neo Ring/1 - Setup (textures, materiaux, scene)")]
    public static void Setup() { ImportTextures(); Materials(); Scene(); }

    public static void ImportTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex.TrimEnd('/') }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid); var ti = (TextureImporter)AssetImporter.GetAtPath(p); string n = System.IO.Path.GetFileNameWithoutExtension(p);
            ti.wrapMode = n == "sky_gradient" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.maxTextureSize = 2048; ti.anisoLevel = 4; ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = n.StartsWith("holo");
            ti.SaveAndReimport();
        }
    }

    static Material M(string name, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, Mats + name + ".mat"); }
        m.shader = Shader.Find(shader); return m;
    }
    static Material Lit(string name, string albedo, string emissive, float intensity, Color? tint = null)
    {
        var m = M(name, "Standard"); m.mainTexture = T(albedo); m.color = tint ?? Color.white; m.SetFloat("_Metallic", .55f); m.SetFloat("_Glossiness", .4f);
        if (emissive != null) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; m.SetTexture("_EmissionMap", T(emissive)); m.SetColor("_EmissionColor", Color.white * intensity); }
        return m;
    }
    static Material Additive(string name, string tex) { var m = M(name, "Legacy Shaders/Particles/Additive"); m.mainTexture = T(tex); return m; }

    public static void Materials()
    {
        Lit("M_FloorPanels", "floor_panels_albedo", "floor_panels_emissive", 1.8f);
        Lit("M_WallPanels", "wall_panels_albedo", "wall_panels_emissive", 1.8f);
        Lit("M_WallCircuit", "wall_circuit_albedo", "wall_circuit_emissive", 1.2f);
        Lit("M_Hazard", "hazard_stripes", null, 0).mainTextureScale = new Vector2(1, .5f); // texture 4:1 -> 1 repetition = 2 m x 1 m via les UV du Box
        // floor_grate n'a pas d'alpha (carreaux pleins) : opaque, pas de Cutout.
        var grate = Lit("M_Grate", "floor_grate_albedo", null, 0);
        grate.SetFloat("_Mode", 0); grate.DisableKeyword("_ALPHATEST_ON"); grate.renderQueue = -1; grate.SetOverrideTag("RenderType", "");
        foreach (var (n, c) in new[] { ("Cyan", Cyan), ("Magenta", Magenta), ("Yellow", Yellow) })
        {
            var m = Lit("M_Neon_" + n, "neon_strip_" + n.ToLower(), "neon_strip_" + n.ToLower(), 2f, Color.black);
            m.SetFloat("_Metallic", 0); m.SetFloat("_Glossiness", .9f); m.SetColor("_EmissionColor", c * 2f);
        }
        Additive("M_BoostPad", "boost_pad"); Additive("M_Holo", "holo_billboard"); Additive("M_HoloRing", "holo_ring");
        var sky = M("M_Sky", "Skybox/Panoramic"); sky.mainTexture = T("sky_gradient"); sky.SetFloat("_Mapping", 1); sky.SetFloat("_ImageType", 0); sky.SetFloat("_Exposure", 1f);
        // Anneaux recolores cyan : copie du materiau d'origine.
        var ringSrc = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Ring.prefab").GetComponentInChildren<Renderer>().sharedMaterial;
        var ring = AssetDatabase.LoadAssetAtPath<Material>(Mats + "M_RingCyan.mat");
        if (!ring) { ring = new Material(ringSrc); AssetDatabase.CreateAsset(ring, Mats + "M_RingCyan.mat"); }
        ring.SetColor("_DifuseColor", Cyan); ring.SetColor("_RimColor", Color.white); ring.SetColor("_CenterColor", Cyan * .6f);
        AssetDatabase.SaveAssets();
    }

    static void Scene()
    {
        bool dirtyOpen = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
        if (dirtyOpen) Debug.LogWarning("NeoRing : une scene ouverte a des modifications, creation en additif.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, dirtyOpen ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        RenderSettings.skybox = Mat("M_Sky");
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Hex("1a1535");
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = Fog; RenderSettings.fogDensity = .0035f;

        var sun = Group("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = Hex("b0a8ff"); sun.intensity = .7f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45, -35, 0);

        var player = Prefab("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab", new Vector3(0, 1, 0), Quaternion.identity, null);
        var lpc = player.GetComponentInChildren<LevelProgressControl>(true);
        lpc.IdealTimeSeconds = 60; lpc.MedalTimesSeconds = new[] { 60f, 70f, 85f, 110f };
        lpc.GetComponent<HurtControl>().FallDeathHeight = -100; // le moteur tue sous y=10 par defaut ; la ville basse descend sous 0

        Group("NeoRing");
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        Debug.Log("NeoRing : scene creee " + ScenePath);
    }

    // ---------------------------------------------------------------- Briques communes
    static GameObject BoostPad(Vector3 pos, Quaternion rot, Transform parent)
    {
        var pad = Prefab(Prefabs + "Speed Pad.prefab", pos, rot, parent);
        var q = Quad("BoostVisual", pos + rot * new Vector3(0, 1.08f, 0), rot * Quaternion.Euler(90, 0, 0), new Vector2(4.5f, 6.5f), Mat("M_BoostPad"), pad.transform);
        q.AddComponent<NeonPulse>().Speed = 6f;
        return pad;
    }
    static GameObject Ring(Vector3 pos, Transform parent)
    {
        var r = Prefab(Prefabs + "Ring.prefab", pos, Quaternion.identity, parent);
        var cyan = Mat("M_RingCyan");
        foreach (var rend in r.GetComponentsInChildren<Renderer>(true)) rend.sharedMaterial = cyan;
        return r;
    }
    static void RingLine(Vector3 from, Vector3 to, int count, Transform parent) { for (int i = 0; i < count; i++) Ring(Vector3.Lerp(from, to, count == 1 ? 0 : i / (count - 1f)), parent); }
    static GameObject Holo(Vector3 pos, Quaternion rot, Vector2 size, Transform parent)
    {
        var q = Quad("Holo", pos, rot, size, Mat("M_Holo"), parent);
        var p = q.AddComponent<NeonPulse>(); p.Flicker = true; p.MinScale = .98f; p.MaxScale = 1.02f; p.Speed = 1.5f;
        return q;
    }
    // Tube neon sans collider le long d'un axe.
    static GameObject Neon(string name, Vector3 center, Vector3 size, string color, Transform parent, Quaternion? rot = null) => Box(name, center, size, rot ?? Quaternion.identity, Mat("M_Neon_" + color), parent, false);

    // ---------------------------------------------------------------- Section 1 : depart sur le toit
    // Course le long de +Z. Joueur en (0,1,0). Toit de z=-10 a z=80, 14 m de large.
    [MenuItem("Tools/Neo Ring/2 - Section 1 : Depart (toit)")]
    public static void Section1()
    {
        var s = Section("S1_Depart");
        Box("Roof", new Vector3(0, -.5f, 35), new Vector3(14, 1, 90), Quaternion.identity, Mat("M_FloorPanels"), s);
        Box("Tower", new Vector3(0, -31, 35), new Vector3(18, 60, 94), Quaternion.identity, Mat("M_WallCircuit"), s);
        foreach (float x in new[] { -7.25f, 7.25f })
        {
            Box("Parapet", new Vector3(x, .6f, 35), new Vector3(.5f, 1.2f, 90), Quaternion.identity, Mat("M_WallPanels"), s);
            Neon("NeonEdge", new Vector3(x, 1.28f, 35), new Vector3(.16f, .16f, 90), "Cyan", s);
        }
        Box("BackWall", new Vector3(0, 2, -10.25f), new Vector3(14.5f, 4, .5f), Quaternion.identity, Mat("M_WallPanels"), s);
        Box("EdgeStripes", new Vector3(0, .005f, 79.5f), new Vector3(14, .01f, 1), Quaternion.identity, Mat("M_Hazard"), s, false);

        BoostPad(new Vector3(0, 0, 20), Quaternion.identity, s);
        BoostPad(new Vector3(0, 0, 58), Quaternion.identity, s);
        RingLine(new Vector3(0, 1.6f, 30), new Vector3(0, 1.6f, 50), 11, s);

        foreach (float x in new[] { -16f, 16f })
            Holo(new Vector3(x, 8, 45), Quaternion.Euler(0, x < 0 ? -90 : 90, 0), new Vector2(10, 5), s);

        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
        Debug.Log("NeoRing : section 1 construite");
    }

    // ---------------------------------------------------------------- Section 2 : autoroute de neon
    // Chaine : droite 60 m, virage droite 25deg, droite 70 m (voie haute en caillebotis), virage gauche 25deg, droite 60 m.
    const float RoadW = 14, S2A = 60, S2B = 70, S2C = 60, TurnAngle = 25, TurnInner = 50;
    static readonly Vector3 S2Start = new Vector3(0, -2, 88); // saut de 8 m depuis le toit (fin z=80, y=0)

    // Rejoue la geometrie sans rien construire : fin de la section 2 = debut de la section 3.
    public static void S2End(out Vector3 pos, out Quaternion rot)
    {
        pos = S2Start; rot = Quaternion.identity;
        pos += rot * Vector3.forward * S2A; Arc(ref pos, ref rot, true, TurnAngle, TurnInner + RoadW * .5f);
        pos += rot * Vector3.forward * S2B; Arc(ref pos, ref rot, false, TurnAngle, TurnInner + RoadW * .5f);
        pos += rot * Vector3.forward * S2C;
    }
    static void Arc(ref Vector3 pos, ref Quaternion rot, bool right, float angle, float radius)
    {
        float sign = right ? 1 : -1; Vector3 c = pos + rot * Vector3.right * sign * radius;
        var d = Quaternion.AngleAxis(sign * angle, Vector3.up); pos = c + d * (pos - c); rot = d * rot;
    }

    // Troncon droit : sol (2 m d'epaisseur), murs bas wall_panels et tubes cyan. start = centre de la voie au niveau du sol.
    static void Straight(Transform s, Vector3 start, Quaternion rot, float length, float width = RoadW, bool walls = true)
    {
        Box("Road", start + rot * new Vector3(0, -1, length / 2), new Vector3(width, 2, length), rot, Mat("M_FloorPanels"), s);
        if (!walls) return;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (width / 2 + .25f);
            Box("Wall", start + rot * new Vector3(x, 1.25f, length / 2), new Vector3(.5f, 2.5f, length), rot, Mat("M_WallPanels"), s);
            Neon("NeonEdge", start + rot * new Vector3(x, 2.58f, length / 2), new Vector3(.16f, .16f, length), "Cyan", s, rot);
        }
    }

    static SonicFX.Structures.SonicBankedTurn Turn(Transform s, ref Vector3 pos, ref Quaternion rot, bool right)
    {
        var go = Prefab("Assets/Structure/VirageReleve/Virage_Releve_GreenHill.prefab", pos, rot, s);
        var t = go.GetComponent<SonicFX.Structures.SonicBankedTurn>();
        t.direction = right ? SonicFX.Structures.SonicBankedTurn.TurnDirection.Droite : SonicFX.Structures.SonicBankedTurn.TurnDirection.Gauche;
        t.angle = TurnAngle; t.innerRadius = TurnInner; t.flatWidth = RoadW; t.rampWidth = 8; t.wallHeight = 7; t.baseDepth = 2;
        t.fillInterior = false; t.smoothSides = true; t.sideSmoothing = .15f; t.uvSize = 2;
        go.GetComponent<MeshRenderer>().sharedMaterial = Mat("M_FloorPanels");
        go.transform.position = pos - rot * t.EntryPosition; // pivot = bord interieur de l'entree ; on cale le centre de la voie sur pos
        t.Rebuild();
        // Anneaux en arc au milieu de la voie
        for (int i = 0; i < 7; i++) Ring(go.transform.TransformPoint(t.Point(TurnAngle * Mathf.Deg2Rad * i / 6f, TurnInner + RoadW * .5f, 1.6f)), s);
        // Muret interieur en 8 segments (le generateur ne fait que le mur exterieur) ; le bord interieur est un vide sinon.
        float span = TurnAngle * Mathf.Deg2Rad, rIn = TurnInner - .25f, chord = 2 * rIn * Mathf.Sin(span / 16) + .3f;
        for (int k = 0; k < 8; k++)
        {
            float th = span * (k + .5f) / 8;
            var tangent = go.transform.TransformDirection(new Vector3(t.Sign * rIn * Mathf.Sin(th), 0, rIn * Mathf.Cos(th)));
            var wr = Quaternion.LookRotation(tangent);
            Box("InnerWall", go.transform.TransformPoint(t.Point(th, rIn, 1.25f)), new Vector3(.5f, 2.5f, chord), wr, Mat("M_WallPanels"), s);
            Neon("InnerNeon", go.transform.TransformPoint(t.Point(th, rIn, 2.58f)), new Vector3(.16f, .16f, chord), "Cyan", s, wr);
        }
        Vector3 exit = go.transform.TransformPoint(t.ExitPosition);
        Arc(ref pos, ref rot, right, TurnAngle, TurnInner + RoadW * .5f);
        if ((exit - pos).magnitude > .05f) Debug.LogWarning("NeoRing : ecart sortie de virage " + (exit - pos).magnitude);
        return t;
    }

    [MenuItem("Tools/Neo Ring/3 - Section 2 : Autoroute")]
    public static void Section2()
    {
        var s = Section("S2_Autoroute");
        Vector3 pos = S2Start; Quaternion rot = Quaternion.identity;
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        // A : 60 m, checkpoint, pad, anneaux
        Straight(s, pos, rot, S2A);
        Prefab(Prefabs + "CheckPoint.prefab", L(5.5f, 0, 6), rot, s);
        BoostPad(L(0, 0, 14), rot, s);
        RingLine(L(0, 1.6f, 26), L(0, 1.6f, 46), 8, s);
        pos += rot * Vector3.forward * S2A;

        Turn(s, ref pos, ref rot, true);

        // B : 70 m, voie haute en caillebotis a gauche (+6 m), rampe d'acces, pads
        Straight(s, pos, rot, S2B);
        BoostPad(L(2, 0, 15), rot, s);
        RingLine(L(3, 1.6f, 30), L(3, 1.6f, 54), 8, s);
        float rampLen = 24, h = 6; float tilt = -Mathf.Atan2(h, rampLen) * Mathf.Rad2Deg;
        var rampRot = rot * Quaternion.Euler(tilt, 0, 0);
        Box("Ramp", L(-4, h / 2 - .25f * Mathf.Cos(tilt * Mathf.Deg2Rad), rampLen / 2), new Vector3(4, .5f, Mathf.Sqrt(rampLen * rampLen + h * h)), rampRot, Mat("M_Grate"), s);
        float cwStart = rampLen, cwEnd = S2B - 12;
        Box("Catwalk", L(-4, h - .15f, (cwStart + cwEnd) / 2), new Vector3(4, .3f, cwEnd - cwStart), rot, Mat("M_Grate"), s);
        Box("CatwalkEdge", L(-4, h + .005f, cwEnd - .5f), new Vector3(4, .01f, 1), rot, Mat("M_Hazard"), s, false);
        Neon("CatwalkNeon", L(-6.1f, h + .08f, (cwStart + cwEnd) / 2), new Vector3(.12f, .12f, cwEnd - cwStart), "Yellow", s, rot);
        for (float z = cwStart + 6; z < cwEnd; z += 12) Box("Arm", L(-5.6f, h - 1, z), new Vector3(3.6f, .4f, .4f), rot, Mat("M_WallCircuit"), s);
        BoostPad(L(-4, h, cwStart + 5), rot, s);
        RingLine(L(-4, h + 1.6f, cwStart + 14), L(-4, h + 1.6f, cwEnd - 6), 10, s);
        pos += rot * Vector3.forward * S2B;

        Turn(s, ref pos, ref rot, false);

        // C : 60 m, pad, anneaux
        Straight(s, pos, rot, S2C);
        BoostPad(L(0, 0, 12), rot, s);
        RingLine(L(0, 1.6f, 26), L(0, 1.6f, 50), 9, s);
        pos += rot * Vector3.forward * S2C;

        S2End(out var endPos, out var endRot);
        Debug.Log("NeoRing : section 2 construite, fin " + pos + " / " + endPos + " cap " + endRot.eulerAngles.y);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 3 : chute verticale n 1
    // Passerelle en caillebotis de 24 m, puis trou : puits de 60 m de long entre deux facades wall_circuit, sol 30 m plus bas.
    // Helice d'anneaux dans le puits, 3 Springs inclines a 40deg (force 160, comme dans SpeerunMadeByCOCO) relancent a l'horizontale.
    const float S3Catwalk = 24, S3Shaft = 60, S3Drop = 30;
    public static void S3End(out Vector3 pos, out Quaternion rot) { S2End(out pos, out rot); pos += rot * new Vector3(0, -S3Drop, S3Catwalk + S3Shaft); }

    [MenuItem("Tools/Neo Ring/4 - Section 3 : Chute")]
    public static void Section3()
    {
        var s = Section("S3_Chute");
        S2End(out var pos, out var rot);
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);
        float fy = -S3Drop; // sol du puits, relatif a la route

        // Passerelle : murs bas mais neons magenta (danger)
        Box("Catwalk", L(0, -.5f, S3Catwalk / 2), new Vector3(RoadW, 1, S3Catwalk), rot, Mat("M_Grate"), s);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (RoadW / 2 + .25f);
            Box("Wall", L(x, 1.25f, S3Catwalk / 2), new Vector3(.5f, 2.5f, S3Catwalk), rot, Mat("M_WallPanels"), s);
            Neon("NeonEdge", L(x, 2.58f, S3Catwalk / 2), new Vector3(.16f, .16f, S3Catwalk), "Magenta", s, rot);
        }
        Box("EdgeStripes", L(0, .005f, S3Catwalk - .5f), new Vector3(RoadW, .01f, 1), rot, Mat("M_Hazard"), s, false);
        Neon("EdgeNeon", L(0, .1f, S3Catwalk), new Vector3(RoadW + .5f, .2f, .2f), "Magenta", s, rot);

        // Puits
        float zc = S3Catwalk + S3Shaft / 2;
        Box("ShaftFloor", L(0, fy - 1, zc), new Vector3(20, 2, S3Shaft), rot, Mat("M_FloorPanels"), s);
        Box("ShaftBack", L(0, fy / 2, S3Catwalk - .5f), new Vector3(20, S3Drop, 1), rot, Mat("M_WallCircuit"), s);
        Box("FacadeL", L(-10.5f, (fy - 1 + 17) / 2, zc), new Vector3(1, 18 - (fy - 1), S3Shaft), rot, Mat("M_WallCircuit"), s);
        Box("FacadeR", L(10.5f, (fy - 1 + 1) / 2, zc), new Vector3(1, 2 - (fy - 1), S3Shaft), rot, Mat("M_WallCircuit"), s);
        foreach (float x in new[] { -9.8f, 9.8f }) Neon("FloorNeon", L(x, fy + .1f, zc), new Vector3(.16f, .16f, S3Shaft), "Cyan", s, rot);
        Holo(L(-9.9f, 6, S3Catwalk + 20), rot * Quaternion.Euler(0, -90, 0), new Vector2(12, 6), s);
        Holo(L(-9.9f, fy + 14, S3Catwalk + 44), rot * Quaternion.Euler(0, -90, 0), new Vector2(12, 6), s);

        // Helice d'anneaux : 16 anneaux, rayon 5, de -4 a -28 m
        for (int i = 0; i < 16; i++)
        {
            float a = i * 60 * Mathf.Deg2Rad, y = -4 - i * 1.6f;
            Ring(L(5 * Mathf.Cos(a), y, S3Catwalk + 10 + 5 * Mathf.Sin(a)), s);
        }

        // Relance : 3 Springs inclines vers l'avant + un pad de securite plus loin pour ceux qui ont saute long
        foreach (float x in new[] { -4.5f, 0, 4.5f })
        {
            var sp = Prefab(Prefabs + "Spring.prefab", L(x, fy, S3Catwalk + 20), rot * Quaternion.Euler(40, 0, 0), s);
            sp.GetComponent<Spring_Proprieties>().SpringForce = 160;
        }
        BoostPad(L(0, fy, S3Catwalk + 48), rot, s);

        S3End(out var e, out _);
        Debug.Log("NeoRing : section 3 construite, fin " + e);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }
}
