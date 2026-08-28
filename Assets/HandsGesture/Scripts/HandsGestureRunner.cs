using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace HandsGesture
{
  /// <summary>
  /// カメラ映像から MediaPipe Hand Landmarker で手を検出し、
  /// ArcadeGestureInterpreter を通して 左移動/右移動/決定 の3イベントだけを発火する。
  ///
  /// homuler/MediaPipeUnityPlugin 同梱の Hand Landmark Detection サンプル
  /// (HandLandmarkerRunner.cs)の検出ループを土台にしつつ、注釈描画やScreen表示は行わず
  /// イベント発火のみに絞っている。可視化が必要な場合はサンプルシーンを別途参照する。
  /// </summary>
  public class HandsGestureRunner : VisionTaskApiRunner<HandLandmarker>
  {
    [SerializeField] private GestureConfig _config;

    public UnityEvent onMoveLeft = new UnityEvent();
    public UnityEvent onMoveRight = new UnityEvent();
    public UnityEvent onConfirm = new UnityEvent();

    /// <summary>直近フレームのデバッグ情報(DebugHud用)</summary>
    public GestureFrameResult LatestResult { get; private set; }

    /// <summary>設定(ゾーン境界等)をUI側からも参照できるように公開。カメラパススルー描画用</summary>
    public GestureConfig Config => _config;

    /// <summary>現在のカメラ映像テクスチャ(カメラパススルー表示用)。未準備なら null</summary>
    public Texture CurrentCameraTexture => ImageSourceProvider.ImageSource?.GetCurrentTexture();

    private readonly HandLandmarkDetectionConfig _mpConfig = new HandLandmarkDetectionConfig();
    private TextureFramePool _textureFramePool;
    private ArcadeGestureInterpreter _interpreter;

    // LIVE_STREAM モードの結果コールバックは MediaPipe 側のワーカースレッドから呼ばれ、
    // Unity API(Time.time 等)はメインスレッド以外から呼ぶと例外になる。
    // そのためコールバック内では純粋なデータ変換(HandSignalReader.Read)のみ行い、
    // 結果はここに一時保存して、メインスレッドである Update() で消費する。
    private readonly object _pendingSignalLock = new object();
    private HandSignal? _pendingSignal;

    // 内蔵(フロントカメラ扱い)のカメラは isFrontFacing=true と判定され、
    // ImageSource.GetTransformationOptions().flipHorizontally によって
    // MediaPipeに渡す前のピクセル自体が既にミラー反転されている(直感的な「セルフィー」表示のため)。
    // 外付けカメラは isFrontFacing=false のことが多く、この自動反転が入らない。
    // config.mirrorHorizontal(「見た目を直感的なミラーにしたいか」)と
    // 既に反転済みかどうかをXORし、二重反転/反転なしを避けて常に一貫した向きにする。
    private bool _sourceAlreadyFlippedHorizontally;

    private void Update()
    {
      HandSignal? signal;
      lock (_pendingSignalLock)
      {
        signal = _pendingSignal;
        _pendingSignal = null;
      }

      if (signal == null || _interpreter == null)
      {
        return;
      }

      var frameResult = _interpreter.Update(signal.Value, Time.time);
      LatestResult = frameResult;

      if (frameResult.moveLeftFired)
      {
        onMoveLeft.Invoke();
      }
      if (frameResult.moveRightFired)
      {
        onMoveRight.Invoke();
      }
      if (frameResult.confirmFired)
      {
        onConfirm.Invoke();
      }
    }

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
    }

    protected override IEnumerator Run()
    {
      _interpreter = new ArcadeGestureInterpreter(_config);

      // GestureConfig(Inspectorで調整可能)の値をMediaPipe側のしきい値に反映する
      _mpConfig.MinHandDetectionConfidence = _config.minHandDetectionConfidence;
      _mpConfig.MinTrackingConfidence = _config.minTrackingConfidence;

      yield return AssetLoader.PrepareAssetAsync(_mpConfig.ModelPath);

      var options = _mpConfig.GetHandLandmarkerOptions(
        _mpConfig.RunningMode == Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null);
      taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
      var imageSource = ImageSourceProvider.ImageSource;

      yield return imageSource.Play();

      if (!imageSource.isPrepared)
      {
        Debug.LogError($"{nameof(HandsGestureRunner)}: Failed to start ImageSource, exiting...");
        yield break;
      }

      _textureFramePool = new TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
      _sourceAlreadyFlippedHorizontally = flipHorizontally;
      var imageProcessingOptions = new Mediapipe.Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: (int)transformationOptions.rotationAngle);

      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();
      var result = HandLandmarkerResult.Alloc(options.numHands);

      while (true)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return waitForEndOfFrame;
          continue;
        }

        req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
        yield return waitUntilReqDone;

        if (req.hasError)
        {
          Debug.LogWarning($"{nameof(HandsGestureRunner)}: Failed to read texture from the image source");
          continue;
        }

        var image = textureFrame.BuildCPUImage();
        textureFrame.Release();

        switch (taskApi.runningMode)
        {
          case Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE:
            if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
            {
              ProcessResult(result);
            }
            break;
          case Mediapipe.Tasks.Vision.Core.RunningMode.VIDEO:
            if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
            {
              ProcessResult(result);
            }
            break;
          case Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM:
            taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
            break;
        }
      }
    }

    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Mediapipe.Image image, long timestamp)
    {
      ProcessResult(result);
    }

    /// <summary>
    /// IMAGE/VIDEOモードではメインスレッドの Run() コルーチンから、
    /// LIVE_STREAMモードでは MediaPipe のワーカースレッドから呼ばれる。
    /// どちらの場合も Unity API を叩かず、信号への変換のみ行って Update() に渡す。
    /// </summary>
    private void ProcessResult(in HandLandmarkerResult result)
    {
      var effectiveMirror = _config.mirrorHorizontal != _sourceAlreadyFlippedHorizontally;
      var signal = HandSignalReader.Read(result, effectiveMirror, _config.minHandPresenceConfidence);
      lock (_pendingSignalLock)
      {
        _pendingSignal = signal;
      }
    }
  }
}
