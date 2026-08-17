using System.Collections;
using UnityEngine;

public class OpponentIntroUI : MonoBehaviour
{
    public GameObject popupOpponentIntro;

    void Start()
    {
        StartCoroutine(ShowIntro());
    }

    IEnumerator ShowIntro()
    {
        // 처음에 켜기
        if (popupOpponentIntro != null)
            popupOpponentIntro.SetActive(true);

        // 1초 기다림
        yield return new WaitForSeconds(1f);

        // 끄기
        if (popupOpponentIntro != null)
            popupOpponentIntro.SetActive(false);
    }
}