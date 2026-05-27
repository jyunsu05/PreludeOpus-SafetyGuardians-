using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class OpeningStarWarsCrawl : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float initialDelay = 0.8f;

    [Header("Text")]
    [SerializeField] TMP_FontAsset storyFont;
    [Range(28f, 84f)]
    [SerializeField] float fontSize = 52f;
    [SerializeField] FontWeight fontWeight = FontWeight.Bold;
    [SerializeField] Color textColor = new Color(1f, 0.82f, 0.12f, 1f);
    [SerializeField] float lineSpacing = 10f;
    [SerializeField] float textBoxWidth = 1200f;

    [Header("Credits Scroll")]
    [SerializeField] float crawlSpeed = 55f;
    [SerializeField] float startOffsetFromBottom = 40f;
    [SerializeField] float finishPadding = 260f;
    [SerializeField] bool fadeAtTop = true;
    [SerializeField] float topFadeRange = 220f;

    const string CreditsObjectName = "StoryCreditsText";

    RectTransform canvasRect;
    TextMeshProUGUI creditsText;
    RectTransform creditsRect;
    float creditsHeight;
    float runtimeStartY;
    float elapsed;
    float scrollOffset;
    bool finished;

    const string StoryText =
        "인류는 끊임없는 발전과 풍요라는 달콤한 과실을 따기 위해, 매일같이 과학의 한계를 시험대 위에 올렸다. " +
        "구 도심 외곽에 거대하게 솟아오른 '프로메테우스 화학 공장'은 그 오만한 진보의 심장부였다. " +
        "신문명을 이끌어갈 혁신적인 에너지원을 발명해 내겠다는 일념 하에, 통제 불가능할 정도로 위험하고 과도한 실험들이 밤낮을 잊은 채 반복되었다.\n\n" +
        "그러나 한계를 모르는 인간의 욕망은 결국 파멸의 도화선에 불을 붙였다.\n\n" +
        "어느 날 밤, 무리하게 가동되던 핵심 실험로가 임계점을 넘어서며 사상 유례없는 연쇄 대폭발을 일으켰다. " +
        "그것은 지옥의 문이 열리는 소리였다. 그 순간, 인류의 기술로는 감당할 수조차 없는 미지의 정제되지 않은 화학 물질들이 " +
        "차가운 공장 바닥과 대기 중으로 무차별하게 쏟아져 나왔다.";

    void Awake()
    {
        canvasRect = GetComponent<RectTransform>();
        BuildCreditsText();
        ResetCrawl();
    }

    void Update()
    {
        if (finished)
            return;

        elapsed += Time.deltaTime;
        if (elapsed < initialDelay)
            return;

        scrollOffset += crawlSpeed * Time.deltaTime;
        UpdateCreditsTransform();

        if (creditsRect != null && (creditsRect.anchoredPosition.y - creditsHeight) > GetTopY() + finishPadding)
            finished = true;
    }

    void BuildCreditsText()
    {
        Transform old = transform.Find(CreditsObjectName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject go = new GameObject(CreditsObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        creditsText = go.GetComponent<TextMeshProUGUI>();
        creditsRect = creditsText.rectTransform;
        creditsRect.anchorMin = new Vector2(0.5f, 0.5f);
        creditsRect.anchorMax = new Vector2(0.5f, 0.5f);
        creditsRect.pivot = new Vector2(0.5f, 1f);
        creditsRect.sizeDelta = new Vector2(textBoxWidth, 1000f);

        if (storyFont != null)
            creditsText.font = storyFont;

        creditsText.text = StoryText;
        creditsText.fontSize = fontSize;
        creditsText.fontWeight = fontWeight;
        creditsText.color = textColor;
        creditsText.alignment = TextAlignmentOptions.Top;
        creditsText.lineSpacing = lineSpacing;
        creditsText.enableWordWrapping = true;
        creditsText.overflowMode = TextOverflowModes.Overflow;
        creditsText.raycastTarget = false;

        creditsText.ForceMeshUpdate();
        creditsHeight = creditsText.preferredHeight + 120f;
        creditsRect.sizeDelta = new Vector2(textBoxWidth, creditsHeight);
    }

    void OnValidate()
    {
        fontSize = Mathf.Clamp(fontSize, 28f, 84f);

        if (!Application.isPlaying)
            return;

        if (creditsText == null || creditsRect == null)
            return;

        creditsText.fontSize = fontSize;
        creditsText.fontWeight = fontWeight;
        creditsText.lineSpacing = lineSpacing;
        creditsRect.sizeDelta = new Vector2(textBoxWidth, Mathf.Max(creditsHeight, 1f));
    }

    void ResetCrawl()
    {
        elapsed = 0f;
        scrollOffset = 0f;
        finished = false;
        runtimeStartY = GetBottomY() - startOffsetFromBottom;
        UpdateCreditsTransform();
    }

    void UpdateCreditsTransform()
    {
        if (creditsRect == null || creditsText == null)
            return;

        float y = runtimeStartY + scrollOffset;
        creditsRect.anchoredPosition = new Vector2(0f, y);

        Color c = textColor;
        if (fadeAtTop)
        {
            float bottomOfText = y - creditsHeight;
            float fadeStart = GetBottomY();
            if (bottomOfText > fadeStart)
            {
                float t = Mathf.Clamp01((bottomOfText - fadeStart) / Mathf.Max(1f, topFadeRange));
                c.a *= 1f - t;
            }
        }

        creditsText.color = c;
    }

    float GetTopY()
    {
        if (canvasRect == null || canvasRect.rect.height <= 0f)
            return Screen.height * 0.5f;

        return canvasRect.rect.height * 0.5f;
    }

    float GetBottomY()
    {
        return -GetTopY();
    }
}
