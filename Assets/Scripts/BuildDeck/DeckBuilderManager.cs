using System.Collections.Generic;
using System.Linq; // 리스트 검색용 기능
using UnityEngine;
using TMPro; // 텍스트 사용
using System.IO; //파일 관리

[System.Serializable]
public class DeckSaveData
{
    public List<string> cardIdList;
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

    public GameObject savePopup; // 덱 불러오기
    public TextMeshProUGUI saveText; // 불러온 덱 이름

    public GameObject deleteConfirmPopup; // 삭제 확인 팝업
    public TextMeshProUGUI deleteConfirmText; // 삭제 덱 이름

    public GameObject newDeckPopup; // 새 덱 만들기 팝업

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

    // 실제 데이터 (덱에 들어있는 카드 ID 목록)
    private List<string> myDeck = new List<string>();
    private const int MAX_DECK_COUNT = 20;

    // 화면에 떠 있는 카드 슬롯들을 관리하는 리스트 (Collection 쪽)
    private List<CardUI> collectionSlots = new List<CardUI>();

    void Start()
    {
        // 1. 게임 시작 시 전체 카드 목록(Collection)을 먼저 만듭니다.
        InitCollection();

        // 2. 덱 화면 초기화
        RefreshDeckList();
    }

    // ---------------------------------------------------
    // 덱 / 덱리스트 관련
    // ---------------------------------------------------

    //임시 슬라임20장 시작 덱
    void CreateStarterDeck()
    {
        myDeck.Clear();

        string slimeID = "11001";
        int starterCount = 20;

        for (int i = 0; i < starterCount; i++)
        {
            myDeck.Add(slimeID);
        }

        // 덱 이름 입력칸도 "DefaultDeck" 등으로 채워주면 더 좋습니다.
        if (deckNameInput != null)
        {
            deckNameInput.text = "StarterDeck";
        }

        // 데이터가 변경되었으니 화면을 갱신합니다. (중요!)
        RefreshAllUI();

        Debug.Log("기본 슬라임 덱 생성 완료!");
    }

    // 덱 리스트 정리
    public void RefreshDeckList(string focusDeckName = null)
    {
        // 1. 드롭다운 초기화 (기존 목록 지우기)
        deckListDropdown.ClearOptions();

        // 2. 해당 폴더의 모든 .json 파일 경로를 가져옴
        string folderPath = DeckStorage.EnsureFolder();
        string[] filePaths = DeckStorage.GetDeckFiles();

        Debug.Log("검색 중인 폴더 위치: " + folderPath);
        Debug.Log("발견된 JSON 파일 개수: " + filePaths.Length + "개");

        List<string> options = new List<string>();

        // 3. 파일 경로에서 "파일 이름"만 쏙 빼서 목록에 추가
        foreach (string path in filePaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            options.Add(fileName);
        }

        if (options.Count == 0)
        {
            options.Add("덱이 없습니다.");
            deckListDropdown.interactable = false;
            deckListDropdown.AddOptions(options);
            CreateStarterDeck(); // 파일이 없으면 스타터 덱 생성 및 저장
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
            if (deckName != "덱이 없습니다.")
            {
                LoadDeckFromJson(deckName + ".json");
            }
        }
    }

    //드롭다운에서 덱 선택 시
    public void OnDeckSelected(int index)
    {
        //// 선택된 덱의 이름 가져오기
        //string selectedName = deckListDropdown.options[index].text;

        //// 그 이름으로 파일 로딩
        //LoadDeckFromJson(selectedName + ".json");
    }

