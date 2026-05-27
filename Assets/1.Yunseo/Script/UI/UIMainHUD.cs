using UnityEngine;
using UnityEngine.UI;

public class UIMainHUD : MonoBehaviour
{
    [Header("--- HUD 상시 자식 컴포넌트들 ---")]
    [SerializeField] private Button bagButton;        // 가방 버튼 (UI_Bag_Button)
    [SerializeField] private Slider oxygenBarSlider;  // 산소 게이지 바 (UI_OxygenBar_Slider)

    // [Header("--- [나중용] 인벤토리 UI 판넬 ---")]
    // [SerializeField] private GameObject inventoryPanel; 

    void Start()
    {
        // 1. 게임이 시작되면 가방 버튼의 클릭 이벤트를 자동으로 가로챕니다.
        if (bagButton != null)
        {
            bagButton.onClick.AddListener(OnBagButtonClick);
        }
        else
        {
            Debug.LogWarning("[UI_MainHUD] bagButton 슬롯이 비어 있습니다! 하이어라키에서 연결해 주세요.");
        }
    }

    /// <summary>
    /// ★ 나중에 플레이어 스크립트가 산소 수치를 깎을 때마다 이 함수를 호출해 줄 겁니다.
    /// </summary>
    public void UpdateOxygenGauge(float currentOxygen, float maxOxygen)
    {
        if (oxygenBarSlider != null)
        {
            oxygenBarSlider.maxValue = maxOxygen;
            oxygenBarSlider.value = currentOxygen;
        }
    }

    /// <summary>
    /// HUD 내부의 가방 버튼을 클릭했을 때 실행되는 함수
    /// </summary>
    private void OnBagButtonClick()
    {
        // 아직 인벤토리창이 없으므로 콘솔 로그로 먼저 테스트!
        Debug.Log("<color=cyan>[UI_MainHUD]</color> HUD 내부의 가방 버튼이 클릭되었습니다! 인벤토리 개방 프로토콜 가동.");

        // TODO: 나중에 인벤토리 UI 판넬이 완성되면 아래 주석을 해제합니다.
        /*
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
        }
        */
    }
}
