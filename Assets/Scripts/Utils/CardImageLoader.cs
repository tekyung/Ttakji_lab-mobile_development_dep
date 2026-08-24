using TCG_Project.Scripts.Systems;
using UnityEngine;
using UnityEngine.UI;

public static class CardImageLoader
{
    public static string NormalizeResourcesPath(string originalPath)
    {
        if (string.IsNullOrEmpty(originalPath))
            return string.Empty;

        string path = originalPath
            .Replace("Assets/Resources/", "")
            .Replace(".png", "")
            .Replace(".jpg", "");

        return path;
    }

    public static Sprite LoadSprite(string originalPath)
    {
        string path = NormalizeResourcesPath(originalPath);
        if (string.IsNullOrEmpty(path))
            return null;

        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        Sprite[] all = Resources.LoadAll<Sprite>(path);
        return all.Length > 0 ? all[0] : null;
    }

    public static bool ApplyToImage(Image image, string originalPath)
    {
        if (image == null)
            return false;

        Sprite sprite = LoadSprite(originalPath);
        if (sprite == null)
            return false;

        image.sprite = sprite;
        image.preserveAspect = true;
        return true;
    }

    public static bool ApplyDefaultCardBack(GameObject cardObject)
    {
        return ApplyCardBack(cardObject, GameRules.DefaultCardBackPath);
    }

    /// <summary>
    /// 카드 뒷면 그림을 입힌다.
    ///
    /// ★ 예전에는 자식 Image를 <b>전부</b> 칠했다. 그런데 뒷면 오브젝트 안에는
    ///   [회수] 버튼이 들어 있어서, 그 버튼의 배경까지 카드 뒷면으로 바뀌었다.
    ///   preserveAspect까지 켜지는 바람에 <b>뒷면 위에 작은 뒷면이 얹힌</b> 모양이 됐다.
    ///
    ///   그래서 규칙을 좁혔다:
    ///     ① 루트에 Image가 있으면 <b>그것만</b> 칠하고 끝낸다 (보통 여기서 끝난다)
    ///     ② 루트에 Image가 없을 때만 자식을 훑되, 버튼 같은 조작 요소는 건너뛴다
    /// </summary>
    public static bool ApplyCardBack(GameObject cardObject, string originalPath)
    {
        if (cardObject == null || string.IsNullOrEmpty(originalPath))
            return false;

        Image rootImage = cardObject.GetComponent<Image>();
        if (rootImage != null)
        {
            bool ok = ApplyToImage(rootImage, originalPath);
            if (!ok)
            {
                Debug.LogWarning(
                    $"[CardImageLoader] 카드 뒷면 이미지 적용 실패 — 로드 실패: {originalPath}");
            }

            return ok;
        }

        bool applied = false;
        foreach (Image childImage in cardObject.GetComponentsInChildren<Image>(true))
        {
            // 버튼의 배경은 그 버튼의 모양이다. 뒷면으로 덮어쓰면 안 된다.
            if (childImage.GetComponentInParent<Selectable>(true) != null)
                continue;

            applied |= ApplyToImage(childImage, originalPath);
        }

        if (!applied)
        {
            Debug.LogWarning(
                $"[CardImageLoader] 카드 뒷면 이미지 적용 실패 — Image 없음 또는 로드 실패: {originalPath}");
        }

        return applied;
    }
}
