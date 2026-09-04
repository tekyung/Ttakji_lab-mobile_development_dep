// SceneChanger.cs — 버튼에 걸어 쓰는 씬 이동 도구.
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    [Header("봇전")]
    [Tooltip("[봇과 대전] 버튼이 열 씬. 대전 설정 화면이 들어 있는 씬이어야 한다.")]
    [SerializeField] private string botMatchSceneName = "TestGameScene";

    public void ChageScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 봇과의 대전을 시작한다. 씬에 들어가면 대전 설정 화면이 떠서
    /// 양쪽 덱·용병·사람/봇을 고를 수 있다.
    ///
    /// ★ 세션 흔적을 먼저 지우는 것이 핵심이다.
    ///   <c>GameData.SessionCode</c>는 static이라 씬을 넘어 살아남는다.
    ///   앞서 온라인 대전을 하고 돌아왔다면 그 코드가 남아 있고,
    ///   그러면 <c>LocalMatchStarter</c>가 "온라인이다"라고 판단해 통째로 물러나
    ///   <b>봇전이 아예 시작되지 않는다.</b>
    ///   (세션 코드가 비어 있으면 ClearSession은 아무 일도 하지 않으므로 무조건 불러도 된다)
    /// </summary>
    public void StartBotMatch()
    {
        OnlineMatchStarter.ClearSession();
        SceneManager.LoadScene(botMatchSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
