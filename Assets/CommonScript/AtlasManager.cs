using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class AtlasManager : MonoBehaviour
{
    public static AtlasManager Instance { get; private set; }

    private const int MaxAtlasFrameProbe = 128;

    [Header("--- Sprite Atlas References ---")]
    [SerializeField] private SpriteAtlas itemAtlas;
    [SerializeField] private SpriteAtlas MonsterImages;

    private readonly HashSet<string> preloadedMonsterImageKeys = new HashSet<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (itemAtlas == null)
                Debug.LogError("[AtlasManager] ItemAtlas가 인스펙터에 할당되지 않았습니다.");

            if (MonsterImages == null)
                Debug.LogError("[AtlasManager] MonsterImages가 인스펙터에 할당되지 않았습니다.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(PreloadMonsterAtlasesRoutine());
    }

    private IEnumerator PreloadMonsterAtlasesRoutine()
    {
        yield return null;

        PreloadEntireMonsterAtlas();
        PreloadAllMonsterBattleSprites();

        Debug.Log("[AtlasManager] MonsterImages 아틀라스 선로딩 완료");
    }

    public void PreloadMonsterBattleSpritesForId(string monsterId)
    {
        if (DataManager.Instance == null || string.IsNullOrEmpty(monsterId))
            return;

        MonsterData data = DataManager.Instance.GetMonsterData(monsterId);
        if (data == null || string.IsNullOrEmpty(data.image_key))
            return;

        PreloadMonsterBattleSprites(data.image_key);
    }

    public void PreloadMonsterBattleSpritesForMonsterObject(GameObject monsterObject)
    {
        if (monsterObject == null)
            return;

        PreloadMonsterBattleSpritesForId(ResolveMonsterIdFromObjectName(monsterObject.name));
    }

    public void PreloadMonsterBattleSprites(string imageKey)
    {
        if (MonsterImages == null || string.IsNullOrEmpty(imageKey))
            return;

        if (!preloadedMonsterImageKeys.Add(imageKey))
            return;

        TouchMonsterSpriteExact($"{imageKey}_idle", $"{imageKey}_Idle");
        TouchMonsterSpriteExact($"{imageKey}_hit", $"{imageKey}_Hit");

        for (int i = 0; i < MaxAtlasFrameProbe; i++)
        {
            TouchMonsterSpriteExact(
                $"{imageKey}_idle_{i}",
                $"{imageKey}_Idle_{i}",
                $"{imageKey}_hit_{i}",
                $"{imageKey}_Hit_{i}",
                $"{imageKey}_{i}");
        }

        TouchMonsterSpriteExact(imageKey);
    }

    private void PreloadAllMonsterBattleSprites()
    {
        if (DataManager.Instance == null)
            return;

        List<string> monsterIds = DataManager.Instance.GetMonsterIds();
        for (int i = 0; i < monsterIds.Count; i++)
            PreloadMonsterBattleSpritesForId(monsterIds[i]);
    }

    private void PreloadEntireMonsterAtlas()
    {
        if (MonsterImages == null)
            return;

        int spriteCount = MonsterImages.spriteCount;
        if (spriteCount <= 0)
            return;

        var sprites = new Sprite[spriteCount];
        MonsterImages.GetSprites(sprites);
    }

    private static string ResolveMonsterIdFromObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        string lower = objectName.ToLowerInvariant();

        if (objectName.Contains("슬라임") || lower.Contains("slime") || lower.Contains("m001"))
            return "M-001";

        if (objectName.Contains("곰팡") || lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002"))
            return "M-002";

        if (objectName.Contains("불") || lower.Contains("fire") || lower.Contains("m003"))
            return "M-003";

        return null;
    }

    private void TouchMonsterSpriteExact(params string[] spriteNames)
    {
        if (spriteNames == null)
            return;

        for (int i = 0; i < spriteNames.Length; i++)
        {
            if (string.IsNullOrEmpty(spriteNames[i]))
                continue;

            WarmupSprite(MonsterImages.GetSprite(spriteNames[i]));
        }
    }

    private static void WarmupSprite(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;
    }

    /// <summary>
    /// ItemAtlas에서 스프라이트를 반환합니다.
    /// DataManager 등에서 전달받은 iconName과 ItemAtlas 내부 스프라이트 이름이 정확히 일치해야 합니다.
    /// </summary>
    public Sprite GetSprite(string spriteName)
    {
        return GetSpriteFromAtlas(itemAtlas, spriteName, "ItemAtlas");
    }

    /// <summary>
    /// MonsterAtlas에서 스프라이트를 반환합니다.
    /// DataManager 등에서 전달받은 spriteName과 MonsterAtlas 내부 스프라이트 이름이 정확히 일치해야 합니다.
    /// </summary>
    public Sprite GetMonsterSprite(string spriteName)
    {
        return GetMonsterSprite(spriteName, true);
    }

    public Sprite GetMonsterSprite(string spriteName, bool logIfMissing)
    {
        return GetSpriteFromAtlas(MonsterImages, spriteName, "MonsterImages", logIfMissing);
    }

    /// <summary>
    /// MonsterAtlas에서 이름이 정확히 일치하는 스프라이트만 반환합니다. (_0 폴백 없음)
    /// </summary>
    public Sprite TryGetMonsterSpriteExact(string spriteName)
    {
        if (MonsterImages == null || string.IsNullOrEmpty(spriteName))
            return null;

        return MonsterImages.GetSprite(spriteName);
    }

    private Sprite GetSpriteFromAtlas(SpriteAtlas atlas, string spriteName, string atlasName)
    {
        return GetSpriteFromAtlas(atlas, spriteName, atlasName, true);
    }

    private Sprite GetSpriteFromAtlas(
        SpriteAtlas atlas,
        string spriteName,
        string atlasName,
        bool logIfMissing)
    {
        if (atlas == null || string.IsNullOrEmpty(spriteName))
            return null;

        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
            sprite = atlas.GetSprite($"{spriteName}_0");

        if (sprite == null && logIfMissing)
            Debug.LogWarning($"[AtlasManager] {atlasName}에서 스프라이트를 찾을 수 없습니다: {spriteName}");

        return sprite;
    }
}
