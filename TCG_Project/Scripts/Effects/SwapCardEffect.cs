using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Effects
{
    // [미래의 확장성을 위한 청사진] 교환 전용 클래스
    public class SwapCardEffect : ICardEffect
    {
        private MoveCardEffect _moveAtoB;
        private MoveCardEffect _moveBtoA;
        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters;

            // 파라미터를 파싱해서 두 개의 Move 효과에 셋팅
            // 예: "내 패 1장을 적 필드 1장과 교환"

            var paramsAtoB = new Dictionary<string, object> {
                { "src", "Hand" }, { "dest", "Field" },
                { "srcTarget", "Self" }, { "destTarget", "Opponent" },
                { "count", 1 }
            };
            _moveAtoB = new MoveCardEffect();
            _moveAtoB.Initialize(paramsAtoB);

            var paramsBtoA = new Dictionary<string, object> {
                { "src", "Field" }, { "dest", "Hand" },
                { "srcTarget", "Opponent" }, { "destTarget", "Self" },
                { "count", 1 }
            };
            _moveBtoA = new MoveCardEffect();
            _moveBtoA.Initialize(paramsBtoA);
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 비동기 체이닝: A->B 이동이 끝나면 B->A 이동을 실행
            _moveAtoB.Execute(context, () =>
            {
                _moveBtoA.Execute(context, () =>
                {
                    // 두 번의 이동이 모두 끝나면 교환 완료!
                    onComplete?.Invoke();
                });
            });
        }

        public ICardEffect Clone()
        {
            var clone = new SwapCardEffect();
            clone.Initialize(this._cachedParams);
            return clone;
        }
    }
}