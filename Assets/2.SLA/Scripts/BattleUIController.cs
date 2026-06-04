using UnityEngine;

/// <summary>
/// [도망] 산소 패널티 전용. 필드 복귀는 UIBattleManager.CompleteFleeExit()가 담당합니다.
/// </summary>
public class BattleUIController : MonoBehaviour
{
    [Header("도망 패널티 설정")]
    [Tooltip("도망 선택 시 즉시 차감될 산소량")]
    [SerializeField] private float fleePenaltyAmount = 15f;

    [Header("컴포넌트 연결")]
    [SerializeField] private PlayerOxygen playerOxygen;
    [SerializeField] private PlayerController playerController;

    /// <summary>인스펙터에 직접 연결된 레거시 진입점.</summary>
    public void OnFleeButtonClicked()
    {
        UIBattleManager battleManager = FindAnyObjectByType<UIBattleManager>();
        if (battleManager != null)
        {
            if (!battleManager.TryBeginFleeExit())
                return;

            ApplyFleePenaltyOnly();
            battleManager.CompleteFleeExit();
            return;
        }

        ApplyFleePenaltyOnly();
        BattleEncounterContext.MarkFleeExit();

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
    }

    public void ApplyFleePenaltyOnly()
    {
        ResolveActivePlayerOxygen();
        ResolveActivePlayerController();

        if (playerOxygen != null)
            playerOxygen.ApplyFleePenalty(fleePenaltyAmount);
        else
            Debug.LogWarning("[BattleUIController] PlayerOxygen이 없어 도망 패널티는 생략합니다.");

        if (playerController != null)
            playerController.BeginPostFleeGraceWindow();
        else
            Debug.LogWarning("[BattleUIController] PlayerController를 찾지 못해 도망 후 재진입 방지 시간을 적용하지 못했습니다.");

        Debug.Log("[BattleUIController] 도망 패널티 적용 완료.");
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
