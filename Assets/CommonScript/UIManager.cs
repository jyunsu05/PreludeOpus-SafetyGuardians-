using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("--- UI 패널 ---")]
    [SerializeField] private UIInventory inventory;
    [SerializeField] private UIAquisitionPopup acquisitionPopup;
    [SerializeField] private GameObject battleUIPanel;
    [SerializeField] private UIMainHUD mainHUD;

    [Header("--- 선택: 오염도 UI ---")]
    [SerializeField] private Slider pollutionSlider;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        CloseAllPanels();
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBattleStarted += OpenBattleUI;
            GameManager.Instance.OnBattleEnded += CloseBattleUI;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBattleStarted -= OpenBattleUI;
            GameManager.Instance.OnBattleEnded -= CloseBattleUI;
        }
    }

    // --- 인벤토리 ---
    public void OpenInventory()
    {
        if (inventory != null)
            inventory.Open();
        else
            Debug.LogWarning("[UIManager] inventory가 연결되지 않았습니다.");
    }

    public void CloseInventory()
    {
        if (inventory != null)
            inventory.Close();
    }

    public void ToggleInventory()
    {
        if (inventory == null) return;
        bool isOpen = inventory.gameObject.activeSelf;
        if (isOpen) inventory.Close();
        else inventory.Open();
    }

    // --- 아이템 획득 팝업 (데이터 전달 + 화면 갱신) ---
    public void ShowAcquisitionPopup(string itemId, int count)
    {
        if (acquisitionPopup == null)
        {
            Debug.LogWarning("[UIManager] acquisitionPopup이 연결되지 않았습니다.");
            return;
        }

        acquisitionPopup.SetupPopup(itemId, count);
        acquisitionPopup.gameObject.SetActive(true);
    }

    public void CloseAcquisitionPopup()
    {
        SetPanelActive(acquisitionPopup != null ? acquisitionPopup.gameObject : null, false);
    }

    // --- 배틀 UI ---
    public void OpenBattleUI() => SetPanelActive(battleUIPanel, true);
    public void CloseBattleUI() => SetPanelActive(battleUIPanel, false);

    // --- HUD ---
    public void UpdateOxygenGauge(float currentOxygen, float maxOxygen)
    {
        if (mainHUD != null)
            mainHUD.UpdateOxygenGauge(currentOxygen, maxOxygen);
    }

    // PollutionManager 등 외부에서 비율만 넘겨 호출 (UIManager는 PollutionManager를 직접 참조하지 않음)
    public void UpdatePollutionBar(float ratio)
    {
        if (pollutionSlider != null)
            pollutionSlider.value = Mathf.Clamp01(ratio);
    }

    // --- 범용 ---
    public void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    public void CloseAllPanels()
    {
        CloseInventory();
        CloseAcquisitionPopup();
        CloseBattleUI();
    }
}
