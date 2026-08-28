namespace HandsGesture
{
  public enum ZoneState
  {
    Left,
    Center,
    Right,
  }

  public readonly struct GestureFrameResult
  {
    /// <summary>このフレームで左移動が確定発火したか</summary>
    public readonly bool moveLeftFired;
    /// <summary>このフレームで右移動が確定発火したか</summary>
    public readonly bool moveRightFired;
    /// <summary>このフレームで決定が確定発火したか</summary>
    public readonly bool confirmFired;

    /// <summary>デバッグ表示用: 現在のゾーン</summary>
    public readonly ZoneState zone;
    /// <summary>デバッグ表示用: 現在ピンチ中か</summary>
    public readonly bool isPinching;
    /// <summary>デバッグ表示用: 手が検出されているか</summary>
    public readonly bool isHandPresent;
    /// <summary>デバッグ表示用/カーソル描画用: 手首の正規化x座標(0-1、鏡像反転適用後)</summary>
    public readonly float handX;

    public GestureFrameResult(bool moveLeftFired, bool moveRightFired, bool confirmFired,
      ZoneState zone, bool isPinching, bool isHandPresent, float handX)
    {
      this.moveLeftFired = moveLeftFired;
      this.moveRightFired = moveRightFired;
      this.confirmFired = confirmFired;
      this.zone = zone;
      this.isPinching = isPinching;
      this.isHandPresent = isHandPresent;
      this.handX = handX;
    }
  }
}
