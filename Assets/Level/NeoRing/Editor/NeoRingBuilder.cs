using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.Splines;
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

    public const string RingCyanPrefab = Root + "Ring_Cyan.prefab";
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
        // Variant de prefab Ring deja cyan : pour tout ce qui instancie des anneaux en jeu (RingSpawnerEternal du LightDashSpline)
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(RingCyanPrefab))
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Ring.prefab"));
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = ring;
            PrefabUtility.SaveAsPrefabAsset(inst, RingCyanPrefab); Object.DestroyImmediate(inst);
        }
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

        UsePlayer(null);

        Group("NeoRing");
        EditorSceneManager.SaveScene(scene, ScenePath);
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
        Debug.Log("NeoRing : scene creee " + ScenePath);
    }

    // Joueur du niveau : SonicManiaFree (vitesses du pack, sans nerf). Remplace l'instance existante si on en passe une.
    public const string PlayerPrefab = "Assets/BumperEngineV1/PlayerPrefabs/SonicManiaFree.prefab";
    [MenuItem("Tools/Neo Ring/Joueur : SonicManiaFree")]
    public static void SwapPlayer() { var cur = Object.FindFirstObjectByType<LevelProgressControl>(); UsePlayer(cur ? cur.transform.root.gameObject : null); EditorSceneManager.SaveOpenScenes(); }
    static GameObject UsePlayer(GameObject existing)
    {
        Vector3 pos = existing ? existing.transform.position : new Vector3(0, 1, 0); Quaternion rot = existing ? existing.transform.rotation : Quaternion.identity;
        if (existing) Object.DestroyImmediate(existing);
        var player = Prefab(PlayerPrefab, pos, rot, null);
        var lpc = player.GetComponentInChildren<LevelProgressControl>(true);
        lpc.IdealTimeSeconds = 60; lpc.MedalTimesSeconds = new[] { 60f, 70f, 85f, 110f };
        lpc.GetComponent<HurtControl>().FallDeathHeight = -100; // le moteur tue sous y=10 par defaut ; la ville basse descend sous 0
        return player;
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
        // Garde-fou : pas d'anneau dans la geometrie (murs, corniches). Les colliders crees dans la meme commande doivent etre synchronises.
        Physics.SyncTransforms();
        if (Physics.OverlapSphere(pos, .9f, ~0, QueryTriggerInteraction.Ignore).Length > 0) { Debug.LogWarning("NeoRing : anneau ignore, dans la geometrie @" + pos); return null; }
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
    // Tunnel ferme a section "fond plat + voute circulaire" : parois interieures praticables (MeshCollider), 2 materiaux
    // (sol au fond, circuit sur la voute). UV en metres monde / 2 comme Box(). start = centre du fond plat a l'entree.
    static GameObject TubeShell(string name, Vector3 start, Quaternion rot, float length, float radius, float centerY, int arcSegments, Transform parent)
    {
        float w = Mathf.Sqrt(radius * radius - centerY * centerY);
        var prof = new System.Collections.Generic.List<Vector2> { new Vector2(-w, 0), new Vector2(w, 0) };
        float a0 = Mathf.Atan2(-centerY, w), a1 = Mathf.Atan2(-centerY, -w) + 2 * Mathf.PI;
        for (int i = 1; i < arcSegments; i++) { float a = Mathf.Lerp(a0, a1, i / (float)arcSegments); prof.Add(new Vector2(radius * Mathf.Cos(a), centerY + radius * Mathf.Sin(a))); }
        int n = prof.Count; var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>();
        var triFloor = new System.Collections.Generic.List<int>(); var triWall = new System.Collections.Generic.List<int>();
        int slices = Mathf.Max(1, Mathf.CeilToInt(length / 40f)); float dz = length / slices; // troncons <= 40 m : PhysX refuse les triangles > 500 m
        for (int k = 0; k < slices; k++)
        {
            float z0 = k * dz, z1 = (k + 1) * dz; Vector3 axis = new Vector3(0, centerY, (z0 + z1) / 2); float u = 0;
            for (int j = 0; j < n; j++)
            {
                var pa = prof[j]; var pb = prof[(j + 1) % n]; float seg = Vector2.Distance(pa, pb);
                int o = v.Count;
                v.Add(new Vector3(pa.x, pa.y, z0)); v.Add(new Vector3(pb.x, pb.y, z0)); v.Add(new Vector3(pb.x, pb.y, z1)); v.Add(new Vector3(pa.x, pa.y, z1));
                uv.Add(new Vector2(u / 2, z0 / 2)); uv.Add(new Vector2((u + seg) / 2, z0 / 2)); uv.Add(new Vector2((u + seg) / 2, z1 / 2)); uv.Add(new Vector2(u / 2, z1 / 2));
                Vector3 nrm = Vector3.Cross(v[o + 1] - v[o], v[o + 2] - v[o]);
                bool inward = Vector3.Dot(nrm, axis - v[o]) > 0; // on veut voir la face depuis l'interieur
                var tri = j == 0 ? triFloor : triWall;
                // Unity : face avant = sens horaire ; on choisit l'ordre qui rend la face visible (et collidable) depuis l'axe du tube
                if (inward) tri.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 }); else tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
                u += seg;
            }
        }
        var m = new Mesh { name = name, subMeshCount = 2 }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(triFloor, 0); m.SetTriangles(triWall, 1); m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(start, rot);
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mat("M_FloorPanels"), Mat("M_WallCircuit") };
        go.AddComponent<MeshCollider>().sharedMesh = m;
        return go;
    }

    // Tube neon sans collider le long d'un axe.
    static GameObject Neon(string name, Vector3 center, Vector3 size, string color, Transform parent, Quaternion? rot = null) => Box(name, center, size, rot ?? Quaternion.identity, Mat("M_Neon_" + color), parent, false);

    // ---------------------------------------------------------------- Section 1 : depart sur le toit
    // Course le long de +Z. Joueur en (0,1,0). Toit de z=-10 a z=S1Len, 14 m de large.
    // Les 70 premiers metres sont un tunnel ferme (murs pleins + plafond) : un joueur booste ne peut plus etre ejecte du niveau.
    const float S1Len = 1050, S1Tunnel = S1Len; // tunnel sur toute la longueur, il debouche directement sur la chute (section 3)
    [MenuItem("Tools/Neo Ring/2 - Section 1 : Depart (toit)")]
    public static void Section1()
    {
        var s = Section("S1_Depart");
        float zc = (S1Len - 10) / 2, len = S1Len + 10, tz = (S1Tunnel - 10) / 2, tl = S1Tunnel + 10;
        // Toit plat seulement apres le tunnel ; dans le tunnel, le sol est le fond plat du tube
        Box("Roof", new Vector3(0, -.5f, (S1Tunnel + S1Len) / 2), new Vector3(14, 1, S1Len - S1Tunnel), Quaternion.identity, Mat("M_FloorPanels"), s);
        Box("Tower", new Vector3(0, -31, zc), new Vector3(18, 60, len + 4), Quaternion.identity, Mat("M_WallCircuit"), s);
        // Tunnel : tube ferme, fond plat ~6.4 m, voute de rayon 7.6 m (sommet a 14.5 m), parois praticables
        TubeShell("Tunnel", new Vector3(0, 0, -10), Quaternion.identity, tl, 7.6f, 6.9f, 28, s);
        foreach (float x in new[] { -3f, 3f }) Neon("TunnelNeon", new Vector3(x, .15f, tz), new Vector3(.16f, .16f, tl), "Cyan", s);
        Box("BackWall", new Vector3(0, 7, -10.25f), new Vector3(16, 16, .5f), Quaternion.identity, Mat("M_WallPanels"), s);
        // Apres le tunnel (s'il reste du toit a l'air libre) : parapets bas jusqu'au bord
        float pz = (S1Tunnel + S1Len) / 2, pl = S1Len - S1Tunnel;
        if (pl > 0)
        {
            foreach (float x in new[] { -7.25f, 7.25f })
            {
                Box("Parapet", new Vector3(x, .6f, pz), new Vector3(.5f, 1.2f, pl), Quaternion.identity, Mat("M_WallPanels"), s);
                Neon("NeonEdge", new Vector3(x, 1.28f, pz), new Vector3(.16f, .16f, pl), "Cyan", s);
            }
            Box("EdgeStripes", new Vector3(0, .005f, S1Len - .5f), new Vector3(14, .01f, 1), Quaternion.identity, Mat("M_Hazard"), s, false);
        }

        // Contenu du tunnel, par tranche de 150 m : pad, ligne au sol, vague d'anneaux sur les parois (gauche <-> droite)
        for (float z0 = 30; z0 < S1Len - 60; z0 += 150)
        {
            BoostPad(new Vector3(0, 0, z0), Quaternion.identity, s);
            RingLine(new Vector3(0, 1.6f, z0 + 14), new Vector3(0, 1.6f, z0 + 54), 14, s);
            // vague : angle autour du fond du tube, -90 = fond, +-75 vers les parois
            for (int i = 0; i <= 24; i++) { float a = (-90 + 75 * Mathf.Sin(i / 24f * 2 * Mathf.PI)) * Mathf.Deg2Rad; Ring(new Vector3(5.4f * Mathf.Cos(a), 6.9f + 5.4f * Mathf.Sin(a), z0 + 66 + i * 3), s); }
        }
        // Bande hazard et neon magenta a la sortie du tube : la passerelle et le trou suivent
        Box("ExitStripes", new Vector3(0, .005f, S1Len - 3), new Vector3(6, .01f, 2), Quaternion.identity, Mat("M_Hazard"), s, false);

        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
        Debug.Log("NeoRing : section 1 construite");
    }

    // ---------------------------------------------------------------- Section 2 : autoroute de neon
    // Chaine : droite 60 m, virage droite 25deg, droite 70 m (voie haute en caillebotis), virage gauche 25deg, droite 60 m.
    const float RoadW = 14, S2A = 200, S2B = 240, S2C = 200, TurnAngle = 25, TurnInner = 90;
    static readonly Vector3 S2Start = new Vector3(0, -2, S1Len + 8); // saut de 8 m depuis le toit (y=0)

    // Rejoue la geometrie sans rien construire : fin de la section 2 = debut de la section 3.
    // Section 2 (autoroute) desactivee a la demande : le tunnel va jusqu'a la chute. S2End = sortie du tunnel.
    public static void S2End(out Vector3 pos, out Quaternion rot) { pos = new Vector3(0, 0, S1Len); rot = Quaternion.identity; }
    public static void S2EndHighway(out Vector3 pos, out Quaternion rot)
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

    [MenuItem("Tools/Neo Ring/3 - Section 2 : Autoroute (desactivee)")]
    public static void Section2() { Section("S2_Autoroute"); Debug.Log("NeoRing : section 2 desactivee, le tunnel mene a la chute (Section2Highway pour la reconstruire)"); }
    public static void Section2Highway()
    {
        var s = Section("S2_Autoroute");
        Vector3 pos = S2Start; Quaternion rot = Quaternion.identity;
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        // A : 60 m, checkpoint, pad, anneaux
        Straight(s, pos, rot, S2A);
        // CheckPos du prefab est a +7 m en X local : poteau a gauche, respawn a x=+1.5 sur la route (a droite, il tombait dans le vide).
        Prefab(Prefabs + "CheckPoint.prefab", L(-5.5f, 0, 6), rot, s);
        foreach (float z in new[] { 14f, 80f, 150f }) BoostPad(L(0, 0, z), rot, s);
        RingLine(L(0, 1.6f, 26), L(0, 1.6f, 60), 12, s); RingLine(L(-3, 1.6f, 96), L(-3, 1.6f, 130), 12, s); RingLine(L(3, 1.6f, 166), L(3, 1.6f, 190), 9, s);
        pos += rot * Vector3.forward * S2A;

        Turn(s, ref pos, ref rot, true);

        // B : 70 m, voie haute en caillebotis a gauche (+6 m), rampe d'acces, pads
        Straight(s, pos, rot, S2B);
        foreach (float z in new[] { 15f, 100f, 190f }) BoostPad(L(2, 0, z), rot, s);
        RingLine(L(3, 1.6f, 30), L(3, 1.6f, 80), 14, s); RingLine(L(3, 1.6f, 120), L(3, 1.6f, 170), 14, s);
        float rampLen = 24, h = 6; float tilt = -Mathf.Atan2(h, rampLen) * Mathf.Rad2Deg;
        var rampRot = rot * Quaternion.Euler(tilt, 0, 0);
        Box("Ramp", L(-4, h / 2 - .25f * Mathf.Cos(tilt * Mathf.Deg2Rad), rampLen / 2), new Vector3(4, .5f, Mathf.Sqrt(rampLen * rampLen + h * h)), rampRot, Mat("M_Grate"), s);
        float cwStart = rampLen, cwEnd = S2B - 12;
        Box("Catwalk", L(-4, h - .15f, (cwStart + cwEnd) / 2), new Vector3(4, .3f, cwEnd - cwStart), rot, Mat("M_Grate"), s);
        Box("CatwalkEdge", L(-4, h + .005f, cwEnd - .5f), new Vector3(4, .01f, 1), rot, Mat("M_Hazard"), s, false);
        Neon("CatwalkNeon", L(-6.1f, h + .08f, (cwStart + cwEnd) / 2), new Vector3(.12f, .12f, cwEnd - cwStart), "Yellow", s, rot);
        for (float z = cwStart + 6; z < cwEnd; z += 16) Box("Arm", L(-5.6f, h - 1, z), new Vector3(3.6f, .4f, .4f), rot, Mat("M_WallCircuit"), s);
        BoostPad(L(-4, h, cwStart + 5), rot, s); BoostPad(L(-4, h, (cwStart + cwEnd) / 2), rot, s);
        RingLine(L(-4, h + 1.6f, cwStart + 14), L(-4, h + 1.6f, cwEnd - 6), 24, s);
        pos += rot * Vector3.forward * S2B;

        Turn(s, ref pos, ref rot, false);

        // C : 60 m, pad, anneaux
        Straight(s, pos, rot, S2C);
        foreach (float z in new[] { 12f, 90f, 170f }) BoostPad(L(0, 0, z), rot, s);
        RingLine(L(0, 1.6f, 26), L(0, 1.6f, 70), 14, s); RingLine(L(2, 1.6f, 106), L(2, 1.6f, 150), 14, s);
        pos += rot * Vector3.forward * S2C;

        S2EndHighway(out var endPos, out var endRot);
        Debug.Log("NeoRing : section 2 construite, fin " + pos + " / " + endPos + " cap " + endRot.eulerAngles.y);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 3 : chute verticale n 1
    // Passerelle en caillebotis de 24 m, puis trou : puits de 60 m de long entre deux facades wall_circuit, sol 30 m plus bas.
    // Helice d'anneaux dans le puits, 3 Springs inclines a 40deg (force 160, comme dans SpeerunMadeByCOCO) relancent a l'horizontale.
    const float S3Catwalk = 80, S3Shaft = 160, S3Drop = 30;
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
        BoostPad(L(0, fy, S3Catwalk + 48), rot, s); BoostPad(L(0, fy, S3Catwalk + 110), rot, s);
        RingLine(L(0, fy + 1.6f, S3Catwalk + 60), L(0, fy + 1.6f, S3Catwalk + 100), 12, s);

        S3End(out var e, out _);
        Debug.Log("NeoRing : section 3 construite, fin " + e);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 4 : zone ennemis (ville basse)
    // Rue de 150 m x 24 m entre deux rangees d'immeubles. Vide de 40 m au milieu : pont lateral en caillebotis (chemin principal)
    // ou chaine de homing sur 3 drones au-dessus du vide (raccourci). Trigger Pit sous le vide.
    const float S4Len = 500, S4W = 24, S4VoidA = 260, S4VoidB = 320;
    public static void S4End(out Vector3 pos, out Quaternion rot) { S3End(out pos, out rot); pos += rot * new Vector3(0, 0, S4Len); }

    // Ennemi cyberpunk ; garantit une cible de homing (tag HomingTarget) attachee au corps mobile.
    static GameObject Enemy(string name, Vector3 pos, Quaternion rot, Transform parent)
    {
        var go = Prefab("Assets/Ennemy/NeoRing/" + name + ".prefab", pos, rot, parent);
        if (!go.GetComponentsInChildren<Transform>(true).Any(t => t.CompareTag("HomingTarget")))
        {
            var body = go.GetComponentInChildren<EnemyHealth>(true).transform;
            var ht = new GameObject("HomingTarget") { tag = "HomingTarget" }; ht.transform.SetParent(body, false);
        }
        return go;
    }

    [MenuItem("Tools/Neo Ring/5 - Section 4 : Zone ennemis")]
    public static void Section4()
    {
        var s = Section("S4_Ennemis");
        S3End(out var pos, out var rot);
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        // Sol en deux parties, de part et d'autre du vide
        Box("Street_A", L(0, -1, S4VoidA / 2), new Vector3(S4W, 2, S4VoidA), rot, Mat("M_FloorPanels"), s);
        Box("Street_B", L(0, -1, (S4VoidB + S4Len) / 2), new Vector3(S4W, 2, S4Len - S4VoidB), rot, Mat("M_FloorPanels"), s);
        // Immeubles contigus des deux cotes (canyon ferme), hauteurs variees
        for (int k = 0; k < Mathf.CeilToInt(S4Len / 30); k++)
            foreach (float side in new[] { -1f, 1f })
            {
                float h = 30 + ((k + (side > 0 ? 1 : 0)) % 3) * 12;
                Box("Tower", L(side * (S4W / 2 + 4), h / 2 - 1, 15 + 30 * k), new Vector3(8, h, 30), rot, Mat("M_WallCircuit"), s);
                if (k % 2 == 0) Holo(L(side * (S4W / 2 - .1f), 9, 15 + 30 * k), rot * Quaternion.Euler(0, side < 0 ? -90 : 90, 0), new Vector2(10, 5), s);
            }
        foreach (float side in new[] { -1f, 1f }) Neon("StreetNeon", L(side * (S4W / 2 - .2f), .1f, S4Len / 2), new Vector3(.16f, .16f, S4Len), "Cyan", s, rot);

        Prefab(Prefabs + "CheckPoint.prefab", L(-5.5f, 0, 6), rot, s);

        // Zone A : rouleurs puis crabes
        foreach (var (x, z) in new[] { (-5f, 40f), (5f, 80f), (0f, 120f), (6f, 160f), (6f, 200f) }) Enemy("Neo_Rouleur", L(x, 0, z), rot, s);
        foreach (var (x, z) in new[] { (-7f, 100f), (7f, 140f), (-7f, 215f), (7f, 240f) }) Enemy("Neo_CrabeSentinelle", L(x, 0, z), rot * Quaternion.Euler(0, 180, 0), s);
        RingLine(L(0, 1.6f, 12), L(0, 1.6f, 60), 14, s); RingLine(L(-4, 1.6f, 125), L(-4, 1.6f, 180), 14, s); // pas de booster : ennemis juste apres

        // Vide : trigger Pit 14 m plus bas, cadre magenta
        var pit = Group("Pit", s); pit.tag = "Pit"; pit.transform.SetPositionAndRotation(L(0, -14, (S4VoidA + S4VoidB) / 2), rot);
        var pc = pit.AddComponent<BoxCollider>(); pc.isTrigger = true; pc.size = new Vector3(S4W + 20, 2, S4VoidB - S4VoidA - 2);
        foreach (float z in new[] { S4VoidA, S4VoidB }) Neon("VoidNeon", L(0, .1f, z), new Vector3(S4W, .2f, .2f), "Magenta", s, rot);
        Box("VoidStripes_A", L(0, .005f, S4VoidA - .5f), new Vector3(S4W, .01f, 1), rot, Mat("M_Hazard"), s, false);
        Box("VoidStripes_B", L(0, .005f, S4VoidB + .5f), new Vector3(S4W, .01f, 1), rot, Mat("M_Hazard"), s, false);

        // Pont lateral gauche (chemin principal), bord interieur magenta
        float vc = (S4VoidA + S4VoidB) / 2, vl = S4VoidB - S4VoidA;
        Box("Bridge", L(-9, -.5f, vc), new Vector3(6, 1, vl + 4), rot, Mat("M_Grate"), s);
        Neon("BridgeNeon", L(-6.1f, .1f, vc), new Vector3(.12f, .12f, vl), "Magenta", s, rot);
        RingLine(L(-9, 1.6f, S4VoidA + 4), L(-9, 1.6f, S4VoidB - 4), 12, s);

        // Raccourci : 3 drones au-dessus du vide (corps a +4 m, route +-5 m en x), anneaux entre eux
        for (float z = S4VoidA + 8; z < S4VoidB - 4; z += 15) { Enemy("Neo_DroneGuepe", L(0, 0, z - 2.5f), rot, s); if (z + 15 < S4VoidB - 4) Ring(L(0, 5, z + 7.5f), s); }

        // Zone B : relance
        foreach (var (x, z) in new[] { (0f, 350f), (6f, 410f), (-6f, 460f) }) Enemy("Neo_Rouleur", L(x, 0, z), rot, s);
        foreach (var (x, z) in new[] { (-6f, 375f), (5f, 435f), (-5f, 480f) }) Enemy("Neo_CrabeSentinelle", L(x, 0, z), rot * Quaternion.Euler(0, 180, 0), s);
        RingLine(L(4, 1.6f, 328), L(4, 1.6f, 365), 10, s); RingLine(L(-4, 1.6f, 385), L(-4, 1.6f, 430), 12, s); RingLine(L(0, 1.6f, 448), L(0, 1.6f, 476), 8, s); // pas de pad en fin : la section 5 est un puits vertical

        S4End(out var e, out _);
        Debug.Log("NeoRing : section 4 construite, fin " + e);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 5 : montee verticale n 2
    // Puits de 14 m x 16 m entre deux tours, 40 m de haut. Corniches alternees gauche/droite tous les 10 m ;
    // sur chacune un lanceur (Spring ou DashRing) vise la corniche opposee. Balistique calee sur la gravite du joueur (S5G).
    // Raccourci : LightDashSpline en diagonale du bas vers la sortie.
    const float S5H = 60, S5D = 16, S5W = 14;
    // PlayerBhysics ajoute Gravity (0,-1.5,0) a la vitesse a chaque FixedUpdate : g effectif = 1.5 / pas fixe (mesure en Play : ~150 m/s2 a 100 Hz).
    static float S5G => 1.5f / Time.fixedDeltaTime;
    public static void S5End(out Vector3 pos, out Quaternion rot) { S4End(out pos, out rot); pos += rot * new Vector3(0, S5H, S5D); }

    // Vitesse de saut (vx lateral, vy) pour passer a dx a l'horizontale et dy+2.5 en hauteur (marge pour le corps) en t secondes.
    static Vector2 Hop(float dx, float dy, float t = .5f) => new Vector2(dx / t, (dy + 2.5f + .5f * S5G * t * t) / t);

    [MenuItem("Tools/Neo Ring/6 - Section 5 : Montee")]
    public static void Section5()
    {
        var s = Section("S5_Montee");
        S4End(out var pos, out var rot);
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        Box("ShaftFloor", L(0, -1, S5D / 2), new Vector3(S5W, 2, S5D), rot, Mat("M_FloorPanels"), s);
        foreach (float side in new[] { -1f, 1f })
            Box("Tower", L(side * (S5W / 2 + 4), (S5H + 30 - 2) / 2, S5D / 2), new Vector3(8, S5H + 30, S5D), rot, Mat("M_WallCircuit"), s);
        Box("BackWall", L(0, (S5H - 2) / 2, S5D + .5f), new Vector3(S5W + 16, S5H + 2, 1), rot, Mat("M_WallCircuit"), s); // sommet = niveau de sortie
        Neon("FloorNeon", L(0, .1f, S5D - .3f), new Vector3(S5W, .16f, .16f), "Cyan", s, rot);

        // Corniches alternees droite/gauche tous les 10 m ; la sortie (y=S5H) est du cote oppose a la derniere corniche.
        int N = Mathf.RoundToInt(S5H / 10) - 1; float zc = S5D / 2, ledgeW = 7;
        float xLedge(int k) => (k % 2 == 1 ? 1 : -1) * (S5W / 2 - ledgeW / 2);
        float xExit = -xLedge(N);
        for (int k = 1; k <= N; k++)
        {
            float y = 10 * k;
            Box("Ledge", L(xLedge(k), y - .5f, zc), new Vector3(ledgeW, 1, 6), rot, Mat("M_FloorPanels"), s);
            Box("LedgeStripes", L(xLedge(k) - Mathf.Sign(xLedge(k)) * (ledgeW / 2 - .5f), y + .005f, zc), new Vector3(1, .01f, 6), rot, Mat("M_Hazard"), s, false);
            Neon("LedgeNeon", L(xLedge(k) - Mathf.Sign(xLedge(k)) * (ledgeW / 2), y + .1f, zc), new Vector3(.16f, .16f, 6), "Yellow", s, rot);
        }
        // Corniche de sortie (gauche, y=40) prolongee jusqu'au mur du fond : la section 6 commence dessus.
        Box("ExitLedge", L(xExit, S5H - .5f, (zc - 3 + S5D + 1) / 2), new Vector3(ledgeW, 1, S5D + 1 - (zc - 3)), rot, Mat("M_FloorPanels"), s);
        Neon("ExitNeon", L(xExit - Mathf.Sign(xExit) * ledgeW / 2, S5H + .1f, (zc - 3 + S5D + 1) / 2), new Vector3(.16f, .16f, S5D + 1 - (zc - 3)), "Yellow", s, rot);
        // Lanceurs : sol -> corniche 1, corniche k -> corniche k+1, corniche 3 -> sortie (x=-4.5, y=40)
        // Lanceur a l'oppose de sa cible (cote mur de sa propre corniche) : la parabole doit passer au-dessus du bord
        // interieur de la corniche visee, sinon le joueur se cogne dessous et retombe dans l'anneau (boucle infinie, vu en Play).
        var fromX = new float[N + 1]; var fromY = new float[N + 1]; var toX = new float[N + 1]; var toY = new float[N + 1];
        for (int i = 0; i <= N; i++)
        {
            fromX[i] = i == 0 ? -Mathf.Sign(xLedge(1)) * 3f : xLedge(i) + Mathf.Sign(xLedge(i)) * 2; fromY[i] = 10 * i;
            toX[i] = i == N ? xExit : xLedge(i + 1); toY[i] = 10 * (i + 1);
        }
        for (int i = 0; i <= N; i++)
        {
            Vector2 v = Hop(toX[i] - fromX[i], toY[i] - fromY[i]);
            Vector3 dirLocal = new Vector3(v.x, v.y, 0).normalized; float speed = v.magnitude;
            Vector3 at = L(fromX[i], fromY[i], i == 0 ? 10 : zc);
            // Spring seulement sur la corniche 2 (le joueur y arrive en l'air). Au sol, un joueur lance est recolle au sol par
            // PlayerBhysics (rayon de sol allonge par la vitesse) : le lanceur du bas est donc un DashRing, teste en Play.
            if (i > 0 && i % 2 == 0)
            {
                // Spring : lance selon transform.up
                var sp = Prefab(Prefabs + "Spring.prefab", at, rot * Quaternion.FromToRotation(Vector3.up, dirLocal), s);
                sp.GetComponent<Spring_Proprieties>().SpringForce = speed;
            }
            else
            {
                // DashRing : lance selon transform.forward, anneau debout (son plan contient la direction)
                var dr = Prefab(Prefabs + "DashRing.prefab", at + rot * new Vector3(0, 2.5f, 0), rot * Quaternion.LookRotation(dirLocal, Vector3.forward), s);
                dr.GetComponentInChildren<SpeedPadData>().Speed = speed;
            }
        }
        // Anneaux : 3 par bond, le long de la parabole
        for (int i = 0; i <= N; i++)
        {
            Vector2 v = Hop(toX[i] - fromX[i], toY[i] - fromY[i]);
            for (int j = 1; j <= 3; j++) { float t = .5f * j / 5; Ring(L(fromX[i] + v.x * t, fromY[i] + 1.5f + v.y * t - .5f * S5G * t * t, zc), s); }
        }

        // Raccourci : light dash en diagonale (anneaux tous les 3 m)
        var ld = Prefab(Prefabs + "LightDashSpline.prefab", L(0, 0, 0), rot, s);
        var spline = ld.GetComponent<Spline>();
        spline.nodes[0].SetPosition(new Vector3(-xExit, 2, 3)); spline.nodes[0].SetDirection(new Vector3(-xExit, 14, 6));
        spline.nodes[1].SetPosition(new Vector3(xExit, S5H + 3, zc)); spline.nodes[1].SetDirection(new Vector3(xExit, S5H + 13, zc + 1));
        var sower = ld.GetComponent<SplineSower>(); sower.spacing = 3; sower.Sow();
        var cyanRing = AssetDatabase.LoadAssetAtPath<GameObject>(RingCyanPrefab);
        foreach (var sp in ld.GetComponentsInChildren<RingSpawnerEternal>(true)) sp.Ring = cyanRing; // sinon anneaux jaunes en jeu

        // Panneaux holo qui scintillent dans le puits
        for (int h = 0; 6 + 18 * h < S5H; h++)
            Holo(L((h % 2 == 1 ? 1 : -1) * (S5W / 2 - .1f), 6 + 18 * h, zc), rot * Quaternion.Euler(0, h % 2 == 1 ? 90 : -90, 0), new Vector2(8, 4), s);

        S5End(out var e, out _);
        Debug.Log("NeoRing : section 5 construite, fin " + e);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 6 : tube final + grand saut
    // Plateforme de depart (CheckPoint), tube SonicTube qui plonge et vire a droite, plateforme de sortie, rampe,
    // DashRing au sommet qui propulse au-dessus de la ville jusqu'au sprint final (section 7, 15 m plus bas).
    const float S6Deck = 120, S6TubeLen = 220, S6ExitZ = S6Deck + S6TubeLen, S6Jump = S6ExitZ + 71, S6Drop = 15;
    public static void S6End(out Vector3 pos, out Quaternion rot) { S5End(out pos, out rot); pos += rot * new Vector3(0, -S6Drop, S6Jump); }

    [MenuItem("Tools/Neo Ring/7 - Section 6 : Tube")]
    public static void Section6()
    {
        var s = Section("S6_Tube");
        S5End(out var pos, out var rot);
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        Straight(s, pos, rot, S6Deck);
        Prefab(Prefabs + "CheckPoint.prefab", L(-5.5f, 0, 4), rot, s);
        RingLine(L(0, 1.6f, 14), L(0, 1.6f, 60), 14, s); RingLine(L(0, 1.6f, 70), L(0, 1.6f, 100), 10, s);

        // Tube : noeuds en local du tube, tangentes auto ; entree au bout de la plateforme, sortie 9 m plus bas
        var tube = Prefab("Assets/Structure/Tube Slide/Tube_Test.prefab", L(0, 0, S6Deck), rot, s);
        var container = tube.GetComponent<SplineContainer>();
        var spline = container.Spline; spline.Clear();
        foreach (var k in new[] { new Vector3(0, 1.6f, 0), new Vector3(0, -5, S6TubeLen * .32f), new Vector3(7, -13, S6TubeLen * .64f), new Vector3(0, -9, S6TubeLen) }) spline.Add(new BezierKnot(k), TangentMode.AutoSmooth);
        tube.transform.Find("Entree_Tube").localPosition = new Vector3(0, 1.6f, 0); tube.transform.Find("Sortie_Tube").localPosition = new Vector3(0, -9, S6TubeLen); // SonicTube les recale aussi en Play
        foreach (var mr in tube.GetComponentsInChildren<MeshRenderer>(true)) mr.sharedMaterial = Mat("M_WallCircuit");
        foreach (var ex in tube.GetComponentsInChildren<SplineExtrude>(true)) ex.Rebuild();

        // Sortie : plateforme, rampe a 25 deg, DashRing au sommet vers le ciel
        float ez = S6ExitZ;
        Box("ExitDeck", L(0, -11.5f, ez + 7), new Vector3(12, 2, 14), rot, Mat("M_FloorPanels"), s);
        float rampLen = 12, rampH = rampLen * Mathf.Tan(25 * Mathf.Deg2Rad);
        Box("JumpRamp", L(0, -10.5f + rampH / 2 - .25f, ez + 14 + rampLen / 2), new Vector3(12, .5f, rampLen / Mathf.Cos(25 * Mathf.Deg2Rad)), rot * Quaternion.Euler(-25, 0, 0), Mat("M_FloorPanels"), s);
        Box("RampStripes", L(0, -10.5f + rampH + .3f, ez + 14 + rampLen - .4f), new Vector3(12, .01f, .8f), rot * Quaternion.Euler(-25, 0, 0), Mat("M_Hazard"), s, false);
        foreach (float x in new[] { -6.1f, 6.1f }) Neon("RampNeon", L(x, -10.5f + rampH / 2 + .1f, ez + 14 + rampLen / 2), new Vector3(.16f, .16f, rampLen / Mathf.Cos(25 * Mathf.Deg2Rad)), "Yellow", s, rot * Quaternion.Euler(-25, 0, 0));
        Vector3 jump = new Vector3(0, Mathf.Sin(35 * Mathf.Deg2Rad), Mathf.Cos(35 * Mathf.Deg2Rad));
        var ring = Prefab(Prefabs + "DashRing.prefab", L(0, -10.5f + rampH + 3, ez + 14 + rampLen + 1), rot * Quaternion.LookRotation(jump, Vector3.up), s);
        ring.GetComponentInChildren<SpeedPadData>().Speed = 70; // vy 40, vx 57 : ~64 m de portee jusqu'a 12 m plus bas (g=90)

        // Ville en contrebas du saut : tours decoratives
        foreach (var (x, h, z) in new[] { (-14f, 40f, 35f), (10f, 55f, 48f), (-6f, 30f, 60f), (16f, 48f, 65f) })
            Box("CityTower", L(x, -S6Drop - 30 - 2 + h / 2 - h, ez + z), new Vector3(10, h, 10), rot, Mat("M_WallCircuit"), s);

        S6End(out var e, out _);
        Debug.Log("NeoRing : section 6 construite, fin " + e);
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }

    // ---------------------------------------------------------------- Section 7 : sprint final
    const float S7Len = 400;
    [MenuItem("Tools/Neo Ring/8 - Section 7 : Sprint final")]
    public static void Section7()
    {
        var s = Section("S7_Sprint");
        S6End(out var pos, out var rot);
        Vector3 L(float x, float y, float z) => pos + rot * new Vector3(x, y, z);

        Straight(s, pos, rot, S7Len);
        Box("EndWall", L(0, 4, S7Len + .25f), new Vector3(RoadW + .5f, 8, .5f), rot, Mat("M_WallPanels"), s);
        foreach (float z in new[] { 60f, 150f, 240f, 330f }) BoostPad(L(0, 0, z), rot, s);
        RingLine(L(-3, 1.6f, 75), L(-3, 1.6f, 130), 14, s); RingLine(L(3, 1.6f, 165), L(3, 1.6f, 220), 14, s); RingLine(L(-3, 1.6f, 255), L(-3, 1.6f, 310), 14, s); RingLine(L(3, 1.6f, 345), L(3, 1.6f, 375), 8, s);
        var goal = Prefab(Prefabs + "GoalRing.prefab", L(0, 5.6f, S7Len - 12), rot, s);
        foreach (var r in goal.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial && r.sharedMaterial.shader.name.Contains("RingShader")) r.sharedMaterial = Mat("M_RingCyan");
        foreach (float x in new[] { -16f, 16f }) Holo(L(x, 8, S7Len - 30), Quaternion.Euler(0, x < 0 ? -90 : 90, 0), new Vector2(10, 5), s);

        Debug.Log("NeoRing : section 7 construite, GoalRing a " + L(0, 5.6f, S7Len - 12));
        EditorSceneManager.MarkSceneDirty(s.gameObject.scene); EditorSceneManager.SaveScene(s.gameObject.scene);
    }
}
