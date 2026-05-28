using UnityEngine;
using TMPro;

public class UIAquisitionPopup : MonoBehaviour
{
    [Header("--- 팝업 내부 문구 ---")]
    [SerializeField] private TextMeshProUGUI rewardMessageText;

    [Header("--- 인벤토리 연동 ---")]
    [SerializeField] private UIInventory inventory;

    private string pendingItemName;
    private string pendingDescription;
    private string pendingItemType;

    public void SetupPopup(string itemName, string description, string itemType, int count)
    {
        pendingItemName = itemName;
        pendingDescription = description;
        pendingItemType = itemType;

        if (rewardMessageText != null)
            rewardMessageText.text = $"{itemName}을(를) {count}개 수집했습니다.\n아이템은 인벤토리에 자동으로 들어갑니다.";
    }

    // [확인] 버튼과 연결할 함수
    public void OnConfirmButtonClick()
    {
        Debug.Log("[UIAquisitionPopup] 확인 버튼 클릭.");

        // 인벤토리에 아이템 추가
        if (inventory != null)
            inventory.AddItem(pendingItemName, pendingDescription, pendingItemType);
        else
            Debug.LogWarning("[UIAquisitionPopup] inventory가 연결되지 않았습니다!");

        gameObject.SetActive(false);

        // TODO: 나중에 공장 맵이 완성되면 여기에 씬 전환 코드를 추가할 예정입니다.
        // UnityEngine.SceneManagement.SceneManager.LoadScene("FactoryScene");
    }
}