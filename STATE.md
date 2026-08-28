# STATE.md — touchless-cursor-unity セッションステート

**最終更新:** 2026-08-28
**リポジトリ:** https://github.com/tQy2015/touchless-cursor-unity (Public)

## 経緯

非公開repo `mediapipe-hands-unity` で作った「左右移動+決定」アーケード風タッチレスUIの
MVP実装を、公開テンプレートとして切り出したもの。MediaPipe(Apache 2.0)/homuler(MIT)の
ライセンス確認済み(2026-08-28)、再配布条件を満たせば公開して問題ないと判断。

将来的にMediaPipe以外の検出エンジンを実装する可能性を見込み、リポジトリ名は
検出エンジン名を含まない `touchless-cursor-unity` を採用(2026-08-28)。

## 進行中タスク(公開テンプレート化)

- [x] 空リポジトリ作成・GitHub public repo化(2026-08-28)
- [x] `Assets/HandsGesture/` を `mediapipe-hands-unity` からコピー(2026-08-28)
- [x] `docs/implementation_manual.md` をコピー・非公開案件名(SRD-ELF)を汎用表現に置換(2026-08-28)
- [x] README(セットアップ手順)作成
- [x] THIRD_PARTY_NOTICES.md 作成(Apache 2.0 + MIT表記、Bootstrap.cs改変箇所の明記)
- [x] LICENSE本文確定(MIT、2026-08-28)
- [x] `mediapipe-hands-unity`からフルUnityプロジェクト化(2026-08-28)。
      `ProjectSettings/`・`Packages/manifest.json`・`Packages/packages-lock.json`・
      `Assets/MediaPipeUnity/`(homulerサンプル資産・MIT・3.6MB)をコピー。
      `Packages/com.github.homuler.mediapipe`(ネイティブライブラリ本体・392MB)は
      意図的に**含めない**(利用者が`.unitypackage`をインポートすると自動的に
      同じ場所へ展開される想定、`packages-lock.json`の`source: embedded`参照は
      相対パスなので機種依存なし)。`Library/`・`Logs/`・`UserSettings/`もコピー不要
      (Unity初回オープン時に再生成)
- [ ] コピーしたシーン(`ArcadeGestureDemo.unity`等)がhomulerプラグイン未導入状態で
      Missing Prefab等のエラーを出さないか確認(Bootstrap.prefab参照が外部依存のため)。
      → homulerインポート後に実際にUnityで開いて動作確認する必要あり
- [ ] `Assets/HandsGesture/GestureConfig.asset` に含まれる調整値(内蔵/外付けカメラ判定名等)が
      個人環境依存でないか確認(`preferredDeviceNameContains`等)
- [ ] リポジトリ名を反映してnamespace/フォルダ名を `HandsGesture` → `TouchlessCursor` に
      リネームするか検討(現状は移植コストを避けて`HandsGesture`のまま)
- [x] コミット・push(2026-08-28)

## 元repoとの関係

- `mediapipe-hands-unity`(非公開)は元のまま維持。研究ドキュメント・実案件(SRD-ELF)由来の
  文脈はそちらに残す
- このrepoは実装コードの汎用テンプレートとしての複製・独立repo。以後の機能追加は
  基本的にこちら側で行い、必要なら元repoへも反映を検討(現状は片方向コピーのみ)
