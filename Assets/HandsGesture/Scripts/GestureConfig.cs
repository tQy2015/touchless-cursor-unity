using UnityEngine;

namespace HandsGesture
{
  [CreateAssetMenu(fileName = "GestureConfig", menuName = "HandsGesture/Gesture Config")]
  public class GestureConfig : ScriptableObject
  {
    [Header("カメラ選択")]
    [Tooltip("ON: 内蔵カメラを優先して選択する(開発機での動作確認用)。OFF: 外付けカメラを優先する(本来の運用時の想定)")]
    public bool preferBuiltInCamera = false;

    [Tooltip("この文字列を含むデバイス名を優先して選択する(例: 外付けUSBカメラの製品名の一部)。空なら除外リストのみで判定。preferBuiltInCamera=ON時は無視される")]
    public string preferredDeviceNameContains = "";

    [Tooltip("内蔵カメラ名の一部(例: FaceTime, Integrated, Built-in, 内蔵)。preferBuiltInCamera=OFF時はこれらを除外して外付けを選ぶ基準に、ON時はこれに一致するものを選ぶ基準に使う")]
    public string[] builtInDeviceNameContains = { "FaceTime", "Integrated", "Built-in", "内蔵" };

    [Header("左右移動: ゾーンしきい値 (手首x, 正規化0-1)")]
    [Range(0f, 0.5f)] public float leftZoneBoundary = 0.40f;
    [Range(0.5f, 1f)] public float rightZoneBoundary = 0.60f;

    [Header("左右移動: クールダウン")]
    [Tooltip("1ステップ発火後、次のステップ発火まで最低限空ける時間(秒)")]
    public float moveCooldownSeconds = 0.3f;

    [Header("決定: ピンチ判定")]
    [Tooltip("親指先-人差し指先の距離(手首-中指MCP距離で正規化)がこの値未満ならピンチ成立")]
    [Range(0f, 1f)] public float pinchThreshold = 0.3f;

    [Tooltip("決定イベントの多重発火防止クールダウン(秒)")]
    public float confirmCooldownSeconds = 0.5f;

    [Header("手の検出(本モジュール側のゲート)")]
    [Tooltip("この信頼度(handedness score)未満の検出は「手なし」として扱う。画面端で手が見切れた際の誤検出対策")]
    [Range(0f, 1f)] public float minHandPresenceConfidence = 0.5f;

    [Header("手の検出(MediaPipe Hand Landmarker 側のしきい値)")]
    [Tooltip("この信頼度未満では、そもそも手として検出しない(MediaPipe内部のしきい値)")]
    [Range(0f, 1f)] public float minHandDetectionConfidence = 0.5f;

    [Tooltip("フレーム間のトラッキング(追跡)を継続する信頼度のしきい値")]
    [Range(0f, 1f)] public float minTrackingConfidence = 0.5f;

    [Header("表示")]
    [Tooltip("セルフィー表示(鏡像)を前提に、手を右に動かしたらカーソルも右に動くよう水平反転を適用する")]
    public bool mirrorHorizontal = true;
  }
}
