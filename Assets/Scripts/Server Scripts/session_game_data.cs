using UnityEngine;
using System.Collections.Generic;

public static class GameData
{
    public static string SessionCode;
    public static string MyID;
    public static string MyRole;
    public static List<int> MyDeck = new List<int>();
}

public enum ActionType
{
    A,
    B
}