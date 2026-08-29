using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    public string deckEditSceneName = "SampleScene";

    public void OnClickMatch()
    {
        Debug.Log("Match Start (TODO)");
        // TODO: ��? ��/��? ���� ����
    }

    public void OnClickDeckEdit()
    {
        SceneManager.LoadScene(deckEditSceneName);
    }

    public void OnClickQuit()
    {
        Debug.Log("Quit");
        Application.Quit();
    }
    [Header("매칭 팝업")]
    [Tooltip("화면 전체를 덮는 반투명 막. 클릭을 막는 역할도 한다.")]
    public GameObject popupDim;

    [Tooltip("매칭 방식(랜덤/커스텀)을 고르는 창. PopupDim의 자식이다.")]
    public GameObject popupMatchMode;

    public void OpenMatchMode()
    {
        if (popupDim != null)
            popupDim.SetActive(true);

        if (popupMatchMode != null)
            popupMatchMode.SetActive(true);
    }

    /// <summary>
    /// 매칭 흐름에서 완전히 빠져나온다.
    ///
    /// ★ 여는 짝만 있고 닫는 짝이 없었다.
    ///   PopupDim은 raycastTarget이 켜진 전체 화면 막이라, 창을 닫아도 이것이 남아 있으면
    ///   화면이 멀쩡해 보이는데 <b>어떤 버튼도 눌리지 않는다.</b>
    ///   실제로 매칭을 취소한 뒤 다시 방을 잡을 수 없던 원인이 이것이었다.
    ///
    ///   매칭 창들은 PopupDim의 자식이므로 막을 끄면 함께 사라진다.
    /// </summary>
    public void CloseMatchMode()
    {
        if (popupMatchMode != null)
            popupMatchMode.SetActive(false);

        if (popupDim != null)
            popupDim.SetActive(false);
    }
}