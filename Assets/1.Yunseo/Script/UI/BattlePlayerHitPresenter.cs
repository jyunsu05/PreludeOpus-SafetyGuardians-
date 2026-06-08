using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 몬스터 공격 시 플레이어 화면에 반투명 Hit 이미지를 잠깐 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class BattlePlayerHitPresenter : MonoBehaviour
{
    [Header("--- 플레이어 Hit 오버레이 ---")]
    [Tooltip("공격 시 잠깐 켤 UI Image. 비우면 자식 HitEffectRoot Image를 자동 탐색합니다.")]
    [SerializeField] private Image hitOverlayImage;
    [SerializeField] private float hitOverlayAlpha = 0.35f;
    [FormerlySerializedAs("hitAnimationDuration")]
    [SerializeField] private float hitOverlayDuration = 0.45f;

    private static Sprite fallbackWhiteSprite;

    private Color overlayBaseColor;

    public float HitOverlayDuration => hitOverlayDuration;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlaySprite();
        CacheOverlayBaseColor();
        HideHitOverlay();
    }

    public void ShowHitOverlay()
    {
        ResolveReferences();
        EnsureOverlaySprite();
        if (hitOverlayImage == null)
        {
            Debug.LogWarning("[BattlePlayerHitPresenter] Hit Overlay Image가 연결되지 않았습니다.");
            return;
        }

        gameObject.SetActive(true);

        Color color = overlayBaseColor;
        color.a = hitOverlayAlpha;
        hitOverlayImage.color = color;
        hitOverlayImage.raycastTarget = false;
        hitOverlayImage.enabled = true;
        hitOverlayImage.gameObject.SetActive(true);
        hitOverlayImage.rectTransform.SetAsLastSibling();
    }

    public void HideHitOverlay()
    {
        if (hitOverlayImage != null)
        {
            hitOverlayImage.gameObject.SetActive(false);
            hitOverlayImage.enabled = true;
        }

        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (hitOverlayImage != null)
            return;

        Transform root = transform.Find("HitEffectRoot");
        if (root != null)
            hitOverlayImage = root.GetComponent<Image>();

        if (hitOverlayImage == null)
            hitOverlayImage = GetComponentInChildren<Image>(true);
    }

    private void EnsureOverlaySprite()
    {
        if (hitOverlayImage == null || hitOverlayImage.sprite != null)
            return;

        hitOverlayImage.sprite = GetFallbackWhiteSprite();
    }

    private static Sprite GetFallbackWhiteSprite()
    {
        if (fallbackWhiteSprite != null)
            return fallbackWhiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "BattlePlayerHitOverlay",
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        fallbackWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        fallbackWhiteSprite.name = "BattlePlayerHitOverlaySprite";
        return fallbackWhiteSprite;
    }

    private void CacheOverlayBaseColor()
    {
        if (hitOverlayImage == null)
        {
            overlayBaseColor = Color.red;
            return;
        }

        overlayBaseColor = hitOverlayImage.color;
        overlayBaseColor.a = 1f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hitOverlayDuration <= 0f)
            hitOverlayDuration = 0.45f;

        hitOverlayAlpha = Mathf.Clamp01(hitOverlayAlpha);
    }
#endif
}
