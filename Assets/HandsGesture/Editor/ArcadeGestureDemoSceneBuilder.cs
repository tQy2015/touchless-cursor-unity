using System.IO;
using HandsGesture;
using HandsGesture.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HandsGesture.Editor
{
  /// <summary>
  /// ArcadeGestureDemo.unity を毎回同じ構成で再現性よく組み立てるためのビルダー。
  /// GameObject配置・Inspector配線を手作業でなくコードで行うことで、
  /// シーン構成をレビュー可能なC#として残す。
  ///
  /// メニュー: Tools/HandsGesture/Build Arcade Gesture Demo Scene
  /// </summary>
  public static class ArcadeGestureDemoSceneBuilder
  {
    private const string ConfigAssetPath = "Assets/HandsGesture/GestureConfig.asset";
    private const string ScenePath = "Assets/HandsGesture/Scenes/ArcadeGestureDemo.unity";
    private const string BootstrapPrefabPath = "Assets/MediaPipeUnity/Samples/Resources/Bootstrap.prefab";
    private const int OptionCount = 5;

    [MenuItem("Tools/HandsGesture/Build Arcade Gesture Demo Scene")]
    public static void Build()
    {
      var config = LoadOrCreateConfig();
      var bootstrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
      if (bootstrapPrefab == null)
      {
        Debug.LogError($"Bootstrap prefab not found at {BootstrapPrefabPath}. homuler/MediaPipeUnityPlugin が正しくインポートされているか確認してください");
        return;
      }

      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

      // Game View に何も描画されない("No cameras rendering")のを避けるための最小限のカメラ。
      // WebCamTexture(MediaPipe入力)とは別物 — こちらは画面表示用のUnityカメラ。
      var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
      var mainCamera = cameraGo.GetComponent<Camera>();
      mainCamera.clearFlags = CameraClearFlags.SolidColor;
      mainCamera.backgroundColor = Color.black;
      cameraGo.tag = "MainCamera";

      var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

      var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      var canvas = canvasGo.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1280, 720);

      // --- 選択肢の横一列 ---
      var rowGo = CreateUIObject("OptionRow", canvasGo.transform);
      var rowRect = rowGo.GetComponent<RectTransform>();
      rowRect.anchorMin = new Vector2(0.5f, 0.5f);
      rowRect.anchorMax = new Vector2(0.5f, 0.5f);
      rowRect.anchoredPosition = new Vector2(0, 40);
      rowRect.sizeDelta = new Vector2(1000, 160);
      var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
      rowLayout.spacing = 24;
      rowLayout.childAlignment = TextAnchor.MiddleCenter;
      rowLayout.childForceExpandHeight = false;
      rowLayout.childForceExpandWidth = false;

      var optionImages = new Image[OptionCount];
      for (var i = 0; i < OptionCount; i++)
      {
        var optionGo = CreateUIObject($"Option{i + 1}", rowGo.transform);
        var image = optionGo.AddComponent<Image>();
        image.color = Color.white;
        var layoutElement = optionGo.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 160;
        layoutElement.preferredHeight = 160;
        optionImages[i] = image;

        var textGo = CreateUIObject("Label", optionGo.transform);
        var text = textGo.AddComponent<Text>();
        text.text = $"Option {i + 1}";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
      }

      // --- 決定結果ラベル ---
      var confirmedLabelGo = CreateUIObject("ConfirmedLabel", canvasGo.transform);
      var confirmedRect = confirmedLabelGo.GetComponent<RectTransform>();
      confirmedRect.anchorMin = new Vector2(0.5f, 0.5f);
      confirmedRect.anchorMax = new Vector2(0.5f, 0.5f);
      confirmedRect.anchoredPosition = new Vector2(0, -160);
      confirmedRect.sizeDelta = new Vector2(600, 60);
      var confirmedText = confirmedLabelGo.AddComponent<Text>();
      confirmedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      confirmedText.alignment = TextAnchor.MiddleCenter;
      confirmedText.fontSize = 28;
      confirmedText.color = Color.black;
      confirmedText.text = "";

      // --- 決定確認モーダル(左=戻る/右=決定の2択。誤爆対策の二段階確認) ---
      var modalPanelGo = CreateUIObject("ConfirmationModal", canvasGo.transform);
      var modalPanelRect = modalPanelGo.GetComponent<RectTransform>();
      modalPanelRect.anchorMin = Vector2.zero;
      modalPanelRect.anchorMax = Vector2.one;
      modalPanelRect.offsetMin = Vector2.zero;
      modalPanelRect.offsetMax = Vector2.zero;
      var modalBackdrop = modalPanelGo.AddComponent<Image>();
      modalBackdrop.color = new Color(0f, 0f, 0f, 0.6f);

      // ダイアログ本体の板(全画面フェードの上に敷く、選択肢2つを収める背景プレート)
      var modalPlateGo = CreateUIObject("Plate", modalPanelGo.transform);
      var modalPlateRect = modalPlateGo.GetComponent<RectTransform>();
      modalPlateRect.anchorMin = new Vector2(0.5f, 0.5f);
      modalPlateRect.anchorMax = new Vector2(0.5f, 0.5f);
      modalPlateRect.anchoredPosition = Vector2.zero;
      modalPlateRect.sizeDelta = new Vector2(600, 260);
      var modalPlateImage = modalPlateGo.AddComponent<Image>();
      modalPlateImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

      var modalRowGo = CreateUIObject("ModalOptionRow", modalPanelGo.transform);
      var modalRowRect = modalRowGo.GetComponent<RectTransform>();
      modalRowRect.anchorMin = new Vector2(0.5f, 0.5f);
      modalRowRect.anchorMax = new Vector2(0.5f, 0.5f);
      modalRowRect.anchoredPosition = Vector2.zero;
      modalRowRect.sizeDelta = new Vector2(500, 160);
      var modalRowLayout = modalRowGo.AddComponent<HorizontalLayoutGroup>();
      modalRowLayout.spacing = 40;
      modalRowLayout.childAlignment = TextAnchor.MiddleCenter;
      modalRowLayout.childForceExpandHeight = false;
      modalRowLayout.childForceExpandWidth = false;

      var modalBackGo = CreateUIObject("BackOption", modalRowGo.transform);
      var modalBackImage = modalBackGo.AddComponent<Image>();
      modalBackImage.color = Color.white;
      var modalBackLayout = modalBackGo.AddComponent<LayoutElement>();
      modalBackLayout.preferredWidth = 200;
      modalBackLayout.preferredHeight = 140;
      CreateModalOptionLabel(modalBackGo.transform, "戻る");

      var modalConfirmGo = CreateUIObject("ConfirmOption", modalRowGo.transform);
      var modalConfirmImage = modalConfirmGo.AddComponent<Image>();
      modalConfirmImage.color = Color.white;
      var modalConfirmLayout = modalConfirmGo.AddComponent<LayoutElement>();
      modalConfirmLayout.preferredWidth = 200;
      modalConfirmLayout.preferredHeight = 140;
      CreateModalOptionLabel(modalConfirmGo.transform, "決定");

      // --- デバッグHUD(左上) ---
      var debugLabelGo = CreateUIObject("DebugLabel", canvasGo.transform);
      var debugRect = debugLabelGo.GetComponent<RectTransform>();
      debugRect.anchorMin = new Vector2(0, 1);
      debugRect.anchorMax = new Vector2(0, 1);
      debugRect.pivot = new Vector2(0, 1);
      debugRect.anchoredPosition = new Vector2(16, -16);
      debugRect.sizeDelta = new Vector2(320, 100);
      var debugText = debugLabelGo.AddComponent<Text>();
      debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      debugText.alignment = TextAnchor.UpperLeft;
      debugText.fontSize = 18;
      debugText.color = Color.black;
      debugText.text = "hand: none";

      // --- カメラパススルー + ゾーン帯 + カーソル(開発/調整用オーバーレイ。左上、DebugHudの下) ---
      const float overlayWidth = 320f;
      const float overlayHeight = 240f;

      var overlayPanelGo = CreateUIObject("HandRecognitionOverlay", canvasGo.transform);
      var overlayPanelRect = overlayPanelGo.GetComponent<RectTransform>();
      overlayPanelRect.anchorMin = new Vector2(0, 1);
      overlayPanelRect.anchorMax = new Vector2(0, 1);
      overlayPanelRect.pivot = new Vector2(0, 1);
      overlayPanelRect.anchoredPosition = new Vector2(16, -132);
      overlayPanelRect.sizeDelta = new Vector2(overlayWidth, overlayHeight);

      var passthroughGo = CreateUIObject("Passthrough", overlayPanelGo.transform);
      var passthroughRect = passthroughGo.GetComponent<RectTransform>();
      passthroughRect.anchorMin = Vector2.zero;
      passthroughRect.anchorMax = Vector2.one;
      passthroughRect.offsetMin = Vector2.zero;
      passthroughRect.offsetMax = Vector2.zero;
      var passthroughImage = passthroughGo.AddComponent<RawImage>();
      passthroughImage.color = Color.white;

      var leftBandGo = CreateUIObject("LeftZoneBand", overlayPanelGo.transform);
      var leftBandRect = leftBandGo.GetComponent<RectTransform>();
      leftBandGo.AddComponent<Image>();

      var centerBandGo = CreateUIObject("CenterZoneBand", overlayPanelGo.transform);
      var centerBandRect = centerBandGo.GetComponent<RectTransform>();
      centerBandGo.AddComponent<Image>();

      var rightBandGo = CreateUIObject("RightZoneBand", overlayPanelGo.transform);
      var rightBandRect = rightBandGo.GetComponent<RectTransform>();
      rightBandGo.AddComponent<Image>();

      var cursorGo = CreateUIObject("HandCursor", overlayPanelGo.transform);
      var cursorRect = cursorGo.GetComponent<RectTransform>();
      cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
      cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
      cursorRect.sizeDelta = new Vector2(24, 24);
      var cursorImage = cursorGo.AddComponent<Image>();
      cursorImage.color = Color.yellow;

      var overlay = overlayPanelGo.AddComponent<HandRecognitionOverlay>();
      var overlaySo = new SerializedObject(overlay);
      overlaySo.FindProperty("_passthrough").objectReferenceValue = passthroughImage;
      overlaySo.FindProperty("_passthroughRect").objectReferenceValue = passthroughRect;
      overlaySo.FindProperty("_leftZoneBand").objectReferenceValue = leftBandRect;
      overlaySo.FindProperty("_centerZoneBand").objectReferenceValue = centerBandRect;
      overlaySo.FindProperty("_rightZoneBand").objectReferenceValue = rightBandRect;
      overlaySo.FindProperty("_cursor").objectReferenceValue = cursorRect;
      overlaySo.FindProperty("_cursorImage").objectReferenceValue = cursorImage;
      // _runner はこの後 HandsGestureRunner 作成時にまとめて設定する
      overlaySo.ApplyModifiedPropertiesWithoutUndo();

      // --- SelectionCursorUI(選択ロジックの受け皿。Canvas上に置く) ---
      var selectionCursor = canvasGo.AddComponent<SelectionCursorUI>();
      var selectionSo = new SerializedObject(selectionCursor);
      var highlightsProp = selectionSo.FindProperty("_optionHighlights");
      highlightsProp.arraySize = optionImages.Length;
      for (var i = 0; i < optionImages.Length; i++)
      {
        highlightsProp.GetArrayElementAtIndex(i).objectReferenceValue = optionImages[i];
      }
      selectionSo.FindProperty("_confirmedLabel").objectReferenceValue = confirmedText;
      selectionSo.FindProperty("_modalPanel").objectReferenceValue = modalPanelGo;
      selectionSo.FindProperty("_modalBackHighlight").objectReferenceValue = modalBackImage;
      selectionSo.FindProperty("_modalConfirmHighlight").objectReferenceValue = modalConfirmImage;
      selectionSo.ApplyModifiedPropertiesWithoutUndo();

      // --- ExternalWebCamSelector: Runner より Hierarchy 上で先に置く(実行順の保険はDefaultExecutionOrderにもあり) ---
      var selectorGo = new GameObject("ExternalWebCamSelector", typeof(ExternalWebCamSelector));
      var selector = selectorGo.GetComponent<ExternalWebCamSelector>();
      var selectorSo = new SerializedObject(selector);
      selectorSo.FindProperty("_config").objectReferenceValue = config;
      selectorSo.ApplyModifiedPropertiesWithoutUndo();

      // --- HandsGestureRunner ---
      var runnerGo = new GameObject("HandsGestureRunner", typeof(HandsGestureRunner));
      var runner = runnerGo.GetComponent<HandsGestureRunner>();
      var runnerSo = new SerializedObject(runner);
      runnerSo.FindProperty("_config").objectReferenceValue = config;
      runnerSo.FindProperty("_bootstrapPrefab").objectReferenceValue = bootstrapPrefab;
      runnerSo.ApplyModifiedPropertiesWithoutUndo();

      overlaySo.FindProperty("_runner").objectReferenceValue = runner;
      overlaySo.ApplyModifiedPropertiesWithoutUndo();

      // --- DebugHud ---
      var debugHudGo = new GameObject("DebugHud", typeof(DebugHud));
      var debugHud = debugHudGo.GetComponent<DebugHud>();
      var debugHudSo = new SerializedObject(debugHud);
      debugHudSo.FindProperty("_runner").objectReferenceValue = runner;
      debugHudSo.FindProperty("_label").objectReferenceValue = debugText;
      debugHudSo.ApplyModifiedPropertiesWithoutUndo();

      // --- UnityEvent配線: HandsGestureRunner.onMoveLeft/onMoveRight/onConfirm -> SelectionCursorUI ---
      UnityEventTools.AddPersistentListener(runner.onMoveLeft, selectionCursor.MoveLeft);
      UnityEventTools.AddPersistentListener(runner.onMoveRight, selectionCursor.MoveRight);
      UnityEventTools.AddPersistentListener(runner.onConfirm, selectionCursor.Confirm);

      Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
      EditorSceneManager.SaveScene(scene, ScenePath);
      AssetDatabase.SaveAssets();

      Debug.Log($"ArcadeGestureDemo scene built and saved to {ScenePath}");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      return go;
    }

    private static void CreateModalOptionLabel(Transform parent, string label)
    {
      var textGo = CreateUIObject("Label", parent);
      var text = textGo.AddComponent<Text>();
      text.text = label;
      text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      text.alignment = TextAnchor.MiddleCenter;
      text.fontSize = 24;
      text.color = Color.black;
      var textRect = textGo.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;
    }

    private static GestureConfig LoadOrCreateConfig()
    {
      var existing = AssetDatabase.LoadAssetAtPath<GestureConfig>(ConfigAssetPath);
      if (existing != null)
      {
        return existing;
      }

      var config = ScriptableObject.CreateInstance<GestureConfig>();
      Directory.CreateDirectory(Path.GetDirectoryName(ConfigAssetPath)!);
      AssetDatabase.CreateAsset(config, ConfigAssetPath);
      return config;
    }
  }
}
