using System.Collections;
using LingoteRush.Core;
using LingoteRush.Systems.Extraction;
using LingoteRush.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Managers
{
    public sealed class SceneFlowManager : MonoBehaviour
    {
        [SerializeField] private bool allowSpaceSceneCycling = true;

        public bool IsTransitioning { get; private set; }

        private void Update()
        {
            if (!allowSpaceSceneCycling || IsTransitioning)
            {
                return;
            }

            if (WasAdvanceScenePressed())
            {
                LoadNextMainScene();
            }
        }

        public void LoadNextMainScene()
        {
            var nextSceneName = SceneCatalog.GetNextMainScene(SceneManager.GetActiveScene().name);
            LoadScene(nextSceneName);
        }

        public void LoadScene(string sceneName)
        {
            if (IsTransitioning || string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"Scene '{sceneName}' is not available in Build Settings.");
                return;
            }

            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsTransitioning = true;

            EnsurePersistentBowlCarrier();
            PersistentCrucibleCarrier.PrepareActiveCarrierForSceneTransition();

            if (GameManager.HasInstance)
            {
                GameManager.Instance.SetGameState(GameState.SceneTransition);
            }

            var asyncOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (asyncOperation == null)
            {
                Debug.LogError($"Failed to start scene load for '{sceneName}'.");
                IsTransitioning = false;

                if (GameManager.HasInstance)
                {
                    GameManager.Instance.SetGameState(GameState.Ready);
                }

                yield break;
            }

            while (!asyncOperation.isDone)
            {
                yield return null;
            }

            IsTransitioning = false;

            if (GameManager.HasInstance)
            {
                GameManager.Instance.SetGameState(GameState.Ready);
            }
        }

        private void EnsurePersistentBowlCarrier()
        {
            if (PersistentCrucibleCarrier.ActiveCarrier != null)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();

            if (!string.Equals(activeScene.name, SceneCatalog.Extraction, System.StringComparison.Ordinal))
            {
                return;
            }

            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                var bowlTransform = FindChildByName(rootObject.transform, "Bowl 2");

                if (bowlTransform == null)
                {
                    continue;
                }

                PersistentCrucibleCarrier.EnsureRegistered(bowlTransform.gameObject);
                return;
            }

            Debug.LogWarning("Persistent Bowl 2 could not be registered because Bowl 2 was not found.");
        }

        private static Transform FindChildByName(Transform rootTransform, string targetName)
        {
            if (rootTransform == null)
            {
                return null;
            }

            if (rootTransform.name == targetName)
            {
                return rootTransform;
            }

            for (var index = 0; index < rootTransform.childCount; index++)
            {
                var match = FindChildByName(rootTransform.GetChild(index), targetName);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool WasAdvanceScenePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
