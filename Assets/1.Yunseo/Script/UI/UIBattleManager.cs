using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIBattleManager : MonoBehaviour
{
    public event System.Action OnContaminationEmpty;
    [Header("--- 몬스터 기본 정보 UI (항상 보임) ---")]
    [SerializeField] private Image monsterImage;
    [SerializeField] private TextMeshProUGUI monsterNameText;       // 몬스터: name
    [SerializeField] private TextMeshProUGUI difficultyText;        // 포획 난이도: New Text
    [SerializeField] private Slider contaminationSlider;            // 오염도 게이지 바
    [SerializeField] private string defaultMonsterId = "M-001";

    [Header("--- 탐색 시 통째로 열리는 부모 Panel ---")]
    [SerializeField] private GameObject scanInfoPanel;              // 3개를 하나로 묶으신 부모 오브젝트

    [Header("--- 부모 Panel 내부의 텍스트들 ---")]
    [SerializeField] private TextMeshProUGUI infectionTypeText;     // 감염 물질 : 감염물질 이름
    [SerializeField] private TextMeshProUGUI descriptionText;       // 정화 방법 : 정화 방법 설명
    [SerializeField] private TextMeshProUGUI inventoryStatusText;   // 인벤토리 상황 : 아이템 보유

    private MonsterData currentMonsterData;

    void Awake()
    {
        ResetBattleUIState();
    }

    void OnEnable()
    {
        ResetBattleUIState();
        LoadMonsterFromData();
    }

    public void ResetBattleUIState()
    {
        if (scanInfoPanel != null)
            scanInfoPanel.SetActive(false);

        if (infectionTypeText != null) infectionTypeText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (inventoryStatusText != null) inventoryStatusText.text = string.Empty;

        if (contaminationSlider != null)
            contaminationSlider.value = contaminationSlider.maxValue;
    }

    public MonsterData GetCurrentMonsterData() => currentMonsterData;

    public void SetMonsterById(string monsterId)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[UIBattleManager] DataManager가 없어 몬스터 데이터를 불러올 수 없습니다.");
            return;
        }

        MonsterData data = DataManager.Instance.GetMonsterData(monsterId);
        if (data == null)
        {
            Debug.LogWarning($"[UIBattleManager] 몬스터 ID '{monsterId}' 데이터를 찾을 수 없습니다.");
            return;
        }

        ApplyMonsterData(data);
    }

    private void LoadMonsterFromData()
    {
        if (DataManager.Instance == null)
            return;

        string monsterId = defaultMonsterId;
        if (DataManager.Instance.GetMonsterData(monsterId) == null)
        {
            List<string> ids = DataManager.Instance.GetMonsterIds();
            if (ids.Count == 0)
                return;

            monsterId = ids[0];
        }

        SetMonsterById(monsterId);
    }

    private void ApplyMonsterData(MonsterData data)
    {
        currentMonsterData = data;

        string difficulty = string.IsNullOrEmpty(data.capture_difficulty) ? "Unknown" : data.capture_difficulty;
        int contamination = data.contamination_level > 0 ? data.contamination_level : 100;
        SetMonsterBasicUI(data.name, difficulty, contamination);

        if (monsterImage != null)
            monsterImage.sprite = GetMonsterSprite(data);
    }

    private Sprite GetMonsterSprite(MonsterData data)
    {
        if (AtlasManager.Instance == null || data == null)
            return null;

        if (!string.IsNullOrEmpty(data.image_key))
        {
            Sprite sprite = AtlasManager.Instance.GetMonsterSprite(data.image_key);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.id))
        {
            Sprite sprite = AtlasManager.Instance.GetMonsterSprite(data.id);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.name))
            return AtlasManager.Instance.GetMonsterSprite(data.name);

        return null;
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