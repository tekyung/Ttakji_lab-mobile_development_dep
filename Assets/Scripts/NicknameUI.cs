// NicknameUI.cs — 닉네임을 정하고 고치는 창
//
// ★ 왜 필요해졌나
//   커스텀 방에서 호스트는 들어온 사람을 보고 [시작]할지 [퇴장]시킬지 정한다.
//   그런데 이 프로젝트에는 이름이라는 개념이 없어서, 서버에 올라가던 값은
//   session_manage의 폴백인 <c>Random.Range(0, 1000000)</c> — 즉 "482913" 같은 숫자였다.
//
// 값 자체는 PlayerProfile이 파일로 들고 있다. 이 스크립트는 창을 여닫고 넘겨줄 뿐이다.
using TMPro;
using UnityEngine;

public class NicknameUI : MonoBehaviour
{
    [Header("창")]
    public GameObject popupNickname;

    [Header("입력")]
    public TMP_InputField inputNickname;

    [Tooltip("메인 화면에 지금 이름을 보여 주는 자리. 눌러서 이 창을 연다.")]
    public TMP_Text labelNickname;

    private void Start()
    {
        RefreshLabel();

        // 이름을 정한 적이 없으면 먼저 물어본다. 그래야 상대 화면에 숫자가 뜨지 않는다.
        if (!PlayerProfile.Exists)
            OpenNicknamePopup();
        else if (popupNickname != null)
            popupNickname.SetActive(false);
    }

    /// <summary>창을 연다. 지금 이름을 미리 채워 두어 고치기 쉽게 한다.</summary>
    public void OpenNicknamePopup()
    {
        if (inputNickname != null)
        {
            inputNickname.characterLimit = PlayerProfile.MaxNicknameLength;

            // 첫 실행이면 "이름없음"을 채워 두지 않는다 — 지우고 쓰게 만들 이유가 없다.
            inputNickname.text = PlayerProfile.Exists ? PlayerProfile.Nickname : "";
        }

        if (popupNickname != null) popupNickname.SetActive(true);
    }

    /// <summary>[확인] — 저장하고 닫는다. 비어 있으면 닫지 않는다.</summary>
    public void ConfirmNickname()
    {
        string typed = inputNickname != null ? inputNickname.text : null;

        if (string.IsNullOrWhiteSpace(typed))
        {
            Debug.Log("[NicknameUI] 이름이 비어 있어 저장하지 않았습니다.");
            return;
        }

        PlayerProfile.SetNickname(typed);
        RefreshLabel();

        if (popupNickname != null) popupNickname.SetActive(false);
    }

    /// <summary>
    /// [닫기] — 저장하지 않고 닫는다.
    ///
    /// ★ 이름을 아직 한 번도 정하지 않았다면 닫지 않는다. 닫아 버리면 상대에게
    ///   "이름없음#1234"로 보이고, 다시 열 방법을 모르는 사람은 그대로 대전에 들어간다.
    /// </summary>
    public void CloseNicknamePopup()
    {
        if (!PlayerProfile.Exists)
        {
            Debug.Log("[NicknameUI] 아직 이름을 정하지 않아 창을 닫지 않습니다.");
            return;
        }

        if (popupNickname != null) popupNickname.SetActive(false);
    }

    private void RefreshLabel()
    {
        if (labelNickname == null) return;

        labelNickname.text = PlayerProfile.Exists ? PlayerProfile.DisplayName : "이름 정하기";
    }
}
