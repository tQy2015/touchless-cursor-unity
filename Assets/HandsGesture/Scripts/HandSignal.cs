namespace HandsGesture
{
  /// <summary>
  /// MediaPipe の HandLandmarkerResult から抽出した、本モジュールが関心を持つ最小限の信号。
  /// カメラ・MediaPipeへの依存を持たない純粋なデータ構造にすることで、
  /// ArcadeGestureInterpreter を合成信号だけで単体テストできるようにする。
  /// </summary>
  public readonly struct HandSignal
  {
    /// <summary>信頼度十分な手が検出されているか</summary>
    public readonly bool isPresent;

    /// <summary>手首の正規化x座標(0-1、鏡像反転適用後)。isPresent=false のときは意味を持たない</summary>
    public readonly float handX;

    /// <summary>親指先-人差し指先の距離を手のサイズで正規化した値。小さいほどピンチに近い</summary>
    public readonly float pinchRatio;

    public HandSignal(bool isPresent, float handX, float pinchRatio)
    {
      this.isPresent = isPresent;
      this.handX = handX;
      this.pinchRatio = pinchRatio;
    }

    public static readonly HandSignal None = new HandSignal(false, 0.5f, 1f);
  }
}
