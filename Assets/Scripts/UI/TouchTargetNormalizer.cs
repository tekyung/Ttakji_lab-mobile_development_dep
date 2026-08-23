// TouchTargetNormalizer.cs — 버튼이 "손가락으로 누를 만한 크기"인지 보장한다.
//
// 왜 코드로 하는가:
//   씬·프리팹은 유니티 에디터에서 열어야 제대로 고칠 수 있고, 화면마다 버튼이 흩어져 있어
//   하나씩 손보면 새 화면이 생길 때마다 같은 실수가 반복된다.
//   그래서 씬이 로드될 때마다 훑어 **부족한 것만 키우는** 방식을 택했다.
//
// 규칙 (모두 "키우기만" 한다 — 이미 충분한 버튼은 건드리지 않는다):
//   1) 최소 높이 확보
//   2) 최소 4:3 비율 확보 (가로 ≥ 세로 × 4/3)
//   3) 캔버스 밖으로 삐져나온 버튼은 안쪽으로 끌어당긴다
//
// ⚠️ LayoutGroup이 배치를 관리하는 버튼은 sizeDelta를 직접 만져도 다음 프레임에 되돌아간다.
//    그런 버튼은 LayoutElement의 minWidth/minHeight로 "이만큼은 달라"고 요청한다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TouchTargetNormalizer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 조절값. 인스펙터에서 바꿀 수 있다.
    //
    // ★ 씬에 이 컴포넌트를 직접 올려 두면 그 설정이 쓰인다.
    //   올려 두지 않으면 아래 기본값으로 자동 생성된다.
    //   배치를 직접 잡는 동안에는 enabled를 꺼 두는 편이 편하다.
    // ─────────────────────────────────────────────────────────────

    [Header("끄고 켜기")]
    [Tooltip("끄면 아무것도 보정하지 않는다. 씬 배치를 손으로 잡을 때 꺼 두면 편하다.")]
    [SerializeField] private bool normalizeEnabled = true;

    [Header("최소 크기")]
    [Tooltip("캔버스 높이 대비 최소 버튼 높이 비율. 1080p 기준 0.05 ≈ 54px.")]
    [SerializeField] private float minHeightRatio = 0.05f;

    [Tooltip("비율과 무관하게 지켜야 할 최소 높이(캔버스 단위).")]
    [SerializeField] private float absoluteMinHeight = 72f;

    [Tooltip("가로 ≥ 세로 × 이 값. 기획에서 정한 4:3.")]
    [SerializeField] private float minAspect = 4f / 3f;

    [Tooltip("드롭다운이 주변 버튼 대비 최소 이 비율은 되게 한다.")]
    [SerializeField] private float dropdownSizeRatio = 0.8f;

    [Header("간격")]
    [Tooltip("버튼끼리 최소한 이만큼은 벌어져 있어야 한다. 겹쳐서 하나처럼 보이는 것을 막는다.")]
    [SerializeField] private float minGap = 16f;

    [Tooltip("캔버스 가장자리에서 이만큼은 떨어뜨린다.")]
    [SerializeField] private float edgePadding = 8f;

    [Tooltip("겹침을 푸는 반복 횟수. 셋이 나란히 겹치면 한 번으로는 안 풀린다.")]
    [SerializeField] private int separationPasses = 4;

    private static TouchTargetNormalizer _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        // 씬에 직접 올려 둔 것이 있으면 그 설정을 쓴다 (인스펙터로 조절하려는 의도)
        TouchTargetNormalizer existing = FindFirstObjectByType<TouchTargetNormalizer>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("TouchTargetNormalizer");
        _instance = go.AddComponent<TouchTargetNormalizer>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(NormalizeSoon());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(NormalizeSoon());

    /// <summary>
    /// 레이아웃이 한 번 돌고 난 뒤에 잰다.
    /// 씬 로드 직후에는 LayoutGroup이 아직 자리를 잡지 않아 크기가 0인 버튼이 섞여 있다.
    /// </summary>
    private IEnumerator NormalizeSoon()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        Normalize();
    }

    /// <summary>수동 호출용. 화면을 코드로 새로 그린 뒤에 부르면 된다.</summary>
    public static void NormalizeNow()
    {
        if (_instance != null) _instance.StartCoroutine(_instance.NormalizeSoon());
    }

    private void Normalize()
    {
        if (!normalizeEnabled) return;

        // 버튼만이 아니라 드롭다운·토글도 "손가락으로 누르는 것"이다.
        // 메인 화면 덱 선택 칸(드롭다운)이 빠져 있어 옆 버튼에 가려졌다.
        var targets = new List<Selectable>();
        foreach (Selectable selectable in FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (selectable == null) continue;
            if (selectable.GetComponent<TouchTargetExempt>() != null) continue;   // 스스로 크기를 정하는 UI

            // 슬라이더·스크롤바는 손대면 오히려 망가진다
            if (selectable is Button || selectable is TMP_Dropdown || selectable is Dropdown || selectable is Toggle)
                targets.Add(selectable);
        }

        if (targets.Count == 0) return;

        var resized = new List<string>();
        var moved = new List<string>();
        var freeRects = new List<RectTransform>();   // 위치를 우리가 옮겨도 되는 것들

        // ─ 1단계: 크기 ─
        foreach (Selectable target in targets)
        {
            var rect = target.transform as RectTransform;
            if (rect == null) continue;

            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) continue;

            var canvasRect = canvas.rootCanvas.transform as RectTransform;
            if (canvasRect == null) continue;

            float minHeight = Mathf.Max(absoluteMinHeight, canvasRect.rect.height * minHeightRatio);
            float minWidth = minHeight * minAspect;

            // 드롭다운은 형제 버튼들에 견줘 너무 작지 않도록 하한을 더 올린다
            if (!(target is Button))
            {
                Vector2 peer = LargestSiblingButtonSize(rect);
                if (peer.y > 0f)
                {
                    minHeight = Mathf.Max(minHeight, peer.y * dropdownSizeRatio);
                    minWidth = Mathf.Max(minWidth, peer.x * dropdownSizeRatio);
                }
            }

            if (EnsureMinimumSize(rect, minWidth, minHeight))
                resized.Add(Path(target.transform));

            if (IsFreelyPositioned(rect)) freeRects.Add(rect);
        }

        // 크기를 바꿨으니 레이아웃을 한 번 확정시키고 겹침을 잰다
        Canvas.ForceUpdateCanvases();

        // ─ 2단계: 겹침 풀기 ─
        int separated = Separate(freeRects);

        // ─ 3단계: 화면 안으로 ─
        foreach (RectTransform rect in freeRects)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect == null) continue;

            if (PullInside(rect, canvasRect)) moved.Add(Path(rect));
        }

        if (resized.Count > 0)
            Debug.Log($"[TouchTarget] 크기 보정 {resized.Count}개 — {string.Join(", ", resized)}");

        if (separated > 0)
            Debug.Log($"[TouchTarget] 겹침 해소 {separated}쌍 — 버튼 사이를 최소 {minGap}만큼 벌렸다");

        if (moved.Count > 0)
            Debug.Log($"[TouchTarget] 화면 안으로 이동 {moved.Count}개 — {string.Join(", ", moved)}");
    }

    /// <summary>
    /// 견줄 버튼의 크기. 같은 부모를 먼저 보고, 없으면 같은 캔버스 전체에서 찾는다.
    /// (메인 화면처럼 드롭다운과 버튼의 부모가 다를 수 있다)
    /// </summary>
    private Vector2 LargestSiblingButtonSize(RectTransform rect)
    {
        Vector2 biggest = Vector2.zero;

        if (rect.parent != null)
        {
            foreach (Transform sibling in rect.parent)
            {
                if (sibling == rect) continue;
                if (sibling.GetComponent<Button>() == null) continue;

                var siblingRect = sibling as RectTransform;
                if (siblingRect == null) continue;

                biggest.x = Mathf.Max(biggest.x, siblingRect.rect.width);
                biggest.y = Mathf.Max(biggest.y, siblingRect.rect.height);
            }
        }

        if (biggest.y > 0f) return biggest;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return biggest;

        foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
        {
            var buttonRect = button.transform as RectTransform;
            if (buttonRect == null || buttonRect == rect) continue;

            biggest.x = Mathf.Max(biggest.x, buttonRect.rect.width);
            biggest.y = Mathf.Max(biggest.y, buttonRect.rect.height);
        }

        return biggest;
    }

    /// <summary>우리가 위치를 옮겨도 되는 버튼인가 (레이아웃·스트레치가 관리하지 않는 것).</summary>
    private static bool IsFreelyPositioned(RectTransform rect)
    {
        if (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x)) return false;
        if (!Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y)) return false;
        if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null) return false;

        return true;
    }

    /// <summary>
    /// 버튼·드롭다운이 서로 <see cref="minGap"/>만큼은 떨어지도록 밀어낸다.
    ///
    /// ★ 부모가 서로 다를 수 있으므로(메인 화면의 덱 드롭다운이 그렇다)
    ///   비교와 이동을 모두 <b>월드 좌표</b>로 한다. 부모가 같을 때만 비교하면
    ///   정작 겹친 것들을 놓친다.
    ///
    /// 한쪽만 버튼이면 버튼은 제자리에 두고 다른 쪽만 비킨다 —
    /// 원래 배치를 정한 건 버튼 쪽이고, 유저도 "덱 선택 칸을 옮기라"고 했다.
    /// </summary>
    private int Separate(List<RectTransform> rects)
    {
        int fixedPairs = 0;

        for (int pass = 0; pass < separationPasses; pass++)
        {
            bool touchedAny = false;

            for (int i = 0; i < rects.Count; i++)
            {
                for (int j = i + 1; j < rects.Count; j++)
                {
                    RectTransform a = rects[i];
                    RectTransform b = rects[j];
                    if (a == null || b == null) continue;

                    Rect ra = WorldRect(a);
                    Rect rb = WorldRect(b);

                    float scale = Mathf.Max(a.lossyScale.x, 0.0001f);
                    float gap = minGap * scale;

                    float needX = (ra.width + rb.width) * 0.5f + gap - Mathf.Abs(rb.center.x - ra.center.x);
                    float needY = (ra.height + rb.height) * 0.5f + gap - Mathf.Abs(rb.center.y - ra.center.y);

                    // 어느 축으로든 충분히 떨어져 있으면 겹치지 않은 것이다
                    if (needX <= 0f || needY <= 0f) continue;

                    bool aIsButton = a.GetComponent<Button>() != null;
                    bool bIsButton = b.GetComponent<Button>() != null;
                    float shareA = aIsButton && !bIsButton ? 0f : (!aIsButton && bIsButton ? 1f : 0.5f);
                    float shareB = 1f - shareA;

                    // 덜 밀어도 되는 축으로 민다 (원래 배치를 최대한 지킨다)
                    if (needX <= needY)
                    {
                        float dir = ResolveDir(a, b, rb.center.x - ra.center.x, shareA, shareB, horizontal: true);
                        a.position += new Vector3(-dir * needX * shareA, 0f, 0f);
                        b.position += new Vector3(dir * needX * shareB, 0f, 0f);
                    }
                    else
                    {
                        float dir = ResolveDir(a, b, rb.center.y - ra.center.y, shareA, shareB, horizontal: false);
                        a.position += new Vector3(0f, -dir * needY * shareA, 0f);
                        b.position += new Vector3(0f, dir * needY * shareB, 0f);
                    }

                    touchedAny = true;
                    if (pass == 0) fixedPairs++;
                }
            }

            if (!touchedAny) break;
        }

        return fixedPairs;
    }

    private static Rect WorldRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        return new Rect(
            corners[0].x, corners[0].y,
            corners[3].x - corners[0].x,
            corners[1].y - corners[0].y);
    }

    /// <summary>
    /// 밀 방향. 기본은 지금 벌어져 있는 쪽이지만,
    /// <b>한쪽만 움직이는 경우</b>에는 캔버스 안에서 여유가 더 많은 쪽으로 보낸다.
    /// 그래야 밀려난 쪽이 곧바로 화면 밖으로 나가지 않는다.
    /// </summary>
    private static float ResolveDir(
        RectTransform a, RectTransform b, float delta, float shareA, float shareB, bool horizontal)
    {
        float natural = delta >= 0f ? 1f : -1f;
        if (Mathf.Approximately(shareA, shareB)) return natural;

        RectTransform mover = shareA > shareB ? a : b;
        Canvas canvas = mover.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null) return natural;

        Rect bounds = WorldRect(canvasRect);
        Rect self = WorldRect(mover);

        float roomPositive = horizontal ? bounds.xMax - self.xMax : bounds.yMax - self.yMax;
        float roomNegative = horizontal ? self.xMin - bounds.xMin : self.yMin - bounds.yMin;

        float moverDir = shareA > shareB ? -natural : natural;   // mover가 실제로 가는 방향
        float wanted = roomPositive >= roomNegative ? 1f : -1f;

        return Mathf.Approximately(moverDir, wanted) ? natural : -natural;
    }

    /// <summary>최소 크기를 만족시킨다. 줄이지 않는다. 실제로 바꿨으면 true.</summary>
    private bool EnsureMinimumSize(RectTransform rect, float minWidth, float minHeight)
    {
        float width = rect.rect.width;
        float height = rect.rect.height;

        // 앵커로 늘어나는(stretch) 버튼은 크기를 부모가 정하므로 건드리지 않는다.
        bool stretchX = !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
        bool stretchY = !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);

        float wantHeight = Mathf.Max(height, minHeight);
        float wantWidth = Mathf.Max(width, minWidth, wantHeight * minAspect);

        bool changed = false;

        // LayoutGroup 아래라면 sizeDelta를 직접 만져도 되돌아간다. LayoutElement로 요청한다.
        if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
        {
            var element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();

            if (element.minHeight < wantHeight) { element.minHeight = wantHeight; changed = true; }
            if (element.minWidth < wantWidth) { element.minWidth = wantWidth; changed = true; }

            if (changed) LayoutRebuilder.MarkLayoutForRebuild(rect);
            return changed;
        }

        Vector2 size = rect.sizeDelta;
        if (!stretchY && wantHeight > height + 0.5f) { size.y += wantHeight - height; changed = true; }
        if (!stretchX && wantWidth > width + 0.5f) { size.x += wantWidth - width; changed = true; }

        if (changed) rect.sizeDelta = size;
        return changed;
    }

    /// <summary>
    /// 캔버스 밖으로 나간 버튼을 안쪽으로 끌어당긴다.
    /// 덱 편집 화면의 [메인으로] 버튼이 하단에 잘려 있던 것이 이 경우다.
    /// </summary>
    private bool PullInside(RectTransform rect, RectTransform canvasRect)
    {
        // stretch 버튼은 부모가 위치를 정하므로 손대지 않는다.
        if (!Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x)) return false;
        if (!Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y)) return false;
        if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null) return false;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector3[] bounds = new Vector3[4];
        canvasRect.GetWorldCorners(bounds);

        // corners/bounds: 0=좌하, 1=좌상, 2=우상, 3=우하
        float scale = canvasRect.lossyScale.x;
        if (Mathf.Approximately(scale, 0f)) return false;

        float padding = edgePadding * scale;
        float dx = 0f, dy = 0f;

        if (corners[0].x < bounds[0].x + padding) dx = (bounds[0].x + padding) - corners[0].x;
        else if (corners[3].x > bounds[3].x - padding) dx = (bounds[3].x - padding) - corners[3].x;

        if (corners[0].y < bounds[0].y + padding) dy = (bounds[0].y + padding) - corners[0].y;
        else if (corners[1].y > bounds[1].y - padding) dy = (bounds[1].y - padding) - corners[1].y;

        if (Mathf.Abs(dx) < 0.5f && Mathf.Abs(dy) < 0.5f) return false;

        rect.position += new Vector3(dx, dy, 0f);
        return true;
    }

    /// <summary>로그에서 어느 버튼인지 알아볼 수 있게 부모까지 붙인 이름.</summary>
    private static string Path(Transform t)
    {
        return t.parent != null ? $"{t.parent.name}/{t.name}" : t.name;
    }
}
