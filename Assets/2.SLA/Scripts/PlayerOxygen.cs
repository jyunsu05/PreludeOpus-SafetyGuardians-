using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerOxygen : MonoBehaviour
{
    private static PlayerOxygen playerInstance;

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
    private readonly List<UIBattleOxygenGauge> battleOxygenGauges = new List<UIBattleOxygenGauge>();

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
        RegisterAsPlayerInstanceIfNeeded();
        TryResolveOxygenSliderReference();
        TryResolveGameOverPanel();
    }

    void OnEnable()
    {
        RegisterAsPlayerInstanceIfNeeded();
    }

    void OnDisable()
    {
        if (playerInstance == this)
            playerInstance = null;
    }

    /// <summary>씬에 존재하는 플레이어 PlayerOxygen만 찾습니다. 프리팹 에셋 참조는 무시합니다.</summary>
    public static PlayerOxygen ResolveRuntime()
    {
        if (IsSceneInstance(playerInstance))
            return playerInstance;

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                PlayerOxygen onPlayer = playerObject.GetComponent<PlayerOxygen>();
                if (IsSceneInstance(onPlayer))
                    return onPlayer;
            }
        }
        catch (UnityException)
        {
        }

        PlayerOxygen[] candidates = FindObjectsByType<PlayerOxygen>(FindObjectsInactive.Include);
        PlayerOxygen activeOnPlayer = null;
        PlayerOxygen activeAny = null;
        PlayerOxygen sceneAny = null;

        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerOxygen candidate = candidates[i];
            if (!IsSceneInstance(candidate))
                continue;

            sceneAny = candidate;
            bool isPlayerObject = candidate.CompareTag("Player") || candidate.GetComponent<PlayerController>() != null;

            if (!candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy)
                continue;

            activeAny = candidate;
            if (isPlayerObject)
                activeOnPlayer = candidate;
        }

        if (activeOnPlayer != null)
            return activeOnPlayer;

        if (activeAny != null)
            return activeAny;

        return sceneAny;
    }

    private static bool IsSceneInstance(PlayerOxygen oxygen)
    {
        return oxygen != null && oxygen.gameObject.scene.IsValid();
    }

    private void RegisterAsPlayerInstanceIfNeeded()
    {
        if (GetComponent<PlayerController>() != null || CompareTag("Player"))
            playerInstance = this;
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
        if (GameManager.Instance == null)
            return false;

        if (GameManager.Instance.IsAwaitingPostOpeningPlaySession)
            return true;

        // 배틀 중에는 필드 감소를 멈추고 턴 행동으로만 산소가 변합니다.
        return GameManager.Instance.IsInBattle;
    }

    public void RegisterBattleOxygenGauge(UIBattleOxygenGauge gauge)
    {
        if (gauge == null || battleOxygenGauges.Contains(gauge))
            return;

        battleOxygenGauges.Add(gauge);
        gauge.UpdateGauge(currentOxygen, maxOxygen);
    }

    public void UnregisterBattleOxygenGauge(UIBattleOxygenGauge gauge)
    {
        if (gauge == null)
            return;

        battleOxygenGauges.Remove(gauge);
    }

    /// <summary>턴제 배틀 행동으로 산소를 차감합니다. 생존 여부를 반환합니다.</summary>
    public bool ApplyBattleOxygenCost(float amount)
    {
        if (isOxygenGameOver || amount <= 0f)
            return !isOxygenGameOver;

        currentOxygen -= amount;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);
        SyncOxygenVisual();

        if (currentOxygen <= 0f)
        {
            TriggerOxygenGameOver();
            return false;
        }

        Debug.Log($"[PlayerOxygen] 배틀 산소 소모 -{amount:0.#} / 현재: {currentOxygen:0.#}");
        return true;
    }

    /// <summary>몬스터 정화 성공 등 배틀 보상으로 산소를 회복합니다.</summary>
    public void ApplyBattleOxygenRestore(float amount)
    {
        if (isOxygenGameOver || amount <= 0f)
            return;

        currentOxygen += amount;
        currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);
        SyncOxygenVisual();
        Debug.Log($"[PlayerOxygen] 배틀 산소 회복 +{amount:0.#} / 현재: {currentOxygen:0.#}");
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
        SyncBattleOxygenGauges();
        UIMainHUD.TryUpdateOxygenGaugeGlobal(currentOxygen, maxOxygen);

        if (!IsValidOxygenSlider(oxygenSlider))
        {
            TryResolveOxygenSliderReference();
            if (!IsValidOxygenSlider(oxygenSlider))
                return;
        }

        oxygenSlider.maxValue = maxOxygen;
        oxygenSlider.value = currentOxygen;
    }

    private void SyncBattleOxygenGauges()
    {
        for (int i = battleOxygenGauges.Count - 1; i >= 0; i--)
        {
            UIBattleOxygenGauge gauge = battleOxygenGauges[i];
            if (gauge == null)
            {
                battleOxygenGauges.RemoveAt(i);
                continue;
            }

            gauge.UpdateGauge(currentOxygen, maxOxygen);
        }
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
