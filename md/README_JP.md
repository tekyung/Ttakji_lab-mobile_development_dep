# C# 環境テスト (ConsoleRunner.cs を実行する場合) _ VS Code基準

- ビルド: `dotnet build`、実行: `dotnet run --project ./TCG_Project.csproj`

---

# 🎮 TCG プロジェクト: Core ロジック連動ガイド (UI 開発チーム向け)

TCGのコアロジックとUI演出を連携させるためのガイドラインです。
本ゲームは、コアエンジンと画面演出が完全に分離された**イベント駆動型アーキテクチャ (Event-Driven Architecture)**を採用しています。

UIチームは、ゲームのステータス(HP、デッキの残り枚数など)を直接書き換えてはいけません。`EventManager`が発信する放送(イベント)を購読(Subscribe)し、アニメーションやエフェクトを再生する役割のみを担います。

---

## 📌 1. イベントの購読および解除の基本ルール (重要)

Unityでイベントを扱う際、最も重要なのは**メモリリーク (Memory Leak) の防止**です。
イベントを購読する際は、必ず`OnEnable`で登録(`+=`)し、`OnDisable`または`OnDestroy`で解除(`-=`)しなければなりません。

```csharp
using UnityEngine;
using TCG_Project.Scripts.Managers;

public class BattleUIManager : MonoBehaviour
{
    private void OnEnable()
    {
        // 購読: 放送局の周波数を合わせます。
        EventManager.OnLifeChange += UpdateHealthBar;
        EventManager.OnCardMove += AnimateCardFly;
    }

    private void OnDisable()
    {
        // 解除: オブジェクトが非アクティブになるか破棄される時、必ず周波数を切断してください！(リーク防止)
        EventManager.OnLifeChange -= UpdateHealthBar;
        EventManager.OnCardMove -= AnimateCardFly;
    }

    // 実際の演出用関数
    private void UpdateHealthBar(Player player, int currentLife) 
    { 
        /* 体力バーのゲージが減るアニメーションを再生 */ 
    }

    private void AnimateCardFly(Card card, Player fromPlayer, ZoneType fromZone, Player toPlayer, ZoneType toZone) 
    { 
        /* カードがA地点からB地点へスッと飛んでいく演出 */ 
    }
}
```

---

## 📡 2. イベントリスト (Event Dictionary)

UI演出のために`EventManager`が提供する主要なイベントのリストです。用途に合わせて購読して使用してください。

### ⚔️ A. ゲーム進行および状態変化

| イベント名 | パラメータ (受け取る値) | 呼び出しタイミングおよびUI演出の推奨事項 |
| --- | --- | --- |
| `OnGameStart` | `Player p1, Player p2` | ゲーム開始時。両プレイヤーのプロフィール生成、初期体力バー/デッキUIのセットアップ |
| `OnTurnStart` | `int turn, string subject` | 各ラウンド開始時。画面中央に「ROUND 1」テキストアニメーション |
| `OnGameSet` | `Player winner` | 誰かのHPが0になりゲーム終了時。勝利/敗北のリザルト画面ポップアップ |
| `OnGameDraw` | `Player p1, Player p2, int turn` | 同時ダメージ等で引き分け処理された際に呼び出し。引き分け演出 |
| `OnLifeChange` | `Player p, int newValue` | 体力変動時に呼び出し。HPバーのアニメーションや被弾画面エフェクトの再生 |
| `OnTiebreaker` | `Player p1, Player p2` | タイブレーク状況突入時に呼び出し。タイブレーク演出 (例: カードのカウント) |

### 🎴 B. カードの移動およびアクション (最重要)

カードが画面上で動いたり、効果が発動したりする際に呼び出されます。

