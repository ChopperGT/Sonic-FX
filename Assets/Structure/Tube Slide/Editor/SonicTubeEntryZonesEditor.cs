using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SonicTubeEntryZones)), CanEditMultipleObjects]
public sealed class SonicTubeEntryZonesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Agrandissez les zones pour faciliter l'entree de Sonic en boule. Les deux cotes se reglent separement. Dans Scene, tirez les poignees colorees. Le tube et sa courbe gardent leur taille.", MessageType.Info);
        foreach (var item in targets)
        {
            var zones = (SonicTubeEntryZones)item;
            if (targets.Length > 1) EditorGUILayout.LabelField(zones.name, EditorStyles.boldLabel);
            SonicTubeZoneHandles.Inspector(zones.FindEntrance(false), "Rayon entree");
            SonicTubeZoneHandles.Inspector(zones.FindEntrance(true), "Rayon sortie");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Vitesse minimale dans les montees", EditorStyles.boldLabel);
            SonicTubeZoneHandles.MinimumSpeedInspector(zones.FindEntrance(false), "Depuis l'entree");
            SonicTubeZoneHandles.MinimumSpeedInspector(zones.FindEntrance(true), "Depuis la sortie");
            EditorGUILayout.HelpBox("Ce reglage evite de s'arreter dans une montee. Il ne definit pas une vitesse obligatoire pour entrer et n'accelere pas une entree plus lente.", MessageType.Info);
        }
    }

    void OnSceneGUI()
    {
        var zones = (SonicTubeEntryZones)target;
        SonicTubeZoneHandles.Draw(zones.FindEntrance(false), "Entree", Color.cyan);
        SonicTubeZoneHandles.Draw(zones.FindEntrance(true), "Sortie", new Color(1f, 0.65f, 0.1f));
    }
}

[CustomEditor(typeof(SonicTube)), CanEditMultipleObjects]
public sealed class SonicTubeEntranceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        foreach (var item in targets)
            SonicTubeZoneHandles.Inspector((SonicTube)item, "Rayon detection");
        DrawDefaultInspector();
    }

    void OnSceneGUI()
    {
        var entrance = (SonicTube)target;
        SonicTubeZoneHandles.Draw(entrance, entrance.entranceAtEnd ? "Sortie" : "Entree", Color.cyan);
    }
}

internal static class SonicTubeZoneHandles
{
    internal static void MinimumSpeedInspector(SonicTube entrance, string label)
    {
        if (entrance == null) return;
        var settings = new SerializedObject(entrance);
        settings.Update();
        var speed = settings.FindProperty("minimumSpeed");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(speed, new GUIContent(label,
            "Vitesse minimale conservee dans les montees, limitee a la vitesse reelle d'entree si elle est plus lente."));
        if (EditorGUI.EndChangeCheck())
        {
            speed.floatValue = Mathf.Max(0.1f, speed.floatValue);
            settings.ApplyModifiedProperties();
        }
    }

    internal static void Inspector(SonicTube entrance, string label)
    {
        if (entrance == null)
        {
            EditorGUILayout.HelpBox(label + " : aucune extremite trouvee.", MessageType.Warning);
            return;
        }
        var sensor = entrance.GetComponent<SphereCollider>();
        if (sensor == null) return;
        EditorGUI.BeginChangeCheck();
        float radius = EditorGUILayout.FloatField(new GUIContent(label,
            "Rayon local de la Sphere Collider, en unites Unity. La taille du tube ne change pas."), sensor.radius);
        if (EditorGUI.EndChangeCheck()) SetRadius(entrance, radius);
    }

    internal static void SetRadius(SonicTube entrance, float radius)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius)) return;
        var sensor = entrance.GetComponent<SphereCollider>();
        radius = Mathf.Max(0.05f, radius);
        if (Mathf.Approximately(sensor.radius, radius)) return;
        Undo.RecordObjects(new Object[] { entrance, sensor }, "Redimensionner la zone d'entree du tube");
        // Keep the standing barrier unchanged when editing only the detection.
        if (entrance.standingBarrierRadius <= 0f) entrance.standingBarrierRadius = sensor.radius;
        sensor.radius = radius;
        PrefabUtility.RecordPrefabInstancePropertyModifications(entrance);
        PrefabUtility.RecordPrefabInstancePropertyModifications(sensor);
        EditorUtility.SetDirty(entrance);
        EditorUtility.SetDirty(sensor);
        SceneView.RepaintAll();
    }

    internal static void Draw(SonicTube entrance, string label, Color color)
    {
        if (entrance == null) return;
        var sensor = entrance.GetComponent<SphereCollider>();
        if (sensor == null) return;
        Vector3 scale = entrance.transform.lossyScale;
        float scaleFactor = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        if (scaleFactor < 0.00001f) return;
        Vector3 centre = entrance.transform.TransformPoint(sensor.center);
        using (new Handles.DrawingScope(color, Matrix4x4.identity))
        {
            Handles.Label(centre + Vector3.up * sensor.radius * scaleFactor, label + " : detection");
            EditorGUI.BeginChangeCheck();
            float radius = Handles.RadiusHandle(Quaternion.identity, centre, sensor.radius * scaleFactor);
            if (EditorGUI.EndChangeCheck()) SetRadius(entrance, radius / scaleFactor);
        }
    }
}
