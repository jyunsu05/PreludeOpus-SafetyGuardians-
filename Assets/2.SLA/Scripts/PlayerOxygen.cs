using UnityEngine;
using UnityEngine.UI;

public class PlayerOxygen : MonoBehaviour
{
    [Header("산소 설정")]
    public float maxOxygen = 100f;
    public float currentOxygen;
    public float decayRate = 1f;       // 초당 산소 감소량

    [Header("연동할 UI")]
    public Slider oxygenSlider;        // HUD 프리팹의 산소 게이지 슬라이더

    void Start()
    {
        currentOxygen = maxOxygen;

        if (oxygenSlider == null)
        {
            oxygenSlider = FindAnyObjectByType<Slider>();
        }

        // 슬라이더 범위를 코드 기준(0~1)으로 강제 통일
        // Inspector에서 Max Value가 다르게 설정되어 있어도 항상 올바르게 동작합니다.
        if (oxygenSlider != null)
        {
            oxygenSlider.minValue = 0f;
            oxygenSlider.maxValue = 1f;
            oxygenSlider.value = 1f;
        }
    }

    void Update()
    {
        currentOxygen -= decayRate * Time.deltaTime;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);

        if (oxygenSlider != null)
        {
            oxygenSlider.value = currentOxygen / maxOxygen;
        }

        if (currentOxygen <= 0)
        {
            Debug.LogWarning("산소가 전부 고갈되었습니다! 데미지를 주거나 게임오버 처리가 필요합니다.");
            // TODO: 정윤서 팀장님이 만들 게임오버 팝업 연동 구역
        }
    }

    /// <summary>
    /// 전투 씬에서 [도망] 선택 시 호출. 고정 수치만큼 산소를 즉시 차감합니다.
    /// BattleUIController.OnFleeButtonClicked()에서 호출됩니다.
    /// </summary>
    public void ApplyFleePenalty(float penaltyAmount = 15f)
    {
        currentOxygen -= penaltyAmount;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);

        if (oxygenSlider != null)
            oxygenSlider.value = currentOxygen / maxOxygen;

        Debug.Log($"[PlayerOxygen] 도망 패널티 적용! -{penaltyAmount} / 현재 산소: {currentOxygen:F1}");
    }
}