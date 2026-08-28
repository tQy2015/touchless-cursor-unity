using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandsGesture.Editor
{
    public static class SceneIntegrityCheck
    {
        private static readonly string[] Scenes =
        {
            "Assets/HandsGesture/Scenes/ArcadeGestureDemo.unity",
            "Assets/HandsGesture/Scenes/Scene-1.unity",
            "Assets/HandsGesture/Scenes/Scene-2.unity",
            "Assets/HandsGesture/Scenes/Scene-3.unity",
            "Assets/HandsGesture/Scenes/Scene-4.unity",
            "Assets/HandsGesture/Scenes/Scene-5.unity",
        };

        public static void Run()
        {
            var missingCount = 0;
            foreach (var scenePath in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var go = t.gameObject;
                        if (go.name.Contains("Missing Prefab") || PrefabUtility.GetPrefabInstanceStatus(go) == PrefabInstanceStatus.MissingAsset)
                        {
                            Debug.LogError($"[SceneIntegrityCheck] MISSING PREFAB in {scenePath}: {go.name}");
                            missingCount++;
                        }
                    }
                    var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var b in behaviours)
                    {
                        if (b == null)
                        {
                            Debug.LogError($"[SceneIntegrityCheck] MISSING SCRIPT in {scenePath}");
                            missingCount++;
                        }
                    }
                }
                Debug.Log($"[SceneIntegrityCheck] checked {scenePath}");
            }
            Debug.Log($"[SceneIntegrityCheck] DONE missingCount={missingCount}");
        }
    }
}