| イベント名 | パラメータ (受け取る値) | 呼び出しタイミングおよびUI演出の推奨事項 |
|---|---|---|
| `OnCardMove` | `Card c, Player p1, Zone z1, Player p2, Zone z2` | カードがゾーンを移動する時。(例: セットゾーン -> 戦場ゾーン)。軌跡移動アニメーション |
| `OnCardDraw` | `Card c, Player p, Zone z` | デッキからカードを引く時に呼び出し。デッキからカードが飛び出して手札に入る演出 |
| `OnPlayCard` | `Player p, Card c` | メインフェイズやスタックへの反応としてカードが「発動」される時。カードイラストのカットインや打撃エフェクト |
| `OnCardStateChange` | `Card c` | カードの状態が変化する時。(表裏の変更) |
| `OnPlayFailed` | `Card c` | カードの発動失敗時。(リソース不足、条件不満など) |
| `OnCardDiscard` | `Card c, Player p`, `Zone z` | カードが捨てられる時に呼び出し。カードがゾーンから捨てられる演出(現在は未使用) |
| `OnCardSet` | `Player p, Card c` | カードがセットゾーンに置かれる時。カードがセットゾーンに定着する演出(OnCardMove先行) |
| `OnCardStacked` | `Player p, Card c` | カードがスタックに積まれる時。カードがスタックゾーンに追加される演出(OnCardMove先行) |
| `OnCardUnstacked` | `Player p, Card c` | カードがスタックから除外される時。カードの効果が終了する演出(OnCardMove後行) |
| `OnCardBattlefield` | `Player p, Card c` | カードが戦場に配置される時。フィールドが展開される演出(OnCardMove先行) |
| `OnCardUnbattlefield` | `Player p, Card c` | カードが戦場から除外される時。フィールドが消える演出(OnCardMove後行) |
| `OnCardResourceAdded` | `Player p, Card c` | カードがリソースとして追加される時。リソースコイン/エネルギーUIの更新準備(OnCardMove先行) |

### 🔄 C. フェイズ移行の通知

画面上部の「現在のフェイズUI」を光らせたり更新したりする際に使用します。

| イベント名 | パラメータ (受け取る値) | 呼び出しタイミングおよびUI演出の推奨事項 |
|---|---|---|
| `OnResourcePhase` | `string subject, int turn` | リソースフェイズ開始時。リソースコイン/エネルギーUIの更新準備 |
| `OnDrawPhase` | `string subject, int turn` | ドローフェイズ開始時 |
| `OnSetPhase` | `string subject, int turn` | セットフェイズ開始時 |
| `OnOpenPhase` | `string subject, int turn` | オープンフェイズ開始時 |
| `OnMainPhase` | `string subject, int turn` | メインフェイズ開始時。戦闘開始演出 (VSマークなど) |

---

## ✋ 3. プレイヤー入力処理 (Human Input)

コアエンジンはBotの行動は自ら決定しますが、**人間(Human)**のターンが来るとUI側へ**「ユーザーがボタンを押すまで待機する！」**とコールバック(Callback)を投げます。
UIチームは該当するイベントを購読して画面にボタンを表示し、ユーザーが選択を終えたら**必ずコールバック関数を実行(`Invoke`)してエンジンに回答を返さなければなりません。** (返さないとゲームがフリーズします！)

| イベント名 | パラメータ (受け取る値) | 呼び出しタイミングおよびUI演出の推奨事項 |
| --- | --- | --- |
| `OnRequireSetPhaseAction` | `Player, GameContext, Action<Card>` | **セットフェイズ:** 自分の手札をクリックできるように活性化。ユーザーがカードを選んだら`コールバック(選んだカード)`を呼び出し |
| `OnRequireOpenPhaseAction` | `Player, Card, int, GameContext, Action<OpenPhaseChoice>` | **オープンフェイズ:** 「公開」/「廃棄」の2つのボタンUIをポップアップ。選択時に`コールバック(OpenPhaseChoice.Open または Abandon)`を呼び出し |
| `OnRequireStackResponse` | `Player, Card(自分のカード), Card(相手のカード), Action<bool>` | **スタック発動:** 相手から攻撃された時！自分のスタックカードを発動するか「はい/いいえ」ボタンをポップアップ。選択時に`コールバック(true/false)`を呼び出し |

