using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyUI : MonoBehaviour
{
    public string deckEditSceneName = "SampleScene";

    public void OnClickMatch()
    {
        Debug.Log("Match Start (TODO)");
        // TODO: ¸ÅÄª ¾À/¸ÅÄª ·ÎÁ÷ ¿¬°á
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
}