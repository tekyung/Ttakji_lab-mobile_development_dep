using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static session_manage;

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

    public Button btn_action_A; // 행동 A
    public Button btn_action_B; // 행동 B
    public Button btn_action_C; // 행동 C

    public TextMeshProUGUI session_timer;

    [System.Serializable]
    public class ActionButton
    {
        public ActionType type; // 행동 종류 (A, B...)
        public Button button;   // 연결할 버튼
    }
    public List<ActionButton> actionButtons;

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
    public void SetActionButtonsState(bool isActive)
    {
         btn_action_A.interactable = isActive;
         btn_action_B.interactable = isActive;
         btn_action_C.interactable = isActive;
    }

    public void DestroySessionTimer(float time) //세션 제한 시간
    {
        if (session_timer == null) return;
        if (time > 0)
        {
            session_timer.text = $"Left session : {Mathf.CeilToInt(time)}";
        }
        else
        {
            session_timer.text = "";
        }
    }
    public void CheckButton(ActionType targetType)
    {
        // 요청받은 타입과 똑같은 버튼을 찾음
        foreach (var pair in actionButtons)
        {
            if (pair.type == targetType)
            {
                if (pair.button != null)
                    pair.button.image.color = Color.green;
            }
        }
    }

    public void ResetButtons()
    {
        foreach (var pair in actionButtons)
        {
            if (pair.button != null)
                pair.button.image.color = Color.white;
        }
    }
}