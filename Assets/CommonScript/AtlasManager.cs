using UnityEngine;
using UnityEngine.U2D;

public class AtlasManager : MonoBehaviour
{
    public static AtlasManager Instance { get; private set; }

    [Header("--- Sprite Atlas References ---")]
    [SerializeField] private SpriteAtlas itemAtlas;
    [SerializeField] private SpriteAtlas monsterAtlas;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (itemAtlas == null)
                Debug.LogError("[AtlasManager] ItemAtlas가 인스펙터에 할당되지 않았습니다.");

            if (monsterAtlas == null)
                Debug.LogError("[AtlasManager] MonsterAtlas가 인스펙터에 할당되지 않았습니다.");
        }
        else
        {
            Destroy(gameObject);
        }
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
        return GetSpriteFromAtlas(monsterAtlas, spriteName, "MonsterAtlas");
    }

    private Sprite GetSpriteFromAtlas(SpriteAtlas atlas, string spriteName, string atlasName)
    {
        if (atlas == null || string.IsNullOrEmpty(spriteName))
            return null;

        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
            sprite = atlas.GetSprite($"{spriteName}_0");

        if (sprite == null)
            Debug.LogWarning($"[AtlasManager] {atlasName}에서 스프라이트를 찾을 수 없습니다: {spriteName}");

        return sprite;
    }
}
