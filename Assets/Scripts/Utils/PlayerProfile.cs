// PlayerProfile.cs — 이 기기의 플레이어 이름을 관리하는 단일 창구
//
// ★ 왜 필요한가
//   커스텀 방에서 호스트는 "들어온 사람이 누구인지" 보고 [시작]할지 [퇴장]시킬지 정한다.
//   그런데 지금까지 서버에 올라가던 ID는 session_manage.GetOrGenerateID()의
//   `Random.Range(0, 1000000).ToString()` 이었다 — MainMenu에는 session_ui가 없어서
//   그 폴백을 탄다. 호스트가 보는 이름이 "482913"이면 확인할 것이 없다.
//
// ★ 왜 파일인가 (PlayerPrefs가 아니라)
//   덱과 같은 취급이다. 사람이 정한 값이고, 지워지면 상대에게 다른 사람으로 보인다.
//   저장 위치도 DeckStorage와 같은 Application.persistentDataPath를 쓴다 —
//   Application.dataPath는 안드로이드에서 APK 내부(읽기 전용)를 가리켜 쓰기가 통째로 실패한다.
//   그 사고 기록은 DeckStorage.cs 상단 주석에 있다.
//
// ★ 왜 이름 뒤에 #1234가 붙는가
//   닉네임은 중복될 수 있다. "홍길동" 둘이 같은 방을 오가면 호스트가 구분하지 못한다.
//   꼬리표는 <b>최초 1회만 뽑고 이후 고정</b>한다 — 이름을 고쳐도 유지되므로
//   상대에게는 계속 같은 사람으로 보인다.
using System;
using System.IO;
using UnityEngine;

/// <summary>파일로 저장되는 프로필. JsonUtility가 읽고 쓰므로 필드는 public이어야 한다.</summary>
[Serializable]
public class PlayerProfileData
{
    public string nickname;
    public string tag;

    /// <summary>
    /// 이 설치본의 <b>신원</b>. 사람에게 보이지 않고 바뀌지도 않는다.
    ///
    /// ★ 표시 이름과 반드시 갈라야 한다. 서버의 ExitSession은
    ///   <c>myID == hostId</c> / <c>myID == guestId</c> 로 호스트와 게스트를 가르는데,
    ///   여기에 표시 이름을 쓰면 <b>이름이 겹치는 순간 엉뚱한 사람을 가리킨다</b> —
    ///   게스트가 나가려는데 서버가 호스트로 보고 방을 통째로 지우는 식이다.
    /// </summary>
    public string playerId;
}

public static class PlayerProfile
{
    private const string FileNameBase = "profile";
    private const string FileExtension = ".json";

    /// <summary>이름을 아직 정하지 않았을 때 임시로 쓰는 값.</summary>
    public const string DefaultNickname = "이름없음";

    /// <summary>닉네임 길이 상한. 상대 화면의 이름 칸을 넘지 않을 정도로 둔다.</summary>
    public const int MaxNicknameLength = 12;

    private static PlayerProfileData _cached;

    /// <summary>
    /// 프로필 파일 경로. <see cref="PlayerStorage"/>의 꼬리표가 붙는다 —
    /// 한 PC에서 두 클라이언트를 띄워 테스트할 때 서로 다른 사람이 되게 하려는 것이다.
    /// </summary>
    public static string FilePath => Path.Combine(
        Application.persistentDataPath, FileNameBase + PlayerStorage.FileSuffix + FileExtension);

    /// <summary>이 기기가 이름을 정한 적이 있는가. 없으면 첫 실행이므로 입력 창을 띄운다.</summary>
    public static bool Exists => File.Exists(FilePath);

    /// <summary>사람이 정한 이름. 꼬리표는 붙지 않는다.</summary>
    public static string Nickname => Load().nickname;

    /// <summary>
    /// 서버에 올리는 <b>신원</b>. 표시 이름이 아니다 — 이름이 겹쳐도 이것은 겹치지 않는다.
    /// </summary>
    public static string PlayerId => Load().playerId;

    /// <summary>사람에게 보여 주는 이름. <c>이름#1234</c> 형태다.</summary>
    public static string DisplayName
    {
        get
        {
            PlayerProfileData d = Load();
            return $"{d.nickname}#{d.tag}";
        }
    }

    /// <summary>
    /// 이름을 바꾼다. <b>꼬리표는 건드리지 않는다</b> — 같은 사람으로 남아야 하기 때문이다.
    /// 빈 이름은 무시하고, 너무 길면 자른다.
    /// </summary>
    public static void SetNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return;

        string trimmed = nickname.Trim();
        if (trimmed.Length > MaxNicknameLength)
            trimmed = trimmed.Substring(0, MaxNicknameLength);

        PlayerProfileData d = Load();
        d.nickname = trimmed;
        Save(d);
    }

    /// <summary>프로필을 읽는다. 없으면 새로 만들어 저장한 뒤 돌려준다.</summary>
    public static PlayerProfileData Load()
    {
        if (_cached != null) return _cached;

        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonUtility.FromJson<PlayerProfileData>(File.ReadAllText(FilePath));

                // 손상됐거나 옛 형식이면 빈 칸만 채워 넣는다. 통째로 버리지 않는다.
                if (loaded != null)
                {
                    bool repaired = false;

                    if (string.IsNullOrWhiteSpace(loaded.nickname)) { loaded.nickname = DefaultNickname; repaired = true; }
                    if (string.IsNullOrWhiteSpace(loaded.tag)) { loaded.tag = NewTag(); repaired = true; }
                    if (string.IsNullOrWhiteSpace(loaded.playerId)) { loaded.playerId = NewPlayerId(); repaired = true; }

                    // ★ 채운 값은 곧바로 파일에 남긴다.
                    //   안 그러면 playerId가 <b>실행할 때마다 새로 만들어져</b> 신원이 되지 못한다
                    //   (playerId가 없던 옛 프로필이 정확히 그 상태였다).
                    if (repaired)
                    {
                        Save(loaded);
                        return _cached;
                    }

                    _cached = loaded;
                    return _cached;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PlayerProfile] 프로필을 읽지 못해 새로 만듭니다: {e.Message}");
        }

        _cached = new PlayerProfileData
        {
            nickname = DefaultNickname,
            tag = NewTag(),
            playerId = NewPlayerId(),
        };
        return _cached;
    }

    private static void Save(PlayerProfileData data)
    {
        _cached = data;

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[PlayerProfile] 저장했습니다: {DisplayName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerProfile] 저장하지 못했습니다: {e.Message}");
        }
    }

    /// <summary>
    /// 같은 닉네임을 고른 사람들을 <b>화면에서</b> 가르는 꼬리표. 1000~9999라 항상 네 자리다.
    ///
    /// ★ 이것은 신원이 아니다. 9000가지뿐이고 서버가 유일성을 검사하지도 않는다.
    ///   신원은 <see cref="PlayerProfileData.playerId"/> 가 맡는다.
    /// </summary>
    private static string NewTag() => UnityEngine.Random.Range(1000, 10000).ToString();

    /// <summary>설치본마다 한 번 만들어지는 신원. 사실상 겹치지 않는다.</summary>
    private static string NewPlayerId() => System.Guid.NewGuid().ToString("N");
}
