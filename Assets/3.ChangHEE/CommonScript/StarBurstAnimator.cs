using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StarBurstAnimator : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] Canvas targetCanvas;

    [Header("Timeline")]
    [SerializeField] float appearDelay = 0.8f;
    [SerializeField] float burstDuration = 0.35f;
    [SerializeField] float fadeDuration = 0.6f;

    [Header("Look")]
    [SerializeField] float startSize = 16f;
    [SerializeField] float burstSize = 220f;
    [SerializeField] Color starColor = new Color(1f, 1f, 1f, 1f);

    RectTransform starRoot;
    Image starImage;
    float startedAt;
    float totalDuration;

    public bool IsSequenceComplete => (Time.time - startedAt) >= totalDuration;

    void Awake()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas == null)
        {
            enabled = false;
            return;
        }

        BuildStar();
        totalDuration = appearDelay + burstDuration + fadeDuration;
        startedAt = Time.time;
        ApplyState(0f, startSize);
    }

    void Update()
    {
        float elapsed = Time.time - startedAt;
        if (elapsed < appearDelay)
        {
            ApplyState(0f, startSize);
            return;
        }

        float local = elapsed - appearDelay;
        if (local < burstDuration)
        {
            float t = Mathf.Clamp01(local / burstDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            ApplyState(Mathf.Lerp(0.2f, 1f, eased), Mathf.Lerp(startSize, burstSize, eased));
            return;
        }

        float fadeT = Mathf.Clamp01((local - burstDuration) / fadeDuration);
        float fade = 1f - fadeT;
        ApplyState(fade * 0.55f, Mathf.Lerp(burstSize, burstSize * 0.6f, fadeT));
    }

    void BuildStar()
    {
        Transform starLayer = targetCanvas.transform.Find("StarLayer");
        if (starLayer == null)
        {
            GameObject layer = new GameObject("StarLayer", typeof(RectTransform));
            layer.transform.SetParent(targetCanvas.transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            starLayer = layer.transform;
        }

        Transform existing = starLayer.Find("CentralStar");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject root = new GameObject("CentralStar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(starLayer, false);

        starRoot = root.GetComponent<RectTransform>();
        starRoot.anchorMin = new Vector2(0.5f, 0.5f);
        starRoot.anchorMax = new Vector2(0.5f, 0.5f);
        starRoot.pivot = new Vector2(0.5f, 0.5f);
        starRoot.anchoredPosition = Vector2.zero;

        starImage = root.GetComponent<Image>();
        starImage.sprite = BuildRadialSprite(128);
        starImage.raycastTarget = false;
        starImage.color = starColor;
    }

    void ApplyState(float alpha, float size)
    {
        if (starRoot == null || starImage == null)
            return;

        starRoot.sizeDelta = new Vector2(size, size);
        Color c = starColor;
        c.a = Mathf.Clamp01(alpha);
        starImage.color = c;
    }

    static Sprite BuildRadialSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float a = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
