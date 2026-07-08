using System;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Core
{
    public static class DebugHelper
    {
        // 유니티와 콘솔 양쪽에서 색상을 표현하기 위해 Rich Text 태그(<color=...>)를 사용합니다.
        
        public static void LogSpell(string message)
        {
            // Cyan 색상 태그 적용
            EventManager.OnLogMessage?.Invoke($"<color=cyan>   🔮 [Spell Debug] {message}</color>");
        }

        public static void LogEffect(string effectType, string detail)
        {
            // Yellow 색상 태그 적용
            EventManager.OnLogMessage?.Invoke($"<color=yellow>      ⚡ [{effectType}] {detail}</color>");
        }

        public static void LogWarning(string message)
        {
            // Red 색상 태그 적용
            EventManager.OnLogMessage?.Invoke($"<color=red>      ⚠️ [Warning] {message}</color>");
        }
    }
}
