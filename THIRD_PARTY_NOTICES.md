# サードパーティ ライセンス表記

このリポジトリは以下のソフトウェアを利用する(同梱はしていない。セットアップ手順で
利用者が別途導入する)。

## MediaPipe (Google)

- ライセンス: Apache License 2.0
- リポジトリ: https://github.com/google-ai-edge/mediapipe
- Hand Landmarkerモデルファイルも同ライセンスの対象
- "MediaPipe" "Google" 等の商標・製品名を本リポジトリのブランディングとして使用していない

## MediaPipeUnityPlugin (homuler)

- ライセンス: MIT License
- Copyright (c) 2021 homuler
- リポジトリ: https://github.com/homuler/MediaPipeUnityPlugin

## 改変箇所

`Assets/HandsGesture/`配下のコードはこのリポジトリのオリジナル実装。
homulerプラグインのサンプルコード(`Bootstrap.cs`)に対しては、Unity Editorでの
複数回Play時のglog再初期化クラッシュを回避するためのガード処理を追加している
(利用者が`.unitypackage`をインポートした後、各自の環境で同様のパッチを適用する必要がある。
詳細は `docs/implementation_manual.md` §5.2 を参照)。
