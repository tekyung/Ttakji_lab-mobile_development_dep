// ZoneCountHoverUI.cs — 덱·폐기존에 커서를 올리면 남은 장수를 말풍선으로 보여 준다.
//
// 덱과 폐기존은 카드가 겹쳐 쌓여 있어 몇 장인지 눈으로 셀 수 없다.
// 클릭은 이미 다른 뜻이 있으므로(폐기존은 목록 열기), 커서를 올리기만 하면 뜨게 한다.
//
// ── 판정 방식 ──────────────────────────────────────────────────────
//   커서 좌표가 존 Rect 안에 있는지 매 프레임 확인한다.
//   CardZoomPopupUI의 손패 호버와 같은 방식이다 — 이 보드는 카드마다 중첩 Canvas가 붙고
//   존마다 레이캐스트를 껐다 켜서, 포인터 이벤트만으로는 존까지 오지 않는 경우가 있다.
//
//   ⚠️ 알려진 한계: 에디터에서 Game 뷰에 포커스가 없으면 Input.mousePosition이 갱신되지 않는다.
//      손패 호버 미리보기가 겪는 것과 같은 문제다(HANDOFF 섹션 5). 빌드에서는 정상이다.
//
// ── 화면은 프리팹이 갖는다 ────────────────────────────────────────
//   Resources/Build/ZoneCountTooltipRoot
using System.Collections;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ZoneCountHoverUI : MonoBehaviour
{
    public static ZoneCountHoverUI Instance { get; private set; }

    /// <summary>Resources 아래에서 말풍선을 찾을 경로.</summary>
    private const string TooltipResourcePath = "Build/ZoneCountTooltipRoot";

    [Tooltip("말풍선을 존 위쪽으로 얼마나 띄울지(캔버스 단위).")]
    public float verticalOffset = 90f;

    private Canvas _hostCanvas;
    private GameObject _root;
    private ZoneCountTooltipView _view;
    private bool _ownsRoot;

    private Player _p1;
    private Player _p2;

    /// <summary>지금 보여 주고 있는 문구. 같으면 다시 쓰지 않는다(매 프레임 갱신이므로).</summary>
    private string _shownText;

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("ZoneCountHoverUI");
        Instance = go.AddComponent<ZoneCountHoverUI>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnGameStart += HandleGameStart;
        EventManager.OnGameSet += HandleGameEnd;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        StartCoroutine(AdoptSceneRootSoon());
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _p1 = null;
        _p2 = null;
        StartCoroutine(AdoptSceneRootSoon());
    }

    private IEnumerator AdoptSceneRootSoon()
    {
        yield return null;
        AdoptSceneRootIfPresent();
    }

    private void HandleGameStart(Player p1, Player p2)
    {
        _p1 = p1;
        _p2 = p2;
        EnsureUI();
        Hide();
    }

    private void HandleGameEnd(Player winner) => Hide();

    // ─── 매 프레임 판정 ─────────────────────────────────────────────────

    private void Update()
    {
        if (_p1 == null || _p2 == null) return;

        EnsureUI();
        if (_view == null || _view.panel == null) return;

        Vector2 pointer = Input.mousePosition;
        Camera cam = ResolveEventCamera();

        if (TryShowForZone(pointer, cam)) return;

        Hide();
    }

    /// <summary>커서 아래에 덱·폐기존이 있으면 말풍선을 띄운다.</summary>
    private bool TryShowForZone(Vector2 pointer, Camera cam)
    {
        Player mine = LocalPlayerContext.ResolveMine(_p1, _p2) ?? _p1;
        Player foe = ReferenceEquals(mine, _p1) ? _p2 : _p1;

        var ui = PlayerUIManager.Instance;
        if (ui != null)
        {
            if (Show(ui.myDeckTransform, pointer, cam, "메인덱", mine?.Deck?.Count)) return true;
            if (Show(ui.myGraveyardTransform, pointer, cam, "폐기존", mine?.Graveyard?.Count)) return true;
        }

        var enemy = FindFirstObjectByType<EnemyVisualTester>();
        if (enemy != null)
        {
            if (Show(enemy.enemyDeckTransform, pointer, cam, "상대 메인덱", foe?.Deck?.Count)) return true;
            if (Show(enemy.enemyGraveyardTransform, pointer, cam, "상대 폐기존", foe?.Graveyard?.Count)) return true;
        }

        return false;
    }

    private bool Show(Transform zone, Vector2 pointer, Camera cam, string label, int? count)
    {
        if (count == null) return false;
        if (!ContainsPoint(zone, pointer, cam)) return false;

        string text = $"{label} {count}장";
        if (_shownText != text)
        {
            _view.label.text = text;
            _shownText = text;
        }

        PlaceAbove(zone);
        if (!_view.panel.gameObject.activeSelf) _view.panel.gameObject.SetActive(true);
        return true;
    }

    private void Hide()
    {
        if (_view == null || _view.panel == null) return;

        if (_view.panel.gameObject.activeSelf) _view.panel.gameObject.SetActive(false);
        _shownText = null;
    }

    /// <summary>말풍선을 그 존 바로 위에 놓는다. 캔버스가 달라도 좌표가 맞도록 월드를 거친다.</summary>
    private void PlaceAbove(Transform zone)
    {
        var zoneRect = zone as RectTransform;
        if (zoneRect == null) return;

        _view.panel.position = zoneRect.position;
        _view.panel.anchoredPosition += new Vector2(0f, verticalOffset);
    }

    private static bool ContainsPoint(Transform t, Vector2 screenPos, Camera cam)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;

        var rect = t as RectTransform;
        return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, cam);
    }

    private Camera ResolveEventCamera()
    {
        if (_hostCanvas == null) return null;
        return _hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _hostCanvas.worldCamera;
    }

    // ─── 화면 마련 ──────────────────────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null) return;

        if (_view != null && _hostCanvas == canvas) return;

        // ★ 우리가 찍은 것만 파괴한다. 씬에 놓인 것을 지우면 기획자의 작업이 사라진다.
        if (_root != null && _ownsRoot) Destroy(_root);
        _root = null;
        _view = null;
        _ownsRoot = false;
        _shownText = null;

        _hostCanvas = canvas;

        if (AdoptSceneRootIfPresent()) return;
        BuildFromPrefab(canvas);
    }

    private bool AdoptSceneRootIfPresent()
    {
        if (_ownsRoot && _view != null) return true;

        foreach (ZoneCountTooltipView candidate in FindObjectsByType<ZoneCountTooltipView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;
            if (candidate == _view) return true;

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning($"[ZoneCountHover] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뜁니다 — {reason}");
                continue;
            }

            Canvas canvas = candidate.GetComponentInParent<Canvas>();

            _root = candidate.gameObject;
            _view = candidate;
            _ownsRoot = false;
            _hostCanvas = canvas != null ? (canvas.rootCanvas != null ? canvas.rootCanvas : canvas) : null;

            Hide();
            return true;
        }

        return false;
    }

    private void BuildFromPrefab(Canvas canvas)
    {
        GameObject prefab = Resources.Load<GameObject>(TooltipResourcePath);
        if (prefab == null)
        {
            Debug.LogError(
                $"[ZoneCountHover] 프리팹을 찾지 못했습니다: Resources/{TooltipResourcePath}. 장수 표시가 뜨지 않습니다.");
            return;
        }

        _root = Instantiate(prefab, canvas.transform);
        _root.name = prefab.name;

        _view = _root.GetComponent<ZoneCountTooltipView>()
                ?? _root.GetComponentInChildren<ZoneCountTooltipView>(true);

        if (_view == null)
        {
            Debug.LogError($"[ZoneCountHover] '{prefab.name}'에 ZoneCountTooltipView가 없습니다.");
            Destroy(_root);
            _root = null;
            return;
        }

        if (!_view.Validate(out string reason))
        {
            Debug.LogError($"[ZoneCountHover] 프리팹 참조가 온전하지 않습니다 — {reason}");
            Destroy(_root);
            _root = null;
            _view = null;
            return;
        }

        _ownsRoot = true;
        Hide();
    }

    private Canvas ResolveCanvas()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myHandTransform != null)
        {
            var c = PlayerUIManager.Instance.myHandTransform.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }

        return FindFirstObjectByType<Canvas>();
    }
}
