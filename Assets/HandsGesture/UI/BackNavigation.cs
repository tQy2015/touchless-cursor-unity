using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandsGesture.UI
{
  /// <summary>
  /// 決定確認モーダルなどを介さず、単発の決定ジェスチャー(ピンチ)で
  /// 指定シーン(既定: ArcadeGestureDemo)へ戻る。
  /// Scene-1〜Scene-N のような「行き先だけの画面」からの帰り道用の実装。
  ///
  /// 非接触操作ではジェスチャーが認識されたことを都度見せるのが重要なため、
  /// ArcadeGestureDemo側の決定フローと同様に「状態変化(色)を見せる → 少し待つ → 遷移」
  /// のパターンを踏襲している。即座に遷移はしない。
  /// </summary>
  public class BackNavigation : MonoBehaviour
  {
    [SerializeField] private string _targetSceneName = "ArcadeGestureDemo";
    [SerializeField] private Text _statusLabel;
    [SerializeField] private string _recognizedText = "戻ります...";
    [SerializeField] private Color _recognizedColor = Color.green;
    [Tooltip("ジェスチャーを認識した状態変化を見せてから、実際に遷移するまでの待ち時間(秒)")]
    [SerializeField] private float _recognizedStateDelaySeconds = 0.6f;

    [Header("シーン入場直後の決定キャンセル")]
    [Tooltip("このシーンが始まってから、この秒数はConfirm(決定=戻る)入力を無視する。" +
      "直前のシーンでの決定ジェスチャーの余韻がそのままこのシーンで誤って戻ると" +
      "認識されるのを防ぐための猶予")]
    [SerializeField] private float _sceneEntryCooldownSeconds = 1f;

    private bool _isTransitioning;
    private float _acceptInputTime;

    private void Start()
    {
      _acceptInputTime = Time.time + _sceneEntryCooldownSeconds;
    }

    public void GoBack()
    {
      if (_isTransitioning || Time.time < _acceptInputTime)
      {
        return;
      }
      StartCoroutine(GoBackWithFeedback());
    }

    private IEnumerator GoBackWithFeedback()
    {
      _isTransitioning = true;

      if (_statusLabel != null)
      {
        _statusLabel.text = _recognizedText;
        _statusLabel.color = _recognizedColor;
      }

      yield return new WaitForSeconds(_recognizedStateDelaySeconds);

      SceneManager.LoadScene(_targetSceneName);
    }
  }
}
