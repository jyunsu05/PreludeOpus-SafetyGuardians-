using UnityEngine;
using UnityEngine.U2D;

public class AtlasManager : MonoBehaviour
{
    public static AtlasManager Instance { get; private set; }

    private const string ItemAtlasPath = "Atlases/ItemAtlas";
    private const string MonsterAtlasPath = "Atlases/MonsterAtlas";

    private SpriteAtlas itemAtlas;
    private SpriteAtlas monsterAtlas;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            itemAtlas = Resources.Load<SpriteAtlas>(ItemAtlasPath);
            monsterAtlas = Resources.Load<SpriteAtlas>(MonsterAtlasPath);

            if (itemAtlas == null)
                Debug.LogError($"[AtlasManager] ItemAtlas를 찾을 수 없습니다: {ItemAtlasPath}");

            if (monsterAtlas == null)
                Debug.LogError($"[AtlasManager] MonsterAtlas를 찾을 수 없습니다: {MonsterAtlasPath}");
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
            Debug.LogWarning($"[AtlasManager] {atlasName}에서 스프라이트를 찾을 수 없습니다: {spriteName}");

        return sprite;
    }
}
