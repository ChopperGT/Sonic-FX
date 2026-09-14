using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class CrabAnimationBuilder
{
    private const string Package = "Assets/Ennemy/Crab_Animations";
    [Serializable] private class RigData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] weights;
        public int[] metalTriangles;
        public int[] armorTriangles;
        public Joint[] bones;
        public MotionData[] motions;
    }
    [Serializable] private class Joint { public string name; public int parent; public Vector3 pivot; }
    [Serializable] private class MotionData { public string name; public float duration; public Track[] tracks; }
    [Serializable] private class Track { public int bone; public Vector3[] positions; public Quaternion[] rotations; }

    [MenuItem("Tools/Sonic FX/Creer le crabe anime")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Crabe", "Arrete Play avant de creer le prefab.", "OK");
            return;
        }
        TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(Package + "/Editor/CrabRigData.json");
        if (source == null) throw new InvalidOperationException("CrabRigData.json manque. Reimporte le paquet complet.");
        RigData data = JsonUtility.FromJson<RigData>(source.text);
        if (data.vertices.Length != data.weights.Length || data.vertices.Length != data.uv.Length
            || data.vertices.Length != data.normals.Length || data.bones.Length != 15)
            throw new InvalidOperationException("Donnees du crabe incompletes.");

        Material metal = AssetDatabase.LoadAssetAtPath<Material>(Package + "/Materials/Crab_Metal.mat");
        Material armor = AssetDatabase.LoadAssetAtPath<Material>(Package + "/Materials/Crab_Carapace.mat");
        if (metal == null || armor == null) throw new InvalidOperationException("Les materiaux du paquet sont manquants.");

        string folder = AssetDatabase.GenerateUniqueAssetPath(Package + "/Crab_Anime");
        AssetDatabase.CreateFolder(Package, System.IO.Path.GetFileName(folder));
        GameObject root = new GameObject("Crab_Anime");
        try
        {
            Transform rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            Transform[] bones = new Transform[data.bones.Length];
            string[] paths = new string[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                Joint joint = data.bones[i];
                if (joint.parent >= i) throw new InvalidOperationException("Hierarchie invalide.");
                bones[i] = new GameObject(joint.name).transform;
                Transform parent = joint.parent < 0 ? rig : bones[joint.parent];
                bones[i].SetParent(parent, false);
                bones[i].position = joint.pivot;
                paths[i] = AnimationUtility.CalculateTransformPath(bones[i], root.transform);
            }

            Mesh mesh = new Mesh { name = "Crab_Rigged", vertices = data.vertices, normals = data.normals, uv = data.uv };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(data.metalTriangles, 0);
            mesh.SetTriangles(data.armorTriangles, 1);
            BoneWeight[] weights = new BoneWeight[data.vertices.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                if (data.weights[i] < 0 || data.weights[i] >= bones.Length) throw new InvalidOperationException("Poids invalide.");
                weights[i] = new BoneWeight { boneIndex0 = data.weights[i], weight0 = 1f };
            }
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            mesh.boneWeights = weights;
            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, folder + "/Crab_Rigged.asset");

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = rig;
            renderer.sharedMaterials = new[] { metal, armor };
            Bounds expanded = mesh.bounds;
            expanded.Expand(1.2f);
            renderer.localBounds = expanded;
            renderer.updateWhenOffscreen = false;

            var clips = new AnimationClip[data.motions.Length];
            for (int m = 0; m < clips.Length; m++)
            {
                MotionData motion = data.motions[m];
                AnimationClip clip = new AnimationClip { name = motion.name, frameRate = 30f, legacy = false };
                foreach (Track track in motion.tracks)
                {
                    string path = paths[track.bone];
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int axis = channel;
                        SetCurve(clip, path, "m_LocalPosition." + "xyz"[axis], track.positions.Length, motion.duration, k => track.positions[k][axis]);
                    }
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int axis = channel;
                        SetCurve(clip, path, "m_LocalRotation." + "xyzw"[axis], track.rotations.Length, motion.duration, k => track.rotations[k][axis]);
                    }
                }
                clip.EnsureQuaternionContinuity();
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, folder + "/" + motion.name + ".anim");
                clips[m] = clip;
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Crab_Animator.controller");
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "WalkRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Repos"); idle.motion = clips[0];
            var walk = machine.AddState("Marche"); walk.motion = clips[1];
            idle.writeDefaultValues = false; walk.writeDefaultValues = false;
            walk.speedParameter = "WalkRate"; walk.speedParameterActive = true;
            machine.defaultState = idle;
            var start = idle.AddTransition(walk); start.hasExitTime = false; start.hasFixedDuration = true; start.duration = 0.12f;
            start.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            var stop = walk.AddTransition(idle); stop.hasExitTime = false; stop.hasFixedDuration = true; stop.duration = 0.15f;
            stop.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.AddComponent<CrabLocomotionAnimator>();

            // Sample both animations to validate binding and include all motion in the bounds.
            foreach (var clip in clips)
                for (int frame = 0; frame <= 16; frame++)
                {
                    clip.SampleAnimation(root, clip.length * frame / 16f);
                    Mesh baked = new Mesh();
                    renderer.BakeMesh(baked);
                    if (baked.vertexCount != mesh.vertexCount) throw new InvalidOperationException("Validation du skinning echouee.");
                    expanded.Encapsulate(baked.bounds.min); expanded.Encapsulate(baked.bounds.max);
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localRotation = Quaternion.identity;
                bones[i].position = data.bones[i].pivot;
            }
            renderer.localBounds = expanded;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Crab_Anime.prefab");
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Crabe anime cree : " + folder + "/Crab_Anime.prefab. Glisse-le dans la scene ; coche Preview Walking pendant Play pour tester.", prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void SetCurve(AnimationClip clip, string path, string property, int count, float duration, Func<int, float> sample)
    {
        var keys = new Keyframe[count];
        for (int i = 0; i < count; i++) keys[i] = new Keyframe(duration * i / (count - 1), sample(i));
        var curve = new AnimationCurve(keys);
        for (int i = 0; i < count; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }
}
