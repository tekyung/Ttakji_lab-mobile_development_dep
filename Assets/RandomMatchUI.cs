using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class RandomMatchUI : MonoBehaviour
{
    [Header("·£´ý ¸ÅÄª ÆË¾÷")]
    public GameObject popupRandomMatching;

    [Header("·£´ý ¸ÅÄª ¿Ï·á ÆË¾÷")]
    public GameObject popupRandomMatched;

    [Header("ÅØ½ºÆ®")]
    public TMP_Text timerText;

    [Header("Å×½ºÆ®¿ë ÀÚµ¿ ¼º°ø ½Ã°£(ÃÊ)")]
    public float matchSuccessTime = 3f;

    [Header("ÀÌµ¿ÇÒ ¾À ÀÌ¸§")]
    public string nextSceneName = "TestGameScene";

    private float timer = 0f;
    private bool isMatching = false;
    private bool isSuccessProcessing = false;

    void Update()
    {
        if (!isMatching) return;

        timer += Time.deltaTime;

        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.FloorToInt(timer % 60f);

        if (timerText != null)
            timerText.text = $"{minutes:00}:{seconds:00}";

        if (timer >= matchSuccessTime && !isSuccessProcessing)
        {
            isSuccessProcessing = true;
            MatchSuccess();
        }
    }

    public void StartRandomMatch()
    {
        timer = 0f;
        isMatching = true;
        isSuccessProcessing = false;

        if (timerText != null)
            timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(true);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(false);
    }

    public void CancelRandomMatch()
    {
        isMatching = false;
        isSuccessProcessing = false;
        timer = 0f;

        if (timerText != null)
            timerText.text = "00:00";

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(false);
    }

    private void MatchSuccess()
    {
        isMatching = false;
        Debug.Log("¸ÅÄª ¼º°ø!");

        if (popupRandomMatching != null)
            popupRandomMatching.SetActive(false);

        if (popupRandomMatched != null)
            popupRandomMatched.SetActive(true);

        StartCoroutine(CoGoToBattleScene());
    }

    private IEnumerator CoGoToBattleScene()
    {
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene(nextSceneName);
    }
}