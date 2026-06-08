using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIInventoryItemSceneView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private TextMeshProUGUI itemTypeText;
    [SerializeField] private Button useButton;
    [SerializeField] private Image slotBackgroundImage;

    private string itemId;
    private Action<string> onUseRequested;
    private Func<bool> canUsePredicate;

    public string ItemId => itemId;
    public bool HasItem => !string.IsNullOrEmpty(itemId);

    public void Setup(string itemId, string itemName, string description, string itemType, Sprite icon = null)
    {
        this.itemId = itemId;

        if (itemNameText != null)
            itemNameText.text = itemName ?? string.Empty;

        if (itemDescriptionText != null)
            itemDescriptionText.text = description ?? string.Empty;

        if (itemTypeText != null)
            itemTypeText.text = itemType ?? string.Empty;

        if (itemIconImage != null)
            itemIconImage.sprite = icon;

        if (slotBackgroundImage == null)
            slotBackgroundImage = GetComponent<Image>();

        gameObject.SetActive(true);
        BindUseButton();
        UpdateUseButtonState();
    }

    public void ConfigureBattleUse(Action<string> onUse, Func<bool> canUse = null)
    {
        onUseRequested = onUse;
        canUsePredicate = canUse;
        BindUseButton();
        UpdateUseButtonState();
    }

    public void ClearBattleUse()
    {
        onUseRequested = null;
        canUsePredicate = null;
        UpdateUseButtonState();
    }

    public void RefreshBattleInteractable()
    {
        UpdateUseButtonState();
    }

    private void BindUseButton()
    {
        if (useButton == null)
            useButton = GetComponent<Button>();

        if (useButton == null)
            useButton = GetComponentInChildren<Button>(true);

        if (useButton == null)
            return;

        useButton.onClick.RemoveListener(HandleUseClicked);
        useButton.onClick.AddListener(HandleUseClicked);
    }

    private bool CanUseNow()
    {
        if (!HasItem || onUseRequested == null)
            return false;

        return canUsePredicate == null || canUsePredicate.Invoke();
    }

    private void UpdateUseButtonState()
    {
        bool canUse = CanUseNow();

        if (useButton != null)
        {
            useButton.interactable = canUse;
            useButton.gameObject.SetActive(HasItem && onUseRequested != null);
        }

        if (slotBackgroundImage != null)
            slotBackgroundImage.raycastTarget = canUse;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (useButton != null)
            return;

        HandleUseClicked();
    }

    private void HandleUseClicked()
    {
        if (!CanUseNow())
            return;

        onUseRequested.Invoke(itemId);
    }

    private void OnDestroy()
    {
        if (useButton != null)
            useButton.onClick.RemoveListener(HandleUseClicked);
    }
}
