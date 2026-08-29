// LocalMatchStarter.cs — 로컬(오프라인) 대전을 시작한다. TestGameScene 전용이다.
//
// 두 갈래로 시작할 수 있다:
//   · 설정 화면 — 실행하면 창이 뜨고 양쪽 덱·용병·사람/봇을 고른 뒤 [시작]  (기본)
//   · 바로 시작 — useSetupPanel을 끄면 예전처럼 아래 하드코딩 조합으로 곧장 시작한다
//
// ★ 하드코딩 덱은 지우지 않는다. 회귀 기준선이자 설정 화면의 `(기본 테스트 덱)` 항목이다.
//   저장된 덱이 하나도 없어도 대전이 시작돼야 한다.
using System.Collections;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum LocalMatchMode
{
    BotVsBot,
    HumanVsBot
}

[DefaultExecutionOrder(100)]
public class LocalMatchStarter : MonoBehaviour
{
    [SerializeField] private LocalMatchMode mode = LocalMatchMode.BotVsBot;
    [SerializeField] private bool autoStart = true;

    [Header("설정 화면")]
    [Tooltip("켜면 실행 시 설정 창이 뜨고, 거기서 고른 조합으로 대전을 시작한다. " +
             "끄면 아래 하드코딩 조합으로 곧장 시작한다(예전 동작).")]
    [SerializeField] private bool useSetupPanel = true;

    [Header("BotVsBot Spectator")]
    [SerializeField] private bool revealAllHandsInBotVsBot = true;
    [SerializeField] private EnemyVisualTester enemyVisualTester;

    private bool _started;

    /// <summary>Resources 아래에서 설정 화면을 찾을 경로.</summary>
    private const string SetupResourcePath = "Build/MatchSetupRoot";

    /// <summary>덱 드롭다운의 첫 항목. 아래 하드코딩 조합을 뜻한다.</summary>
    private const string DefaultDeckOption = "(기본 테스트 덱)";

    private static readonly string[] P1UniqueCardIds =
    {
        "ELLI-02", "ELLI-03", "ELLI-04", "ELLI-05", "ELLI-06", "ELLI-07",
        "DAIN-02", "DAIN-03", "DAIN-07", "DAIN-09"
    };

    private static readonly string[] P2UniqueCardIds =
    {
        "VERO-02", "VERO-03", "VERO-05", "VERO-07", "VERO-11",
        "SONI-02", "SONI-03", "SONI-05", "SONI-06", "SONI-07"
    };

    /// <summary>기본 테스트 덱이 쓰는 용병 (characterId). 설정 화면의 첫 항목이 이 값을 채운다.</summary>
    private static readonly string[] P1DefaultCharacters = { "ELLIE", "DAINA" };
    private static readonly string[] P2DefaultCharacters = { "VERONICA", "SONIA" };

    /// <summary>
    /// 매칭을 거쳐 들어온 온라인 대전인지. `session_manage`가 매칭 성사 시 `GameData`를 채운다.
    /// </summary>
    public static bool IsOnlineSessionActive => !string.IsNullOrEmpty(GameData.SessionCode);

    private void Awake()
    {
        // ★ 온라인 대전으로 진입한 경우에는 로컬 봇전이 절대 끼어들면 안 된다.
        //   이 가드가 없으면 매칭이 성사돼 이 씬으로 넘어왔을 때 아래에서 session_game_manage를 꺼 버려
        //   온라인 세션이 죽고 봇전이 자동 시작된다. (온라인 준비는 OnlineMatchStarter가 맡는다)
        if (IsOnlineSessionActive)
        {
            enabled = false;
            Debug.Log($"[LocalMatchStarter] 온라인 세션({GameData.SessionCode}) 진행 중 — 로컬 봇전을 시작하지 않는다.");
            return;
        }

        session_game_manage sessionGameManage = FindFirstObjectByType<session_game_manage>();
        if (sessionGameManage != null)
        {
            sessionGameManage.enabled = false;
            Debug.Log("[LocalMatchStarter] session_game_manage disabled for local play.");
        }
    }

    private void Start()
    {
        if (IsOnlineSessionActive) return;

        if (useSetupPanel)
        {
            StartCoroutine(ShowSetupSoon());
            return;
        }

        if (autoStart)
            StartCoroutine(StartMatchAfterBattleManagerReady());
    }

    // ─── 바로 시작 (예전 동작) ──────────────────────────────────────────