### 📝 入力処理の実装例 (オープンフェイズ)

```csharp
private void OnEnable()
{
    EventManager.OnRequireOpenPhaseAction += ShowOpenAbandonUI;
}

private void ShowOpenAbandonUI(Player p, Card c, int cost, GameContext ctx, Action<OpenPhaseChoice> callback)
{
    // 1. 画面に [公開(コスト: cost)] / [廃棄] ボタンパネルを表示します。
    uiPanel.SetActive(true);

    // 2. ボタンに一時的にイベントを付与します。(ユーザーのクリック待機)
    btnOpen.onClick.AddListener(() => 
    {
        uiPanel.SetActive(false);
        callback.Invoke(OpenPhaseChoice.Open); // ★ コアエンジンへ回答を送信！(ゲーム進行再開)
    });

    btnAbandon.onClick.AddListener(() => 
    {
        uiPanel.SetActive(false);
        callback.Invoke(OpenPhaseChoice.Abandon); // ★ コアエンジンへ回答を送信！
    });
}
```

---

## ⚠️ 4. その他の注意事項 (Caveats)

1. **データへの直接修正は絶対禁止:** UIスクリプトから`Player.LifeTokens -= 1`のようにコアデータを直接操作しないでください。すべての論理演算は既にエンジンが完了させています。UIは受け取った値(`newValue`)を画面のテキストコンポーネントに反映させるだけで構いません。
2. **演出の遅延 (コルーチン待機):** ロジックは瞬時に演算されますが、視覚的な演出には時間が必要です。`BattleManager.cs`内部のフェイズ移行やカード使用時に`ActionDelay`の設定値があるので、UIアニメーションの時間に合わせてUnityのインスペクターからこの時間を調整してください。
3. **エフェクトの種類の確認:**
`OnPlayCard` が呼び出される際、該当カードの`Type`や名前(`Name`)を読み取り、攻撃カードなら銃弾エフェクト、防御カードなら盾エフェクトなど、アセットを分岐して生成してください。

---

# 開発進捗履歴 (Changelog)

## 26.01.30 進捗事項
- CardとEffectの分離、CardはEffectのidを呼び出すよう変更
- Cards.json内の各種数式をパース後、Eval(評価)してゲーム内の変数として活用する機能を実装
- Rules.jsonによるゲーム内の詳細数値管理を実装
- 各カードごとの発動条件設定 -> 使用可能かどうかの判別機能を追加
- カード効果の分岐設定 -> 1枚のカードが異なるタイプの効果を2つ以上含めるよう変更

## 26.01.31 進捗事項
- ターゲティングシステム実装(N個のデータベースからM個を指定)
- MoveCardEffect(カード移動効果)による各種カード移動効果の実装と代替
- ModifyCardEffect(カード修正効果)による各種カードステータス修正効果の実装
- 現在過渡期であり使用していないEffect.csが混在しているため、今後の整理が必要

## 26.02.10 進捗事項
- ユニット召喚時の効果発動条件をJSONに記入
  - 幼虫 (1ドロー): `"on_play_condition_type": "DeckNotEmpty"`
  - 爆弾蜂 (1破壊): `"on_play_condition_type": "EnemyUnitExist"`
  - ピクシードラゴン (200弱体化): `"on_play_condition_type": "EnemyUnitExist"`
- 変更されたルールでのシミュレーションテストに成功
- 最新化されたJSONを使用中。旧バージョン使用時はエラーが発生する点に注意

## 26.02.15 進捗事項
- Unityで使用するイベントロジックのドラフトを構成 (ノートPCのスペック問題でテスト未実施。まだ動作しない可能性あり)
- データファイルのパスを修正したが、後日再検証が必要
- ルールファイル `CommonConfig.json` の読み込みを実装予定 (現在は一時的に固定値を使用中)
- 当面はイベントログとコンソールログを同時出力する構成にし、進捗に合わせてコンソール側は徐々に削除する予定

