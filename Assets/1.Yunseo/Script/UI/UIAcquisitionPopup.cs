using UnityEngine;
using TMPro;

public class UIAcquisitionPopup : MonoBehaviour
{
    [Header("--- 팝업 내부 문구 ---")]
    [SerializeField] private TextMeshProUGUI rewardMessageText;

    private string pendingItemId;

    public void SetupPopup(string itemId, int count)
    {
        pendingItemId = itemId;

        // DataManager에서 실제 아이템 이름 가져오기
        string itemName = itemId;
        if (DataManager.Instance != null)
        {
            ItemData data = DataManager.Instance.GetItemData(itemId);
            if (data != null) itemName = data.name;
        }

        if (rewardMessageText != null)
            rewardMessageText.text = $"{itemName}을(를) {count}개 수집했습니다.\n아이템은 인벤토리에 자동으로 들어갑니다.";
    }

    // [확인] 버튼과 연결할 함수
    public void OnConfirmButtonClick()
    {
        Debug.Log("[UIAcquisitionPopup] 확인 버튼 클릭.");

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.AddItem(pendingItemId);
        else
            Debug.LogWarning("[UIAcquisitionPopup] InventoryManager가 없습니다!");

        gameObject.SetActive(false);

        // TODO: 나중에 공장 맵이 완성되면 여기에 씬 전환 코드를 추가할 예정입니다.
        // UnityEngine.SceneManagement.SceneManager.LoadScene("FactoryScene");
    }
}
