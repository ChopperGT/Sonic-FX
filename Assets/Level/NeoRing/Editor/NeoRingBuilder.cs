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
            ti.alphaSource = n == "floor_grate_albedo" ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.FromInput; // la grille n'a pas d'alpha : trous = pixels sombres
            ti.alphaIsTransparency = n.StartsWith("holo") || n == "floor_grate_albedo";
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
        var grate = Lit("M_Grate", "floor_grate_albedo", null, 0);
        grate.SetFloat("_Mode", 1); grate.EnableKeyword("_ALPHATEST_ON"); grate.SetFloat("_Cutoff", .35f); grate.renderQueue = 2450; grate.SetOverrideTag("RenderType", "TransparentCutout");
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
    static GameObject Neon(string name, Vector3 center, Vector3 size, string color, Transform parent) => Box(name, center, size, Quaternion.identity, Mat("M_Neon_" + color), parent, false);

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
}
