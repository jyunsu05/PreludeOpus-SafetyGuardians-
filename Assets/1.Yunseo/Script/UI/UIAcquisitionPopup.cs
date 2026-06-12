using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIAcquisitionPopup : MonoBehaviour
{
    [Header("--- 팝업 내부 문구 ---")]
    [SerializeField] private TextMeshProUGUI rewardMessageText;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Image rewardItemIconImage;

    private string pendingItemId;
    private bool addToInventoryOnConfirm = true;

    public void SetupPopup(string itemId, int count, bool shouldAddToInventory = true)
    {
        addToInventoryOnConfirm = shouldAddToInventory;
        pendingItemId = ResolveInventoryItemId(itemId);

        // DataManager에서 실제 아이템 이름 가져오기
        string itemName = pendingItemId;
        string itemDescription = string.Empty;
        Sprite itemIcon = null;
        if (DataManager.Instance != null)
        {
            ItemData data = DataManager.Instance.GetItemData(pendingItemId);
            if (data != null)
            {
                itemName = data.name;
                itemDescription = data.description;
                itemIcon = GetItemSprite(data);
            }
        }

        if (itemNameText != null)
            itemNameText.text = itemName;

        if (itemDescriptionText != null)
            itemDescriptionText.text = itemDescription;

        if (rewardItemIconImage != null)
            rewardItemIconImage.sprite = itemIcon;

        if (rewardMessageText != null)
            rewardMessageText.text = $"{itemName}을(를) {count}개 수집했습니다.\n아이템은 인벤토리에 자동으로 들어갑니다.";

        UIButtonClickSoundPlayer.Instance?.PlayBattleItemPopupSound();
    }

    private string ResolveInventoryItemId(string itemId)
    {
        if (DataManager.Instance == null)
            return itemId;

        return DataManager.Instance.GetFactoryItemIdForInventory(itemId);
    }

    private Sprite GetItemSprite(ItemData data)
    {
        if (AtlasManager.Instance == null || data == null)
            return null;

        if (!string.IsNullOrEmpty(data.image_key))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.image_key);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.id))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.id);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.name))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.name);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    // [확인] 버튼과 연결할 함수
    public void OnConfirmButtonClick()
    {
        Debug.Log("[UIAcquisitionPopup] 확인 버튼 클릭.");

        if (addToInventoryOnConfirm)
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.AddItem(pendingItemId, showAcquireToast: false);
            else
                Debug.LogWarning("[UIAcquisitionPopup] InventoryManager가 없습니다!");
        }

        MonsterBattleTracker.TryRemoveEncounteredMonsterFromField();
        ExitBattleUIIfNeeded();

        gameObject.SetActive(false);

        // TODO: 나중에 공장 맵이 완성되면 여기에 씬 전환 코드를 추가할 예정입니다.
        // UnityEngine.SceneManagement.SceneManager.LoadScene("FactoryScene");
    }

    private void ExitBattleUIIfNeeded()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
    }
}
