using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIBattleManager : MonoBehaviour
{
    public event System.Action OnContaminationEmpty;
    [Header("--- 몬스터 기본 정보 UI (항상 보임) ---")]
    [SerializeField] private TextMeshProUGUI monsterNameText;       // 몬스터: name
    [SerializeField] private TextMeshProUGUI difficultyText;        // 포획 난이도: New Text
    [SerializeField] private Slider contaminationSlider;            // 오염도 게이지 바

    [Header("--- 탐색 시 통째로 열리는 부모 Panel ---")]
    [SerializeField] private GameObject scanInfoPanel;              // 3개를 하나로 묶으신 부모 오브젝트

    [Header("--- 부모 Panel 내부의 텍스트들 ---")]
    [SerializeField] private TextMeshProUGUI infectionTypeText;     // 감염 물질 : 감염물질 이름
    [SerializeField] private TextMeshProUGUI descriptionText;       // 정화 방법 : 정화 방법 설명
    [SerializeField] private TextMeshProUGUI inventoryStatusText;   // 인벤토리 상황 : 아이템 보유

    void Awake()
    {
        if (scanInfoPanel != null)
        {
            scanInfoPanel.SetActive(false);
        }
    }

    public void SetMonsterBasicUI(string name, string difficulty, int maxContamination)
    {
        monsterNameText.text = name;
        difficultyText.text = difficulty;
        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = maxContamination;
    }

    // [탐색] 버튼을 눌렀을 때 실행될 함수
    public void RevealScannedInfo(string infectionType, string description, string inventoryStatus)
    {
        if (scanInfoPanel != null)
        {
            scanInfoPanel.SetActive(true);
        }

        infectionTypeText.text = infectionType;
        descriptionText.text = description;
        inventoryStatusText.text = inventoryStatus;
    }

    public void UpdateContaminationGauge(int currentContamination)
    {
        contaminationSlider.value = currentContamination;
    }

    public void ReduceContamination(int amount)
    {
        if (contaminationSlider == null) return;

        contaminationSlider.value = Mathf.Max(0, contaminationSlider.value - amount);
        Debug.Log($"[UIBattleManager] 오염도 감소: {contaminationSlider.value}");

        if (contaminationSlider.value <= 0)
        {
            Debug.Log("[UIBattleManager] 오염도 0 도달! 정화 완료.");
            OnContaminationEmpty?.Invoke();
        }
    }
}