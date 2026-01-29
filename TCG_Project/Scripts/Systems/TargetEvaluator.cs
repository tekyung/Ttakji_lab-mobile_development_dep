using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class TargetEvaluator
    {
        // 타겟 문자열을 분석해 대상 플레이어 리스트를 반환
        public static List<Player> Evaluate(object targetParam, GameContext context)
        {
            var targets = new List<Player>();
            string key = targetParam.ToString().ToLower(); // 대소문자 무시

            switch (key)
            {
                case "self":
                case "activeplayer": // 스크립트 스타일 지원
                    targets.Add(context.ActivePlayer);
                    break;

                case "opponent":
                    targets.Add(context.GetOpponent(context.ActivePlayer));
                    break;

                case "all":
                case "both":
                    // GameContext에 Players 리스트가 있다고 가정
                    if (context.Players != null)
                        targets.AddRange(context.Players);
                    break;

                default:
                    Console.WriteLine($"[Warning] 알 수 없는 타겟: {key}");
                    break;
            }

            return targets;
        }
    }
}