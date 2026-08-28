using Mediapipe.Tasks.Vision.HandLandmarker;

namespace HandsGesture
{
  /// <summary>
  /// MediaPipe の HandLandmarkerResult(ランドマーク座標)を、
  /// ArcadeGestureInterpreter が扱う HandSignal(handX, pinchRatio)へ変換する薄い層。
  /// MediaPipe の型を知っているのはこのクラスだけにする。
  /// </summary>
  public static class HandSignalReader
  {
    // MediaPipe Hand Landmarker のランドマークインデックス
    private const int Wrist = 0;
    private const int ThumbTip = 4;
    private const int IndexTip = 8;
    private const int MiddleFingerMcp = 9;

    /// <summary>
    /// <paramref name="minHandPresenceConfidence"/> は handedness(左右判定)のスコアをゲートに使う。
    /// 手が画面端で見切れる/一部隠れると、ランドマーク自体は出力され続けるものの
    /// 位置が不安定になり(親指-人差し指が異常接近してピンチと誤判定される等)、
    /// このスコアが下がるので、それを検出品質の目安として弾く。
    /// </summary>
    public static HandSignal Read(in HandLandmarkerResult result, bool mirrorHorizontal, float minHandPresenceConfidence)
    {
      if (result.handLandmarks == null || result.handLandmarks.Count == 0)
      {
        return HandSignal.None;
      }

      if (result.handedness != null && result.handedness.Count > 0)
      {
        var categories = result.handedness[0].categories;
        if (categories != null && categories.Count > 0 && categories[0].score < minHandPresenceConfidence)
        {
          return HandSignal.None;
        }
      }

      // NumHands=1 を前提に先頭の手のみを使う(本MVPのスコープ: 片手のみ)
      var landmarks = result.handLandmarks[0].landmarks;
      if (landmarks == null || landmarks.Count <= MiddleFingerMcp)
      {
        return HandSignal.None;
      }

      var wrist = landmarks[Wrist];
      var thumbTip = landmarks[ThumbTip];
      var indexTip = landmarks[IndexTip];
      var middleMcp = landmarks[MiddleFingerMcp];

      var handX = mirrorHorizontal ? 1f - wrist.x : wrist.x;

      var handSize = Distance2D(wrist.x, wrist.y, middleMcp.x, middleMcp.y);
      var pinchDistance = Distance2D(thumbTip.x, thumbTip.y, indexTip.x, indexTip.y);
      // 手のサイズが極端に小さい(検出不安定)場合の 0 除算を避ける
      var pinchRatio = handSize > 1e-4f ? pinchDistance / handSize : 1f;

      return new HandSignal(true, handX, pinchRatio);
    }

    private static float Distance2D(float x1, float y1, float x2, float y2)
    {
      var dx = x1 - x2;
      var dy = y1 - y2;
      return UnityEngine.Mathf.Sqrt((dx * dx) + (dy * dy));
    }
  }
}
