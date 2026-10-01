using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Water.Editor
{
    [InitializeOnLoad]
    public static class SonicUnderwaterVerification
    {
        static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/UnderwaterEffect");
        static SonicUnderwaterVerification()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(Path.Combine(Folder,"unity-tests.txt"))) Run();
            };
        }
        static Color[] Read(RenderTexture target)
        {
            RenderTexture old = RenderTexture.active;
            var image = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                return image.GetPixels();
            }
            finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image); }
        }
        static void Check(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        [MenuItem("Tools/Sonic FX/Eau/Verifier le filtre sous-marin")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            var shader=Resources.Load<Shader>("SonicUnderwater");
            Texture2D input=null;Material material=null;RenderTexture result=null;
            try
            {
                Check(shader!=null && !ShaderUtil.ShaderHasError(shader) && shader.isSupported,"Shader compilation/support");
                material=new Material(shader);
                input=new Texture2D(128,128,TextureFormat.RGBA32,false,true);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                    input.SetPixel(x,y,new Color((x%16)<8?.75f:.2f,y/127f,.4f,1));
                input.Apply();input.wrapMode=TextureWrapMode.Clamp;
                result=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                Graphics.Blit(input,result);var baseline=Read(result);
                material.SetColor("_Tint",new Color(.2f,.7f,1));
                material.SetFloat("_TintStrength",0);material.SetFloat("_Distortion",0);
                Graphics.Blit(input,result,material);var disabled=Read(result);
                float same=0;
                for(int i=0;i<baseline.Length;i++)same+=Mathf.Abs(baseline[i].r-disabled[i].r)+Mathf.Abs(baseline[i].g-disabled[i].g)+Mathf.Abs(baseline[i].b-disabled[i].b);
                Check(same/baseline.Length<.005f,"Effect off preserves image");
                material.SetFloat("_TintStrength",.3f);Graphics.Blit(input,result,material);var tint=Read(result);
                float red=0,blue=0;
                for(int i=0;i<tint.Length;i++){red+=tint[i].r-baseline[i].r;blue+=tint[i].b-baseline[i].b;}
                Check(red<0 && blue>0,"Blue tint");
                material.SetFloat("_Distortion",.008f);material.SetFloat("_Clock",1.25f);
                Graphics.Blit(input,result,material);var wave=Read(result);float movement=0;
                for(int i=0;i<wave.Length;i++)movement+=Mathf.Abs(wave[i].r-tint[i].r);
                Check(movement/wave.Length>.001f,"Wave distorts image");
                File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\nShader compiled and supported. GPU checks: unchanged image when disabled; blue tint; wave distortion.\nCamera immersion transitions need a Play test.");
                Debug.Log("Filtre sous-marin : shader et tests GPU valides.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                if(result!=null)RenderTexture.ReleaseTemporary(result);
                if(material!=null)UnityEngine.Object.DestroyImmediate(material);
                if(input!=null)UnityEngine.Object.DestroyImmediate(input);
            }
        }
    }
}
