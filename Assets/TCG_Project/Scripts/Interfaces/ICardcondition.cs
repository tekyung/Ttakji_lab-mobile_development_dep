using System.Collections.Generic;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Interfaces // 이 부분이 필수입니다!
{
    // 카드 효과의 공통 인터페이스_조건 판별
    public interface ICardCondition
    {
        void Initialize(Dictionary<string, object> parameters);
        bool IsMet(GameContext context); // 조건이 충족되었는지 True/False 반환
    }
}
