using UnityEngine;
using System.Collections.Generic;

public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("--- JSON 파일 ---")]
    [SerializeField] private TextAsset factoryItemsJson;
    [SerializeField] private TextAsset monsterItemsJson;
    [SerializeField] private TextAsset monstersJson;

    // 1. 데이터 보관함 (딕셔너리)
    private Dictionary<string, ItemData> FactoryItemDict = new Dictionary<string, ItemData>();
    private Dictionary<string, ItemData> MonsterItemDict = new Dictionary<string, ItemData>();
    private Dictionary<string, MonsterData> MonsterDict = new Dictionary<string, MonsterData>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadAllData();
    }

    private void LoadAllData()
    {
        if (factoryItemsJson != null)  LoadDataToDict<ItemData, FactoryItemDataList>(factoryItemsJson, FactoryItemDict);
        if (monsterItemsJson != null)  LoadDataToDict<ItemData, MonsterItemDataList>(monsterItemsJson, MonsterItemDict);
        if (monstersJson != null)      LoadDataToDict<MonsterData, MonsterDataList>(monstersJson, MonsterDict);

        Debug.Log("DataManager: 모든 데이터 로드 완료!");
    }

    // 아이템 ID 존재 여부 확인 (factory + monster 통합 검색)
    public bool HasItem(string id) => FactoryItemDict.ContainsKey(id) || MonsterItemDict.ContainsKey(id);

    // 아이템 데이터 반환
    public ItemData GetItemData(string id)
    {
        if (FactoryItemDict.TryGetValue(id, out ItemData item)) return item;
        if (MonsterItemDict.TryGetValue(id, out item)) return item;
        return null;
    }

    // 몬스터 데이터 반환
    public MonsterData GetMonsterData(string id)
    {
        MonsterDict.TryGetValue(id, out MonsterData monster);
        return monster;
    }

    // 제네릭을 사용하면 똑같은 코드를 반복하지 않아도 됩니다!
    private void LoadDataToDict<T, TList>(TextAsset textAsset, Dictionary<string, T> dict) where TList : class, IDataList<T>
    {
        TList dataList = JsonUtility.FromJson<TList>(textAsset.text);

        if (dataList == null || dataList.GetList() == null)
        {
            Debug.LogError($"[DataManager] {textAsset.name} 파일 파싱 실패! JSON 구조를 확인하세요.");
            return;
        }

        foreach (var data in dataList.GetList())
        {
            dict[((IIdentifiable)data).GetId()] = data;
        }
    }
}