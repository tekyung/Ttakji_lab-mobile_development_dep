using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Utils
{
    public static class QaInjection
    {
        public static readonly (string CardId, ZoneType Zone)[] DefaultScenario =
        {
            ("SONI-07", ZoneType.StackZone),
            ("SONI-06", ZoneType.StackZone),
            ("DAIN-02", ZoneType.Hand)
        };

        public static void Inject(GameDataManager dataManager, Player player, string cardId, ZoneType targetZone)
        {
            if (dataManager == null || player == null)
                return;

            if (!dataManager.AllCards.TryGetValue(cardId, out Card template) || template == null)
            {
                EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] 주입 실패: ID '{cardId}'를 찾을 수 없습니다.</color>");
                return;
            }

            Card injectedCard = template.Clone();
            switch (targetZone)
            {
                case ZoneType.Hand:
                    player.InsertCard(ZoneType.Hand, injectedCard);
                    break;
                case ZoneType.Deck:
                    player.Deck.Insert(0, injectedCard);
                    break;
                case ZoneType.Graveyard:
                    player.InsertCard(ZoneType.Graveyard, injectedCard);
                    break;
                case ZoneType.ResourceZone:
                    player.InsertCard(ZoneType.ResourceZone, injectedCard);
                    break;
                case ZoneType.StackZone:
                    player.AddToStackZone(injectedCard);
                    break;
                case ZoneType.BattlefieldZone:
                    player.PlaceBattlefield(injectedCard);
                    break;
                default:
                    EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] '{targetZone}'은(는) 주입이 지원되지 않는 존입니다.</color>");
                    return;
            }

            EventManager.OnLogMessage?.Invoke($"<color=yellow>[QA Inject] {player.Name}의 {targetZone}에 '{injectedCard.Name}' 강제 장전 완료.</color>");
        }

        public static void ApplyDefaultScenario(GameDataManager dataManager, Player p1, Player p2)
        {
            if (p2 != null)
            {
                Inject(dataManager, p2, "SONI-07", ZoneType.StackZone);
                Inject(dataManager, p2, "SONI-06", ZoneType.StackZone);
            }

            if (p1 != null)
                Inject(dataManager, p1, "DAIN-02", ZoneType.Hand);
        }
    }
}
