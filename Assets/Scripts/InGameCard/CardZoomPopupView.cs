// CardZoomPopupView.cs — 카드 확대 보기 세 화면의 참조 모음.
//
// 구조 (프리팹: Resources/Build/CardZoomPopupRoot):
//   CardZoomPopupRoot           이 컴포넌트
//   ├── CardSubPopup   [꺼짐]   화면 왼쪽 중앙. 읽기 전용 미리보기 (Canvas 780)
//   │   └── SubImage
//   ├── CardZoomPopup  [꺼짐]   화면 전체 딤 + 가운데 확대 (Canvas 850)
//   │   ├── ZoomImage
//   │   ├── ZoomName
//   │   └── ZoomDesc
//   └── GraveyardPanel [꺼짐]   화면 오른쪽 벽. 폐기존 목록 (Canvas 800)
//       ├── GraveTitle
//       ├── GraveClose └ Label
//       └── GraveScroll → Viewport → Content
//
// 나눠 갖는 몫:
//   프리팹 — 위치·크기·앵커·글꼴·바탕색·정렬 순서
//   코드   — 어떤 카드를 보여 줄지 · 열고 닫기 · 목록 채우기 · 제목 문구
//
// ★ 예전에는 서브 팝업과 확대 상자의 크기를 <b>실행 중에 캔버스 높이로 계산</b>했다.
//   이제 프리팹에 적힌 크기를 그대로 쓴다 — CanvasScaler가 해상도를 맡으므로
//   계산이 한 겹 줄고, 손으로 조절할 수 있게 된다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardZoomPopupView : MonoBehaviour
{
    [Header("서브 팝업 (왼쪽, 읽기 전용)")]
    public GameObject subRoot;
    public Image subImage;

    [Tooltip("서브 팝업을 한 번 더 누르면 가운데 확대로 넘어간다.")]
    public Button subButton;

    [Header("확대 팝업 (가운데, 모달)")]
    public GameObject zoomRoot;
    public Image zoomImage;
    public TextMeshProUGUI zoomName;
    public TextMeshProUGUI zoomDesc;

    [Tooltip("배경을 누르면 닫힌다.")]
    public Button zoomBackground;

    [Header("폐기존 패널 (오른쪽)")]
    public GameObject graveRoot;
    public TextMeshProUGUI graveTitle;
    public Button graveCloseButton;

    [Tooltip("목록이 쌓일 자리. VerticalLayoutGroup이 붙어 있어야 한다.")]
    public RectTransform graveContent;

    /// <summary>인스펙터에서 비워 둔 것을 이름으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (subRoot == null) subRoot = FindChildObject("CardSubPopup");
        if (subImage == null) subImage = FindChild<Image>("SubImage");
        if (subButton == null && subRoot != null) subButton = subRoot.GetComponent<Button>();

        if (zoomRoot == null) zoomRoot = FindChildObject("CardZoomPopup");
        if (zoomImage == null) zoomImage = FindChild<Image>("ZoomImage");
        if (zoomName == null) zoomName = FindChild<TextMeshProUGUI>("ZoomName");
        if (zoomDesc == null) zoomDesc = FindChild<TextMeshProUGUI>("ZoomDesc");
        if (zoomBackground == null && zoomRoot != null) zoomBackground = zoomRoot.GetComponent<Button>();

        if (graveRoot == null) graveRoot = FindChildObject("GraveyardPanel");
        if (graveTitle == null) graveTitle = FindChild<TextMeshProUGUI>("GraveTitle");
        if (graveCloseButton == null) graveCloseButton = FindChild<Button>("GraveClose");
        if (graveContent == null) graveContent = FindChild<RectTransform>("Content");
    }

    /// <summary>빠진 참조를 이름으로 알린다. 하나라도 없으면 false.</summary>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (subRoot == null) missing.Add(nameof(subRoot));
        if (subImage == null) missing.Add(nameof(subImage));
        if (zoomRoot == null) missing.Add(nameof(zoomRoot));
        if (zoomImage == null) missing.Add(nameof(zoomImage));
        if (graveRoot == null) missing.Add(nameof(graveRoot));
        if (graveContent == null) missing.Add(nameof(graveContent));

        // ★ 닫는 수단이 없으면 갇힌다. 크게 알린다.
        if (zoomBackground == null)
            Debug.LogError($"[{name}] 확대 팝업의 배경 버튼이 없다. 눌러서 닫을 수 없다.");
        if (graveCloseButton == null)
            Debug.LogError($"[{name}] 폐기존 [닫기]가 없다. 패널을 닫을 수 없다.");

        // 이름·설명은 없어도 그림은 보인다.
        if (zoomName == null || zoomDesc == null)
            Debug.LogWarning($"[{name}] 확대 팝업의 이름 또는 설명이 없다.");

        if (graveContent != null && graveContent.GetComponent<VerticalLayoutGroup>() == null)
            missing.Add(nameof(graveContent) + "(VerticalLayoutGroup 없음)");

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
        RectTransform found = FindChild<RectTransform>(targetName);
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
