// UiFontResolver.cs — 런타임에 만든 TMP 텍스트에 쓸 폰트를 찾아 준다.
//
// 코드로 TextMeshProUGUI를 만들면 기본 폰트(LiberationSans SDF)가 적용되는데,
// 이 폰트에는 한글 글리프가 없어서 모든 한글이 ㅁ로 깨진다.
// 그래서 씬이 실제로 쓰고 있는 폰트를 찾아 그대로 따라간다.
//
// 주의: 프로젝트 한글 폰트(CookieRun Black SDF)는 정적 아틀라스이고
//       글리프가 한글 11,172자 + ASCII뿐이다. **특수문자가 하나도 없다.**
//       → ★ — ▶ · × ° ← ♬ ⚔ ♥ ※ ✨ 및 이모지가 전부 ㅁ로 깨진다.
//       엔진 로그와 카드 이름(예: 소니아 "내 선물이야♬")이 이 문자들을 쓰므로
//       EnsureSymbolFallback()으로 OS 폰트를 폴백에 걸어 해결한다.
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class UiFontResolver
{
    private static TMP_FontAsset _cached;
    private static TMP_FontAsset _symbolFallback;
    private static bool _fallbackAttempted;

    /// <summary>
    /// 프로젝트 폰트에 없는 특수문자·이모지를 OS 폰트에서 가져오도록 폴백을 건다.
    ///
    /// TMP의 폴백은 "텍스트 컴포넌트"가 아니라 "폰트 에셋" 단위라서,
    /// 씬에서 실제로 쓰이는 폰트 에셋 전부에 한 번씩 걸어 준다.
    /// 이렇게 하면 우리가 만든 UI뿐 아니라 카드 프리팹의 이름 텍스트까지 함께 고쳐진다.
    ///
    /// 폴백 폰트는 런타임 생성이라 디스크에 저장되지 않는다(동적 아틀라스라 필요한 글자만 채운다).
    /// </summary>
    public static void EnsureSymbolFallback()
    {
        TMP_FontAsset fallback = GetSymbolFallback();
        if (fallback == null) return;

        var seen = new HashSet<TMP_FontAsset>();

        foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.font != null) seen.Add(t.font);
        }

        if (_cached != null) seen.Add(_cached);
        if (TMP_Settings.defaultFontAsset != null) seen.Add(TMP_Settings.defaultFontAsset);

        foreach (var font in seen)
        {
            if (font == null || font == fallback) continue;

            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback))
                font.fallbackFontAssetTable.Add(fallback);
        }
    }

    private static TMP_FontAsset GetSymbolFallback()
    {
        if (_symbolFallback != null) return _symbolFallback;
        if (_fallbackAttempted) return null;
        _fallbackAttempted = true;

        // 한글 + 기호를 폭넓게 담고 있는 OS 폰트를 순서대로 시도한다.
        // (Windows: 맑은 고딕 / Android: Noto Sans CJK)
        string[] candidates =
        {
            "Malgun Gothic", "맑은 고딕",
            "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic",
            "Segoe UI Symbol", "Arial Unicode MS", "Arial"
        };

        Font osFont = Font.CreateDynamicFontFromOSFont(candidates, 48);
        if (osFont == null)
        {
            Debug.LogWarning("[UiFontResolver] 폴백용 OS 폰트를 찾지 못했습니다. 특수문자가 ㅁ로 보일 수 있습니다.");
            return null;
        }

        _symbolFallback = TMP_FontAsset.CreateFontAsset(osFont);
        if (_symbolFallback == null)
        {
            Debug.LogWarning($"[UiFontResolver] '{osFont.name}'으로 TMP 폰트 에셋을 만들지 못했습니다.");
            return null;
        }

        _symbolFallback.name = "RuntimeSymbolFallback";
        _symbolFallback.hideFlags = HideFlags.DontSave; // 에디터에서 프로젝트 에셋으로 저장되지 않도록
        return _symbolFallback;
    }

    /// <summary>
    /// 씬에서 쓰이는 폰트를 반환한다. 기본 폰트(LiberationSans)가 아닌 것을 우선한다.
    /// 호출 시 특수문자 폴백도 함께 보장한다.
    /// </summary>
    public static TMP_FontAsset Resolve()
    {
        if (_cached != null)
        {
            EnsureSymbolFallback();
            return _cached;
        }

        var texts = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        TMP_FontAsset firstFound = null;
        foreach (var t in texts)
        {
            if (t == null || t.font == null) continue;
            if (firstFound == null) firstFound = t.font;

            if (!t.font.name.Contains("LiberationSans"))
            {
                _cached = t.font;
                EnsureSymbolFallback();
                return _cached;
            }
        }

        _cached = firstFound != null ? firstFound : TMP_Settings.defaultFontAsset;
        EnsureSymbolFallback();
        return _cached;
    }

    /// <summary>씬이 바뀌어 폰트를 다시 찾아야 할 때 호출한다.</summary>
    public static void Invalidate() => _cached = null;
}
