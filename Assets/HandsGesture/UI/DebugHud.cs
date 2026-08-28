using UnityEngine;
using UnityEngine.UI;

namespace HandsGesture.UI
{
  /// <summary>
  /// しきい値調整用のデバッグ表示。現在のゾーン/ピンチ状態/手検出有無をテキスト表示する。
  /// </summary>
  public class DebugHud : MonoBehaviour
  {
    [SerializeField] private HandsGestureRunner _runner;
    [SerializeField] private Text _label;

    private void Update()
    {
      if (_runner == null || _label == null)
      {
        return;
      }

      var r = _runner.LatestResult;
      _label.text =
        $"hand: {(r.isHandPresent ? "detected" : "none")}\n" +
        $"zone: {r.zone}\n" +
        $"pinch: {(r.isPinching ? "yes" : "no")}";
    }
  }
}
