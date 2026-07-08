using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 이 카드 자체를 특정 존에 배치하는 효과.
    /// 기존 SelfAsResourceEffect 를 통합한다.
    ///
    /// GameDataManager가 Initialize()에 아래 파라미터를 주입한다:
    ///   to : ZoneType 이름 — 현재는 "ResourceZone" 만 지원 (ELLI-08 보급 전달 등)
    ///
    /// Execute() 후 ConsoleRunner는 player.ResourceZone에 카드가 있음을 감지하여
    /// 폐기존 이동을 생략한다.
    /// </summary>
    public class SelfPlaceEffect : ICardEffect
    {
        private ZoneType _to = ZoneType.ResourceZone;
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();

            if (parameters.ContainsKey("to") &&
                Enum.TryParse<ZoneType>(parameters["to"].ToString(), out var to))
                _to = to;

            if (parameters.ContainsKey("requirePreviousSuccess"))
            {
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);
            }
            
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            Player owner = context.ActivePlayer;
            Card self    = owner?.PlayingCard;

            if (owner == null || self == null) { onComplete?.Invoke(); return; }

            if (_to == ZoneType.ResourceZone)
            {
                self.Type = CardType.Resource;
                owner.ResourceZone.Add(self);
                EventManager.OnLogMessage?.Invoke(
                    $"  [자원 배치] {owner.Name}: '{self.Name}'을 자원카드로 자원존에 배치 " +
                    $"(자원존: {owner.ResourceZone.Count}개)");
            }

            onComplete?.Invoke();
        }

        public ICardEffect Clone()
        {
            var clone = new SelfPlaceEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
