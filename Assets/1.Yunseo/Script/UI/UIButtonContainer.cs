using UnityEngine;
using UnityEngine.UI;

public class UIButtonContainer : MonoBehaviour
{
    // Canvas에 붙어있는 전체 UI 매니저를 참조하기 위한 변수
    private UIBattleManager uiManager;

    [Header("--- 자식 액션 버튼들 ---")]
    [SerializeField] private Button searchButton;   // 탐색 버튼
    [SerializeField] private Button purifyButton;   // 정화 버튼
    [SerializeField] private Button escapeButton;   // 도망 버튼

    [Header("--- 아이템 획득 팝업 ---")]
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;

    private bool isScanned = false; // 탐색 완료 여부 판별

    void Start()
    {
        uiManager = FindAnyObjectByType<UIBattleManager>();

        if (uiManager == null)
        {
            Debug.LogError("[UIButtonContainer] 상위 오브젝트에서 BattleUIManager를 찾을 수 없습니다!");
        }
        else
        {
            uiManager.OnContaminationEmpty += OnContaminationCleared;
        }

        ResetButtonsState();
    }

    void OnDestroy()
    {
        if (uiManager != null)
            uiManager.OnContaminationEmpty -= OnContaminationCleared;
    }

    // 배틀 시작 시 버튼 상태를 초기화하는 함수
    public void ResetButtonsState()
    {
        isScanned = false;
        
        if (searchButton != null) searchButton.interactable = true;
        if (purifyButton != null) purifyButton.gameObject.SetActive(false);
        if (escapeButton != null) escapeButton.gameObject.SetActive(true);
    }

    // [탐색] 버튼 클릭 이벤트
    public void OnSearchClick()
    {
        Debug.Log("[UIButtonContainer] OnSearchClick 호출됨.");

        if (isScanned) return; // 중복 실행 방지

        isScanned = true;

        // 버튼 상태 전환
        if (searchButton != null) searchButton.interactable = false;

        if (purifyButton != null)
        {
            purifyButton.gameObject.SetActive(true);
            Debug.Log("[UIButtonContainer] 정화 버튼 활성화 완료.");
        }
        else
        {
            Debug.LogError("[UIButtonContainer] purifyButton이 null입니다! 인스펙터 확인 필요.");
        }

        // TODO: JSON 데이터 연동 후 실제 데이터로 교체
        if (uiManager != null)
            uiManager.RevealScannedInfo("감염물질 이름", "정화 방법 설명", "아이템 보유 현황");
    }

    // [정화] 버튼 클릭 이벤트
    public void OnPurifyClick()
    {
        if (!isScanned)
        {
            Debug.LogWarning("[UIButtonContainer] 탐색이 완료되지 않아 정화할 수 없습니다.");
            return;
        }

        Debug.Log("[UIButtonContainer] 정화 약제를 살포합니다.");

        if (uiManager != null)
            uiManager.ReduceContamination(10);
    }

    // [도망] 버튼 클릭 이벤트
    public void OnEscapeClick()
    {
        Debug.Log("[UIButtonContainer] 전투 이탈 시도.");
        
        // TODO: 나중에 씬 전환 기능이 들어갈 자리
    }

    // 오염도 0 도달 시 호출 - 모든 버튼 비활성화
    private void OnContaminationCleared()
    {
        if (searchButton != null) searchButton.gameObject.SetActive(false);
        if (purifyButton != null) purifyButton.gameObject.SetActive(false);
        if (escapeButton != null) escapeButton.gameObject.SetActive(false);

        Debug.Log("[UIButtonContainer] 정화 완료 - 모든 버튼 비활성화.");

        if (acquisitionPopup != null)
        {
            acquisitionPopup.gameObject.SetActive(true);
            acquisitionPopup.SetupPopup("MI-101", 1); // TODO: 실제 몬스터 드롭 데이터로 교체
        }
        else
        {
            Debug.LogWarning("[UIButtonContainer] acquisitionPopup이 연결되지 않았습니다!");
        }
    }
}