# HandsGesture モジュール マニュアル(LLM向け)

このドキュメントは、このリポジトリを初めて読むLLM(Claude等)が、コード全体を読み直さなくても
仕組み・設計判断・既知の落とし穴を把握できるようにするための参照資料。
**現在の進捗・未完タスクは`STATE.md`が正典。** このファイルは「仕組みの説明」に専念し、
進捗は書かない(STATE.mdと重複させない)。

## 1. これは何か

USBカメラ(または内蔵カメラ)の映像から Google MediaPipe Hand Landmarker で手を検出し、
**「左右移動」「決定」の2ジェスチャーだけ**を認識してUnityにイベントとして渡すモジュール。
アーケードゲームの「カーソル送り+決定ボタン」のような、離散的でシンプルな操作系を意図している。

**スコープ外(意図的に実装していない)**: 3つ以上のジェスチャー語彙、奥行き/3Dポインティング、
両手同時操作、特定の空間ディスプレイ実機との統合。詳細な経緯は`STATE.md`を参照。

## 2. 全体のデータフロー

```
WebCamTexture(カメラ)
  → HandsGestureRunner.Run()コルーチン: フレームをテクスチャとして読み取りMediaPipeへ
  → HandLandmarker(MediaPipe): 手のランドマーク(21点の正規化座標)を検出
  → HandsGestureRunner.ProcessResult(): HandSignalReader.Read() でランドマーク→HandSignalへ変換
      (ここは LIVE_STREAM モードではワーカースレッドから呼ばれる。詳細は §5)
  → HandsGestureRunner.Update()(メインスレッド): ArcadeGestureInterpreter.Update() で
      HandSignal → GestureFrameResult(このフレームで何が起きたか)に変換
  → onMoveLeft / onMoveRight / onConfirm (UnityEvent) を発火
  → SelectionCursorUI.MoveLeft/MoveRight/Confirm がUI側の状態を更新
```

## 3. ファイル構成と役割

```
Assets/HandsGesture/
  Scripts/
    GestureConfig.cs          … しきい値・カメラ選択設定をまとめたScriptableObject(Inspectorで調整)
    HandSignal.cs              … MediaPipe非依存の最小データ(isPresent, handX, pinchRatio)
    HandSignalReader.cs         … HandLandmarkerResult → HandSignal の変換(MediaPipe型を知るのはここだけ)
    ArcadeGestureInterpreter.cs … HandSignal → GestureFrameResult の状態機械(MediaPipe/Unity非依存の純粋C#)
    GestureEvents.cs            … ZoneState enum, GestureFrameResult struct の定義
    HandsGestureRunner.cs       … カメラ→MediaPipe検出ループ本体。homulerサンプルのRunnerを土台に自作
    ExternalWebCamSelector.cs   … 内蔵/外付けカメラの明示選択
  UI/
    SelectionCursorUI.cs        … 選択肢のハイライト+決定確認モーダルの状態機械
    DebugHud.cs                 … zone/pinch/hand検出のテキスト表示
    HandRecognitionOverlay.cs   … カメラパススルー+ゾーン帯+カーソルの可視化(調整用)
  Editor/
    ArcadeGestureDemoSceneBuilder.cs … シーンをコードで再現性よく組み立てるビルダー
      (メニュー: Tools/HandsGesture/Build Arcade Gesture Demo Scene)
  Scenes/ArcadeGestureDemo.unity … 上記ビルダーで生成された実行可能シーン
  GestureConfig.asset            … 実際に使われる設定アセット(Inspectorで値を変更可能)

Assets/MediaPipeUnity/            … homuler/MediaPipeUnityPlugin(ベンダーコード、原則編集しない)
  Samples/Common/Scripts/Bootstrap.cs … 例外的に1箇所パッチ済み(§6参照)
```

## 4. 中核ロジック: ArcadeGestureInterpreter

