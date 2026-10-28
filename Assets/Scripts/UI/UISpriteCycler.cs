using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UISpriteCycler : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 8f;
    public float bobHeight = 12f;
    public float bobSpeed = 2f;

    Image image;
    Vector2 basePosition;

    void Start()
    {
        image = GetComponent<Image>();
        basePosition = ((RectTransform)transform).anchoredPosition;
    }

    void Update()
    {
        if (frames != null && frames.Length > 0)
            image.sprite = frames[(int)(Time.time * framesPerSecond) % frames.Length];
        ((RectTransform)transform).anchoredPosition =
            basePosition + Vector2.up * Mathf.Sin(Time.time * bobSpeed + basePosition.x * 0.01f) * bobHeight;
    }
}
