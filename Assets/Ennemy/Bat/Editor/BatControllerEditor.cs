using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
namespace SonicFX.Bat.Editor
{
    [CustomEditor(typeof(BatController))]
    class BatControllerEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle zone=new BoxBoundsHandle();
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();EditorGUILayout.HelpBox("Placer sous un plafond avec collider. Orange : zone surveillee ; cyan : portee du hurlement. Les sauts restent normaux quand Sonic porte des chauves-souris. Boule (R1) remplit la barre et libere une chauve-souris a la fois.",MessageType.Info);
            var bat=(BatController)target;
            if(!Application.isPlaying && GUILayout.Button("Accrocher au plafond maintenant"))
            {
                if(bat.FindCeiling(bat.transform.position,out var point)){Undo.RecordObject(bat.transform,"Accrocher la chauve-souris");bat.transform.position=point;PrefabUtility.RecordPrefabInstancePropertyModifications(bat.transform);}
                else Debug.LogWarning("Aucun plafond trouve : verifier sa couche et Recherche du plafond.",bat);
            }
        }
        void OnSceneGUI()
        {
            if(Application.isPlaying)return;var bat=(BatController)target;
            using(new Handles.DrawingScope(new Color(1,.6f,.08f),Matrix4x4.TRS(bat.transform.position,bat.transform.rotation,Vector3.one)))
            {
                zone.center=bat.watchCenter;zone.size=bat.watchSize;EditorGUI.BeginChangeCheck();zone.DrawHandle();
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(bat,"Zone de la chauve-souris");bat.watchCenter=zone.center;bat.watchSize=zone.size;PrefabUtility.RecordPrefabInstancePropertyModifications(bat);EditorUtility.SetDirty(bat);}
            }
        }
    }
}
