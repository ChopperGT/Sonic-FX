using UnityEditor;
using UnityEngine;

namespace SonicFX.Structures.Editor
{
    [CustomEditor(typeof(SonicWideSpring))]
    public sealed class SonicWideSpringEditor : UnityEditor.Editor
    {
        void OnEnable(){Undo.undoRedoPerformed+=Restore;}
        void OnDisable(){Undo.undoRedoPerformed-=Restore;}
        void Restore(){if(target!=null && !EditorUtility.IsPersistent(target)){((SonicWideSpring)target).Refresh();Repaint();SceneView.RepaintAll();}}
        public override void OnInspectorGUI()
        {
            var spring=(SonicWideSpring)target;
            bool persistent=EditorUtility.IsPersistent(target);
            EditorGUILayout.HelpBox("Etire l'axe X ou les poignees orange : les etoiles et ressorts sont ajoutes ou retires automatiquement. Leur taille reste constante. Chaque emplacement utilise le Spring du jeu.",MessageType.Info);
            if(persistent)EditorGUILayout.HelpBox("Glisse ce prefab dans la Scene ou ouvre-le par un double-clic pour modifier ses reglages.",MessageType.None);
            using(new EditorGUI.DisabledScope(persistent))
            {
                serializedObject.Update();EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("width"),new GUIContent("Largeur de base (metres)"));
                EditorGUILayout.LabelField("Emplacements actifs",spring.SlotCount+" / "+SonicWideSpring.MaximumSlots);
                EditorGUILayout.LabelField("Largeur effective",spring.ActualWidth.ToString("F2")+" m");
                EditorGUILayout.Space();EditorGUILayout.LabelField("Propulsion — comme Spring",EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("springForce"),new GUIContent("Force de propulsion"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("isAdditive"),new GUIContent("Ajouter a la vitesse actuelle"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lockControl"),new GUIContent("Bloquer les commandes"));
                if(serializedObject.FindProperty("lockControl").boolValue)EditorGUILayout.PropertyField(serializedObject.FindProperty("lockTime"),new GUIContent("Duree du blocage (frames)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("activationCooldown"),new GUIContent("Delai entre activations (secondes)"));
                EditorGUILayout.Space();EditorGUILayout.LabelField("Animation",EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("animationDuration"),new GUIContent("Duree (secondes)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("compression"),new GUIContent("Compression du ressort"));
                bool change=EditorGUI.EndChangeCheck();serializedObject.ApplyModifiedProperties();
                if(change){spring.Refresh(true);SceneView.RepaintAll();}
            }
            EditorGUILayout.HelpBox("La largeur avance par emplacements entiers (1 a 32). Y / Z reglent la hauteur et la profondeur. Tourne l'objet pour changer la direction de propulsion. Les emplacements retires sont caches et leurs collisions sont desactivees.",MessageType.None);
        }
        void OnSceneGUI()
        {
            var p=(SonicWideSpring)target;if(EditorUtility.IsPersistent(p))return;
            var t=p.transform;Vector3 axis=t.TransformVector(Vector3.right).normalized;
            Handles.color=new Color(1,.55f,.05f);
            foreach(int side in new[]{-1,1})
            {
                var position=t.position+axis*p.ActualWidth*.5f*side+t.TransformVector(Vector3.up*1.5f);
                Handles.Label(position,"Largeur / emplacements");EditorGUI.BeginChangeCheck();
                var moved=Handles.Slider(position,axis*side,HandleUtility.GetHandleSize(position)*.16f,Handles.SphereHandleCap,0);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(p,"Etirer le Spring large");float half=p.ActualWidth*.5f+Vector3.Dot(moved-position,axis)*side;
                    p.width=Mathf.Max(1,half*2/p.AxisScale);p.Refresh(true);EditorUtility.SetDirty(p);
                }
            }
        }
    }
}
