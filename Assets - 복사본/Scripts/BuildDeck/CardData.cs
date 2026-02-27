using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CardData
{
    public int id;
    public string name;
    public string type;
    public int max_deck_count;
    public string skin_res;
    //public string description;

    [System.Serializable]
    public class CardDataWrapper
    {
        public List<CardData> Card;
    }
}
