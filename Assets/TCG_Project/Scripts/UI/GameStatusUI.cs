// GameStatusUI.cs — 게임 상태 표시 UI
// 구독 이벤트: OnLifeChange, OnTurnStart, OnLogMessage, OnGameStart
//
// 씬 배치: Canvas > GameStatusPanel 오브젝트에 이 컴포넌트를 추가한다.
// Inspector 연결:
//   p1LifeText    : Player1 라이프 텍스트
//   p2LifeText    : Player2 라이프 텍스트
//   turnText      : 현재 턴 텍스트
//   logText       : 게임 로그 텍스트 (ScrollView 내 Text 권장)
//   maxLogLines   : 로그 최대 줄 수 (기본 30)

using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace TCG_Project.Scripts.UI
{
    public class GameStatusUI : MonoBehaviour
    {
        [Header("라이프 표시")]
        [SerializeField] private Text p1LifeText;
        [SerializeField] private Text p2LifeText;

        [Header("턴 표시")]
        [SerializeField] private Text turnText;

        [Header("로그")]
        [SerializeField] private Text logText;
        [SerializeField] private int  maxLogLines = 30;

        private readonly Queue<string> _logQueue = new Queue<string>();

        // 플레이어 이름 캐싱 (OnGameStart에서 설정)
        private string _p1Name = "Player1";
        private string _p2Name = "Player2";

        private void OnEnable()
        {
            EventManager.OnGameStart  += HandleGameStart;
            EventManager.OnLifeChange += HandleLifeChange;
            EventManager.OnTurnStart  += HandleTurnStart;
            EventManager.OnLogMessage += HandleLogMessage;
        }

        private void OnDisable()
        {
            EventManager.OnGameStart  -= HandleGameStart;
            EventManager.OnLifeChange -= HandleLifeChange;
            EventManager.OnTurnStart  -= HandleTurnStart;
            EventManager.OnLogMessage -= HandleLogMessage;
        }

        private void HandleGameStart(Player p1, Player p2)
        {
            _p1Name = p1.Name;
            _p2Name = p2.Name;
            RefreshLifeText(p1.Name, p1.LifeTokens);
            RefreshLifeText(p2.Name, p2.LifeTokens);
        }

        private void HandleLifeChange(Player player, int newLife)
        {
            RefreshLifeText(player.Name, newLife);
        }

        private void HandleTurnStart(int turn, string playerName)
        {
            if (turnText != null)
                turnText.text = $"Turn {turn}";
        }

        private void HandleLogMessage(string message)
        {
            _logQueue.Enqueue(message);
            while (_logQueue.Count > maxLogLines)
                _logQueue.Dequeue();

            if (logText != null)
                logText.text = string.Join("\n", _logQueue);
        }

        private void RefreshLifeText(string playerName, int life)
        {
            if (playerName == _p1Name && p1LifeText != null)
                p1LifeText.text = $"{playerName} ♥ {life}";
            else if (playerName == _p2Name && p2LifeText != null)
                p2LifeText.text = $"{playerName} ♥ {life}";
        }
    }
}
