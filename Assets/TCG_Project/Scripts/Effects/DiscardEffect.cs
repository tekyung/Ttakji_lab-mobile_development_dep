using System;
using System.Collections.Generic;
using UnityEngine;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    // JSON의 count와 mode 값에 따라 '전체/랜덤/선택' 버리기를 모두 수행하는 만능 효과
    public class DiscardEffect : ICardEffect
    {
        public bool IsStackAction { get; set; }
        public bool RequirePreviousSuccess { get; set; }

        // JSON에서 받아올 세팅값들
        private int _count = 1;
        private string _mode = "random"; // 기본값 설정

        // 복제를 위한 데이터 저장소
        private Dictionary<string, object> _cachedParams;

        // 1. 초기화: JSON에서 넘어온 데이터를 읽어서 세팅합니다.
        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters;

            if (parameters != null)
            {
                // count 읽어오기 (안전한 형변환)
                if (parameters.TryGetValue("count", out object countObj))
                {
                    _count = Convert.ToInt32(countObj);
                }

                // mode 읽어오기 ("all", "random", "choose")
                if (parameters.TryGetValue("mode", out object modeObj))
                {
                    _mode = modeObj.ToString().ToLower();
                }

                // 스택 반응 및 이전 성공 여부 옵션이 있다면 읽어오기
                if (parameters.TryGetValue("isStackAction", out object stackObj))
                    IsStackAction = Convert.ToBoolean(stackObj);

                if (parameters.TryGetValue("requirePreviousSuccess", out object reqObj))
                    RequirePreviousSuccess = Convert.ToBoolean(reqObj);
            }
        }

        // 2. 복제: 완전히 동일한 세팅을 가진 분신을 만듭니다.
        public ICardEffect Clone()
        {
            var clone = new DiscardEffect();
            clone.Initialize(_cachedParams);
            clone.IsStackAction = this.IsStackAction;
            clone.RequirePreviousSuccess = this.RequirePreviousSuccess;
            return clone;
        }

        // 3. 발동: 실제로 카드를 버리는 로직!
        public void Execute(GameContext context, Action onComplete)
        {
            Player owner = context.ActivePlayer;

            // 방어 코드: 버릴 패가 없으면 바로 종료
            if (owner.Hand.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke("🎲 [패 버리기] 손에 버릴 카드가 없습니다.");
                onComplete?.Invoke();
                return;
            }

            // ─── [모드 1] 몽땅 버리기 ("all") ───
            if (_mode == "all")
            {
                int totalDiscarded = 0;
                // 리스트를 순회하며 삭제할 땐 '복사본'을 만들어야 에러가 안 납니다!
                var cardsToDiscard = new List<Card>(owner.Hand);

                foreach (var card in cardsToDiscard)
                {
                    if (owner.ExtractCard(ZoneType.Hand, card))
                    {
                        owner.InsertCard(ZoneType.Graveyard, card);
                        EventManager.OnCardMove?.Invoke(card, owner, ZoneType.Hand, owner, ZoneType.Graveyard);
                        totalDiscarded++;
                    }
                }
                EventManager.OnLogMessage?.Invoke($"🎲 [전체 버리기] 패의 모든 카드({totalDiscarded}장)를 무덤으로 보냈습니다!");
            }
            // ─── [모드 2] 랜덤 버리기 ("random") ───
            else if (_mode == "random")
            {
                // 요구 수량(count)이 내 패보다 많으면 패 장수만큼만 버립니다.
                int actualDiscardCount = Mathf.Min(_count, owner.Hand.Count);

                for (int i = 0; i < actualDiscardCount; i++)
                {
                    // 버릴 때마다 남은 패의 장수가 변하므로 매번 새로 랜덤을 돌립니다.
                    int randomIndex = UnityEngine.Random.Range(0, owner.Hand.Count);
                    Card cardToDiscard = owner.Hand[randomIndex];

                    if (owner.ExtractCard(ZoneType.Hand, cardToDiscard))
                    {
                        owner.InsertCard(ZoneType.Graveyard, cardToDiscard);
                        EventManager.OnCardMove?.Invoke(cardToDiscard, owner, ZoneType.Hand, owner, ZoneType.Graveyard);
                        EventManager.OnLogMessage?.Invoke($"🎲 [무작위 폐기] '{cardToDiscard.Name}' 카드가 무덤으로 보내졌습니다!");
                    }
                }
            }
            // ─── [모드 3] 직접 골라서 버리기 ("choose") ───
            else if (_mode == "choose")
            {
                // [참고] 이 부분은 나중에 UI를 띄워서 유저가 클릭할 때까지 기다려야 하므로 코루틴/콜백이 필요합니다.
                // 지금은 임시로 '랜덤 버리기'와 똑같이 작동하게 해두겠습니다.
                EventManager.OnLogMessage?.Invoke($"🎲 [선택 폐기] (UI 미구현으로 임시 랜덤 처리) {_count}장의 카드를 버립니다!");

                int actualDiscardCount = Mathf.Min(_count, owner.Hand.Count);
                for (int i = 0; i < actualDiscardCount; i++)
                {
                    int randomIndex = UnityEngine.Random.Range(0, owner.Hand.Count);
                    Card cardToDiscard = owner.Hand[randomIndex];
                    if (owner.ExtractCard(ZoneType.Hand, cardToDiscard))
                    {
                        owner.InsertCard(ZoneType.Graveyard, cardToDiscard);
                        EventManager.OnCardMove?.Invoke(cardToDiscard, owner, ZoneType.Hand, owner, ZoneType.Graveyard);
                    }
                }
            }

            // ⭐ 처리가 모두 끝났음을 시스템에 알림 (필수!)
            onComplete?.Invoke();
        }
    }
}