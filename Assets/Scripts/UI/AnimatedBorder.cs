using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class AnimatedBorder : MonoBehaviour
{
    public Sprite dotSprite;
    public int dotCount = 48;
    public float dotSize = 26f;
    public float speed = 120f;
    public Color colourA = new Color(1f, 0.85f, 0.2f);
    public Color colourB = new Color(0.3f, 0.9f, 1f);

    RectTransform rect;
    RectTransform[] dots;
    Image[] images;

    void Start()
    {
        rect = (RectTransform)transform;
        dots = new RectTransform[dotCount];
        images = new Image[dotCount];
        for (int i = 0; i < dotCount; i++)
        {
            GameObject dot = new GameObject("BorderDot" + i, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(transform, false);
            dots[i] = (RectTransform)dot.transform;
            dots[i].sizeDelta = new Vector2(dotSize, dotSize);
            dots[i].anchorMin = dots[i].anchorMax = Vector2.zero;
            images[i] = dot.GetComponent<Image>();
            images[i].sprite = dotSprite;
            images[i].raycastTarget = false;
        }
    }

    void Update()
    {
        Vector2 size = rect.rect.size;
        float perimeter = 2f * (size.x + size.y);
        for (int i = 0; i < dotCount; i++)
        {
            float distance = Mathf.Repeat(Time.time * speed + perimeter * i / dotCount, perimeter);
            dots[i].anchoredPosition = PointOnPerimeter(distance, size);
            float wave = Mathf.Sin(Time.time * 4f + i * 0.6f) * 0.5f + 0.5f;
            images[i].color = Color.Lerp(colourA, colourB, wave);
            dots[i].localScale = Vector3.one * (0.7f + wave * 0.5f);
        }
    }

    static Vector2 PointOnPerimeter(float d, Vector2 size)
    {
        if (d < size.y) return new Vector2(0f, d);
        d -= size.y;
        if (d < size.x) return new Vector2(d, size.y);
        d -= size.x;
        if (d < size.y) return new Vector2(size.x, size.y - d);
        d -= size.y;
        return new Vector2(size.x - d, 0f);
    }
}
