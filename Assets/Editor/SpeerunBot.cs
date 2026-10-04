using System.IO;
using UnityEditor;
using UnityEngine;

// Play-test bot: follows a route file (Temp/speerun_route_<name>.txt) at 3x time scale, logs progress to Temp/speerun_bot.txt.
[InitializeOnLoad]
public static class SpeerunBot
{
    const string Key = "SpeerunBot";
    static PlayerBhysics p; static float t0; static float nextLog; static StreamWriter w;
    static Vector3[] route; static int idx;

    static SpeerunBot() { EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(Key)) Start(); if (s == PlayModeStateChange.ExitingPlayMode) Stop(); }; }

    static void Start()
    {
        p = Object.FindAnyObjectByType<PlayerBhysics>();
        if (!p) return;
        var inp = p.GetComponent<PlayerBinput>(); if (inp) inp.enabled = false;
        string name = EditorPrefs.GetString("SpeerunBotRoute", "main");
        var lines = File.ReadAllLines(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/speerun_route_" + name + ".txt"));
        route = new Vector3[lines.Length];
        for (int i = 0; i < lines.Length; i++) { var f = lines[i].Split(' '); route[i] = new Vector3(float.Parse(f[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture)); }
        idx = 0; t0 = Time.time; nextLog = 0; Time.timeScale = 3f;
        w = new StreamWriter(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/speerun_bot.txt"));
        w.WriteLine("route " + name + " points " + route.Length);
        EditorApplication.update += Tick;
    }

    static void Stop() { Time.timeScale = 1f; EditorApplication.update -= Tick; w?.Close(); w = null; }

    static void Tick()
    {
        if (!p) { Stop(); return; }
        float t = Time.time - t0;
        Vector3 pos = p.transform.position;
        // advance along the route (search a window ahead, never back)
        float best = 1e12f;
        for (int i = idx; i < Mathf.Min(route.Length, idx + 60); i++)
        { float d = (route[i] - pos).sqrMagnitude; if (d < best) { best = d; idx = i; } }
        int look = Mathf.Min(route.Length - 1, idx + Mathf.RoundToInt(Mathf.Clamp(p.SpeedMagnitude * 0.25f, 12, 70) / 3f));
        Vector3 dir = route[look] - pos; dir.y = 0;
        p.MoveInput = dir.normalized;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SpeerunMadeByCOCO") { w.WriteLine("END scene change t=" + t); Stop(); EditorApplication.isPlaying = false; return; }
        if (t >= nextLog)
        {
            w.WriteLine($"t={t:F1} s={idx * 3} off={Mathf.Sqrt(best):F0} pos=({pos.x:F0},{pos.y:F0},{pos.z:F0}) speed={p.SpeedMagnitude:F0} grounded={p.Grounded}");
            w.Flush(); nextLog += 1;
        }
        if (t > 200 || idx >= route.Length - 5) { w.WriteLine("END t=" + t + " idx=" + idx + "/" + route.Length); Stop(); EditorApplication.isPlaying = false; }
    }
}
