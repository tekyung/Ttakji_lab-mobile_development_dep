// DeckStorage.cs — 저장된 덱 파일의 위치를 관리하는 단일 창구
//
// ★ 왜 옮겼나
//   종전에는 덱을 `Application.dataPath/MyDeck`에 저장했다. 에디터에서는 그게 프로젝트의
//   `Assets/MyDeck`이라 잘 동작했지만, **안드로이드에서 `Application.dataPath`는 APK 내부를 가리킨다.**
//   읽기 전용인 데다 일반 디렉터리도 아니라서 덱 저장·불러오기가 통째로 실패한다.
//   `Application.persistentDataPath`는 플랫폼마다 앱 전용 쓰기 가능 폴더를 돌려준다.
//
// ★ 기존 덱은 자동으로 옮겨 온다
//   `Assets/MyDeck`에 이미 만들어 둔 덱들이 있어서, 경로만 갈아치우면 화면에서 사라진 것처럼 보인다.
//   그래서 새 폴더가 비어 있을 때 한 번만 옛 폴더의 `*.json`을 복사해 온다.
//   (안드로이드에서는 옛 폴더를 읽을 수 없으므로 조용히 건너뛴다)
//
// 경로를 쓰는 곳은 전부 이 클래스를 거친다. 새로 `Application.dataPath`를 직접 부르지 말 것.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class DeckStorage
{
    private const string FolderName = "MyDeck";
    private const string DeckExtension = ".json";

    private static bool _migrationChecked;

    /// <summary>덱이 저장되는 폴더. 플랫폼마다 앱 전용 쓰기 가능 위치다.</summary>
    public static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

    /// <summary>구 저장 위치. 에디터/PC에서만 의미가 있다 (안드로이드에서는 APK 내부).</summary>
    public static string LegacyFolderPath => Path.Combine(Application.dataPath, FolderName);

    /// <summary>덱 폴더를 보장하고 경로를 돌려준다. 최초 1회 구 폴더에서 덱을 옮겨 온다.</summary>
    public static string EnsureFolder()
    {
        string path = FolderPath;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        MigrateLegacyDecksOnce(path);
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

    /// <summary>
    /// 구 폴더(`Assets/MyDeck`)의 덱을 새 폴더로 한 번만 복사한다.
    /// 새 폴더에 이미 같은 이름이 있으면 건드리지 않는다 — 사용자가 새로 만든 덱이 우선이다.
    /// </summary>
    private static void MigrateLegacyDecksOnce(string targetFolder)
    {
        if (_migrationChecked) return;
        _migrationChecked = true;

        string legacy = LegacyFolderPath;
        if (legacy == targetFolder) return;

        try
        {
            if (!Directory.Exists(legacy)) return;

            var copied = new List<string>();
            foreach (string src in Directory.GetFiles(legacy, "*" + DeckExtension))
            {
                string dest = Path.Combine(targetFolder, Path.GetFileName(src));
                if (File.Exists(dest)) continue;

                File.Copy(src, dest);
                copied.Add(Path.GetFileNameWithoutExtension(src));
            }

            if (copied.Count > 0)
                Debug.Log($"[DeckStorage] 기존 덱 {copied.Count}개를 새 저장 위치로 옮겼다: {string.Join(", ", copied)}");
        }
        catch (System.Exception e)
        {
            // 안드로이드에서 APK 내부를 읽으려다 실패하는 경우가 여기로 온다. 치명적이지 않다.
            Debug.Log($"[DeckStorage] 기존 덱 이전 생략: {e.Message}");
        }
    }
}
