using System.Collections;
using System.Linq;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;

namespace HandsGesture
{
  /// <summary>
  /// ノートPC内蔵カメラと外付けUSBカメラが両方存在する環境で、
  /// WebCamSource の既定選択(devices[0] = 内蔵カメラのことが多い)を上書きし、
  /// GestureConfig.preferBuiltInCamera に応じて内蔵/外付けのどちらかを明示的に選択する。
  /// (開発機での動作確認時は内蔵、実運用時は外付け、を切り替えられるようにするため)
  ///
  /// ImageSourceProvider.ImageSource は Bootstrap のコルーチンが完了するまで null なので、
  /// それを待ってから選択を適用する。選択は Runner(VisionTaskApiRunner系)が
  /// imageSource.Play() を呼ぶより前に完了している必要がある。
  /// Runner 側も同じ bootstrap.isFinished を WaitUntil で待つ実装のため、
  /// 同一フレームで両方の待機が解除された場合は StartCoroutine の呼び出し順(≒Hierarchy上の並び順)で
  /// 再開順序が決まる。保険として [DefaultExecutionOrder] で早めに実行させているが、
  /// 念のため**このコンポーネントを持つ GameObject は、Runner の GameObject より
  /// Hierarchy 上で上(先)に置くこと。**
  /// </summary>
  [DefaultExecutionOrder(-100)]
  public class ExternalWebCamSelector : MonoBehaviour
  {
    [SerializeField] private GestureConfig _config;

    private void Start()
    {
      StartCoroutine(SelectWhenReady());
    }

    private IEnumerator SelectWhenReady()
    {
      yield return new WaitUntil(() => ImageSourceProvider.ImageSource != null);

      var devices = WebCamTexture.devices;
      if (devices == null || devices.Length == 0)
      {
        Debug.LogWarning($"{nameof(ExternalWebCamSelector)}: カメラデバイスが見つかりません");
        yield break;
      }

      Debug.Log($"{nameof(ExternalWebCamSelector)}: 検出されたカメラ一覧 - {string.Join(", ", devices.Select(d => d.name))}");

      var preferBuiltIn = _config != null && _config.preferBuiltInCamera;
      var chosenIndex = ResolveCameraIndex(devices, preferBuiltIn);
      if (chosenIndex < 0)
      {
        Debug.LogWarning($"{nameof(ExternalWebCamSelector)}: 条件に合うカメラを特定できず既定(先頭)のまま使用します。" +
          $" GestureConfig の preferBuiltInCamera / preferredDeviceNameContains / builtInDeviceNameContains を調整してください");
        yield break;
      }

      var kind = preferBuiltIn ? "内蔵カメラ" : "外付けカメラ";
      Debug.Log($"{nameof(ExternalWebCamSelector)}: {kind}として \"{devices[chosenIndex].name}\" (index={chosenIndex}) を選択");

      var imageSource = ImageSourceProvider.ImageSource;
      if (imageSource is WebCamSource webCamSource)
      {
        webCamSource.SelectSource(chosenIndex);
      }
      else
      {
        Debug.LogWarning($"{nameof(ExternalWebCamSelector)}: 現在の ImageSource が WebCamSource ではないため選択を適用できません");
      }
    }

    private int ResolveCameraIndex(WebCamDevice[] devices, bool preferBuiltIn)
    {
      var builtInPatterns = _config != null ? _config.builtInDeviceNameContains : null;

      if (preferBuiltIn)
      {
        // 内蔵カメラ名のパターンに一致する最初のデバイスを選ぶ
        if (builtInPatterns != null && builtInPatterns.Length > 0)
        {
          for (var i = 0; i < devices.Length; i++)
          {
            if (MatchesAny(devices[i].name, builtInPatterns))
            {
              return i;
            }
          }
        }
        // 一致しなければ先頭(devices[0]は内蔵カメラのことが多い)にフォールバック
        return devices.Length > 0 ? 0 : -1;
      }

      // 外付け優先: preferredDeviceNameContains があれば最優先
      if (!string.IsNullOrEmpty(_config != null ? _config.preferredDeviceNameContains : null))
      {
        var preferredIndex = System.Array.FindIndex(devices,
          d => d.name.IndexOf(_config.preferredDeviceNameContains, System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (preferredIndex >= 0)
        {
          return preferredIndex;
        }
      }

      // 内蔵カメラ名のパターンに一致しない最初のデバイスを外付けとみなす
      if (builtInPatterns != null && builtInPatterns.Length > 0)
      {
        for (var i = 0; i < devices.Length; i++)
        {
          if (!MatchesAny(devices[i].name, builtInPatterns))
          {
            return i;
          }
        }
      }

      return -1;
    }

    private static bool MatchesAny(string deviceName, string[] patterns)
    {
      return patterns.Any(pattern =>
        !string.IsNullOrEmpty(pattern) &&
        deviceName.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }
  }
}
