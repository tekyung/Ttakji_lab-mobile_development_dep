using System.Collections.Generic;
using System.Linq; // 리스트 검색용 기능
using UnityEngine;
using TMPro; // 텍스트 사용
using UnityEngine.UI; // 용병 슬롯·선택 목록을 코드로 만든다
using System.IO; //파일 관리
using UnityEngine.SceneManagement; // 미저장 확인 후 씬 이동

[System.Serializable]
public class DeckSaveData
{
    public List<string> cardIdList;

    /// <summary>
    /// 이 덱이 쓰는 용병 characterId 목록 ("ELLIE", "SONIA" …). 최대 2개.
    ///
    /// ★ 예전에는 서버가 카드들의 characterId로 용병을 <b>역산</b>했다.
    ///   그러면 용병 2명을 골라 놓고 한쪽 카드만 넣은 덱이 1명짜리로 읽힌다.
    ///   구버전 덱은 이 값이 비어 있으므로, 읽는 쪽은 비었을 때 역산으로 넘어가야 한다.
    /// </summary>
    public List<string> characterIdList;
}

public class DeckBuilderManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Transform deckContent;       // MyDeckPanel의 Content
    public Transform collectionContent; // CollectionPanel의 Content
    public GameObject cardPrefab;       // CardSlot 프리팹
    public TextMeshProUGUI deckCountText; // 20/20 텍스트 (없으면 연결 안 해도 됨)

    [Header("팝업 연결")]
    public GameObject PopupPanel;

    public GameObject warningPopup; // 20장 안될 때 팝업
    public GameObject okPopup; //덱 저장 완료시 팝업

    [Tooltip("모든 안내 문구가 지나가는 범용 메시지 팝업. 씬의 MessagePanel.")]
    public GameObject messagePopup;

    [Tooltip("범용 메시지 팝업의 문구. ShowMessagePopup이 매번 덮어쓴다.")]
    public TextMeshProUGUI messageText;

    public GameObject deleteConfirmPopup; // 삭제 확인 팝업
    public TextMeshProUGUI deleteConfirmText; // 삭제 덱 이름

    public GameObject newDeckPopup; // 새 덱 만들기 팝업

    [Header("저장 확인 팝업 — [저장]/[저장안함]/[취소] 세 버튼")]
    [Tooltip("저장하지 않은 채 다른 덱·씬으로 넘어가려 할 때 뜬다. 꺼진 채로 씬에 만들어 둔다.")]
    public GameObject saveConfirmPopup;

    [Tooltip("확인 팝업의 안내 문구. 없어도 동작한다.")]
    public TextMeshProUGUI saveConfirmText;

    private string deckToDelete = "";

    /// <summary>
    /// 지금 편집 중인(= 파일에서 불러왔거나 방금 저장한) 덱 이름. 새 덱이면 비어 있다.
    /// 저장 시 이 이름과 같으면 덮어쓰고, 다르면 이름 충돌로 보아 자동 번호를 붙인다.
    /// </summary>
    private string _editingDeckName = "";

    [Header("카드 확대 팝업")]
    public GameObject cardZoomPopup; // 팝업창
    public CardUI zoomedCardUI;

    [Header("덱 이름")]
    public TMP_InputField deckNameInput; // 덱 이름

    [Header("덱 목록 UI")]
    public TMP_Dropdown deckListDropdown;

    // ─────────────────────────────────────────────────────────────
    // 씬에서 배치를 직접 잡고 싶을 때 연결하는 자리.
    //
    // ★ 비워 두면 예전처럼 코드가 만들어 준다(기존 씬 호환).
    //   하나라도 연결하면 그쪽이 우선이고, 코드는 내용만 채운다.
    // ─────────────────────────────────────────────────────────────

    [Header("용병 슬롯 — 씬에 만들어 두고 연결하면 배치를 직접 조절할 수 있다")]
    [Tooltip("1번, 2번 순서대로 넣는다. 비우면 아래 프리팹 → 코드 생성 순으로 넘어간다.")]
    public CharacterSlotView[] characterSlots;

    [Tooltip("슬롯 바 프리팹. 비우면 Resources/Build/CharacterSlotBar 를 자동으로 찾는다.")]
    public GameObject characterSlotBarPrefab;

    [Tooltip("지금 편집 중인 덱 이름을 보여 줄 텍스트. 없어도 된다.")]
    public TextMeshProUGUI editingDeckNameText;

    [Header("용병 선택 팝업 — 씬에 비활성으로 두고 연결한다")]
    [Tooltip("팝업 루트. 비우면 코드가 자동 생성한다.")]
    public GameObject characterPickerPanel;

    [Tooltip("목록 항목이 들어갈 부모(보통 Content). 비우면 팝업 루트에 직접 넣는다.")]
    public Transform characterPickerContent;

    [Tooltip("목록 한 줄로 쓸 프리팹(Button + 자식 Text). 비우면 코드가 줄을 만든다.")]
    public GameObject characterPickerRowPrefab;

    [Tooltip("'1번 슬롯 — 현재: OO' 를 보여 줄 제목. 없어도 된다.")]
    public TextMeshProUGUI characterPickerTitle;

    [Header("용병 목록 줄 색")]
    [Tooltip("지금 이 슬롯이 쓰는 용병")]
    public Color pickerCurrentColor = new Color(0.28f, 0.44f, 0.30f, 1f);

    [Tooltip("고를 수 있는 용병")]
    public Color pickerNormalColor = new Color(0.26f, 0.26f, 0.32f, 1f);

    [Tooltip("다른 슬롯이 이미 쓰는 용병")]
    public Color pickerDisabledColor = new Color(0.18f, 0.18f, 0.2f, 1f);

    [Header("카드·용병 미리보기 — 씬 좌측 하단에 만들어 두고 연결한다")]
    [Tooltip("카드를 짧게 클릭하거나 용병 슬롯을 누르면 여기에 그림과 효과가 뜬다. 비워 두면 미리보기 기능만 조용히 꺼진다(다른 동작에는 영향 없다).")]
    public CardPreviewPanelView cardPreview;

    // 실제 데이터 (덱에 들어있는 카드 ID 목록)
    private List<string> myDeck = new List<string>();
    private const int MAX_DECK_COUNT = 20;

    // 화면에 떠 있는 카드 슬롯들을 관리하는 리스트 (Collection 쪽)
    private List<CardUI> collectionSlots = new List<CardUI>();

    // ★ "지금 고른 덱"은 PlayerStorage가 들고 있다. 메인 화면(DeckSelect)과 같은 칸을 본다.

    void Start()
    {
        // 1. 게임 시작 시 전체 카드 목록(Collection)을 먼저 만듭니다.
        InitCollection();

        // 2. 용병 슬롯 UI 생성 (카드 목록 필터의 기준이 되므로 덱을 읽기 전에 만든다)
        SetupCharacterSlots();

        // 2-1. ★ 용병 선택 팝업을 미리 정리해 둔다.
        //   예전에는 "팝업을 열 때" 정리했는데, 그러면 씨에 켜둔 채로 저장한 팝업이
        //   화면 진입자마자 보이고, 안에 남은 예시 줄의 [닫기]는 아무 동작도 하지 않는다.
        EnsurePickerPanel();

        // 2-2. 미리보기 패널을 점검하고 비워 둔다. 없으면 조용히 넘어간다.
        SetupCardPreview();

        // 2-3. ★ 드롭다운에서 덱을 고르면 바로 불러오도록 잇는다.
        //   씬의 onValueChanged는 비어 있었다 — 그래서 덱을 골라도 아무 일이 없었다.
        //   코드로 다는 이유: 배선을 씬에 흩뿌리지 않고 억제 플래그와 한자리에 두기 위해서다.
        if (deckListDropdown != null)
        {
            deckListDropdown.onValueChanged.RemoveListener(OnDeckSelected);
            deckListDropdown.onValueChanged.AddListener(OnDeckSelected);
        }

        // 확인 팝업은 꺼진 채로 시작한다 (씬에 켜 둔 채 저장했을 수 있다)
        if (saveConfirmPopup != null) saveConfirmPopup.SetActive(false);

        // 3. ★ 메인 화면에서 고른 덱을 그대로 연다.
        //   focusDeckName 인자는 원래 있었는데 아무도 넘기지 않아, 늘 목록 맨 위 덱이 열렸다.
        RefreshDeckList(PlayerStorage.GetSelectedDeck());
    }

    // ─────────────────────────────────────────────────────────────
    // 카드·용병 미리보기 (좌측 하단)
    //
    // 화면은 사람이 씬에 만든다. 여기서는 "무엇을 그릴지"만 넣는다.
    // 그림은 CardImageLoader가 preserveAspect를 켜므로 상자 안에서 비율을 지킨 채 커진다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>미리보기가 쓸 만한 상태인가. Start에서 한 번만 판정하고 결과를 들고 있는다.</summary>
    private bool _previewReady;

    private void SetupCardPreview()
    {
        // ★ 인스펙터 연결을 깜빡해도 씬에 있으면 찾아 쓴다.
        //   용병 슬롯 바(TryBindSlotBarInScene)와 같은 방식이다 —
        //   씬에 만들어 두고 연결을 잊는 일이 실제로 있었다.
        if (cardPreview == null)
        {
            cardPreview = FindFirstObjectByType<CardPreviewPanelView>(FindObjectsInactive.Include);

            if (cardPreview != null)
                Debug.Log($"[DeckBuilder] 인스펙터가 비어 있어 씬에서 '{cardPreview.name}'을 찾아 쓴다. "
                          + "인스펙터에 직접 연결해 두는 편이 확실하다.");
        }

        if (cardPreview == null)
        {
            // 안 만든 것도 정상이다. 미리보기 기능만 조용히 꺼진다.
            Debug.Log("[DeckBuilder] 미리보기 패널이 없어 카드 효과 미리보기를 쓰지 않는다.");
            _previewReady = false;
            return;
        }

        if (!cardPreview.Validate(out string reason))
        {
            // 여기까지 왔다는 것은 패널은 만들었는데 알맹이가 빠졌다는 뜻이다. 크게 알린다.
            Debug.LogError(
                $"[DeckBuilder] '{cardPreview.name}'의 참조가 온전하지 않아 미리보기를 쓸 수 없다 — {reason}. "
                + "그림 오브젝트에 Image 컴포넌트가, 설명 오브젝트에 TextMeshPro 컴포넌트가 붙어 있는지 확인할 것.");
            _previewReady = false;
            return;
        }

        _previewReady = true;
        ClearPreview();
    }

    /// <summary>카드 한 장을 미리보기에 올린다. 카드 슬롯을 짧게 클릭하면 불린다.</summary>
    public void ShowCardPreview(string cardId)
    {
        if (!_previewReady) return;

        CardData data = CardDataManager.Instance != null ? CardDataManager.Instance.GetCard(cardId) : null;
        if (data == null)
        {
            ClearPreview();
            return;
        }

        ApplyPreview(data.imagePath, data.name, data.description);
    }

    /// <summary>용병 한 명을 미리보기에 올린다. 빈 슬롯이면 비운다.</summary>
    public void ShowCharacterPreview(string characterId)
    {
        if (!_previewReady) return;

        CharacterData character = CardDataManager.Instance != null
            ? CardDataManager.Instance.GetCharacter(characterId)
            : null;

        if (character == null)
        {
            ClearPreview();
            return;
        }

        ApplyPreview(character.imagePath, character.name, character.description);
    }

    /// <summary>미리보기를 비운다. 아직 아무것도 고르지 않았을 때의 모습이다.</summary>
    public void ClearPreview()
    {
        if (!_previewReady) return;

        cardPreview.image.sprite = null;
        cardPreview.image.enabled = false;

        cardPreview.descText.text = string.Empty;
        if (cardPreview.nameText != null) cardPreview.nameText.text = string.Empty;

        if (cardPreview.imageRoot != null) cardPreview.imageRoot.SetActive(false);
        if (cardPreview.descRoot != null) cardPreview.descRoot.SetActive(false);
        if (cardPreview.emptyHint != null) cardPreview.emptyHint.SetActive(true);
    }

    private void ApplyPreview(string imagePath, string displayName, string description)
    {
        if (cardPreview.emptyHint != null) cardPreview.emptyHint.SetActive(false);
        if (cardPreview.imageRoot != null) cardPreview.imageRoot.SetActive(true);
        if (cardPreview.descRoot != null) cardPreview.descRoot.SetActive(true);

        // ★ 여기서 preserveAspect가 켜진다 — 상자를 키우면 그림이 비율을 지킨 채 따라 커진다.
        bool loaded = CardImageLoader.ApplyToImage(cardPreview.image, imagePath);
        if (!loaded)
        {
            cardPreview.image.sprite = null;
            Debug.LogWarning($"[DeckBuilder] 미리보기 그림을 불러오지 못했다: {imagePath}");
        }

        cardPreview.image.enabled = loaded;

        if (cardPreview.nameText != null)
            cardPreview.nameText.text = displayName ?? string.Empty;

        cardPreview.descText.text = string.IsNullOrWhiteSpace(description)
            ? "(효과 설명이 없다)"
            : description;
    }

    /// <summary>메인 화면과 선택을 맞춘다. 편집 화면에서 덱을 바꾸거나 저장할 때 부른다.</summary>
    private void RememberSelectedDeck(string deckName)
    {
        if (string.IsNullOrEmpty(deckName) || deckName == EMPTY_DECK_LABEL) return;

        PlayerStorage.SetSelectedDeck(deckName);
    }

    // ---------------------------------------------------
    // 용병 슬롯 — 카드 목록의 기준
    // ---------------------------------------------------
    //
    // 예전에는 40종을 전부 늘어놓고 "알아서 2종만 고르라"고 했다.
    // 룰을 아는 사람만 쓸 수 있는 화면이라, 먼저 용병을 고르고
    // 그 용병의 카드만 보이게 바꾼다. (용병 1명당 효과 카드 10종)

    /// <summary>지금 고른 용병 characterId 목록. 순서는 슬롯 순서다.</summary>
    private readonly List<string> _selectedCharacters = new List<string>();

    /// <summary>
    /// 실제로 화면에 그릴 슬롯들. 씬에서 연결했으면 그것들이고, 아니면 코드가 만든 것들이다.
    /// 예전에는 버튼·배경·초상·이름을 네 개의 리스트로 따로 들고 있어 인덱스가 어긋날 위험이 있었다.
    /// </summary>
    private readonly List<CharacterSlotView> _slotViews = new List<CharacterSlotView>();

    private TextMeshProUGUI _editingNameLabel;

    /// <summary>코드가 만든 팝업. 씬에서 연결한 경우에는 쓰지 않는다(파괴하면 안 되므로).</summary>
    private GameObject _generatedPickerPanel;

    /// <summary>지금 열려 있는 팝업(씬 것이든 코드 것이든).</summary>
    private GameObject _openPickerPanel;

    /// <summary>팝업에 넣은 줄들. 다시 열 때 지우기 위해 들고 있는다.</summary>
    private readonly List<GameObject> _pickerRows = new List<GameObject>();

    /// <summary>런타임에 만든 글자에 쓸 폰트. 지정하지 않으면 한글이 깨진다.</summary>
    private TMP_FontAsset _uiFont;

    /// <summary>
    /// 용병 슬롯을 준비한다.
    ///
    /// ★ 씬에서 <see cref="characterSlots"/>를 연결했으면 그것을 그대로 쓴다 —
    ///   배치·크기·색을 기획자가 유니티에서 직접 잡을 수 있다.
    ///   연결하지 않았으면 예전처럼 코드가 만든다(기존 씬 호환).
    /// </summary>
    private void SetupCharacterSlots()
    {
        _uiFont = UiFontResolver.Resolve();

        // ★ 프로젝트 폰트에 없는 기호를 OS 폰트에서 끌어오도록 폴백을 건다.
        //   예전에는 코드 생성 경로에서만 불러서,
        //   씨·프리팡을 쓰면 기호가 □로 깨졌다.
        UiFontResolver.EnsureSymbolFallback();

        _slotViews.Clear();

        // ① 씬에 직접 배치한 슬롯이 있으면 그것이 최우선
        if (characterSlots != null && characterSlots.Length > 0)
        {
            BindSceneCharacterSlots();
            return;
        }

        // ② 씬에 이미 올려 둔 슬롯 바가 있으면 그것을 쓴다
        //    ★ 이 검사가 없으면 씬에 배치한 것 위에 프리팹을 하나 더 찍어 **겹쳐 보인다.**
        if (TryBindSlotBarInScene()) return;

        // ③ 아무것도 없으면 프리팹을 찍는다
        if (TryBuildSlotBarFromPrefab()) return;

        // ④ 마지막 수단: 코드로 만든다
        BuildCharacterSlotBar();
    }

    /// <summary>
    /// 씬에 이미 배치된 슬롯 바를 찾아 연결한다. 위치·크기는 손대지 않는다.
    ///
    /// 인스펙터에 연결하지 않고 프리팹을 씬에 드래그해 둔 경우를 위한 길이다.
    /// </summary>
    private bool TryBindSlotBarInScene()
    {
        CharacterSlotView[] found = FindObjectsByType<CharacterSlotView>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (found.Length == 0) return false;

        // FindObjectsByType의 순서는 하이어라키 순서가 아니다.
        // 1번·2번을 제대로 가리려면 공통 부모(바)에서 다시 훑어야 한다.
        Transform bar = found[0].transform.parent;
        CharacterSlotView[] ordered = bar != null
            ? bar.GetComponentsInChildren<CharacterSlotView>(true)
            : found;

        foreach (CharacterSlotView view in ordered)
        {
            if (_slotViews.Count >= MAX_DECK_CHARACTERS) break;

            view.ResolveMissingReferences();

            int slotIndex = _slotViews.Count;
            if (view.button != null)
            {
                view.button.onClick.RemoveAllListeners();
                view.button.onClick.AddListener(() => OpenCharacterPicker(slotIndex));
            }

            _slotViews.Add(view);
        }

        if (_slotViews.Count == 0) return false;

        ResolveEditingNameLabel(bar);

        Debug.Log($"[DeckBuilder] 씬에 배치된 슬롯 바 '{(bar != null ? bar.name : "?")}'를 사용한다 — 슬롯 {_slotViews.Count}칸.");
        return true;
    }

    /// <summary>
    /// 슬롯 바 프리팹을 찍어 화면에 올린다.
    ///
    /// ★ 이게 있으면 배치·크기·색을 유니티에서 편집하고 저장할 수 있다.
    ///   코드 생성은 씬을 볼 수 없던 시절의 임시방편이라, 프리팹이 있으면 늘 이쪽이 낫다.
    /// </summary>
    private bool TryBuildSlotBarFromPrefab()
    {
        GameObject prefab = characterSlotBarPrefab != null
            ? characterSlotBarPrefab
            : Resources.Load<GameObject>(SlotBarResourcePath);

        if (prefab == null) return false;

        Canvas canvas = deckContent != null ? deckContent.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[DeckBuilder] 캔버스를 찾지 못해 슬롯 바 프리팹을 올리지 못했다.");
            return false;
        }

        // 기준 버튼 옆에 붙인다. 못 찾으면 캔버스 바로 아래에 둔다.
        RectTransform anchorButton = FindButtonByMethod("OnClickNewDeckButton")
                                     ?? FindButtonByMethod("OnClickSaveDeck");

        Transform parent = anchorButton != null ? anchorButton.parent : canvas.transform;
        GameObject bar = Instantiate(prefab, parent);
        bar.name = prefab.name;   // (Clone) 꼬리표를 떼어 하이어라키를 읽기 쉽게

        var barRect = bar.transform as RectTransform;

        if (anchorButton != null && barRect != null)
        {
            PlaceBarLeftOf(barRect, anchorButton);
            StartCoroutine(PlaceBarWhenLayoutSettles(barRect, anchorButton));
        }

        // 프리팹 안의 슬롯을 순서대로 거둔다 (하이어라키 순서 = 1번, 2번)
        foreach (CharacterSlotView view in bar.GetComponentsInChildren<CharacterSlotView>(true))
        {
            if (_slotViews.Count >= MAX_DECK_CHARACTERS) break;

            view.ResolveMissingReferences();

            int slotIndex = _slotViews.Count;
            if (view.button != null)
                view.button.onClick.AddListener(() => OpenCharacterPicker(slotIndex));

            _slotViews.Add(view);
        }

        if (_slotViews.Count == 0)
        {
            Debug.LogWarning($"[DeckBuilder] '{prefab.name}' 안에 CharacterSlotView가 없다. 코드 생성으로 되돌아간다.");
            Destroy(bar);
            return false;
        }

        ResolveEditingNameLabel(bar.transform);

        Debug.Log($"[DeckBuilder] 슬롯 바 프리팹 '{prefab.name}' 사용 — 슬롯 {_slotViews.Count}칸.");
        return true;
    }

    /// <summary>Resources 아래에서 슬롯 바를 찾을 경로 (확장자·Resources 접두어 없이).</summary>
    private const string SlotBarResourcePath = "Build/CharacterSlotBar";

    /// <summary>
    /// "편집 중: OO" 텍스트를 정한다.
    ///
    /// ★ <see cref="editingDeckNameText"/>를 연결했으면 <b>어디에 있든</b> 그것을 쓴다 —
    ///   슬롯 바와 떨어진 자리에 두고 싶을 때를 위한 것이다.
    ///   연결하지 않았을 때만 바 안에서 (슬롯에 속하지 않은) 텍스트를 찾아 쓴다.
    /// </summary>
    private void ResolveEditingNameLabel(Transform bar)
    {
        // 바 안에 들어 있는 "편집 중" 텍스트(슬롯에 속하지 않은 것)를 찾아 둔다
        TextMeshProUGUI insideBar = null;

        if (bar != null)
        {
            foreach (TextMeshProUGUI text in bar.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                // 슬롯 안의 이름표는 건드리지 않는다
                if (text.GetComponentInParent<CharacterSlotView>() != null) continue;

                insideBar = text;
                break;
            }
        }

        if (editingDeckNameText != null)
        {
            _editingNameLabel = editingDeckNameText;

            // ★ 따로 배치했으면 바 안의 것은 숨긴다 — 둘이 같이 보이면 중복이다.
            //   (프리팡을 고치지 않고도 "어디든 배치"가 가능해진다)
            if (insideBar != null && insideBar != editingDeckNameText)
                insideBar.gameObject.SetActive(false);

            return;
        }

        _editingNameLabel = insideBar;
    }

    /// <summary>씬에 배치된 슬롯에 클릭만 연결한다. 위치·크기는 건드리지 않는다.</summary>
    private void BindSceneCharacterSlots()
    {
        int used = 0;

        for (int i = 0; i < characterSlots.Length && used < MAX_DECK_CHARACTERS; i++)
        {
            CharacterSlotView view = characterSlots[i];
            if (view == null) continue;

            view.ResolveMissingReferences();

            int slotIndex = used;
            if (view.button != null)
            {
                view.button.onClick.RemoveListener(() => OpenCharacterPicker(slotIndex));
                view.button.onClick.AddListener(() => OpenCharacterPicker(slotIndex));
            }
            else
            {
                Debug.LogWarning($"[DeckBuilder] '{view.name}'에 Button이 없어 클릭을 연결하지 못했다.");
            }

            _slotViews.Add(view);
            used++;
        }

        if (_slotViews.Count == 0)
        {
            Debug.LogWarning("[DeckBuilder] characterSlots가 모두 비어 있어 코드 생성으로 되돌아간다.");
            BuildCharacterSlotBar();
            return;
        }

        ResolveEditingNameLabel(_slotViews[0].transform.parent);

        Debug.Log($"[DeckBuilder] 씬에 배치된 용병 슬롯 {_slotViews.Count}칸을 사용한다.");
    }

    /// <summary>
    /// 용병 슬롯 2칸 + "편집 중인 덱" 표시를 만든다.
    ///
    /// ★ 자리: [새 덱 만들기]·[덱 저장하기] 버튼의 <b>왼쪽</b> —
    ///   즉 덱 리스트와 카드 리스트 사이의 띠에 놓는다.
    ///   (예전엔 화면 맨 위에 붙여 두어 덱 리스트를 가렸다)
    ///   기준 버튼을 못 찾으면 화면 상단으로 물러난다.
    /// </summary>
    private void BuildCharacterSlotBar()
    {
        Canvas canvas = deckContent != null ? deckContent.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
        {
            Debug.LogWarning("[DeckBuilder] 캔버스를 찾지 못해 용병 슬롯을 만들지 못했다.");
            return;
        }

        // 기준이 될 버튼을 찾는다. 씬 배선을 건드리지 않고 onClick에 걸린 메서드 이름으로 역추적한다.
        RectTransform anchorButton = FindButtonByMethod("OnClickNewDeckButton")
                                     ?? FindButtonByMethod("OnClickSaveDeck");

        var bar = new GameObject("CharacterSlotBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var barRect = (RectTransform)bar.transform;

        const float Spacing = 12f;

        // ★ 슬롯 높이는 "덱 리스트와 카드 리스트 사이 빈 띠"에서 뽑는다.
        //   고정값이면 화면 비율이 바뀔 때 리스트를 침범하거나 쓸데없이 작아진다.
        float slotHeight = MeasureSlotHeight();
        float slotWidth = slotHeight * PortraitAspect();
        float barWidth = slotWidth * MAX_DECK_CHARACTERS
                       + Spacing * (MAX_DECK_CHARACTERS - 1) + 260f + Spacing;
        float SlotHeight = slotHeight;   // 아래 배치 코드가 쓰는 이름

        if (anchorButton != null)
        {
            // 버튼과 같은 부모·같은 앵커를 쓰고, 버튼 왼쪽으로 밀어 놓는다
            barRect.SetParent(anchorButton.parent, false);
            barRect.sizeDelta = new Vector2(barWidth, SlotHeight);

            PlaceBarLeftOf(barRect, anchorButton);

            // ★ Start 시점에는 레이아웃이 아직 돌지 않아 버튼 크기가 제값이 아니다.
            //   (TouchTargetNormalizer가 뒤닮어 크기를 바꾸기도 한다)
            //   한 프레임 뒤에 한 번 더 재어 자리를 확정한다.
            StartCoroutine(PlaceBarWhenLayoutSettles(barRect, anchorButton));
        }
        else
        {
            Debug.LogWarning("[DeckBuilder] 덱 버튼을 찾지 못해 용병 슬롯을 화면 상단에 둔다.");
            barRect.SetParent(canvas.transform, false);
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.sizeDelta = new Vector2(barWidth, SlotHeight);
            barRect.anchoredPosition = new Vector2(16f, -8f);
        }

        var layout = bar.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = Spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        for (int i = 0; i < MAX_DECK_CHARACTERS; i++)
            CreateCharacterSlot(barRect, i, slotWidth, slotHeight);

        // 지금 어떤 덱을 편집 중인지 항상 보이게 한다 (메인에서 고른 덱과 헷갈리던 문제)
        if (editingDeckNameText != null)
        {
            // 씬 어딘가에 따로 배치해 뒀다면 바 안에 또 만들지 않는다
            _editingNameLabel = editingDeckNameText;
        }
        else
        {
            _editingNameLabel = CreateLabel(barRect, "편집 중: -", 156f, SlotHeight, 15.6f);
            _editingNameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }

    }

    /// <summary>슬롯 위아래로 남길 여백.</summary>
    private const float BandPadding = 14f;

    /// <summary>슬롯이 너무 작거나 화면을 잡아먹지 않도록 하는 범위.</summary>
    private const float MinSlotHeight = 84f;
    private const float MaxSlotHeight = 192f;

    /// <summary>
    /// 덱 리스트 아래끝과 카드 리스트 위끝 사이의 빈 높이를 잰다.
    /// 둘 중 하나라도 못 찾으면 안전한 기본값으로 돌아간다.
    /// </summary>
    private float MeasureSlotHeight()
    {
        RectTransform deckPanel = FindScrollViewport(deckContent);
        RectTransform cardPanel = FindScrollViewport(collectionContent);

        if (deckPanel == null || cardPanel == null) return MinSlotHeight;

        var deckCorners = new Vector3[4];
        var cardCorners = new Vector3[4];
        deckPanel.GetWorldCorners(deckCorners);
        cardPanel.GetWorldCorners(cardCorners);

        // corners: 0=좌하, 1=좌상, 2=우상, 3=우하
        // 두 리스트 중 위에 있는 쪽의 아래끝 ~ 아래에 있는 쪽의 위끝
        float upperBottom = Mathf.Max(deckCorners[0].y, cardCorners[0].y);
        float lowerTop = Mathf.Min(deckCorners[1].y, cardCorners[1].y);

        float scale = deckPanel.lossyScale.y;
        if (Mathf.Approximately(scale, 0f)) return MinSlotHeight;

        float band = (upperBottom - lowerTop) / scale;
        if (band <= 0f) return MinSlotHeight;   // 겹쳐 있거나 잴 수 없다

        return Mathf.Clamp(band - BandPadding * 2f, MinSlotHeight, MaxSlotHeight);
    }

    /// <summary>스크롤 뷰의 보이는 영역(Viewport). Content의 부모다.</summary>
    private static RectTransform FindScrollViewport(Transform content)
    {
        if (content == null) return null;

        var scroll = content.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.viewport != null) return scroll.viewport;

        return content.parent as RectTransform;
    }

    /// <summary>용병 초상의 원본 가로/세로 비율. 못 구하면 3:4(세로로 긴 카드)로 본다.</summary>
    private float PortraitAspect()
    {
        if (CardDataManager.Instance != null)
        {
            foreach (CharacterData character in CardDataManager.Instance.allCharacterList)
            {
                if (character == null) continue;

                Sprite sprite = CardImageLoader.LoadSprite(character.imagePath);
                if (sprite == null || sprite.rect.height <= 0f) continue;

                return sprite.rect.width / sprite.rect.height;
            }
        }

        return 3f / 4f;
    }

    /// <summary>기준 버튼 왼쪽에 바를 붙인다.</summary>
    private static void PlaceBarLeftOf(RectTransform bar, RectTransform anchorButton)
    {
        if (bar == null || anchorButton == null) return;

        bar.anchorMin = anchorButton.anchorMin;
        bar.anchorMax = anchorButton.anchorMax;
        bar.pivot = new Vector2(1f, anchorButton.pivot.y);

        float buttonLeft = anchorButton.anchoredPosition.x - anchorButton.rect.width * anchorButton.pivot.x;
        bar.anchoredPosition = new Vector2(buttonLeft - 24f, anchorButton.anchoredPosition.y);
    }

    private System.Collections.IEnumerator PlaceBarWhenLayoutSettles(RectTransform bar, RectTransform anchorButton)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        PlaceBarLeftOf(bar, anchorButton);
    }

    /// <summary>
    /// onClick에 이 스크립트의 <paramref name="methodName"/>이 걸린 버튼을 찾는다.
    /// 인스펙터 배선을 바꾸지 않고 "저 버튼 옆"이라는 위치를 잡기 위한 것이다.
    /// </summary>
    private RectTransform FindButtonByMethod(string methodName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null) continue;

            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) as Object != this) continue;
                if (button.onClick.GetPersistentMethodName(i) != methodName) continue;

                return button.transform as RectTransform;
            }
        }

        return null;
    }

    private void CreateCharacterSlot(RectTransform parent, int index, float width, float height)
    {
        var go = new GameObject($"CharacterSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(width, height);

        // ★ 자동 보정에서 뺀다. 그 규칙은 "최소 4:3"을 강제하는데,
        //   용병 초상은 세로로 길어 그대로 두면 가로로 억지로 늘어난다.
        go.AddComponent<TouchTargetExempt>().reason = "용병 초상 — 원본 비율 유지";

        var element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;

        // 배경(클릭을 받는 면)
        var background = go.GetComponent<Image>();
        background.color = new Color(0.24f, 0.24f, 0.28f, 1f);

        // ★ 초상을 별도 Image로 둔다.
        //   배경 Image에 직접 스프라이트를 넣으면 비었을 때 흰 네모만 남아
        //   "깨진 칸"처럼 보인다. 초상은 있을 때만 켜준다.
        var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        var portraitRect = (RectTransform)portraitGo.transform;
        portraitRect.SetParent(rect, false);
        portraitRect.anchorMin = new Vector2(0f, 0.2f);
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = new Vector2(4f, 0f);
        portraitRect.offsetMax = new Vector2(-4f, -4f);

        var portrait = portraitGo.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portraitGo.SetActive(false);

        // 이름은 아래쪽 띄에 둔다 (초상과 겹치지 않게)
        var label = CreateLabel(rect, "용병 선택 +", 0f, 0f, 13.2f);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = new Vector2(1f, 0.2f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        int captured = index;
        go.GetComponent<Button>().onClick.AddListener(() => OpenCharacterPicker(captured));

        // 씬에서 만든 슬롯과 똑같은 모양으로 묶어 둔다 — 이후 코드가 둘을 구분할 필요가 없다
        var view = go.AddComponent<CharacterSlotView>();
        view.button = go.GetComponent<Button>();
        view.background = background;
        view.portrait = portrait;
        view.label = label;

        _slotViews.Add(view);
    }

    private TextMeshProUGUI CreateLabel(RectTransform parent, string text, float width, float height, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        if (width > 0f || height > 0f) rect.sizeDelta = new Vector2(width, height);

        var label = go.GetComponent<TextMeshProUGUI>();

        // ★ 런타임 생성 텍스트는 폰트를 직접 넣어 줘야 한다.
        //   지정하지 않으면 기본 폰트에 한글 글리프가 없어 글자가 깨져 보인다.
        if (_uiFont != null) label.font = _uiFont;

        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>슬롯을 눌렀을 때 뜨는 용병 선택 목록 (비우기 포함).</summary>
    private void OpenCharacterPicker(int slotIndex)
    {
        if (CardDataManager.Instance == null) return;

        CloseCharacterPicker();

        // ★ 창을 여는 이 클릭은 "바깥 클릭"으로 세면 안 된다 — 아래 Update 주석 참조.
        _pickerOpenedFrame = Time.frameCount;

        // 팝업을 확보한다: 인스펙터 연결 → 씬 배치 → 프리팹 → 코드 생성
        EnsurePickerPanel();

        RectTransform rowParent = characterPickerPanel != null
            ? PrepareScenePicker()
            : PrepareGeneratedPicker();

        if (rowParent == null) return;

        // ★ 지금 이 슬롯에 뭐가 들어 있는지를 제목에도 밝힌다.
        //   예전에는 "다른 슬롯이 쓰는 용병"만 흐리게 표시해,
        //   유저가 그 흐린 항목을 "선택된 것"으로 읽어 1·2번이 바뀐 듯 보였다.
        string currentId = slotIndex < _selectedCharacters.Count ? _selectedCharacters[slotIndex] : null;
        CharacterData current = CardDataManager.Instance.GetCharacter(currentId);

        // 슬롯을 누르면 미리보기도 그 용병으로 바꾼다. 빈 슬롯이면 비워진다.
        ShowCharacterPreview(currentId);
        string currentName = current != null ? current.name : "비어 있음";
        string title = $"{slotIndex + 1}번 슬롯  —  현재: {currentName}";

        if (characterPickerTitle != null) characterPickerTitle.text = title;
        else CreateLabel(rowParent, title, 0f, 28.8f, 15.6f);

        foreach (CharacterData character in CardDataManager.Instance.allCharacterList)
        {
            if (character == null) continue;

            int usedAt = _selectedCharacters.IndexOf(character.characterId);
            bool isThisSlot = usedAt == slotIndex;
            bool usedByOtherSlot = usedAt >= 0 && !isThisSlot;

            // 세 상태를 글로 구분한다 — 색만으로는 뭐가 뭔지 알 수 없다.
            string rowText = character.name;
            if (isThisSlot) rowText += $"   {CheckMark} 현재 이 슬롯";
            else if (usedByOtherSlot) rowText += $"   ({usedAt + 1}번 슬롯이 사용 중)";

            CharacterData captured = character;
            AddPickerRow(rowParent, rowText, !usedByOtherSlot,
                () => SetCharacterSlot(slotIndex, captured.characterId), isThisSlot);
        }

        AddPickerRow(rowParent, "비우기", true, () => SetCharacterSlot(slotIndex, null));
        AddPickerRow(rowParent, "닫기", true, CloseCharacterPicker);
    }

    /// <summary>
    /// 선택 표시에 쓸 기호.
    ///
    /// ✓(U+2713)는 프로젝트 한글 폰트에 없어서, OS 폰트 폴백이 닿아야 보인다.
    /// 닿았는지를 <see cref="UiFontResolver.CanRender"/>로 물어보고,
    /// 안 되는 환경에서만 깔끔한 대체 기호로 물러난다.
    /// — □가 보이는 것보다는 낫다.
    /// </summary>
    private static string CheckMark => UiFontResolver.CanRender('✓') ? "✓" : "—";

    /// <summary>Resources 아래에서 용병 선택 팝업을 찾을 경로.</summary>
    private const string PickerResourcePath = "Build/CharacterPicker";

    /// <summary>프리팹에서 만든 팝업. 한 번만 만들어 두고 껐다 켠다.</summary>
    private bool _pickerReady;

    /// <summary>
    /// 팝업을 확보한다. 이미 있으면 아무것도 하지 않는다.
    ///
    /// 순서: 인스펙터 연결 → 씬에 배치된 것 → 프리팹 → (없으면 코드 생성으로 넘어간다)
    /// ★ 씬 검사가 없으면 씬에 둔 팝업 위에 프리팹을 또 찍어 겹친다.
    /// </summary>
    private void EnsurePickerPanel()
    {
        if (_pickerReady) return;

        Canvas canvas = deckContent != null ? deckContent.GetComponentInParent<Canvas>() : null;

        // ① 인스펙터에 연결하지 않았다면 씬에서 찾아본다 (이름으로)
        if (characterPickerPanel == null && canvas != null)
        {
            Transform found = FindDeepChild(canvas.transform, "CharacterPicker");
            if (found != null) characterPickerPanel = found.gameObject;
        }

        // ② 그래도 없으면 프리팹을 한 번 찍는다
        if (characterPickerPanel == null && canvas != null)
        {
            GameObject prefab = Resources.Load<GameObject>(PickerResourcePath);
            if (prefab != null)
            {
                characterPickerPanel = Instantiate(prefab, canvas.transform);
                characterPickerPanel.name = prefab.name;
            }
        }

        if (characterPickerPanel == null) return;   // 코드 생성 경로로 넘어간다

        ResolvePickerParts();

        // ★ 팝업이 이제야 화면에 등장했으므로, 그 안의 폰트에도 폴백을 걸어 준다.
        //   폴백은 폰트 에셋 단위라, 나중에 나타난 에셋은 새로 걸어야 한다.
        UiFontResolver.EnsureSymbolFallback();

        characterPickerPanel.SetActive(false);
        _pickerReady = true;

        if (!UiFontResolver.CanRender('✓'))
        {
            Debug.LogWarning("[DeckBuilder] 체크 기호(✓)를 그릴 폰트를 찾지 못해 대체 기호를 씁니다.");
        }
    }

    /// <summary>
    /// 팝업 안에서 제목·목록 부모·줄 템플릿을 알아낸다.
    ///
    /// 프리팹은 실행 화면을 그대로 캡처한 모양이라 <b>예시 줄이 여러 개 들어 있다.</b>
    /// 그중 첫 줄을 템플릿으로 삼고 나머지는 치운다 — 남겨 두면 엉뚱한 목록이 함께 보인다.
    /// </summary>
    private void ResolvePickerParts()
    {
        Transform panel = characterPickerPanel.transform;

        // 줄 = Button을 가진 자식. 하이어라키 순서대로 모은다.
        var rows = new List<GameObject>();
        foreach (Transform childTransform in panel)
        {
            if (childTransform.GetComponent<Button>() != null) rows.Add(childTransform.gameObject);
        }

        if (characterPickerRowPrefab == null && rows.Count > 0)
        {
            characterPickerRowPrefab = rows[0];
            characterPickerRowPrefab.SetActive(false);   // 템플릿은 늘 꺼 둔다
        }

        // 예시로 들어 있던 나머지 줄은 제거한다
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == characterPickerRowPrefab) continue;
            Destroy(rows[i]);
        }

        if (characterPickerContent == null)
            characterPickerContent = panel;

        // 제목 = 줄에 속하지 않은 텍스트
        if (characterPickerTitle == null)
        {
            foreach (TextMeshProUGUI text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.GetComponentInParent<Button>() != null) continue;

                characterPickerTitle = text;
                break;
            }
        }

        Debug.Log($"[DeckBuilder] 용병 선택 팝업 준비 완료 — 템플릿 " +
                  $"{(characterPickerRowPrefab != null ? characterPickerRowPrefab.name : "없음(코드 생성)")}");
    }

    /// <summary>이름으로 자손을 찾는다(비활성 포함).</summary>
    private static Transform FindDeepChild(Transform root, string targetName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == targetName) return t;

        return null;
    }

    /// <summary>팝업을 켜고 목록을 비운다. 위치·크기는 건드리지 않는다.</summary>
    private RectTransform PrepareScenePicker()
    {
        characterPickerPanel.SetActive(true);
        _openPickerPanel = characterPickerPanel;

        Transform parent = characterPickerContent != null
            ? characterPickerContent
            : characterPickerPanel.transform;

        return parent as RectTransform;
    }

    /// <summary>씬에 팝업이 없을 때 코드로 만든다(예전 방식).</summary>
    private RectTransform PrepareGeneratedPicker()
    {
        Canvas canvas = deckContent != null ? deckContent.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;

        _generatedPickerPanel = new GameObject(
            "CharacterPicker", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));

        var rect = (RectTransform)_generatedPickerPanel.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 520f);
        rect.anchoredPosition = Vector2.zero;
        _generatedPickerPanel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.15f, 0.97f);

        var layout = _generatedPickerPanel.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childForceExpandHeight = false;

        _openPickerPanel = _generatedPickerPanel;
        return rect;
    }

    /// <summary>
    /// 목록 한 줄을 더한다.
    /// 프리팹을 연결했으면 그것을 복제하고(글자만 채운다), 없으면 코드로 만든다.
    /// </summary>
    private void AddPickerRow(
        RectTransform parent, string text, bool interactable, System.Action onClick, bool highlight = false)
    {
        GameObject go = characterPickerRowPrefab != null
            ? Instantiate(characterPickerRowPrefab, parent)
            : BuildPickerRowObject(parent);

        go.name = "Row";   // (Clone) 꼬리표 제거

        go.SetActive(true);
        _pickerRows.Add(go);

        // 글자: 프리팹이면 자식 텍스트를 찾고, 코드 생성이면 방금 만든 것을 찾는다
        var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            if (_uiFont != null) label.font = _uiFont;
            label.text = text;
            label.color = interactable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        // 상태별 색은 프리팹이든 코드 생성이든 똑같이 입힌다.
        // 템플릿 색을 그대로 쓰면 모든 줄이 같은 색이 되어 "현재/사용 중" 구분이 사라진다.
        // 색 자체는 인스펙터에서 바꿀 수 있다.
        var image = go.GetComponent<Image>();
        if (image != null)
            image.color = highlight ? pickerCurrentColor
                        : interactable ? pickerNormalColor
                        : pickerDisabledColor;

        var button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = interactable;
            if (interactable) button.onClick.AddListener(() => onClick());
        }
    }

    /// <summary>프리팹이 없을 때 쓸 기본 줄.</summary>
    private GameObject BuildPickerRowObject(RectTransform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 76f);

        go.AddComponent<LayoutElement>().minHeight = 76f;

        var label = CreateLabel(rect, string.Empty, 0f, 0f, 14.4f);
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return go;
    }

    /// <summary>용병 창을 연 프레임. 그 프레임의 바깥 클릭 판정은 건너뛴다.</summary>
    private int _pickerOpenedFrame = -1;

    /// <summary>
    /// 용병 선택 창 <b>바깥</b>을 누르면 닫는다.
    ///
    /// ★ 클릭을 가로채지 않는다. 좌표만 보고 판단하므로 누른 버튼·카드는 제 할 일을 그대로 한다
    ///   (화면을 덮는 투명 버튼을 깔면 그 클릭이 삼켜진다 — 그 방식을 쓰지 않은 이유다).
    ///
    /// ★ 프레임 가드가 반드시 필요하다. 용병 슬롯 버튼은 창 <b>바깥</b>에 있어서,
    ///   창을 여는 그 클릭이 같은 프레임에 "바깥 클릭"으로도 읽힌다.
    ///   EventSystem과 이 Update의 실행 순서는 보장되지 않으므로,
    ///   연 프레임을 적어 두고 그 프레임은 건너뛴다. 이게 없으면 창이 열리자마자 닫힌다.
    /// </summary>
    private void Update()
    {
        if (_openPickerPanel == null) return;
        if (Time.frameCount == _pickerOpenedFrame) return;

        if (!PointerInput.TryGetPressPoint(out Vector2 point)) return;

        var panelRect = _openPickerPanel.transform as RectTransform;
        if (panelRect == null) return;

        // 창 안을 눌렀다면 그 줄이 알아서 처리한다.
        if (RectTransformUtility.RectangleContainsScreenPoint(panelRect, point, ResolvePickerCamera()))
            return;

        CloseCharacterPicker();
    }

    /// <summary>창이 올라탄 캔버스의 이벤트 카메라. 오버레이면 null이어야 좌표가 맞는다.</summary>
    private Camera ResolvePickerCamera()
    {
        if (_openPickerPanel == null) return null;

        Canvas canvas = _openPickerPanel.GetComponentInParent<Canvas>();
        if (canvas == null) return null;

        Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    private void CloseCharacterPicker()
    {
        // 씬에 만들어 둔 팝업은 **끄기만** 한다. 파괴하면 다음에 열 수 없다.
        foreach (GameObject row in _pickerRows)
        {
            // 템플릿은 지우면 안 된다 — 다음에 열 때 쓸 원본이다
            if (row != null && row != characterPickerRowPrefab) Destroy(row);
        }

        _pickerRows.Clear();

        if (characterPickerPanel != null) characterPickerPanel.SetActive(false);

        if (_generatedPickerPanel != null)
        {
            Destroy(_generatedPickerPanel);
            _generatedPickerPanel = null;
        }

        _openPickerPanel = null;
    }

    /// <summary>슬롯에 용병을 넣거나 비운다. 비울 때 그 용병 카드가 덱에 있으면 함께 뺀다.</summary>
    private void SetCharacterSlot(int slotIndex, string characterId)
    {
        while (_selectedCharacters.Count <= slotIndex) _selectedCharacters.Add(null);

        string previous = _selectedCharacters[slotIndex];
        _selectedCharacters[slotIndex] = characterId;

        // 빠진 용병의 카드는 덱에 남겨 둘 수 없다 (목록에서 사라져 뺄 방법이 없어진다)
        if (!string.IsNullOrEmpty(previous) && previous != characterId)
        {
            int removed = myDeck.RemoveAll(id =>
            {
                CardData data = CardDataManager.Instance?.GetCard(id);
                return data != null && data.characterId == previous;
            });

            if (removed > 0)
                ShowMessagePopup($"용병을 바꿔 그 용병의 카드 {removed}장을 덱에서 뺐습니다.");
        }

        CloseCharacterPicker();
        RefreshAllUI();
    }

    /// <summary>덱에 든 카드로부터 용병 슬롯을 복원한다 (덱을 불러올 때).</summary>
    private void SyncCharactersFromDeck(List<string> explicitCharacters)
    {
        _selectedCharacters.Clear();

        // 저장된 명시값이 있으면 그대로 쓴다
        if (explicitCharacters != null)
        {
            foreach (string id in explicitCharacters)
                if (!string.IsNullOrEmpty(id) && !_selectedCharacters.Contains(id))
                    _selectedCharacters.Add(id);
        }

        // 구버전 덱(명시값 없음)은 카드에서 역산한다
        foreach (string owner in GetDeckCharacters())
            if (!_selectedCharacters.Contains(owner) && _selectedCharacters.Count < MAX_DECK_CHARACTERS)
                _selectedCharacters.Add(owner);
    }

    private void RefreshCharacterSlotUI()
    {
        for (int i = 0; i < _slotViews.Count; i++)
        {
            CharacterSlotView view = _slotViews[i];
            if (view == null) continue;

            string id = i < _selectedCharacters.Count ? _selectedCharacters[i] : null;
            CharacterData character = CardDataManager.Instance != null
                ? CardDataManager.Instance.GetCharacter(id)
                : null;

            if (view.label != null)
                view.label.text = character != null ? character.name : "용병 선택 +";

            if (view.background != null)
                view.background.color = character != null
                    ? new Color(0.28f, 0.34f, 0.5f, 1f)
                    : new Color(0.24f, 0.24f, 0.28f, 1f);

            if (view.portrait != null)
            {
                Sprite sprite = character != null ? CardImageLoader.LoadSprite(character.imagePath) : null;
                view.portrait.sprite = sprite;
                view.portrait.gameObject.SetActive(sprite != null);
            }
        }

        if (_editingNameLabel != null)
            _editingNameLabel.text = string.IsNullOrEmpty(_editingDeckName)
                ? "편집 중: -"
                : $"편집 중: {_editingDeckName}";
    }

    /// <summary>이 카드가 지금 고른 용병의 것인가.</summary>
    private bool IsCardUnlocked(string cardId)
    {
        if (_selectedCharacters.Count == 0) return false;

        CardData data = CardDataManager.Instance?.GetCard(cardId);
        return data != null && _selectedCharacters.Contains(data.characterId);
    }

    // ---------------------------------------------------
    // 덱 / 덱리스트 관련
    // ---------------------------------------------------

    /// <summary>덱 목록이 비었을 때 드롭다운에 띄우는 문구.</summary>
    private const string EMPTY_DECK_LABEL = "덱이 없습니다.";


    // 덱 리스트 정리
    public void RefreshDeckList(string focusDeckName = null)
    {
        // ★ 목록을 다시 채우면 dropdown.value가 바뀌고, 그러면 OnDeckSelected가 불린다.
        //   저장 → RefreshDeckList → 값 변경 → 불러오기 → … 로 되도는 것을 여기서 끊는다.
        bool previous = _suppressDropdownEvent;
        _suppressDropdownEvent = true;

        try { RefreshDeckListInternal(focusDeckName); }
        finally { _suppressDropdownEvent = previous; }

        _lastAppliedDropdownIndex = deckListDropdown != null ? deckListDropdown.value : 0;
    }

    private void RefreshDeckListInternal(string focusDeckName)
    {
        // 1. 드롭다운 초기화 (기존 목록 지우기)
        deckListDropdown.ClearOptions();

        // 2. 해당 폴더의 모든 .json 파일 경로를 가져옴
        string folderPath = DeckStorage.EnsureFolder();
        string[] filePaths = DeckStorage.GetDeckFiles();


        List<string> options = new List<string>();

        // 3. 파일 경로에서 "파일 이름"만 쏙 빼서 목록에 추가
        foreach (string path in filePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            options.Add(fileName);
        }

        if (options.Count == 0)
        {
            // ★ 예전에는 여기서 CreateStarterDeck()이 존재하지 않는 카드 ID("11001")로 덱을 만들었다.
            //   그 다음 RefreshDeckUI가 null 카드를 그대로 역참조해 **화면이 터졌다.**
            //   새 설치나 저장 경로가 바뀐 복사본에서 바로 만나는 상황이라 죽은 코드를 걷어냈다.
            //   덱이 없으면 그냥 빈 상태로 두고 [새 덱 만들기]를 쓰게 한다.
            options.Add(EMPTY_DECK_LABEL);
            deckListDropdown.interactable = false;
            deckListDropdown.AddOptions(options);

            myDeck.Clear();
            _selectedCharacters.Clear();
            _editingDeckName = "";
            if (deckNameInput != null) deckNameInput.text = "";

            // 남은 덱이 없으니 기준점도 빈 상태로 옮긴다.
            // 안 그러면 지운 덱의 내용이 기준으로 남아 곧바로 "미저장"으로 잡힌다.
            MarkSaved();

            RefreshAllUI();
            ShowMessagePopup("저장된 덱이 없습니다.\n[새 덱 만들기]로 시작하세요.");
            return;
        }

        // 3. 드롭다운 목록 채우기
        deckListDropdown.interactable = true;
        deckListDropdown.AddOptions(options);

        int targetIndex = 0;

        if (string.IsNullOrEmpty(focusDeckName) == false)
        {
            // 방금 저장한 이름이 목록의 몇 번째에 있는지 찾습니다.
            int findIndex = options.IndexOf(focusDeckName);
            if (findIndex >= 0)
            {
                targetIndex = findIndex;
            }
        }

        // 4. 드롭다운의 값을 변경합니다.
        deckListDropdown.value = targetIndex;

        if (deckListDropdown.options.Count > targetIndex)
        {
            string deckName = deckListDropdown.options[targetIndex].text;
            if (deckName != EMPTY_DECK_LABEL)
            {
                LoadDeckFromJson(deckName + ".json");
                RememberSelectedDeck(deckName);
            }
        }
    }

    /// <summary>
    /// 드롭다운에서 덱을 고르면 곧바로 불러온다.
    ///
    /// ★ 되먹임 방어가 두 겹이다.
    ///   ① <c>_suppressDropdownEvent</c> — 코드가 값을 바꿀 때(RefreshDeckList 등)는 무시한다
    ///   ② 이미 열려 있는 덱과 같으면 무시 — ①을 빠뜨려도 무한히 돌지는 않는다
    /// </summary>
    public void OnDeckSelected(int index)
    {
        if (_suppressDropdownEvent) return;
        if (deckListDropdown == null) return;
        if (index < 0 || index >= deckListDropdown.options.Count) return;

        string selectedName = deckListDropdown.options[index].text;
        if (selectedName == EMPTY_DECK_LABEL) return;
        if (selectedName == _editingDeckName) return;

        RequestLoadDeck(selectedName);
    }

    public void OnClickLoadDeckButton()
    {
        // 1. 현재 드롭다운의 인덱스 가져오기
        int index = deckListDropdown.value;

        // 2. 그 번호에 해당하는 덱 이름 가져오기
        string selectedName = deckListDropdown.options[index].text;

        // 3. 덱이 없을 경우
        if (selectedName == EMPTY_DECK_LABEL)
        {
            Debug.Log("불러올 덱이 없습니다.");
            return;
        }

        RequestLoadDeck(selectedName);
    }

    /// <summary>실제 불러오기 + 선택 동기화. 미저장 경고 이후에도 이 경로로 모인다.</summary>
    private void LoadDeckSelected(string selectedName)
    {
        // ★ 예전에는 LoadDeckFromJson을 두 번 불렀다(팝업 띄우고 또 로드).
        //   동작은 했지만 불필요하고, 팝업 뒤에 다시 덮어써 혼란스러웠다.
        bool isSuccess = LoadDeckFromJson(selectedName + ".json");

        if (isSuccess)
        {
            RememberSelectedDeck(selectedName);

            // ★ 실제로 연 덱에 드롭다운을 맞춘다.
            //   [저장]을 거쳐 온 경우 SaveDeckToJson이 RefreshDeckList로 드롭다운을
            //   '방금 저장한 덱'에 옮겨 놓기 때문에, 여기서 바로잡지 않으면
            //   화면은 목표 덱인데 드롭다운은 다른 덱을 가리킨다.
            SyncDropdownTo(selectedName);

            ShowMessagePopup($"{selectedName}을(를) 불러왔습니다.");
        }

        Debug.Log($"'{selectedName}' 덱을 불러왔습니다.");
    }

    // 전체 카드 리스트 생성 (처음에 한 번만 실행)
    void InitCollection()
    {
        foreach (var kvp in CardDataManager.Instance.cardDic)
        {
            string id = kvp.Key;
            CardData data = kvp.Value;

            // 프리팹 생성
            GameObject go = Instantiate(cardPrefab, collectionContent);
            CardUI ui = go.GetComponent<CardUI>();

            // 정보 입력 (처음엔 덱에 0장 있으므로 개수는 0)
            ui.Setup(id, 0, data.max_deck_count, this, false);

            // 리스트에 등록해둠 (나중에 개수 갱신할 때 쓰려고)
            collectionSlots.Add(ui);

            LongPressTrigger trigger = go.GetComponent<LongPressTrigger>();
            if (trigger != null)
            {
                trigger.cardId = id; // "너는 11001번이야!" 명찰 달기
            }
        }
    }

    // ---------------------------------------------------
    // 카드 추가/제거 로직
    // ---------------------------------------------------

    /// <summary>룰북 기본값 — 메인덱은 10종류 × 각 2장 = 20장이므로 카드당 상한은 2다.</summary>
    private const int DEFAULT_MAX_COPIES = 2;

    public void AddCard(string id)
    {
        // 1. 전체 20장 제한 체크
        if (myDeck.Count >= MAX_DECK_COUNT)
        {
            Debug.Log("덱이 가득 찼습니다!");
            return;
        }

        CardData data = CardDataManager.Instance != null ? CardDataManager.Instance.GetCard(id) : null;
        if (data == null)
        {
            Debug.LogWarning($"[DeckBuilder] 카드 데이터를 찾을 수 없다: {id}");
            return;
        }

        // 고른 용병의 카드만 넣을 수 있다. 목록에서 이미 걸러지지만 마지막으로 한 번 더 본다.
        if (!IsCardUnlocked(id))
        {
            ShowMessagePopup("먼저 상단에서 용병을 고르세요. 고른 용병의 카드만 덱에 넣을 수 있습니다.");
            return;
        }

        // 2. 카드별 장수 제한 체크
        // ★ JSON에 max_deck_count가 없으면 0으로 파싱돼 "0 < 0"이 되고,
        //   추가 버튼이 아무 반응 없이 죽는다(실제로 그랬다). 값이 없으면 룰북 기본값으로 본다.
        int maxCopies = data.max_deck_count > 0 ? data.max_deck_count : DEFAULT_MAX_COPIES;
        int currentCount = myDeck.Count(x => x == id);

        if (currentCount >= maxCopies)
        {
            Debug.Log($"[DeckBuilder] '{data.name}'은(는) 최대 {maxCopies}장까지만 넣을 수 있다.");
            return;
        }

        myDeck.Add(id);
        RefreshAllUI(); // 화면 갱신
    }

    public void RemoveCard(string id)
    {
        if (myDeck.Contains(id))
        {
            myDeck.Remove(id);
            RefreshAllUI(); // 화면 갱신
        }
    }
    // ---------------------------------------------------
    //  텍스트 갱신
    // ---------------------------------------------------
    void UpdateDeckCountText()
    {
        if (deckCountText != null)
        {
            int currenCount = myDeck.Count;

            deckCountText.text = $"{currenCount} / {MAX_DECK_COUNT}";
        }
    }

    // ---------------------------------------------------
    // 화면 갱신 로직
    // ---------------------------------------------------

    void RefreshAllUI()
    {
        RefreshCharacterSlotUI(); // 용병 슬롯이 카드 목록의 기준이므로 먼저
        RefreshDeckUI();       // 위쪽 화면 다시 그리기
        RefreshCollectionUI(); // 아래쪽 화면 숫자 바꾸기

        UpdateDeckCountText();

        // 덱 장수 텍스트 갱신 (예: 12/20)
        if (deckCountText) deckCountText.text = $"{myDeck.Count}/20";
    }

    // 위쪽: 내 덱 리스트는 매번 지우고 다시 그립니다 (순서 정렬 등을 위해)
    void RefreshDeckUI()
    {
        // 기존 슬롯 다 삭제
        foreach (Transform child in deckContent) Destroy(child.gameObject);

        // ★ 추가한 순서가 아니라 룰북 순서로 보여 준다
        //   (엘리 → 베로니카 → 다이나 → 소니아, 각 용병 안에서 공격 → 방어 → 지원)
        List<string> uniqueIDs = SortByRulebookOrder(myDeck.Distinct().ToList());

        foreach (string id in uniqueIDs)
        {
            CardData data = CardDataManager.Instance?.GetCard(id);
            if (data == null)
            {
                Debug.LogWarning($"[DeckBuilder] 알 수 없는 카드가 덱에 있다(건너뜀): {id}");
                continue;
            }

            GameObject go = Instantiate(cardPrefab, deckContent);
            CardUI ui = go.GetComponent<CardUI>();

            int countInDeck = myDeck.Count(x => x == id);

            ui.Setup(id, countInDeck, data.max_deck_count, this, true);

            if (ui.removeAllButton != null)
            {
                ui.removeAllButton.gameObject.SetActive(true);
            }

            LongPressTrigger trigger = go.GetComponent<LongPressTrigger>();
            if (trigger != null)
            {
                trigger.cardId = id; // "너는 11001번이야!" 명찰 달기
            }
        }
    }

    // 아래쪽: 전체 목록은 지우지 않고 '숫자'만 바꿉니다. (스크롤 위치 유지 위함)
    void RefreshCollectionUI()
    {
        foreach (CardUI slot in collectionSlots)
        {
            if (slot == null) continue;

            CardData data = CardDataManager.Instance?.GetCard(slot.myCardID);
            if (data == null)
            {
                // 카드 데이터가 사라진 경우(구버전 덱 등). 예전엔 여기서 null을 그대로 역참조해 터졌다.
                slot.gameObject.SetActive(false);
                continue;
            }

            // ★ 고른 용병의 카드만 보여 준다. 슬롯을 지우지 않고 켜고 끄기만 해
            //   스크롤 위치가 유지된다.
            bool unlocked = IsCardUnlocked(slot.myCardID);
            if (slot.gameObject.activeSelf != unlocked) slot.gameObject.SetActive(unlocked);
            if (!unlocked) continue;

            int count = myDeck.Count(x => x == slot.myCardID);
            slot.UpdateCount(count, data.max_deck_count);
        }
    }
    // ---------------------------------------------------
    // 덱 저장, 삭제
    // ---------------------------------------------------
    // 다른데서 덱 가져올 때 사용
    public List<string> GetCurrentDeck()
    {
        return myDeck;
    }

    /// <summary>메인덱에 들어갈 수 있는 용병 테마 수. 룰북상 용병 2종을 골라 그 테마로만 덱을 짠다.</summary>
    private const int MAX_DECK_CHARACTERS = 2;

    /// <summary>씬의 [덱 저장하기] 버튼. 결과는 쓰지 않는다.</summary>
    public void OnClickSaveDeck() => TrySaveDeck();

    /// <summary>검증까지 마치고 실제로 저장했으면 true. 확인 팝업의 [저장]이 이 결과를 본다.</summary>
    private bool TrySaveDeck()
    {
        // 1. 장수 체크 (20장인지 확인)
        if (myDeck.Count != MAX_DECK_COUNT)
        {
            // 20장이 아니면 경고 팝업 띄우기
            if (PopupPanel != null) PopupPanel.SetActive(true);
            if (warningPopup != null) warningPopup.SetActive(true);

            Debug.Log("저장 실패: 덱이 완성되지 않았습니다.");
            return false;
        }

        // 2. 용병 테마 체크 (2종까지)
        //    카드당 장수는 강제하지 않는다 — 룰북은 2장씩이지만 현재 1장도 허용하는 방침이다.
        List<string> characters = GetDeckCharacters();
        if (characters.Count > MAX_DECK_CHARACTERS)
        {
            ShowMessagePopup(
                $"용병은 최대 {MAX_DECK_CHARACTERS}종까지만 섞을 수 있습니다.\n" +
                $"현재 {characters.Count}종: {string.Join(", ", characters)}");
            Debug.Log($"저장 실패: 용병 {characters.Count}종 ({string.Join(", ", characters)})");
            return false;
        }

        // 3. 저장 진행
        return SaveDeckToJson();
    }

    /// <summary>
    /// 룰북 순서로 정렬한다.
    /// <see cref="CardDataManager.GetSortOrder"/> 하나로 "용병 순서"와 "공격→방어→지원"이 모두 해결된다
    /// (RulebookCards.json이 이미 그 순서로 쓰여 있다).
    /// </summary>
    private List<string> SortByRulebookOrder(List<string> ids)
    {
        if (CardDataManager.Instance == null) return ids;

        return ids.OrderBy(id => CardDataManager.Instance.GetSortOrder(id)).ToList();
    }

    // ─────────────────────────────────────────────────────────────
    // 미저장 보호
    //
    // 저장하지 않은 편집을 안고 다른 덱이나 씬으로 넘어가려 하면 먼저 물어본다.
    // 두 입구(RequestLoadDeck · TryLeaveToScene)가 "대기 중인 동작" 하나로 모이고,
    // 확인 팝업의 세 버튼이 그 대기 동작을 실행하거나 취소한다.
    //
    // ★ 예전에는 경고만 띄우고 _savedSnapshot을 현재 덱으로 덮어썼다.
    //   "한 번 더 누르면 진행"을 위한 임시방편이었는데, 그 순간 미저장 상태가 지워져
    //   이후에는 아무 경고 없이 편집이 날아갔다. 그래서 걷어냈다.
    // ─────────────────────────────────────────────────────────────

    private enum PendingAction { None, LoadDeck, LeaveScene }

    private PendingAction _pendingAction = PendingAction.None;
    private string _pendingArg;

    /// <summary>코드가 드롭다운 값을 바꾸는 동안 OnDeckSelected를 재우는 표시.</summary>
    private bool _suppressDropdownEvent;

    /// <summary>지금 실제로 열려 있는 덱의 드롭다운 자리. [취소]는 이 자리로 되돌린다.</summary>
    private int _lastAppliedDropdownIndex;

    // ── 저장 기준점 ────────────────────────────────────────────────
    // 카드·용병·이름 셋을 함께 찍는다. 하나만 봐서는 용병만 바꾸거나
    // 이름만 고친 변경을 놓친다.

    private List<string> _savedSnapshot = new List<string>();
    private List<string> _savedCharacters = new List<string>();
    private string _savedDeckName = "";

    /// <summary>지금 상태를 "저장된 것"으로 삼는다. 저장·불러오기·새 덱 직후에 부른다.</summary>
    private void MarkSaved()
    {
        _savedSnapshot = new List<string>(myDeck);
        _savedCharacters = new List<string>(_selectedCharacters);
        _savedDeckName = NormalizeDeckName(deckNameInput != null ? deckNameInput.text : _editingDeckName);
    }

    /// <summary>저장하지 않은 편집이 있는가. 카드·용병·덱 이름 셋을 본다.</summary>
    private bool HasUnsavedChanges()
    {
        // 카드 — 순서는 상관없다. 구성만 비교한다
        if (myDeck.Count != _savedSnapshot.Count) return true;

        var a = SortByRulebookOrder(new List<string>(myDeck));
        var b = SortByRulebookOrder(new List<string>(_savedSnapshot));

        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return true;

        // 용병 — 슬롯 순서가 곧 1번·2번이므로 순서까지 본다
        if (_selectedCharacters.Count != _savedCharacters.Count) return true;

        for (int i = 0; i < _selectedCharacters.Count; i++)
            if (_selectedCharacters[i] != _savedCharacters[i]) return true;

        // 덱 이름
        // ★ 다듬어서 비교한다. 보이지 않는 글자 하나 때문에 "미저장"으로 잡히면 안 된다.
        string currentName = NormalizeDeckName(deckNameInput != null ? deckNameInput.text : _editingDeckName);
        return currentName != _savedDeckName;
    }

    // ── 입구 두 곳 ────────────────────────────────────────────────

    /// <summary>덱을 연다. 저장하지 않은 편집이 있으면 먼저 물어본다.</summary>
    private void RequestLoadDeck(string deckName)
    {
        if (string.IsNullOrEmpty(deckName) || deckName == EMPTY_DECK_LABEL) return;

        if (HasUnsavedChanges())
        {
            ShowSaveConfirm(PendingAction.LoadDeck, deckName,
                $"저장하지 않은 변경이 있습니다.\n'{deckName}'을(를) 열기 전에 저장할까요?");
            return;
        }

        LoadDeckSelected(deckName);
    }

    /// <summary>
    /// 씬을 나간다. 저장하지 않은 편집이 있으면 먼저 물어본다.
    /// 씬의 [메인 메뉴] 버튼 onClick을 <c>SceneChanger.ChageScene</c> 대신 이것으로 잇는다.
    /// </summary>
    public void TryLeaveToScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[DeckBuilder] 나갈 씬 이름이 비어 있다. 버튼의 인자를 확인할 것.");
            return;
        }

        if (HasUnsavedChanges())
        {
            ShowSaveConfirm(PendingAction.LeaveScene, sceneName,
                "저장하지 않은 변경이 있습니다.\n나가기 전에 저장할까요?");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    // ── 확인 팝업 ─────────────────────────────────────────────────

    private void ShowSaveConfirm(PendingAction action, string arg, string message)
    {
        _pendingAction = action;
        _pendingArg = arg;

        if (saveConfirmPopup == null)
        {
            // 팝업을 안 만들었다면 편집을 잃게 두느니 이동을 막는다.
            Debug.LogError("[DeckBuilder] 저장 확인 팝업(saveConfirmPopup)이 연결되지 않았다. "
                           + "저장하지 않은 변경이 있어 이동을 취소한다.");
            ShowMessagePopup(message);
            CancelPendingAction();
            return;
        }

        if (saveConfirmText != null) saveConfirmText.text = message;

        if (PopupPanel != null) PopupPanel.SetActive(true);
        saveConfirmPopup.SetActive(true);
    }

    /// <summary>[저장] — 저장에 성공했을 때만 넘어간다.</summary>
    public void OnClickSaveAndContinue()
    {
        PendingAction action = _pendingAction;
        string arg = _pendingArg;

        HideSaveConfirm();

        if (!TrySaveDeck())
        {
            // 20장이 아니거나 용병이 3종이면 저장이 거절된다. 그 경고는 TrySaveDeck이 띄웠다.
            // 넘어가지 않고 그 자리에 머문다 — 드롭다운도 되돌린다.
            CancelPendingAction();
            return;
        }

        RunPendingAction(action, arg);
    }

    /// <summary>[저장안함] — 변경을 버리고 넘어간다.</summary>
    public void OnClickDiscardAndContinue()
    {
        PendingAction action = _pendingAction;
        string arg = _pendingArg;

        HideSaveConfirm();
        ClosePopup();

        RunPendingAction(action, arg);
    }

    /// <summary>[취소] — 편집을 이어간다. 드롭다운을 원래 자리로 되돌린다.</summary>
    public void OnClickCancelPendingAction()
    {
        HideSaveConfirm();
        CancelPendingAction();
        ClosePopup();
    }

    private void RunPendingAction(PendingAction action, string arg)
    {
        _pendingAction = PendingAction.None;
        _pendingArg = null;

        switch (action)
        {
            case PendingAction.LoadDeck:
                ClosePopup();
                LoadDeckSelected(arg);
                break;

            case PendingAction.LeaveScene:
                SceneManager.LoadScene(arg);
                break;
        }
    }

    /// <summary>대기 중인 동작을 지우고 드롭다운을 지금 열려 있는 덱으로 되돌린다.</summary>
    private void CancelPendingAction()
    {
        bool hadPending = _pendingAction != PendingAction.None;

        _pendingAction = PendingAction.None;
        _pendingArg = null;

        // 드롭다운 때문에 뜬 확인이었다면, 안 고른 것으로 되돌려야 화면이 어긋나지 않는다.
        if (hadPending) SetDropdownValueSilently(_lastAppliedDropdownIndex);
    }

    private void HideSaveConfirm()
    {
        if (saveConfirmPopup != null) saveConfirmPopup.SetActive(false);
    }

    /// <summary>드롭다운을 그 이름의 덱으로 맞춘다. 목록에 없으면 그대로 둔다.</summary>
    private void SyncDropdownTo(string deckName)
    {
        if (deckListDropdown == null || string.IsNullOrEmpty(deckName)) return;

        for (int i = 0; i < deckListDropdown.options.Count; i++)
        {
            if (deckListDropdown.options[i].text != deckName) continue;

            SetDropdownValueSilently(i);
            return;
        }
    }

    /// <summary>OnDeckSelected를 깨우지 않고 드롭다운 값을 바꾼다.</summary>
    private void SetDropdownValueSilently(int index)
    {
        if (deckListDropdown == null) return;
        if (index < 0 || index >= deckListDropdown.options.Count) return;

        bool previous = _suppressDropdownEvent;
        _suppressDropdownEvent = true;

        try
        {
            deckListDropdown.value = index;
            deckListDropdown.RefreshShownValue();
        }
        finally { _suppressDropdownEvent = previous; }

        _lastAppliedDropdownIndex = index;
    }

    /// <summary>현재 덱에 들어 있는 용병 테마 목록 (카드의 characterId 기준).</summary>
    private List<string> GetDeckCharacters()
    {
        var characters = new List<string>();
        if (CardDataManager.Instance == null) return characters;

        foreach (string id in myDeck)
        {
            CardData data = CardDataManager.Instance.GetCard(id);
            string owner = data != null ? data.characterId : null;

            if (!string.IsNullOrEmpty(owner) && !characters.Contains(owner))
                characters.Add(owner);
        }

        return characters;
    }

    /// <summary>
    /// 덱 이름을 다듬는다. 앞뒤 공백과 <b>보이지 않는 글자</b>를 뺀다.
    ///
    /// ★ 이게 없으면 "저장했는데 새 덱이 하나 더 생기는" 일이 난다.
    ///   눈에는 같은 이름인데 끝에 공백이나 제로폭 공백(U+200B)이 하나 붙어 있으면
    ///   "지금 편집 중인 그 덱"과 문자열 비교가 어긋나기 때문이다.
    ///   실제로 이 씬의 덱 이름 칸에는 제로폭 공백이 박혀 있었다.
    /// </summary>
    private static string NormalizeDeckName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            // 제로폭 공백·비결합자·BOM — 화면에는 없고 비교에만 남는 글자들
            if (c == '\u200B' || c == '\u200C' || c == '\u200D' || c == '\uFEFF') continue;

            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    // JSON 저장
    //
    // ★ 규칙: <b>이름칸의 이름으로 저장하고, 그 이름의 덱이 이미 있으면 덮어쓴다.</b>
    //   예전에는 "지금 편집 중인 덱과 이름이 같을 때만" 덮어쓰고, 아니면 뒤에 _1, _2를 붙여
    //   새 파일을 만들었다. 그런데 그 판정이 어긋나는 순간(공백 한 칸이면 충분하다)
    //   <b>고치던 덱은 그대로 두고 사본이 쌓였다</b> — 실제로 MyDeck_1 … _7이 그렇게 생겼다.
    //   새 덱은 [새로운 덱 만들기]가 겹치지 않는 이름을 붙여 주므로 자동 번호는 필요 없다.
    bool SaveDeckToJson()
    {
        string deckName = NormalizeDeckName(deckNameInput != null ? deckNameInput.text : null);
        if (string.IsNullOrEmpty(deckName))
        {
            ShowMessagePopup("덱 이름을 입력해 주세요.");
            Debug.Log("덱 이름 입력하시오");
            return false;
        }

        // 다듬은 이름을 화면에도 되돌려 놓는다 — 다음 비교부터는 어긋날 일이 없다
        if (deckNameInput != null && deckNameInput.text != deckName) deckNameInput.text = deckName;

        // 저장할 데이터 객체 만들기
        DeckSaveData data = new DeckSaveData();

        // ★ 룰북 순서로 재배열해서 저장한다. 다음에 열었을 때 항상 같은 순서로 보인다.
        data.cardIdList = SortByRulebookOrder(new List<string>(myDeck));

        // ★ 고른 용병을 명시적으로 남긴다.
        //   카드에서 역산하면 "용병 2명을 골랐지만 한쪽 카드만 넣은 덱"의 의도가 사라진다.
        data.characterIdList = _selectedCharacters.Where(c => !string.IsNullOrEmpty(c)).ToList();

        // JSON 문자열로 변환
        string json = JsonUtility.ToJson(data, true);

        // 저장할 경로, 이름 설정 (PC, 모바일 모두 작동하는 경로)
        string folderPath = DeckStorage.EnsureFolder();

        string path = Path.Combine(folderPath, deckName + ".json");

        bool existed = File.Exists(path);

        // ★ 저장하기 전의 편집 대상. 아래에서 _editingDeckName을 덮어쓰기 때문에 지금 붙잡아 둔다.
        string previousName = _editingDeckName;

        // 이름칸을 고쳐서 저장했는가 = 이름 바꾸기다.
        bool renamed = !string.IsNullOrEmpty(previousName) && previousName != deckName;

        // 편집하던 덱이 아닌 다른 덱을 덮어쓰는 경우다. 조용히 지나가면 안 된다.
        bool replacedAnotherDeck = existed && deckName != previousName;

        // 파일 쓰기 — 같은 이름이 있으면 그 자리에 덮어쓴다
        File.WriteAllText(path, json);

        // ★ 이름을 바꿨다면 예전 파일을 지운다 — 사본을 만드는 게 아니라 <b>이름 바꾸기</b>다.
        //   순서가 중요하다: 먼저 쓰고 나중에 지운다.
        //   반대로 하면 쓰기가 실패했을 때 덱이 통째로 사라진다.
        if (renamed) DeckStorage.DeleteDeck(previousName);

        // 방금 저장한 덱이 이제 편집 대상이다. 이어서 또 저장하면 이 파일을 덮어쓴다
        _editingDeckName = deckName;

        // 메인 화면과 선택을 맞춘다
        RememberSelectedDeck(deckName);

        if (deckNameInput != null) deckNameInput.text = deckName;

        Debug.Log(renamed
            ? $"저장 완료(이름 변경): {previousName} → {deckName}"
            : existed
                ? $"저장 완료(덮어쓰기): {deckName}"
                : $"저장 완료(새 덱): {deckName}");

        RefreshDeckList(deckName);

        // ★ 기준점은 목록·이름칸을 모두 정리한 뒤에 찍는다.
        //   위에서 찍으면 RefreshDeckList가 이름칸을 바꾸는 바람에 곧바로 "미저장"으로 잡혔다.
        MarkSaved();

        // ★ 덱이 사라지거나 덮어써졌다면 그 사실을 분명히 알린다.
        //   이름칸을 고쳐 저장하면 예전 이름의 덱이 없어지므로 조용히 지나가면 안 된다.
        string notice = null;

        if (renamed && replacedAnotherDeck)
            notice = $"'{previousName}'을(를) '{deckName}'(으)로 바꿔 저장했습니다." + "\n" +
                     $"이미 있던 '{deckName}'은(는) 덮어썼습니다.";
        else if (renamed)
            notice = $"'{previousName}'을(를) '{deckName}'(으)로 이름을 바꿨습니다.";
        else if (replacedAnotherDeck)
            notice = $"이미 있던 '{deckName}' 덱을 덮어썼습니다.";

        if (notice != null)
        {
            if (okPopup != null) okPopup.SetActive(false);
            ShowMessagePopup(notice);
        }
        else
        {
            if (PopupPanel != null) PopupPanel.SetActive(true);
            if (okPopup != null) okPopup.SetActive(true);
        }

        return true;
    }

    //JSON 파일 이름 받기
    bool LoadDeckFromJson(string fileName)
    {
        // 1. 경로 설정 (Assets 폴더 기준)
        string path = DeckStorage.GetDeckPath(fileName);


        // 2. 파일이 진짜 있는지 검사
        if (File.Exists(path) == false)
        {
            Debug.LogWarning("파일이 없습니다: " + path);
            return false; // 파일 없으면 실패(false) 반환
        }

        // 3. 파일 읽어오기
        string json = File.ReadAllText(path);

        // 4. JSON을 다시 데이터 객체로 변환
        DeckSaveData data = JsonUtility.FromJson<DeckSaveData>(json);

        // 5. 내 덱 리스트(myDeck)를 저장된 데이터로 덮어쓰기
        myDeck = data.cardIdList != null ? new List<string>(data.cardIdList) : new List<string>();

        // 용병 슬롯 복원 — 명시값이 있으면 그대로, 구버전 덱이면 카드에서 역산
        SyncCharactersFromDeck(data.characterIdList);

        // 6. 덱 이름 입력칸도 파일 이름으로 맞춰주기 (확장자 .json 제거)
        string loadedName = fileName.Replace(".json", "");
        if (deckNameInput != null)
        {
            deckNameInput.text = loadedName;
        }

        // 이제부터 [덱 저장하기]는 이 덱을 덮어쓴다
        _editingDeckName = loadedName;

        // ★ 미저장 판정 기준점은 이름칸까지 맞춘 뒤에 찍는다.
        //   먼저 찍으면 바로 아래에서 이름을 바꾸는 바람에 곧장 "미저장"으로 잡힌다.
        MarkSaved();

        // 7. 화면 갱신 (중요!)
        RefreshAllUI();

        return true; // 성공(true) 반환
    }

    // 덱 삭제
    public void OnClickDeleteDeckButton()
    {
        // 1. 드롭다운에서 현재 덱 가져오기
        int index = deckListDropdown.value;
        string selectedName = deckListDropdown.options[index].text;

        // 2. 예외처리
        if (selectedName == EMPTY_DECK_LABEL)
        {
            Debug.Log("삭제할 덱이 없습니다.");
            return;
        }

        // 3. 삭제할 이름을 기억
        deckToDelete = selectedName;

        // 4. 확인 팝업 텍스트 변경 후 띄우기
        if (deleteConfirmText != null)
        {
            deleteConfirmText.text = $"{selectedName}을(를) 삭제하시겠습니까?";
        }

        if (PopupPanel != null) PopupPanel.SetActive(true);
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(true);
    }

    // 팝업에서 확인 눌렀을 때 덱 삭제
    public void OnConfirmDelete()
    {
        // 파일 삭제. 이제 지운 덱은 다시 돌아오지 않는다
        //   (구 폴더에서 되살려 오던 DeckStorage의 이관 코드를 걷어냈다)
        DeckStorage.DeleteDeck(deckToDelete);

        // 편집 중이던 덱을 지웠다면 더 이상 덮어쓸 대상이 없다
        if (_editingDeckName == deckToDelete) _editingDeckName = "";

        // 확인 팝업 닫기
        ClosePopup();

        // 성공 팝업 띄우기
        ShowMessagePopup($"{deckToDelete}을(를)\n삭제하였습니다.");

        // 목록 갱신 (첫 번째 덱으로 초기화)
        RefreshDeckList(null);
    }

    // 내 덱에서 해당 카드 전부 제거
    public void RemoveAllCards(string id)
    {
        myDeck.RemoveAll(x => x == id);

        // 2. 화면 갱신
        RefreshAllUI();

    }

    // 새로운 덱 만들기
    public void OnClickNewDeckButton()
    {
        if (newDeckPopup != null)
        {
            if (PopupPanel != null) PopupPanel.SetActive(true);
            newDeckPopup.SetActive(true);
        }
    }

    public void OnConfirmNewDeck()
    {
        // 1. 덱 초기화 로직 (아까 만들었던 코드)
        myDeck.Clear();

        // ★ 용병도 함께 비운다. 이게 없으면 "새 덱"인데 직전 덱의 용병이 그대로 남아,
        //   이름과 달리 빈 덱이 아니게 된다.
        //   용병이 없으면 카드 목록도 비어 보이는데(IsCardUnlocked), 그것이 맞는 모습이다 —
        //   카드를 넣으려 하면 "먼저 상단에서 용병을 고르세요" 안내가 뜬다.
        _selectedCharacters.Clear();

        //if (deckNameInput != null) deckNameInput.text = "새 덱";  // 기존꺼

        // 2. [핵심] "새 덱" 이름 중복 검사 및 자동 번호 매기기
        string folderPath = DeckStorage.EnsureFolder();
        string baseName = "새 덱";
        string finalName = baseName;

        // 폴더가 있고, "새 덱.json" 파일이 이미 존재한다면?
        if (Directory.Exists(folderPath) && File.Exists(Path.Combine(folderPath, baseName + ".json")))
        {
            int maxNumber = 0;

            // "새 덱"으로 시작하는 모든 파일을 찾음
            string[] files = Directory.GetFiles(folderPath, baseName + "*.json");

            foreach (string filePath in files)
            {
                string fName = Path.GetFileNameWithoutExtension(filePath);

                // "새 덱_숫자" 형식인지 확인
                string prefix = baseName + "_";
                if (fName.StartsWith(prefix))
                {
                    string numPart = fName.Substring(prefix.Length);
                    if (int.TryParse(numPart, out int num))
                    {
                        if (num > maxNumber) maxNumber = num;
                    }
                }
            }

            // 가장 큰 숫자 다음 번호로 설정
            // 예: "새 덱", "새 덱_1"이 있으면 max는 1 -> 결과는 "새 덱_2"
            finalName = $"{baseName}_{maxNumber + 1}";
        }

        // 3. 결정된 이름을 입력칸에 넣기
        if (deckNameInput != null) deckNameInput.text = finalName;

        // 아직 파일로 존재하지 않는 덱이다. 첫 저장은 새로 만드는 것이 맞다
        _editingDeckName = "";

        // ★ 기준점을 여기로 옮긴다. 안 그러면 방금 만든 빈 덱이 곧바로 "미저장"으로 잡혀,
        //   아무것도 안 했는데 덱을 바꾸려 할 때마다 저장을 묻는다.
        MarkSaved();

        RefreshAllUI();

        // 2. 열려있는 확인 팝업 닫기
        ClosePopup();

        // 3. 성공 메시지 띄우기
        ShowMessagePopup("새 덱을 생성하였습니다.");

        Debug.Log("새 덱 생성 완료");
    }
    // ---------------------------------------------------
    // 팝업 관련
    // ---------------------------------------------------
    // 팝업 열기
    void ShowMessagePopup(string msg)
    {
        if (messageText != null) messageText.text = msg;
        if (PopupPanel != null) PopupPanel.SetActive(true);
        if (messagePopup != null) messagePopup.SetActive(true);
    }

    // 팝업 닫기
    public void ClosePopup()
    {
        if (warningPopup != null) warningPopup.SetActive(false);

        if (okPopup != null) okPopup.SetActive(false);

        if (messagePopup != null) messagePopup.SetActive(false);

        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);

        if (cardZoomPopup != null) cardZoomPopup.SetActive(false);

        if (newDeckPopup != null) newDeckPopup.SetActive(false);

        // 확인 팝업이 다른 경로로 닫히면 대기 동작도 함께 거둔다.
        // 안 그러면 드롭다운은 새 덱을 가리키는데 화면은 옛 덱인 채로 어긋난다.
        if (saveConfirmPopup != null && saveConfirmPopup.activeSelf)
        {
            saveConfirmPopup.SetActive(false);
            CancelPendingAction();
        }

        if (PopupPanel != null) PopupPanel.SetActive(false);
    }


    public void OpenCardZoom(string cardId)
    {
        // 1. 팝업 켜기
        if (PopupPanel != null) PopupPanel.SetActive(true);
        cardZoomPopup.SetActive(true);

        // 2. 확대용 함수 호출 (ID만 넘겨주면 알아서 그림)
        if (zoomedCardUI != null)
        {
            zoomedCardUI.SetupForZoom(cardId);
        }
    }
}