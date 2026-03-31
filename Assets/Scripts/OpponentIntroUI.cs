using System.Collections;
using UnityEngine;

public class OpponentIntroUI : MonoBehaviour
{
    public GameObject popupOpponentIntro;

    void Start()
    {
        StartCoroutine(CoShowIntro());
    }

    private IEnumerator CoShowIntro()
    {
        if (popupOpponentIntro != null)
            popupOpponentIntro.SetActive(true);

        yield return new WaitForSeconds(1f);

        if (popupOpponentIntro != null)
            popupOpponentIntro.SetActive(false);
    }
}