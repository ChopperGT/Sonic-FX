using UnityEditor;
using UnityEngine;
namespace SonicFX.Lava.Editor
{
    [CustomEditor(typeof(SonicLavaGeyser))]
    public sealed class SonicLavaGeyserEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var g=(SonicLavaGeyser)target;
            if(g.LastRefusal!=null)EditorGUILayout.HelpBox(g.LastRefusal,MessageType.Warning);
            EditorGUILayout.HelpBox("Declenchement : redimensionner l'enfant Zone_Declenchement (Box Collider). Chutes : deplacer les Zones_Chute avec les poignees orange. Les rochers restent apres impact si leur duree vaut 0.",MessageType.Info);
            if(!g.gameObject.scene.IsValid()){EditorGUILayout.HelpBox("Glisser ce prefab dans la scene pour regler ses zones.",MessageType.Info);return;}
            if(GUILayout.Button("Ajouter une zone de chute"))
            {
                Undo.RecordObject(g,"Ajouter une cible de geyser");var go=new GameObject("Zone_Chute_"+(g.landingZones.Length+1));Undo.RegisterCreatedObjectUndo(go,"Ajouter une cible de geyser");go.transform.SetParent(g.transform,false);go.transform.localPosition=new Vector3(8+g.landingZones.Length*3,0,0);
                var z=go.AddComponent<SonicGeyserLandingZone>();var list=new System.Collections.Generic.List<SonicGeyserLandingZone>(g.landingZones);list.Add(z);g.landingZones=list.ToArray();EditorUtility.SetDirty(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g);Selection.activeGameObject=go;
            }
            if(GUILayout.Button("Aligner le geyser sur la lave"))
            {
                if(g.FindLava()){Undo.RecordObject(g.transform,"Aligner le geyser sur la lave");Vector3 p=g.lava.transform.InverseTransformPoint(g.transform.position);p.y=0;g.transform.position=g.lava.transform.TransformPoint(p);EditorUtility.SetDirty(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g);PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);}
                else EditorUtility.DisplayDialog("Geyser","Aucune lave proche. Placer le geyser a moins de la distance de surface reglee dans l'Inspector.","OK");
            }
            using(new EditorGUI.DisabledScope(!Application.isPlaying))if(GUILayout.Button("Tester l'eruption"))if(!g.TryErupt())Debug.LogWarning(g.LastRefusal,g);
        }
        void OnSceneGUI()
        {
            var g=(SonicLavaGeyser)target;if(g.landingZones==null)return;
            foreach(var z in g.landingZones)if(z!=null){Handles.color=new Color(1,.35f,.02f);Handles.Label(z.transform.position+Vector3.up,z.name);EditorGUI.BeginChangeCheck();var p=Handles.PositionHandle(z.transform.position,Quaternion.identity);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(z.transform,"Deplacer une chute de geyser");z.transform.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(z.transform);}Handles.DrawDottedLine(g.transform.position,z.transform.position,5);}
        }
    }
    [CustomEditor(typeof(SonicGeyserLandingZone))]
    public sealed class SonicGeyserLandingZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){DrawDefaultInspector();EditorGUILayout.HelpBox("Rayon = dispersion des rochers dans cette zone. Taille minimale/maximale = taille des rochers. Un rayon de 0 vise exactement le centre. Decocher la projection pour imposer manuellement la hauteur d'impact.",MessageType.Info);}
        void OnSceneGUI()
        {
            var z=(SonicGeyserLandingZone)target;Handles.color=new Color(1,.3f,.03f);Handles.DrawWireDisc(z.transform.position,z.transform.up,z.radius*Mathf.Max(z.transform.lossyScale.x,z.transform.lossyScale.z));
            EditorGUI.BeginChangeCheck();var matrix=Handles.matrix;Handles.matrix=z.transform.localToWorldMatrix;float r=Handles.RadiusHandle(Quaternion.identity,Vector3.zero,z.radius);Handles.matrix=matrix;
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(z,"Taille zone de chute");z.radius=Mathf.Max(0,r);EditorUtility.SetDirty(z);PrefabUtility.RecordPrefabInstancePropertyModifications(z);}
        }
    }
}
