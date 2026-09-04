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
// ── 터치에는 호버가 없다 ──────────────────────────────────────────
//   손가락은 화면 위를 떠다니지 못한다. 그래서 휴대폰에서는 위 방식만으로 <b>아예 안 뜬다.</b>
//   <b>탭 경로</b>를 하나 더 둔다 — 존을 누르면 말풍선이 뜨고 몇 초 뒤 스스로 닫힌다.
//
//   마우스가 달린 기기에서는 호버가 계속 우선한다(커서를 치우면 곧바로 닫힌다).
//   그 구분은 <c>Input.mousePresent</c>로 한다. 터치만 되는 기기에서는 이 값이 false다.
//   ★ 이 구분이 필요한 이유: 터치 기기에서도 <c>Input.mousePosition</c>은 값을 돌려주는데,
//     손을 뗀 뒤에도 <b>마지막으로 닿은 자리에 그대로 멈춰 있다.</b>
//     호버로 착각하면 말풍선이 영영 안 닫힌다.
//
// ── 화면은 프리팹이 갖는다 ────────────────────────────────────────
//   Resources/Build/ZoneCountTooltipRoot
using System.Collections;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class ZoneCountHoverUI : MonoBehaviour
{
    public static ZoneCountHoverUI Instance { get; private set; }

    /// <summary>Resources 아래에서 말풍선을 찾을 경로.</summary>
    private const string TooltipResourcePath = "Build/ZoneCountTooltipRoot";

    [Tooltip("말풍선을 존 위쪽으로 얼마나 띄울지(캔버스 단위).")]
    public float verticalOffset = 90f;

    [Tooltip("탭으로 띄웠을 때 몇 초 뒤에 스스로 닫힐지. 마우스 호버에는 쓰이지 않는다.")]
    public float tapHoldSeconds = 2f;

    private Canvas _hostCanvas;
    private GameObject _root;
    private ZoneCountTooltipView _view;
    private bool _ownsRoot;

    private Player _p1;
    private Player _p2;

    /// <summary>지금 보여 주고 있는 문구. 같으면 다시 쓰지 않는다(매 프레임 갱신이므로).</summary>
    private string _shownText;

    /// <summary>포커스 없음을 이미 알렸는가. 매 프레임 찍지 않으려고 둔다.</summary>
    private bool _focusWarned;

    /// <summary>탭으로 띄운 말풍선이 닫힐 시각. 0이면 탭으로 띄운 것이 아니다.</summary>
    private float _tapExpireAt;

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

    private void HandleGameEnd(Player winner)
    {
        _tapExpireAt = 0f;
        Hide();
    }

    // ─── 매 프레임 판정 ─────────────────────────────────────────────────

    private void Update()
    {
        if (_p1 == null || _p2 == null) return;

        EnsureUI();
        if (_view == null || _view.panel == null) return;

        Camera cam = ResolveEventCamera();
        bool hasMouse = Input.mousePresent;

        // ─ 탭 경로 ─ 누른 순간 그 자리가 존이면 띄우고, 정해진 시간 뒤에 스스로 닫는다.
        if (TryGetPressPoint(out Vector2 press))
        {
            if (TryShowForZone(press, cam))
            {
                _tapExpireAt = Time.unscaledTime + Mathf.Max(0.2f, tapHoldSeconds);
                return;
            }

            // 존 밖을 눌렀다면 띄워 둔 것을 곧바로 거둔다.
            _tapExpireAt = 0f;
        }

        if (_tapExpireAt > 0f && Time.unscaledTime >= _tapExpireAt) _tapExpireAt = 0f;

        // ─ 호버 경로 ─ 마우스가 달린 기기에서만 뜻이 있다.
        if (hasMouse)
        {
            // ★ 포커스가 없으면 입력 모듈이 멈춰 커서 좌표가 낡는다(손패 호버와 같은 이유).
            //   조용히 안 뜨면 원인을 찾기 어려우므로 한 번만 알린다.
            if (EventSystem.current != null && !EventSystem.current.isFocused)
            {
                if (!_focusWarned)
                {
                    _focusWarned = true;
                    Debug.Log("[ZoneCountHover] 게임 화면에 포커스가 없어 커서 판정이 멈춰 있습니다. 화면을 한 번 클릭하세요.");
                }

                Hide();
                _tapExpireAt = 0f;
                return;
            }

            _focusWarned = false;

            // 커서가 존 위에 있으면 호버가 이긴다 — 탭으로 띄운 것보다 최신이다.
            if (TryShowForZone(Input.mousePosition, cam))
            {
                _tapExpireAt = 0f;
                return;
            }
        }

        // 탭으로 띄운 것이 아직 살아 있으면 그대로 둔다.
        if (_tapExpireAt > 0f) return;

        Hide();
    }

    /// <summary>
    /// 이번 프레임에 새로 눌렸는가.
    /// 덱 편집 화면의 용병 선택 창도 같은 판정을 쓰게 되어 <see cref="PointerInput"/>으로 옮겼다.
    /// </summary>
    private static bool TryGetPressPoint(out Vector2 point) => PointerInput.TryGetPressPoint(out point);

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
