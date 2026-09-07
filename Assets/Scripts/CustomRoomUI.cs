// CustomRoomUI.cs — 커스텀(방 번호) 매칭 화면
//
// ★ 예전에는 서버와 한 줄도 이어져 있지 않았다.
//   방 번호를 <b>로컬 난수</b>로 만들어 화면에만 띄웠고(서버에 그 방은 없었다),
//   [시작]은 GameData.SessionCode를 채우지 않은 채 씬만 넘겨 <b>로컬 봇전</b>을 시작했다.
//   방 참가는 아예 배선조차 없었다.
//
//   서버에는 필요한 것이 이미 다 있었다 — session_manage의 비공개 방 생성·입장·퇴장·시작.
//   아무도 부르지 않았을 뿐이다. 이 파일이 그 사이를 잇는다.
//
// ★ 콜백은 대입(=)이다. RandomMatchUI와 <b>같은 자리를 두고 다툰다.</b>
//   패널을 닫을 때 반드시 null로 되돌려야 랜덤 매칭이 다시 동작한다.
//   (RandomMatchUI.CancelRandomMatch가 하는 그대로다)
//
// ★ 화면을 닫을 때는 PopupDim까지 걷어야 한다.
//   PopupDim은 raycastTarget이 켜진 전체 화면 막이라, 창만 닫으면 화면은 멀쩡해 보이는데
//   <b>어떤 버튼도 눌리지 않는다.</b> LobbyUI.CloseMatchMode()가 그 짝이다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CustomRoomUI : MonoBehaviour
{
    [Header("세션 매니저")]
    [SerializeField] private session_manage matchManager;

    [Header("팝업")]
    public GameObject popupCustomMenu;
    public GameObject popupCreateRoom;
    public GameObject popupFindRoom;

    [Tooltip("그 번호의 방이 없을 때 뜬다.")]
    public GameObject popupRoomNotFound;

    [Tooltip("이미 두 사람이 찬 방일 때 뜬다.")]
    public GameObject popupRoomFull;

    [Header("방 화면")]
    public TMP_Text txtRoomNumberValue;
    public TMP_Text txtPlayerCount;

    [Tooltip("내 이름 칸(아래/왼쪽).")]
    public TMP_Text txtPlayer1;

    [Tooltip("내 덱 이름 칸. 상대 덱은 대전 씬에 들어가야 올라오므로 비워 둔다.")]
    public TMP_Text txtDeck1;

    [Tooltip("상대 이름 칸(위/오른쪽).")]
    public TMP_Text txtPlayer2;

    [Header("버튼")]
    [Tooltip("호스트만 보인다. 상대가 들어와야 눌린다.")]
    public Button btnStartRoom;

    [Tooltip("호스트만 보인다. 상대만 내보내고 방은 남긴다.")]
    public Button btnKickGuest;

    [Header("방 찾기")]
    public TMP_InputField inputRoomCode;

    /// <summary>내가 지금 이 화면의 주인인가. 콜백을 쥐고 있는 동안만 true다.</summary>
    private bool _owning;

    // ─── 방 만들기 (호스트) ─────────────────────────────────────────────

    public void OpenCreateRoom()
    {
        if (!EnsureManager()) return;

        Claim();

        if (popupCustomMenu != null) popupCustomMenu.SetActive(false);
        if (popupFindRoom != null) popupFindRoom.SetActive(false);
        if (popupCreateRoom != null) popupCreateRoom.SetActive(true);

        ShowWaitingRoom(hostSide: true);
        matchManager.OnClickCreateCustomRoom();
    }

    // ─── 방 찾기 (게스트) ───────────────────────────────────────────────

    /// <summary>[입장] — 입력한 번호로 들어간다.</summary>
    public void JoinRoom()
    {
        if (!EnsureManager()) return;

        string code = inputRoomCode != null ? inputRoomCode.text : null;
        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.Log("[CustomRoomUI] 방 번호가 비어 있습니다.");
            return;
        }

        Claim();
        matchManager.JoinRoomByCode(code);
    }

    // ─── 호스트의 세 선택 ───────────────────────────────────────────────

    /// <summary>[시작] — 세션을 PLAYING으로 올린다. 씬 이동은 session_manage가 한다.</summary>
    public void StartRoomGame()
    {
        if (!EnsureManager()) return;
        if (!matchManager.IsHost) return;
        if (string.IsNullOrEmpty(matchManager.OpponentName)) return;

        matchManager.OnClickSessionStart();
    }

    /// <summary>[퇴장] — 상대만 내보낸다. 방은 남아 다음 사람을 기다린다.</summary>
    public void KickGuest()
    {
        if (!EnsureManager()) return;
        if (!matchManager.IsHost) return;

        matchManager.KickGuest();
    }

    /// <summary>[나가기] — 방을 떠난다. 호스트가 누르면 방 자체가 사라진다.</summary>
    public void LeaveRoom()
    {
        if (matchManager != null)
            matchManager.OnClickExitSession();

        Release();
        CloseAllRoomPopups();

        if (popupCustomMenu != null) popupCustomMenu.SetActive(true);
    }

    // ─── 서버가 알려 오는 것들 ──────────────────────────────────────────

    private void HandleStatus(string message)
    {
        // 어떤 알림이 오든 서버 상태를 다시 읽어 그린다. 화면이 스스로 상태를 기억하지 않게 한다.
        RefreshFromManager();
        Debug.Log($"[CustomRoomUI] {message}");
    }

    /// <summary>호스트: 게스트가 들어왔다 / 게스트: 내가 입장에 성공했다.</summary>
    private void HandleMatched()
    {
        if (matchManager == null) return;

        if (!matchManager.IsHost)
        {
            // 게스트는 방 찾기 창을 닫고 같은 방 화면으로 옮겨 간다.
            if (popupFindRoom != null) popupFindRoom.SetActive(false);
            if (popupCustomMenu != null) popupCustomMenu.SetActive(false);
            if (popupCreateRoom != null) popupCreateRoom.SetActive(true);

            ShowWaitingRoom(hostSide: false);
        }

        RefreshFromManager();
    }

    private void HandleGuestJoined(string guestName)
    {
        RefreshFromManager();
        Debug.Log($"[CustomRoomUI] 상대가 들어왔습니다: {guestName}");
    }

    private void HandleGuestLeft()
    {
        RefreshFromManager();
    }

    /// <summary>게스트가 방에서 빠졌다 — 내보내졌거나 호스트가 방을 닫았다.</summary>
    private void HandleKicked()
    {
        Release();
        CloseAllRoomPopups();

        if (popupCustomMenu != null) popupCustomMenu.SetActive(true);
    }

    private void HandleJoinFailed(JoinResult result)
    {
        Release();

        // 방 찾기 창은 열어 둔다 — 번호를 고쳐 다시 시도할 수 있어야 한다.
        GameObject notice = result == JoinResult.NotFound ? popupRoomNotFound : popupRoomFull;
        if (notice != null) notice.SetActive(true);
    }

    // ─── 화면 그리기 ────────────────────────────────────────────────────

    /// <summary>아직 아무도 없는 방의 모습. 방 번호는 서버 응답이 와야 채워진다.</summary>
    private void ShowWaitingRoom(bool hostSide)
    {
        if (txtRoomNumberValue != null) txtRoomNumberValue.text = "방 번호 …";
        if (txtPlayerCount != null) txtPlayerCount.text = "1 / 2";

        if (txtPlayer1 != null) txtPlayer1.text = PlayerProfile.DisplayName;
        if (txtDeck1 != null) txtDeck1.text = PlayerStorage.GetSelectedDeck();
        if (txtPlayer2 != null) txtPlayer2.text = "상대를 기다리는 중…";

        // 호스트만 상대를 판단한다. 게스트에게는 두 버튼 모두 의미가 없다.
        if (btnStartRoom != null) btnStartRoom.gameObject.SetActive(hostSide);
        if (btnKickGuest != null) btnKickGuest.gameObject.SetActive(hostSide);

        if (btnStartRoom != null) btnStartRoom.interactable = false;
        if (btnKickGuest != null) btnKickGuest.interactable = false;
    }

    /// <summary>서버 상태를 그대로 옮겨 그린다.</summary>
    private void RefreshFromManager()
    {
        if (matchManager == null) return;

        string code = matchManager.CurrentSessionCode;
        string opponent = matchManager.OpponentName;
        bool hasOpponent = !string.IsNullOrEmpty(opponent);
        bool host = matchManager.IsHost;

        if (txtRoomNumberValue != null)
            txtRoomNumberValue.text = string.IsNullOrEmpty(code) ? "방 번호 …" : $"방 번호 {code}";

        if (txtPlayerCount != null)
            txtPlayerCount.text = hasOpponent ? "2 / 2" : "1 / 2";

        if (txtPlayer1 != null) txtPlayer1.text = PlayerProfile.DisplayName;
        if (txtDeck1 != null) txtDeck1.text = PlayerStorage.GetSelectedDeck();

        if (txtPlayer2 != null)
            txtPlayer2.text = hasOpponent ? opponent : "상대를 기다리는 중…";

        if (btnStartRoom != null)
        {
            btnStartRoom.gameObject.SetActive(host);
            btnStartRoom.interactable = host && hasOpponent;
        }

        if (btnKickGuest != null)
        {
            btnKickGuest.gameObject.SetActive(host);
            btnKickGuest.interactable = host && hasOpponent;
        }
    }

    // ─── 콜백 주고받기 ──────────────────────────────────────────────────
    //
    // session_manage의 콜백은 <b>대입</b>이라 한 번에 하나의 화면만 가질 수 있다.
    // 랜덤 매칭과 번갈아 쓰므로, 쓰는 동안만 쥐고 끝나면 반드시 놓는다.

    private void Claim()
    {
        if (matchManager == null || _owning) return;

        matchManager.onStatusUpdate = HandleStatus;
        matchManager.onMatchedAndReady = HandleMatched;
        matchManager.onGuestJoinedName = HandleGuestJoined;
        matchManager.onGuestLeft = HandleGuestLeft;
        matchManager.onKickedFromRoom = HandleKicked;
        matchManager.onJoinFailed = HandleJoinFailed;

        _owning = true;
    }

    private void Release()
    {
        if (matchManager == null || !_owning) return;

        matchManager.onStatusUpdate = null;
        matchManager.onMatchedAndReady = null;
        matchManager.onGuestJoinedName = null;
        matchManager.onGuestLeft = null;
        matchManager.onKickedFromRoom = null;
        matchManager.onJoinFailed = null;

        _owning = false;
    }

    private void OnDisable() => Release();

    // ─── 잡일 ───────────────────────────────────────────────────────────

    private bool EnsureManager()
    {
        if (matchManager != null) return true;

        matchManager = FindAnyObjectByType<session_manage>();
        if (matchManager != null) return true;

        Debug.LogError("[CustomRoomUI] session_manage를 찾지 못했습니다. 인스펙터에 연결해 주세요.");
        return false;
    }

    /// <summary>방 관련 창을 모두 닫고 화면 막까지 걷는다.</summary>
    private void CloseAllRoomPopups()
    {
        if (popupCreateRoom != null) popupCreateRoom.SetActive(false);
        if (popupFindRoom != null) popupFindRoom.SetActive(false);
        if (popupRoomNotFound != null) popupRoomNotFound.SetActive(false);
        if (popupRoomFull != null) popupRoomFull.SetActive(false);
    }

    /// <summary>매칭 흐름에서 완전히 빠져나온다(PopupDim 포함). 취소 경로가 늘어도 막이 새지 않게 한다.</summary>
    public void CloseCustomMatch()
    {
        LeaveRoom();

        if (popupCustomMenu != null) popupCustomMenu.SetActive(false);

        LobbyUI lobby = GetComponent<LobbyUI>();
        if (lobby == null) lobby = FindAnyObjectByType<LobbyUI>();

        if (lobby != null) lobby.CloseMatchMode();
        else Debug.LogWarning("[CustomRoomUI] LobbyUI를 찾지 못해 화면 막을 걷지 못했습니다.");
    }
}
