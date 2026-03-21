using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 데미지 효과 통합 클래스.
    /// 기존 DamageEffect / PiercingDamageEffect / MultiHitDamageEffect / SelfDamageEffect 를 통합한다.
    ///
    /// GameDataManager가 Initialize()에 아래 파라미터를 주입한다:
    ///   amount     : int  — 1회 데미지량
    ///   isPiercing : bool — true = 관통 (아머 무시, 슈퍼아머만 반응)
    ///   Times      : int  — 반복 횟수 (기본값 1)
    ///   targetSelf : bool — true = 자해 (방어 효과 무시하고 ActivePlayer.LoseLife 직접 호출)
    ///   requirePreviousSuccess : bool — true = 이전 효과가 성공해야만 이 효과가 실행 ("그 후," 시맨틱)
    /// </summary>
    public class DamageEffect : ICardEffect
    {
        private int _amount = 1;
        public bool isPiercing { get; private set; } = false;
        public int Times { get; private set; } = 1;
        public bool TargetSelf = false;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)

        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();

            if (parameters.ContainsKey("amount"))
                _amount = Convert.ToInt32(parameters["amount"]);

            if (parameters.ContainsKey("isPiercing"))
                isPiercing = Convert.ToBoolean(parameters["isPiercing"]); // _isPiercing 대신 프로퍼티에 할당

            if (parameters.ContainsKey("times"))
                Times = Math.Max(1, Convert.ToInt32(parameters["times"]));

            if (parameters.ContainsKey("targetSelf"))
                TargetSelf = Convert.ToBoolean(parameters["targetSelf"]);

            if (parameters.ContainsKey("requirePreviousSuccess"))
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);
                
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            Player attacker = context.ActivePlayer;

            if (TargetSelf)
            {
                if (attacker == null) { onComplete?.Invoke(); return; }
                EventManager.OnLogMessage?.Invoke(
                    $"  [자해] {attacker.Name} 자신에게 라이프 -{_amount}");
                attacker.LoseLife(_amount, context);
                onComplete?.Invoke();
                return;
            }

            Player defender = context.TargetPlayer;
            if (attacker == null || defender == null)
            {
                EventManager.OnLogMessage?.Invoke("[DamageEffect] 공격자 또는 방어자가 없음. 효과 취소.");
                onComplete?.Invoke();
                return;
            }

            string typeLabel = isPiercing ? "관통 데미지" : "기본 데미지";

            if (Times > 1)
                EventManager.OnLogMessage?.Invoke(
                    $"  [{typeLabel}] {attacker.Name} → {defender.Name} ({_amount} × {Times}회)");
            else
                EventManager.OnLogMessage?.Invoke(
                    $"  [{typeLabel}] {attacker.Name} → {defender.Name} ({_amount})");

            for (int i = 0; i < Times; i++)
            {
                if (Times > 1)
                    EventManager.OnLogMessage?.Invoke($"  [타격 {i + 1}/{Times}]");
                DamageResolver.ResolveDamage(attacker, defender, _amount, isPiercing, context);
            }

            onComplete?.Invoke();
        }

        public ICardEffect Clone()
        {
            var clone = new DamageEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
