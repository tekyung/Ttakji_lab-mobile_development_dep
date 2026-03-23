// Scripts/Effects/OptionalActionEffect.cs 신규 생성
using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    /// <summary>
    /// 룰북 기믹: "A를 할 수 있다. 했다면 B를 한다."를 처리하는 이펙트.
    /// 플레이어에게 의사를 묻고, 수락하면 ActionToPerform을 실행합니다.
    /// 거절하면 GameContext.LastEffectSucceeded를 false로 만들어 후속 "그 후" 효과들을 취소시킵니다.
    /// </summary>
    public class OptionalActionEffect : ICardEffect
    {
        public string Description { get; private set; }
        public ICardEffect ActionToPerform { get; private set; } // Yes 시 수행할 실제 효과 (예: 덱 4장 폐기)
        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        public void Initialize(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("description", out var descObj))
                Description = descObj.ToString();

            // JSON 파싱 시 내부 효과 생성 (GameDataManager의 팩토리 로직 연계 필요)
            // if (parameters.TryGetValue("action", out var actionDict))
            // {
            //     ActionToPerform = GameDataManager.CreateEffect((Dictionary<string,object>)actionDict);
            // }

            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
            }
        }

        public void Execute(GameContext context, Action onComplete)
        {
            Player me = context.ActivePlayer;

            // 플레이어의 선택을 처리하는 로컬 콜백 함수
            Action<bool> handleChoice = (choice) =>
            {
                if (choice)
                {
                    EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 선택: '{Description}' (수행함)");

                    if (ActionToPerform != null)
                    {
                        // 선택 행동을 실제로 수행 (이 내부에서 실패하면 LastEffectSucceeded가 false가 됨)
                        ActionToPerform.Execute(context, () =>
                        {
                            onComplete?.Invoke();
                        });
                    }
                    else
                    {
                        context.LastEffectSucceeded = true;
                        onComplete?.Invoke();
                    }
                }
                else
                {
                    // 행동을 포기했으므로, "그 후" 따라오는 후속 효과는 불발되어야 함
                    EventManager.OnLogMessage?.Invoke($"  ▶ [{me.Name}] 선택: '{Description}' (취소함)");
                    context.LastEffectSucceeded = false;
                    onComplete?.Invoke();
                }
            };

            // 행동 주체가 봇인지 인간인지 분기
            if (me.Type == UserType.Bot)
            {
                // TODO: 향후 BotBrain.ChooseOptionalAction() 으로 위임하여 전략화 가능
                // 현재는 봇이 가능한 옵션은 항상 수락한다고 가정
                handleChoice(true);
            }
            else
            {
                // 인간일 경우 UI의 선택 응답을 대기 (콜백을 통해 재개)
                EventManager.OnRequireOptionalAction?.Invoke(me, Description, context, handleChoice);
            }
        }

        public ICardEffect Clone()
        {
            return new OptionalActionEffect
            {
                Description = this.Description,
                ActionToPerform = this.ActionToPerform?.Clone()
            };
        }
    }
}