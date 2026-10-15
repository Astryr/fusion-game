using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform Face;
    public float Lip = 5f;

    private Button _button;
    private bool _hover;
    private bool _down;
    private float _scale = 1f;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnDisable()
    {
        _hover = false;
        _down = false;
        _scale = 1f;
        transform.localScale = Vector3.one;
        SetFaceOffset(0f);
    }

    private void Update()
    {
        bool usable = _button == null || _button.interactable;
        float target = !usable ? 1f : _down ? 0.97f : _hover ? 1.04f : 1f;
        _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(_scale, _scale, 1f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hover = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
        if (_down)
        {
            _down = false;
            SetFaceOffset(0f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable)
        {
            return;
        }

        _down = true;
        SetFaceOffset(3f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _down = false;
        SetFaceOffset(0f);
    }

    private void SetFaceOffset(float press)
    {
        if (Face == null)
        {
            return;
        }

        Face.offsetMin = new Vector2(0f, Lip - press);
        Face.offsetMax = new Vector2(0f, -press);
    }
}
