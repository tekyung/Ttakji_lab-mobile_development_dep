using System.Collections.Generic;
using System.Linq; // 리스트 검색용 기능
using UnityEngine;
using TMPro; // 텍스트 사용

public class DeckBuilderManager : MonoBehaviour
{
    [Header("UI 연결")]
    public Transform deckContent;       // MyDeckPanel의 Content
    public Transform collectionContent; // CollectionPanel의 Content
    public GameObject cardPrefab;       // CardSlot 프리팹
    public TextMeshProUGUI deckCountText; // 20/20 텍스트 (없으면 연결 안 해도 됨)

    // 실제 데이터 (덱에 들어있는 카드 ID 목록)
    private List<int> myDeck = new List<int>();

    // 화면에 떠 있는 카드 슬롯들을 관리하는 리스트 (Collection 쪽)
    private List<CardUI> collectionSlots = new List<CardUI>();

    void Start()
    {
        // 1. 게임 시작 시 전체 카드 목록(Collection)을 먼저 만듭니다.
        InitCollection();

        // 2. 덱 화면 초기화
        RefreshDeckUI();
    }

    // 전체 카드 리스트 생성 (처음에 한 번만 실행)
    void InitCollection()
    {
        foreach (var kvp in CardDataManager.Instance.CardDict)
        {
            int id = kvp.Key;
            CardData data = kvp.Value;

            // 프리팹 생성
            GameObject go = Instantiate(cardPrefab, collectionContent);
            CardUI ui = go.GetComponent<CardUI>();

            // 정보 입력 (처음엔 덱에 0장 있으므로 개수는 0)
            ui.Setup(id, 0, data.maxDeckCount, this);

            // 리스트에 등록해둠 (나중에 개수 갱신할 때 쓰려고)
            collectionSlots.Add(ui);
        }
    }

    // ---------------------------------------------------
    // 카드 추가/제거 로직
    // ---------------------------------------------------

    public void AddCard(int id)
    {
        // 1. 전체 20장 제한 체크
        if (myDeck.Count >= 20)
        {
            Debug.Log("덱이 가득 찼습니다!");
            return;
        }

        // 2. 카드별 2장 제한 체크
        int currentCount = myDeck.Count(x => x == id);
        CardData data = CardDataManager.Instance.GetCard(id);

        if (currentCount < data.maxDeckCount)
        {
            myDeck.Add(id);
            RefreshAllUI(); // 화면 갱신
        }
    }

    public void RemoveCard(int id)
    {
        if (myDeck.Contains(id))
        {
            myDeck.Remove(id);
            RefreshAllUI(); // 화면 갱신
        }
    }

    // ---------------------------------------------------
    // 화면 갱신 로직
    // ---------------------------------------------------

    void RefreshAllUI()
    {
        RefreshDeckUI();       // 위쪽 화면 다시 그리기
        RefreshCollectionUI(); // 아래쪽 화면 숫자 바꾸기

        // 덱 장수 텍스트 갱신 (예: 12/20)
        if (deckCountText) deckCountText.text = $"{myDeck.Count}/20";
    }

    // 위쪽: 내 덱 리스트는 매번 지우고 다시 그립니다 (순서 정렬 등을 위해)
    void RefreshDeckUI()
    {
        // 기존 슬롯 다 삭제
        foreach (Transform child in deckContent) Destroy(child.gameObject);

        List<int> uniqueIDs = myDeck.Distinct().ToList();

        foreach (int id in uniqueIDs)
        {
            GameObject go = Instantiate(cardPrefab, deckContent);
            CardUI ui = go.GetComponent<CardUI>();
            CardData data = CardDataManager.Instance.GetCard(id);

            int countInDeck = myDeck.Count(x => x == id);

            ui.Setup(id, countInDeck, data.maxDeckCount, this);
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
            slot.UpdateCount(count, data.maxDeckCount);
        }
    }
}