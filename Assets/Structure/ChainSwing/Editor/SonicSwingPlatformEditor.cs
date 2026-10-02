using UnityEditor;
using UnityEngine;

namespace SonicFX.Structures.Editor
{
    [CustomEditor(typeof(SonicSwingPlatform)),CanEditMultipleObjects]
    public sealed class SonicSwingPlatformEditor : UnityEditor.Editor
    {
        bool preview;
        double previewStart;
        void OnEnable(){EditorApplication.update+=Animate;Undo.undoRedoPerformed+=RefreshPlatforms;}
        void OnDisable(){EditorApplication.update-=Animate;Undo.undoRedoPerformed-=RefreshPlatforms;foreach(var t in targets)if(t!=null && !Application.isPlaying)((SonicSwingPlatform)t).PlaceAtAngle(0,false);}
        void RefreshPlatforms(){foreach(var t in targets)if(t!=null)((SonicSwingPlatform)t).Rebuild();SceneView.RepaintAll();Repaint();}
        static void ResizePlatform(SonicSwingPlatform p,Vector2 metres)
        {
            Undo.RecordObjects(new Object[]{p,p.Platform},"Taille de la plateforme seule");
            p.SetWorldPlatformSize(metres);
            PrefabUtility.RecordPrefabInstancePropertyModifications(p);
            PrefabUtility.RecordPrefabInstancePropertyModifications(p.Platform);
            EditorUtility.SetDirty(p);EditorUtility.SetDirty(p.Platform);
        }
        void Animate()
        {
            if(!preview || Application.isPlaying)return;
            foreach(var t in targets)if(t!=null){var p=(SonicSwingPlatform)t;p.PlaceAtAngle(p.AngleAt((float)(EditorApplication.timeSinceStartup-previewStart)),false);}
            SceneView.RepaintAll();Repaint();
        }
        void Property(string name,string label){EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label));}
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Chaines et longueur",EditorStyles.boldLabel);
            Property("linkCount","Nombre de maillons");
            if(targets.Length==1){var p=(SonicSwingPlatform)target;EditorGUI.BeginChangeCheck();float value=EditorGUILayout.FloatField("Longueur Y (metres)",p.WorldLength);if(EditorGUI.EndChangeCheck()){
                serializedObject.ApplyModifiedProperties();Undo.RecordObject(p,"Longueur des chaines");p.SetWorldLength(Mathf.Max(.01f,value));p.Rebuild();PrefabUtility.RecordPrefabInstancePropertyModifications(p);EditorUtility.SetDirty(p);serializedObject.Update();
            }}
            EditorGUILayout.Space();EditorGUILayout.LabelField("Plateforme seule",EditorStyles.boldLabel);
            if(targets.Length==1 && ((SonicSwingPlatform)target).Platform!=null){
                var p=(SonicSwingPlatform)target;var dimensions=p.WorldPlatformSize;
                EditorGUI.BeginChangeCheck();
                dimensions.x=EditorGUILayout.FloatField("Largeur X (metres)",dimensions.x);
                dimensions.y=EditorGUILayout.FloatField("Profondeur Z (metres)",dimensions.y);
                if(EditorGUI.EndChangeCheck()){
                    serializedObject.ApplyModifiedProperties();ResizePlatform(p,dimensions);serializedObject.Update();
                }
            }else Property("platformSizeScale","Taille X/Z (multiplicateurs)");
            EditorGUILayout.Space();EditorGUILayout.LabelField("Balancement",EditorStyles.boldLabel);
            Property("swingEnabled","Activer le balancement");Property("amplitude","Amplitude (degres)");Property("period","Aller-retour (secondes)");Property("direction","Direction X/Z (degres)");Property("phase","Decalage de depart (degres)");Property("carryPlayer","Transporter Sonic");
            if(serializedObject.ApplyModifiedProperties())foreach(var t in targets){var p=(SonicSwingPlatform)t;p.Rebuild();}
            EditorGUILayout.HelpBox("Dans la vue Scene, les quatre points aux bords de la planche reglent sa largeur X et sa profondeur Z, autour de son centre. La poignee Y sous la planche ajoute ou retire des maillons. Les collisions suivent la taille de la planche et son epaisseur reste identique.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying || EditorUtility.IsPersistent(target)))if(GUILayout.Button(preview?"Arreter l'apercu":"Apercu du balancement")){
                preview=!preview;previewStart=EditorApplication.timeSinceStartup;
                if(!preview)foreach(var t in targets)((SonicSwingPlatform)t).PlaceAtAngle(0,false);
            }
        }
        void OnSceneGUI()
        {
            if(target==null || Application.isPlaying || preview)return;
            var p=(SonicSwingPlatform)target;var start=p.AnchorPosition;var rest=p.RestPosition();
            DrawPlatformSizeHandles(p);
            Handles.color=new Color(.2f,.75f,1);Handles.DrawDottedLine(start,rest,5);
            float size=HandleUtility.GetHandleSize(rest)*.15f;
            Handles.Label(rest+Vector3.down*size*2,"Longueur Y : "+p.WorldLength.ToString("0.00")+" m");
            EditorGUI.BeginChangeCheck();var point=Handles.Slider(rest,Vector3.down,size,Handles.CubeHandleCap,0);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(p,"Ajouter / retirer des maillons");p.SetWorldLength(p.WorldLength+Vector3.Dot(point-rest,Vector3.down));p.Rebuild();PrefabUtility.RecordPrefabInstancePropertyModifications(p);EditorUtility.SetDirty(p);
            }
            Handles.color=new Color(1,.7f,.1f,.7f);
            var offset=rest-start+Vector3.up*p.WorldLength;
            var pivot=start+offset;var normal=Vector3.Cross(Vector3.down,p.SwingDirection);
            var left=p.PositionAtAngle(-p.amplitude);var right=p.PositionAtAngle(p.amplitude);
            Handles.DrawWireArc(pivot,normal,(left-pivot).normalized,p.amplitude*2,p.WorldLength);
            Handles.DrawDottedLine(pivot,left,5);Handles.DrawDottedLine(pivot,right,5);
        }
        static void DrawPlatformSizeHandles(SonicSwingPlatform p)
        {
            if(p.Platform==null)return;
            var bounds=p.PlatformLocalBounds;
            for(int axis=0;axis<2;axis++)for(int side=-1;side<=1;side+=2){
                var local=bounds.center;local.y=bounds.max.y;
                if(axis==0)local.x+=side*bounds.extents.x;else local.z+=side*bounds.extents.z;
                var point=p.Platform.TransformPoint(local);
                var direction=(axis==0?p.Platform.right:p.Platform.forward)*side;
                Handles.color=axis==0?new Color(1,.35f,.3f):new Color(.3f,.55f,1);
                float size=HandleUtility.GetHandleSize(point)*.08f;
                Handles.Label(point+Vector3.up*size*2,axis==0?"Largeur X":"Profondeur Z");
                EditorGUI.BeginChangeCheck();
                var moved=Handles.Slider(point,direction,size,Handles.DotHandleCap,0);
                if(EditorGUI.EndChangeCheck()){
                    var dimensions=p.WorldPlatformSize;
                    dimensions[axis]=Mathf.Max(.01f,dimensions[axis]+2*Vector3.Dot(moved-point,direction));
                    ResizePlatform(p,dimensions);
                }
            }
        }
    }
}
