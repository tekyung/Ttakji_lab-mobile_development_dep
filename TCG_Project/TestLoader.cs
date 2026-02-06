using System;
using System.IO;
using System.Text;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Systems;

public class TestLoader
{
    public static void RunTest()
    {
        Console.WriteLine("=== 🛠️ 데이터 로더 테스트 시작 ===");

        // 1. 매니저 생성 및 데이터 로드
        GameDataManager dataManager = new GameDataManager();
        // 콘솔에서 UTF-8 인코딩 사용 설정
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        // [주의] 실제 json 파일들이 있는 경로를 적어주세요.
        // 유니티: Application.streamingAssetsPath + "/Data"
        // 콘솔: "./Data" 또는 절대 경로
        string dataPath = "./Data";

        if (!Directory.Exists(dataPath))
        {
            Console.WriteLine($"[Error] 데이터 폴더를 찾을 수 없습니다: {Path.GetFullPath(dataPath)}");
            return;
        }

        dataManager.LoadAllData(dataPath);

        // ---------------------------------------------------------
        // 2. 검증 A: 유닛 데이터 (슬라임 - ID 11001)
        // [기대결과] Type: Unit, Power: 300, AttackCost: 1, 효과 없음
        // ---------------------------------------------------------
        if (dataManager.AllCards.TryGetValue("11001", out Card slime))
        {
            Console.WriteLine($"\n✅ [유닛 로드 성공] {slime.Name} (ID: 11001)");
            Console.WriteLine($"   - Type: {slime.Type}"); // Unit
            Console.WriteLine($"   - Power(HP): {slime.MaxHealth}"); // 300
            Console.WriteLine($"   - Attack Cost: {slime.AttackCost}"); // 1
            Console.WriteLine($"   - Prize: {slime.Prize}"); // 1
            Console.WriteLine($"   - Effects: {slime.Effects.Count}개"); // 0개여야 함 (on_play -1이므로)
        }
        else Console.WriteLine("\n❌ [실패] 슬라임(11001)을 찾을 수 없습니다.");

        // ---------------------------------------------------------
        // 3. 검증 B: 유닛 효과 연결 (애벌레 - ID 11004)
        // [기대결과] 효과 1개 보유 (Effect ID 210015 -> Draw 1장)
        // ---------------------------------------------------------
        if (dataManager.AllCards.TryGetValue("11004", out Card worm))
        {
            Console.WriteLine($"\n✅ [유닛 효과 검증] {worm.Name} (ID: 11004)");
            if (worm.Effects.Count > 0)
            {
                Console.WriteLine($"   - Effect Loaded: OK ({worm.Effects.Count}개)");
                Console.WriteLine($"   - Effect Type: {worm.Effects[0].GetType().Name}"); // MoveCardEffect 예상
                // 실제 내부 파라미터 확인은 디버거로 보거나, 실행해봐야 알 수 있음
            }
            else Console.WriteLine("   ❌ [실패] 애벌레의 효과(Draw)가 누락되었습니다.");
        }

        // ---------------------------------------------------------
        // 4. 검증 C: 스킬 데이터 (파이어볼 - ID 21001)
        // [기대결과] Type: Skill, Cost: 1, Effect: KillUnit (ID 210011)
        // ---------------------------------------------------------
        if (dataManager.AllCards.TryGetValue("21001", out Card fireball))
        {
            Console.WriteLine($"\n✅ [스킬 로드 성공] {fireball.Name} (ID: 21001)");
            Console.WriteLine($"   - Type: {fireball.Type}"); // Skill
            Console.WriteLine($"   - Cost: {fireball.Cost}"); // 1

            if (fireball.Effects.Count > 0)
            {
                Console.WriteLine($"   - Effect Loaded: OK");
                // CardSkill.json -> after_use_effect_id: 210011
                // CardEffect.json -> 210011은 "KillUnit"
                // GameDataManager -> "KillUnit"은 MoveCardEffect로 변환됨
                Console.WriteLine($"   - Converted Effect: {fireball.Effects[0].GetType().Name}"); // MoveCardEffect
            }
            else Console.WriteLine("   ❌ [실패] 파이어볼의 효과(KillUnit)가 누락되었습니다.");
        }

        Console.WriteLine("\n=== 데이터 로드 테스트 종료 ===");
        Run();
    }

    public static void Run()
        {
            Console.WriteLine("=== ⚔️ 전투 시스템 테스트 ===");

            // 1. 세팅
            GameDataManager dataManager = new GameDataManager();
            dataManager.LoadAllData("./Data"); // 데이터 로드

            Player p1 = new Player { Name = "Hero", Health = 30, Mana = 10 };
            Player p2 = new Player { Name = "Villain", Health = 30, Mana = 0 };
            GameContext context = new GameContext();

            BattleSystem battleSys = new BattleSystem();

            // 2. 유닛 생성 (데이터 매니저에서 복사해옴)
            // 슬라임(11001): 300/300, Cost 1
            // 킹슬라임(11003): 800/800, Cost 3
            Card slime = CloneCard(dataManager.AllCards["11001"]);
            Card kingSlime = CloneCard(dataManager.AllCards["11003"]);

            // 소유권 설정
            slime.SetOwner(p1);
            kingSlime.SetOwner(p2);

            // 3. 소환 테스트
            Console.WriteLine("\n[Phase 1] 소환 테스트");
            p1.Hand.Add(slime);
            p1.PlayCard(slime, context); // 필드로 가야 함

            // 강제로 필드 배치 (킹슬라임)
            p2.InsertCard(ZoneType.Field, kingSlime);

            // 4. 공격 테스트 (소환 직후라 실패해야 정상)
            Console.WriteLine("\n[Phase 2] 소환 후유증 체크");
            battleSys.Attack(slime, kingSlime, context); // 실패 예상

            // 5. 턴 넘김 (상태 회복)
            Console.WriteLine("\n[Phase 3] 턴 시작 (상태 회복)");
            slime.RefreshUnitState(); // 이제 공격 가능

            // 6. 전투 (슬라임 -> 킹슬라임)
            // 슬라임(300) vs 킹슬라임(800) -> 슬라임 사망, 킹슬라임 HP 500 남음
            Console.WriteLine("\n[Phase 4] 유닛 간 교전");
            battleSys.Attack(slime, kingSlime, context);

            // 결과 확인
            Console.WriteLine($"\n[Result] P1 묘지: {p1.Graveyard.Count}장 (슬라임 있어야 함)");
            Console.WriteLine($"[Result] 킹슬라임 체력: {kingSlime.Health}/800");

            Console.WriteLine("\n=== 전투 시스템 테스트 종료 ===");
        }

        // 카드 복제 헬퍼 (데이터 원본 훼손 방지)
        private static Card CloneCard(Card original)
        {
            return new Card
            {
                Id = original.Id,
                Name = original.Name,
                Type = original.Type,
                Power = original.Power,
                MaxHealth = original.MaxHealth,
                Health = original.MaxHealth,
                AttackCost = original.AttackCost,
                Cost = original.Cost,
                Effects = new System.Collections.Generic.List<TCG_Project.Scripts.Interfaces.ICardEffect>(original.Effects)
            };
        }
        
}