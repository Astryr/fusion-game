using UnityEngine;

/// <summary>
/// Contenedor local de props de un minijuego (plataformas, avalancha, etc.).
/// Se destruye entero al limpiar.
/// </summary>
public static class MiniGameWorld
{
    public const string RootName = "MiniGameWorld";

    public static Transform GetRoot()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
        }

        return root.transform;
    }

    public static void Clear()
    {
        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            Object.Destroy(root);
        }
    }

    public static GameObject SpriteObject(string name, Sprite sprite, Vector3 position, Vector3 scale, string sortingLayer, int order = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(GetRoot(), false);
        go.transform.position = position;
        go.transform.localScale = scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = sortingLayer;
        renderer.sortingOrder = order;
        return go;
    }
}
