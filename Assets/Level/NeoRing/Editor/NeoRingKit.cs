using UnityEngine;
using UnityEditor;

// Outils de construction du niveau Neo Ring Zone (appeles depuis des commandes editeur).
public static class NeoRingKit
{
    public const string Root = "Assets/Level/NeoRing/";
    const float TexMeters = 2f; // 1 repetition de texture = 2 m

    public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");

    public static GameObject Prefab(string path, Vector3 pos, Quaternion rot, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    // Boite a UV en metres monde : textures a l'echelle quelle que soit la taille, un seul BoxCollider (pas de jointure).
    public static GameObject Box(string name, Vector3 center, Vector3 size, Quaternion rot, Material mat, Transform parent, bool collider = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, rot);
        var m = new Mesh { name = name };
        Vector3 h = size * .5f;
        var v = new Vector3[24]; var n = new Vector3[24]; var uv = new Vector2[24]; var tr = new int[36];
        // faces : +Y, -Y, +X, -X, +Z, -Z ; (normale, axe u, axe v)
        Vector3[,] f = {
            { Vector3.up, Vector3.right, Vector3.forward }, { Vector3.down, Vector3.right, Vector3.back },
            { Vector3.right, Vector3.back, Vector3.up }, { Vector3.left, Vector3.forward, Vector3.up },
            { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.left, Vector3.up } };
        for (int i = 0; i < 6; i++)
        {
            Vector3 nn = f[i, 0], uu = f[i, 1], vv = f[i, 2];
            float su = Mathf.Abs(Vector3.Dot(size, uu)), sv = Mathf.Abs(Vector3.Dot(size, vv));
            Vector3 c = Vector3.Scale(nn, h);
            for (int k = 0; k < 4; k++)
            {
                float a = (k == 1 || k == 2) ? .5f : -.5f, b = k >= 2 ? .5f : -.5f;
                v[i * 4 + k] = c + uu * a * su + vv * b * sv;
                n[i * 4 + k] = nn;
                uv[i * 4 + k] = new Vector2((a + .5f) * su, (b + .5f) * sv) / TexMeters;
            }
            int o = i * 4;
            // ordre horaire vu depuis l'exterieur
            if (Vector3.Dot(Vector3.Cross(uu, vv), nn) < 0) { tr[i*6]=o; tr[i*6+1]=o+2; tr[i*6+2]=o+1; tr[i*6+3]=o; tr[i*6+4]=o+3; tr[i*6+5]=o+2; }
            else { tr[i*6]=o; tr[i*6+1]=o+1; tr[i*6+2]=o+2; tr[i*6+3]=o; tr[i*6+4]=o+2; tr[i*6+5]=o+3; }
        }
        m.vertices = v; m.normals = n; m.uv = uv; m.triangles = tr; m.RecalculateBounds(); m.RecalculateTangents();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    public static GameObject Quad(string name, Vector3 pos, Quaternion rot, Vector2 size, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = new Vector3(size.x, size.y, 1);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    public static GameObject Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        return go;
    }
}
