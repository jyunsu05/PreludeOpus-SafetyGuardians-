using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIBattleManager : MonoBehaviour
{
    public event System.Action OnContaminationEmpty;
    [Header("--- 몬스터 기본 정보 UI (항상 보임) ---")]
    [SerializeField] private Image monsterImage;
    [SerializeField] private TextMeshProUGUI monsterNameText;       // 몬스터: name
    [SerializeField] private TextMeshProUGUI difficultyText;        // 포획 난이도: New Text
    [SerializeField] private Slider contaminationSlider;            // 오염도 게이지 바
    [SerializeField] private string defaultMonsterId = "M-001";

    [Header("--- 탐색 시 통째로 열리는 부모 Panel ---")]
    [SerializeField] private GameObject scanInfoPanel;              // 3개를 하나로 묶으신 부모 오브젝트

    [Header("--- 부모 Panel 내부의 텍스트들 ---")]
    [SerializeField] private TextMeshProUGUI infectionTypeText;     // 감염 물질 : 감염물질 이름
    [SerializeField] private TextMeshProUGUI descriptionText;       // 정화 방법 : 정화 방법 설명
    [SerializeField] private TextMeshProUGUI inventoryStatusText;   // 인벤토리 상황 : 아이템 보유

    private MonsterData currentMonsterData;
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
        ResetBattleUIState();
    }

    void OnEnable()
    {
        ResetBattleUIState();
        LoadMonsterFromData();
        LockPlayerMovementAtBattleEntry();
        LockMonsterMovementAtBattleEntry();
    }

    void OnDisable()
    {
        UnlockPlayerMovement();
        UnlockMonsterMovement();
    }

    void OnDestroy()
    {
        UnlockPlayerMovement();
        UnlockMonsterMovement();
    }

    public void ResetBattleUIState()
    {
        if (scanInfoPanel != null)
            scanInfoPanel.SetActive(false);

        if (infectionTypeText != null) infectionTypeText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (inventoryStatusText != null) inventoryStatusText.text = string.Empty;

        if (contaminationSlider != null)
            contaminationSlider.value = contaminationSlider.maxValue;
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

        string monsterId = BattleEncounterContext.ConsumeEncounteredMonsterId();
        if (string.IsNullOrEmpty(monsterId))
            monsterId = TryResolveEncounteredMonsterIdFromScene();

        if (string.IsNullOrEmpty(monsterId))
            monsterId = defaultMonsterId;

        if (DataManager.Instance.GetMonsterData(monsterId) == null)
        {
            List<string> ids = DataManager.Instance.GetMonsterIds();
            if (ids.Count == 0)
                return;

            monsterId = ids[0];
        }

        SetMonsterById(monsterId);
    }

    private string TryResolveEncounteredMonsterIdFromScene()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        float radius = 0.6f;
        if (playerCollider != null)
            radius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) + 0.2f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !IsMonsterLikeCollider(hit))
                continue;

            string resolvedId = TryResolveMonsterIdFromObjectName(hit.gameObject.name);
            if (!string.IsNullOrEmpty(resolvedId))
                return resolvedId;
        }

        return null;
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
        return lower.Contains("slime") || lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002") || lower.Contains("fire") ||
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
        }

        if (lowerObjectName.Contains("slime") || objectName.Contains("슬라임"))
            return "M-001";

        if (lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") || objectName.Contains("곰팡"))
            return "M-002";

        if (lowerObjectName.Contains("fire") || objectName.Contains("불"))
            return "M-003";

        return null;
    }

    private void ApplyMonsterData(MonsterData data)
    {
        currentMonsterData = data;

        string difficulty = string.IsNullOrEmpty(data.capture_difficulty) ? "Unknown" : data.capture_difficulty;
        int contamination = data.contamination_level > 0 ? data.contamination_level : 100;
        SetMonsterBasicUI(data.name, difficulty, contamination);

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
        monsterNameText.text = name;
        difficultyText.text = difficulty;
        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = maxContamination;
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

    public void UpdateContaminationGauge(int currentContamination)
    {
        contaminationSlider.value = currentContamination;
    }

    public void ReduceContamination(int amount)
    {
        if (contaminationSlider == null) return;

        contaminationSlider.value = Mathf.Max(0, contaminationSlider.value - amount);
        Debug.Log($"[UIBattleManager] 오염도 감소: {contaminationSlider.value}");

        if (contaminationSlider.value <= 0)
        {
            Debug.Log("[UIBattleManager] 오염도 0 도달! 정화 완료.");
            OnContaminationEmpty?.Invoke();
        }
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
        if (lockedPlayerController == null)
            return;

        if (lockedPlayerRigidbody != null)
        {
            lockedPlayerRigidbody.constraints = playerConstraints;
            lockedPlayerRigidbody.simulated = wasPlayerRigidbodySimulated;
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
            snapshot.rigidbody.simulated = snapshot.wasSimulated;
            snapshot.rigidbody.linearVelocity = Vector2.zero;
            snapshot.rigidbody.angularVelocity = 0f;
        }

        lockedMonsters.Clear();
    }
}