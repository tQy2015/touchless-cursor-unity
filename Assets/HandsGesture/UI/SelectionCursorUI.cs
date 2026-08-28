using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandsGesture.UI
{
  /// <summary>
  /// 横一列に並んだ選択肢の中から現在の index をハイライトし、
  /// MoveLeft/MoveRight/Confirm の3操作だけで選択・決定するアーケード風UI。
  /// HandsGestureRunner の onMoveLeft/onMoveRight/onConfirm から
  /// Inspector上で本コンポーネントの MoveLeft/MoveRight/Confirm を呼ぶよう配線する。
  ///
  /// 決定の流れ(誤爆対策として状態変化を都度見せてから次に進む):
  /// 1. 選択中に1回目のConfirm(ピンチ) → 選んでいるOptionが緑になる(「認識した」合図)
  /// 2. Pre Modal Delay Seconds 待つ(この間は入力を受け付けない)
  /// 3. 「戻る/決定」の2択モーダルが開く(初期カーソルは戻る側)。
  ///    モーダルが開いた直後は Modal Input Cooldown Seconds の間入力を無視する
  /// 4. モーダル中、左=戻る/右=決定でカーソル移動。2回目のConfirmで、
  ///    カーソルが「決定」側ならそこで初めて確定処理へ、「戻る」側ならキャンセルして選択に戻る
  /// 5. 確定処理: 決定ボタンが緑になる → Confirmed State Delay Seconds 待つ →
  ///    選んだOptionに対応するシーン(Scene-1〜Scene-N)へ遷移
  /// </summary>
  public class SelectionCursorUI : MonoBehaviour
  {
    [SerializeField] private Image[] _optionHighlights;
    [SerializeField] private Text _confirmedLabel;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _highlightColor = Color.yellow;
    [SerializeField] private Color _confirmedFlashColor = Color.green;

    [Header("移動直後の決定キャンセル(選択中/モーダル中とも共通)")]
    [Tooltip("MoveLeft/MoveRightでカーソル(選択中のOption、またはモーダル内の戻る/決定)が" +
      "動いた直後、この秒数はConfirm(決定)入力を無視する。左右移動のジェスチャーの余韻で" +
      "そのまま決定と誤認識されるのを防ぐための猶予")]
    [SerializeField] private float _moveToConfirmCooldownSeconds = 0.5f;

    [Header("シーン入場直後の決定キャンセル")]
    [Tooltip("このシーンが始まってから、この秒数はConfirm(決定)入力を無視する。" +
      "直前のシーンでの決定ジェスチャーの余韻がそのままこのシーンで誤って決定と" +
      "認識されるのを防ぐための猶予")]
    [SerializeField] private float _sceneEntryCooldownSeconds = 1f;

    [Header("1回目のConfirm → モーダル表示")]
    [Tooltip("1回目のConfirmで選択中のOptionが緑になってから、確認モーダルが開くまでの待ち時間(秒)")]
    [SerializeField] private float _preModalDelaySeconds = 0.6f;

    [Header("決定確認モーダル")]
    [SerializeField] private GameObject _modalPanel;
    [SerializeField] private Image _modalBackHighlight;
    [SerializeField] private Image _modalConfirmHighlight;
    [Tooltip("モーダルが開いた直後、この秒数は入力(MoveLeft/MoveRight/Confirm)を無視する。" +
      "1回目のConfirmを発火させたジェスチャー(ピンチの余韻等)がそのままモーダル側の" +
      "誤操作に繋がらないようにするための猶予")]
    [SerializeField] private float _modalInputCooldownSeconds = 1f;

    [Header("モーダルの決定 → シーン遷移")]
    [Tooltip("モーダルで決定確定した際、決定ボタンの状態変化(色)を見せてから実際にシーン遷移するまでの待ち時間(秒)")]
    [SerializeField] private float _confirmedStateDelaySeconds = 0.6f;
    [Tooltip("遷移先シーン名のプレフィックス。Option1確定なら \"Scene-1\" を読み込む")]
    [SerializeField] private string _sceneNamePrefix = "Scene-";

    [Header("モーダルの戻る → クローズ")]
    [Tooltip("モーダルで「戻る」を確定した際、戻るボタンの状態変化(色)を見せてからモーダルを閉じるまでの待ち時間(秒)。" +
      "非接触操作では特に、ジェスチャーを認識したことを都度見せることが重要なため、決定側と同じパターンにしている")]
    [SerializeField] private float _cancelledStateDelaySeconds = 0.6f;

    private enum Mode
    {
      Selecting,
      PendingModal,
      ConfirmingModal,
      ClosingModal,
      TransitioningToScene,
    }

    // モーダル内の選択: 0=戻る(キャンセル), 1=決定(確定)
    private const int ModalBackIndex = 0;
    private const int ModalConfirmIndex = 1;

    private Mode _mode = Mode.Selecting;
    private int _currentIndex;
    private int _modalCursorIndex = ModalBackIndex;
    private float _modalInputAcceptTime = -1f;
    private float _confirmAcceptTimeAfterMove = -1f;

    private bool IsInputBlocked =>
      _mode == Mode.PendingModal ||
      _mode == Mode.ClosingModal ||
      _mode == Mode.TransitioningToScene ||
      (_mode == Mode.ConfirmingModal && Time.time < _modalInputAcceptTime);

    private void Start()
    {
      _currentIndex = 0;
      RefreshHighlight();
      CloseModal();
      _confirmAcceptTimeAfterMove = Time.time + _sceneEntryCooldownSeconds;
    }

    /// <summary>端でクランプ(ラップしない)。最も単純なアーケード挙動</summary>
    public void MoveLeft()
    {
      if (IsInputBlocked)
      {
        return;
      }

      if (_mode == Mode.ConfirmingModal)
      {
        SetModalCursor(ModalBackIndex);
        _confirmAcceptTimeAfterMove = Time.time + _moveToConfirmCooldownSeconds;
        return;
      }

      if (_optionHighlights == null || _optionHighlights.Length == 0)
      {
        return;
      }
      _currentIndex = Mathf.Max(0, _currentIndex - 1);
      RefreshHighlight();
      _confirmAcceptTimeAfterMove = Time.time + _moveToConfirmCooldownSeconds;
    }

    public void MoveRight()
    {
      if (IsInputBlocked)
      {
        return;
      }

      if (_mode == Mode.ConfirmingModal)
      {
        SetModalCursor(ModalConfirmIndex);
        _confirmAcceptTimeAfterMove = Time.time + _moveToConfirmCooldownSeconds;
        return;
      }

      if (_optionHighlights == null || _optionHighlights.Length == 0)
      {
        return;
      }
      _currentIndex = Mathf.Min(_optionHighlights.Length - 1, _currentIndex + 1);
      RefreshHighlight();
      _confirmAcceptTimeAfterMove = Time.time + _moveToConfirmCooldownSeconds;
    }

    /// <summary>
    /// 選択中(Selecting)なら「認識した」状態変化を見せてからモーダルを開く処理を開始する
    /// (まだ何も確定しない)。モーダル中(ConfirmingModal)なら、カーソルが「決定」側なら
    /// 確定処理(状態変化→遷移)を開始、「戻る」側ならキャンセルしてモーダルを閉じる。
    /// </summary>
    public void Confirm()
    {
      if (_optionHighlights == null || _optionHighlights.Length == 0)
      {
        return;
      }

      if (IsInputBlocked || Time.time < _confirmAcceptTimeAfterMove)
      {
        return;
      }

      if (_mode == Mode.Selecting)
      {
        StartCoroutine(ShowRecognizedThenOpenModal());
        return;
      }

      // Mode.ConfirmingModal
      if (_modalCursorIndex == ModalConfirmIndex)
      {
        StartCoroutine(ConfirmAndTransition());
      }
      else
      {
        StartCoroutine(ShowCancelledThenCloseModal());
      }
    }

    /// <summary>
    /// モーダルの2回目のConfirm(戻る側): 戻るボタンの色を変えて状態変化を見せ、
    /// 少し待ってからモーダルを閉じて選択画面に戻る。この間は入力を受け付けない。
    /// </summary>
    private IEnumerator ShowCancelledThenCloseModal()
    {
      _mode = Mode.ClosingModal;

      if (_modalBackHighlight != null)
      {
        _modalBackHighlight.color = _confirmedFlashColor;
      }

      yield return new WaitForSeconds(_cancelledStateDelaySeconds);

      CloseModal();
    }

    /// <summary>
    /// 1回目のConfirm: 選択中のOptionを緑にして「認識した」ことを見せ、
    /// 少し待ってから確認モーダルを開く。この間は入力を受け付けない。
    /// </summary>
    private IEnumerator ShowRecognizedThenOpenModal()
    {
      _mode = Mode.PendingModal;

      if (_optionHighlights[_currentIndex] != null)
      {
        _optionHighlights[_currentIndex].color = _confirmedFlashColor;
      }

      yield return new WaitForSeconds(_preModalDelaySeconds);

      RefreshHighlight();
      OpenModal();
    }

    private void OpenModal()
    {
      _mode = Mode.ConfirmingModal;
      _modalInputAcceptTime = Time.time + _modalInputCooldownSeconds;
      SetModalCursor(ModalBackIndex);
      if (_modalPanel != null)
      {
        _modalPanel.SetActive(true);
      }
    }

    private void CloseModal()
    {
      _mode = Mode.Selecting;
      if (_modalPanel != null)
      {
        _modalPanel.SetActive(false);
      }
    }

    private void SetModalCursor(int index)
    {
      _modalCursorIndex = index;
      if (_modalBackHighlight != null)
      {
        _modalBackHighlight.color = (index == ModalBackIndex) ? _highlightColor : _normalColor;
      }
      if (_modalConfirmHighlight != null)
      {
        _modalConfirmHighlight.color = (index == ModalConfirmIndex) ? _highlightColor : _normalColor;
      }
    }

    /// <summary>
    /// モーダルの2回目のConfirm(決定側): 決定ボタンの色を変えて状態変化を見せ、
    /// 少し待ってから遷移先シーンをロードする。この間は入力を一切受け付けない。
    /// </summary>
    private IEnumerator ConfirmAndTransition()
    {
      _mode = Mode.TransitioningToScene;

      if (_confirmedLabel != null)
      {
        _confirmedLabel.text = $"決定: Option {_currentIndex + 1}";
      }
      if (_optionHighlights[_currentIndex] != null)
      {
        _optionHighlights[_currentIndex].color = _confirmedFlashColor;
      }
      if (_modalConfirmHighlight != null)
      {
        _modalConfirmHighlight.color = _confirmedFlashColor;
      }

      yield return new WaitForSeconds(_confirmedStateDelaySeconds);

      var sceneName = $"{_sceneNamePrefix}{_currentIndex + 1}";
      SceneManager.LoadScene(sceneName);
    }

    private void RefreshHighlight()
    {
      for (var i = 0; i < _optionHighlights.Length; i++)
      {
        if (_optionHighlights[i] != null)
        {
          _optionHighlights[i].color = (i == _currentIndex) ? _highlightColor : _normalColor;
        }
      }
    }
  }
}
