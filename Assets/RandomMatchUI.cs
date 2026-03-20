using UnityEngine;
using TMPro;

public class RandomMatchUI : MonoBehaviour
{
    [Header("랜덤 매칭 팝업")]
    public GameObject popupRandomMatching;

    [Header("텍스트")]
    public TMP_Text timerText;

    [Header("테스트용 자동 성공 시간(초)")]
    public float matchSuccessTime = 3f;

    private float timer = 0f;
    private bool isMatching = false;

    void Update()
    {
        if (!isMatching) return;

        timer += Time.deltaTime;

        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.FloorToInt(timer % 60f);

        timerText.text = $"{minutes:00}:{seconds:00}";

        if (timer >= matchSuccessTime)
        {
            MatchSuccess();
        }
    }

    public void StartRandomMatch()
    {
        timer = 0f;
        isMatching = true;
        timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(true);
    }

    public void CancelRandomMatch()
    {
        isMatching = false;
        timer = 0f;
        timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);
    }

    private void MatchSuccess()
    {
        isMatching = false;
        Debug.Log("매칭 성공!");

        // 지금은 테스트용으로 랜덤 매칭 팝업만 끔
        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        // 나중에 여기서 매칭 성공 팝업 또는 BattleScene 이동 넣으면 됨
    }
}