    private IEnumerator StartMatchAfterBattleManagerReady()
    {
        yield return null;

        if (_started) yield break;
        if (!EnsureBattleManager()) yield break;

        StartMatchWith(
            mode,
            BuildDefaultSetup(isFirstSide: true, matchMode: mode),
            BuildDefaultSetup(isFirstSide: false, matchMode: mode));
    }

    private bool EnsureBattleManager()
    {
        if (BattleManager.Instance != null) return true;

        Debug.LogError("[LocalMatchStarter] BattleManager.Instance is null. Attach BattleManager to the scene.");
        return false;
    }

    /// <summary>고른 조합으로 실제 매치를 연다. 두 갈래가 모두 여기로 모인다.</summary>
    private void StartMatchWith(LocalMatchMode matchMode, PlayerSetupData p1Setup, PlayerSetupData p2Setup)
    {
        if (_started) return;
        if (!EnsureBattleManager()) return;

        _started = true;

        // 로컬에서는 사람이 얼마든지 생각할 수 있어야 한다.
        // 정적 값이라 앞선 온라인 매치(또는 도메인 리로드를 끈 에디터의 이전 플레이)의 설정이 남을 수 있어 명시적으로 되돌린다.
        TCG_Project.Scripts.Systems.GameLogicHelpers.AllowUnlimitedHumanInput = true;

        EnemyVisualTester visualTester = enemyVisualTester != null
            ? enemyVisualTester
            : FindFirstObjectByType<EnemyVisualTester>();

        if (visualTester != null)
        {
            visualTester.ConfigureBotVsBotSpectator(
                matchMode == LocalMatchMode.BotVsBot && revealAllHandsInBotVsBot);
        }

        Debug.Log($"[LocalMatchStarter] Starting local match ({matchMode}). " +
                  $"1P={Describe(p1Setup)} / 2P={Describe(p2Setup)}");

        BattleManager.Instance.StartMatch(p1Setup, p2Setup);
    }

    private static string Describe(PlayerSetupData setup)
        => $"{setup.PlayerName}({setup.Type}) {setup.DeckCardIds?.Count ?? 0}장 " +
           $"[{setup.MainCharacterId}/{setup.SubCharacterId}]";

    // ─── 설정 화면 ──────────────────────────────────────────────────────

    private GameObject _setupRoot;
    private MatchSetupView _setupView;

    /// <summary>용병 드롭다운의 항목 순서와 같은 characterId 목록.</summary>
    private readonly List<string> _characterIds = new List<string>();

    /// <summary>
    /// 한 프레임 기다렸다가 설정 화면을 연다.
    /// 씬 로드 직후에는 캔버스·레이아웃이 아직 자리를 잡지 않았고,
    /// CardDataManager가 용병 목록을 읽기 전일 수도 있다.
    /// </summary>
    private IEnumerator ShowSetupSoon()
    {
        yield return null;

        if (!EnsureSetupUI())
        {
            // 화면을 마련하지 못하면 대전이 아예 시작되지 않는다. 예전 방식으로라도 진행한다.
            Debug.LogWarning("[LocalMatchStarter] 설정 화면을 열지 못해 기본 조합으로 시작한다.");
            StartCoroutine(StartMatchAfterBattleManagerReady());
            yield break;
        }

        PopulateSetup();
        _setupView.panel.SetActive(true);
    }

    private bool EnsureSetupUI()
    {
        if (_setupView != null) return true;

        // ① 씬에 이미 놓여 있으면 그것을 쓴다 (중복 방지)
        if (AdoptSceneSetupIfPresent()) return true;

        // ② 없으면 프리팹을 찍는다
        return BuildSetupFromPrefab();
    }

    private bool AdoptSceneSetupIfPresent()
    {
        foreach (MatchSetupView candidate in FindObjectsByType<MatchSetupView>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate == null) continue;

            if (!candidate.Validate(out string reason))
            {
                Debug.LogWarning($"[LocalMatchStarter] 씬의 '{candidate.name}'은 참조가 온전하지 않아 건너뛴다 — {reason}");
                continue;
            }

            // ★ 꺼진 부모 아래에 있으면 무슨 짓을 해도 화면에 나오지 않는다.
            //   이 화면이 안 뜨면 대전을 시작할 수가 없다.
            if (FindInactiveAncestor(candidate.transform) is Transform blocker)
            {
                Debug.LogError(
                    $"[LocalMatchStarter] 씬의 '{candidate.name}'은 꺼져 있는 '{blocker.name}' 아래에 있어 화면에 뜰 수 없다. " +
                    "캔버스 바로 아래로 옮기거나 씬에서 지울 것. 지금은 프리팹을 새로 찍어 쓴다.");
                continue;
            }

            _setupRoot = candidate.gameObject;
            _setupView = candidate;

            PrepareSetupView();
            Debug.Log($"[LocalMatchStarter] 씬에 배치된 '{candidate.name}'을 사용한다.");
            return true;
        }

