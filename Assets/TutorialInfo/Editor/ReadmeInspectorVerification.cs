using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
static class ReadmeInspectorVerification
{
    static readonly string Reports="C:/Users/Lecle/Documents/Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ReadmeFix";
    static ReadmeInspectorVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        var request=Path.Combine(Reports,"request.txt");
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
        File.Delete(request);
        Readme temporary=null;UnityEditor.Editor editor=null;StyleSheet custom=null;
        try{
            var actual=AssetDatabase.LoadAssetAtPath<Readme>("Assets/Readme.asset");
            if(actual==null)throw new Exception("Readme asset not loaded.");
            editor=UnityEditor.Editor.CreateEditor(actual);
            if(editor.CreateInspectorGUI()==null)throw new Exception("Actual Readme Inspector not created.");
            UnityEngine.Object.DestroyImmediate(editor);editor=null;
            temporary=ScriptableObject.CreateInstance<Readme>();temporary.title="Verification Readme";temporary.sections=null;
            editor=UnityEditor.Editor.CreateEditor(temporary);
            var recovered=editor.CreateInspectorGUI();
            if(recovered.styleSheets.count!=2)throw new Exception("Bundled styles not recovered with null references.");
            temporary.sections=new Readme.Section[]{null,new Readme.Section{heading="Section",text="Texte"}};
            custom=ScriptableObject.CreateInstance<StyleSheet>();temporary.commonStyle=custom;
            var assigned=editor.CreateInspectorGUI();
            if(!assigned.styleSheets.Contains(custom) || assigned.styleSheets.count!=2)throw new Exception("Assigned style not preserved.");
            var method=typeof(ReadmeEditor).GetMethod("AddStyleSheet",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var missing=new VisualElement();method.Invoke(null,new object[]{missing,null,"MissingVerification.uss"});
            if(missing.styleSheets.count!=0)throw new Exception("Absent fallback handled incorrectly.");
            File.WriteAllText(Path.Combine(Reports,"verification.txt"),"PASS : actual Readme, missing styles recovered, absent fallback safe, assigned style preserved, null sections safe. No project assets changed by verification.\n"+DateTime.Now.ToString("s"));
        }catch(Exception e){File.WriteAllText(Path.Combine(Reports,"verification.txt"),"FAIL\n"+e);}
        finally{if(editor!=null)UnityEngine.Object.DestroyImmediate(editor);if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);if(custom!=null)UnityEngine.Object.DestroyImmediate(custom);}
    }
}
