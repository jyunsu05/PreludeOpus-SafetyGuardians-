using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 에디터/개발용 배틀 UI 단축키. 빌드에서는 기본 비활성입니다.
/// </summary>
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

    [Header("--- 디버그 단축키 (F1~F5) ---")]
    [Tooltip("켜면 F3 정화 등 키 입력이 동작합니다. 일반 플레이 시 끄세요.")]
    [SerializeField] private bool enableDebugHotkeys;

    private void Start()
    {
        AutoBindIfNeeded();

#if UNITY_EDITOR
        if (!enableDebugHotkeys)
            Debug.Log("[UIBattleSceneTester] 디버그 단축키 비활성. 필요 시 인스펙터에서 Enable Debug Hotkeys를 켜세요.");
        else
            Debug.Log("[UIBattleSceneTester] F1: 배틀UI, F2: 탐색, F3: 정화, F4: 도망, F5: 닫기");
#else
        enableDebugHotkeys = false;
#endif
    }

    private void Update()
    {
        if (!enableDebugHotkeys)
            return;

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

            string randomMonsterId = GetRandomMonsterIdFromJson();
            if (!string.IsNullOrEmpty(randomMonsterId))
            {
                battleManager.SetMonsterById(randomMonsterId);
                Debug.Log($"[UIBattleSceneTester] 랜덤 몬스터 로드: {randomMonsterId}");
            }
            else
            {
                battleManager.SetMonsterBasicUI(testMonsterName, testDifficulty, testMaxContamination);
                Debug.LogWarning("[UIBattleSceneTester] 몬스터 JSON 목록이 없어 기본 테스트 UI 값을 사용합니다.");
            }
        }

        if (buttonContainer != null)
            buttonContainer.ResetButtonsState();

        Debug.Log("[UIBattleSceneTester] 배틀 UI 테스트 시작.");
    }

    private string GetRandomMonsterIdFromJson()
    {
        if (DataManager.Instance == null)
            return null;

        List<string> monsterIds = DataManager.Instance.GetMonsterIds();
        if (monsterIds == null || monsterIds.Count == 0)
            return null;

        int randomIndex = Random.Range(0, monsterIds.Count);
        return monsterIds[randomIndex];
    }

    private void ForceCloseBattleUI()
    {
        if (battleUIPanel != null)
            battleUIPanel.SetActive(false);

        Debug.Log("[UIBattleSceneTester] 배틀 UI 강제 닫기.");
    }
}
