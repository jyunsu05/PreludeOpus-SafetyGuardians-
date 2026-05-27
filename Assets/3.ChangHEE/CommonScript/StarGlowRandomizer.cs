using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class StarGlowRandomizer : MonoBehaviour
{
    [SerializeField] string targetPrefix = "StarGlow";
    [SerializeField] int visibleCount = 8;
    [SerializeField] float minSize = 40f;
    [SerializeField] float maxSize = 90f;
    [SerializeField] bool randomizeEveryFrame = true;
    [SerializeField] float randomizeInterval = 0.15f; // 0이면 매 프레임

    [Header("Strong twinkle")]
    [SerializeField] float twinkleMinAlpha = 0.05f;
    [SerializeField] float twinkleMaxAlpha = 1f;
    [SerializeField] float twinkleMinSpeed = 1.5f;
    [SerializeField] float twinkleMaxSpeed = 4f;

    RectTransform canvasRect;
    const string RuntimeCloneSuffix = "_RuntimeClone";
    const string RuntimeTemplateName = "StarGlow_RuntimeTemplate";
    readonly List<Image> starImages = new List<Image>();
    float nextRandomizeTime;
    static Sprite generatedStarSprite;

    void Start()
    {
        canvasRect = GetComponent<RectTransform>();
        BuildStarPool();
        RandomizeStarGlows();
    }

    void Update()
    {
        if (!randomizeEveryFrame)
            return;

        if (Time.time < nextRandomizeTime)
            return;

        RandomizeStarGlows();
        nextRandomizeTime = Time.time + Mathf.Max(0f, randomizeInterval);
    }

    void RandomizeStarGlows()
    {
        if (starImages.Count == 0)
            return;

        for (int i = 0; i < starImages.Count; i++)
        {
            int swapIndex = Random.Range(i, starImages.Count);
            Image tmp = starImages[i];
            starImages[i] = starImages[swapIndex];
            starImages[swapIndex] = tmp;
        }

        int showCount = Mathf.Clamp(visibleCount, 0, starImages.Count);

        for (int i = 0; i < starImages.Count; i++)
        {
            Image img = starImages[i];
            bool shouldShow = i < showCount;
            img.gameObject.SetActive(shouldShow);
            if (!shouldShow)
                continue;

            RectTransform rt = img.rectTransform;
            if (rt == null)
                continue;

            float size = Random.Range(minSize, maxSize);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            float xHalf = (canvasRect.rect.width * 0.5f) - (size * 0.5f);
            float yHalf = (canvasRect.rect.height * 0.5f) - (size * 0.5f);
            float x = Random.Range(-xHalf, xHalf);
            float y = Random.Range(-yHalf, yHalf);
            rt.anchoredPosition = new Vector2(x, y);

            StarGlowTwinkle twinkle = img.GetComponent<StarGlowTwinkle>();
            if (twinkle == null)
                twinkle = img.gameObject.AddComponent<StarGlowTwinkle>();

            twinkle.Configure(twinkleMinAlpha, twinkleMaxAlpha, twinkleMinSpeed, twinkleMaxSpeed);
        }
    }

    void BuildStarPool()
    {
        RemoveOldRuntimeClones();
        starImages.Clear();

        var images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img != null && img.name.StartsWith(targetPrefix))
                starImages.Add(img);
        }

        if (starImages.Count == 0)
            CreateRuntimeTemplate();

        if (visibleCount <= starImages.Count)
            return;

        Image template = starImages[0];
        int need = visibleCount - starImages.Count;
        for (int i = 0; i < need; i++)
        {
            Image clone = Instantiate(template, template.transform.parent);
            clone.name = targetPrefix + RuntimeCloneSuffix + "_" + i;
            clone.gameObject.SetActive(true);
            clone.raycastTarget = false;
            starImages.Add(clone);
        }
    }

    void CreateRuntimeTemplate()
    {
        GameObject go = new GameObject(RuntimeTemplateName, typeof(RectTransform), typeof(Image), typeof(StarGlowTwinkle));
        go.transform.SetParent(transform, false);

        Image img = go.GetComponent<Image>();
        img.sprite = GetGeneratedStarSprite();
        img.color = Color.white;
        img.raycastTarget = false;

        StarGlowTwinkle twinkle = go.GetComponent<StarGlowTwinkle>();
        twinkle.Configure(twinkleMinAlpha, twinkleMaxAlpha, twinkleMinSpeed, twinkleMaxSpeed);

        starImages.Add(img);
    }

    static Sprite GetGeneratedStarSprite()
    {
        if (generatedStarSprite != null)
            return generatedStarSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GeneratedStarGlow";

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float distance = Vector2.Distance(p, center) / maxDistance;
                float glow = Mathf.Clamp01(1f - distance);

                float horizontalRay = Mathf.Clamp01(1f - Mathf.Abs(y - center.y) / 2.2f) * Mathf.Clamp01(1f - Mathf.Abs(x - center.x) / 30f);
                float verticalRay = Mathf.Clamp01(1f - Mathf.Abs(x - center.x) / 2.2f) * Mathf.Clamp01(1f - Mathf.Abs(y - center.y) / 30f);
                float alpha = Mathf.Clamp01((glow * glow * 0.7f) + horizontalRay + verticalRay);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        generatedStarSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return generatedStarSprite;
    }

    void RemoveOldRuntimeClones()
    {
        var images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img != null && (img.name.Contains(RuntimeCloneSuffix) || img.name == RuntimeTemplateName))
                Destroy(img.gameObject);
        }
    }
}
