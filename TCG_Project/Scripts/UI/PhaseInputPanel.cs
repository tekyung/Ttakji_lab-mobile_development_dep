// PhaseInputPanel.cs — 세트 페이즈 / 오픈 페이즈 Human 입력 패널
//
// 구독 이벤트:
//   OnRequireSetPhaseAction  → 패 카드 목록 표시 + 카드 버튼 클릭 → 세트 완료
//   OnRequireOpenPhaseAction → "공개" / "폐기" 버튼 표시 → 선택 완료
//
// 씬 배치: Canvas > PhaseInputPanel (기본 비활성)
// Inspector 연결:
//   setPhasePanel      : 세트 페이즈 서브 패널
//   openPhasePanel     : 오픈 페이즈 서브 패널
//   cardButtonPrefab   : 패 카드 표시용 버튼 Prefab (Text 자식 포함)
//   handContainer      : 패 카드 버튼들이 배치될 부모 Transform
//   openButton         : "공개" 버튼
//   abandonButton      : "폐기" 버튼
//   setCardInfoText    : 세트 카드 이름 표시 텍스트 (오픈 패널)
//   costText           : 유효 코스트 표시 텍스트 (오픈 패널)

using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace TCG_Project.Scripts.UI
{
    public class PhaseInputPanel : MonoBehaviour
    {
        [Header("세트 페이즈")]
        [SerializeField] private GameObject setPhasePanel;
        [SerializeField] private GameObject cardButtonPrefab;
        [SerializeField] private Transform  handContainer;

        [Header("오픈 페이즈")]
        [SerializeField] private GameObject openPhasePanel;
        [SerializeField] private Button     openButton;
        [SerializeField] private Button     abandonButton;
        [SerializeField] private Text       setCardInfoText;
        [SerializeField] private Text       costText;

        // 현재 콜백 저장 (응답 완료 시 호출)
        private Action<Card>              _setCallback;
        private Action<OpenPhaseChoice>   _openCallback;
        private List<GameObject>          _spawnedButtons = new List<GameObject>();

        private void OnEnable()
        {
            EventManager.OnRequireSetPhaseAction  += HandleSetPhaseRequest;
            EventManager.OnRequireOpenPhaseAction += HandleOpenPhaseRequest;
        }

        private void OnDisable()
        {
            EventManager.OnRequireSetPhaseAction  -= HandleSetPhaseRequest;
            EventManager.OnRequireOpenPhaseAction -= HandleOpenPhaseRequest;
        }

        // ─── 세트 페이즈 ────────────────────────────────────────────────

        private void HandleSetPhaseRequest(Player player, GameContext context, Action<Card> onCardSelected)
        {
            _setCallback = onCardSelected;
            ShowSetPanel(player.Hand);
        }

        private void ShowSetPanel(List<Card> hand)
        {
            ClearHandButtons();
            if (setPhasePanel != null) setPhasePanel.SetActive(true);
            if (openPhasePanel != null) openPhasePanel.SetActive(false);
            gameObject.SetActive(true);

            foreach (var card in hand)
            {
                Card captured = card;
                var btn = Instantiate(cardButtonPrefab, handContainer);
                var label = btn.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"{card.Name}\n(Cost:{card.Cost} / {card.Type} / Spd:{card.Speed})";

                btn.GetComponent<Button>()?.onClick.AddListener(() => OnSetCardClicked(captured));
                _spawnedButtons.Add(btn);
            }
        }

        private void OnSetCardClicked(Card card)
        {
            gameObject.SetActive(false);
            ClearHandButtons();
            _setCallback?.Invoke(card);
            _setCallback = null;
        }

        // ─── 오픈 페이즈 ────────────────────────────────────────────────

        private void HandleOpenPhaseRequest(
            Player player, Card setCard, int effectiveCost,
            GameContext context, Action<OpenPhaseChoice> onChoiceSelected)
        {
            _openCallback = onChoiceSelected;
            ShowOpenPanel(setCard, effectiveCost, player.CanAfford(effectiveCost));
        }

        private void ShowOpenPanel(Card setCard, int effectiveCost, bool canAfford)
        {
            ClearHandButtons();
            if (setPhasePanel  != null) setPhasePanel.SetActive(false);
            if (openPhasePanel != null) openPhasePanel.SetActive(true);
            gameObject.SetActive(true);

            if (setCardInfoText != null)
                setCardInfoText.text = $"{setCard.Name}  [{setCard.Type} / Spd:{setCard.Speed}]";

            if (costText != null)
                costText.text = canAfford
                    ? $"코스트: {effectiveCost}  (지불 가능)"
                    : $"코스트: {effectiveCost}  (부족 시 메인에서 효과 없이 폐기)";

            openButton?.onClick.RemoveAllListeners();
            abandonButton?.onClick.RemoveAllListeners();

            // 룰: 오픈 선언은 가능. 코스트 미달은 메인 페이즈 지불 단계에서 폐기 처리.
            if (openButton != null) openButton.interactable = true;

            openButton?.onClick.AddListener(() => OnOpenChoice(OpenPhaseChoice.Open));
            abandonButton?.onClick.AddListener(() => OnOpenChoice(OpenPhaseChoice.Abandon));
        }

        private void OnOpenChoice(OpenPhaseChoice choice)
        {
            gameObject.SetActive(false);
            _openCallback?.Invoke(choice);
            _openCallback = null;
        }

        // ─── 공통 ────────────────────────────────────────────────────────

        private void ClearHandButtons()
        {
            foreach (var btn in _spawnedButtons)
                if (btn != null) Destroy(btn);
            _spawnedButtons.Clear();
        }
    }
}
