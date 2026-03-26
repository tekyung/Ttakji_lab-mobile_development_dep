using TCG_Project.Scripts.Core;    // ZoneType, Player 등이 있는 네임스페이스
using UnityEngine.UI; // UI 레이아웃 조절용
using UnityEngine;
using TCG_Project.Scripts.Managers;
using System.Collections;

public class EnemyVisualTester : MonoBehaviour
{
    [Header("Enemy UI Zones")]
    public Transform enemyHandTransform;    // 적군의 패 영역 (가로 정렬 Layout Group 추천)
    public Transform enemySetZoneTransform; // 적군이 카드를 내려놓을 세트 존
    public Transform enemyGraveyardTransform;
    public Transform enemyStackZoneTransform;
    public Transform enemyBattlefieldTransform;

    [Header("Prefabs")]
    public GameObject cardBackPrefab; // 적군 카드는 뒷면만 보이면 되니 뒷면 프리팹!
    public GameObject cardFrontPrefab; // 앞면 프리팹

    private HorizontalLayoutGroup handLayoutGroup;

    private GameObject currentBotSetCard;

    public float enemyCardWidth = 200f;

    private void Awake()
    {
        // 봇의 패 영역에 정렬 컴포넌트(Layout Group)가 없으면 알아서 달아줍니다!
        if (enemyHandTransform != null)
        {
            handLayoutGroup = enemyHandTransform.GetComponent<HorizontalLayoutGroup>();
            if (handLayoutGroup == null)
            {
                handLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
                handLayoutGroup.childControlWidth = false;
                handLayoutGroup.childControlHeight = false;
                handLayoutGroup.childForceExpandWidth = false; // 카드가 강제로 커지는 것 방지
                handLayoutGroup.childForceExpandHeight = false;
            }
        }
    }

    private void OnEnable()
    {
        // 🌟 핵심! BattleManager가 쏘아올리는 '카드 이동 이벤트'를 여기서 엿듣습니다.
        // (주의: EventManager에 OnCardMove 이벤트가 정의되어 있다고 가정한 코드입니다)
        EventManager.OnCardMove += HandleEnemyCardMove;
        EventManager.OnPlayCard += HandleEnemyPlayCard;
    }

    private void OnDisable()
    {
        EventManager.OnCardMove -= HandleEnemyCardMove;
        EventManager.OnPlayCard -= HandleEnemyPlayCard;
    }

