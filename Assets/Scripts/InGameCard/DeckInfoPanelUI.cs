// DeckInfoPanelUI.cs — 좌측 상단 톱니바퀴 → 덱 리스트 + 항복 패널
//
//   · 화면 좌측 상단에 톱니바퀴 버튼. 누르면 패널이 열린다 (다시 누르면 닫힘)
//   · 패널 안에 내 덱 20장을 5 × 4 격자로 표시. 덱에 남아 있지 않은 카드는 흐리게 처리한다
//   · 격자 아래, 패널 우측에 [항복] 버튼. 오조작 방지를 위해 한 번 더 확인을 받는다
//
// 덱 구성은 OnGameStart 시점에 스냅샷한다. 이 이벤트는 엔진의 시작 드로우보다 먼저 발행되므로
// (InitializeSingleGame: OnGameStart → CharacterFieldBroadcast → DrawCards 순서)
// 그 시점의 player.Deck이 곧 온전한 20장이다. 엔진에 별도 API를 뚫지 않아도 된다.
//
// 씬 배선 불필요 — RuntimeInitializeOnLoadMethod로 자가 생성하고 UI도 코드로 만든다.
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckInfoPanelUI : MonoBehaviour
{
    public static DeckInfoPanelUI Instance { get; private set; }

    [Header("Grid")]
    public int columns = 5;
    public int rows = 4;
    public Vector2 cellSize = new Vector2(112f, 157f);
    public Vector2 cellSpacing = new Vector2(10f, 10f);
    [Tooltip("격자 칸에 맞춰 카드 프리팹을 축소하는 배율. 카드 원본은 200x280이다.")]
    public float cardScale = 0.56f;

    [Header("Panel")]
    public Color panelColor = new Color(0.10f, 0.10f, 0.13f, 0.95f);
    public Color gearColor = new Color(0.18f, 0.18f, 0.22f, 0.92f);
    [Range(0f, 1f)] public float usedCardAlpha = 0.28f;

    [Header("Surrender")]
    public Color surrenderColor = new Color(0.80f, 0.25f, 0.25f, 1f);
    public Color surrenderConfirmColor = new Color(0.95f, 0.35f, 0.20f, 1f);

    private Canvas _hostCanvas;
    private TMP_FontAsset _font;

    private Button _gearButton;
    private GameObject _panel;
    private RectTransform _grid;
    private TextMeshProUGUI _titleText;
    private Button _surrenderButton;
    private TextMeshProUGUI _surrenderLabel;

    private Player _human;
    private readonly List<Card> _deckSnapshot = new List<Card>();
    private readonly List<GameObject> _cells = new List<GameObject>();
    private bool _surrenderArmed;

    // ─── 부트스트랩 ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("DeckInfoPanelUI");
        Instance = go.AddComponent<DeckInfoPanelUI>();
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
    }

    private void OnDisable()
    {
        EventManager.OnGameStart -= HandleGameStart;
        EventManager.OnGameSet -= HandleGameEnd;
    }

    // ─── 게임 흐름 ──────────────────────────────────────────────────────

    private void HandleGameStart(Player p1, Player p2)
    {
        _human = PlayerUIManager.ResolveHumanPlayer(p1, p2);

        // 시작 드로우 이전이라 이 시점의 Deck이 온전한 덱 구성이다
        _deckSnapshot.Clear();
        if (_human != null) _deckSnapshot.AddRange(_human.Deck);

        EnsureUI();
        SetGearVisible(_human != null);
        ClosePanel();
        WireLegacySurrenderButton();
    }

    /// <summary>
    /// 씬에 남아 있던 기존 [Surrender] 버튼을 이 패널에 연결한다.
    /// 코드 참조가 없어 눌러도 아무 일도 일어나지 않던 죽은 버튼이었다.
    /// 항복 확인 절차를 한 곳으로 모으기 위해, 바로 항복시키지 않고 이 패널을 연다.
    /// </summary>
    private void WireLegacySurrenderButton()
    {
        GameObject go = GameObject.Find("Surrender");
        if (go == null) return;

        var button = go.GetComponent<Button>();
        if (button == null || button == _legacySurrenderButton) return;

        // 씬을 다시 로드하면 버튼 인스턴스가 새로 생기므로, 참조가 바뀔 때마다 다시 연결한다.
        // (이 컴포넌트는 DontDestroyOnLoad라 bool 플래그로 막으면 재로드 후 연결되지 않는다)
        button.onClick.RemoveListener(OpenPanel);
        button.onClick.AddListener(OpenPanel);
        _legacySurrenderButton = button;
    }

    private Button _legacySurrenderButton;

    private void HandleGameEnd(Player winner)
    {
        ClosePanel();
        SetGearVisible(false);
    }

    private void SetGearVisible(bool visible)
    {
        if (_gearButton != null) _gearButton.gameObject.SetActive(visible);
    }

    // ─── 패널 열기 / 닫기 ───────────────────────────────────────────────

    private void TogglePanel()
    {
        if (_panel == null) return;

        if (_panel.activeSelf) ClosePanel();
        else OpenPanel();
    }

    private void OpenPanel()
    {
        EnsureUI();
        if (_panel == null) return;

        DisarmSurrender();
        RebuildGrid();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    private void ClosePanel()
    {
        DisarmSurrender();
        if (_panel != null) _panel.SetActive(false);
    }

    // ─── 덱 격자 ────────────────────────────────────────────────────────

    private void RebuildGrid()
    {
        foreach (var go in _cells) if (go != null) Destroy(go);
        _cells.Clear();

        if (_human == null) return;

        // 덱에 아직 남아 있는 수를 카드 ID별로 센다 (같은 카드가 2장이므로 개수 기반으로 판정)
        var remaining = new Dictionary<string, int>();
        foreach (var card in _human.Deck)
        {
            if (card == null) continue;
            remaining.TryGetValue(card.Id, out int n);
            remaining[card.Id] = n + 1;
        }

        int shown = 0;
        foreach (var card in _deckSnapshot)
        {
            if (card == null) continue;

            bool stillInDeck = false;
            if (remaining.TryGetValue(card.Id, out int left) && left > 0)
            {
                remaining[card.Id] = left - 1;
                stillInDeck = true;
            }

            _cells.Add(CreateCell(card, stillInDeck));
            shown++;
        }

        if (_titleText != null)
            _titleText.text = $"내 덱  ({_human.Deck.Count} / {shown}장 남음)";
    }

    private GameObject CreateCell(Card card, bool stillInDeck)
    {
        var cellGo = new GameObject($"Cell_{card.Id}", typeof(RectTransform), typeof(CanvasGroup));
        var cellRect = (RectTransform)cellGo.transform;
        cellRect.SetParent(_grid, false);
        cellRect.sizeDelta = cellSize;

        var group = cellGo.GetComponent<CanvasGroup>();
        group.alpha = stillInDeck ? 1f : usedCardAlpha;   // 덱에 없는 카드는 흐리게
        group.blocksRaycasts = false;

        GameObject prefab = ResolveCardPrefab();
        if (prefab == null) return cellGo;

        GameObject cardGo = Instantiate(prefab, cellRect);
        var ui = cardGo.GetComponent<CardUI>();
        if (ui != null)
        {
            ui.BindEngineCard(card);
            ui.SetFaceDown(false);
        }

        var interaction = cardGo.GetComponent<CardInteraction>();
        if (interaction != null) interaction.enabled = false;

        // 카드 내부는 자식들이 고정 크기라 sizeDelta로는 줄어들지 않는다. localScale로 축소한다.
        var cardRect = cardGo.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.localScale = Vector3.one * cardScale;
        }

        foreach (var g in cardGo.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        return cellGo;
    }

    private static GameObject ResolveCardPrefab()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myCardPrefab != null)
            return PlayerUIManager.Instance.myCardPrefab;

        return Resources.Load<GameObject>("Build/CardSlotInGame");
    }

    // ─── 항복 ───────────────────────────────────────────────────────────

    private void OnSurrenderClicked()
    {
        if (!_surrenderArmed)
        {
            // 1차 클릭: 확인 요청 (오조작 방지)
            _surrenderArmed = true;
            _surrenderLabel.text = "정말 항복?";
            var img = _surrenderButton.GetComponent<Image>();
            if (img != null) img.color = surrenderConfirmColor;
            return;
        }

        // 2차 클릭: 실제 항복
        ClosePanel();

        if (BattleManager.Instance != null && _human != null)
            BattleManager.Instance.SurrenderBy(_human);
    }

    private void DisarmSurrender()
    {
        _surrenderArmed = false;
        if (_surrenderLabel != null) _surrenderLabel.text = "항복";
        if (_surrenderButton != null)
        {
            var img = _surrenderButton.GetComponent<Image>();
            if (img != null) img.color = surrenderColor;
        }
    }

    // ─── UI 생성 ────────────────────────────────────────────────────────

    private void EnsureUI()
    {
        Canvas canvas = ResolveCanvas();
        if (canvas == null) return;
        if (_panel != null && _hostCanvas == canvas) return;

        if (_panel != null) Destroy(_panel);
        if (_gearButton != null) Destroy(_gearButton.gameObject);

        _hostCanvas = canvas;
        _font = UiFontResolver.Resolve();
        Build(canvas);
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

    private void Build(Canvas canvas)
    {
        float gridW = columns * cellSize.x + (columns - 1) * cellSpacing.x;
        float gridH = rows * cellSize.y + (rows - 1) * cellSpacing.y;
        const float pad = 20f;
        const float headerH = 44f;
        const float footerH = 64f;

        // ── 좌측 상단 톱니바퀴 버튼 ──
        var gearGo = new GameObject("GearButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Canvas), typeof(GraphicRaycaster));
        var gearRect = (RectTransform)gearGo.transform;
        gearRect.SetParent(canvas.transform, false);
        gearRect.anchorMin = new Vector2(0f, 1f);
        gearRect.anchorMax = new Vector2(0f, 1f);
        gearRect.pivot = new Vector2(0f, 1f);
        gearRect.sizeDelta = new Vector2(52f, 52f);
        gearRect.anchoredPosition = new Vector2(12f, -12f);
        gearGo.GetComponent<Image>().color = gearColor;

        var gearCanvas = gearGo.GetComponent<Canvas>();
        gearCanvas.overrideSorting = true;
        gearCanvas.sortingOrder = 600;

        var gearIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var gearIconRect = (RectTransform)gearIcon.transform;
        gearIconRect.SetParent(gearRect, false);
        gearIconRect.anchorMin = new Vector2(0.5f, 0.5f);
        gearIconRect.anchorMax = new Vector2(0.5f, 0.5f);
        gearIconRect.pivot = new Vector2(0.5f, 0.5f);
        gearIconRect.sizeDelta = new Vector2(32f, 32f);
        var gearImg = gearIcon.GetComponent<Image>();
        gearImg.sprite = GetGearSprite();
        gearImg.raycastTarget = false;

        _gearButton = gearGo.GetComponent<Button>();
        _gearButton.onClick.AddListener(TogglePanel);
        gearGo.SetActive(false);

        // ── 패널 ──
        _panel = new GameObject("DeckInfoPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
        var panelRect = (RectTransform)_panel.transform;
        panelRect.SetParent(canvas.transform, false);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(gridW + pad * 2f, gridH + headerH + footerH + pad * 2f);
        panelRect.anchoredPosition = Vector2.zero;
        _panel.GetComponent<Image>().color = panelColor;

        var panelCanvas = _panel.GetComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 700; // 선택 다이얼로그(500)보다 위, 결과 오버레이(900)보다 아래

        // 헤더
        _titleText = CreateText(panelRect, "내 덱", 24f, TextAlignmentOptions.MidlineLeft);
        var titleRect = _titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-pad * 2f, headerH);
        titleRect.anchoredPosition = new Vector2(0f, -pad * 0.5f);

        // 닫기 버튼 (헤더 우측)
        TextMeshProUGUI closeLabel;
        Button closeButton = CreateButton(panelRect, "CloseButton", "닫기", new Color(0.32f, 0.32f, 0.38f, 1f), out closeLabel);
        var closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(96f, 38f);
        closeRect.anchoredPosition = new Vector2(-pad, -pad * 0.5f);
        closeButton.onClick.AddListener(ClosePanel);

        // 5 × 4 격자
        var gridGo = new GameObject("DeckGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        _grid = (RectTransform)gridGo.transform;
        _grid.SetParent(panelRect, false);
        _grid.anchorMin = new Vector2(0.5f, 1f);
        _grid.anchorMax = new Vector2(0.5f, 1f);
        _grid.pivot = new Vector2(0.5f, 1f);
        _grid.sizeDelta = new Vector2(gridW, gridH);
        _grid.anchoredPosition = new Vector2(0f, -(headerH + pad * 0.5f));

        var glg = gridGo.GetComponent<GridLayoutGroup>();
        glg.cellSize = cellSize;
        glg.spacing = cellSpacing;
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = columns;
        glg.childAlignment = TextAnchor.UpperCenter;

        // 항복 버튼 (격자 아래, 패널 우측)
        _surrenderButton = CreateButton(panelRect, "SurrenderButton", "항복", surrenderColor, out _surrenderLabel);
        var surRect = _surrenderButton.GetComponent<RectTransform>();
        surRect.anchorMin = new Vector2(1f, 0f);
        surRect.anchorMax = new Vector2(1f, 0f);
        surRect.pivot = new Vector2(1f, 0f);
        surRect.sizeDelta = new Vector2(160f, 46f);
        surRect.anchoredPosition = new Vector2(-pad, pad * 0.6f);
        _surrenderButton.onClick.AddListener(OnSurrenderClicked);

        _panel.SetActive(false);
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color, out TextMeshProUGUI labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        labelText = CreateText(go.transform, label, 22f, TextAlignmentOptions.Center);
        var r = labelText.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    // ─── 톱니바퀴 아이콘 ────────────────────────────────────────────────
    // ⚙(U+2699)는 프로젝트 한글 폰트(정적 아틀라스)에 글리프가 없어 ㅁ로 깨진다.
    // 문자 대신 코드로 그린다.

    private static Sprite _gearSprite;

    private static Sprite GetGearSprite()
    {
        if (_gearSprite != null) return _gearSprite;

        const int size = 64;
        const int teeth = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float c = (size - 1) * 0.5f;
        float bodyR = size * 0.30f;   // 톱니 뿌리 반지름
        float toothR = size * 0.44f;  // 톱니 끝 반지름
        float holeR = size * 0.13f;   // 가운데 구멍
        Color on = Color.white;
        Color off = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 각도에 따라 톱니 구간에서는 반지름을 늘린다
                float angle = Mathf.Atan2(dy, dx);
                float wave = Mathf.Cos(angle * teeth);
                float radius = wave > 0.35f ? toothR : bodyR;

                bool inside = dist <= radius && dist >= holeR;
                tex.SetPixel(x, y, inside ? on : off);
            }
        }

        tex.Apply();
        _gearSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return _gearSprite;
    }
}
