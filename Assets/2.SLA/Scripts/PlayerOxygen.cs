using UnityEngine;
using UnityEngine.UI; // UI 슬라이더를 만지려면 이 줄이 꼭 필요합니다!

public class PlayerOxygen : MonoBehaviour
{
    [Header("산소 설정")]
    public float maxOxygen = 100f;     // 최대 산소량
    public float currentOxygen;        // 현재 산소량
    public float decayRate = 1f;       // 초당 산소 감소량 (1초에 1씩 감소)

    [Header("연동할 UI")]
    public Slider oxygenSlider;        // 화면에 보여줄 UI 슬라이더

    // ★ 여기에 그라데이션 관련 변수 2개만 새로 추가했습니다!
    [Header("그라데이션 설정")]
    public Gradient oxygenGradient;    // 유니티 인스펙터 창에서 편집할 색상 틀
    public Image fillImage;            // 색상을 직접 바꿀 슬라이더 안쪽 알맹이(Fill)의 이미지 컴포넌트

    void Start()
    {
        // 게임이 시작되면 현재 산소를 가득 채웁니다.
        currentOxygen = maxOxygen;

        // 혹시 에디터에서 슬라이더 연결을 깜빡했다면 자동으로 찾아보는 안전장치
        if (oxygenSlider == null)
        {
            oxygenSlider = FindAnyObjectByType<Slider>();
        }

        // ★ 슬라이더가 연결되어 있다면, 그 슬라이더가 가진 '알맹이(fillRect)의 이미지'를 자동으로 찾아옵니다.
        if (oxygenSlider != null && fillImage == null)
        {
            fillImage = oxygenSlider.fillRect.GetComponent<Image>();
        }
    }

    void Update()
    {
        // 시간이 흐름에 따라 산소를 깎아내려갑니다. (Time.deltaTime = 컴퓨터 사양에 상관없이 1초를 맞춰주는 기능)
        currentOxygen -= decayRate * Time.deltaTime;

        // 산소가 0 밑으로 떨어지지 않게 막아줍니다.
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);

        // 0부터 1 사이의 비율 계산 (예: 산소가 가득 차면 1.0, 반 남으면 0.5, 다 닳으면 0.0)
        float oxygenRatio = currentOxygen / maxOxygen;

        // UI 슬라이더에 내 산소 비율을 실시간으로 반영합니다.
        if (oxygenSlider != null)
        {
            oxygenSlider.value = oxygenRatio;
        }

        // ★ 실시간으로 남은 산소 비율에 맞춰 알맹이 색상을 그라데이션 색상으로 바꿉니다!
        if (fillImage != null && oxygenGradient != null)
        {
            fillImage.color = oxygenGradient.Evaluate(oxygenRatio);
        }

        // 산소가 완전히 고갈되었을 때의 처리
        if (currentOxygen <= 0)
        {
            Debug.LogWarning("산소가 전부 고갈되었습니다! 데미지를 주거나 게임오버 처리가 필요합니다.");
            // TODO: 정윤서 팀장님이 만들 게임오버 팝업 연동 구역
        }
    }
}