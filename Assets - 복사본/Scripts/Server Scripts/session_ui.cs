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
        if (session_start != null)
        {
            session_start.interactable = host;
        }
    }

    public void ToggleUI(bool isOn) //버튼 ON OFF
    {
        if (session_id != null) session_id.interactable = isOn;
        if (session_create != null) session_create.interactable = isOn;
        if (session_join != null) session_join.interactable = isOn;
        if (session_code != null) session_code.interactable = isOn;
        if (session_random_match != null) session_random_match.interactable = isOn;
    }
    public void SetActionButtonsState(bool isActive)
    {
        if (btn_action_A != null) btn_action_A.interactable = isActive;
        if (btn_action_B != null) btn_action_B.interactable = isActive;
        if (btn_action_C != null) btn_action_C.interactable = isActive;
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
        foreach (var pair in actionButtons)
        {
            if (pair.type == targetType && pair.button != null)
            {
                ColorBlock cb = pair.button.colors;
                cb.normalColor = Color.green;
                cb.selectedColor = Color.green;
                cb.highlightedColor = Color.green;
                cb.pressedColor = Color.green;
                cb.disabledColor = Color.green;

                pair.button.colors = cb;
            }
        }
    }

    public void ResetButtons()
    {
        foreach (var pair in actionButtons)
        {
            if (pair.button != null)
            {
                // image.color = Color.white 대신 ColorBlock 방식을 사용합니다.
                ColorBlock cb = pair.button.colors;
                cb.normalColor = Color.white;
                cb.selectedColor = Color.white;
                cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f);
                cb.pressedColor = new Color(0.78f, 0.78f, 0.78f);
                cb.disabledColor = new Color(0.78f, 0.78f, 0.78f);

                pair.button.colors = cb;
            }
        }
    }
}