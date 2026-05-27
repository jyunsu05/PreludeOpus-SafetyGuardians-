using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class OpeningStarWarsCrawl : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] TMP_FontAsset storyFont;
    [SerializeField] float fontSize = 58f;
    [SerializeField] Color textColor = new Color(1f, 0.82f, 0.12f, 1f);

    [Header("Crawl")]
    [SerializeField] float crawlSpeed = 38f;
    [SerializeField] float startY = -520f;
    [SerializeField] float finishPadding = 220f;

    [Header("Fake Perspective")]
    [SerializeField] float bottomScale = 2.2f;
    [SerializeField] float topScale = 0.4f;
    [SerializeField] float bottomWidth = 1500f;
    [SerializeField] float topWidth = 420f;
    [SerializeField] float lineSpacing = 90f;
    [SerializeField] float bottomSpacingMultiplier = 2.6f;
    [SerializeField] float topSpacingMultiplier = 0.65f;

    const string ContainerName = "StoryCrawlLines";
    const string LegacyObjectName = "StoryCrawlText";

    readonly List<TextMeshProUGUI> lineTexts = new List<TextMeshProUGUI>();
    RectTransform canvasRect;
    RectTransform containerRect;
    float scrollOffset;
    bool finished;

    static readonly string[] StoryLines =
    {
        "인류는 끊임없는 발전과 풍요라는 달콤한 과실을 따기 위해,",
        "매일같이 과학의 한계를 시험대 위에 올렸다.",
        "",
        "구 도심 외곽에 거대하게 솟아오른",
        "'프로메테우스 화학 공장'은",
        "그 오만한 진보의 심장부였다.",
        "",
        "신문명을 이끌어갈 혁신적인 에너지원을 발명해 내겠다는 일념 하에,",
        "통제 불가능할 정도로 위험하고 과도한 실험들이",
        "밤낮을 잊은 채 반복되었다.",
        "",
        "그러나 한계를 모르는 인간의 욕망은",
        "결국 파멸의 도화선에 불을 붙였다.",
        "",
        "어느 날 밤,",
        "무리하게 가동되던 핵심 실험로가 임계점을 넘어서며",
        "사상 유례없는 연쇄 대폭발을 일으켰다.",
        "",
        "그것은 지옥의 문이 열리는 소리였다.",
        "",
        "그 순간,",
        "인류의 기술로는 감당할 수조차 없는",
        "미지의 정제되지 않은 화학 물질들이",
        "차가운 공장 바닥과 대기 중으로",
        "무차별하게 쏟아져 나왔다."
    };

    void Awake()
    {
        canvasRect = GetComponent<RectTransform>();
        if (canvasRect.rect.width <= 0f || canvasRect.rect.height <= 0f)
            Debug.LogWarning("[OpeningStarWarsCrawl] Canvas RectTransform 크기가 0이면 글자가 보이지 않습니다.", this);

        RemoveLegacyObject();
        BuildLines();
        ResetCrawl();
    }

    void Update()
    {
        if (finished)
            return;

        scrollOffset += crawlSpeed * Time.deltaTime;
        UpdateLineTransforms();

        float lastLineY = GetRawLineY(StoryLines.Length - 1);
        if (lastLineY > GetTopY() + finishPadding)
        {
            finished = true;
            Debug.Log("Opening crawl finished");
        }
    }

    void BuildLines()
    {
        Transform oldContainer = transform.Find(ContainerName);
        if (oldContainer != null)
            Destroy(oldContainer.gameObject);

        GameObject container = new GameObject(ContainerName, typeof(RectTransform));
        container.transform.SetParent(transform, false);
        containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = canvasRect.rect.size;

        lineTexts.Clear();
        for (int i = 0; i < StoryLines.Length; i++)
        {
            GameObject go = new GameObject("StoryCrawlLine_" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(containerRect, false);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = StoryLines[i];
            if (storyFont != null)
                text.font = storyFont;

            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            lineTexts.Add(text);
        }
    }

    void ResetCrawl()
    {
        scrollOffset = 0f;
        finished = false;
        UpdateLineTransforms();
    }

    void UpdateLineTransforms()
    {
        float bottomY = GetBottomY();
        float topY = GetTopY();
        float visibleHeight = topY - bottomY;
        float currentRawY = startY + scrollOffset;

        for (int i = 0; i < lineTexts.Count; i++)
        {
            TextMeshProUGUI text = lineTexts[i];
            RectTransform rt = text.rectTransform;

            float rawY = currentRawY;
            float heightT = Mathf.InverseLerp(bottomY, topY, rawY);
            float clampedT = Mathf.Clamp01(heightT);
            float nearFactor = 1f - clampedT;

            float scale = Mathf.Lerp(topScale, bottomScale, nearFactor);
            float width = Mathf.Lerp(topWidth, bottomWidth, nearFactor);
            float widthScale = width / Mathf.Max(1f, bottomWidth);
            float spacingMultiplier = Mathf.Lerp(topSpacingMultiplier, bottomSpacingMultiplier, nearFactor);
            float alpha = Mathf.Lerp(0f, 1f, nearFactor);
            bool visible = rawY > bottomY - (lineSpacing * 2f) && rawY < topY + finishPadding;

            // 위쪽으로 갈수록 줄 간격도 눌러 소실점 쪽으로 모이는 느낌을 줍니다.
            float perspectiveY = bottomY + (clampedT * visibleHeight);
            rt.anchoredPosition = new Vector2(0f, perspectiveY);
            rt.sizeDelta = new Vector2(width, lineSpacing * 1.2f);
            rt.localScale = new Vector3(scale * widthScale, scale, 1f);

            // TMP 한 줄은 RectTransform width만 줄여도 실제 글자 폭이 크게 변하지 않아,
            // 글자 크기 자체도 같이 줄여 사다리꼴 원근감을 만듭니다.
            text.fontSize = Mathf.Lerp(fontSize * 0.45f, fontSize, nearFactor);

            Color c = textColor;
            c.a = visible ? alpha : 0f;
            text.color = c;

            // 다음 줄은 현재 줄보다 아래에서 따라오며, 가까울수록 큰 글자에 맞춰 더 멀리 띄웁니다.
            currentRawY -= lineSpacing * spacingMultiplier;
        }
    }

    float GetRawLineY(int lineIndex)
    {
        float rawY = startY + scrollOffset;
        float bottomY = GetBottomY();
        float topY = GetTopY();

        for (int i = 0; i < lineIndex; i++)
        {
            float t = Mathf.InverseLerp(bottomY, topY, rawY);
            float nearFactor = 1f - Mathf.Clamp01(t);
            float spacingMultiplier = Mathf.Lerp(topSpacingMultiplier, bottomSpacingMultiplier, nearFactor);
            rawY -= lineSpacing * spacingMultiplier;
        }

        return rawY;
    }

    float GetBottomY()
    {
        return -canvasRect.rect.height * 0.5f;
    }

    float GetTopY()
    {
        return canvasRect.rect.height * 0.5f;
    }

    void RemoveLegacyObject()
    {
        Transform old = transform.Find(LegacyObjectName);
        if (old != null)
            Destroy(old.gameObject);
    }
}
