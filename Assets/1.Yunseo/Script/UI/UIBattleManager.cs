using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class UIBattleManager : MonoBehaviour
{
    private const int DefaultContaminationLevel = 100;
    private static readonly Dictionary<string, int> contaminationProgressByMonsterId = new Dictionary<string, int>();
    private static string lastResolvedEncounterMonsterId;

    public static void ResetSavedContaminationProgress()
    {
        contaminationProgressByMonsterId.Clear();
        lastResolvedEncounterMonsterId = null;
    }

    public event System.Action OnContaminationEmpty;
    [Header("--- 몬스터 기본 정보 UI (항상 보임) ---")]
    [SerializeField] private Image monsterImage;
    [SerializeField] private TextMeshProUGUI monsterNameText;       // 몬스터: name
    [SerializeField] private TextMeshProUGUI difficultyText;        // 포획 난이도: New Text
    [SerializeField] private Slider contaminationSlider;            // 오염도 게이지 바
    [SerializeField] private string defaultMonsterId = string.Empty;

    [Header("--- 탐색 시 통째로 열리는 부모 Panel ---")]
    [SerializeField] private GameObject scanInfoPanel;              // 3개를 하나로 묶으신 부모 오브젝트

    [Header("--- 부모 Panel 내부의 텍스트들 ---")]
    [SerializeField] private TextMeshProUGUI infectionTypeText;     // 감염 물질 : 감염물질 이름
    [SerializeField] private TextMeshProUGUI descriptionText;       // 정화 방법 : 정화 방법 설명
    [SerializeField] private TextMeshProUGUI inventoryStatusText;   // 인벤토리 상황 : 아이템 보유

    [Header("--- 정화 / 도망 상태 제약 ---")]
    [SerializeField] private int purifyDurabilityPerUse = 10;

    [Header("--- 턴제 배틀 ---")]
    [SerializeField] private BattleTurnController turnController;

    [Header("--- 배틀 산소 UI ---")]
    [Tooltip("배틀 씬에 만든 산소 게이지(UIBattleOxygenGauge). 비우면 자식에서 자동 탐색합니다.")]
    [SerializeField] private UIBattleOxygenGauge battleOxygenGauge;

    private const string DefaultPurifyItemId = "MI-101";

    public bool IsScanned { get; private set; }
    public bool IsPurifying { get; private set; }
    public bool IsEscapeLocked { get; private set; }

    /// <summary>true면 도망 UI를 숨깁니다(UIButtonContainer가 SetActive 처리).</summary>
    public event Action<bool> OnEscapeLockChanged;

    private MonsterData currentMonsterData;
    private string currentMonsterId;
    private int contaminationAtBattleEntry;
    private bool isSubscribedToBattleEnded;
    private bool hasFinalizedContaminationForSession;
    private bool isProcessingBattleExit;
    private PlayerController lockedPlayerController;
    private Rigidbody2D lockedPlayerRigidbody;
    private bool wasPlayerRigidbodySimulated;
    private RigidbodyConstraints2D playerConstraints;
    private readonly List<MonsterPhysicsSnapshot> lockedMonsters = new List<MonsterPhysicsSnapshot>();

    private sealed class MonsterPhysicsSnapshot
    {
        public Rigidbody2D rigidbody;
        public bool wasSimulated;
        public RigidbodyConstraints2D constraints;
    }

    void Awake()
    {
    }

    void OnEnable()
    {
        hasFinalizedContaminationForSession = false;
        isProcessingBattleExit = false;
        SubscribeBattleEnded();
        ExitBattle();
        NotifyEscapeUnlockedForNewBattle();
        ResetBattleUIState();
        LoadMonsterFromData();
        EnsureEnemyStatus();
        EnsureTurnController();
        ResolveAndBindPlayerOxygen();
        turnController?.BeginBattle();
        LockPlayerMovementAtBattleEntry();
        LockMonsterMovementAtBattleEntry();
        DisableContaminationSliderDirectInput();
    }

    private void DisableContaminationSliderDirectInput()
    {
        if (contaminationSlider == null)
            return;

        contaminationSlider.interactable = false;
    }

    void OnDisable()
    {
        UnsubscribeBattleEnded();
        ExitBattle();
        FinalizeContaminationOnce();
        ForceRestoreFieldPhysics();
    }

    void OnDestroy()
    {
        UnsubscribeBattleEnded();
        ForceRestoreFieldPhysics();
    }

    public bool CanAttemptEscape =>
        !IsPurifying &&
        !IsEscapeLocked &&
        !isProcessingBattleExit &&
        IsPlayerTurnActive();

    public BattleTurnController TurnController => turnController;
    public bool IsPlayerTurnActive()
    {
        ResolveTurnController();
        return turnController == null || turnController.IsPlayerTurn;
    }

    /// <summary>탐색 성공 — 정화 버튼을 켤 수 있는 상태로 전환합니다.</summary>
    public void NotifySearchCompleted()
    {
        IsScanned = true;
    }

    /// <summary>정화 버튼(OnClickPurify) — 아이템 확인 후 정화 시작 시 도망을 즉시 잠급니다.</summary>
    public bool OnClickPurify(out int consumedDurability)
    {
        consumedDurability = 0;

        if (IsPurifying)
            return false;

        if (!IsPlayerTurnActive())
        {
            Debug.LogWarning("[UIBattleManager] 플레이어 턴이 아닙니다.");
            return false;
        }

        if (!IsScanned)
        {
            Debug.LogWarning("[UIBattleManager] 탐색 전에는 정화할 수 없습니다.");
            return false;
        }

        string itemId = GetRequiredPurifyItemId();
        if (!CanPurifyWithInventory(itemId))
            return false;

        BeginPurifySession();

        int consumeRequest = Mathf.Max(1, purifyDurabilityPerUse);
        consumedDurability = InventoryManager.Instance.ConsumeItemDurability(itemId, consumeRequest);
        if (consumedDurability <= 0)
        {
            Debug.LogWarning("[UIBattleManager] 아이템 내구도 소모 실패 — 정화를 취소합니다.");
            EndPurifyAttempt(unlockEscape: true);
            return false;
        }

        ResolveTurnController();
        int basePurifyDamage = consumedDurability;
        int finalPurifyDamage = turnController != null
            ? turnController.CalculateAmplifiedContaminationDamage(consumedDurability)
            : consumedDurability;

        ReduceContamination(finalPurifyDamage);
        EndPurifyAttempt(unlockEscape: false);

        if (GetCurrentContamination() > 0)
        {
            ResolveTurnController();
            turnController?.TryResolvePlayerPurify(basePurifyDamage, finalPurifyDamage);
        }

        return true;
    }

    public bool CanPurifyWithInventory(string itemId = null)
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = GetRequiredPurifyItemId();

        if (InventoryManager.Instance == null || string.IsNullOrEmpty(itemId))
            return false;

        if (!InventoryManager.Instance.HasItem(itemId))
            return false;

        return InventoryManager.Instance.GetItemRemainingDurability(itemId) > 0;
    }

    public string GetRequiredPurifyItemId()
    {
        if (currentMonsterData == null)
            return DefaultPurifyItemId;

        if (!string.IsNullOrEmpty(currentMonsterData.drop_item_id))
            return currentMonsterData.drop_item_id;

        if (currentMonsterData.drop_items != null && currentMonsterData.drop_items.Count > 0)
            return currentMonsterData.drop_items[0].item_id;

        return DefaultPurifyItemId;
    }

    public string BuildInventoryStatusText()
    {
        if (currentMonsterData == null)
            return "필요 아이템 정보 없음";

        string itemId = GetRequiredPurifyItemId();
        if (string.IsNullOrEmpty(itemId))
            return "필요 아이템 정보 없음";

        string itemName = ResolveItemDisplayName(itemId);
        if (!CanPurifyWithInventory(itemId))
            return $"{itemName} 없음";

        int remaining = InventoryManager.Instance.GetItemRemainingDurability(itemId);
        return $"{itemName} 보유 {remaining}";
    }

    /// <summary>배틀 종료·UI 비활성 시 상태/도망 잠금을 방어적으로 해제합니다.</summary>
    public void ExitBattle()
    {
        IsScanned = false;
        IsPurifying = false;
        isProcessingBattleExit = false;
        SetEscapeLocked(false);
    }

    /// <summary>도망 버튼 광클 방지. 성공 시 MarkFleeExit까지 처리됨.</summary>
    public bool TryBeginFleeExit()
    {
        if (!CanAttemptEscape)
        {
            if (IsPurifying || IsEscapeLocked)
                Debug.Log("[UIBattleManager] 정화 진행 중 — 도망할 수 없습니다.");
            else
                Debug.Log("[UIBattleManager] 도망 처리 중 — 추가 입력 무시.");
            return false;
        }

        isProcessingBattleExit = true;
        BattleEncounterContext.MarkFleeExit();
        return true;
    }

    public void CompleteFleeExit()
    {
        ExitBattle();

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
        else
            Debug.LogError("[UIBattleManager] GameManager를 찾을 수 없습니다.");
    }

    private void BeginPurifySession()
    {
        IsPurifying = true;
        SetEscapeLocked(true);
    }

    private void EndPurifyAttempt(bool unlockEscape)
    {
        IsPurifying = false;
        if (unlockEscape)
            SetEscapeLocked(false);
    }

    private void SetEscapeLocked(bool locked)
    {
        if (IsEscapeLocked == locked)
            return;

        IsEscapeLocked = locked;
        OnEscapeLockChanged?.Invoke(locked);
    }

    /// <summary>배틀 진입 시 도망 UI를 기본(해제) 상태로 동기화합니다. 이전 전투의 잠금이 남지 않도록 항상 알립니다.</summary>
    private void NotifyEscapeUnlockedForNewBattle()
    {
        IsEscapeLocked = false;
        OnEscapeLockChanged?.Invoke(false);
    }

    private static string ResolveItemDisplayName(string itemId)
    {
        if (DataManager.Instance == null || string.IsNullOrEmpty(itemId))
            return itemId;

        ItemData data = DataManager.Instance.GetItemData(itemId);
        if (data == null || string.IsNullOrEmpty(data.name))
            return itemId;

        return data.name;
    }

    public void ResetBattleUIState()
    {
        if (scanInfoPanel != null)
            scanInfoPanel.SetActive(false);

        if (infectionTypeText != null) infectionTypeText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (inventoryStatusText != null) inventoryStatusText.text = string.Empty;

        ResetContaminationGaugeToInitial();
    }

    public MonsterData GetCurrentMonsterData() => currentMonsterData;

    public void SetMonsterById(string monsterId)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[UIBattleManager] DataManager가 없어 몬스터 데이터를 불러올 수 없습니다.");
            return;
        }

        MonsterData data = DataManager.Instance.GetMonsterData(monsterId);
        if (data == null)
        {
            Debug.LogWarning($"[UIBattleManager] 몬스터 ID '{monsterId}' 데이터를 찾을 수 없습니다.");
            return;
        }

        ApplyMonsterData(data);
    }

    private void LoadMonsterFromData()
    {
        if (DataManager.Instance == null)
            return;

        string monsterId = ResolveBattleMonsterId();
        if (string.IsNullOrEmpty(monsterId))
        {
            Debug.LogWarning("[UIBattleManager] 유효한 몬스터 ID를 찾지 못했습니다. JSON/충돌 매핑을 확인하세요.");
            ClearCurrentMonsterUI();
            return;
        }

        SetMonsterById(monsterId);
    }

    private void ClearCurrentMonsterUI()
    {
        currentMonsterData = null;
        currentMonsterId = null;

        if (monsterNameText != null)
            monsterNameText.text = "Unknown";

        if (difficultyText != null)
            difficultyText.text = "Unknown";

        if (monsterImage != null)
            monsterImage.sprite = null;

        if (contaminationSlider != null)
        {
            contaminationSlider.maxValue = DefaultContaminationLevel;
            contaminationSlider.value = DefaultContaminationLevel;
        }
    }

    private string ResolveBattleMonsterId()
    {
        string encounterId = BattleEncounterContext.ConsumeEncounteredMonsterId();
        bool isEncounterValid = IsValidMonsterId(encounterId);
        if (isEncounterValid)
        {
            lastResolvedEncounterMonsterId = encounterId;
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounterId='{encounterId}' (valid)");
            return encounterId;
        }

        string sceneResolvedId = TryResolveEncounteredMonsterIdFromScene();
        bool isSceneResolvedValid = IsValidMonsterId(sceneResolvedId);
        if (isSceneResolvedValid)
        {
            lastResolvedEncounterMonsterId = sceneResolvedId;
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounterId='{encounterId ?? "null"}' invalid, sceneResolvedId='{sceneResolvedId}' (valid)");
            return sceneResolvedId;
        }

        bool isCachedValid = IsValidMonsterId(lastResolvedEncounterMonsterId);
        if (isCachedValid)
        {
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounter/scene invalid, cachedId='{lastResolvedEncounterMonsterId}' (valid)");
            return lastResolvedEncounterMonsterId;
        }

        Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId 실패. encounterId='{encounterId ?? "null"}', sceneResolvedId='{sceneResolvedId ?? "null"}', cachedId='{lastResolvedEncounterMonsterId ?? "null"}'");

        return null;
    }

    private bool IsValidMonsterId(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId) || DataManager.Instance == null)
            return false;

        return DataManager.Instance.GetMonsterData(monsterId) != null;
    }

    private string GetFirstMonsterIdFromJson()
    {
        if (DataManager.Instance == null)
            return null;

        List<string> ids = DataManager.Instance.GetMonsterIds();
        if (ids == null || ids.Count == 0)
            return null;

        ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            if (IsValidMonsterId(ids[i]))
                return ids[i];
        }

        return null;
    }

    private string TryResolveEncounteredMonsterIdFromScene()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            const int maxOverlapCount = 32;
            Collider2D[] overlapHits = new Collider2D[maxOverlapCount];
            int hitCount = playerCollider.Overlap(ContactFilter2D.noFilter, overlapHits);
            string overlapResolvedId = ResolveClosestMonsterId(player.transform.position, overlapHits, hitCount);
            if (!string.IsNullOrEmpty(overlapResolvedId))
                return overlapResolvedId;
        }

        float radius = 0.6f;
        if (playerCollider != null)
            radius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) + 0.3f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        return ResolveClosestMonsterId(player.transform.position, hits, hits.Length);
    }

    private string ResolveClosestMonsterId(Vector2 playerPosition, Collider2D[] hits, int hitCount)
    {
        if (hits == null || hitCount <= 0)
            return null;

        string closestResolvedId = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !IsMonsterLikeCollider(hit))
                continue;

            string resolvedId = TryResolveMonsterIdFromObjectName(hit.gameObject.name);
            if (string.IsNullOrEmpty(resolvedId))
                continue;

            float distanceSqr = ((Vector2)hit.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestResolvedId = resolvedId;
            }
        }

        return closestResolvedId;
    }

    private bool IsMonsterLikeCollider(Collider2D col)
    {
        try
        {
            if (col.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
        }

        string objectName = col.gameObject.name;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
         return lower.Contains("slime") || lower.Contains("m001") ||
             lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002") ||
             lower.Contains("fire") || lower.Contains("m003") ||
               objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불");
    }

    private string TryResolveMonsterIdFromObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || DataManager.Instance == null)
            return null;

        string lowerObjectName = objectName.ToLowerInvariant();
        List<string> ids = DataManager.Instance.GetMonsterIds();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            MonsterData data = DataManager.Instance.GetMonsterData(id);
            if (data == null)
                continue;

            if (!string.IsNullOrEmpty(data.name) && lowerObjectName.Contains(data.name.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.image_key) && lowerObjectName.Contains(data.image_key.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.id) && lowerObjectName.Contains(data.id.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.id))
            {
                string normalizedId = data.id.Replace("-", string.Empty).ToLowerInvariant();
                if (lowerObjectName.Contains(normalizedId))
                    return id;
            }
        }

        if (lowerObjectName.Contains("slime") || lowerObjectName.Contains("m001") || objectName.Contains("슬라임"))
            return "M-001";

        if (lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") || objectName.Contains("곰팡"))
            return "M-002";

        if (lowerObjectName.Contains("fire") || lowerObjectName.Contains("m003") || objectName.Contains("불"))
            return "M-003";

        return null;
    }

    private void ApplyMonsterData(MonsterData data)
    {
        currentMonsterData = data;
        currentMonsterId = data != null ? data.id : null;

        string difficulty = string.IsNullOrEmpty(data.capture_difficulty) ? "Unknown" : data.capture_difficulty;
        int maxContamination = GetMonsterMaxContamination(data);
        int currentContamination = ResolveInitialContamination(data);
        contaminationAtBattleEntry = currentContamination;
        BattleEncounterContext.ClearFleeExit();
        SetMonsterBasicUI(data.name, difficulty, maxContamination, currentContamination);

        if (monsterImage != null)
            monsterImage.sprite = GetMonsterSprite(data);
    }

    private Sprite GetMonsterSprite(MonsterData data)
    {
        if (AtlasManager.Instance == null || data == null)
            return null;

        if (!string.IsNullOrEmpty(data.image_key))
        {
            Sprite sprite = GetBestMonsterAtlasSprite(data.image_key);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.id))
        {
            Sprite sprite = AtlasManager.Instance.GetMonsterSprite(data.id);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.name))
            return AtlasManager.Instance.GetMonsterSprite(data.name);

        return null;
    }

    private Sprite GetBestMonsterAtlasSprite(string baseKey)
    {
        Sprite direct = AtlasManager.Instance.GetMonsterSprite(baseKey);
        if (direct == null)
            return null;

        // Large sprites are usually already the intended representative image.
        // Skip extra probing to avoid warning spam from missing *_N keys.
        if (GetSpriteArea(direct) >= 4096f)
            return direct;

        Sprite best = direct;

        // For very small fallback slices (e.g. fire _0), probe sequential variants conservatively.
        for (int i = 1; i <= 8; i++)
        {
            Sprite candidate = AtlasManager.Instance.GetMonsterSprite($"{baseKey}_{i}");
            if (candidate == null)
                break;

            if (best == null || GetSpriteArea(candidate) > GetSpriteArea(best))
                best = candidate;
        }

        return best;
    }

    private static float GetSpriteArea(Sprite sprite)
    {
        Rect rect = sprite.rect;
        return rect.width * rect.height;
    }

    public void SetMonsterBasicUI(string name, string difficulty, int maxContamination)
    {
        SetMonsterBasicUI(name, difficulty, maxContamination, maxContamination);
    }

    private void SetMonsterBasicUI(string name, string difficulty, int maxContamination, int currentContamination)
    {
        monsterNameText.text = name;
        difficultyText.text = difficulty;
        GetComponent<EnemyStatus>()?.ConfigureStatusText(difficultyText, difficulty);
        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = Mathf.Clamp(currentContamination, 0, maxContamination);
    }

    // [탐색] 버튼을 눌렀을 때 실행될 함수
    public void RevealScannedInfo(string infectionType, string description, string inventoryStatus)
    {
        if (scanInfoPanel != null)
        {
            scanInfoPanel.SetActive(true);
        }

        infectionTypeText.text = infectionType;
        descriptionText.text = description;
        inventoryStatusText.text = inventoryStatus;
    }

    public int GetCurrentContamination()
    {
        if (contaminationSlider == null)
            return 0;

        return Mathf.RoundToInt(contaminationSlider.value);
    }

    public int GetMaxContamination()
    {
        if (contaminationSlider == null)
            return DefaultContaminationLevel;

        return Mathf.RoundToInt(contaminationSlider.maxValue);
    }

    public void UpdateContaminationGauge(int currentContamination)
    {
        contaminationSlider.value = currentContamination;
    }

    public void ReduceContamination(int amount)
    {
        if (contaminationSlider == null) return;

        contaminationSlider.value = Mathf.Max(0, contaminationSlider.value - amount);
        CacheCurrentMonsterContamination((int)contaminationSlider.value);
        Debug.Log($"[UIBattleManager] 오염도 감소: {contaminationSlider.value}");

        if (contaminationSlider.value <= 0)
        {
            ClearCurrentMonsterContamination();
            Debug.Log("[UIBattleManager] 오염도 0 도달! 정화 완료.");
            ResolveTurnController();
            turnController?.NotifyBattleWon();
            OnContaminationEmpty?.Invoke();
        }
    }

    private void ResolveTurnController()
    {
        if (turnController != null)
            return;

        turnController = GetComponent<BattleTurnController>();
        if (turnController == null)
            turnController = FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);
    }

    private void EnsureTurnController()
    {
        ResolveTurnController();
        if (turnController != null)
            return;

        turnController = gameObject.AddComponent<BattleTurnController>();
    }

    private void EnsureEnemyStatus()
    {
        EnemyStatus status = GetComponent<EnemyStatus>();
        if (status == null)
            status = gameObject.AddComponent<EnemyStatus>();

        status.ConfigureStatusText(difficultyText, GetDifficultyDisplayText());
    }

    private void ResolveAndBindPlayerOxygen()
    {
        PlayerOxygen runtimeOxygen = PlayerOxygen.ResolveRuntime();
        turnController?.BindPlayerOxygen(runtimeOxygen);

        if (battleOxygenGauge == null)
            battleOxygenGauge = GetComponentInChildren<UIBattleOxygenGauge>(true);

        UIBattleOxygenGauge[] gauges =
            GetComponentsInChildren<UIBattleOxygenGauge>(true);

        for (int i = 0; i < gauges.Length; i++)
        {
            if (gauges[i] != null)
                gauges[i].BindToRuntimePlayerOxygen();
        }
    }

    public string GetDifficultyDisplayText()
    {
        if (difficultyText == null)
            return string.Empty;

        return difficultyText.text;
    }

    private void SubscribeBattleEnded()
    {
        if (isSubscribedToBattleEnded || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        isSubscribedToBattleEnded = true;
    }

    private void UnsubscribeBattleEnded()
    {
        if (!isSubscribedToBattleEnded || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        isSubscribedToBattleEnded = false;
    }

    private void HandleBattleEnded()
    {
        ResolveTurnController();
        turnController?.NotifyBattleEnded();

        ExitBattle();
        FinalizeContaminationOnce();
        ForceRestoreFieldPhysics();

        lastResolvedEncounterMonsterId = null;
        BattleEncounterContext.SetEncounteredMonsterId(null);
    }

    private void FinalizeContaminationOnce()
    {
        if (hasFinalizedContaminationForSession)
            return;

        hasFinalizedContaminationForSession = true;
        FinalizeContaminationOnBattleClose();
    }

    /// <summary>배틀 UI 비활성·게임오버 등에서 플레이어/몬스터 물리 잠금을 해제합니다.</summary>
    public void ForceRestoreFieldPhysics()
    {
        UnlockPlayerMovement();
        UnlockMonsterMovement();
        RestoreAllMonsterRigidbodiesInScene();
    }

    /// <summary>씬에 존재하는 모든 UIBattleManager의 배틀 상태·물리를 초기화합니다.</summary>
    public static void ResetAllRuntimeBattleState()
    {
        UIBattleManager[] managers =
            FindObjectsByType<UIBattleManager>(FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager manager = managers[i];
            if (manager == null)
                continue;

            manager.ExitBattle();
            manager.ForceRestoreFieldPhysics();
        }

        RestoreAllMonsterRigidbodiesInScene();
    }

    private static void RestoreAllMonsterRigidbodiesInScene()
    {
        Rigidbody2D[] rigidbodies = FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];
            if (body == null)
                continue;

            if (!body.simulated)
                body.simulated = true;
        }
    }

    private void ResetContaminationGaugeToInitial()
    {
        if (contaminationSlider == null)
            return;

        int maxContamination = GetMonsterMaxContamination(currentMonsterData);
        int initialContamination = ResolveInitialContamination(currentMonsterData);

        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = Mathf.Clamp(initialContamination, 0, maxContamination);
    }

    private int GetMonsterMaxContamination(MonsterData data)
    {
        if (data == null)
            return DefaultContaminationLevel;

        return data.contamination_level > 0 ? data.contamination_level : DefaultContaminationLevel;
    }

    private int ResolveInitialContamination(MonsterData data)
    {
        if (data == null)
            return DefaultContaminationLevel;

        int maxContamination = GetMonsterMaxContamination(data);
        if (!string.IsNullOrEmpty(data.id) && contaminationProgressByMonsterId.TryGetValue(data.id, out int savedContamination))
            return Mathf.Clamp(savedContamination, 0, maxContamination);

        return maxContamination;
    }

    private void CacheCurrentMonsterContamination(int contamination)
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        contaminationProgressByMonsterId[currentMonsterId] = Mathf.Max(0, contamination);
    }

    private void ClearCurrentMonsterContamination()
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        contaminationProgressByMonsterId.Remove(currentMonsterId);
    }

    private void FinalizeContaminationOnBattleClose()
    {
        if (BattleEncounterContext.IsFleeExitPending)
        {
            RevertContaminationProgressAfterFlee();
            BattleEncounterContext.ClearFleeExit();
            return;
        }

        SaveCurrentContaminationProgress();
    }

    private void RevertContaminationProgressAfterFlee()
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        int restored = Mathf.Max(0, contaminationAtBattleEntry);
        contaminationProgressByMonsterId[currentMonsterId] = restored;

        if (contaminationSlider != null)
            contaminationSlider.value = Mathf.Clamp(restored, 0, contaminationSlider.maxValue);

        Debug.Log($"[UIBattleManager] 도망 → 오염도 진행도 복구: {currentMonsterId} = {restored}");
    }

    private void SaveCurrentContaminationProgress()
    {
        if (contaminationSlider == null || string.IsNullOrEmpty(currentMonsterId))
            return;

        int current = Mathf.RoundToInt(contaminationSlider.value);
        if (current <= 0)
        {
            contaminationProgressByMonsterId.Remove(currentMonsterId);
            return;
        }

        contaminationProgressByMonsterId[currentMonsterId] = current;
    }

    private void LockPlayerMovementAtBattleEntry()
    {
        if (lockedPlayerController != null)
            return;

        lockedPlayerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (lockedPlayerController == null)
            return;

        lockedPlayerRigidbody = lockedPlayerController.GetComponent<Rigidbody2D>();
        if (lockedPlayerRigidbody != null)
        {
            wasPlayerRigidbodySimulated = lockedPlayerRigidbody.simulated;
            playerConstraints = lockedPlayerRigidbody.constraints;
            lockedPlayerRigidbody.linearVelocity = Vector2.zero;
            lockedPlayerRigidbody.angularVelocity = 0f;
            lockedPlayerRigidbody.simulated = false;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (lockedPlayerRigidbody != null)
        {
            lockedPlayerRigidbody.constraints = playerConstraints;
            lockedPlayerRigidbody.simulated = true;
            lockedPlayerRigidbody.linearVelocity = Vector2.zero;
            lockedPlayerRigidbody.angularVelocity = 0f;
        }

        lockedPlayerController = null;
        lockedPlayerRigidbody = null;
        wasPlayerRigidbodySimulated = false;
        playerConstraints = RigidbodyConstraints2D.None;
    }

    private void LockMonsterMovementAtBattleEntry()
    {
        if (lockedMonsters.Count > 0)
            return;

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || !IsMonsterLikeCollider(col))
                continue;

            Rigidbody2D rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponent<Rigidbody2D>();
            if (rb == null || HasLockedSnapshot(rb))
                continue;

            lockedMonsters.Add(new MonsterPhysicsSnapshot
            {
                rigidbody = rb,
                wasSimulated = rb.simulated,
                constraints = rb.constraints
            });

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }

    private bool HasLockedSnapshot(Rigidbody2D rb)
    {
        for (int i = 0; i < lockedMonsters.Count; i++)
        {
            if (lockedMonsters[i].rigidbody == rb)
                return true;
        }

        return false;
    }

    private void UnlockMonsterMovement()
    {
        for (int i = 0; i < lockedMonsters.Count; i++)
        {
            MonsterPhysicsSnapshot snapshot = lockedMonsters[i];
            if (snapshot == null || snapshot.rigidbody == null)
                continue;

            snapshot.rigidbody.constraints = snapshot.constraints;
            snapshot.rigidbody.simulated = true;
            snapshot.rigidbody.linearVelocity = Vector2.zero;
            snapshot.rigidbody.angularVelocity = 0f;
        }

        lockedMonsters.Clear();
    }
}