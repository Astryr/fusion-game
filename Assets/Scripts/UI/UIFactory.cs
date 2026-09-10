using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Utilidades para armar una UI basica (Canvas, botones, inputs, texto)
/// enteramente por codigo. Se eligio este enfoque para que el proyecto
/// tenga un menu y un HUD funcionales desde el primer commit sin depender
/// de escenas .unity con jerarquias de UI armadas a mano en el editor.
/// Cuando el equipo tenga el diseño final de UI (segun el GDD), esta UI
/// placeholder se puede reemplazar sin tocar la logica de red.
/// </summary>
public static class UIFactory
{
    private static Font _titleFont;
    private const string TitleFontResourcePath = "Fonts/BananaRushTitle";

    public static Canvas CreateCanvas(string name)
    {
        var canvasGO = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    public static Camera EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            return cam;
        }

        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        cam = camGO.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0, 0, -10f);
        return cam;
    }

    public static RectTransform CreatePanel(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = color;

        var rect = go.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        return rect;
    }

    public static Text CreateText(Transform parent, string content, int fontSize, TextAnchor alignment, Color color)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return text;
    }

    /// <summary>
    /// Texto grande para titulos/banners (ej. "BANANA RUSH" en el menu o el
    /// anuncio del ganador), con la tipografia divertida de
    /// <c>Assets/Resources/Fonts/BananaRushTitle.ttf</c> y un borde (Outline)
    /// para que se lea bien sobre cualquier fondo.
    /// </summary>
    public static Text CreateTitleText(Transform parent, string content, int fontSize, Color color, Color outlineColor)
    {
        var go = new GameObject("Title", typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = GetTitleFont();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);

        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return text;
    }

    private static Font GetTitleFont()
    {
        if (_titleFont == null)
        {
            _titleFont = Resources.Load<Font>(TitleFontResourcePath);
        }

        return _titleFont != null ? _titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static Button CreateButton(Transform parent, string label, Color backgroundColor)
    {
        var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = backgroundColor;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        CreateText(go.transform, label, 22, TextAnchor.MiddleCenter, Color.white);

        return button;
    }

    public static InputField CreateInputField(Transform parent, string placeholder)
    {
        var go = new GameObject("InputField", typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

        var inputField = go.GetComponent<InputField>();

        var textGO = new GameObject("Text", typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        var text = textGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.color = new Color(0.1f, 0.1f, 0.1f);
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));

        var placeholderGO = new GameObject("Placeholder", typeof(Text));
        placeholderGO.transform.SetParent(go.transform, false);
        var placeholderText = placeholderGO.GetComponent<Text>();
        placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderText.text = placeholder;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        placeholderText.fontSize = 20;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        SetRect(placeholderText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));

        inputField.textComponent = text;
        inputField.placeholder = placeholderText;

        return inputField;
    }

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
