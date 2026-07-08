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

    public static bool ApplyCardBack(GameObject cardObject, string originalPath)
    {
        if (cardObject == null || string.IsNullOrEmpty(originalPath))
            return false;

        bool applied = false;

        Image rootImage = cardObject.GetComponent<Image>();
        if (rootImage != null)
            applied |= ApplyToImage(rootImage, originalPath);

        Image[] childImages = cardObject.GetComponentsInChildren<Image>(true);
        foreach (Image childImage in childImages)
        {
            if (childImage == rootImage)
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
