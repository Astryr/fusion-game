using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UIStyle
{
    public static readonly Color Banana = new Color(1f, 0.82f, 0.22f, 1f);
    public static readonly Color BananaDeep = new Color(0.96f, 0.60f, 0.10f, 1f);
    public static readonly Color Brown = new Color(0.30f, 0.16f, 0.05f, 1f);
    public static readonly Color PanelFill = new Color(0.05f, 0.12f, 0.08f, 0.93f);
    public static readonly Color FieldFill = new Color(0.97f, 0.96f, 0.88f, 1f);
    public static readonly Color FieldText = new Color(0.10f, 0.14f, 0.08f, 1f);
    public static readonly Color Muted = new Color(0.74f, 0.86f, 0.74f, 1f);
    public static readonly Color Go = new Color(0.27f, 0.70f, 0.30f, 1f);
    public static readonly Color Info = new Color(0.22f, 0.52f, 0.86f, 1f);
    public static readonly Color Danger = new Color(0.84f, 0.27f, 0.22f, 1f);
    public static readonly Color Warn = new Color(0.93f, 0.55f, 0.15f, 1f);
    public static readonly Color Neutral = new Color(0.30f, 0.38f, 0.34f, 1f);

    private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
    private static Sprite _glow;
    private static Sprite _verticalFade;
    private static Sprite _crown;

    public static Color Shade(Color color, float factor)
    {
        return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }

    public static string Escape(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('<', '\u2039').Replace('>', '\u203A');
    }

    public static string Exclaim(string text)
    {
        Font font = UIFactory.GetTitleFont();
        return font != null && font.HasCharacter('\u00A1') ? "\u00A1" + text + "!" : text + "!";
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    public static Sprite Rounded(int radius)
    {
        if (RoundedCache.TryGetValue(radius, out Sprite cached) && cached != null)
        {
            return cached;
        }

        int size = radius * 2 + 2;
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float dx = Mathf.Max(Mathf.Abs(px) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(py) - inner, 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                byte alpha = (byte)(Mathf.Clamp01(0.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        Texture2D texture = MakeTexture(size, size, pixels);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.hideFlags = HideFlags.DontSave;
        RoundedCache[radius] = sprite;
        return sprite;
    }

    public static Sprite Glow()
    {
        if (_glow != null)
        {
            return _glow;
        }

        const int size = 64;
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                byte alpha = (byte)(falloff * falloff * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        _glow = Sprite.Create(MakeTexture(size, size, pixels), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _glow.hideFlags = HideFlags.DontSave;
        return _glow;
    }

    public static Sprite VerticalFade()
    {
        if (_verticalFade != null)
        {
            return _verticalFade;
        }

        const int height = 128;
        var pixels = new Color32[2 * height];
        for (int y = 0; y < height; y++)
        {
            float t = y / (height - 1f);
            float alpha = Mathf.SmoothStep(0f, 1f, t);
            byte value = (byte)(alpha * 255f);
            pixels[y * 2] = new Color32(255, 255, 255, value);
            pixels[y * 2 + 1] = new Color32(255, 255, 255, value);
        }

        _verticalFade = Sprite.Create(MakeTexture(2, height, pixels), new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
        _verticalFade.hideFlags = HideFlags.DontSave;
        return _verticalFade;
    }

    public static Sprite Crown()
    {
        if (_crown != null)
        {
            return _crown;
        }

        const int width = 48;
        const int height = 36;
        Vector2[] outline =
        {
            new Vector2(4f, 4f), new Vector2(44f, 4f), new Vector2(44f, 26f), new Vector2(34f, 15f),
            new Vector2(24f, 30f), new Vector2(14f, 15f), new Vector2(4f, 26f),
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int covered = 0;
                for (int sy = 0; sy < 3; sy++)
                {
                    for (int sx = 0; sx < 3; sx++)
                    {
                        var point = new Vector2(x + (sx + 0.5f) / 3f, y + (sy + 0.5f) / 3f);
                        if (PointInPolygon(point, outline))
                        {
                            covered++;
                        }
                    }
                }

                bool band = y >= 8 && y <= 10;
                byte shade = band ? (byte)185 : (byte)255;
                pixels[y * width + x] = new Color32(shade, shade, shade, (byte)(covered / 9f * 255f));
            }
        }

        _crown = Sprite.Create(MakeTexture(width, height, pixels), new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _crown.hideFlags = HideFlags.DontSave;
        return _crown;
    }

    public static Image CreateImage(Transform parent, string name, Color color, int radius = 12)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = Rounded(radius);
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    public static Image CreateSprite(Transform parent, string name, Sprite sprite, Color color, bool preserveAspect = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    public sealed class Frame
    {
        public Image Border;
        public Image Fill;
        public RectTransform Content => Fill.rectTransform;
    }

    public static Frame CreateFrame(Transform parent, string name, Color border, Color fill, int radius = 18, float thickness = 3f)
    {
        Image outer = CreateImage(parent, name, border, radius);
        Image inner = CreateImage(outer.transform, "Fill", fill, Mathf.Max(3, radius - Mathf.RoundToInt(thickness)));
        UIFactory.SetRect(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(thickness, thickness), new Vector2(-thickness, -thickness));
        return new Frame { Border = outer, Fill = inner };
    }

    public static Text CreateLabel(Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
    {
        Text text = UIFactory.CreateText(parent, content, size, anchor, color);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (bold)
        {
            text.fontStyle = FontStyle.Bold;
        }

        return text;
    }

    public static Text CreateHeading(Transform parent, string content, int size, Color color, Color outline, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        Text text = UIFactory.CreateTitleText(parent, content, size, color, outline);
        text.alignment = anchor;
        text.raycastTarget = false;
        text.GetComponent<Outline>().effectDistance = new Vector2(Mathf.Max(2f, size / 18f), -Mathf.Max(2f, size / 18f));
        return text;
    }

    public static Button CreateButton(Transform parent, string label, Color color, int fontSize = 26, int radius = 14, float lip = 5f)
    {
        var root = new GameObject(label + "Button", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        Image lipImage = CreateImage(root.transform, "Lip", Shade(color, 0.52f), radius);
        lipImage.raycastTarget = false;
        UIFactory.SetRect(lipImage.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Image face = CreateImage(root.transform, "Face", color, radius);
        UIFactory.SetRect(face.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, lip), Vector2.zero);

        var button = root.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f),
            pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.48f, 0.5f, 0.5f, 0.9f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        Text text = CreateHeading(face.transform, label, fontSize, Color.white, new Color(0f, 0f, 0f, 0.5f));
        text.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 10;
        text.resizeTextMaxSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        UIFactory.SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

        var fx = root.AddComponent<ButtonFx>();
        fx.Face = face.rectTransform;
        fx.Lip = lip;
        return button;
    }

    public static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
        {
            return;
        }

        Transform lip = button.transform.Find("Lip");
        if (lip != null)
        {
            lip.GetComponent<Image>().color = Shade(color, 0.52f);
        }

        if (button.targetGraphic != null)
        {
            button.targetGraphic.color = color;
        }
    }

    public static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    public static InputField CreateInputField(Transform parent, string placeholder, int fontSize = 20)
    {
        InputField field = UIFactory.CreateInputField(parent, placeholder);
        Image background = field.GetComponent<Image>();
        background.sprite = Rounded(10);
        background.type = Image.Type.Sliced;
        background.color = FieldFill;

        field.textComponent.color = FieldText;
        field.textComponent.fontSize = fontSize;
        if (field.placeholder is Text hint)
        {
            hint.color = WithAlpha(FieldText, 0.45f);
            hint.fontSize = fontSize - 2;
        }

        field.customCaretColor = true;
        field.caretColor = FieldText;
        field.selectionColor = new Color(1f, 0.82f, 0.22f, 0.55f);
        return field;
    }

    public static Dropdown CreateDropdown(Transform parent, IList<string> options, int fontSize = 19)
    {
        Dropdown dropdown = UIFactory.CreateDropdown(parent, options);

        Image background = dropdown.GetComponent<Image>();
        background.sprite = Rounded(10);
        background.type = Image.Type.Sliced;
        background.color = FieldFill;

        dropdown.captionText.color = FieldText;
        dropdown.captionText.fontSize = fontSize;
        dropdown.captionText.fontStyle = FontStyle.Bold;

        Image list = dropdown.template.GetComponent<Image>();
        list.sprite = Rounded(10);
        list.type = Image.Type.Sliced;
        list.color = new Color(0.06f, 0.14f, 0.09f, 0.98f);

        dropdown.itemText.color = Color.white;
        dropdown.itemText.fontSize = fontSize - 2;
        Toggle itemToggle = dropdown.itemText.transform.parent.GetComponent<Toggle>();
        if (itemToggle != null)
        {
            itemToggle.GetComponent<Image>().color = Color.white;
            itemToggle.colors = new ColorBlock
            {
                normalColor = new Color(0.10f, 0.22f, 0.14f, 1f),
                highlightedColor = new Color(0.22f, 0.46f, 0.28f, 1f),
                pressedColor = new Color(0.30f, 0.58f, 0.34f, 1f),
                selectedColor = new Color(0.18f, 0.40f, 0.24f, 1f),
                disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.05f,
            };
        }

        return dropdown;
    }

    private static Texture2D MakeTexture(int width, int height, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static bool PointInPolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
            if (crosses && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
