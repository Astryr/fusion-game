using System.Collections.Generic;
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

    public static Dropdown CreateDropdown(Transform parent, IList<string> options, int selectedIndex = 0)
    {
        var root = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

        Text caption = CreateText(root.transform, options.Count > 0 ? options[0] : string.Empty, 16, TextAnchor.MiddleLeft, new Color(0.1f, 0.1f, 0.1f));
        caption.raycastTarget = false;
        SetRect(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-28, -4));

        Text arrow = CreateText(root.transform, "v", 14, TextAnchor.MiddleCenter, new Color(0.2f, 0.2f, 0.2f));
        arrow.raycastTarget = false;
        SetRect(arrow.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-26, 0), new Vector2(-4, 0));

        var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        template.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = Vector2.zero;
        templateRect.sizeDelta = new Vector2(0f, 220f);

        var templateCanvas = template.AddComponent<Canvas>();
        templateCanvas.overrideSorting = true;
        templateCanvas.sortingOrder = 400;
        template.AddComponent<GraphicRaycaster>();

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(template.transform, false);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 28f);

        var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(0f, 28f);

        Image itemBackground = item.GetComponent<Image>();
        itemBackground.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        toggle.isOn = true;

        Text itemLabel = CreateText(item.transform, "Opcion", 15, TextAnchor.MiddleLeft, new Color(0.1f, 0.1f, 0.1f));
        itemLabel.raycastTarget = false;
        SetRect(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        ScrollRect scrollRect = template.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        Dropdown dropdown = root.GetComponent<Dropdown>();
        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.captionText = caption;
        dropdown.itemText = itemLabel;
        dropdown.template = templateRect;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.value = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1));
        dropdown.RefreshShownValue();

        template.SetActive(false);
        return dropdown;
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
