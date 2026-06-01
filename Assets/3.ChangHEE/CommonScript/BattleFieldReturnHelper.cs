using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerController가 battleSceneUI / mainHUD를 직접 켜고 끄기 때문에,
/// GameManager.ReturnToField()만으로는 전투 UI가 남을 수 있습니다.
/// OnBattleEnded에서 전투 UI를 끄고 HUD를 다시 켭니다.
/// </summary>
public class BattleFieldReturnHelper : MonoBehaviour
{
    [Header("Player와 동일하게 연결")]
    [SerializeField] private GameObject battleSceneUI;
    [SerializeField] private GameObject mainHUD;

    [Header("정화 보상 팝업 (Canvas 아래 UIAcquisitionPopup)")]
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;

    private bool subscribed;
    private Coroutine confirmRoutine;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void Update()
    {
        if (GameManager.Instance == null)
            subscribed = false;

        if (!subscribed)
            TrySubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleReturnToField;
        GameManager.Instance.OnBattleEnded += HandleReturnToField;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleReturnToField;
        subscribed = false;
    }

    private void HandleReturnToField()
    {
        ApplyFieldUI();

        if (confirmRoutine != null)
            StopCoroutine(confirmRoutine);

        confirmRoutine = StartCoroutine(ConfirmFieldUIAfterFrame());
    }

    private IEnumerator ConfirmFieldUIAfterFrame()
    {
        yield return null;
        ApplyFieldUI();
        confirmRoutine = null;
    }

    private void ApplyFieldUI()
    {
        ResetBattleButtons();

        if (battleSceneUI != null)
            battleSceneUI.SetActive(false);

        CloseAcquisitionPopup();

        var playerOxygen = FindAnyObjectByType<PlayerOxygen>();
        if (playerOxygen != null && playerOxygen.currentOxygen <= 0f)
        {
            if (mainHUD != null)
                mainHUD.SetActive(false);
            return;
        }

        if (mainHUD != null)
            mainHUD.SetActive(true);
    }

    private void CloseAcquisitionPopup()
    {
        if (acquisitionPopup != null)
        {
            acquisitionPopup.gameObject.SetActive(false);
            return;
        }

        if (battleSceneUI != null)
        {
            var popupInBattle = battleSceneUI.GetComponentInChildren<UIAcquisitionPopup>(true);
            if (popupInBattle != null)
            {
                popupInBattle.gameObject.SetActive(false);
                return;
            }
        }

        var popup = FindAnyObjectByType<UIAcquisitionPopup>(FindObjectsInactive.Include);
        if (popup != null)
            popup.gameObject.SetActive(false);
    }

    private void ResetBattleButtons()
    {
        if (battleSceneUI == null)
            return;

        var buttons = battleSceneUI.GetComponentInChildren<UIButtonContainer>(true);
        if (buttons != null)
            buttons.ResetButtonsState();
    }
}
