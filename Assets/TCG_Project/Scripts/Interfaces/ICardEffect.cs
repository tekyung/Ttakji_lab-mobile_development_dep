using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using System;

namespace TCG_Project.Scripts.Interfaces // 이 부분이 필수!
{
    // 모든 효과는 이 인터페이스를 따릅니다.
    public interface ICardEffect
    {
        // ★ 신규: 스택존에서 상대 공격에 반응하여 터지는 효과인가?
        // (기본값은 카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        bool IsStackAction { get; set; }
        
        // JSON에서 읽어온 파라미터를 설정하는 함수
        void Initialize(Dictionary<string, object> parameters);

        // ★ 이전 효과가 성공해야만 이 효과가 실행되는가? ("그 후," 시맨틱)
        bool RequirePreviousSuccess { get; set; }

        // ★ 콜백 : 효과 처리가 전부 끝나면 onComplete()를 호출해 주어야 함
        void Execute(GameContext context, Action onComplete);

        // 자신과 완벽히 동일한 상태를 가진 새로운 인스턴스를 반환합니다.
        ICardEffect Clone();
    }
}

