using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CustomRoomUI : MonoBehaviour
{
    public GameObject popupCustomMenu;
    public GameObject popupCreateRoom;

    public TMP_Text txtRoomNumberValue;
    public TMP_Text txtPlayerCount;
    public TMP_Dropdown tmpAIDropdown;

    public Button btnStartRoom;
    public string nextSceneName = "TestGameScene";

    private int playerCount = 1;
    private string roomCode = "";

    public void OpenCreateRoom()
    {
        roomCode = GenerateRoomCode();
        playerCount = 1;

        if (popupCustomMenu != null)
            popupCustomMenu.SetActive(false);

        if (popupCreateRoom != null)
            popupCreateRoom.SetActive(true);

        if (tmpAIDropdown != null)
        {
            tmpAIDropdown.ClearOptions();
            tmpAIDropdown.AddOptions(new System.Collections.Generic.List<string> { "None", "AI" });
            tmpAIDropdown.value = 0;
            tmpAIDropdown.RefreshShownValue();
        }

        RefreshRoomUI();
    }

    public void LeaveRoom()
    {
        if (popupCreateRoom != null)
            popupCreateRoom.SetActive(false);

        if (popupCustomMenu != null)
            popupCustomMenu.SetActive(true);
    }

    public void OnChangeAI(int index)
    {
        if (index == 0)
            playerCount = 1;
        else
            playerCount = 2;

        RefreshRoomUI();
    }

    public void StartRoomGame()
    {
        if (playerCount < 2)
            return;

        SceneManager.LoadScene(nextSceneName);
    }

    private void RefreshRoomUI()
    {
        if (txtRoomNumberValue != null)
            txtRoomNumberValue.text = "Room: " + roomCode;

        if (txtPlayerCount != null)
            txtPlayerCount.text = "Players : " + playerCount;

        if (btnStartRoom != null)
            btnStartRoom.interactable = (playerCount >= 2);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string result = "";

        for (int i = 0; i < 6; i++)
        {
            result += chars[Random.Range(0, chars.Length)];
        }

        return result;
    }
}