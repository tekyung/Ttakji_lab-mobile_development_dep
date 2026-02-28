using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 컨트롤 탈취(Mind Control) 등 일시적인 이동 효과가 만료되었을 때,
    /// 카드를 원래 주인에게 되돌려주기 위해 시스템 내부적으로 생성되는 효과입니다.
    /// (JSON에서 파싱되지 않고 런타임에 동적으로 주입됩니다.)
    /// </summary>
    public class RevertControlEffect : ICardEffect
    {
        private List<Card> _cardsToRevert;
        private Player _originalOwner;
        private ZoneType _returnZone;

        // 시스템 내부에서 동적으로 생성하기 위한 커스텀 생성자
        public RevertControlEffect(List<Card> cards, Player owner, ZoneType zone)
        {
            // 원본 리스트가 변경되어도 영향을 받지 않도록 얕은 복사본을 유지합니다.
            _cardsToRevert = new List<Card>(cards);
            _originalOwner = owner;
            _returnZone = zone;
        }

        public void Initialize(Dictionary<string, object> parameters)
        {
            // 이 클래스는 JSON을 통한 팩토리 초기화(Initialize)를 거치지 않고, 
            // 시스템이 생성자를 통해 직접 셋팅하므로 비워둡니다.
        }

        public void Execute(GameContext context, Action onComplete)
        {
            foreach (var card in _cardsToRevert)
            {
                Player currentController = card.Controller;

                // [안전장치] 카드가 여전히 필드에 존재할 때만 되돌려줍니다.
                // 만약 뺏어온 턴에 카드가 파괴되어 이미 묘지로 갔다면(Controller가 바뀌었거나 필드에 없다면) 무시합니다.
                if (currentController != null && currentController.ExtractCard(ZoneType.Field, card))
                {
                    card.ResetState(); // 스탯 및 소유권 상태 완전 초기화
                    _originalOwner.InsertCard(_returnZone, card);

                    EventManager.OnLogMessage?.Invoke($"↩️ [만료] {card.Name}의 컨트롤이 {_originalOwner.Name}에게 돌아갑니다.");
                    EventManager.OnCardMove?.Invoke(card, currentController, ZoneType.Field, _originalOwner, _returnZone);
                }
            }

            // 물리적인 복귀 처리가 모두 끝나면 다음 체인으로 넘김
            onComplete?.Invoke();
        }

        public ICardEffect Clone()
        {
            // 이 효과는 복제되더라도 '원래 타겟팅했던 바로 그 카드 객체들'을 
            // 동일하게 쥐고 있어야 하므로, 같은 인스턴스 참조를 넘겨서 복제합니다.
            return new RevertControlEffect(this._cardsToRevert, this._originalOwner, this._returnZone);
        }
    }
}