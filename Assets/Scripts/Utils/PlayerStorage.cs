// PlayerStorage.cs — "이 클라이언트의 것"과 "이 기기의 것"을 가르는 창구
//
// ★ 왜 필요한가 (2026-09-07)
//   한 PC에서 2인 대전을 테스트했더니 **두 사람이 같은 덱으로 시작**했다.
//   원인은 코드가 아니라 저장 위치였다 —
//     PlayerPrefs  →  HKCU\Software\{companyName}\{productName}
//     파일         →  {persistentDataPath} = .../LocalLow/{companyName}/{productName}
//   둘 다 <b>기기 + 제품</b> 단위다. 에디터든 빌드든 같은 곳을 본다.
//   그래서 `SelectedDeckName` 칸이 하나뿐이고, 나중에 고른 쪽이 두 클라이언트 모두의 값이 됐다.
//   profile.json도 공유되어 <b>닉네임과 태그까지 똑같아졌다.</b>
//
// ★ 어떻게 가르는가
//     1. `-profile <이름>` 인자가 있으면  → 그것을 쓴다 (가장 세다)
//     2. 없으면
//        · 스탠드얼론 빌드              → <b>설치 폴더</b>에서 자동 생성
//        · 에디터 / 모바일 / 그 밖      → 기본 칸(빈 문자열)
//
//   즉 빌드를 `C:\TestA\` 와 `C:\TestB\` 에 각각 풀어 두면 <b>인자 없이도</b> 다른 사람이 된다.
//
// ★ 왜 파일을 빌드 폴더에 두지 않는가 (그게 더 간단해 보이지만)
//   빌드 폴더는 곧 `Application.dataPath`인데, <b>안드로이드에서 그것은 APK 내부</b>다.
//   읽기 전용인 데다 일반 디렉터리도 아니라 저장이 통째로 실패한다 —
//   2026-08-17에 이미 겪고 persistentDataPath로 옮긴 사고다(DeckStorage.cs 상단 주석).
//   그래서 <b>저장 위치는 그대로 두고 이름표만 폴더에서 뽑는다.</b>
//
// ★ 왜 모바일은 자동 생성에서 빼는가
//   안드로이드의 APK 경로는 `/data/app/<패키지>-<임의문자>/base.apk` 꼴이고,
//   <b>앱을 업데이트하면 그 임의 부분이 바뀐다.</b> 경로로 이름표를 만들면
//   업데이트할 때마다 플레이어의 닉네임과 덱 선택이 리셋된다.
//
// ★ 왜 에디터도 빼는가
//   기본 칸에 남겨 두면 지금까지 쓰던 프로필과 덱 선택이 그대로 보존되고,
//   빌드들과는 어차피 갈린다.
//
//   <b>덱 파일 자체는 일부러 가르지 않는다.</b> 덱은 공용 자료실이고,
//   갈라야 하는 것은 "누가 무엇을 골랐는가"와 "내가 누구인가"뿐이다.
//   그래야 빌드 쪽에서 덱을 새로 만들지 않고도 서로 다른 덱을 고를 수 있다.
using System.IO;
using UnityEngine;

public static class PlayerStorage
{
    private const string SelectedDeckKeyBase = "SelectedDeckName";
    private const string ProfileArg = "-profile";

    /// <summary>이름표에 넣는 폴더 이름의 길이 상한. 넘으면 자른다.</summary>
    private const int MaxFolderLabelLength = 12;

    private static string _suffix;
    private static bool _resolved;

    /// <summary>
    /// 이 클라이언트를 가르는 이름표. 기본은 빈 문자열(= 지금까지와 같은 칸).
    /// </summary>
    public static string Suffix
    {
        get
        {
            if (_resolved) return _suffix;
            _resolved = true;
            _suffix = ResolveSuffix();

            if (!string.IsNullOrEmpty(_suffix))
                Debug.Log($"[PlayerStorage] 저장 칸 '{_suffix}' 를 씁니다 (설치 위치: {Application.dataPath})");

            return _suffix;
        }
    }

    /// <summary>고른 덱 이름이 담기는 PlayerPrefs 키.</summary>
    public static string SelectedDeckKey => SelectedDeckKeyBase + Suffix;

    /// <summary>이 클라이언트가 고른 덱 이름.</summary>
    public static string GetSelectedDeck() => PlayerPrefs.GetString(SelectedDeckKey, "");

    /// <summary>고른 덱을 기억한다.</summary>
    public static void SetSelectedDeck(string deckName)
    {
        PlayerPrefs.SetString(SelectedDeckKey, deckName);
        PlayerPrefs.Save();
    }

