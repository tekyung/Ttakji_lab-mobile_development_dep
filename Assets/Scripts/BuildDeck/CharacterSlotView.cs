// CharacterSlotView.cs — 용병 슬롯 1칸의 "겉모습" 담당.
//
// 왜 있는가:
//   예전에는 DeckBuilderManager가 슬롯을 코드로 만들었다. 씬을 눈으로 볼 수 없는 상황에서
//   화면을 굴리기 위한 임시방편이었는데, 그러면 **기획자가 배치를 직접 못 만진다.**
//
//   이제는 씬에 슬롯을 직접 만들어 두고 이 컴포넌트만 붙이면 된다.
//   DeckBuilderManager는 "어디에 무엇을 그릴지"만 이 컴포넌트에 묻는다.
//   (인스펙터에 연결하지 않으면 예전처럼 코드가 만들어 주므로 기존 씬도 그대로 돈다)
//
// 씬에서 쓰는 법:
//   1. 슬롯으로 쓸 UI 오브젝트(Image + Button)를 만든다
//   2. 이 컴포넌트를 붙인다 — 같은 오브젝트/자식에서 Button·Image·Text를 자동으로 찾아 채운다
//   3. 초상 Image와 이름 Text는 필요하면 인스펙터에서 직접 지정한다
//   4. DeckBuilderManager의 characterSlots 배열에 순서대로(1번, 2번) 넣는다
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CharacterSlotView : MonoBehaviour
{
    [Tooltip("클릭을 받는 버튼. 비우면 이 오브젝트에서 찾는다.")]
    public Button button;

    [Tooltip("슬롯 배경. 선택 여부에 따라 색이 바뀐다. 비우면 이 오브젝트에서 찾는다.")]
    public Image background;

    [Tooltip("용병 초상. 용병이 없으면 자동으로 숨긴다. 없어도 동작한다.")]
    public Image portrait;

    [Tooltip("용병 이름. 없어도 동작한다.")]
    public TextMeshProUGUI label;

    /// <summary>인스펙터에서 비워 둔 것을 자동으로 채운다. 여러 번 불러도 안전하다.</summary>
    public void ResolveMissingReferences()
    {
        if (button == null) button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();

        // 초상과 이름은 자식에 있는 경우가 많다. 배경과 같은 Image를 초상으로 오인하지 않도록 구분한다.
        if (portrait == null)
        {
            foreach (Image image in GetComponentsInChildren<Image>(true))
            {
                if (image == background) continue;

                portrait = image;
                break;
            }
        }

        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Reset() => ResolveMissingReferences();
}
