using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MyHandManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform handArea;     // 카드가 놓일 곳 오브젝트 (HandArea)
    public GameObject cardPrefab;  // 생성할 임시 카드 프리팹

    public HorizontalLayoutGroup handLayoutGroup;

    [Header("Settings")]
    public int maxHandSize = 10;   // 패의 최대 개수
    public float cardWidth = 200f;

    public void DrawCard()
    {
        if (handArea.childCount >= maxHandSize) return;

        GameObject newCard = Instantiate(cardPrefab);
        newCard.transform.SetParent(handArea, false);

        // ★ [수정됨] 카드를 낳자마자 계산하지 않고, UI가 업데이트될 틈을 줍니다.
        StartCoroutine(UpdateSpacingRoutine());
    }

    // ★ [새로 추가된 코루틴 함수]
    private IEnumerator UpdateSpacingRoutine()
    {
        // 유니티 시스템이 UI 크기 배치를 마칠 때까지 한 프레임 대기
        yield return null;

        int cardCount = handArea.childCount;
        if (cardCount < 2)
        {
            handLayoutGroup.spacing = 0;
            yield break; // 코루틴 종료
        }

        RectTransform handRect = handArea.GetComponent<RectTransform>();
        float panelWidth = handRect.rect.width;
        float totalCardWidth = cardWidth * cardCount;

        if (totalCardWidth > panelWidth)
        {
            float neededSpacing = (panelWidth - totalCardWidth) / (cardCount - 1);
            handLayoutGroup.spacing = neededSpacing;
        }
        else
        {
            handLayoutGroup.spacing = -20f;
        }
    }
}
