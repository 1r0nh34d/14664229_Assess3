using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Grows a button while it is highlighted and fires onHover (used for the hover sound).
public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public float highlightScale = 1.12f;
    public float scaleSpeed = 10f;
    public UnityEvent onHover = new UnityEvent();

    Vector3 targetScale = Vector3.one;

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * scaleSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData) => Highlight(true);
    public void OnPointerExit(PointerEventData eventData) => Highlight(false);
    public void OnSelect(BaseEventData eventData) => Highlight(true);
    public void OnDeselect(BaseEventData eventData) => Highlight(false);

    void Highlight(bool on)
    {
        targetScale = on ? Vector3.one * highlightScale : Vector3.one;
        if (on) onHover.Invoke();
    }
}