    /// <summary>파일 이름에 붙일 이름표. <c>profile.json</c> → <c>profile_TestA_a3f91c2b.json</c></summary>
    public static string FileSuffix => Suffix;

    // ─── 판정 ───────────────────────────────────────────────────────────

    private static string ResolveSuffix()
    {
        // 1) 인자가 가장 세다. 폴더와 무관하게 원하는 칸을 지정할 수 있어야 한다.
        string fromArg = ResolveFromArgs();
        if (!string.IsNullOrEmpty(fromArg)) return fromArg;

        // 2) 스탠드얼론 빌드만 설치 폴더로 자동 구분한다. 이유는 파일 상단 주석 참조.
#if !UNITY_EDITOR && UNITY_STANDALONE
        return DeriveSuffixFromPath(Application.dataPath);
#else
        return "";
#endif
    }

    private static string ResolveFromArgs()
    {
        string[] args;
        try
        {
            args = System.Environment.GetCommandLineArgs();
        }
        catch (System.Exception)
        {
            return "";   // 일부 플랫폼(WebGL 등)은 인자를 못 읽는다.
        }

        if (args == null) return "";

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], ProfileArg, System.StringComparison.OrdinalIgnoreCase))
                continue;

            string label = Sanitize(args[i + 1], int.MaxValue);
            return string.IsNullOrEmpty(label) ? "" : "_" + label;
        }

        return "";
    }

    /// <summary>
    /// 설치 경로에서 이름표를 만든다. <c>_TestA_a3f91c2b</c> 꼴이다.
    ///
    /// 폴더 이름은 <b>사람이 읽으려고</b> 넣는다 — 파일 이름만 보고 어느 빌드 것인지 알 수 있어야 한다.
    /// 해시는 <b>겹침을 막으려고</b> 넣는다 — 폴더 이름은 같아도 경로는 다를 수 있다
    /// (<c>C:/a/TestA</c> 와 <c>D:/b/TestA</c>).
    ///
    /// ★ 테스트에서 직접 부를 수 있도록 public 이다. <see cref="ResolveSuffix"/>가 쓰는 것과 같은 함수다.
    /// </summary>
    /// <param name="dataPath">보통 <c>Application.dataPath</c>.</param>
    public static string DeriveSuffixFromPath(string dataPath)
    {
        string normalized = Normalize(dataPath);
        if (string.IsNullOrEmpty(normalized)) return "";

        // 빌드 폴더 이름. dataPath 는 "<설치폴더>/<제품명>_Data" 라 부모가 곧 설치 폴더다.
        string folder = "";
        try
        {
            string parent = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrEmpty(parent)) folder = Path.GetFileName(parent);
        }
        catch (System.Exception)
        {
            // 경로 모양이 이상해도 해시만으로 충분히 갈린다.
        }

        string label = Sanitize(folder, MaxFolderLabelLength);
        string hash = StableHash(normalized).ToString("x8");

        return string.IsNullOrEmpty(label) ? "_" + hash : $"_{label}_{hash}";
    }

    // ─── 잡일 ───────────────────────────────────────────────────────────

    /// <summary>표기가 달라도 같은 폴더면 같은 값이 나오도록 다듬는다.</summary>
    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";

        string s = path.Trim().Replace('\\', '/').TrimEnd('/');
        return s.ToLowerInvariant();
    }

    /// <summary>파일 이름과 레지스트리 키에 함께 쓰이므로 ASCII 영숫자만 남긴다.</summary>
    private static string Sanitize(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var sb = new System.Text.StringBuilder();
        foreach (char c in value.Trim())
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                sb.Append(c);

            if (sb.Length >= maxLength) break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// 실행할 때마다 같은 값이 나오는 해시(FNV-1a, 32비트).
    ///
    /// ★ <c>string.GetHashCode()</c>를 쓰면 안 된다. 런타임/프로세스마다 값이 달라질 수 있어
    ///   <b>실행할 때마다 다른 이름표</b>가 나온다 — 그러면 켤 때마다 다른 사람이 된다.
    ///   (같은 이유로 playerId가 매번 새로 생기던 버그를 바로 앞에 겪었다)
    /// </summary>
    private static uint StableHash(string text)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        uint hash = OffsetBasis;
        foreach (char c in text)
        {
            hash ^= (byte)(c & 0xFF);
            hash *= Prime;
            hash ^= (byte)((c >> 8) & 0xFF);
            hash *= Prime;
        }

        return hash;
    }
}
