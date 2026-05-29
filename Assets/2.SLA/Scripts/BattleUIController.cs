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

    private void Start()
    {
        if (playerOxygen == null)
        {
            playerOxygen = FindAnyObjectByType<PlayerOxygen>();
            if (playerOxygen == null)
                Debug.LogError("[BattleUIController] PlayerOxygen 컴포넌트를 찾을 수 없습니다! Player 오브젝트를 확인하세요.");
        }
    }

    // [도망] 버튼 OnClick() 에 연결
    public void OnFleeButtonClicked()
    {
        if (playerOxygen == null) return;

        // 1. 산소 패널티 즉시 차감
        playerOxygen.ApplyFleePenalty(fleePenaltyAmount);

        // 2. GameManager에 필드 복귀 알림 (OnBattleEnded 이벤트 발행)
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else
            Debug.LogError("[BattleUIController] GameManager를 찾을 수 없습니다!");

        Debug.Log("[BattleUIController] 도망 선택 → 패널티 적용 완료, 필드 복귀 요청");
    }
}
