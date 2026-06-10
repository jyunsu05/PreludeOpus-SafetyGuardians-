using TMPro;
using UnityEngine;

/// <summary>
/// 자동 전투 중 플레이어 입력을 막는 가림막과 상태 인디케이터 UI를 담당합니다.
/// BattleAutoManager가 표시/숨김만 제어하고, 연출 세부는 이 컴포넌트에 위임합니다.
/// </summary>
[DisallowMultipleComponent]
public class BattleAutoOverlayView : MonoBehaviour
{
    [Header("--- 가림막 ---")]
    [Tooltip("비어 있으면 이 오브젝트에서 CanvasGroup을 찾습니다.")]
    [SerializeField] private CanvasGroup inputBlocker;

    [Header("--- 인디케이터 ---")]
    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private TextMeshProUGUI indicatorText;
    [SerializeField] private string defaultMessage = "자동 정화 중...";

    public bool IsVisible { get; private set; }

    private void Awake()
    {
        ResolveInputBlocker();
        SetVisible(false);
    }

    public void SetVisible(bool visible, string message = null)
    {
        ResolveInputBlocker();
        IsVisible = visible;

        if (indicatorRoot != null)
            indicatorRoot.SetActive(visible);

        if (indicatorText != null)
            indicatorText.text = string.IsNullOrEmpty(message) ? defaultMessage : message;

        if (inputBlocker != null)
        {
            inputBlocker.alpha = visible ? 1f : 0f;
            inputBlocker.interactable = visible;
            inputBlocker.blocksRaycasts = visible;
        }
        else if (visible)
        {
            Debug.LogWarning("[BattleAutoOverlayView] CanvasGroup이 없어 입력 차단 가림막을 표시하지 못했습니다.");
        }
    }

    private void ResolveInputBlocker()
    {
        if (inputBlocker != null)
            return;

        inputBlocker = GetComponent<CanvasGroup>();
    }
}
