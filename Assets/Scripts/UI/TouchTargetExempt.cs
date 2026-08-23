// TouchTargetExempt.cs — 이 표식이 붙은 UI는 TouchTargetNormalizer가 건드리지 않는다.
//
// 왜 필요한가:
//   자동 보정은 "최소 4:3 비율"을 강제한다. 대부분의 버튼에는 맞는 규칙이지만,
//   원본 그림의 비율을 지켜야 하는 것(예: 용병 초상 슬롯)에는 해가 된다.
//   세로로 긴 그림이 억지로 가로로 늘어나 버린다.
//
//   그래서 "내 크기는 내가 정한다"고 선언할 수단을 둔다.
//   코드로 만든 UI는 생성할 때 이 컴포넌트를 함께 붙이면 된다.
using UnityEngine;

[DisallowMultipleComponent]
public class TouchTargetExempt : MonoBehaviour
{
    /// <summary>왜 예외인지 남겨 둔다. 나중에 보는 사람이 의도를 알 수 있도록.</summary>
    [TextArea]
    public string reason = "원본 비율을 유지해야 하는 UI";
}
