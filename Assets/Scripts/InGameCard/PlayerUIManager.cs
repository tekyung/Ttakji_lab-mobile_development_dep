using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Manager;
using TCG_Project.Scripts.Managers;
using UnityEngine;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    [Header("Player UI Zones")]
    public Transform myHandTransform;   // 손패 영역 (MyHand)
    public Transform mySetZoneTransform; // 세트 존 (set)

    [Header("Prefabs")]
    public GameObject myCardPrefab;     // 앞면이 보이는 진짜 카드 프리팹 (CardUI 달린 것!)

    public GameObject readyButtonObj; // 준비 완료 버튼

    // 심판(엔진)이 주고 간 무전기(콜백)를 보관할 주머니
    private Action<Card> pendingSetCallback;
    private Player localPlayer; // 내 정보

    // 대기 중인 카드 기억하기
    private Card pendingCardToSet;
    private GameObject pendingCardObj;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        EventManager.OnCardMove += HandleCardMove;
        EventManager.OnRequireSetPhaseAction += HandleRequireSet; // 🌟 핵심! 심판의 요청 듣기
        EventManager.OnRequireOpenPhaseAction += HandleRequireOpen;
    }

    private void OnDisable()
    {
        EventManager.OnCardMove -= HandleCardMove;
        EventManager.OnRequireSetPhaseAction -= HandleRequireSet;
        EventManager.OnRequireOpenPhaseAction -= HandleRequireOpen;
    }

    // 1. 내 패에 카드가 들어오면 화면에 진짜로 그려줍니다!
    private void HandleCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        if (owner.Type != UserType.Human) return; // 봇 카드는 무시
        localPlayer = owner; // 내 정보 업데이트

        if (toZone == ZoneType.Hand && fromZone != ZoneType.SetZone)
        {
            Debug.Log($"🎴 내 화면에 카드 생성: {card.Name}");
            GameObject newCard = Instantiate(myCardPrefab, myHandTransform);

            // 잠기님이 완벽하게 만들어두신 SetupForBattle 실행!
            CardUI ui = newCard.GetComponent<CardUI>();
            if (ui != null) ui.SetupForBattle(card.Id);
        }
    }

    // 2. 심판(엔진)이 "카드 내주세요!" 하고 기다릴 때 발동!
    private void HandleRequireSet(Player player, GameContext context, Action<Card> callback)
    {
        if (player.Type != UserType.Human) return;

        Debug.Log("🔔 [시스템] 세트 페이즈입니다! 카드를 세트존에 놓아주세요.");

        // 심판이 준 무전기를 주머니에 쏙 챙겨둡니다.
        pendingSetCallback = callback;

        if (readyButtonObj != null) readyButtonObj.SetActive(false);
    }

    // 3. 잠기님이 UI에서 카드를 딱! 내려놓았을 때 호출될 함수
    public void ConfirmSetCard(string cardId, GameObject cardObj)
    {
        // 무전기가 있고(세트 타이밍이고), 내 정보가 있다면?
        if (pendingSetCallback != null && localPlayer != null)
        {
            // 메모리(엔진) 속 내 패에서 방금 내려놓은 그 카드를 찾습니다.
            pendingCardToSet = localPlayer.Hand.Find(c => c.Id == cardId);
            pendingCardObj = cardObj;

            if (pendingCardToSet != null)
            {
                // 시각적으로 카드를 세트존에 안착
                cardObj.transform.SetParent(mySetZoneTransform);

                // ⭐ 피벗 고장 해결! (앵커와 피벗을 완벽한 정중앙 0.5로 강제 고정)
                RectTransform rect = cardObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;

                // ⭐ 카드를 놨으니 '준비 완료' 버튼을 짠! 하고 보여줍니다.
                if (readyButtonObj != null) readyButtonObj.SetActive(true);

                Debug.Log($"⚠️ '{pendingCardToSet.Name}'을(를) 올려두었습니다. 확정하려면 준비 버튼을 누르세요!");
            }
        }
    }

    // 2. ⭐ '준비 완료' 버튼을 클릭했을 때 발동할 함수!
    public void OnClickReadyButton()
    {
        if (pendingSetCallback != null && pendingCardToSet != null)
        {
            Debug.Log($"✅ [시스템] '{pendingCardToSet.Name}' 최종 제출! 엔진 멈춤 해제! (이제 봇이 냅니다)");

            // 버튼 숨기기
            if (readyButtonObj != null) readyButtonObj.SetActive(false);

            // 심판에게 무전기 치기!
            var callback = pendingSetCallback;
            Card finalCard = pendingCardToSet;

            // 데이터 비우기
            pendingSetCallback = null;
            pendingCardToSet = null;
            pendingCardObj = null;

            // 엔진으로 전송!
            callback.Invoke(finalCard);
        }
    }

    // 오픈 페이즈 끝나고 바로 턴 종료
    private void HandleRequireOpen(Player player, Card card, int cost, GameContext context, Action<OpenPhaseChoice> callback)
    {
        if (player.Type != UserType.Human) return;

        Debug.Log($"⏩ [임시 테스트] 오픈 페이즈 강제 패스! '{card.Name}'을(를) 바로 폐기존으로 던집니다.");

        // 고민할 필요 없이 심판의 무전기에 "Abandon(폐기) 할게!" 라고 자동 응답합니다.
        // 이렇게 하면 메인 페이즈(효과 발동)가 생략되고 즉시 다음 턴으로 넘어갑니다!
        callback.Invoke(OpenPhaseChoice.Abandon);
    }
}