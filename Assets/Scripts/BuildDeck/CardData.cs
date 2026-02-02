using UnityEngine;

[System.Serializable]
public class CardData
{
    public int id;
    public string name;
    public string type;
    public int maxDeckCount;
    public string skinPath;

    public CardData(int _id, string _name, string _type, int _maxCount, string _skin)
    {
        id = _id;
        name = _name;
        type = _type;
        maxDeckCount = _maxCount;
        skinPath = _skin;
    }
}
