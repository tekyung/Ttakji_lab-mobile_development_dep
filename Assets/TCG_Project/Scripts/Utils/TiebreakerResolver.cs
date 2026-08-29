using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{
    public class TiebreakerResolver
    {
        /// <summary>
        /// 동시 덱아웃 발생 시 룰북에 명시된 6단계 타이브레이커 판정을 수행합니다.
        /// 반환값: 양수(p1 승리), 음수(p2 승리), 0(무승부로 코인토스 필요)
        ///
        /// ★ 각 단계의 비교 결과를 로그로 남긴다.
        ///   결과만 보면 "왜 저쪽이 이겼는지" 알 수가 없어, 판정이 맞는지 확인할 방법이 없었다.
        /// </summary>
        public static int ResolveTiebreaker(Player p1, Player p2)
        {
            EventManager.OnLogMessage?.Invoke(
                $"<color=#ffd479>[타이브레이커] 동시 종료 — {p1.Name} vs {p2.Name} 단계별 비교를 시작합니다.</color>");

            // 1단계: 묘지 존 카드 비교 (적은 쪽 승)
            int step = Compare(1, "폐기존", "적은 쪽 승", p1, p2,
                p1.Graveyard.Count, p2.Graveyard.Count, lowerWins: true);
            if (step != 0) return step;

            // 2단계: 자원 존 카드 비교 (많은 쪽 승)
            step = Compare(2, "자원존", "많은 쪽 승", p1, p2,
                p1.ResourceZone.Count, p2.ResourceZone.Count, lowerWins: false);
            if (step != 0) return step;

            // 3단계: 덱 카드 비교 (많은 쪽 승)
            step = Compare(3, "메인덱", "많은 쪽 승", p1, p2,
                p1.Deck.Count, p2.Deck.Count, lowerWins: false);
            if (step != 0) return step;

            // 4단계: 자원 덱 카드 비교 (많은 쪽 승)
            step = Compare(4, "자원덱", "많은 쪽 승", p1, p2,
                p1.ResourceDeck.Count, p2.ResourceDeck.Count, lowerWins: false);
            if (step != 0) return step;

            // 5단계: 라이프 토큰 비교 (많은 쪽 승)
            step = Compare(5, "라이프", "많은 쪽 승", p1, p2,
                p1.LifeTokens, p2.LifeTokens, lowerWins: false);
            if (step != 0) return step;

            // 6단계: 여기까지 모두 같으면 0을 반환 (호출한 쪽이 코인 토스 진행)
            EventManager.OnLogMessage?.Invoke(
                "<color=#ffd479>[타이브레이커] 6단계까지 모두 동률 — 코인 토스로 넘어갑니다.</color>");
            return 0;
        }

        /// <summary>
        /// 한 단계를 비교하고 그 결과를 로그로 남긴다.
        /// 승부가 갈리면 부호(양수=p1 승)를 돌려주고, 동률이면 0을 돌려준다.
        /// </summary>
        private static int Compare(
            int stepNumber, string label, string rule,
            Player p1, Player p2, int v1, int v2, bool lowerWins)
        {
            if (v1 == v2)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"    [{stepNumber}단계] {label}({rule}): {p1.Name} {v1} vs {p2.Name} {v2} → 동률, 다음 단계로");
                return 0;
            }

            // lowerWins면 작은 쪽이 이긴다 — 비교 방향을 뒤집는다.
            int result = lowerWins ? v2.CompareTo(v1) : v1.CompareTo(v2);
            string winner = result > 0 ? p1.Name : p2.Name;

            EventManager.OnLogMessage?.Invoke(
                $"<color=#ffd479>    [{stepNumber}단계] {label}({rule}): {p1.Name} {v1} vs {p2.Name} {v2} → {winner} 승리로 판정</color>");

            return result;
        }
    }
}
