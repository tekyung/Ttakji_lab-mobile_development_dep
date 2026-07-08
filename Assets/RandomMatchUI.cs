using UnityEngine;
using TMPro;

public class RandomMatchUI : MonoBehaviour
{
    [Header("?? ?? ??")]
    public GameObject popupRandomMatching;

    [Header("?? ?? ?? ??")]
    public GameObject popupRandomMatched;

    [Header("???")]
    public TMP_Text timerText;

    [Header("?? ???")]
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
            Debug.LogError("[RandomMatchUI] matchManager? ???? ?????.");
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

    private void HandleMatchedAndReady()
    {
        isMatching = false;
        Debug.Log("?? ??!");

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(true);
    }
}
