using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace SonicFX.Buzz.Editor
{
    public static class BuzzBuilder
    {
        [Serializable] private class Model { public Node[] nodes; }
        [Serializable] private class Node
        {
            public string name, parent;
            public float[] position, vertices, uv;
            public int[] triangles;
            public int material;
        }
        private static string Package
        {
            get
            {
                foreach (string guid in AssetDatabase.FindAssets("BuzzBuilder t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileName(path) == "BuzzBuilder.cs") return Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace('\\', '/');
                }
                throw new InvalidOperationException("Dossier BuzzBomber introuvable.");
            }
        }
        [MenuItem("Tools/Sonic FX/Creer la guepe robot")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorUtility.DisplayDialog("Guepe robot", "Quitte le mode Play avant de generer le prefab.", "OK"); return; }
            string package = Package;
            var data = JsonUtility.FromJson<Model>(File.ReadAllText(package + "/Editor/BuzzModel.json"));
            if (data == null || data.nodes == null || data.nodes.Length == 0) throw new InvalidDataException("Modele incomplet.");
            int enemyLayer = LayerMask.NameToLayer("Enemies"), triggerLayer = LayerMask.NameToLayer("EnemyTrigger");
            if (enemyLayer < 0 || triggerLayer < 0) throw new InvalidOperationException("Ce pack attend les couches Enemies et EnemyTrigger de Sonic-FX.");
            foreach (string tag in new[] { "Enemy", "HomingTarget", "Player" })
                if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(tag)) throw new InvalidOperationException("Tag manquant : " + tag);
            string folder = AssetDatabase.GenerateUniqueAssetPath(package + "/Guepe_Generee");
            AssetDatabase.CreateFolder(package, Path.GetFileName(folder));
            AssetDatabase.CreateFolder(folder, "Meshes");
            var texturePath = package + "/Textures/Buzz_Atlas.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (atlas == null) throw new InvalidOperationException("Buzz_Atlas.png manquant.");
            Material[] mats = Materials(folder, atlas);
            var meshes = new Dictionary<string, Mesh>();
            foreach (var n in data.nodes)
            {
                if (n.vertices == null || n.vertices.Length == 0) continue;
                if (n.vertices.Length % 3 != 0 || n.uv.Length != n.vertices.Length / 3 * 2) throw new InvalidDataException(n.name);
                var vertices = new Vector3[n.vertices.Length / 3]; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                { vertices[i] = new Vector3(n.vertices[i*3], n.vertices[i*3+1], n.vertices[i*3+2]); uv[i] = new Vector2(n.uv[i*2],n.uv[i*2+1]); }
                var mesh = new Mesh { name = n.name, vertices = vertices, uv = uv, triangles = n.triangles };
                mesh.RecalculateNormals(); SmoothSeams(mesh); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, folder + "/Meshes/" + n.name + ".asset"); meshes.Add(n.name, mesh);
            }
            GameObject system = new GameObject("Guepe_Robot_Parcours");
            try
            {
                var routeObject = new GameObject("Parcours_MODIFIER_LES_POINTS"); routeObject.transform.SetParent(system.transform, false);
                var route = routeObject.AddComponent<BuzzRoute>();
                var positions = new[] { new Vector3(-5,4,0), new Vector3(5,4,0), new Vector3(5,5,5), new Vector3(-5,4,5) };
                route.points = new BuzzRoutePoint[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    var point = new GameObject("Point_" + (i+1)); point.transform.SetParent(routeObject.transform, false); point.transform.localPosition = positions[i];
                    route.points[i] = point.AddComponent<BuzzRoutePoint>(); route.points[i].speed = 3f; route.points[i].pause = i % 2 == 0 ? 1.2f : .5f;
                }
                GameObject enemy = new GameObject("Ennemi_Guepe"); enemy.transform.SetParent(system.transform, false); enemy.layer = enemyLayer; enemy.tag = "Enemy";
                var transforms = CreateModel(enemy, data, meshes, mats, enemyLayer);
                var clips = new Dictionary<string, AnimationClip>();
                foreach (string kind in new[] { "SurPlace", "Vol", "Attaque", "Destruction" })
                {
                    var clip = MakeClip(kind, transforms, enemy.transform);
                    AssetDatabase.CreateAsset(clip, folder + "/Guepe_" + kind + ".anim"); clips.Add(kind, clip);
                }
                var anim = enemy.AddComponent<Animator>(); anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Guepe_Animator.controller");
                controller.AddParameter("Flying", AnimatorControllerParameterType.Bool); controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
                var sm = controller.layers[0].stateMachine;
                var idle = sm.AddState("SurPlace"); idle.motion = clips["SurPlace"]; idle.writeDefaultValues = false; sm.defaultState = idle;
                var fly = sm.AddState("Vol"); fly.motion = clips["Vol"]; fly.writeDefaultValues = false;
                var attack = sm.AddState("Attaque"); attack.motion = clips["Attaque"]; attack.writeDefaultValues = false;
                Transition(idle, fly, "Flying", true); Transition(fly, idle, "Flying", false);
                var shoot = sm.AddAnyStateTransition(attack); shoot.hasExitTime = false; shoot.duration = .06f; shoot.hasFixedDuration = true; shoot.canTransitionToSelf = false;
                shoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
                var recover = attack.AddTransition(idle); recover.hasExitTime = true; recover.exitTime = 1f; recover.duration = .08f; recover.hasFixedDuration = true;
                anim.runtimeAnimatorController = controller;

                GameObject impact = Impact(folder, mats[1]);
                GameObject death = new GameObject("Guepe_Destruction");
                GameObject deathPrefab;
                try
                {
                    CreateModel(death, data, meshes, mats, enemyLayer);
                    var deathAnim = death.AddComponent<Animator>(); deathAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var deathController = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Guepe_Destruction.controller");
                    var state = deathController.layers[0].stateMachine.AddState("Destruction"); state.motion = clips["Destruction"];
                    deathController.layers[0].stateMachine.defaultState = state; deathAnim.runtimeAnimatorController = deathController;
                    death.AddComponent<BuzzLifetime>().seconds = 1.2f;
                    var sparks = UnityEngine.Object.Instantiate(impact, death.transform); sparks.name = "Etincelles";
                    deathPrefab = PrefabUtility.SaveAsPrefabAsset(death, folder + "/Guepe_Destruction.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(death); }
                BuzzProjectile projectile = Projectile(folder, mats, impact);
                var rb = enemy.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var solid = new GameObject("CollisionCorps"); solid.transform.SetParent(enemy.transform, false); solid.layer = enemyLayer;
                solid.AddComponent<SphereCollider>().radius = .65f;
                var hit = new GameObject("ContactSonic"); hit.transform.SetParent(enemy.transform, false); hit.tag = "Enemy"; hit.layer = triggerLayer;
                var sphere = hit.AddComponent<SphereCollider>(); sphere.radius = .95f; sphere.isTrigger = true;
                var homing = new GameObject("HomingTarget"); homing.transform.SetParent(enemy.transform, false); homing.tag = "HomingTarget"; homing.layer = enemyLayer;
                var health = enemy.AddComponent<EnemyHealth>(); health.MaxHealth = 1; health.Explosion = deathPrefab;
                var brain = enemy.AddComponent<BuzzController>(); brain.route = route; brain.muzzle = transforms["Muzzle"]; brain.projectilePrefab = projectile;
                enemy.transform.localPosition = positions[0];
                var prefab = PrefabUtility.SaveAsPrefabAsset(system, folder + "/Guepe_Robot_Parcours.prefab");
                AssetDatabase.SaveAssets(); Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
                Debug.Log("Guepe prete : glisse Guepe_Robot_Parcours dans ta scene. Developpe Parcours_MODIFIER_LES_POINTS pour deplacer les points avec W.", prefab);
            }
            finally { UnityEngine.Object.DestroyImmediate(system); }
        }
        private static Dictionary<string, Transform> CreateModel(GameObject root, Model data, Dictionary<string, Mesh> meshes, Material[] materials, int layer)
        {
            var result = new Dictionary<string, Transform>();
            foreach (var n in data.nodes)
            {
                var go = new GameObject(n.name); go.layer = layer;
                go.transform.SetParent(string.IsNullOrEmpty(n.parent) ? root.transform : result[n.parent], false);
                go.transform.localPosition = new Vector3(n.position[0], n.position[1], n.position[2]); result.Add(n.name, go.transform);
                if (!meshes.TryGetValue(n.name, out Mesh mesh)) continue;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = materials[n.material];
            }
            return result;
        }
        private static Material[] Materials(string folder, Texture atlas)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Ce pack utilise le shader Standard du projet Sonic-FX.");
            var armor = new Material(shader) { name = "Guepe_Atlas", mainTexture = atlas }; armor.SetFloat("_Metallic", .3f); armor.SetFloat("_Glossiness", .52f);
            var yellow = new Material(shader) { name = "Guepe_Energie", color = new Color(1,.68f,.08f) };
            yellow.EnableKeyword("_EMISSION"); yellow.SetColor("_EmissionColor", new Color(1,.45f,.025f)*2f);
            var flame = new Material(shader) { name = "Guepe_Flamme", color = new Color(1,.12f,.015f) };
            flame.EnableKeyword("_EMISSION"); flame.SetColor("_EmissionColor", new Color(1,.12f,.01f)*1.4f);
            var white = new Material(shader) { name = "Guepe_Yeux", color = new Color(.97f,.99f,1f) }; white.SetFloat("_Glossiness", .7f);
            var result = new[] { armor, yellow, flame, white };
            foreach (var mat in result) AssetDatabase.CreateAsset(mat, folder + "/" + mat.name + ".mat");
            return result;
        }
        private static void Transition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            var tr = from.AddTransition(to); tr.hasExitTime = false; tr.duration = .15f; tr.hasFixedDuration = true;
            tr.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
        }
        private static void SmoothSeams(Mesh mesh)
        {
            // Weld normals at UV seams without welding vertices or moving UVs.
            var vertices = mesh.vertices; var normals = mesh.normals;
            var buckets = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i] * 100000f; var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (!buckets.TryGetValue(key, out var list)) { list = new List<int>(); buckets.Add(key, list); } list.Add(i);
            }
            foreach (var list in buckets.Values)
            { Vector3 n = Vector3.zero; foreach (int i in list) n += normals[i]; n.Normalize(); foreach (int i in list) normals[i] = n; }
            mesh.normals = normals;
        }
        private static AnimationClip MakeClip(string kind, Dictionary<string, Transform> transforms, Transform root)
        {
            float duration = kind == "Attaque" ? 1.2f : kind == "Destruction" ? 1f : 2f;
            var clip = new AnimationClip { name = "Guepe_" + kind, frameRate = 60f };
            int frames = Mathf.RoundToInt(duration * 60) + 1;
            foreach (var pair in transforms)
            {
                string name = pair.Key;
                bool animated = name == "Visuel" || name == "Corps" || name == "Abdomen" || name == "Canon"
                    || name == "AntenneG" || name == "AntenneD" || name == "FlammeG" || name == "FlammeD"
                    || name == "AileGAvant" || name == "AileGArriere" || name == "AileDAvant" || name == "AileDArriere";
                if (!animated) continue;
                string path = AnimationUtility.CalculateTransformPath(pair.Value, root);
                var positions = new Vector3[frames]; var rotations = new Quaternion[frames]; var scales = new Vector3[frames];
                for (int i = 0; i < frames; i++)
                {
                    float t = duration * i / (frames-1); Vector3 pos = pair.Value.localPosition, angles = Vector3.zero, scale = Vector3.one;
                    float charge = kind == "Attaque" ? Mathf.Min(Mathf.Clamp01(t/.35f), Mathf.Clamp01((1.2f-t)/.3f)) : 0f;
                    float kick = kind == "Attaque" ? Pulse(t,.58f,.12f) + Pulse(t,.8f,.12f) : 0f;
                    if (name == "Corps") { pos.y += .08f*Mathf.Sin(t*Mathf.PI*2); angles.x = kind == "Vol" ? -10f : -3f; angles.x += charge*8f; pos.z -= kick*.11f; }
                    if (name.StartsWith("Aile")) angles.z = (name.StartsWith("AileG") ? -1 : 1) * (12f+28f*Mathf.Sin(t*Mathf.PI*(kind=="Vol"?16f:12f)));
                    if (name.StartsWith("Antenne")) angles.x = 6f*Mathf.Sin(t*Mathf.PI*4) - charge*15f;
                    if (name == "Abdomen") angles.x = charge*-12f;
                    if (name == "Canon") pos.z -= kick*.13f;
                    if (name.StartsWith("Flamme")) scale.z = (kind == "Vol" ? 1.3f : .8f) + .15f*Mathf.Sin(t*Mathf.PI*14);
                    if (name == "Visuel" && kind == "Destruction")
                    { pos.y -= t*t*.9f; angles = new Vector3(t*130f, t*230f, t*190f); scale *= Mathf.Lerp(1f,.01f,Mathf.SmoothStep(0f,1f,t)); }
                    positions[i] = pos; rotations[i] = Quaternion.Euler(angles); scales[i] = scale;
                }
                for (int a = 0; a < 3; a++)
                {
                    int axis = a; SetCurve(clip,path,"m_LocalPosition."+"xyz"[a],duration,frames,i=>positions[i][axis]);
                    SetCurve(clip,path,"m_LocalScale."+"xyz"[a],duration,frames,i=>scales[i][axis]);
                }
                for (int a = 0; a < 4; a++) { int axis = a; SetCurve(clip,path,"m_LocalRotation."+"xyzw"[a],duration,frames,i=>rotations[i][axis]); }
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = kind == "SurPlace" || kind == "Vol"; AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (kind == "Attaque") AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time=.52f, functionName="FireBuzzProjectile", intParameter=0 },
                new AnimationEvent { time=.74f, functionName="FireBuzzProjectile", intParameter=1 },
                new AnimationEvent { time=1.16f, functionName="FinishBuzzAttack" }
            });
            return clip;
        }
        private static float Pulse(float time, float center, float width) => Mathf.Max(0f,1f-Mathf.Abs(time-center)/width);
        private static void SetCurve(AnimationClip clip,string path,string property,float duration,int count,Func<int,float> sample)
        {
            var curve = new AnimationCurve(); for(int i=0;i<count;i++) curve.AddKey(duration*i/(count-1),sample(i));
            for(int i=0;i<count;i++) { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear); }
            clip.SetCurve(path,typeof(Transform),property,curve);
        }
        private static GameObject Impact(string folder, Material glow)
        {
            var go = new GameObject("Guepe_Impact");
            try
            {
                var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop=false; main.duration=.35f; main.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.5f);
                main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4f); main.startSize=new ParticleSystem.MinMaxCurve(.05f,.15f);
                main.startColor=new Color(1,.5f,.08f); main.gravityModifier=.2f; main.maxParticles=24; main.stopAction=ParticleSystemStopAction.Destroy;
                var emission=ps.emission; emission.rateOverTime=0; emission.SetBursts(new[] {new ParticleSystem.Burst(0,20)});
                var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.1f;
                var renderer=go.GetComponent<ParticleSystemRenderer>();
                var shader=Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
                var glowTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Guepe_Halo",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[32*32];
                for(int y=0;y<32;y++) for(int x=0;x<32;x++)
                {float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),2));}
                glowTexture.SetPixels(pixels);glowTexture.Apply();AssetDatabase.CreateAsset(glowTexture,folder+"/Guepe_Halo.asset");
                var mat=new Material(shader){name="Guepe_Etincelles",mainTexture=glowTexture}; AssetDatabase.CreateAsset(mat,folder+"/Guepe_Etincelles.mat"); renderer.sharedMaterial=mat;
                return PrefabUtility.SaveAsPrefabAsset(go,folder+"/Guepe_Impact.prefab");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        private static BuzzProjectile Projectile(string folder, Material[] mats, GameObject impact)
        {
            var go=new GameObject("Guepe_Projectile");
            try
            {
                var shell=new GameObject("Noyau"); shell.transform.SetParent(go.transform,false);
                var core=GameObject.CreatePrimitive(PrimitiveType.Sphere); core.name="Energie"; core.transform.SetParent(shell.transform,false);
                core.transform.localScale=new Vector3(.28f,.28f,.48f); UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>()); core.GetComponent<Renderer>().sharedMaterial=mats[1];
                var halo=GameObject.CreatePrimitive(PrimitiveType.Sphere); halo.name="Arriere"; halo.transform.SetParent(shell.transform,false);
                halo.transform.localPosition=new Vector3(0,0,-.2f); halo.transform.localScale=new Vector3(.19f,.19f,.45f); UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>()); halo.GetComponent<Renderer>().sharedMaterial=mats[2];
                var trail=go.AddComponent<TrailRenderer>(); trail.time=.16f; trail.minVertexDistance=.04f; trail.startWidth=.16f; trail.endWidth=0f;
                trail.startColor=new Color(1,.8f,.15f); trail.endColor=new Color(1,.15f,.01f,0); trail.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Guepe_Etincelles.mat");
                var shot=go.AddComponent<BuzzProjectile>(); shot.impact=impact; shot.shell=shell.transform;
                return PrefabUtility.SaveAsPrefabAsset(go,folder+"/Guepe_Projectile.prefab").GetComponent<BuzzProjectile>();
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
