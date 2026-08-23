// HumanChoiceDialogView.cs — 선택 다이얼로그 프리팹의 "겉모습" 참조 모음.
//
// 왜 있는가:
//   예전에는 HumanChoiceDialogUI가 패널·스크롤·버튼 계층을 전부 코드로 지었다.
//   씬을 볼 수 없는 상황에서 화면을 굴리기 위한 임시방편이었는데,
//   그러면 **기획자가 배치를 직접 못 만진다.**
//
//   이제 배치는 프리팹(Resources/Build/HumanChoiceDialog)이 소유하고,
//   코드는 "무엇을 보여 줄지"만 정한다.
//
//     프리팹 소유 : 위치·크기·앵커·계층·폰트·기본 색
//     코드   소유 : 텍스트 내용·활성 여부·상태 색·목록 개수·애니메이션
//
// ⚠️ 이 UI는 임계 경로다. 엔진이 WaitUntil로 응답을 기다리므로,
//    참조가 하나라도 비면 화면이 안 뜨고 **게임이 그대로 멈춘다.**
//    그래서 Validate()가 무엇이 비었는지 이름으로 짚어 준다. 조용히 실패하면 안 된다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HumanChoiceDialogView : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("슬라이드·표시 대상. 이 RectTransform의 프리팹 상 위치가 '보이는 위치'가 된다.")]
    public RectTransform panel;

    [Header("카드 목록")]
    [Tooltip("가로 스크롤. 카드 항목은 다음 라운드에서 프리팹으로 옮긴다.")]
    public ScrollRect scroll;

    [Tooltip("카드 항목이 들어갈 부모(ScrollRect의 Content).")]
    public RectTransform content;

    [Header("표시 요소")]
    [Tooltip("예/아니오 요청에서 물어본 용병 그림. 평소에는 꺼 둔다.")]
    public Image askerImage;

    [Tooltip("안내 문구.")]
    public TextMeshProUGUI messageText;

    [Header("버튼")]
    public Button confirmButton;

    [Tooltip("확정 버튼의 글자. '확정 (1/2)'처럼 진행 상황을 보여 준다.")]
    public TextMeshProUGUI confirmLabel;

    [Tooltip("예/아니오 요청에서만 켠다.")]
    public Button declineButton;

    [Tooltip("패널을 내리는 ▼ 버튼.")]
    public Button collapseButton;

    [Tooltip("접힌 상태에서 화면 구석에 남는 ▲ 버튼.")]
    public Button expandButton;

    /// <summary>
    /// 인스펙터에서 비워 둔 것을 이름으로 찾아 채운다. 여러 번 불러도 안전하다.
    /// 프리팹을 캡처해 만들었다면 이름이 그대로라 대부분 여기서 해결된다.
    /// </summary>
    public void ResolveMissingReferences()
    {
        if (panel == null) panel = FindByName<RectTransform>("CardChoicePanel");
        if (scroll == null) scroll = GetComponentInChildren<ScrollRect>(true);
        if (content == null && scroll != null) content = scroll.content;

        if (askerImage == null) askerImage = FindByName<Image>("AskingCharacter");
        if (messageText == null) messageText = FindMessageText();

        if (confirmButton == null) confirmButton = FindByName<Button>("ConfirmButton");
        if (declineButton == null) declineButton = FindByName<Button>("DeclineButton");
        if (collapseButton == null) collapseButton = FindByName<Button>("CollapseButton");
        if (expandButton == null) expandButton = FindByName<Button>("ExpandButton");

        if (confirmLabel == null && confirmButton != null)
            confirmLabel = confirmButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>
    /// 빠진 참조를 이름으로 알린다. 하나라도 없으면 false.
    /// </summary>
    /// <param name="reason">무엇이 없는지 사람이 읽을 수 있는 설명.</param>
    public bool Validate(out string reason)
    {
        ResolveMissingReferences();

        var missing = new System.Collections.Generic.List<string>();

        if (panel == null) missing.Add(nameof(panel));
        if (scroll == null) missing.Add(nameof(scroll));
        if (content == null) missing.Add(nameof(content));
        if (messageText == null) missing.Add(nameof(messageText));
        if (confirmButton == null) missing.Add(nameof(confirmButton));
        if (collapseButton == null) missing.Add(nameof(collapseButton));
        if (expandButton == null) missing.Add(nameof(expandButton));

        // askerImage·declineButton·confirmLabel은 없어도 진행은 된다(기능이 일부 빠질 뿐).
        if (askerImage == null) Debug.LogWarning($"[{name}] askerImage가 없어 예/아니오에서 용병 그림이 표시되지 않는다.");
        if (declineButton == null) Debug.LogWarning($"[{name}] declineButton이 없어 [아니오]를 누를 수 없다.");
        if (confirmLabel == null) Debug.LogWarning($"[{name}] confirmLabel이 없어 '확정 (1/2)' 진행 표시가 나오지 않는다.");

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }

        reason = $"연결되지 않은 항목: {string.Join(", ", missing)}";
        return false;
    }

    /// <summary>안내 문구를 찾는다 — 버튼 안의 글자는 제외한다.</summary>
    private TextMeshProUGUI FindMessageText()
    {
        foreach (TextMeshProUGUI text in GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.GetComponentInParent<Button>() != null) continue;

            return text;
        }

        return null;
    }

    private T FindByName<T>(string targetName) where T : Component
    {
        foreach (T candidate in GetComponentsInChildren<T>(true))
        {
            if (candidate.name == targetName) return candidate;
        }

        return null;
    }

    private void Reset() => ResolveMissingReferences();
}
