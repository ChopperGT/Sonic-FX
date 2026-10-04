using UnityEngine;
using UnityEditor;

// Cree les variantes cyberpunk des ennemis existants : on garde toute la logique (IA, colliders, HomingTarget)
// et on remplace seulement le visuel par le modele GLB.
public static class NeoEnemyBuilder
{
    const string Dir = "Assets/Ennemy/NeoRing/";

    [MenuItem("Tools/Neo Ring/Creer les ennemis cyberpunk")]
    public static void Build()
    {
        // (prefab de base, glb, nom, enfant qui porte le visuel (null = objet EnemyHealth), plus grande dimension en m, bob vertical)
        // Le Motobug n'oriente que son enfant Motobug_Generations vers sa direction de marche.
        Make("Assets/Ennemy/[Enemy] - Motobug Variant.prefab", "ennemi-rouleur", "Neo_Rouleur", "Motobug_Generations", 3.2f, 0f);
        Make("Assets/Ennemy/Guepe_Robot_Parcours Variant.prefab", "ennemi-drone", "Neo_DroneGuepe", null, 3.2f, .05f);
        Make("Assets/Ennemy/Crab_Ennemi Variant.prefab", "ennemi-crabe", "Neo_CrabeSentinelle", null, 3.2f, 0f);
        AssetDatabase.SaveAssets();
    }

    static void Make(string basePath, string glb, string name, string hostPath, float size, float bob)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + glb + ".glb");
        if (!basePrefab || !model) { Debug.LogError("NeoEnemyBuilder : introuvable " + basePath + " / " + glb); return; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)) r.enabled = false;

            var health = inst.GetComponentInChildren<EnemyHealth>(true);
            var host = hostPath != null ? inst.transform.Find(hostPath) : health ? health.transform : inst.transform;

            // Bas du modele = bas des colliders de jeu (contact sol), mesure par rapport a l'hote.
            float bottom = 0f;
            var cols = host.GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0) cols = inst.GetComponentsInChildren<Collider>(true);
            foreach (var c in cols) if (!c.isTrigger) { bottom = c.bounds.min.y - host.position.y; break; }

            // Dimensions natives du GLB (echelle 1, sans rotation).
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            Bounds mb = Encapsulate(probe);
            Vector3 mc = mb.center - probe.transform.position, mmin = mb.min - probe.transform.position;
            float s = size / Mathf.Max(mb.size.x, mb.size.y, mb.size.z);
            Object.DestroyImmediate(probe);

            // Calcul en repere local de l'hote : independant de sa rotation en edition (l'IA la reecrit en jeu).
            float hs = host.lossyScale.x;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, host);
            visual.name = "NeoVisual";
            visual.transform.localRotation = Quaternion.identity; // les GLB regardent +Z, comme les IA
            visual.transform.localScale = Vector3.one * (s / hs);
            visual.transform.localPosition = new Vector3(-mc.x * s, bottom - mmin.y * s, -mc.z * s) / hs;
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // Recalage sur les bornes reelles une fois en place (l'hote est droit en edition).
            Bounds wb = Encapsulate(visual);
            visual.transform.localScale *= size / Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
            wb = Encapsulate(visual);
            visual.transform.position += Vector3.up * (host.position.y + bottom - wb.min.y);

            var fx = visual.AddComponent<NeonEnemyFX>();
            fx.BobAmplitude = bob / hs;

            inst.name = name;
            PrefabUtility.SaveAsPrefabAsset(inst, Dir + name + ".prefab");
            Debug.Log("NeoEnemyBuilder : " + name + " cree sur " + host.name + " (taille " + size + " m)");
        }
        finally { Object.DestroyImmediate(inst); }
    }

    static Bounds Encapsulate(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
