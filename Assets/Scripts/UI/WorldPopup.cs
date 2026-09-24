using UnityEngine;

public class WorldPopup : MonoBehaviour
{
    private TextMesh _text;
    private float _life = 0.85f;
    private Vector3 _velocity = new Vector3(0f, 1.4f, 0f);

    public static void Spawn(Vector3 position, string message, Color color)
    {
        var go = new GameObject("WorldPopup");
        go.transform.position = position;
        var popup = go.AddComponent<WorldPopup>();
        popup._text = go.AddComponent<TextMesh>();
        popup._text.text = message;
        popup._text.color = color;
        popup._text.anchor = TextAnchor.MiddleCenter;
        popup._text.alignment = TextAlignment.Center;
        popup._text.characterSize = 0.12f;
        popup._text.fontSize = 48;
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "Player";
            renderer.sortingOrder = 20;
        }
    }

    private void Update()
    {
        transform.position += _velocity * Time.deltaTime;
        _life -= Time.deltaTime;
        if (_text != null)
        {
            Color color = _text.color;
            color.a = Mathf.Clamp01(_life / 0.4f);
            _text.color = color;
        }

        if (_life <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
