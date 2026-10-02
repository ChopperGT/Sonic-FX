using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
internal static class SonicTubeEntryZonesSetup
{
    const string PrefabPath = "Assets/Structure/Tube Slide/Tube_Test.prefab";
    static readonly string Report = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/TubeEntryZones/unity-report.txt");

    static SonicTubeEntryZonesSetup() { EditorApplication.update += Ready; }

    static void Ready()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Ready;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new Exception("Tube_Test.prefab introuvable.");
            if (prefab.GetComponent<SonicTubeEntryZones>() == null)
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    root.AddComponent<SonicTubeEntryZones>();
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (!File.Exists(Report)) Verify();
        }
        catch (Exception e) { WriteReport("FAIL\n" + e); Debug.LogException(e); }
    }

    static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    static void WriteReport(string result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report, result + "\n" + DateTime.Now.ToString("s"));
    }

    [MenuItem("Tools/Sonic FX/Tube/Verifier les zones d'entree")]
    static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject tube = null, sonic = null;
        try
        {
            tube = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            SceneManager.MoveGameObjectToScene(tube, scene);
            var zones = tube.GetComponent<SonicTubeEntryZones>();
            Check(zones != null, "Root controls installed on Tube_Test");
            var entry = zones.FindEntrance(false); var exit = zones.FindEntrance(true);
            Check(entry != null && exit != null && entry != exit, "Both independent entrances available");
            var entrySensor = entry.GetComponent<SphereCollider>(); var exitSensor = exit.GetComponent<SphereCollider>();
            var originalScale = tube.transform.localScale;
            var originalEntryPosition = entry.transform.position;
            var originalExitRadius = exitSensor.radius;
            var spline = entry.splineContainer.Spline;
            int knotCount = spline.Count;
            var knots = new UnityEngine.Splines.BezierKnot[knotCount];
            for (int i = 0; i < knotCount; i++) knots[i] = spline[i];
            var createGate = typeof(SonicTube).GetMethod("CreateGate", BindingFlags.Instance | BindingFlags.NonPublic);
            var updateGate = typeof(SonicTube).GetMethod("UpdateGate", BindingFlags.Instance | BindingFlags.NonPublic);
            createGate.Invoke(entry, null);
            var gate = entry.transform.Find("Barriere_Entree_Automatique").GetComponent<BoxCollider>();
            Vector3 originalGateSize = gate.size;

            SonicTubeZoneHandles.SetRadius(entry, 5f);
            updateGate.Invoke(entry, null);
            Check(entrySensor.radius == 5f && exitSensor.radius == originalExitRadius, "Increasing entry leaves exit unchanged");
            Check(gate.size == originalGateSize, "Increasing detection does not enlarge invisible barrier");
            SonicTubeZoneHandles.SetRadius(entry, 1f);
            SonicTubeZoneHandles.SetRadius(exit, 4f);
            updateGate.Invoke(entry, null);
            Check(entrySensor.radius == 1f && exitSensor.radius == 4f, "Decreasing entry and independently increasing exit");
            Check(gate.size == originalGateSize, "Decreasing detection leaves barrier unchanged");
            SonicTubeZoneHandles.SetRadius(entry, -2f);
            Check(entrySensor.radius == 0.05f, "Invalid small radius is clamped");
            SonicTubeZoneHandles.SetRadius(entry, float.NaN);
            Check(entrySensor.radius == 0.05f, "Non-finite radius rejected");
            Check(entrySensor.isTrigger && exitSensor.isTrigger, "Sensors remain triggers");
            Check(tube.transform.localScale == originalScale && entry.transform.position == originalEntryPosition,
                "Resizing detection preserves tube scale and entrance position");
            Check(spline.Count == knotCount, "Spline knot count unchanged");
            for (int i = 0; i < knotCount; i++) Check(spline[i].Equals(knots[i]), "Spline knot unchanged: " + i);

            sonic = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab"));
            SceneManager.MoveGameObjectToScene(sonic, scene);
            var actions = sonic.GetComponentInChildren<ActionManager>(true);
            Check(actions != null && actions.CanChangeAction(-1), "Internal tube suspension is available");
            actions.ChangeAction(0); actions.ChangeAction(-1);
            Check(actions.Action == -1 && !actions.Action00.enabled && !actions.Action01.enabled, "Tube can suspend normal actions");
            actions.ChangeAction(0);
            Check(actions.Action == 0 && actions.Action00.enabled, "Normal movement restored after tube");
            WriteReport("PASS\nTube_Test: root controls installed; independent entry/exit radii increase and decrease; invalid values handled; invisible barrier, spline and tube transforms preserved. Real Sonic prefab: internal tube suspension and exit restoration work.");
            Debug.Log("Tube_Test : zones d'entree reglables et suspension de Sonic verifiees.");
        }
        catch (Exception e) { WriteReport("FAIL\n" + e); Debug.LogException(e); }
        finally
        {
            if (sonic != null) Object.DestroyImmediate(sonic);
            if (tube != null) Object.DestroyImmediate(tube);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
