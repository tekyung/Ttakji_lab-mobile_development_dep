// StackResponsePanel.cs — 스택 발동 여부 Human 입력 팝업
//
// 구독 이벤트:
//   OnRequireStackResponse → 스택 카드 발동 / 패스 선택 → 콜백 반환
//
// 씬 배치: Canvas > StackResponsePanel (기본 비활성)
// Inspector 연결:
//   stackCardNameText    : 발동 여부를 묻는 스택 카드 이름
//   triggerCardNameText  : 상대가 발동한 카드 이름
//   activateButton       : "발동" 버튼
//   passButton           : "패스" 버튼
//   autoPassSeconds      : 자동 패스까지의 초 (0이면 자동 패스 없음)

using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace TCG_Project.Scripts.UI
{
    public class StackResponsePanel : MonoBehaviour
    {
        [Header("카드 정보 텍스트")]
        [SerializeField] private Text stackCardNameText;
        [SerializeField] private Text triggerCardNameText;

        [Header("선택 버튼")]
        [SerializeField] private Button activateButton;
        [SerializeField] private Button passButton;

        [Header("자동 패스 (0=비활성)")]
        [SerializeField] private float autoPassSeconds = 5f;
        [SerializeField] private Text  countdownText;

        private Action<bool> _responseCallback;
        private float        _countdown;
        private bool         _waiting;

        private void OnEnable()
        {
            EventManager.OnRequireStackResponse += HandleStackResponseRequest;
        }

        private void OnDisable()
        {
            EventManager.OnRequireStackResponse -= HandleStackResponseRequest;
        }

        private void Update()
        {
            if (!_waiting || autoPassSeconds <= 0f) return;

            _countdown -= Time.deltaTime;
            if (countdownText != null)
                countdownText.text = $"자동 패스: {Mathf.CeilToInt(_countdown)}초";

            if (_countdown <= 0f)
                Respond(false);
        }

        private void HandleStackResponseRequest(
            Player stackOwner, Card stackCard, Card playedCard,
            Action<bool> onResponse)
        {
            _responseCallback = onResponse;

            if (stackCardNameText != null)
                stackCardNameText.text =
                    $"[{stackCard.Name}]  발동?  (Cost:{stackCard.Cost} / {stackCard.Type})";

            if (triggerCardNameText != null)
                triggerCardNameText.text =
                    $"상대 카드: [{playedCard.Name}]  (Spd:{playedCard.Speed} / {playedCard.Type})";

            activateButton?.onClick.RemoveAllListeners();
            passButton?.onClick.RemoveAllListeners();
            activateButton?.onClick.AddListener(() => Respond(true));
            passButton?.onClick.AddListener(() => Respond(false));

            _countdown = autoPassSeconds;
            _waiting   = true;
            gameObject.SetActive(true);
        }

        private void Respond(bool activate)
        {
            _waiting = false;
            gameObject.SetActive(false);
            _responseCallback?.Invoke(activate);
            _responseCallback = null;
        }
    }
}