        return false;
    }

    /// <summary>자기 자신을 뺀 조상 중에 꺼져 있는 것이 있으면 돌려준다. 없으면 null.</summary>
    private static Transform FindInactiveAncestor(Transform t)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf) return p;
        }

        return null;
    }

    private bool BuildSetupFromPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(SetupResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[LocalMatchStarter] 설정 화면 프리팹을 찾지 못했다: Resources/{SetupResourcePath}");
            return false;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogError("[LocalMatchStarter] Canvas를 찾지 못해 설정 화면을 띄울 수 없다.");
            return false;
        }

        _setupRoot = Instantiate(prefab, canvas.transform);
        _setupRoot.name = prefab.name;   // (Clone) 꼬리표 제거

        _setupView = _setupRoot.GetComponent<MatchSetupView>()
                     ?? _setupRoot.GetComponentInChildren<MatchSetupView>(true);

        if (_setupView == null)
        {
            Debug.LogError($"[LocalMatchStarter] '{prefab.name}'에 MatchSetupView가 없다. 프리팹 루트에 붙일 것.");
            Destroy(_setupRoot);
            _setupRoot = null;
            return false;
        }

        if (!_setupView.Validate(out string reason))
        {
            Debug.LogError($"[LocalMatchStarter] 설정 화면 프리팹이 온전하지 않다 — {reason}");
            Destroy(_setupRoot);
            _setupRoot = null;
            _setupView = null;
            return false;
        }

        PrepareSetupView();
        return true;
    }

    private static Canvas ResolveCanvas()
    {
        if (PlayerUIManager.Instance != null && PlayerUIManager.Instance.myHandTransform != null)
        {
            var c = PlayerUIManager.Instance.myHandTransform.GetComponentInParent<Canvas>();
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }

        return FindFirstObjectByType<Canvas>();
    }

    /// <summary>
    /// 찍었든 빌려 왔든, 쓰기 전에 똑같이 해 두어야 하는 것들.
    /// 씬에 놓인 것은 편집하기 좋도록 켜진 채 저장돼 있으므로 일단 꺼 둔다.
    /// </summary>
    private void PrepareSetupView()
    {
        _setupView.panel.SetActive(false);

        _setupView.startButton.onClick.RemoveAllListeners();
        _setupView.startButton.onClick.AddListener(OnStartClicked);
    }

    // ─── 목록 채우기 ────────────────────────────────────────────────────

    private void PopulateSetup()
    {
        FillModeDropdown();
        FillCharacterIds();

        FillSide(_setupView.sideA, "1P", P1DefaultCharacters);
        FillSide(_setupView.sideB, "2P", P2DefaultCharacters);
    }

    private void FillModeDropdown()
    {
        TMP_Dropdown dropdown = _setupView.modeDropdown;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "사람 vs 봇", "봇 vs 봇" });
        dropdown.value = mode == LocalMatchMode.HumanVsBot ? 0 : 1;
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(_ => RefreshInfoLabels());
    }

    /// <summary>용병 드롭다운은 CardDataManager가 읽은 Character.json 순서를 그대로 쓴다.</summary>
    private void FillCharacterIds()
    {
        _characterIds.Clear();

        if (CardDataManager.Instance == null)
        {
            Debug.LogWarning("[LocalMatchStarter] CardDataManager가 없어 용병 목록을 채우지 못한다.");
            return;
        }

        foreach (CharacterData character in CardDataManager.Instance.allCharacterList)
        {
            if (character == null || string.IsNullOrEmpty(character.characterId)) continue;
            _characterIds.Add(character.characterId);
        }
    }

    private void FillSide(MatchSetupSideView side, string label, string[] defaultCharacters)
    {
        if (side.header != null) side.header.text = label;

        // 덱 목록 — 첫 항목은 언제나 하드코딩 조합이다
        var deckOptions = new List<string> { DefaultDeckOption };
        deckOptions.AddRange(DeckLoader.ListDeckNames());

        side.deckDropdown.ClearOptions();
        side.deckDropdown.AddOptions(deckOptions);
        side.deckDropdown.value = 0;
        side.deckDropdown.RefreshShownValue();

        // 용병 목록
        var characterNames = new List<string>();
        foreach (string id in _characterIds)
        {
            CharacterData data = CardDataManager.Instance?.GetCharacter(id);
            characterNames.Add(data != null && !string.IsNullOrEmpty(data.name) ? data.name : id);
        }

        FillCharacterDropdown(side.char1Dropdown, characterNames);
        FillCharacterDropdown(side.char2Dropdown, characterNames);

        ApplyCharacters(side, defaultCharacters);

        // 덱을 바꾸면 용병 두 칸이 그 덱의 것으로 다시 채워진다.
        // 그 뒤에 손으로 바꾼 값은 다음에 덱을 다시 고를 때까지 유지된다.
        side.deckDropdown.onValueChanged.RemoveAllListeners();
        side.deckDropdown.onValueChanged.AddListener(_ => OnDeckChanged(side, defaultCharacters));

        side.char1Dropdown.onValueChanged.RemoveAllListeners();
        side.char1Dropdown.onValueChanged.AddListener(_ => RefreshInfoLabels());
        side.char2Dropdown.onValueChanged.RemoveAllListeners();
        side.char2Dropdown.onValueChanged.AddListener(_ => RefreshInfoLabels());

        RefreshInfoLabel(side);
    }

    private static void FillCharacterDropdown(TMP_Dropdown dropdown, List<string> names)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(names);
        dropdown.RefreshShownValue();
    }

    private void OnDeckChanged(MatchSetupSideView side, string[] defaultCharacters)
    {
        string deckName = SelectedDeckName(side);

        if (deckName == null)
        {
            ApplyCharacters(side, defaultCharacters);
        }
        else if (DeckLoader.TryLoad(deckName, out _, out List<string> characterIds) && characterIds.Count > 0)
        {
            ApplyCharacters(side, characterIds.ToArray());
        }

        RefreshInfoLabel(side);
    }

    /// <summary>용병 두 칸을 주어진 characterId로 맞춘다. 목록에 없는 것은 건너뛴다.</summary>
    private void ApplyCharacters(MatchSetupSideView side, string[] characterIds)
    {
        if (characterIds == null || _characterIds.Count == 0) return;

        if (characterIds.Length > 0) SetDropdownTo(side.char1Dropdown, characterIds[0]);
        if (characterIds.Length > 1) SetDropdownTo(side.char2Dropdown, characterIds[1]);
    }

    private void SetDropdownTo(TMP_Dropdown dropdown, string characterId)
    {
        int index = _characterIds.IndexOf(characterId);
        if (index < 0) return;

        dropdown.value = index;
        dropdown.RefreshShownValue();
    }

    private void RefreshInfoLabels()
    {
        RefreshInfoLabel(_setupView.sideA);
        RefreshInfoLabel(_setupView.sideB);
    }

    private void RefreshInfoLabel(MatchSetupSideView side)
    {
        if (side.infoLabel == null) return;

        int cardCount = BuildDeckFor(side, IsFirstSide(side)).Count;
        string first = CharacterNameAt(side.char1Dropdown);
        string second = CharacterNameAt(side.char2Dropdown);

        string role = RoleOf(side);
        side.infoLabel.text = $"{cardCount}장 · {first} / {second}\n{role}";
    }

    private string CharacterNameAt(TMP_Dropdown dropdown)
    {
        if (dropdown.options.Count == 0) return "?";
        return dropdown.options[Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1)].text;
    }

    private string RoleOf(MatchSetupSideView side)
    {
        LocalMatchMode selected = SelectedMode();
        bool isFirst = IsFirstSide(side);

        if (selected == LocalMatchMode.HumanVsBot) return isFirst ? "사람" : "봇";
        return "봇";
    }

    private bool IsFirstSide(MatchSetupSideView side) => side == _setupView.sideA;

    private LocalMatchMode SelectedMode()
        => _setupView.modeDropdown.value == 0 ? LocalMatchMode.HumanVsBot : LocalMatchMode.BotVsBot;

    /// <summary>고른 덱 이름. 첫 항목(기본 테스트 덱)이면 null.</summary>
    private static string SelectedDeckName(MatchSetupSideView side)
    {
        if (side.deckDropdown.value <= 0) return null;

        string name = side.deckDropdown.options[side.deckDropdown.value].text;
        return name == DefaultDeckOption ? null : name;
    }

    // ─── 시작 ───────────────────────────────────────────────────────────

    private void OnStartClicked()
    {
        LocalMatchMode selected = SelectedMode();

        PlayerSetupData p1 = BuildSetupFromSide(_setupView.sideA, isFirstSide: true, matchMode: selected);
        PlayerSetupData p2 = BuildSetupFromSide(_setupView.sideB, isFirstSide: false, matchMode: selected);

        _setupView.panel.SetActive(false);
        StartMatchWith(selected, p1, p2);
    }

    private PlayerSetupData BuildSetupFromSide(MatchSetupSideView side, bool isFirstSide, LocalMatchMode matchMode)
    {
        List<string> deck = BuildDeckFor(side, isFirstSide);

        string char1 = CharacterIdAt(side.char1Dropdown, isFirstSide ? P1DefaultCharacters[0] : P2DefaultCharacters[0]);
        string char2 = CharacterIdAt(side.char2Dropdown, isFirstSide ? P1DefaultCharacters[1] : P2DefaultCharacters[1]);

        return new PlayerSetupData
        {
            PlayerName = ResolveName(isFirstSide, matchMode),
            Type = ResolveType(isFirstSide, matchMode),
            MainCharacterId = ToCharacterCardId(char1),
            SubCharacterId = ToCharacterCardId(char2),
            DeckCardIds = deck
        };
    }

    /// <summary>고른 덱의 카드 목록. 기본 항목이거나 읽지 못하면 하드코딩 덱으로 돌아간다.</summary>
    private List<string> BuildDeckFor(MatchSetupSideView side, bool isFirstSide)
    {
        string deckName = SelectedDeckName(side);

        if (deckName != null && DeckLoader.TryLoad(deckName, out List<string> cardIds, out _))
            return cardIds;

        return BuildDeckList(isFirstSide ? P1UniqueCardIds : P2UniqueCardIds);
    }

    private string CharacterIdAt(TMP_Dropdown dropdown, string fallback)
    {
        if (_characterIds.Count == 0) return fallback;

        int index = Mathf.Clamp(dropdown.value, 0, _characterIds.Count - 1);
        return _characterIds[index];
    }

    /// <summary>
    /// characterId("ELLIE") → 용병 카드 ID("ELLI-01").
    /// 엔진의 PlayerSetupData는 카드 ID를 받는다.
    /// </summary>
    private static string ToCharacterCardId(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return null;

        CharacterData data = CardDataManager.Instance?.GetCharacter(characterId);
        if (data != null && !string.IsNullOrEmpty(data.id)) return data.id;

        Debug.LogWarning($"[LocalMatchStarter] 용병 '{characterId}'의 카드 ID를 찾지 못했다.");
        return null;
    }

    // ─── 기본 조합 ──────────────────────────────────────────────────────

    private static PlayerSetupData BuildDefaultSetup(bool isFirstSide, LocalMatchMode matchMode)
    {
        return new PlayerSetupData
        {
            PlayerName = ResolveName(isFirstSide, matchMode),
            Type = ResolveType(isFirstSide, matchMode),
            MainCharacterId = isFirstSide ? "ELLI-01" : "VERO-01",
            SubCharacterId = isFirstSide ? "DAIN-01" : "SONI-01",
            DeckCardIds = BuildDeckList(isFirstSide ? P1UniqueCardIds : P2UniqueCardIds)
        };
    }

    private static string ResolveName(bool isFirstSide, LocalMatchMode matchMode)
    {
        if (!isFirstSide) return "Bot_Blue";
        return matchMode == LocalMatchMode.HumanVsBot ? "Player" : "Bot_Red";
    }

    private static UserType ResolveType(bool isFirstSide, LocalMatchMode matchMode)
    {
        if (!isFirstSide) return UserType.Bot;
        return matchMode == LocalMatchMode.HumanVsBot ? UserType.Human : UserType.Bot;
    }

    private static List<string> BuildDeckList(string[] uniqueIds)
    {
        var deck = new List<string>(uniqueIds.Length * 2);
        foreach (string id in uniqueIds)
        {
            deck.Add(id);
            deck.Add(id);
        }

        return deck;
    }
}
