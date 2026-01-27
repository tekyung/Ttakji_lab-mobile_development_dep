using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Text.Json;

// 게임의 현재 상태를 효과들에게 전달하는 매개체
public class GameContext
{
    public Player? Player { get; set; }
    public Player? Opponent { get; set; }
    // 게임 매니저 등 필요한 정보 추가
}

// 카드 데이터 구조
public class Card
{
    public string? id { get; set; }
    public string? name { get; set; }
    public int cost { get; set; }
    public string? description { get; set; }
    public List<CardEffectData>? effects { get; set; }
}

public class CardEffectData
{
    public string? type { get; set; }
    public Dictionary<string, object>? @params { get; set; }
}

// 조건 데이터 구조
public class ConditionData
{
    public string? type { get; set; }
    public Dictionary<string, object>? @params { get; set; }
}

// CardEffectFactory: 카드 효과 타입 문자열을 실제 ICardEffect 인스턴스로 변환
public static class CardEffectFactory
{
    public static ICardEffect CreateEffect(string? type, Dictionary<string, object>? parameters)
    {
        if (type == null || parameters == null)
            throw new Exception("Effect type 또는 parameters가 null입니다.");
        switch (type)
        {
            case "Damage":
                var dmg = new DamageEffect();
                dmg.Initialize(parameters);
                return dmg;
            case "DrawCard":
                var draw = new DrawCardEffect();
                draw.Initialize(parameters);
                return draw;
            case "Heal":
                var heal = new HealEffect();
                heal.Initialize(parameters);
                return heal;
            case "Conditional":
                var cond = new ConditionalEffect();
                cond.Initialize(parameters);
                return cond;
            default:
                throw new Exception($"Unknown effect type: {type}");
        }
    }
}

// 조건 인터페이스 및 구현
public interface ICondition
{
    void Initialize(Dictionary<string, object> parameters);
    bool Evaluate(GameContext context);
}

public class CompareHealthCondition : ICondition
{
    private string? op; // "LowerThan" 등
    private string? target; // "Opponent" 등
    public void Initialize(Dictionary<string, object> parameters)
    {
        op = parameters.ContainsKey("operator") ? parameters["operator"]?.ToString() : null;
        target = parameters.ContainsKey("target") ? parameters["target"]?.ToString() : null;
    }
    public bool Evaluate(GameContext context)
    {
        if (context.Player == null || context.Opponent == null || op == null)
            return false;
        int myHealth = context.Player.Health;
        int otherHealth = context.Opponent.Health;
        if (op == "LowerThan")
            return myHealth < otherHealth;
        // 필요시 추가
        return false;
    }
}

public static class ConditionFactory
{
    public static ICondition CreateCondition(string? type, Dictionary<string, object>? parameters)
    {
        if (type == null || parameters == null)
            throw new Exception("Condition type 또는 parameters가 null입니다.");
        switch (type)
        {
            case "CompareHealth":
                var cond = new CompareHealthCondition();
                cond.Initialize(parameters);
                return cond;
            default:
                throw new Exception($"Unknown condition type: {type}");
        }
    }
}

// 조건부 효과
public class ConditionalEffect : ICardEffect
{
    private ICondition? condition;
    private ICardEffect? successEffect;
    public void Initialize(Dictionary<string, object> parameters)
    {
        if (!parameters.ContainsKey("condition") || !parameters.ContainsKey("successEffect"))
            throw new Exception("ConditionalEffect 파라미터 오류");
        var condData = parameters["condition"] as Dictionary<string, object>;
        var condType = condData != null && condData.ContainsKey("type") ? condData["type"]?.ToString() : null;
        var condParams = condData != null && condData.ContainsKey("params") ? condData["params"] as Dictionary<string, object> : null;
        condition = ConditionFactory.CreateCondition(condType, condParams);

        var effectData = parameters["successEffect"] as Dictionary<string, object>;
        var effectType = effectData != null && effectData.ContainsKey("type") ? effectData["type"]?.ToString() : null;
        var effectParams = effectData != null && effectData.ContainsKey("params") ? effectData["params"] as Dictionary<string, object> : null;
        successEffect = CardEffectFactory.CreateEffect(effectType, effectParams);
    }
    public void Execute(GameContext context)
    {
        if (condition != null && successEffect != null && condition.Evaluate(context))
        {
            successEffect.Execute(context);
        }
    }
}

public class Player
{
    public string? Name { get; set; }
    public int Health { get; set; } = 30;
    public void Draw() => Console.WriteLine($"{Name}가 카드를 드로우합니다.");
    public void TakeDamage(int amount)
    {
        Health -= amount;
        Console.WriteLine($"{Name}가 {amount}의 피해를 입었습니다. (남은 체력: {Health})");
    }
    public void Heal(int amount)
    {
        Health += amount;
        Console.WriteLine($"{Name}가 {amount}의 체력을 회복합니다. (현재 체력: {Health})");
    }
}

// 모든 효과는 이 인터페이스를 따릅니다.
public interface ICardEffect
{
    // JSON에서 읽어온 파라미터를 설정하는 함수
    void Initialize(Dictionary<string, object> parameters);

    // 실제 효과를 실행하는 함수
    void Execute(GameContext context);
}

// 1. 데미지 주는 효과
public class DamageEffect : ICardEffect
{
    private int amount;
    private string? targetType; // "Self" or "Opponent"

    public void Initialize(Dictionary<string, object> parameters)
    {
        // JSON 파싱 (안전한 타입 변환 로직 필요, 여기선 간략화)
        amount = Convert.ToInt32(parameters["amount"]);
        targetType = parameters.ContainsKey("target") ? parameters["target"]?.ToString() : null;
    }

    public void Execute(GameContext context)
    {
        Player? target = (targetType == "Opponent") ? context.Opponent : context.Player;
        if (target != null)
            target.TakeDamage(amount);
    }
}

// 2. 카드를 드로우하는 효과
public class DrawCardEffect : ICardEffect
{
    private int count;

    public void Initialize(Dictionary<string, object> parameters)
    {
        count = Convert.ToInt32(parameters["count"]);
    }

    public void Execute(GameContext context)
    {
        for (int i = 0; i < count; i++)
        {
            context.Player.Draw();
        }
    }
}

// 3. 힐 효과 (추가 예시)
public class HealEffect : ICardEffect
{
    private int amount;

    public void Initialize(Dictionary<string, object> parameters)
    {
        amount = Convert.ToInt32(parameters["amount"]);
    }

    public void Execute(GameContext context)
    {
        context.Player.Heal(amount);
    }
}

public class TCG
{
    // TCG 관련 기능들 구현
    int playerHealth = 30;
    int playerMana = 0;
    Player player1 = new Player();
    Player player2 = new Player();
    Card card = new Card();
    List<Card> playerDeck = new List<Card>();
    List<Card> playerHand = new List<Card>();
    public void InitializeGame()
    {
        // 게임 초기화 로직
    }
}