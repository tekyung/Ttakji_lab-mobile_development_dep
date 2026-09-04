// SafeAreaFitter.cs — 붙은 UI를 화면의 "안전 영역" 안으로 밀어 넣는다.
//
// 왜 필요한가:
//   요즘 휴대폰 화면은 네모가 아니다. 위쪽에 카메라 구멍(노치)이 파여 있고,
//   아래쪽에는 홈 표시줄이 늘 떠 있으며, 모서리는 둥글다.
//   그런 자리에 놓인 UI는 <b>가려지거나 잘린다.</b>
//
//   OS는 "여기까지는 안전하다"는 사각형을 <c>Screen.safeArea</c>로 알려 준다.
//   이 컴포넌트는 그 사각형에 맞춰 자기 앵커를 다시 잡는다.
//
// 쓰는 법: 화면 가장자리에 붙는 패널의 <b>루트</b>에 붙인다.
//   지금 붙는 곳 — DeckInfoPanelRoot(좌상단 톱니) · GameStatusPanelRoot(좌측 로그)
//                  · CardZoomPopupRoot(우측 폐기존)
//
// ── 알아 둘 것 ────────────────────────────────────────────────────
//   • 에디터와 PC에서는 safeArea가 화면 전체다. 그래서 <b>아무 일도 하지 않는다</b> —
//     데스크톱 배치는 지금 그대로다.
//   • 처음 앵커를 기억해 두고, 그것을 안전 영역 안쪽으로 옮겨 담는다.
//     그래서 화면 가득 채운 것이든 한쪽 구석에 붙은 것이든 똑같이 동작한다.
//   • 화면 크기나 방향이 바뀔 때만 다시 계산한다. 매 프레임 앵커를 건드리지 않는다.
//   • 전체를 덮는 어두운 배경(확대 팝업의 딤)에 붙이면 노치 자리만 덮이지 않는다.
//     가려지는 것보다는 낫다는 판단으로 지금은 그대로 둔다.
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("좌우 여백도 피할지. 가로 화면에서 노치는 옆에 오므로 보통 켜 둔다.")]
    public bool applyHorizontal = true;

    [Tooltip("위아래 여백도 피할지. 홈 표시줄을 피하려면 켜 둔다.")]
    public bool applyVertical = true;

    private RectTransform _rect;

    /// <summary>붙을 때의 앵커. 안전 영역은 여기에 곱해서 얹는다.</summary>
    private Vector2 _baseAnchorMin;
    private Vector2 _baseAnchorMax;
    private bool _baseCaptured;

    /// <summary>마지막으로 반영한 상태. 같으면 다시 계산하지 않는다.</summary>
    private Rect _appliedArea;
    private int _appliedWidth;
    private int _appliedHeight;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        CaptureBase();
    }

    private void OnEnable() => Apply(force: true);

    private void Update()
    {
        // 화면이 그대로면 아무것도 하지 않는다. 비교 세 번이 전부다.
        if (Screen.width == _appliedWidth && Screen.height == _appliedHeight
            && Screen.safeArea == _appliedArea) return;

        Apply(force: false);
    }

    private void CaptureBase()
    {
        if (_baseCaptured || _rect == null) return;

        _baseAnchorMin = _rect.anchorMin;
        _baseAnchorMax = _rect.anchorMax;
        _baseCaptured = true;
    }

    private void Apply(bool force)
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_rect == null) return;

        CaptureBase();

        int w = Screen.width;
        int h = Screen.height;

        // 화면 크기를 아직 모를 때가 있다(로딩 첫 프레임). 나누면 안 되니 다음 프레임에 다시 온다.
        if (w <= 0 || h <= 0) return;

        Rect area = Screen.safeArea;
        if (area.width <= 0f || area.height <= 0f) return;

        if (!force && w == _appliedWidth && h == _appliedHeight && area == _appliedArea) return;

        // 안전 영역을 0~1 비율로 바꾼다.
        float safeMinX = applyHorizontal ? area.xMin / w : 0f;
        float safeMaxX = applyHorizontal ? area.xMax / w : 1f;
        float safeMinY = applyVertical ? area.yMin / h : 0f;
        float safeMaxY = applyVertical ? area.yMax / h : 1f;

        float spanX = safeMaxX - safeMinX;
        float spanY = safeMaxY - safeMinY;

        // ★ 처음 앵커를 안전 영역 <b>안쪽 좌표계</b>로 옮겨 담는다.
        //   화면 가득 채운 것(0~1)이면 그대로 안전 영역이 되고,
        //   한쪽 구석에 붙은 것이면 그 비율을 지킨 채 안쪽으로 들어온다.
        _rect.anchorMin = new Vector2(
            safeMinX + _baseAnchorMin.x * spanX,
            safeMinY + _baseAnchorMin.y * spanY);

        _rect.anchorMax = new Vector2(
            safeMinX + _baseAnchorMax.x * spanX,
            safeMinY + _baseAnchorMax.y * spanY);

        _appliedArea = area;
        _appliedWidth = w;
        _appliedHeight = h;
    }
}
