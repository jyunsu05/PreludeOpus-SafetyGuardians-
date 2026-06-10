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

    [Header("--- 피격 사운드 ---")]
    [SerializeField] private AudioClip hitClothSoundClip;
    [SerializeField] private AudioClip hitSoundClip;
    [Tooltip("Heavy cloth 재생 후 Hit 사운드를 넣을 타이밍 비율. 0.5면 cloth 길이의 절반 지점입니다.")]
    [SerializeField] private float hitSoundDelayRatio = 0.5f;

    private static Sprite fallbackWhiteSprite;

    private Color overlayBaseColor;

    public float HitOverlayDuration => hitOverlayDuration;
    public AudioClip HitClothSoundClip => hitClothSoundClip;
    public AudioClip ImpactHitSoundClip => hitSoundClip;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlaySprite();
        CacheOverlayBaseColor();
        EnsureRootActive();
        HideHitOverlay();
    }

    private void OnEnable()
    {
        EnsureRootActive();
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

        EnsureHierarchyActive();
        EnsureRootActive();

        if (transform is RectTransform hitVfxRect)
            hitVfxRect.SetAsLastSibling();

        Color color = overlayBaseColor;
        color.a = hitOverlayAlpha;
        hitOverlayImage.color = color;
        hitOverlayImage.raycastTarget = false;
        hitOverlayImage.enabled = true;
        hitOverlayImage.gameObject.SetActive(true);
        hitOverlayImage.rectTransform.SetAsLastSibling();
    }

    public float GetImpactHitDelay()
    {
        if (hitClothSoundClip == null || hitSoundClip == null)
            return 0f;

        return hitClothSoundClip.length * Mathf.Clamp01(hitSoundDelayRatio);
    }

    public float GetImpactHitSoundDuration()
    {
        return hitSoundClip != null ? hitSoundClip.length : 0f;
    }

    public float GetTotalHitSoundDuration()
    {
        if (hitClothSoundClip != null && hitSoundClip != null)
            return GetImpactHitDelay() + hitSoundClip.length;

        if (hitClothSoundClip != null)
            return hitClothSoundClip.length;

        if (hitSoundClip != null)
            return hitSoundClip.length;

        return 0f;
    }

    public void HideHitOverlay()
    {
        if (hitOverlayImage == null)
            return;

        hitOverlayImage.enabled = false;
        hitOverlayImage.gameObject.SetActive(false);
    }

    private void EnsureRootActive()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private void EnsureHierarchyActive()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
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
