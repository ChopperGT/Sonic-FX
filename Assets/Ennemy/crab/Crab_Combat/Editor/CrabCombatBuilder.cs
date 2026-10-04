using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class CrabCombatBuilder
{
    private const string Package = "Assets/Ennemy/Crab_Combat";
    private const string BasePrefab = "Assets/Ennemy/Crab_Animations/Crab_Anime/Crab_Anime.prefab";
    [Serializable] private class AttackData { public float duration; public AttackTrack[] tracks; }
    [Serializable] private class AttackTrack { public string path; public Vector3[] positions; public Quaternion[] rotations; }

    [MenuItem("Tools/Sonic FX/Creer le crabe ennemi")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("Crabe", "Arrete Play avant de creer le prefab.", "OK"); return; }
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab);
        if (template == null)
            throw new InvalidOperationException("Cree d'abord le crabe anime avec Tools > Sonic FX > Creer le crabe anime.");
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Package + "/Editor/CrabAttackData.json");
        if (text == null) throw new InvalidOperationException("CrabAttackData.json manque. Copie le dossier Crab_Combat complet.");
        AttackData data = JsonUtility.FromJson<AttackData>(text.text);
        int enemyLayer = LayerMask.NameToLayer("Enemies"), triggerLayer = LayerMask.NameToLayer("EnemyTrigger");
        if (enemyLayer < 0 || triggerLayer < 0) throw new InvalidOperationException("Couches Enemies/EnemyTrigger introuvables dans ce projet.");
        string folder = AssetDatabase.GenerateUniqueAssetPath(Package + "/Crab_Ennemi");
        AssetDatabase.CreateFolder(Package, System.IO.Path.GetFileName(folder));
        GameObject root = UnityEngine.Object.Instantiate(template);
        root.name = "Crab_Ennemi";
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        try
        {
            // Copy the visual prefab; neither its mesh nor its materials are modified.
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = enemyLayer;
            var oldDriver = root.GetComponent<CrabLocomotionAnimator>();
            if (oldDriver != null) UnityEngine.Object.DestroyImmediate(oldDriver);
            root.tag = "Enemy";
            Animator animator = root.GetComponent<Animator>();
            string originalController = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            string controllerPath = folder + "/Crab_Combat_Animator.controller";
            if (!AssetDatabase.CopyAsset(originalController, controllerPath)) throw new InvalidOperationException("Copie du controleur impossible.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            AnimationClip attack = new AnimationClip { name = "Crab_Tir_Deux_Pinces", frameRate = 30f, legacy = false };
            foreach (var track in data.tracks)
            {
                if (root.transform.Find(track.path) == null) throw new InvalidOperationException("Articulation manquante : " + track.path);
                for (int axis = 0; axis < 3; axis++)
                {
                    int a = axis;
                    Curve(attack, track.path, "m_LocalPosition." + "xyz"[a], track.positions.Length, data.duration, i => track.positions[i][a]);
                }
                for (int axis = 0; axis < 4; axis++)
                {
                    int a = axis;
                    Curve(attack, track.path, "m_LocalRotation." + "xyzw"[a], track.rotations.Length, data.duration, i => track.rotations[i][a]);
                }
            }
            attack.EnsureQuaternionContinuity();
            AnimationUtility.SetAnimationEvents(attack, new[] {
                new AnimationEvent { time = 0.42f, functionName = "FireCrabProjectile", intParameter = 0 },
                new AnimationEvent { time = 0.64f, functionName = "FireCrabProjectile", intParameter = 1 },
                new AnimationEvent { time = 1.1f, functionName = "FinishCrabAttack" }
            });
            var settings = AnimationUtility.GetAnimationClipSettings(attack);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(attack, settings);
            AssetDatabase.CreateAsset(attack, folder + "/Crab_Tir_Deux_Pinces.anim");
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var shooting = machine.AddState("Tir"); shooting.motion = attack; shooting.writeDefaultValues = false;
            var enter = machine.AddAnyStateTransition(shooting);
            enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = 0.04f; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            var leave = shooting.AddTransition(machine.defaultState);
            leave.hasExitTime = true; leave.exitTime = 1f; leave.hasFixedDuration = true; leave.duration = 0.08f;

            // Find muzzles on the animated arms; their transforms follow windup and recoil.
            Transform left = Muzzle(root, "Rig/Body/ArmLeft", "MuzzleLeft", new Vector3(-1.5f, 2.3f, 0.55f));
            Transform right = Muzzle(root, "Rig/Body/ArmRight", "MuzzleRight", new Vector3(1.5f, 2.3f, 0.55f));
            var physicsMaterial = new PhysicsMaterial("Crab_Sans_Friction") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(physicsMaterial, folder + "/Crab_Sans_Friction.physicMaterial");
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f; rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var solid = new GameObject("Collisions"); solid.transform.SetParent(root.transform, false); solid.layer = enemyLayer;
            CapsuleCollider capsule = solid.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.8f, -0.2f); capsule.height = 1.6f; capsule.radius = 0.55f; capsule.sharedMaterial = physicsMaterial;
            var hurtbox = new GameObject("CollisionsTrigger"); hurtbox.transform.SetParent(root.transform, false);
            hurtbox.layer = triggerLayer; hurtbox.tag = "Enemy";
            var trigger = hurtbox.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.center = new Vector3(0, 1.05f, -0.15f); trigger.radius = 0.88f;
            var homing = new GameObject("HomingTarget"); homing.transform.SetParent(root.transform, false);
            homing.transform.localPosition = new Vector3(0, 1.1f, 0); homing.tag = "HomingTarget";
            EnemyHealth health = root.AddComponent<EnemyHealth>(); health.MaxHealth = 1;
            GameObject impact = CreateImpact(folder);
            health.Explosion = impact; // EnemyHealth requires a non-null explosion prefab.
            CrabLobProjectile projectile = CreateProjectile(folder, impact);
            var combat = root.AddComponent<CrabCombatController>();
            combat.leftMuzzle = left; combat.rightMuzzle = right; combat.projectilePrefab = projectile;
            combat.environmentLayers = LayerMask.GetMask("Default");

            // Validate the clip and expand culling bounds around the raised claws.
            SkinnedMeshRenderer renderer = root.GetComponent<SkinnedMeshRenderer>();
            Bounds bounds = renderer.localBounds;
            var saved = new List<(Transform tr, Vector3 pos, Quaternion rot)>();
            foreach (var tr in root.GetComponentsInChildren<Transform>()) saved.Add((tr, tr.localPosition, tr.localRotation));
            for (int k = 0; k <= 24; k++)
            {
                attack.SampleAnimation(root, data.duration * k / 24f);
                Mesh baked = new Mesh(); renderer.BakeMesh(baked); baked.RecalculateBounds();
                if (baked.vertexCount == 0) throw new InvalidOperationException("Maillage anime vide.");
                bounds.Encapsulate(baked.bounds.min); bounds.Encapsulate(baked.bounds.max);
                UnityEngine.Object.DestroyImmediate(baked);
            }
            foreach (var item in saved) { item.tr.localPosition = item.pos; item.tr.localRotation = item.rot; }
            renderer.localBounds = bounds;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Crab_Ennemi.prefab");
            AssetDatabase.SaveAssets(); Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab);
            Debug.Log("Crabe ennemi cree : " + folder + "/Crab_Ennemi.prefab. Glisse-le sur le sol de ton niveau.", prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static Transform Muzzle(GameObject root, string path, string name, Vector3 position)
    {
        Transform bone = root.transform.Find(path);
        if (bone == null) throw new InvalidOperationException("Articulation de pince introuvable : " + path);
        Transform muzzle = new GameObject(name).transform; muzzle.SetParent(bone, false); muzzle.position = position; return muzzle;
    }

    private static Material MaterialAsset(string folder, string name, Color color, float metallic, float emission)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("Shader Standard introuvable.");
        Material mat = new Material(shader) { name = name, color = color };
        mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", 0.65f);
        if (emission > 0f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * emission); }
        AssetDatabase.CreateAsset(mat, folder + "/" + name + ".mat"); return mat;
    }

    private static GameObject CreateImpact(string folder)
    {
        var root = new GameObject("Crab_Impact");
        try
        {
            var ps = root.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 0.3f; main.loop = false; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.55f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.16f); main.startColor = new Color(1f, 0.52f, 0.08f);
            main.gravityModifier = 0.3f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy; main.maxParticles = 24;
            var emission = ps.emission; emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.12f;
            var col = ps.colorOverLifetime; col.enabled = true;
            Gradient gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = gradient;
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            Material spark = new Material(shader) { name = "Crab_Etincelles" }; AssetDatabase.CreateAsset(spark, folder + "/Crab_Etincelles.mat");
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = spark;
            return PrefabUtility.SaveAsPrefabAsset(root, folder + "/Crab_Impact.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static CrabLobProjectile CreateProjectile(string folder, GameObject impact)
    {
        var root = new GameObject("Crab_Projectile");
        try
        {
            var shell = new GameObject("CoqueRotative"); shell.transform.SetParent(root.transform, false);
            Material orange = MaterialAsset(folder, "Crab_Projectile_Orange", new Color(1f, 0.25f, 0.015f), 0.3f, 1.2f);
            Material dark = MaterialAsset(folder, "Crab_Projectile_Metal", new Color(0.065f, 0.08f, 0.11f), 0.8f, 0f);
            Mesh sphere = Sphere(0.175f); sphere.name = "Crab_Projectile_Core";
            Mesh ring = Ring(0.17f, 0.025f); ring.name = "Crab_Projectile_Band";
            AssetDatabase.CreateAsset(sphere, folder + "/Crab_Projectile_Core.asset");
            AssetDatabase.CreateAsset(ring, folder + "/Crab_Projectile_Band.asset");
            Part(shell.transform, "Noyau", sphere, orange, Quaternion.identity);
            Part(shell.transform, "AnneauA", ring, dark, Quaternion.identity);
            Part(shell.transform, "AnneauB", ring, dark, Quaternion.Euler(90, 0, 0));
            var trail = root.AddComponent<TrailRenderer>(); trail.time = 0.22f; trail.minVertexDistance = 0.04f;
            trail.startWidth = 0.13f; trail.endWidth = 0f; trail.startColor = new Color(1, 0.6f, 0.1f, 0.75f); trail.endColor = new Color(1, 0.15f, 0, 0);
            trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Crab_Etincelles.mat");
            var projectile = root.AddComponent<CrabLobProjectile>(); projectile.spinningShell = shell.transform; projectile.trail = trail; projectile.impactEffect = impact;
            return PrefabUtility.SaveAsPrefabAsset(root, folder + "/Crab_Projectile.prefab").GetComponent<CrabLobProjectile>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Part(Transform parent, string name, Mesh mesh, Material material, Quaternion rotation)
    {
        var part = new GameObject(name); part.transform.SetParent(parent, false); part.transform.localRotation = rotation;
        part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Mesh Sphere(float radius)
    {
        const int segments = 16, rings = 10;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int y = 0; y <= rings; y++) for (int x = 0; x <= segments; x++)
        {
            float a = x * Mathf.PI * 2 / segments, b = y * Mathf.PI / rings;
            v.Add(new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)) * radius);
            uv.Add(new Vector2((float)x / segments, (float)y / rings));
        }
        for (int y = 0; y < rings; y++) for (int x = 0; x < segments; x++)
        {
            int a = y * (segments + 1) + x, b = a + segments + 1;
            t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static Mesh Ring(float radius, float tube)
    {
        const int segments = 24, sides = 6;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int x = 0; x <= segments; x++) for (int y = 0; y <= sides; y++)
        {
            float a = x * Mathf.PI * 2 / segments, b = y * Mathf.PI * 2 / sides;
            float r = radius + tube * Mathf.Cos(b);
            v.Add(new Vector3(r * Mathf.Cos(a), tube * Mathf.Sin(b), r * Mathf.Sin(a))); uv.Add(new Vector2((float)x / segments, (float)y / sides));
        }
        for (int x = 0; x < segments; x++) for (int y = 0; y < sides; y++)
        {
            int a = x * (sides + 1) + y, b = a + sides + 1;
            t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static void Curve(AnimationClip clip, string path, string property, int count, float duration, Func<int, float> value)
    {
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++) keys[i] = new Keyframe(duration * i / (count - 1), value(i));
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < count; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }
}
