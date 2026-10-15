using UnityEngine;

public class UiBob : MonoBehaviour
{
    public Vector2 PositionAmplitude = new Vector2(0f, 6f);
    public float PositionSpeed = 1.6f;
    public float RotationAmplitude;
    public float RotationSpeed = 1.2f;
    public float ScaleAmplitude;
    public float ScaleSpeed = 2f;
    public float Phase;

    private RectTransform _rect;
    private Vector2 _basePosition;
    private Vector3 _baseScale;
    private bool _ready;

    private void OnEnable()
    {
        _ready = false;
    }

    private void Update()
    {
        if (!_ready)
        {
            _rect = transform as RectTransform;
            _basePosition = _rect != null ? _rect.anchoredPosition : (Vector2)transform.localPosition;
            _baseScale = transform.localScale;
            _ready = true;
        }

        float t = Time.unscaledTime + Phase;
        Vector2 offset = PositionAmplitude * Mathf.Sin(t * PositionSpeed);
        if (_rect != null)
        {
            _rect.anchoredPosition = _basePosition + offset;
        }
        else
        {
            transform.localPosition = _basePosition + offset;
        }

        if (RotationAmplitude != 0f)
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * RotationSpeed) * RotationAmplitude);
        }

        if (ScaleAmplitude != 0f)
        {
            transform.localScale = _baseScale * (1f + Mathf.Sin(t * ScaleSpeed) * ScaleAmplitude);
        }
    }
}
