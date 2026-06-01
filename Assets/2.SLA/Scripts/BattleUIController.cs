using UnityEngine;

/// <summary>
/// [도망] 버튼 전용 컨트롤러 (SLA 담당)
/// 탐색·정화 버튼은 팀장(UIBattleManager)이 직접 담당합니다.
///
/// [팀장에게 요청할 Inspector 설정]
/// - BattleScene의 [도망] 버튼 OnClick() 에 → BattleUIController.OnFleeButtonClicked() 연결
/// - Player Oxygen 슬롯에 Player 오브젝트의 PlayerOxygen 컴포넌트 연결
/// </summary>
public class BattleUIController : MonoBehaviour
{
    [Header("도망 패널티 설정")]
    [Tooltip("도망 선택 시 즉시 차감될 산소량")]
    [SerializeField] private float fleePenaltyAmount = 15f;

    [Header("컴포넌트 연결")]
    [SerializeField] private PlayerOxygen playerOxygen;

    private bool isFleeProcessing;
    private UnityEngine.UI.Button fleeButton;

    private void Start()
    {
        isFleeProcessing = false;
        CacheAndBindButton();
        ResolveActivePlayerOxygen();
    }

    private void OnEnable()
    {
        isFleeProcessing = false;

        CacheAndBindButton();

        if (fleeButton != null)
            fleeButton.interactable = true;
    }

    private void OnDisable()
    {
        if (fleeButton != null)
            fleeButton.onClick.RemoveListener(OnFleeButtonClicked);
    }

    private void CacheAndBindButton()
    {
        if (fleeButton == null)
            fleeButton = GetComponent<UnityEngine.UI.Button>();

        if (fleeButton == null)
            return;

        // 씬 오버라이드로 Persistent OnClick이 깨져도 런타임 바인딩으로 도망 동작을 보장합니다.
        fleeButton.onClick.RemoveListener(OnFleeButtonClicked);
        fleeButton.onClick.AddListener(OnFleeButtonClicked);
    }

    // [도망] 버튼 OnClick() 에 연결
    public void OnFleeButtonClicked()
    {
        if (isFleeProcessing)
            return;

        isFleeProcessing = true;

        if (fleeButton != null)
            fleeButton.interactable = false;

        ResolveActivePlayerOxygen();

        // 1. 산소 패널티 즉시 차감 (컴포넌트가 있을 때만)
        if (playerOxygen != null)
            playerOxygen.ApplyFleePenalty(fleePenaltyAmount);
        else
            Debug.LogWarning("[BattleUIController] PlayerOxygen이 없어 도망 패널티는 생략하고 배틀 종료만 진행합니다.");

        // 2. GameManager에 필드 복귀 알림 (OnBattleEnded 이벤트 발행)
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
        else
            Debug.LogError("[BattleUIController] GameManager를 찾을 수 없습니다!");

        Debug.Log("[BattleUIController] 도망 선택 → 패널티 적용 완료, 필드 복귀 요청");
    }

    private void ResolveActivePlayerOxygen()
    {
        if (playerOxygen != null && playerOxygen.isActiveAndEnabled)
            return;

        PlayerOxygen found = FindAnyObjectByType<PlayerOxygen>();
        if (found != null && found.isActiveAndEnabled)
        {
            playerOxygen = found;
            return;
        }

        playerOxygen = null;
    }
}
