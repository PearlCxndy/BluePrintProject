using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Shared pointer and keyboard focus treatment, independent of UI scale.</summary>
[RequireComponent(typeof(Button))]
public class ExperimentButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler
{
    public Color idle = new Color(.18f, .19f, .20f);
    public Color focus = new Color(.34f, .29f, .18f);
    Image _image;
    Button _button;
    bool _hover, _selected;

    void Awake() { _image = GetComponent<Image>(); _button = GetComponent<Button>(); }
    void OnEnable() { _hover = _selected = false; transform.localScale = Vector3.one; }
    void Update()
    {
        var focused = _button.interactable && (_hover || _selected);
        var t = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        _image.color = Color.Lerp(_image.color, focused ? focus : idle, t);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * (focused ? 1.035f : 1f), t);
    }
    public void OnPointerEnter(PointerEventData e) { _hover = true; }
    public void OnPointerExit(PointerEventData e) { _hover = false; }
    public void OnSelect(BaseEventData e) { _selected = true; }
    public void OnDeselect(BaseEventData e) { _selected = false; }
}
