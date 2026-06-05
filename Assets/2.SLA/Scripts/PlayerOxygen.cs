using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayerOxygen : MonoBehaviour
{
    [Header("산소 설정")]
    public float maxOxygen = 100f;
    public float currentOxygen;
    public float decayRate = 1f;       // 초당 산소 감소량

    [Header("연동할 UI")]
    public Slider oxygenSlider;        // 산소 게이지만 연결 (공장 오염도 슬라이더 금지)

    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject mainHUD;

    private bool warnedMissingOxygenSlider;
    private bool warnedMissingGameOverPanel;
    private bool isOxygenGameOver;
    private UIGameOver cachedGameOverPanel;

    /// <summary>산소 게이지를 최대치로 되돌리고 게임오버 상태를 해제합니다. 챕터 전환 시 호출됩니다.</summary>
    public void ResetOxygen()
    {
        isOxygenGameOver = false;
        currentOxygen = maxOxygen;
        SyncOxygenVisual();

        if (mainHUD != null && !mainHUD.activeSelf)
            mainHUD.SetActive(true);
    }

    void Awake()
    {
        TryResolveOxygenSliderReference();
        TryResolveGameOverPanel();
    }

    void Start()
    {
        TryResolveOxygenSliderReference();
        TryResolveGameOverPanel();
        ResetOxygen();
    }

    void Update()
    {
        if (ShouldPauseOxygenSimulation() || isOxygenGameOver)
            return;

        currentOxygen -= decayRate * Time.deltaTime;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);
        SyncOxygenVisual();

        if (currentOxygen <= 0f)
            TriggerOxygenGameOver();
    }

    private void TriggerOxygenGameOver()
    {
        if (isOxygenGameOver)
            return;

        isOxygenGameOver = true;
        currentOxygen = 0f;
        SyncOxygenVisual();

        if (mainHUD != null && mainHUD.activeSelf)
            mainHUD.SetActive(false);

        UIGameOver panel = TryResolveGameOverPanel();
        if (panel != null)
        {
            panel.Show();
            return;
        }

        if (!warnedMissingGameOverPanel)
        {
            warnedMissingGameOverPanel = true;
            Debug.LogError(
                "[PlayerOxygen] UIGameOver를 찾지 못했습니다. " +
                "Player의 gameOverUI 슬롯에 UIGameOver 오브젝트를 연결하세요.");
        }

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        GameManager.Instance?.EnterGameOverFreeze();
    }

    /// <summary>
    /// 전투 씬에서 [도망] 선택 시 호출. 고정 수치만큼 산소를 즉시 차감합니다.
    /// </summary>
    public void ApplyFleePenalty(float penaltyAmount = 15f)
    {
        if (isOxygenGameOver)
            return;

        currentOxygen -= penaltyAmount;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);
        SyncOxygenVisual();

        if (currentOxygen <= 0f)
            TriggerOxygenGameOver();
        else
            Debug.Log($"[PlayerOxygen] 도망 패널티 적용! -{penaltyAmount} / 현재 산소: {currentOxygen:F1}");
    }

    private bool ShouldPauseOxygenSimulation()
    {
        return GameManager.Instance != null && GameManager.Instance.IsAwaitingPostOpeningPlaySession;
    }

    private UIGameOver TryResolveGameOverPanel()
    {
        if (cachedGameOverPanel != null)
            return cachedGameOverPanel;

        if (gameOverUI != null)
            cachedGameOverPanel = gameOverUI.GetComponent<UIGameOver>();

        if (cachedGameOverPanel == null)
        {
            cachedGameOverPanel =
                FindAnyObjectByType<UIGameOver>(FindObjectsInactive.Include);
        }

        return cachedGameOverPanel;
    }

    private void TryResolveOxygenSliderReference()
    {
        if (IsValidOxygenSlider(oxygenSlider))
            return;

        oxygenSlider = null;

        UIMainHUD[] huds = FindObjectsByType<UIMainHUD>(FindObjectsInactive.Include);
        for (int i = 0; i < huds.Length; i++)
        {
            Slider candidate = huds[i] != null ? huds[i].GetOxygenBarSlider() : null;
            if (!IsValidOxygenSlider(candidate))
                continue;

            oxygenSlider = candidate;
            return;
        }

        if (!warnedMissingOxygenSlider)
        {
            warnedMissingOxygenSlider = true;
            Debug.LogWarning(
                "[PlayerOxygen] 산소 슬라이더를 찾지 못했습니다. 공장 오염도 게이지는 건드리지 않습니다. " +
                "Player 또는 UIMainHUD의 oxygenBarSlider 연결을 확인하세요.");
        }
    }

    private void SyncOxygenVisual()
    {
        if (UIMainHUD.TryUpdateOxygenGaugeGlobal(currentOxygen, maxOxygen))
            return;

        if (!IsValidOxygenSlider(oxygenSlider))
        {
            TryResolveOxygenSliderReference();
            if (!IsValidOxygenSlider(oxygenSlider))
                return;
        }

        oxygenSlider.maxValue = maxOxygen;
        oxygenSlider.value = currentOxygen;
    }

    private static bool IsValidOxygenSlider(Slider slider)
    {
        if (slider == null)
            return false;

        string objectName = slider.gameObject.name;
        if (objectName.IndexOf("pollution", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return objectName.IndexOf("oxygen", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.Contains("산소");
    }
}
