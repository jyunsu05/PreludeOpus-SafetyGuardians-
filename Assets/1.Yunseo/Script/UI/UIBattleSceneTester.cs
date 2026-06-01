using UnityEngine;

public class UIBattleSceneTester : MonoBehaviour
{
    [Header("--- 테스트 대상 ---")]
    [SerializeField] private GameObject battleUIPanel;
    [SerializeField] private UIBattleManager battleManager;
    [SerializeField] private UIButtonContainer buttonContainer;

    [Header("--- 테스트용 몬스터 UI 값 ---")]
    [SerializeField] private string testMonsterName = "테스트 몬스터";
    [SerializeField] private string testDifficulty = "Easy";
    [SerializeField] private int testMaxContamination = 100;

    void Start()
    {
        AutoBindIfNeeded();

        Debug.Log("[UIBattleSceneTester] 준비 완료. F1: 배틀UI 열기, F2: 탐색, F3: 정화, F4: 도망, F5: 강제 닫기");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            OpenBattleUIForTest();

        if (Input.GetKeyDown(KeyCode.F2) && buttonContainer != null)
            buttonContainer.OnSearchClick();

        if (Input.GetKeyDown(KeyCode.F3) && buttonContainer != null)
            buttonContainer.OnPurifyClick();

        if (Input.GetKeyDown(KeyCode.F4) && buttonContainer != null)
            buttonContainer.OnEscapeClick();

        if (Input.GetKeyDown(KeyCode.F5))
            ForceCloseBattleUI();
    }

    private void AutoBindIfNeeded()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<UIBattleManager>();

        if (buttonContainer == null)
            buttonContainer = FindAnyObjectByType<UIButtonContainer>();

        if (battleUIPanel == null && battleManager != null)
            battleUIPanel = battleManager.gameObject;
    }

    private void OpenBattleUIForTest()
    {
        AutoBindIfNeeded();

        if (battleUIPanel == null)
        {
            Debug.LogError("[UIBattleSceneTester] battleUIPanel이 연결되지 않았습니다.");
            return;
        }

        battleUIPanel.SetActive(true);

        if (battleManager != null)
        {
            battleManager.ResetBattleUIState();
            battleManager.SetMonsterBasicUI(testMonsterName, testDifficulty, testMaxContamination);
        }

        if (buttonContainer != null)
            buttonContainer.ResetButtonsState();

        Debug.Log("[UIBattleSceneTester] 배틀 UI 테스트 시작.");
    }

    private void ForceCloseBattleUI()
    {
        if (battleUIPanel != null)
            battleUIPanel.SetActive(false);

        Debug.Log("[UIBattleSceneTester] 배틀 UI 강제 닫기.");
    }
}