`Assets/HandsGesture/Scripts/ArcadeGestureInterpreter.cs`。
**MediaPipe型にもUnityEngine.Timeにも依存しない純粋C#**なので、合成した`HandSignal`と
経過秒数だけを与えてロジックを単体テストできる(現状テストコードは未整備、書くならここから)。

### 左右移動: ゾーン+ヒステリシスの離散ステップ
- `handX`(0-1)を`leftZoneBoundary`未満=Left、`rightZoneBoundary`超=Right、それ以外=Centerに分類
- **「前フレームがCenterで今フレームがLeft/Right」という遷移エッジでのみ**1回発火する
- 同じ端に居続けても連射しない。再度発火させるには一度Centerへ戻る必要がある(ヒステリシス)
- `moveCooldownSeconds`による時間クールダウンも二重にかかる

### 決定: ピンチの遷移エッジ
- 親指先-人差し指先の距離を手のサイズ(手首-中指MCP距離)で正規化した`pinchRatio`が
  `pinchThreshold`未満なら「ピンチ成立」
- 「開いている→ピンチ」の遷移エッジで1回発火(`confirmCooldownSeconds`でクールダウン)
- **ピンチ中は左右移動の判定そのものを止める**(ゲート)。決定操作中に手がぶれてもカーソルが動かないようにするため

### 手が消えた場合
- `previousZone`を強制的にCenterへ戻す。再出現時に「端→中央」のような不自然な遷移で
  誤発火しないようにするため

## 5. 既知の落とし穴と対処(実装済み)

このモジュールは実機テストで複数のハマりどころに遭遇し、すべて修正済み。
**同種の問題が再発したら、まずここを疑う。**

### 5.1 LIVE_STREAMコールバックはワーカースレッドから呼ばれる
`HandLandmarker`を`RunningMode.LIVE_STREAM`で使うと、結果コールバック
(`OnHandLandmarkDetectionOutput`)はMediaPipe側のネイティブワーカースレッドから呼ばれる。
ここで`Time.time`など**Unity APIを直接呼ぶと`UnityException: get_time can only be called
from the main thread`でクラッシュする**。

対処: コールバック内(`ProcessResult`)は`HandSignalReader.Read`によるスレッドセーフな
純粋データ変換のみ行い、結果を`lock`で保護した`_pendingSignal`に一時保存。実際の
`ArcadeGestureInterpreter.Update`呼び出しとイベント発火はメインスレッドの`Update()`で行う。

