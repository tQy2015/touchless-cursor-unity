using System.Collections.Generic;
using System.IO;
using System.Linq;
using HandsGesture.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandsGesture.Editor
{
  /// <summary>
  /// ArcadeGestureDemoの「決定」で遷移する行き先シーン(Scene-1〜Scene-N)を
  /// 再現性よく組み立てるビルダー。各シーンは中央に "Scene-N" と表示し、
  /// どのOptionを決定した結果ここに来たのかが一目でわかるようにするためのもの。
  /// 決定と同じジェスチャー(ピンチ)で ArcadeGestureDemo へ戻れる(下部に「戻る」表示)。
  ///
  /// 生成したシーンは EditorBuildSettings.scenes に登録する
  /// (SceneManager.LoadScene(name) はビルド設定に載っているシーンしかロードできないため)。
  ///
  /// メニュー: Tools/HandsGesture/Build Destination Scenes (Scene-1..N)
  /// </summary>
  public static class DestinationScenesBuilder
  {
    private const string ScenesDir = "Assets/HandsGesture/Scenes";
    private const string ArcadeDemoScenePath = ScenesDir + "/ArcadeGestureDemo.unity";
    private const string ArcadeDemoSceneName = "ArcadeGestureDemo";
    private const string ConfigAssetPath = "Assets/HandsGesture/GestureConfig.asset";
    private const string BootstrapPrefabPath = "Assets/MediaPipeUnity/Samples/Resources/Bootstrap.prefab";
    private const int SceneCount = 5;

    [MenuItem("Tools/HandsGesture/Build Destination Scenes (Scene-1..N)")]
    public static void Build()
    {
      Directory.CreateDirectory(ScenesDir);

      var config = AssetDatabase.LoadAssetAtPath<GestureConfig>(ConfigAssetPath);
      var bootstrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPrefabPath);
      if (config == null || bootstrapPrefab == null)
      {
        Debug.LogError($"{nameof(DestinationScenesBuilder)}: GestureConfigまたはBootstrap prefabが見つかりません。" +
          "先に ArcadeGestureDemoSceneBuilder.Build() を実行してください");
        return;
      }

      var destinationScenePaths = new List<string>();

      for (var i = 1; i <= SceneCount; i++)
      {
        var sceneName = $"Scene-{i}";
        var scenePath = $"{ScenesDir}/{sceneName}.unity";
        BuildOneScene(sceneName, scenePath, config, bootstrapPrefab);
        destinationScenePaths.Add(scenePath);
      }

      RegisterBuildSettingsScenes(destinationScenePaths);

      Debug.Log($"Destination scenes built: {string.Join(", ", destinationScenePaths)}");
    }

    private static void BuildOneScene(string sceneName, string scenePath, GestureConfig config, GameObject bootstrapPrefab)
    {
      var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

      var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
      var camera = cameraGo.GetComponent<Camera>();
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
      cameraGo.tag = "MainCamera";

      var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

      var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
      var canvas = canvasGo.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      var scaler = canvasGo.GetComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1280, 720);

      var labelGo = new GameObject("SceneLabel", typeof(RectTransform));
      labelGo.transform.SetParent(canvasGo.transform, false);
      var labelRect = labelGo.GetComponent<RectTransform>();
      labelRect.anchorMin = new Vector2(0.5f, 0.5f);
      labelRect.anchorMax = new Vector2(0.5f, 0.5f);
      labelRect.anchoredPosition = Vector2.zero;
      labelRect.sizeDelta = new Vector2(800, 200);
      var label = labelGo.AddComponent<Text>();
      label.text = sceneName;
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.alignment = TextAnchor.MiddleCenter;
      label.fontSize = 96;
      label.color = Color.white;

      // --- 「戻る」表示(下部)。決定と同じジェスチャー(ピンチ)で ArcadeGestureDemo へ戻る ---
      var backLabelGo = new GameObject("BackLabel", typeof(RectTransform));
      backLabelGo.transform.SetParent(canvasGo.transform, false);
      var backLabelRect = backLabelGo.GetComponent<RectTransform>();
      backLabelRect.anchorMin = new Vector2(0.5f, 0f);
      backLabelRect.anchorMax = new Vector2(0.5f, 0f);
      backLabelRect.pivot = new Vector2(0.5f, 0f);
      backLabelRect.anchoredPosition = new Vector2(0, 60);
      backLabelRect.sizeDelta = new Vector2(400, 60);
      var backLabel = backLabelGo.AddComponent<Text>();
      backLabel.text = "つまむジェスチャーで 戻る";
      backLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      backLabel.alignment = TextAnchor.MiddleCenter;
      backLabel.fontSize = 28;
      backLabel.color = new Color(1f, 1f, 1f, 0.8f);

      // --- ジェスチャー検出(この画面では決定=ピンチのみを「戻る」に割り当てる) ---
      var backNavGo = new GameObject("BackNavigation", typeof(BackNavigation));
      var backNav = backNavGo.GetComponent<BackNavigation>();
      var backNavSo = new SerializedObject(backNav);
      backNavSo.FindProperty("_targetSceneName").stringValue = ArcadeDemoSceneName;
      backNavSo.FindProperty("_statusLabel").objectReferenceValue = backLabel;
      backNavSo.ApplyModifiedPropertiesWithoutUndo();

      var runnerGo = new GameObject("HandsGestureRunner", typeof(HandsGestureRunner));
      var runner = runnerGo.GetComponent<HandsGestureRunner>();
      var runnerSo = new SerializedObject(runner);
      runnerSo.FindProperty("_config").objectReferenceValue = config;
      runnerSo.FindProperty("_bootstrapPrefab").objectReferenceValue = bootstrapPrefab;
      runnerSo.ApplyModifiedPropertiesWithoutUndo();

      UnityEventTools.AddPersistentListener(runner.onConfirm, backNav.GoBack);

      Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);
      EditorSceneManager.SaveScene(scene, scenePath);
    }

    /// <summary>
    /// ArcadeGestureDemo.unity(先頭・起動シーン)+ Scene-1..N を Build Settings に登録する。
    /// 既存の登録(このモジュール外のシーン等)は保持し、重複追加はしない。
    /// </summary>
    private static void RegisterBuildSettingsScenes(List<string> destinationScenePaths)
    {
      var existingPaths = EditorBuildSettings.scenes.Select(s => s.path).ToList();

      var orderedPaths = new List<string>();
      if (File.Exists(ArcadeDemoScenePath))
      {
        orderedPaths.Add(ArcadeDemoScenePath);
      }
      orderedPaths.AddRange(destinationScenePaths);

      foreach (var path in existingPaths)
      {
        if (!orderedPaths.Contains(path))
        {
          orderedPaths.Add(path);
        }
      }

      EditorBuildSettings.scenes = orderedPaths
        .Select(path => new EditorBuildSettingsScene(path, true))
        .ToArray();
    }
  }
}