## 26.02.27 進捗事項
### ✅ 1. 完了したコアシステム
- **フェイズチェーン＆非同期エンジン:** コルーチンとコールバック(Action)基盤でターンの流れを再設計し、カード効果の処理が終わるまでシステムが自ら待機(WaitUntil)するように変更。
- **アーキテクチャの分離:** カードの「召喚条件(PlayCondition)」と「効果発動条件(EffectCondition)」を完全に分離して動作するよう修正。
- **安全なステータスパイプライン:** すべてのステータス変動と破壊ロジックを `ModifyPower` という単一の窓口に統合。
- **ディスプレイのデカップリング(分離):** コアロジックは `EventManager` にシグナルとRich Text(カラータグ)のみを投げ、Unityとコンソールの両方で完全に互換性を持つよう実装。

### ⚠️ 2. Unityチーム引き継ぎ前/後の確認事項 (TODO)
- [ ] **JSONデータの最新化 (最重要)**
  ユーザーの直接選択(HumanChoice)、最も高い攻撃力(HighestPower)、ランダム(Random)のターゲティングモードをサポートするため、`CardEffectData`クラスに`target_mode`フィールドを追加しました。
  コードは`target_mode`をパースしますが、デフォルト値として"HighestPower"を使用するようになっています。(つまり、JSONに`target_mode`が明示されていなければ、自動的に最も高い攻撃力を持つ敵をターゲットにします。)
- [ ] **手動ターゲティング(HumanChoice)の一時封印 (Soft-Lock注意)**
  現在、バックエンドは`target_mode`がManualの際、UIの選択を無限に待機するように設計されています。
  そのため、UnityのUI側でターゲットをクリックしてコールバックを返すロジックが必要です。
  **措置:** 当面はゲームがフリーズしないよう、JSONデータの`target_mode`を一括で`HighestPower`に設定しておきました。
- [ ] **Unityリソースパス(Path)の検証**
  一次的にUnityプロジェクト内のフォルダ(`Assets/Resources/GameData`)が存在し、JSONファイルがその中に入っているか点検します。
  無ければ二次的にこのフォルダのDataフォルダのバックアップファイルを使用します。(ログ出力でどのパスのデータか確認できます。)
- [ ] **UIのイベントメモリリーク(Leak)防止**
  Unityで `EventManager` を購読して演出を作る際、必ずUnityの `OnEnable()` で `+=` 購読し、`OnDisable()` や `OnDestroy()` で `-=` 解除するようにしてください。(これを行わないとシーン遷移時に100%エラーが発生します。)

## 26.02.28 進捗事項
- LegacyEffectの削除およびコードの整理
- ファイルローディングシステムのロールバック
- MoveCardEffectの役割分離: 単発的なカード移動効果は `MoveCardEffect` へ、条件付きの期間移動は `RevertControllerEffect` へ分離
- RevertStatusEffect追加: ユニットの状態異常を一定ターン後に自動解除する効果を実装 (実装中)
- SwapCardEffect追加: カードの位置を同時にお互い入れ替える効果を実装 (復元中)

## 26.03.09 進捗事項
- 旧バージョンのコードを完全削除およびリファクタリング (「戦闘！傭兵の時代」のルールに沿って再移植)
- 現在までにリリースされたカード 4 + 40 枚すべての実装完了 (検証は攻撃・防御のみ完了)
- EventManagerの説明書をアップデート
- `DeckValidator.cs` を新ルールに合わせてアップデート (デッキの有効性検証)
- `Scripts/UI` フォルダ内にUnity専用の一時ファイルを作成。上書きしても問題なし。

## 26.03.14 進捗事項
- 全カード 4 + 40 枚の検証完了
- レガシーコードのリファクタリング
- `ConsoleRunner.cs`の118行目、`BattleManager.cs`の218行目でBotのデッキを直接選択できるようにアップデート。`RulebookCards.json`のカードIDで構成。
- `BattleManager.cs`の86行目でプレイヤー/Bot生成。コールバックロジックが未完成の場合、User.TypeをBotに統一する必要あり(自動進行モード)。
- エリーの「撃墜システム」カードを企画意図に合わせて修正 (表記上はスタック撃発時に手札を捨てるが、発動と同時に捨てるよう修正。設計上のタイポ)
- セットゾーンを単一のカード変数ではなく、リストとして管理することについての議論中 (ダイナの「機雷」のようにセットゾーン外でのカード効果発動に対する裁定)
- スタックの種類を防御だけでなく、無敵、反撃、火力などを追加実装

