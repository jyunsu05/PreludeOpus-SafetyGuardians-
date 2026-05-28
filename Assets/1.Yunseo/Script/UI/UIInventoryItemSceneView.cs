using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInventoryItemSceneView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private TextMeshProUGUI itemTypeText;

    public void Setup(string itemName, string description, string itemType, Sprite icon = null)
    {
        if (itemNameText != null)        itemNameText.text = itemName;
        if (itemDescriptionText != null) itemDescriptionText.text = description;
        if (itemTypeText != null)        itemTypeText.text = itemType;
        if (itemIconImage != null && icon != null) itemIconImage.sprite = icon;
    }
}
