using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 전장 효과 통합 클래스.
    /// 기존 BattlefieldEffect / ArmorBattlefieldEffect / FirepowerBattlefieldEffect /
    /// PeriodicRecoveryBattlefieldEffect / CostReductionBattlefieldEffect 를 통합한다.
    ///
    /// Execute()에서 카드를 전장존에 배치한다 (파괴 전까지 지속).
    /// 아래 속성은 GameDataManager가 직접 설정하며, ConsoleRunner가 매 페이즈에 호출한다:
    ///   PerTurnEffect          — 매 드로우 페이즈마다 적용되는 효과 (null 가능)
    ///   PerResourcePhaseEffect — 매 자원 페이즈마다 적용되는 효과 (null 가능)
    ///   CostReductionFilter    — 코스트 감소 대상 필터 (null 가능)
    ///   CostReduction          — 코스트 감소량
    /// </summary>
    public class BattlefieldEffect : ICardEffect
    {
        /// <summary>매 드로우 페이즈마다 발동되는 효과 (VERO-11, DAIN-11, SONI-11)</summary>
        public ICardEffect PerTurnEffect          { get; set; }

        /// <summary>매 자원 페이즈마다 발동되는 효과 (ELLI-11)</summary>
        public ICardEffect PerResourcePhaseEffect { get; set; }

        /// <summary>코스트 감소 대상 필터 (SONI-11 관제탑)</summary>
        public string CostReductionFilter         { get; set; }

        /// <summary>코스트 감소량</summary>
        public int    CostReduction               { get; set; }
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack 속성을 따르지만, JSON에서 오버라이드 가능)

        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();
                
            if (parameters.ContainsKey("requirePreviousSuccess"))
            {
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);
            }
            
            if (parameters.ContainsKey("isStackAction"))
                IsStackAction = Convert.ToBoolean(parameters["isStackAction"]);
            else
                IsStackAction = false; // JSON에 없으면 기본적으로 즉발(Open) 효과로 취급
        }

        public void Execute(GameContext context, Action onComplete)
        {   
            Player owner = context.ActivePlayer;
            Card self    = owner?.PlayingCard;

            if (owner == null || self == null)
            {
                EventManager.OnLogMessage?.Invoke(
                    "[BattlefieldEffect] PlayingCard가 설정되지 않아 전장 배치 불가.");
                onComplete?.Invoke();
                return;
            }

            // owner.PlaceBattlefield(self);
            EventManager.OnLogMessage?.Invoke(
                $"  [전장 배치] {owner.Name}: '{self.Name}' 전장존 — {BuildDetailText()}");
            
            onComplete?.Invoke();
        }

        private string BuildDetailText()
        {
            var parts = new List<string>();

            if (PerTurnEffect is BuffEffect bf)
            {
                parts.Add("매 턴 버프 지속");
            }
            else if (PerTurnEffect != null)
                parts.Add("매 턴 효과 지속");

            if (PerResourcePhaseEffect != null)
                parts.Add("매 자원페이즈 효과");

            if (!string.IsNullOrEmpty(CostReductionFilter))
                parts.Add($"조건({CostReductionFilter}) 카드 코스트 -{CostReduction}");

            return parts.Count > 0 ? string.Join(", ", parts) : "파괴 전까지 지속";
        }

        public ICardEffect Clone()
        {
            return new BattlefieldEffect
            {
                PerTurnEffect          = PerTurnEffect?.Clone(),
                PerResourcePhaseEffect = PerResourcePhaseEffect?.Clone(),
                CostReductionFilter    = CostReductionFilter,
                CostReduction          = CostReduction,
                _cachedParams          = _cachedParams
            };
        }
    }
}
