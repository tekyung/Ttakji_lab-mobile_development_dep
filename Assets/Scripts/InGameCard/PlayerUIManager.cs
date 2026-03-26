using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;

    [Header("Player UI Zones")]
    public Transform myHandTransform;   // 손패 영역 (MyHand)
    public Transform mySetZoneTransform; // 세트 존 (set)
    public Transform myGraveyardTransform; // 무덤(폐기)
    public Transform myStackZoneTransform; // 스택
    public Transform myBattlefieldTransform; // 전장

    [Header("Prefabs")]
    public GameObject myCardPrefab;     // 앞면이 보이는 진짜 카드 프리팹 (CardUI 달린 것!)

    public GameObject readyButtonObj; // 준비 완료 버튼

    // 심판(엔진)이 주고 간 무전기(콜백)를 보관할 주머니
    private Action<Card> pendingSetCallback;
    private Player localPlayer; // 내 정보

    // 대기 중인 카드 기억하기
    private Card pendingCardToSet;
    private GameObject pendingCardObj;
    private bool pendingIsReveal = true;

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

            // SetupForBattle 실행!
            CardUI ui = newCard.GetComponent<CardUI>();
            if (ui != null) ui.SetupForBattle(card.Id);
        }

        // ⭐ 2. 카드를 다 쓰고 무덤(Graveyard)으로 갈 때!
        if (toZone == ZoneType.Graveyard) // fromZone 체크를 빼서 더 유연하게 만듭니다!
        {
            // 지금 무덤으로 간 카드가 내가 세트존에 둔 그 카드라면?
            if (fromZone == ZoneType.SetZone && pendingCardToSet != null && card.Id == pendingCardToSet.Id)
            {
                SendPendingCardToGraveyard(); // 아까 만든 세트존->무덤 함수
                pendingCardToSet = null;
            }
            else if (fromZone == ZoneType.StackZone) // 스택 -> 무덤
            {
                // ⭐ 스택존에 있던 카드를 찾아서 무덤으로 보냅니다!
                MoveCardToGraveyardFromZone(myStackZoneTransform, card.Id);
            }
            else if (fromZone == ZoneType.BattlefieldZone) // 전장 -> 무덤
            {
                // 아까 만들어둔 만능 헬퍼 함수를 쓰면 전장에서 알아서 찾아서 무덤으로 던져줍니다!
                MoveCardToGraveyardFromZone(myBattlefieldTransform, card.Id);
            }
        }
        // 3. 만약 카드가 전장(Battlefield)이나 스택(Stack)으로 남게 되었다면?
        else if (toZone == ZoneType.StackZone && fromZone == ZoneType.SetZone)
        {
            if (pendingCardToSet != null && card.Id == pendingCardToSet.Id && pendingCardObj != null)
            {
                Debug.Log($"🛡️ '{card.Name}'이(가) 내 스택 존으로 이동합니다!");

                // 스택존으로 부모 변경
                pendingCardObj.transform.SetParent(myStackZoneTransform);
                RectTransform rect = pendingCardObj.GetComponent<RectTransform>();
                rect.anchoredPosition = Vector2.zero;

                // 스택존에 간 카드는 앞면(공개)으로 둡니다! (상대도 봐야 하니까요)
                CardUI ui = pendingCardObj.GetComponent<CardUI>();
                if (ui != null) ui.SetFaceDown(false);

                // 상호작용(드래그 등) 끄기
                CardInteraction interaction = pendingCardObj.GetComponent<CardInteraction>();
                if (interaction != null) interaction.enabled = false;

                // 이제 세트존을 떠났으므로 내 기억 수첩에서 지웁니다.
                pendingCardToSet = null;
                pendingCardObj = null;
            }
        }
        // 4. 전장(Battlefield) 존으로 갈 때! (필드 유지 카드)
        else if (toZone == ZoneType.BattlefieldZone)
        {
            // 세트존에서 전장으로 이동하는 경우
            if (fromZone == ZoneType.SetZone && pendingCardToSet != null && card.Id == pendingCardToSet.Id && pendingCardObj != null)
            {
                if (myBattlefieldTransform != null)
                {
                    Debug.Log($"⚔️ '{card.Name}'이(가) 내 전장으로 안착했습니다!");

                    pendingCardObj.transform.SetParent(myBattlefieldTransform);
                    RectTransform rect = pendingCardObj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;

                    // 전장에 남는 카드는 항상 앞면(공개) 상태여야 합니다!
                    CardUI ui = pendingCardObj.GetComponent<CardUI>();
                    if (ui != null) ui.SetFaceDown(false);

                    // 드래그 금지
                    CardInteraction interaction = pendingCardObj.GetComponent<CardInteraction>();
                    if (interaction != null) interaction.enabled = false;
                }
                else
                {
                    Destroy(pendingCardObj); // 전장 UI를 안 만들었다면 화면에서 치웁니다.
                }

                // 세트존을 떠났으므로 수첩에서 지웁니다.
                pendingCardToSet = null;
                pendingCardObj = null;
            }
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

    // 3.카드를 딱! 내려놓았을 때 호출될 함수
    public void ConfirmSetCard(string cardId, GameObject cardObj, bool isReveal)
    {
        // 무전기가 있고(세트 타이밍이고), 내 정보가 있다면?
        if (pendingSetCallback != null && localPlayer != null)
        {
            // 메모리(엔진) 속 내 패에서 방금 내려놓은 그 카드를 찾습니다.
            pendingCardToSet = localPlayer.Hand.Find(c => c.Id == cardId);
            pendingCardObj = cardObj;
            pendingIsReveal = isReveal;

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

                CardInteraction interaction = cardObj.GetComponent<CardInteraction>();
                if (interaction != null) interaction.isInSetZone = true;

                CardUI ui = cardObj.GetComponent<CardUI>(); //뒷면
                if (ui != null) ui.SetFaceDown(true);

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
        if (pendingIsReveal)
        {
            Debug.Log("⏩ [오픈 페이즈] 사용자가 '공개'를 선택했으므로 Open 처리합니다!");
            callback.Invoke(OpenPhaseChoice.Open);
        }
        else
        {
            Debug.Log("⏩ [오픈 페이즈] 사용자가 '폐기'를 선택했으므로 Abandon 처리합니다!");
            SendPendingCardToGraveyard();
            callback.Invoke(OpenPhaseChoice.Abandon);
        }
    }

    public void CancelSet()
    {
        Debug.Log("🔄 세트 취소됨! 레디 버튼을 숨깁니다.");

        pendingCardToSet = null;
        pendingCardObj = null;

        // 레디 버튼 다시 숨기기
        if (readyButtonObj != null) readyButtonObj.SetActive(false);
    }

    // 카드 무덤 보내기
    private void SendPendingCardToGraveyard()
    {
        if (pendingCardObj != null && myGraveyardTransform != null)
        {
            pendingCardObj.transform.SetParent(myGraveyardTransform);

            RectTransform rect = pendingCardObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            CardInteraction interaction = pendingCardObj.GetComponent<CardInteraction>();
            if (interaction != null)
            {
                interaction.DeselectCard();
                if (interaction.actionButtonPanel != null) interaction.actionButtonPanel.SetActive(false);
                if (interaction.actionButtonPanelBottom != null) interaction.actionButtonPanelBottom.SetActive(false);
                interaction.enabled = false;
            }

            // 공개(true)면 앞면(false), 폐기(false)면 뒷면(true)
            CardUI ui = pendingCardObj.GetComponent<CardUI>();
            if (ui != null) ui.SetFaceDown(!pendingIsReveal);

            pendingCardObj = null; // 세트존 비우기 완료!
        }
    
    }
    // ⭐ 특정 존(스택존 등)에 있는 카드를 아이디로 찾아서 무덤으로 던지는 함수
    private void MoveCardToGraveyardFromZone(Transform zone, string cardId)
    {
        if (myGraveyardTransform == null) return;

        foreach (Transform child in zone)
        {
            CardUI ui = child.GetComponent<CardUI>();
            if (ui != null && ui.myCardID == cardId)
            {
                Debug.Log($"🪦 스택 존에 있던 '{cardId}' 카드가 다 쓰여서 무덤으로 갑니다!");
                child.SetParent(myGraveyardTransform);
                child.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                break; // 찾았으니 종료
            }
        }
    }
}