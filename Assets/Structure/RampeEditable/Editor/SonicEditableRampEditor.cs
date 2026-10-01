using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace SonicFX.Structures.Editor
{
    [CustomEditor(typeof(SonicEditableRamp))]
    public sealed class SonicEditableRampEditor : UnityEditor.Editor
    {
        readonly HashSet<int> selected=new HashSet<int>();
        bool showPoints=true;
        void OnEnable(){Undo.undoRedoPerformed+=OnUndo;}
        void OnDisable(){Undo.undoRedoPerformed-=OnUndo;}
        void OnUndo(){if(target!=null){((SonicEditableRamp)target).Rebuild();Repaint();SceneView.RepaintAll();}}
        static void Changed(SonicEditableRamp r)
        {
            r.Rebuild();EditorUtility.SetDirty(r);
            if(PrefabUtility.IsPartOfPrefabInstance(r))PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            SceneView.RepaintAll();
        }
        Vector3 Center(SonicEditableRamp r){var p=Vector3.zero;foreach(int i in selected)p+=r.Points[i];return selected.Count==0?p:p/selected.Count;}
        void SelectLayer(int axis,int layer)
        {
            selected.Clear();for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++)
                if((axis==0?x:axis==1?y:z)==layer)selected.Add(SonicEditableRamp.Index(x,y,z));
            SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            var r=(SonicEditableRamp)target;
            if(r.SourceMesh==null){EditorGUILayout.HelpBox("Utilise Tools > Sonic FX > Structures > Installer et verifier ramp_C editable.",MessageType.Warning);return;}
            EditorGUILayout.HelpBox("Clique un point orange puis deplace les fleches X / Y / Z. Maj + clic ou Ctrl + clic selectionne plusieurs points. La grille deforme progressivement la rampe, ses materiaux et ses collisions.",MessageType.Info);
            showPoints=EditorGUILayout.Toggle("Afficher les points",showPoints);
            EditorGUI.BeginChangeCheck();
            float verticalScale=r.transform.TransformVector(Vector3.up).magnitude;
            float depth=Mathf.Max(0,EditorGUILayout.FloatField("Profondeur du mur (metres)",r.wallDepth*verticalScale));
            if(EditorGUI.EndChangeCheck() && verticalScale>.00001f){Undo.RecordObject(r,"Profondeur du mur de rampe");r.wallDepth=depth/verticalScale;Changed(r);}
            EditorGUILayout.HelpBox("La profondeur ajoute un mur sous la rampe jusqu'a une base plate, sans changer la piste. La poignee bleue permet de tirer le mur vers le bas. Zero restaure le dessous d'origine.",MessageType.None);
            EditorGUI.BeginChangeCheck();
            var scale=r.transform.lossyScale;scale=new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            var size=Vector3.Scale(r.ControlBounds.size,scale);
            size=EditorGUILayout.Vector3Field("Dimensions locales (metres)",size);
            if(EditorGUI.EndChangeCheck() && Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))>.00001f){
                Undo.RecordObject(r,"Redimensionner la rampe");r.Resize(new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z));Changed(r);
            }
            EditorGUILayout.LabelField("Selection rapide",EditorStyles.boldLabel);
            for(int axis=0;axis<3;axis++){
                EditorGUILayout.BeginHorizontal();string a=axis==0?"X":axis==1?"Y":"Z";
                for(int layer=0;layer<3;layer++)if(GUILayout.Button(a+(layer==0?" -":layer==1?" milieu":" +")))SelectLayer(axis,layer);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Tout selectionner")){selected.Clear();for(int i=0;i<27;i++)selected.Add(i);SceneView.RepaintAll();}
            if(GUILayout.Button("Deselectionner")){selected.Clear();SceneView.RepaintAll();}
            EditorGUILayout.EndHorizontal();
            if(selected.Count>0){
                EditorGUILayout.LabelField(selected.Count+" point(s) selectionne(s)",EditorStyles.boldLabel);
                Vector3 center=Center(r);EditorGUI.BeginChangeCheck();
                Vector3 moved=EditorGUILayout.Vector3Field("Centre local du groupe",center);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(r,"Deplacer les points de rampe");r.MovePoints(selected,moved-center);Changed(r);}
            }
            if(GUILayout.Button("Retrouver la forme originale")){Undo.RecordObject(r,"Reinitialiser la rampe");r.ResetShape();Changed(r);}
            if(!string.IsNullOrEmpty(r.LastError))EditorGUILayout.HelpBox(r.LastError,MessageType.Warning);
            EditorGUILayout.HelpBox("Y + : haut de la rampe. Les tranches X / Z permettent d'etirer une extremite ou de courber les cotes. Ctrl + Z annule. Chaque exemplaire garde sa propre forme.",MessageType.None);
        }
        void OnSceneGUI()
        {
            var r=(SonicEditableRamp)target;if(!showPoints || r.Points==null || r.Points.Length!=27)return;
            Transform t=r.transform;Handles.color=new Color(0,1,1,.35f);
            var bounds=r.ControlBounds;
            Vector3 baseLocal=new Vector3(bounds.center.x,r.OriginalBottomY-r.wallDepth,bounds.center.z);
            Vector3 baseWorld=t.TransformPoint(baseLocal);
            Handles.color=new Color(.2f,.55f,1);Handles.Label(baseWorld,"Profondeur du mur");
            EditorGUI.BeginChangeCheck();
            Vector3 dragged=Handles.Slider(baseWorld,-t.up,HandleUtility.GetHandleSize(baseWorld)*.18f,Handles.CubeHandleCap,0);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(r,"Etirer le mur sous la rampe");r.wallDepth=Mathf.Max(0,r.OriginalBottomY-t.InverseTransformPoint(dragged).y);Changed(r);
            }
            Handles.color=new Color(0,1,1,.35f);
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++){
                Vector3 p=t.TransformPoint(r.Points[SonicEditableRamp.Index(x,y,z)]);
                if(x<2)Handles.DrawLine(p,t.TransformPoint(r.Points[SonicEditableRamp.Index(x+1,y,z)]));
                if(y<2)Handles.DrawLine(p,t.TransformPoint(r.Points[SonicEditableRamp.Index(x,y+1,z)]));
                if(z<2)Handles.DrawLine(p,t.TransformPoint(r.Points[SonicEditableRamp.Index(x,y,z+1)]));
            }
            for(int i=0;i<27;i++){
                var p=t.TransformPoint(r.Points[i]);float s=HandleUtility.GetHandleSize(p)*.06f;
                Handles.color=selected.Contains(i)?Color.yellow:new Color(1,.5f,.1f);
                bool toggle=Event.current.shift || Event.current.control || Event.current.command;
                if(Handles.Button(p,Quaternion.identity,s,s*1.3f,Handles.SphereHandleCap)){
                    if(!toggle)selected.Clear();if(!selected.Add(i))selected.Remove(i);Repaint();
                }
            }
            if(selected.Count==0)return;
            Vector3 center=Center(r),world=t.TransformPoint(center);
            EditorGUI.BeginChangeCheck();Vector3 moved=Handles.PositionHandle(world,Tools.pivotRotation==PivotRotation.Local?t.rotation:Quaternion.identity);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(r,"Deplacer les points de rampe");r.MovePoints(selected,t.InverseTransformPoint(moved)-center);Changed(r);
            }
        }
    }
}
