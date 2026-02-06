using System;

namespace TCG_Project.Scripts.Core
{
    public static class DebugHelper
    {
        public static void LogSpell(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan; // 스펠은 하늘색
            Console.WriteLine($"   🔮 [Spell Debug] {message}");
            Console.ResetColor();
        }

        public static void LogEffect(string effectType, string detail)
        {
            Console.ForegroundColor = ConsoleColor.Yellow; // 효과 상세는 노란색
            Console.WriteLine($"      ⚡ [{effectType}] {detail}");
            Console.ResetColor();
        }

        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"      ⚠️ [Warning] {message}");
            Console.ResetColor();
        }
    }
}