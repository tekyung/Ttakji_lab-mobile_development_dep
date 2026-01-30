namespace TCG_Project.Scripts.Core
{
    public enum TargetType { Player, Card }

    public class Target
    {
        public TargetType Type { get; private set; }
        public Player PlayerVal { get; private set; }
        public Card CardVal { get; private set; }

        public Target(Player player)
        {
            Type = TargetType.Player;
            PlayerVal = player;
        }

        public Target(Card card)
        {
            Type = TargetType.Card;
            CardVal = card;
        }

        public string Name => Type == TargetType.Player ? PlayerVal.Name : CardVal.Name;

        public override string ToString()
        {
            return Name;
        }
    }
}