// PointerInput.cs — "이번 프레임에 새로 눌린 곳"을 알려 준다.
//
// 왜 있는가:
//   포인터 이벤트(IPointerClickHandler 등)로 잡히지 않는 판정이 이 프로젝트에 여럿 있다.
//   보드는 카드마다 중첩 Canvas가 붙고 존마다 레이캐스트를 껐다 켜서, 이벤트가 목적지까지
//   오지 않는 경우가 있기 때문이다. 그럴 때는 좌표를 직접 보고 Rect와 견주는 수밖에 없다.
//
//   그 "좌표를 얻는 일"만 여기로 모았다. 원래 ZoneCountHoverUI 안에 있던 것을 꺼낸 것으로,
//   덱 편집 화면의 용병 선택 창(바깥을 누르면 닫힘)도 같은 판정을 쓴다.
//
// ★ 터치를 먼저 보고, 터치가 하나도 없을 때만 마우스를 본다.
//   안드로이드는 터치를 마우스 클릭으로도 흘려보내므로, 그대로 두면 같은 탭을 두 번 세게 된다.
using UnityEngine;

public static class PointerInput
{
    /// <summary>
    /// 이번 프레임에 새로 눌린 곳이 있으면 그 화면 좌표를 돌려준다.
    /// 누르고 있는 중이나 떼는 순간은 잡지 않는다 — <b>누르기 시작한 그 프레임</b>만이다.
    /// </summary>
    public static bool TryGetPressPoint(out Vector2 point)
    {
        int touches = Input.touchCount;
        for (int i = 0; i < touches; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began) continue;

            point = touch.position;
            return true;
        }

        // 터치가 하나도 없을 때만 마우스를 본다.
        // (안드로이드에서는 터치가 마우스 클릭으로도 들어와, 그대로 두면 같은 탭을 두 번 세게 된다)
        if (touches == 0 && Input.GetMouseButtonDown(0))
        {
            point = Input.mousePosition;
            return true;
        }

        point = default;
        return false;
    }
}
