using System.Collections.Generic;

// ID를 가진 모든 데이터가 구현해야 할 인터페이스
public interface IIdentifiable { string GetId(); }
public interface IDataList<T> { List<T> GetList(); }

[System.Serializable]
public class ItemData : IIdentifiable
{
    public string id;
    public string name;
    public int durability;
    public string description;
    public string GetId() => id;
}

[System.Serializable]
public class DropItem
{
    public string item_id;
    public int count;
}

[System.Serializable]
public class MonsterData : IIdentifiable
{
    public string id;
    public string name;
    public int contamination_level;
    public string capture_difficulty;
    public int speed;
    public string infection_type;
    public string description;
    public List<DropItem> drop_items;
    public string GetId() => id;
}

// 리스트를 감싸는 래퍼 클래스들 (JSON 루트 키와 필드명 일치시켜야 함)
[System.Serializable]
public class FactoryItemDataList : IDataList<ItemData>
{
    public List<ItemData> factory_items;
    public List<ItemData> GetList() => factory_items;
}

[System.Serializable]
public class MonsterItemDataList : IDataList<ItemData>
{
    public List<ItemData> monster_items;
    public List<ItemData> GetList() => monster_items;
}

[System.Serializable]
public class MonsterDataList : IDataList<MonsterData>
{
    public List<MonsterData> monsters;
    public List<MonsterData> GetList() => monsters;
}
