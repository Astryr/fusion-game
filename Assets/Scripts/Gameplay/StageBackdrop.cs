using UnityEngine;

/// <summary>
/// Fondo de la escena Game vs fondo que sigue a la camara.
/// Los minijuegos estaticos (lluvia, tronco, pecho, puzzle, arbol)
/// dejan el Background/Ground de la escena. Los que corren hacia
/// los costados ocultan ese piso corto y arman un parallax tileado
/// para que no se vea el color solido de la camara.
/// </summary>
public static class StageBackdrop
{
    private const string SceneBackgroundName = "Background";
    private const string SceneGroundName = "Ground";
    private const string RootName = "ParallaxRoot";

    private static GameObject _sceneBackground;
    private static GameObject _sceneGround;
    private static Transform _root;
    private static Transform _farLayer;
    private static Transform _midLayer;

    public static void BeginStaticStage()
    {
        CaptureScene();
        ClearParallax();
        SetSceneVisible(true);
    }

    public static void BeginScrollingStage(float minX, float maxX)
    {
        CaptureScene();
        SetSceneVisible(false);
        BuildParallax(minX, maxX);
    }

    public static void Tick(Vector3 cameraPosition)
    {
        if (_farLayer != null)
        {
            _farLayer.position = new Vector3(cameraPosition.x * 0.16f, cameraPosition.y * 0.06f, 0f);
        }

        if (_midLayer != null)
        {
            _midLayer.position = new Vector3(cameraPosition.x * 0.4f, cameraPosition.y * 0.14f, 0f);
        }
    }

    public static void Clear()
    {
        ClearParallax();
        SetSceneVisible(true);
    }

    private static void CaptureScene()
    {
        if (_sceneBackground == null)
        {
            _sceneBackground = GameObject.Find(SceneBackgroundName);
        }

        if (_sceneGround == null)
        {
            _sceneGround = GameObject.Find(SceneGroundName);
        }
    }

    private static void SetSceneVisible(bool visible)
    {
        if (_sceneBackground != null)
        {
            _sceneBackground.SetActive(visible);
        }

        if (_sceneGround != null)
        {
            _sceneGround.SetActive(visible);
        }
    }

    private static void BuildParallax(float minX, float maxX)
    {
        ClearParallax();

        Sprite sprite = null;
        if (_sceneBackground != null)
        {
            sprite = _sceneBackground.GetComponent<SpriteRenderer>()?.sprite;
        }

        if (sprite == null)
        {
            return;
        }

        var rootGO = new GameObject(RootName);
        _root = rootGO.transform;

        _farLayer = CreateLayer("Far", sprite, minX, maxX, 28f, 20f, new Color(0.78f, 0.88f, 0.62f, 1f), -4);
        _midLayer = CreateLayer("Mid", sprite, minX, maxX, 22f, 17f, new Color(0.55f, 0.72f, 0.38f, 0.55f), -3);
    }

    private static Transform CreateLayer(string name, Sprite sprite, float minX, float maxX,
        float tileWidth, float tileHeight, Color tint, int order)
    {
        var layer = new GameObject(name);
        layer.transform.SetParent(_root, false);

        float spriteW = sprite.rect.width / sprite.pixelsPerUnit;
        float spriteH = sprite.rect.height / sprite.pixelsPerUnit;
        Vector3 scale = new Vector3(tileWidth / spriteW, tileHeight / spriteH, 1f);

        float start = minX - tileWidth;
        float end = maxX + tileWidth;
        int index = 0;
        for (float x = start; x <= end; x += tileWidth)
        {
            var go = new GameObject($"{name}_{index}");
            go.transform.SetParent(layer.transform, false);
            go.transform.localPosition = new Vector3(x, BananaRushConfig.CameraCenterY, 0f);
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingLayerName = "Background";
            renderer.sortingOrder = order;
            index++;
        }

        return layer.transform;
    }

    private static void ClearParallax()
    {
        if (_root != null)
        {
            Object.Destroy(_root.gameObject);
            _root = null;
            _farLayer = null;
            _midLayer = null;
        }

        GameObject leftover = GameObject.Find(RootName);
        if (leftover != null)
        {
            Object.Destroy(leftover);
        }
    }
}