    public void OnClickLoadDeckButton()
    {
        // 1. 현재 드롭다운의 인덱스 가져오기
        int index = deckListDropdown.value;

        // 2. 그 번호에 해당하는 덱 이름 가져오기
        string selectedName = deckListDropdown.options[index].text;

        // 3. 덱이 없을 경우
        if (selectedName == "덱이 없습니다.")
        {
            Debug.Log("불러올 덱이 없습니다.");
            return;
        }

        bool isSuccess = LoadDeckFromJson(selectedName + ".json");

        if (isSuccess)
        {
            ShowMessagePopup($"{selectedName}을(를) 불러왔습니다.");
        }

        // 4. 로딩
        LoadDeckFromJson(selectedName + ".json");

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

        List<string> uniqueIDs = myDeck.Distinct().ToList();

        foreach (string id in uniqueIDs)
        {
            GameObject go = Instantiate(cardPrefab, deckContent);
            CardUI ui = go.GetComponent<CardUI>();
            CardData data = CardDataManager.Instance.GetCard(id);

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
            // 덱에 이 카드가 몇 장 있는지 셉니다.
            int count = myDeck.Count(x => x == slot.myCardID);
            CardData data = CardDataManager.Instance.GetCard(slot.myCardID);

            // 숫자만 갱신 (깜빡임 없음)
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

    //저장 버튼 클릭
    public void OnClickSaveDeck()
    {
        // 1. 장수 체크 (20장인지 확인)
        if (myDeck.Count != MAX_DECK_COUNT)
        {
            // 20장이 아니면 경고 팝업 띄우기
            if (PopupPanel != null) PopupPanel.SetActive(true);
            if (warningPopup != null) warningPopup.SetActive(true);

            Debug.Log("저장 실패: 덱이 완성되지 않았습니다.");
            return;
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
            return;
        }

        // 3. 저장 진행
        SaveDeckToJson();
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

    // JSON 저장
    void SaveDeckToJson()
    {
        if (deckNameInput.text == "")
        {
            Debug.Log("덱 이름 입력하시오");
            return;
        }

        // 저장할 데이터 객체 만들기
        DeckSaveData data = new DeckSaveData();
        data.cardIdList = new List<string>(myDeck); // 현재 덱 복사

        // JSON 문자열로 변환
        string json = JsonUtility.ToJson(data, true);

        // 저장할 경로, 이름 설정 (PC, 모바일 모두 작동하는 경로)
        string folderPath = DeckStorage.EnsureFolder();

        string originalName = deckNameInput.text;
        string finalName = originalName;
        string fileName = finalName + ".json";
        string path = Path.Combine(folderPath, fileName);

        // ★ 지금 편집 중인 그 덱이면 덮어쓴다.
        //   이 분기가 없으면 [덱 저장하기]를 누를 때마다 my_deck_1, _2, _3 … 이 새로 생긴다.
        //   이름을 바꿔서 저장했는데 그게 '다른' 덱과 겹칠 때만 아래 자동 번호가 붙는다.
        bool isOverwritingEditedDeck = File.Exists(path) && originalName == _editingDeckName;

        if (File.Exists(path) && !isOverwritingEditedDeck)
        {
            string baseName = originalName;
            // 3-1. 이름 뒤에 이미 "_숫자"가 붙어있는지 분석 (예: "Deck_1")
            int lastUnderscore = originalName.LastIndexOf('_');

            // '_'가 있고, 그 뒤에 숫자가 있다면?
            if (lastUnderscore > 0 && lastUnderscore < originalName.Length - 1)
            {
                string numberPart = originalName.Substring(lastUnderscore + 1);

                // 진짜 숫자가 맞는지 확인 (TryParse)
                if (int.TryParse(numberPart, out int currentNumber))
                {
                    // "Deck_1" 이라면 -> baseName은 "Deck", 다음 번호는 2부터 시작
                    baseName = originalName.Substring(0, lastUnderscore);
                }
            }

            int maxNumber = 0;

            string[] files = Directory.GetFiles(folderPath, baseName + "*.json");

            foreach (string filePath in files)
            {
                string fName = Path.GetFileNameWithoutExtension(filePath); // 파일명만 가져옴

                // 정확히 포맷이 맞는지 확인 ("Slime_숫자")
                string prefix = baseName + "_";
                if (fName.StartsWith(prefix))
                {
                    string numStr = fName.Substring(prefix.Length);
                    if (int.TryParse(numStr, out int num))
                    {
                        if (num > maxNumber)
                        {
                            maxNumber = num; // 더 큰 숫자를 발견하면 갱신
                        }
                    }
                }
            }

            int nextNumber = maxNumber + 1;

            finalName = $"{baseName}_{nextNumber}";
            path = Path.Combine(folderPath, finalName + ".json");
        }

        // 파일 쓰기
        File.WriteAllText(path, json);

        // 방금 저장한 덱이 이제 편집 대상이다. 이어서 또 저장하면 이 파일을 덮어쓴다
        _editingDeckName = finalName;

        Debug.Log(isOverwritingEditedDeck
            ? $"저장 완료(덮어쓰기): {finalName}"
            : $"저장 완료(새 덱): {finalName}");

        if (deckNameInput != null)
        {
            deckNameInput.text = finalName;
        }

        if (PopupPanel != null) PopupPanel.SetActive(true);
        if (okPopup != null) okPopup.SetActive(true);

        RefreshDeckList(finalName);
    }

    //JSON 파일 이름 받기
    bool LoadDeckFromJson(string fileName)
    {
        // 1. 경로 설정 (Assets 폴더 기준)
        string path = DeckStorage.GetDeckPath(fileName);

        Debug.Log("파일 찾는 중: " + path);

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
        myDeck = new List<string>(data.cardIdList);

        // 6. 덱 이름 입력칸도 파일 이름으로 맞춰주기 (확장자 .json 제거)
        string loadedName = fileName.Replace(".json", "");
        if (deckNameInput != null)
        {
            deckNameInput.text = loadedName;
        }

        // 이제부터 [덱 저장하기]는 이 덱을 덮어쓴다
        _editingDeckName = loadedName;

        // 7. 화면 갱신 (중요!)
        RefreshAllUI();

        Debug.Log("불러오기 성공: " + fileName);
        return true; // 성공(true) 반환
    }

    // 덱 삭제
    public void OnClickDeleteDeckButton()
    {
        // 1. 드롭다운에서 현재 덱 가져오기
        int index = deckListDropdown.value;
        string selectedName = deckListDropdown.options[index].text;

        // 2. 예외처리
        if (selectedName == "덱이 없습니다.")
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
        // 아까 기억해둔 이름으로 파일 경로 찾기
        string path = DeckStorage.GetDeckPath(deckToDelete);

        // 파일 삭제
        if (File.Exists(path))
        {
            File.Delete(path);
        }

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

        Debug.Log($"카드 ID {id}번을 덱에서 모두 제거했습니다.");
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
        if (saveText != null) saveText.text = msg;
        if (PopupPanel != null) PopupPanel.SetActive(true);
        if (savePopup != null) savePopup.SetActive(true);
    }

    // 팝업 닫기
    public void ClosePopup()
    {
        if (warningPopup != null) warningPopup.SetActive(false);

        if (okPopup != null) okPopup.SetActive(false);

        if (savePopup != null) savePopup.SetActive(false);

        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);

        if (cardZoomPopup != null) cardZoomPopup.SetActive(false);

        if (newDeckPopup != null) newDeckPopup.SetActive(false);

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