using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class session_ui : MonoBehaviour
{
    public TMP_InputField session_id;       //사용자의 id   
    public TMP_InputField session_code;     //세션입장코드
    public TextMeshProUGUI session_status;  //세션상태
    public Button session_create;           //세션생성
    public Button session_join;             //세션입장
    public Button session_random_match;     //랜덤매칭
    public Button session_start;            //세션시작
    public Button session_exit;             //세션나가기

  
    public string GetID()  //사용자ID를 반환            
    {
        return session_id.text; 
    }
    public string GetSessionCode()  //세션입장코드 반환
    {
        return session_code.text;
    }

    public void UpdateStatus(string status) //세션상태업데이트
    {
        session_status.text = status;
        Debug.Log(status);
    }

    public void ToggleHost(bool host)   //버튼 ON OFF
    {
        session_start.interactable = host;
    }

    public void ToggleUI(bool isOn) //버튼 ON OFF
    {
        session_id.interactable = isOn;
        session_create.interactable = isOn;
        session_join.interactable = isOn;
        session_code.interactable = isOn;
        session_random_match.interactable = isOn;
    }
}