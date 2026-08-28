namespace HandsGesture
{
  /// <summary>
  /// HandSignal(手首x座標・ピンチ比)から、アーケード風の離散イベント
  /// (左移動/右移動/決定)を1フレームずつ生成する状態機械。
  ///
  /// UnityEngine.Time や MediaPipe型に依存しない純粋C#クラスなので、
  /// 合成した HandSignal と経過時間だけを与えて単体テストできる。
  ///
  /// 左右移動: ゾーン(左/中央/右)を判定し、「中央→左」または「中央→右」の
  /// 遷移エッジでのみ1回発火させる(ヒステリシス)。連続して同じ側にいても再発火しない。
  /// 決定: ピンチ(開→閉)の遷移エッジで1回発火。ピンチ中は左右移動の判定を止める(ゲート)。
  /// どちらもクールダウンによる多重発火防止を二重にかけている。
  /// </summary>
  public class ArcadeGestureInterpreter
  {
    private readonly GestureConfig _config;

    private ZoneState _previousZone = ZoneState.Center;
    private float _lastMoveFireTime = float.NegativeInfinity;

    private bool _wasPinching;
    private float _lastConfirmFireTime = float.NegativeInfinity;

    public ArcadeGestureInterpreter(GestureConfig config)
    {
      _config = config;
    }

    /// <summary>
    /// 1フレーム分の信号を処理してイベント結果を返す。
    /// </summary>
    /// <param name="signal">このフレームの手信号(検出結果)</param>
    /// <param name="currentTimeSeconds">単調増加する経過時間(秒)。UnityではTime.timeを渡す想定</param>
    public GestureFrameResult Update(in HandSignal signal, float currentTimeSeconds)
    {
      if (!signal.isPresent)
      {
        // 手が消えたら「今どちらの端にいたか」の記憶はリセットする。
        // 再出現時にいきなり端から中央への遷移として誤発火させないため。
        _previousZone = ZoneState.Center;
        _wasPinching = false;
        return new GestureFrameResult(false, false, false, ZoneState.Center, false, false, 0.5f);
      }

      var zone = ClassifyZone(signal.handX);
      var isPinching = signal.pinchRatio < _config.pinchThreshold;

      // --- 決定(ピンチ 開→閉 のエッジ) ---
      var confirmFired = false;
      if (isPinching && !_wasPinching &&
          currentTimeSeconds - _lastConfirmFireTime >= _config.confirmCooldownSeconds)
      {
        confirmFired = true;
        _lastConfirmFireTime = currentTimeSeconds;
      }
      _wasPinching = isPinching;

      // --- 左右移動(中央→端 のエッジ)。ピンチ中は移動判定をゲートして干渉を避ける ---
      var moveLeftFired = false;
      var moveRightFired = false;

      if (!isPinching)
      {
        var cooldownOk = currentTimeSeconds - _lastMoveFireTime >= _config.moveCooldownSeconds;
        if (cooldownOk && _previousZone == ZoneState.Center && zone == ZoneState.Left)
        {
          moveLeftFired = true;
          _lastMoveFireTime = currentTimeSeconds;
        }
        else if (cooldownOk && _previousZone == ZoneState.Center && zone == ZoneState.Right)
        {
          moveRightFired = true;
          _lastMoveFireTime = currentTimeSeconds;
        }

        _previousZone = zone;
      }

      return new GestureFrameResult(moveLeftFired, moveRightFired, confirmFired, zone, isPinching, true, signal.handX);
    }

    private ZoneState ClassifyZone(float handX)
    {
      if (handX < _config.leftZoneBoundary)
      {
        return ZoneState.Left;
      }
      if (handX > _config.rightZoneBoundary)
      {
        return ZoneState.Right;
      }
      return ZoneState.Center;
    }
  }
}
