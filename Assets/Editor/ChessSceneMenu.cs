using System.IO;
using System.Linq;
using BciChess.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BciChess.EditorTools
{
    /// <summary>Creates the playable chess scene: a camera plus a ChessGameBootstrap that builds everything else.</summary>
    public static class ChessSceneMenu
    {
        private const string ScenePath = "Assets/Scenes/Chess.unity";

        [MenuItem("BCI Chess/Create Chess Scene")]
        public static void CreateChessScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("BCI Chess", $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x1B, 0x1F, 0x27, 0xFF);
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            var bootstrap = new GameObject("ChessGame").AddComponent<ChessGameBootstrap>();
            AssignUnicornPrefab(bootstrap);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"Created {ScenePath}. Press Play to start.");
        }

        /// <summary>Points every ChessGameBootstrap in the open scene at the g.tec ERP prefab used in Unicorn mode.</summary>
        [MenuItem("BCI Chess/Assign Unicorn BCI Prefab")]
        public static void AssignUnicornPrefabInScene()
        {
            var bootstraps = Object.FindObjectsOfType<ChessGameBootstrap>();
            if (bootstraps.Length == 0)
            {
                EditorUtility.DisplayDialog("BCI Chess", "No ChessGameBootstrap in the open scene. Open the Chess scene first.", "OK");
                return;
            }

            int assigned = bootstraps.Count(AssignUnicornPrefab);
            if (assigned > 0)
                EditorSceneManager.MarkSceneDirty(bootstraps[0].gameObject.scene);
            Debug.Log($"Assigned the Unicorn ERP prefab to {assigned} bootstrap(s). Save the scene to keep it.");
        }

        private static bool AssignUnicornPrefab(ChessGameBootstrap bootstrap)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnicornPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"g.tec ERP prefab not found at {UnicornPrefabPath}.");
                return false;
            }

            var serialized = new SerializedObject(bootstrap);
            var property = serialized.FindProperty("bci.unicorn.erpPrefab");
            if (property == null)
            {
                Debug.LogWarning("ChessGameBootstrap has no bci.unicorn.erpPrefab field.");
                return false;
            }
            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedProperties();
            return true;
        }

        private const string UnicornPrefabPath = "Assets/g.tec/Unity Interface/Prefabs/BCI/BCI Visual ERP 2D.prefab";

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
