using System.Collections;
using UnityEngine;
using TMPro;

public class RandomMatchUI : MonoBehaviour
{
    [Header("랜덤 매칭 팝업")]
    public GameObject popupRandomMatching;

    [Header("랜덤 매칭 성사 팝업")]
    public GameObject popupRandomMatched;

    [Header("타이머")]
    public TMP_Text timerText;

    [Header("세션 매니저")]
    [SerializeField] private session_manage matchManager;

    private float timer = 0f;
    private bool isMatching = false;

    void Update()
    {
        if (!isMatching) return;

        timer += Time.deltaTime;

        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.FloorToInt(timer % 60f);

        if (timerText != null)
            timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void StartRandomMatch()
    {
        timer = 0f;
        isMatching = true;

        if (timerText != null)
            timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(true);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(false);

        if (matchManager != null)
        {
            matchManager.onStatusUpdate = HandleStatusUpdate;
            matchManager.onMatchedAndReady = HandleMatchedAndReady;
            matchManager.OnClickRandomMatch();
        }
        else
        {
            Debug.LogError("[RandomMatchUI] matchManager가 연결되지 않았습니다.");
        }
    }

    public void CancelRandomMatch()
    {
        isMatching = false;
        timer = 0f;

        if (timerText != null)
            timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(false);

        if (matchManager != null)
        {
            matchManager.onStatusUpdate = null;
            matchManager.onMatchedAndReady = null;
            matchManager.OnClickExitSession();
        }
    }

    private void HandleStatusUpdate(string msg)
    {
        if (timerText != null)
            timerText.text = msg;
    }

    [Header("Auto Start")]
    [Tooltip("매칭 성사 후 호스트가 게임을 시작시키기까지의 대기 시간(초)")]
    [SerializeField] private float autoStartDelay = 1.5f;

    private void HandleMatchedAndReady()
    {
        isMatching = false;
        Debug.Log("[RandomMatchUI] 매칭 성공!");

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(true);

        // ★ 랜덤 매칭에는 로비가 없다. 여기서 시작을 걸어 주지 않으면 양쪽 다 영원히 대기한다.
        //   세션 state를 PLAYING으로 올리는 것은 session_manage.OnClickSessionStart 뿐인데,
        //   그 버튼은 'server ui' 디버그 씬에만 배선돼 있고 메인 메뉴에는 없다.
        //   → 호스트가 자동으로 올린다. (게스트는 ListenForGameStart로 그걸 감지해 따라 들어간다)
        if (matchManager != null && matchManager.IsHost)
            StartCoroutine(AutoStartAsHost());
    }

    /// <summary>
    /// 호스트만 실행. 세션을 PLAYING으로 올려 양쪽을 대전 씬으로 들여보낸다.
    /// ⚠️ 곧바로 부르면 안 된다 — session_manage가 이 콜백 '뒤에' state를 READY로 쓰기 때문에
    ///    PLAYING이 READY로 덮여 다시 멈춘다. 그 쓰기가 끝날 시간을 준 뒤 올린다.
    /// </summary>
    private IEnumerator AutoStartAsHost()
    {
        yield return new WaitForSeconds(autoStartDelay);

        Debug.Log("[RandomMatchUI] 매칭 완료 — 호스트가 대전을 시작합니다.");
        matchManager.OnClickSessionStart();
    }
}