## 26.03.20 進捗事項
- `RulebookCards.json` のカード項目名を Card に変更 (UI側の要望を反映)
- `RulebookCards.json` のパスを `Assets/Resources/GameData` に変更
- スタック発動関連のバグ修正 (相手の自傷ダメージでもスタックが発動してしまうバグを修正)
- QAで状況テストを容易に行えるよう、ゲーム状態の注入ロジックを作成。(`ConsoleRunner`、`BattleManager` のデッキセッティング部分のコメントを参照)

## 26.03.22 進捗事項
- 03.21付でM1に `CardManual.md` ファイルをアップロードしました
- Assetsフォルダ内に TCG_Project フォルダを移動しました
- JSONファイル4つも `Assets/Resources/GameData` にパスを変更しました
- カード画像のパスは一時的に次のように設定しました (01がキャラクター、02〜11が該当テーマカード)
  - `GameDesign/card_image/ELLI/ELLI-01.png`
  - `GameDesign/card_image/VERONICA/VERO-01.png`
  - `GameDesign/card_image/DAINA/DAIN-01.png`
  - `GameDesign/card_image/SONIA/SONI-01.png`

## 26.03.28 進捗事項
- Unity側でスタックがコールバックされず、ゲームが無限待機状態になる問題を推定。一定時間待機後、自動的にコールバックが実行されるよう暫定措置。

## 26.03.31 進捗事項
- 全般的なロジックにタイムアウト(10秒) - 以降自動コールバック実行ロジックを追加 (スタック発動、セットカード選択などユーザー入力待機状況において)
- すべての傭兵、カード効果のコールバック待機状況にタイムアウトロジックを適用完了。タイムアウト発生時、コンソールに「時間超過 - 自動的に選択が行われます」のメッセージを出力
- UnityのC# 9.0バージョンサポート問題のため、一部の最新構文(例: レコード型、パターンマッチングなど)を使用せず、互換性のある方式へコードを修正完了
- `Utils` フォルダに `AsyncTimeout.cs` ファイルを追加 - 非同期メソッドにタイムアウト機能を提供するユーティリティクラス
- セット/オープンフェイズの処理順序を同時処理に変更
- その他のバグ修正完了

## 26.04.04 進捗事項
- `BattleManager` 内部のテスト用ハードコーディングデッキ/傭兵設定を完全削除。外部ロビーやマッチメイキングシステムから組み立てられた明細書(`PlayerSetupData` DTO)を注入されマッチを開始するよう、依存性の注入(DI)構造へ改編。
- `DeckValidator.cs` からタイブレークロジックを `TieBreakerChecker.cs` という独立したクラスに分離。既存のタイブレークのバグを修正。
- Unity作業とビルド間のファイル入出力(`System.IO`)パスの衝突を防ぐため、`IJsonLoader`インターフェースを導入。すべてのJSON読み込みがこのインターフェースを通じて行われるようリファクタリング。`UnityJsonLoader` と `FileJsonLoader` の2つの実装体を作成。
- カードの「裏面状態(`default_card_back`)」を `CommonConfig.json` に裏面画像パス(`GameDesign/card_image/Back_Common.png`)として追加。裏面カードは別個のカードではなく、カードの状態として管理するよう設計変更 (カードが裏返るたびにカードデータの `default_card_back` フィールドを参照して画像を変更)。
- カードは新たに裏面状態(`isFaceUp`)を bool 値として持つよう変更。デフォルト値は True(表面)。
- 各種イベントの追加 (実装優先度は低)</OpenPhaseChoice></Card>