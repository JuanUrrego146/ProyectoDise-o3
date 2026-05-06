using System;
using LingoteRush.Audio;
using LingoteRush.Core;
using LingoteRush.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LingoteRush.Managers
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public static bool HasInstance => Instance != null;

        public SceneFlowManager SceneFlowManager { get; private set; }

        public AudioManager AudioManager { get; private set; }

        public InputManager InputManager { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.Bootstrapping;

        public string CurrentSceneName => SceneManager.GetActiveScene().name;

        public event Action<Scene, LoadSceneMode> SceneLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            gameObject.name = "[LingoteRushSystems]";
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        public void RegisterManagers(SceneFlowManager sceneFlowManager, AudioManager audioManager, InputManager inputManager)
        {
            SceneFlowManager = sceneFlowManager;
            AudioManager = audioManager;
            InputManager = inputManager;
        }

        public void SetGameState(GameState newState)
        {
            CurrentState = newState;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (CurrentState != GameState.SceneTransition)
            {
                CurrentState = GameState.Ready;
            }

            SceneLoaded?.Invoke(scene, loadSceneMode);
        }
    }
}
