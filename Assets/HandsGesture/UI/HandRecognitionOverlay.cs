using UnityEngine;
using UnityEngine.UI;

namespace HandsGesture.UI
{
  /// <summary>
  /// 開発/調整用のカメラパススルー表示。
  /// - カメラ映像をそのまま表示(パススルー)
  /// - 左/中央/右ゾーンの境界を色分けした帯(ポリゴン=Imageの矩形)で重ねて可視化
  /// - 現在の手首x座標にカーソル(丸)を表示。ピンチ中は色を変える
  ///
  /// しきい値(GestureConfigのゾーン境界・ピンチ閾値)を調整する際に、
  /// 「今どのゾーンにいるとカーソルが動くか」を目で見て把握できるようにするためのツール。
  /// </summary>
  public class HandRecognitionOverlay : MonoBehaviour
  {
    [SerializeField] private HandsGestureRunner _runner;
    [SerializeField] private RawImage _passthrough;
    [SerializeField] private RectTransform _passthroughRect;

    [SerializeField] private RectTransform _leftZoneBand;
    [SerializeField] private RectTransform _centerZoneBand;
    [SerializeField] private RectTransform _rightZoneBand;

    [SerializeField] private RectTransform _cursor;
    [SerializeField] private Image _cursorImage;

    [SerializeField] private Color _zoneBandColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color _cursorNormalColor = Color.yellow;
    [SerializeField] private Color _cursorPinchColor = Color.green;
    [SerializeField] private Color _cursorAbsentColor = new Color(1f, 1f, 1f, 0.3f);

    private bool _zoneBandsLaidOut;

    private void Update()
    {
      if (_runner == null)
      {
        return;
      }

      var texture = _runner.CurrentCameraTexture;
      if (texture != null && _passthrough != null)
      {
        _passthrough.texture = texture;
        // セルフィー表示に合わせて水平反転(GestureConfig.mirrorHorizontal と見た目を一致させる)
        if (_runner.Config != null && _runner.Config.mirrorHorizontal)
        {
          _passthrough.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }
      }

      if (!_zoneBandsLaidOut && _runner.Config != null)
      {
        LayoutZoneBands(_runner.Config);
        _zoneBandsLaidOut = true;
      }

      UpdateCursor();
    }

    private void LayoutZoneBands(GestureConfig config)
    {
      SetBandAnchors(_leftZoneBand, 0f, config.leftZoneBoundary);
      SetBandAnchors(_centerZoneBand, config.leftZoneBoundary, config.rightZoneBoundary);
      SetBandAnchors(_rightZoneBand, config.rightZoneBoundary, 1f);

      foreach (var band in new[] { _leftZoneBand, _centerZoneBand, _rightZoneBand })
      {
        if (band != null && band.TryGetComponent<Image>(out var image))
        {
          image.color = _zoneBandColor;
        }
      }
    }

    private static void SetBandAnchors(RectTransform rect, float xMin, float xMax)
    {
      if (rect == null)
      {
        return;
      }
      rect.anchorMin = new Vector2(xMin, 0f);
      rect.anchorMax = new Vector2(xMax, 1f);
      rect.offsetMin = Vector2.zero;
      rect.offsetMax = Vector2.zero;
    }

    private void UpdateCursor()
    {
      if (_cursor == null || _passthroughRect == null)
      {
        return;
      }

      var result = _runner.LatestResult;

      if (_cursorImage != null)
      {
        _cursorImage.color = !result.isHandPresent ? _cursorAbsentColor
          : result.isPinching ? _cursorPinchColor
          : _cursorNormalColor;
      }

      // handX(0-1, 鏡像反転適用後)をパススルー矩形内のローカルX位置に変換。縦は中央固定
      var width = _passthroughRect.rect.width;
      var localX = (result.handX - 0.5f) * width;
      _cursor.anchoredPosition = new Vector2(localX, 0f);
    }
  }
}
