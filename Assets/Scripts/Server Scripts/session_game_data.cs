using UnityEngine;
using System.Collections.Generic;

public static class GameData
{
    public static string SessionCode;
    public static string MyID;
    public static string MyRole;
    public static List<string> MyDeck = new List<string>();

    /// <summary>
    /// 내가 고른 용병 characterId 목록 ("ELLIE" 등). 최대 2개.
    /// 덱 카드에서 역산하면 "용병 2명을 골랐지만 한쪽 카드만 넣은 덱"의 의도가 사라진다.
    /// 구버전 덱은 비어 있을 수 있으므로 읽는 쪽이 역산으로 넘어가야 한다.
    /// </summary>
    public static List<string> MyCharacters = new List<string>();
}

public enum ActionType
{
    A,
    B
}