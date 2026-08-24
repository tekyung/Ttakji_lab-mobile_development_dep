// UiSortingLayer.cs — 이 오브젝트를 다른 UI보다 앞에 그리게 한다.
//
// 왜 필요한가:
//   같은 캔버스 안에서 UI는 **하이어라키 순서**대로 그려진다. 나중에 있는 것이 위로 온다.
//   그런데 코드가 실행 중에 만들어 캔버스에 붙이는 UI는 언제나 맨 뒤에 붙으므로,
//   씬에 미리 놓인 패널은 무조건 그 아래로 깔린다.
//   실제로 화면 가운데 페이즈 표시 바(StatusHeader)가 '설정' 패널을 덮어 버렸다.
//
//   이럴 때 Canvas를 하나 붙이고 정렬 순서를 직접 정해 주면 하이어라키 순서를 넘어설 수 있다.
//   이 컴포넌트가 그 두 줄(Canvas + GraphicRaycaster)을 대신 붙여 준다.
//
// 쓰는 법: 앞으로 끌어올리고 싶은 패널에 붙이고 순서를 정한다.
//
// ── 이 프로젝트의 정렬 순서 사다리 ──────────────────────────────────
//     0   보드·씬 UI (하이어라키 순서대로) · 페이즈 표시 바
//    10   손패에서 집어 든 카드
//   450   설정 패널      ← 이 컴포넌트의 기본값
//   500   카드 선택 다이얼로그
//   600   톱니바퀴 버튼
//   700   덱 리스트 패널
//   780   폐기존 목록
//   800   폐기존 패널
//   850   카드 확대 팝업
//   900   승패 결과 오버레이
//
// 값을 정할 때는 위 사다리를 보고 **가려서는 안 되는 것보다 아래**에 둔다.
// (설정 패널이 700보다 높으면, 거기서 [항복]을 눌러 열리는 덱 패널이 뒤에 숨는다)
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class UiSortingLayer : MonoBehaviour
{
    [Tooltip("클수록 앞에 그려진다. 위 주석의 사다리를 보고 정할 것.")]
    public int sortingOrder = 450;

    [Tooltip("클릭을 받아야 하는 패널이면 켠다. Canvas를 붙이면 레이캐스터도 있어야 버튼이 눌린다.")]
    public bool needsClicks = true;

    private void Awake() => Apply();

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        Apply();
    }
#endif

    private void Apply()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        // Canvas를 붙이면 이 아래는 별도의 레이캐스트 대상이 된다.
        // 레이캐스터가 없으면 버튼이 통째로 먹통이 되므로 짝으로 붙인다.
        if (needsClicks && GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }
}
