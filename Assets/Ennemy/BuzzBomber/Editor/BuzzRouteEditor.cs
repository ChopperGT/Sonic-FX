using UnityEngine;
using UnityEditor;
using System.Linq;

namespace SonicFX.Buzz.Editor
{
    [CustomEditor(typeof(BuzzRoute))]
    public class BuzzRouteEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Deplace les points avec W dans la scene. Selectionne un point pour regler sa vitesse et sa pause. Deplace le parent Guepe_Robot_Parcours pour deplacer tout le systeme.", MessageType.Info);
            var route = (BuzzRoute)target;
            if (GUILayout.Button("Ajouter un point a la fin"))
            {
                Undo.RecordObject(route, "Ajouter un point");
                var existing = route.points == null ? new BuzzRoutePoint[0] : route.points.Where(p=>p!=null).ToArray();
                var go = new GameObject("Point_" + (existing.Length + 1)); Undo.RegisterCreatedObjectUndo(go,"Ajouter un point");
                go.transform.SetParent(route.transform,false);
                go.transform.position = existing.Length > 0 ? existing[existing.Length-1].transform.position + route.transform.right*4f : route.transform.position + Vector3.up*4f;
                var point=go.AddComponent<BuzzRoutePoint>(); route.points=existing.Concat(new[]{point}).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(route); EditorUtility.SetDirty(route); Selection.activeGameObject=go;
            }
            if (GUILayout.Button("Nettoyer les points supprimes"))
            {
                Undo.RecordObject(route,"Nettoyer le parcours"); route.points=(route.points??new BuzzRoutePoint[0]).Where(p=>p!=null).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(route); EditorUtility.SetDirty(route);
            }
        }
        private void OnSceneGUI()
        {
            var route=(BuzzRoute)target; if(route.points==null)return;
            for(int i=0;i<route.points.Length;i++)
            {
                var p=route.points[i]; if(p==null)continue;
                Handles.color=Color.cyan; Handles.Label(p.transform.position+Vector3.up*.4f,"Point "+(i+1)+"  /  "+p.pause+" s");
                EditorGUI.BeginChangeCheck(); Vector3 pos=Handles.PositionHandle(p.transform.position,Quaternion.identity);
                if(EditorGUI.EndChangeCheck()) {Undo.RecordObject(p.transform,"Deplacer le point");p.transform.position=pos;PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);}
            }
        }
    }
}
