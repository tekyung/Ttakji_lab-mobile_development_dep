using UnityEngine;

public class MyHand : MonoBehaviour
{
    [Header("UI References")]
    public Transform handArea;     // 카드가 놓일 곳 오브젝트 (HandArea)
    public GameObject cardPrefab;  // 생성할 임시 카드 프리팹

    [Header("Settings")]
    public int maxHandSize = 10;   // 패의 최대 개수

    public void DrawCard()
    {
        // 1. 패가 꽉 찼는지 검사
        if (handArea.childCount >= maxHandSize)
        {
            Debug.Log("패가 꽉 차서 더 이상 드로우할 수 없습니다!");
            return;
        }

        // 2. 카드 프리팹 생성 (화면 어딘가에 만들어짐)
        GameObject newCard = Instantiate(cardPrefab);

        // 3. 생성된 카드를 HandArea의 자식으로 설정
        // false를 주어야 캔버스 스케일이 꼬이지 않고 원래 프리팹 크기를 유지합니다.
        newCard.transform.SetParent(handArea, false);

        Debug.Log($"드로우 성공! 현재 패: {handArea.childCount}장");
    }
}
