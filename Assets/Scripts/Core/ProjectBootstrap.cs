using LingoteRush.Audio;
using LingoteRush.Input;
using LingoteRush.Managers;
using UnityEngine;

namespace LingoteRush.Core
{
    public static class ProjectBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null)
            {
                return;
            }

            var systemsRoot = new GameObject("[LingoteRushSystems]");
            var gameManager = systemsRoot.AddComponent<GameManager>();
            var sceneFlowManager = systemsRoot.AddComponent<SceneFlowManager>();
            var audioManager = systemsRoot.AddComponent<AudioManager>();
            var inputManager = systemsRoot.AddComponent<InputManager>();

            gameManager.RegisterManagers(sceneFlowManager, audioManager, inputManager);
        }
    }
}
