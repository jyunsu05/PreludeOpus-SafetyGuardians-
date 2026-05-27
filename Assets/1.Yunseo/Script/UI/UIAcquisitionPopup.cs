using UnityEngine;
using TMPro;

public class UIAquisitionPopup : MonoBehaviour
{
    [Header("--- 팝업 내부 문구 ---")]
    // 아까 정한 이름으로 텍스트 컴포넌트를 연결합니다.
    [SerializeField] private TextMeshProUGUI rewardMessageText; 

    // 나중에 BattleUIManager나 데이터 매니저가 이 함수를 호출하며 아이템 정보를 던져줄 겁니다.
    public void SetupPopup(string itemName, int count)
    {
        if (rewardMessageText != null)
        {
            // {아이템 이름}을(를) {개수}개 수집했습니다. 문구 완성
            rewardMessageText.text = $"{itemName}을(를) {count}개 수집했습니다.\n아이템은 인벤토리에 자동으로 들어갑니다.";
        }
    }

    // [확인] 버튼과 연결할 함수
    public void OnConfirmButtonClick()
    {
        Debug.Log("[UIAquisitionPopup] 확인 버튼 클릭. 창을 닫습니다.");

        // 우선은 현재 팝업창 오브젝트를 화면에서 안 보이게 비활성화
        gameObject.SetActive(false);

        // TODO: 나중에 공장 맵이 완성되면 여기에 씬 전환 코드를 추가할 예정입니다.
        // UnityEngine.SceneManagement.SceneManager.LoadScene("FactoryScene");
    }
}