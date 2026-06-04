using UnityEngine;

/// <summary>
/// [도망] 버튼 전용 컨트롤러 (SLA 담당)
/// 실제 버튼 OnClick 진입점은 UIButtonContainer.OnEscapeClick() 입니다.
/// </summary>
public class BattleUIController : MonoBehaviour
{
    [Header("도망 패널티 설정")]
    [Tooltip("도망 선택 시 즉시 차감될 산소량")]
    [SerializeField] private float fleePenaltyAmount = 15f;

    [Header("컴포넌트 연결")]
    [SerializeField] private PlayerOxygen playerOxygen;
    [SerializeField] private PlayerController playerController;

    private bool isFleeProcessing;

    private void OnEnable()
    {
        isFleeProcessing = false;
    }

    public void OnFleeButtonClicked()
    {
        if (isFleeProcessing)
            return;

        isFleeProcessing = true;

        ResolveActivePlayerOxygen();
        ResolveActivePlayerController();

        if (playerOxygen != null)
            playerOxygen.ApplyFleePenalty(fleePenaltyAmount);
        else
            Debug.LogWarning("[BattleUIController] PlayerOxygen이 없어 도망 패널티는 생략하고 배틀 종료만 진행합니다.");

        if (playerController != null)
            playerController.BeginPostFleeGraceWindow();
        else
            Debug.LogWarning("[BattleUIController] PlayerController를 찾지 못해 도망 후 재진입 방지 시간을 적용하지 못했습니다.");

        BattleEncounterContext.MarkFleeExit();

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

    private void ResolveActivePlayerController()
    {
        if (playerController != null && playerController.isActiveAndEnabled)
            return;

        PlayerController found = FindAnyObjectByType<PlayerController>();
        if (found != null && found.isActiveAndEnabled)
        {
            playerController = found;
            return;
        }

        playerController = null;
    }
}
