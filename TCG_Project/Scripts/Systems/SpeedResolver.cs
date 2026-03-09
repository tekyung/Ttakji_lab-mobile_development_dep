using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// 메인 페이즈 카드 해결 순서를 룰북 스피드 규칙에 따라 정렬한다.
    ///
    /// 처리 순서:
    ///   Speed 1 방어 → Speed 1 공격 → Speed 1 지원
    ///   Speed 2 방어 → Speed 2 공격 → Speed 2 지원
    ///   Speed 3 방어 → Speed 3 공격 → Speed 3 지원
    ///   Speed 없음(None) 카드는 맨 뒤 처리
    ///
    /// 완전히 동일한 Speed + Type인 카드는 "동시 처리" 그룹으로 묶인다.
    /// </summary>
    public static class SpeedResolver
    {
        /// <summary>
        /// 공개된 카드 목록을 룰북 스피드 규칙에 따라 처리 그룹으로 묶어 반환한다.
        /// 각 그룹은 동시 처리 단위이며, 그룹 내에서는 방어 효과가 먼저 적용된다.
        /// </summary>
        /// <param name="revealedCards">공개된 (플레이어, 상대, 카드) 쌍의 목록</param>
        /// <returns>처리 순서대로 정렬된 그룹 리스트. 그룹 하나 = 동시 처리 단위.</returns>
        public static List<List<(Player player, Player enemy, Card card)>> GroupByResolutionOrder(
            List<(Player player, Player enemy, Card card)> revealedCards)
        {
            var result = new List<List<(Player player, Player enemy, Card card)>>();

            // Speed/Type 우선순위 키 부여
            var sorted = revealedCards
                .OrderBy(e => GetSpeedPriority(e.card.Speed))
                .ThenBy(e => GetTypePriority(e.card.Type))
                .ToList();

            int i = 0;
            while (i < sorted.Count)
            {
                var current = sorted[i];
                int speedKey = GetSpeedPriority(current.card.Speed);
                int typeKey  = GetTypePriority(current.card.Type);

                // 동일한 Speed + Type인 카드를 한 그룹으로 묶음
                var group = sorted
                    .Skip(i)
                    .TakeWhile(e =>
                        GetSpeedPriority(e.card.Speed) == speedKey &&
                        GetTypePriority(e.card.Type)   == typeKey)
                    .ToList();

                // 그룹 내 방어 카드가 먼저 오도록 정렬
                group = group
                    .OrderBy(e => e.card.Type == CardType.Defense ? 0 : 1)
                    .ToList();

                result.Add(group);
                i += group.Count;
            }

            return result;
        }

        /// <summary>
        /// 그룹 처리 결과를 콘솔 로그로 출력한다.
        /// </summary>
        public static void LogResolutionOrder(
            List<List<(Player player, Player enemy, Card card)>> groups)
        {
            EventManager.OnLogMessage?.Invoke("[SpeedResolver] 처리 순서:");
            int step = 1;
            foreach (var group in groups)
            {
                if (group.Count == 1)
                {
                    var e = group[0];
                    EventManager.OnLogMessage?.Invoke(
                        $"  Step {step}: {e.player.Name} [{e.card.Name}] " +
                        $"(Speed:{e.card.Speed}, Type:{e.card.Type})");
                }
                else
                {
                    var names = string.Join(" / ", group.Select(e =>
                        $"{e.player.Name}[{e.card.Name}]"));
                    EventManager.OnLogMessage?.Invoke(
                        $"  Step {step}: 동시 처리 — {names} " +
                        $"(Speed:{group[0].card.Speed}, Type:{group[0].card.Type})");
                }
                step++;
            }
        }

        // Speed 수치 → 정렬 우선순위 (낮을수록 먼저)
        private static int GetSpeedPriority(CardSpeed speed) => speed switch
        {
            CardSpeed.Speed1 => 1,
            CardSpeed.Speed2 => 2,
            CardSpeed.Speed3 => 3,
            _                => 99 // CardSpeed.None — 맨 뒤
        };

        // CardType → 정렬 우선순위 (낮을수록 먼저)
        // 방어 > 공격 > 지원; 나머지는 맨 뒤
        private static int GetTypePriority(CardType type) => type switch
        {
            CardType.Defense => 0,
            CardType.Attack  => 1,
            CardType.Support => 2,
            _                => 9
        };
    }
}
