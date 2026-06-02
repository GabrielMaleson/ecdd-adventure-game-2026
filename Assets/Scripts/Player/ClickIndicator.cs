using UnityEngine;

public class ClickIndicator : MonoBehaviour
{
    [SerializeField] float duration   = 0.25f;
    [SerializeField] float startScale = 0.15f;

    float elapsed;

    public static void Spawn(Vector3 worldPos)
    {
        var go = new GameObject("ClickIndicator");
        go.transform.position = worldPos;

        var sr        = go.AddComponent<SpriteRenderer>();
        sr.sprite     = CreateCircleSprite(16);
        sr.color      = Color.white;
        sr.sortingOrder = 99;

        go.AddComponent<ClickIndicator>();
    }

    void Start()
    {
        transform.localScale = Vector3.one * startScale;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, 0f, t);

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
            tex.SetPixel(x, y, dist <= center ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }
}
