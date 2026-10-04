using UnityEditor;
using UnityEngine;

namespace SonicFX.Structures.Editor
{
    // Editor preference shared by the control-point tools; never changes the mesh.
    internal static class SonicStructurePointHandles
    {
        const string PreferenceKey = "SonicFX.Structures.ControlPointSize";
        public static float SizeMultiplier => Mathf.Clamp(EditorPrefs.GetFloat(PreferenceKey, 1f), .5f, 5f);

        public static void DrawSizeSetting()
        {
            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider(new GUIContent("Taille des points", "Multiplicateur commun au Cube, a la rampe C et au BlockVirage. Agrandit les points et leur zone de clic dans Scene."), SizeMultiplier, .5f, 5f);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetFloat(PreferenceKey, size);
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }
    }
}
