using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CardData
{
    public string id;
    public string characterId;
    public string name;
    public string type;
    public int speed;
    public int cost;
    public int max_deck_count;
    public string imagePath;
    //public string description;

    [System.Serializable]
    public class CardDataWrapper
    {
        public List<CardData> Card;
    }
}
