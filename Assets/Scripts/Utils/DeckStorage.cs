// DeckStorage.cs — 저장된 덱 파일의 위치를 관리하는 단일 창구
//
// ★ 왜 persistentDataPath인가
//   종전에는 덱을 `Application.dataPath/MyDeck`에 저장했다. 에디터에서는 그게 프로젝트의
//   `Assets/MyDeck`이라 잘 동작했지만, **안드로이드에서 `Application.dataPath`는 APK 내부를 가리킨다.**
//   읽기 전용인 데다 일반 디렉터리도 아니라서 덱 저장·불러오기가 통째로 실패한다.
//   `Application.persistentDataPath`는 플랫폼마다 앱 전용 쓰기 가능 폴더를 돌려준다.
//
// ★ 구 폴더 이관 기능은 <b>일부러 없앴다</b> (2026-09-03). 다시 넣지 말 것.
//   경로를 옮기면서 `Assets/MyDeck`의 덱을 새 폴더로 복사해 오는 코드를 두었는데,
//   이름은 `MigrateLegacyDecksOnce`였지만 **한 번만 돌지 않았다.**
//   판정에 쓰던 static 플래그가 앱을 다시 켤 때마다 초기화되기 때문이다.
//
//   그래서 이런 일이 났다:
//     · 덱을 지워도 <b>다음 실행에 되살아났다</b> (구 폴더에 있으면 다시 복사)
//     · 구 폴더의 여섯 개가 "기본으로 주어지는 덱"처럼 보였다
//     · 그중 `새 덱.json`은 이름과 달리 <b>베로니카 카드 20장짜리 실제 덱</b>이었다
//
//   이관은 제 몫을 이미 다 했고(2026-08-17), 남아서 하는 일은 해로움뿐이었다.
//   구 폴더 `Assets/MyDeck`도 저장소에서 지웠다.
//
// 경로를 쓰는 곳은 전부 이 클래스를 거친다. 새로 `Application.dataPath`를 직접 부르지 말 것.
using System.IO;
using UnityEngine;

public static class DeckStorage
{
    private const string FolderName = "MyDeck";
    private const string DeckExtension = ".json";

    /// <summary>덱이 저장되는 폴더. 플랫폼마다 앱 전용 쓰기 가능 위치다.</summary>
    public static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

    /// <summary>덱 폴더를 보장하고 경로를 돌려준다.</summary>
    public static string EnsureFolder()
    {
        string path = FolderPath;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>덱 이름으로 파일 경로를 만든다. 확장자는 붙어 있어도 되고 없어도 된다.</summary>
    public static string GetDeckPath(string deckName)
    {
        if (string.IsNullOrEmpty(deckName)) return null;

        string fileName = deckName.EndsWith(DeckExtension) ? deckName : deckName + DeckExtension;
        return Path.Combine(EnsureFolder(), fileName);
    }

    /// <summary>저장된 덱 파일 경로 목록.</summary>
    public static string[] GetDeckFiles() => Directory.GetFiles(EnsureFolder(), "*" + DeckExtension);

    /// <summary>해당 이름의 덱이 있는지.</summary>
    public static bool DeckExists(string deckName)
    {
        string path = GetDeckPath(deckName);
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    /// <summary>덱 파일을 지운다. 없으면 아무 일도 하지 않는다.</summary>
    public static bool DeleteDeck(string deckName)
    {
        string path = GetDeckPath(deckName);
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }
}
