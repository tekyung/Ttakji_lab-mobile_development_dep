// DeckLoader.cs — 저장된 덱 이름 하나로 "대전에 필요한 것"을 꺼내 주는 창구.
//
// 덱 파일을 읽어 쓰는 곳이 이미 셋이다(온라인 업로드 · 손패 레거시 테스트 · 이 클래스).
// 그때마다 같은 함정을 두 번씩 밟았기에 여기 한 곳으로 모은다:
//
//   ① 목록이 "중복 포함 20장"인지 "유니크 10종"인지 구분해야 한다.
//      구분하지 않고 2장씩 전개하면 20장짜리가 40장이 된다.
//   ② 용병 목록이 비어 있는 구버전 덱이 있다. 그때는 카드들로 역산해야 한다.
//      (덱 빌더가 characterIdList를 남기기 시작한 것은 나중 일이다)
//
// 읽기만 한다. 저장은 DeckBuilderManager가 맡는다.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DeckLoader
{
    /// <summary>
    /// 덱 이름으로 카드 목록과 용병 목록을 읽는다.
    /// </summary>
    /// <param name="deckName">확장자 없는 덱 이름.</param>
    /// <param name="cardIds">엔진에 그대로 넘길 <b>중복 포함</b> 카드 ID 목록.</param>
    /// <param name="characterIds">용병 characterId("ELLIE" 등). 최대 2개.</param>
    /// <returns>파일을 읽어 카드가 한 장이라도 나왔으면 true.</returns>
    public static bool TryLoad(string deckName, out List<string> cardIds, out List<string> characterIds)
    {
        cardIds = new List<string>();
        characterIds = new List<string>();

        if (string.IsNullOrEmpty(deckName)) return false;

        string path = DeckStorage.GetDeckPath(deckName);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[DeckLoader] 덱 파일을 찾지 못했습니다: {deckName}");
            return false;
        }

        DeckSaveData saved;
        try
        {
            saved = JsonUtility.FromJson<DeckSaveData>(File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DeckLoader] '{deckName}' 을 읽지 못했습니다 — {e.Message}");
            return false;
        }

        if (saved?.cardIdList == null || saved.cardIdList.Count == 0)
        {
            Debug.LogWarning($"[DeckLoader] '{deckName}' 에 카드가 없습니다.");
            return false;
        }

        cardIds = ExpandIfUniqueList(saved.cardIdList);
        characterIds = ResolveCharacters(saved.characterIdList, cardIds);
        return true;
    }

    /// <summary>저장된 덱 이름 목록. 없으면 빈 배열.</summary>
    public static List<string> ListDeckNames()
    {
        var names = new List<string>();

        foreach (string path in DeckStorage.GetDeckFiles())
            names.Add(Path.GetFileNameWithoutExtension(path));

        return names;
    }

    /// <summary>
    /// 유니크 목록이면 2장씩 전개하고, 이미 중복이 섞여 있으면 그대로 쓴다.
    ///
    /// ★ 판단 기준은 "중복이 하나도 없는가"다. 덱 빌더가 저장하는 목록은 같은 카드가
    ///   두 번 들어 있으므로 그대로 통과하고, 콘솔·구버전 방식의 유니크 10종만 전개된다.
    ///   (같은 규칙이 session_game_manage.BuildMainDeck에도 있다)
    /// </summary>
    private static List<string> ExpandIfUniqueList(List<string> ids)
    {
        var unique = new HashSet<string>(ids);
        if (unique.Count != ids.Count) return new List<string>(ids);

        var expanded = new List<string>(ids.Count * 2);
        foreach (string id in ids)
        {
            expanded.Add(id);
            expanded.Add(id);
        }

        return expanded;
    }

    /// <summary>
    /// 용병 목록을 정한다. 덱이 명시한 것이 있으면 그것을 쓰고,
    /// 비어 있으면(구버전 덱) 카드들의 테마로 역산한다.
    /// </summary>
    private static List<string> ResolveCharacters(List<string> saved, List<string> cardIds)
    {
        var result = new List<string>();

        if (saved != null)
        {
            foreach (string id in saved)
            {
                if (string.IsNullOrEmpty(id) || result.Contains(id)) continue;
                result.Add(id);
                if (result.Count == 2) return result;
            }
        }

        if (result.Count == 2) return result;

        // ── 역산: 카드 ID 접두사("ELLI-05" → "ELLI") → 용병 characterId("ELLIE")
        if (CardDataManager.Instance == null) return result;

        foreach (string cardId in cardIds)
        {
            if (string.IsNullOrEmpty(cardId)) continue;

            string prefix = cardId.Contains("-") ? cardId.Split('-')[0] : cardId;

            foreach (CharacterData character in CardDataManager.Instance.allCharacterList)
            {
                if (character == null || string.IsNullOrEmpty(character.id)) continue;
                if (!character.id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) continue;

                if (!result.Contains(character.characterId)) result.Add(character.characterId);
                break;
            }

            if (result.Count == 2) break;
        }

        return result;
    }
}
