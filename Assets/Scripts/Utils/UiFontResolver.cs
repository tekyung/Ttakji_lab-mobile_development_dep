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
//
// ★ 폴백은 **한 개로는 부족하다.**
//   OS 폰트마다 가진 글자가 다르다 — 맑은 고딕에는 한글이 다 있지만 ✓(U+2713) 같은
//   Dingbats 기호는 없고, Segoe UI Symbol에는 기호가 있지만 한글이 부실하다.
//   그래서 여러 폰트를 **순서대로 폴백 체인에 걸어** TMP가 글자마다 알아서 찾게 한다.
//   (예전에는 "제일 먼저 발견된 OS 폰트 하나"만 걸어서, 그 폰트에 없는 기호는 그대로 깨졌다)
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class UiFontResolver
{
    private static TMP_FontAsset _cached;
    private static List<TMP_FontAsset> _symbolFallbacks;

    /// <summary>
    /// 프로젝트 폰트에 없는 특수문자·이모지를 OS 폰트에서 가져오도록 폴백을 건다.
    ///
    /// TMP의 폴백은 "텍스트 컴포넌트"가 아니라 "폰트 에셋" 단위라서,
    /// 씬에서 실제로 쓰이는 폰트 에셋 전부에 한 번씩 걸어 준다.
    /// 이렇게 하면 우리가 만든 UI뿐 아니라 카드 프리팹의 이름 텍스트까지 함께 고쳐진다.
    ///
    /// 여러 번 불러도 안전하다. **UI를 새로 만든 뒤에는 다시 부르는 것이 좋다** —
    /// 그때 처음 등장한 폰트 에셋에도 폴백을 걸어야 하기 때문이다.
    ///
    /// 폴백 폰트는 런타임 생성이라 디스크에 저장되지 않는다(동적 아틀라스라 필요한 글자만 채운다).
    /// </summary>
    public static void EnsureSymbolFallback()
    {
        List<TMP_FontAsset> fallbacks = GetSymbolFallbacks();
        if (fallbacks.Count == 0) return;

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
            if (font == null || fallbacks.Contains(font)) continue;

            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();

            // 순서가 곧 우선순위다. 이미 들어 있는 것은 건너뛴다.
            foreach (TMP_FontAsset fallback in fallbacks)
            {
                if (!font.fallbackFontAssetTable.Contains(fallback))
                    font.fallbackFontAssetTable.Add(fallback);
            }
        }
    }

    /// <summary>
    /// 이 글자를 어딘가에서 그릴 수 있는가. 진단용이다.
    /// 깨지는 기호를 쓰기 전에 확인하거나, 로그로 원인을 남길 때 쓴다.
    /// </summary>
    public static bool CanRender(char character)
    {
        foreach (TMP_FontAsset font in GetSymbolFallbacks())
        {
            if (HasGlyph(font, character)) return true;
        }

        return _cached != null && HasGlyph(_cached, character);
    }

    /// <summary>
    /// 동적 폰트는 처음에 글리프 표가 비어 있다.
    /// 그래서 "있는지"만 묻지 말고 **넣어 보게** 해야 정확하다.
    /// </summary>
    private static bool HasGlyph(TMP_FontAsset font, char character)
    {
        if (font == null) return false;

        return font.HasCharacter(character, searchFallbacks: false, tryAddCharacter: true);
    }

    private static List<TMP_FontAsset> GetSymbolFallbacks()
    {
        if (_symbolFallbacks != null) return _symbolFallbacks;

        _symbolFallbacks = new List<TMP_FontAsset>();

        // 순서 = 우선순위.
        // 한글이 넓은 폰트를 앞에, 기호가 넓은 폰트를 뒤에 둔다.
        // TMP는 앞에서부터 찾다가 없으면 다음으로 넘어가므로 둘 다 걸어 두면 서로를 메운다.
        string[] candidates =
        {
            "Malgun Gothic", "맑은 고딕",
            "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic",
            "Segoe UI Symbol",   // ✓ ✔ ★ 등 Dingbats — 한글 폰트에는 대개 없다
            "Segoe UI Emoji",
            "Arial Unicode MS", "Arial",
        };

        foreach (string name in candidates)
        {
            // ★ 이름 배열을 한꺼번에 넘기면 "먼저 발견된 하나"만 얻는다.
            //   한 개씩 물어봐야 여러 폰트를 모을 수 있다.
            Font osFont = Font.CreateDynamicFontFromOSFont(new[] { name }, 48);
            if (osFont == null) continue;

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(osFont);
            if (asset == null) continue;

            asset.name = $"RuntimeFallback_{name}";
            asset.hideFlags = HideFlags.DontSave; // 에디터에서 프로젝트 에셋으로 저장되지 않도록
            _symbolFallbacks.Add(asset);
        }

        if (_symbolFallbacks.Count == 0)
        {
            Debug.LogWarning("[UiFontResolver] 폴백용 OS 폰트를 하나도 찾지 못했습니다. 특수문자가 ㅁ로 보일 수 있습니다.");
            return _symbolFallbacks;
        }

        Debug.Log($"[UiFontResolver] 기호 폴백 {_symbolFallbacks.Count}종 준비 — " +
                  string.Join(", ", _symbolFallbacks.ConvertAll(f => f.name.Replace("RuntimeFallback_", ""))));

        return _symbolFallbacks;
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
