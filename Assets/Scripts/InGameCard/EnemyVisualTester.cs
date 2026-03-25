using TCG_Project.Scripts.Core;    // ZoneType, Player 등이 있는 네임스페이스
using TCG_Project.Scripts.Manager; // EventManager가 있는 네임스페이스
using UnityEngine;
using TCG_Project.Scripts.Managers;

public class EnemyVisualTester : MonoBehaviour
{
    [Header("Enemy UI Zones")]
    public Transform enemyHandTransform;    // 적군의 패 영역 (가로 정렬 Layout Group 추천)
    public Transform enemySetZoneTransform; // 적군이 카드를 내려놓을 세트 존

    [Header("Prefabs")]
    public GameObject cardBackPrefab; // 적군 카드는 뒷면만 보이면 되니 뒷면 프리팹!

    private void OnEnable()
    {
        // 🌟 핵심! BattleManager가 쏘아올리는 '카드 이동 이벤트'를 여기서 엿듣습니다.
        // (주의: EventManager에 OnCardMove 이벤트가 정의되어 있다고 가정한 코드입니다)
        EventManager.OnCardMove += HandleEnemyCardMove;
    }

    private void OnDisable()
    {
        EventManager.OnCardMove -= HandleEnemyCardMove;
    }

    // 카드가 이동할 때마다 이 함수가 자동으로 실행됩니다!
    private void HandleEnemyCardMove(Card card, Player owner, ZoneType fromZone, Player target, ZoneType toZone)
    {
        // 내 카드가 아니라 봇(적군)의 카드일 때만 화면에 그려줍니다.
        if (owner.Type != UserType.Bot) return;

        // 1. 적군이 덱에서 패(Hand)로 드로우 했을 때
        if (toZone == ZoneType.Hand)
        {
            Debug.Log("👁️ 시각 효과: 봇이 카드를 드로우했습니다!");
            GameObject newCard = Instantiate(cardBackPrefab, enemyHandTransform);

            // 테스트용: 뽑은 카드가 뭔지 이름을 임시로 달아둡니다 (나중에 지우면 됨)
            newCard.name = $"Enemy Card ({card.Name})";
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
    }
}