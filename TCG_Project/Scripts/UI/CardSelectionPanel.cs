// CardSelectionPanel.cs — 카드 목록 선택 팝업 (베로니카/엘리/소니아 능력 등)
//
// 구독 이벤트:
//   OnRequireCardPick    → 카드 목록에서 N장 선택 → List<Card> 콜백
//   OnRequireCardChoice  → 존(ZoneType) 카드에서 N장 선택 → List<Card> 콜백
//
// 씬 배치: Canvas > CardSelectionPanel (기본 비활성)
// Inspector 연결:
//   titleText          : 선택 안내 텍스트
//   cardButtonPrefab   : 카드 버튼 Prefab (Text 자식 포함)
//   cardContainer      : 카드 버튼들이 배치될 부모 Transform
//   confirmButton      : "확인" 버튼 (선택 완료)
//   selectionCountText : "X / N 선택됨" 표시 텍스트

using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace TCG_Project.Scripts.UI
{
    public class CardSelectionPanel : MonoBehaviour
    {
        [Header("UI 요소")]
        [SerializeField] private Text      titleText;
        [SerializeField] private GameObject cardButtonPrefab;
        [SerializeField] private Transform  cardContainer;
        [SerializeField] private Button     confirmButton;
        [SerializeField] private Text       selectionCountText;

        private int                     _requiredCount;
        private List<Card>              _candidates   = new List<Card>();
        private List<Card>              _selected     = new List<Card>();
        private Action<List<Card>>      _pickCallback;
        private List<GameObject>        _spawnedButtons = new List<GameObject>();

        private void OnEnable()
        {
            EventManager.OnRequireCardPick   += HandleCardPick;
            EventManager.OnRequireCardChoice += HandleCardChoice;
        }

        private void OnDisable()
        {
            EventManager.OnRequireCardPick   -= HandleCardPick;
            EventManager.OnRequireCardChoice -= HandleCardChoice;
        }

        // ─── OnRequireCardPick: 명시적 카드 목록에서 N장 선택 ─────────────

        private void HandleCardPick(
            Player player, List<Card> candidates, int count,
            Action<List<Card>> onPicked)
        {
            Open("카드를 선택하세요", candidates, count, onPicked);
        }

        // ─── OnRequireCardChoice: 존(ZoneType)에서 N장 선택 ─────────────

        private void HandleCardChoice(
            Player player, ZoneType zone, int count,
            string filter, Action<List<Card>> onChosen)
        {
            var zoneCandidates = player.GetZone(zone);

            // filter 문자열 간단 적용 (type:Attack 형식)
            if (!string.IsNullOrEmpty(filter))
            {
                zoneCandidates = zoneCandidates.FindAll(c => MatchesFilter(c, filter));
            }

            Open($"{zone}에서 {count}장 선택", zoneCandidates, count, onChosen);
        }

        // ─── 공통 열기/닫기 ──────────────────────────────────────────────

        private void Open(
            string title, List<Card> candidates, int count,
            Action<List<Card>> callback)
        {
            _candidates    = candidates;
            _requiredCount = count;
            _pickCallback  = callback;
            _selected.Clear();
            ClearButtons();

            if (titleText != null) titleText.text = title;

            foreach (var card in candidates)
            {
                Card captured = card;
                var btn = Instantiate(cardButtonPrefab, cardContainer);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{card.Name}\n(Cost:{card.Cost} / {card.Type})";

                var toggle = btn.GetComponent<Toggle>();
                if (toggle != null)
                {
                    toggle.isOn = false;
                    toggle.onValueChanged.AddListener(on => OnCardToggled(captured, on));
                }
                else
                {
                    btn.GetComponent<Button>()?.onClick.AddListener(() => OnCardButtonClicked(captured));
                }

                _spawnedButtons.Add(btn);
            }

            confirmButton?.onClick.RemoveAllListeners();
            confirmButton?.onClick.AddListener(OnConfirm);
            UpdateConfirmButton();

            gameObject.SetActive(true);
        }

        private void OnCardToggled(Card card, bool selected)
        {
            if (selected)
            {
                if (!_selected.Contains(card) && _selected.Count < _requiredCount)
                    _selected.Add(card);
            }
            else
            {
                _selected.Remove(card);
            }
            UpdateConfirmButton();
        }

        private void OnCardButtonClicked(Card card)
        {
            if (_selected.Contains(card))
                _selected.Remove(card);
            else if (_selected.Count < _requiredCount)
                _selected.Add(card);

            UpdateConfirmButton();
        }

        private void OnConfirm()
        {
            // 선택 미달 시 앞에서부터 자동 채움 (베로니카 능력 등)
            if (_selected.Count < _requiredCount && _candidates.Count > 0)
            {
                foreach (var c in _candidates)
                {
                    if (!_selected.Contains(c)) _selected.Add(c);
                    if (_selected.Count >= _requiredCount) break;
                }
            }

            var result = new List<Card>(_selected);
            gameObject.SetActive(false);
            ClearButtons();
            _pickCallback?.Invoke(result);
            _pickCallback = null;
        }

        private void UpdateConfirmButton()
        {
            if (confirmButton != null)
                confirmButton.interactable = _selected.Count > 0;

            if (selectionCountText != null)
                selectionCountText.text = $"{_selected.Count} / {_requiredCount} 선택됨";
        }

        private void ClearButtons()
        {
            foreach (var btn in _spawnedButtons)
                if (btn != null) Destroy(btn);
            _spawnedButtons.Clear();
        }

        // ─── 필터 헬퍼 ───────────────────────────────────────────────────

        private static bool MatchesFilter(Card card, string filter)
        {
            foreach (var cond in filter.Split(','))
            {
                var parts = cond.Trim().Split(':');
                if (parts.Length != 2) continue;
                switch (parts[0].Trim())
                {
                    case "type":
                        if (System.Enum.TryParse<CardType>(parts[1].Trim(), true, out CardType t)
                            && card.Type != t) return false;
                        break;
                    case "character":
                        if (card.CharacterId != parts[1].Trim()) return false;
                        break;
                }
            }
            return true;
        }
    }
}
