using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Interfaces // 이 부분이 필수입니다!
{
    // 모든 효과는 이 인터페이스를 따릅니다.
    public interface ICardEffect
    {
        // JSON에서 읽어온 파라미터를 설정하는 함수
        void Initialize(Dictionary<string, object> parameters);

        // 실제 효과를 실행하는 함수
        void Execute(GameContext context);
    }
}