### 5.2 glogはEditorで2回目以降のPlayでクラッシュする
glog(MediaPipeのログライブラリ)は初期化状態をネイティブライブラリ内の静的変数として持つ。
Unityのドメインリロードはマネージド(C#)側の状態しかリセットしないため、**同一Editorプロセス
内でPlayを2回目以降押すと、`Glog.Initialize`が"Check failed"で`MediaPipeException: MediaPipe
Aborted`を投げてクラッシュする**(`Glog.Shutdown()`を呼んでも再初期化に失敗するケースがある)。

対処: `Assets/MediaPipeUnity/Samples/Common/Scripts/Bootstrap.cs`を1箇所パッチ。
`UnityEditor.SessionState`(エディタプロセス内で永続、エディタ再起動でリセットされるKVS)を使い、
**Editor上ではプロセスにつき一度だけ`Glog.Initialize`を呼ぶ**ようにした。それに合わせて
`OnApplicationQuit`側の`Glog.Shutdown()`もEditorでは呼ばない(次のPlayで再初期化しようと
しないため無害)。ビルド後の実行ファイルでは従来どおりInitialize/Shutdownのペアを維持
(`#if UNITY_EDITOR`で分岐)。

これは`Assets/MediaPipeUnity/`(ベンダーコード)への数少ない例外的パッチ。他のhomuler
コードは原則編集しない方針だが、これは動作を継続させるために必要だった。

### 5.3 内蔵カメラと外付けカメラでミラー(鏡像)の向きが逆転する
`WebCamDevice.isFrontFacing`がtrueと判定されるカメラ(内蔵カメラで多い)は、
`ImageSource.GetTransformationOptions().flipHorizontally`によって**MediaPipeに渡す前の
ピクセル自体が既に自動でミラー反転**される(直感的な「セルフィー」表示のため)。
外付けカメラは`isFrontFacing=false`と判定されることが多く、この自動反転が入らない。

`GestureConfig.mirrorHorizontal`(見た目を直感的なミラーにしたいかの設定)を無条件に
`HandSignalReader.Read`へ渡すと、内蔵カメラでは「自動反転+手動反転」で二重反転し、
外付けカメラでは反転なしになり、**カメラ種別によって手の左右移動とカーソル移動の向きが
逆転する**不具合があった。

対処: `HandsGestureRunner.Run()`で実際に`flipHorizontally`されたかを記録し
(`_sourceAlreadyFlippedHorizontally`)、`ProcessResult`で
`effectiveMirror = config.mirrorHorizontal != _sourceAlreadyFlippedHorizontally`(XOR)
を計算して`HandSignalReader.Read`に渡す。これによりカメラ種別によらず常に一貫した向きになる。

### 5.4 手が画面端で見切れるとピンチ(決定)が誤爆する
手が画面端で一部隠れると、ランドマーク自体は出力され続けるが位置が不安定になり、
親指先-人差し指先の距離が異常接近して誤ってピンチと判定されることがある。

対処(部分的): `HandSignalReader.Read`にMediaPipeの`handedness`(左右判定)信頼度スコアを
`GestureConfig.minHandPresenceConfidence`でゲートする引数を追加。スコアが閾値未満のフレームは
「手なし」として扱う。**これは緩和策であり根治はしていない**(2026-08-21時点、STATE.md参照)。
しきい値を上げれば誤爆はさらに減るが、通常の検出も弾かれやすくなるトレードオフがある。
UI側の対策として§7の決定確認モーダルも参照。

## 6. GestureConfig の全フィールド

`Assets/HandsGesture/Scripts/GestureConfig.cs`。ScriptableObjectなのでInspectorで
実行中でも値を変更でき、即座に反映される(MonoBehaviour側は`_config`を参照で持つのみ)。

| フィールド | 意味 |
|---|---|
| `preferBuiltInCamera` | ON: 内蔵カメラ優先(開発機での動作確認用)。OFF: 外付けカメラ優先(本来の運用時想定) |
| `preferredDeviceNameContains` | この文字列を含むデバイス名を最優先で選択。`preferBuiltInCamera=OFF`時のみ有効 |
| `builtInDeviceNameContains` | 内蔵カメラ名のパターン一覧。ON/OFFどちらの判定にも使う(§カメラ選択ロジック参照) |
| `leftZoneBoundary` / `rightZoneBoundary` | ゾーン境界(handXの正規化座標0-1) |
| `moveCooldownSeconds` | 左右移動の連続発火防止クールダウン(秒) |
| `pinchThreshold` | ピンチ判定の距離しきい値(小さいほど厳しい=つまむ動作が必要) |
| `confirmCooldownSeconds` | 決定の連続発火防止クールダウン(秒) |
| `minHandPresenceConfidence` | 本モジュール側のゲート。handedness信頼度がこれ未満なら「手なし」扱い(§5.4) |
| `minHandDetectionConfidence` | MediaPipe内部のしきい値。これ未満ではそもそも手として検出しない |
| `minTrackingConfidence` | MediaPipe内部のしきい値。フレーム間トラッキング継続の信頼度 |
| `mirrorHorizontal` | 見た目を直感的なミラー(セルフィー)にしたいか。実際の反転適用はXOR処理される(§5.3) |

## 7. 決定確認モーダル(UI側の誤爆対策)

`SelectionCursorUI`は1回のConfirm(ピンチ)では即座に確定せず、**「戻る/決定」の2択モーダル**
を開く。モーダル中もMoveLeft/MoveRight/Confirmの同じ3操作で完結する:

- モーダルを開いた直後は「戻る」側にカーソルがある(デフォルトで安全側)
- 左ジェスチャー → カーソルを「戻る」へ
- 右ジェスチャー → カーソルを「決定」へ
- 2回目のConfirm(ピンチ): カーソルが「決定」側ならそこで初めて確定。「戻る」側ならキャンセルして
  選択画面に戻る(選択中indexは変わらない)

これにより、§5.4の誤爆がたとえ発生しても、モーダルで再度「決定」側にカーソルを動かしてから
2回目のピンチをしない限り確定しない。誤爆1回だけでは実害が出にくい設計。

実装は`SelectionCursorUI`内の`Mode`(Selecting / ConfirmingModal)で状態を切り替えている。

## 8. カメラ選択ロジック(ExternalWebCamSelector)

`Assets/HandsGesture/Scripts/ExternalWebCamSelector.cs`。`[DefaultExecutionOrder(-100)]`で
早めに実行されるが、より確実にするため**Hierarchy上でも`HandsGestureRunner`より前**に
配置すること(`ArcadeGestureDemoSceneBuilder`はこの順序を守って生成している)。

`ImageSourceProvider.ImageSource`は`Bootstrap`のコルーチンが完了するまでnullなので、
`WaitUntil`でそれを待ってから`WebCamSource.SelectSource(index)`を呼ぶ。この選択は
`HandsGestureRunner`が`imageSource.Play()`を呼ぶより前に完了している必要がある。

選択ロジック(`ResolveCameraIndex`):
- `preferBuiltInCamera=true`: `builtInDeviceNameContains`のいずれかに一致する最初のデバイス
  (無ければ`devices[0]`にフォールバック)
- `preferBuiltInCamera=false`: `preferredDeviceNameContains`が指定されていれば最優先。
  次に`builtInDeviceNameContains`のどれにも一致しない最初のデバイスを外付けとみなす

コンソールに検出されたカメラ名一覧と選択結果がログ出力されるので、実機のカメラ名を
確認して`preferredDeviceNameContains`等を調整する。

## 9. シーンの再生成方法

`Assets/HandsGesture/Scenes/ArcadeGestureDemo.unity`はGUIでの手作業ではなく、
`Assets/HandsGesture/Editor/ArcadeGestureDemoSceneBuilder.cs`のコードで組み立てられている。
GameObject配置・UnityEvent配線をすべてコードで行うことで、シーン構成をレビュー可能な
C#として残し、再現性を確保している。

**シーン構造を変更したい場合は、シーンファイルを直接編集するのではなく
`ArcadeGestureDemoSceneBuilder.Build()`を修正してから再実行すること。**

実行方法:
- Editor上のメニュー: `Tools > HandsGesture > Build Arcade Gesture Demo Scene`
- コマンドライン(Unity Editorプロセスがそのプロジェクトを開いていないことが前提):
  ```
  /Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics \
    -projectPath "<このリポジトリのパス>" \
    -executeMethod HandsGesture.Editor.ArcadeGestureDemoSceneBuilder.Build \
    -quit -logFile <ログ出力先>
  ```
  Unity Editorが同じプロジェクトを既に開いていると
  「別のUnityが起動中で、このプロジェクトを開いているようです」で失敗するので、
  実行前にEditorを閉じる必要がある(`lsof <project>/Temp/UnityLockfile`でロック保持プロセスを確認可能)。

## 10. 単体テストが書きやすい境界

もしテストを追加するなら、以下の境界が最も投資対効果が高い:

- `ArcadeGestureInterpreter`: MediaPipe/Unity非依存。`HandSignal`を手で合成し、
  時間を進めながら`Update()`を呼んで`GestureFrameResult`を検証できる
  (ゾーン境界の境目、ヒステリシス、クールダウン、ピンチ中の移動ゲート等)
- `HandSignalReader.Read`: `HandLandmarkerResult`を手で組み立てれば
  (`NormalizedLandmarks`のリストを直接構築)、mirrorHorizontal/confidenceゲートの
  境界値テストが可能

現状(2026-08-21時点)これらのテストコードは未整備。