    // 카드 피벗, 앵커, 위치 등
    private void ForceCenterAndSize(RectTransform rect, bool applySize = false)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        // 높이를 280으로 강제할 때만 작동! (가로 200, 세로 280 기준 - 필요시 조절하세요)
        if (applySize) rect.sizeDelta = new Vector2(200f, 280f);
    }

    // 카드가 이동할 때마다 이 함수가 자동으로 실행됩니다!
    private void HandleEnemyCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        Debug.Log($"📡 [방송 수신] {owner.Name}({owner.Type}) | {fromZone} -> {toZone} | 카드: {(card != null ? card.Name : "null")}");
        // 내 카드가 아니라 봇(적군)의 카드일 때만 화면에 그려줍니다.
        if (owner.Type != UserType.Bot) return;

        // 1. 적군이 덱에서 패(Hand)로 드로우 했을 때
        if (toZone == ZoneType.Hand)
        {
            Debug.Log("👁️ 시각 효과: 봇이 카드를 드로우했습니다!");
            GameObject newCard = Instantiate(cardBackPrefab, enemyHandTransform);

            // 테스트용: 뽑은 카드가 뭔지 이름을 임시로 달아둡니다 (나중에 지우면 됨)
            newCard.name = $"Enemy Card ({card.Name})";

            //RectTransform rect = newCard.GetComponent<RectTransform>();
            //rect.sizeDelta = new Vector2(rect.sizeDelta.x, 280f);
            ForceCenterAndSize(newCard.GetComponent<RectTransform>(), true);

            // ⭐ [추가할 부분] 내가 지금 몇 장째 카드를 손에 쥐었는지 콘솔에 외치기!
            Debug.Log($"🤖 봇 드로우 이벤트 수신! 현재 봇의 패는 총 {enemyHandTransform.childCount}장입니다.");

            StartCoroutine(UpdateEnemySpacingRoutine());
        }

        // 2. 적군이 패에서 세트존(SetZone)으로 카드를 냈을 때
        else if (toZone == ZoneType.SetZone && fromZone == ZoneType.Hand)
        {
            Debug.Log("👁️ 시각 효과: 봇이 세트존에 카드를 엎어두었습니다!");

            // 적군의 패에 있는 카드 중 하나를 찾아서 세트존으로 날려버립니다!
            if (enemyHandTransform.childCount > 0)
            {
                Transform cardToMove = enemyHandTransform.GetChild(0); // 일단 첫 번째 카드 잡기
                cardToMove.SetParent(enemySetZoneTransform);

                // 예쁘게 중앙에 딱 맞추기
                RectTransform rect = cardToMove.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        // 3. 무덤/전장 이동: 전투가 끝나고 다 쓴 카드를 치워줄 때
        else if (toZone == ZoneType.Graveyard || toZone == ZoneType.StackZone || toZone == ZoneType.BattlefieldZone)
        {
            GameObject cardObjToMove = null;

            if (fromZone == ZoneType.SetZone && currentBotSetCard != null)
            {
                cardObjToMove = currentBotSetCard;
            }
            else if (fromZone == ZoneType.StackZone && enemyStackZoneTransform != null)
            {
                Transform t = enemyStackZoneTransform.Find(card.Id);
                if (t != null) cardObjToMove = t.gameObject;
            }
            else if (fromZone == ZoneType.BattlefieldZone && enemyBattlefieldTransform != null)
            {
                // 봇의 전장에서 해당 아이디를 가진 카드를 찾아서 타겟으로 잡습니다!
                Transform t = enemyBattlefieldTransform.Find(card.Id);
                if (t != null) cardObjToMove = t.gameObject;
            }

            // 진짜 이동시키기!
            if (cardObjToMove != null)
            {
                if (toZone == ZoneType.Graveyard && enemyGraveyardTransform != null) //무덤
                {
                    cardObjToMove.transform.SetParent(enemyGraveyardTransform);
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                }
                else if (toZone == ZoneType.StackZone && enemyStackZoneTransform != null) //스택
                {
                    cardObjToMove.transform.SetParent(enemyStackZoneTransform);
                    cardObjToMove.name = card.Id;
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                }
                else if (toZone == ZoneType.BattlefieldZone && enemyBattlefieldTransform != null) //전장
                {
                    cardObjToMove.transform.SetParent(enemyBattlefieldTransform);
                    cardObjToMove.name = card.Id; // 나중에 찾기 쉽게 이름 변경
                    ForceCenterAndSize(cardObjToMove.GetComponent<RectTransform>());
                    // 전장 카드는 앞면을 유지해야 하므로 뒤집지 않습니다!
                }
                else
                {
                    Destroy(cardObjToMove);
                }
            }

            // ⭐ [핵심 방어 코드] 세트존에서 무덤으로 갔다면, 세트존에 남아있는 찌꺼기를 모조리 부숴버립니다!
            if (fromZone == ZoneType.SetZone && enemySetZoneTransform != null)
            {
                foreach (Transform child in enemySetZoneTransform)
                {
                    Destroy(child.gameObject);
                }
                currentBotSetCard = null; // 수첩 초기화
            }
        }
    }

    // ⭐ 봇이 메인 페이즈에서 카드를 발동했을 때!
    private void HandleEnemyPlayCard(Player player, Card card)
    {
        if (player.Type != UserType.Bot) return;

        // 세트존 청소
        if (currentBotSetCard != null) Destroy(currentBotSetCard);
        foreach (Transform child in enemySetZoneTransform) Destroy(child.gameObject);

        // 앞면 진짜 카드 소환!
        GameObject realCard = Instantiate(cardFrontPrefab, enemySetZoneTransform);

        // 여기서도 높이 280 및 정중앙 완벽 세팅!
        ForceCenterAndSize(realCard.GetComponent<RectTransform>(), true);

        CardUI ui = realCard.GetComponent<CardUI>();
        if (ui != null) ui.SetupForBattle(card.Id);

        CardInteraction interaction = realCard.GetComponent<CardInteraction>();
        if (interaction != null) interaction.enabled = false;

        currentBotSetCard = realCard;
    }

    private IEnumerator UpdateEnemySpacingRoutine() //패 겹치는거
    {
        yield return null; // 한 프레임 대기

        int cardCount = enemyHandTransform.childCount;
        if (cardCount < 2)
        {
            handLayoutGroup.spacing = 0;
            yield break;
        }

        RectTransform handRect = enemyHandTransform.GetComponent<RectTransform>();
        float panelWidth = handRect.rect.width;
        float totalCardWidth = enemyCardWidth * cardCount;

        if (totalCardWidth > panelWidth)
        {
            float neededSpacing = (panelWidth - totalCardWidth) / (cardCount - 1);
            handLayoutGroup.spacing = neededSpacing;
        }
        else
        {
            handLayoutGroup.spacing = -20f; // 널널할 때의 기본 간격
        }
    }
}