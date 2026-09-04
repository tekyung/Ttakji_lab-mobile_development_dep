// CardPreviewPanelView.cs — 덱 편집 화면 좌측 하단 "미리보기"의 참조 모음.
//
// 왜 있는가:
//   덱을 짜면서 카드 효과를 읽을 방법이 없었다. 길게 눌러야 뜨는 전체화면 확대가
//   하나 있었지만 그림만 보여 준다. 고르는 내내 옆에 펼쳐 두고 볼 자리가 필요했다.
//
//   화면을 코드로 만들지 않는다. 사람이 씬에 배치하고 이 컴포넌트만 붙이면,
//   DeckBuilderManager가 "무엇을 그릴지"만 여기에 채운다.
//   (CharacterSlotView와 같은 성격이다)
//
// 씬에서 쓰는 법:
//   1. 좌측 하단에 아래 구조로 오브젝트를 만든다 — 크기는 원하는 대로
//
//        CardPreviewPanel      ← 이 컴포넌트
//        ├── ImagePanel        그림이 들어갈 상자
//        │   └── Image         ← 그림. 상자에 꽉 차게 늘려 둔다
//        ├── Name              선택 — 안 만들어도 된다
//        ├── DescPanel         효과가 들어갈 상자
//        │   └── Desc          ← 효과 텍스트
//        └── EmptyHint         선택 — 아무것도 안 골랐을 때 보일 안내
//
//   2. 루트에 이 컴포넌트를 붙인다 (이름을 위와 같게 두면 자동으로 채워진다)
//   3. DeckBuilderManager의 cardPreview 에 연결한다
//
// ★ 비율은 신경 쓰지 않아도 된다.
//   그림을 넣을 때 CardImageLoader가 preserveAspect를 켜므로,
//   <b>상자 안에서 원본 비율을 지킨 채 최대로</b> 커진다.
//   상자를 키우면 그림도 따라 커진다 — 코드는 관여하지 않는다. 용병 슬롯 초상과 같은 방식이다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardPreviewPanelView : MonoBehaviour
{
    [Tooltip("카드·용병 그림. 상자에 꽉 차게 늘려 두면 그 안에서 비율을 지킨 채 커진다.")]
    public Image image;

    [Tooltip("효과 설명. 카드/용병의 description이 그대로 들어간다.")]
    public TextMeshProUGUI descText;

    [Tooltip("카드·용병 이름. 없어도 동작한다.")]
    public TextMeshProUGUI nameText;

    [Tooltip("그림 상자. 비어 있을 때 통째로 끄고 싶으면 연결한다. 없어도 동작한다.")]
    public GameObject imageRoot;

    [Tooltip("효과 상자. 비어 있을 때 통째로 끄고 싶으면 연결한다. 없어도 동작한다.")]
    public GameObject descRoot;

    [Tooltip("아무것도 고르지 않았을 때 보일 안내. 없어도 동작한다.")]
    public GameObject emptyHint;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (imageRoot == null) imageRoot = FindChildObject("ImagePanel");
        if (descRoot == null) descRoot = FindChildObject("DescPanel");
        if (emptyHint == null) emptyHint = FindChildObject("EmptyHint");

        if (image == null) image = FindChild<Image>("Image");
        if (nameText == null) nameText = FindChild<TextMeshProUGUI>("Name");
        if (descText == null) descText = FindChild<TextMeshProUGUI>("Desc");

        // 이름을 다르게 지었을 수도 있다. 상자 안에 하나뿐이면 그것으로 본다.
        if (image == null && imageRoot != null) image = imageRoot.GetComponentInChildren<Image>(true);
        if (descText == null && descRoot != null) descText = descRoot.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (image == null) missing.Add(nameof(image));
        if (descText == null) missing.Add(nameof(descText));

        // 이름은 없어도 그림과 효과만으로 쓸 만하다. 알리기만 한다.
        if (nameText == null)
            Debug.LogWarning($"[{name}] 이름 텍스트가 없다. 그림과 효과만 보여 준다.");

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }

        reason = $"연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    private GameObject FindChildObject(string targetName)
    {
        Transform found = FindChild<Transform>(targetName);
        return found != null ? found.gameObject : null;
    }

    private T FindChild<T>(string targetName) where T : Component
    {
        foreach (T candidate in GetComponentsInChildren<T>(true))
        {
            if (candidate.name == targetName && candidate.gameObject != gameObject) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
