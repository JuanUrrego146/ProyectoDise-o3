using System;
using System.Collections.Generic;

namespace LingoteRush.Utilities
{
    public static class SceneCatalog
    {
        public const string Bootstrap = "Scene_Bootstrap";
        public const string Extraction = "Scene_Extraction";
        public const string Smelting = "Scene_Smelting";
        public const string FinalIngot = "Scene_FinalIngot";

        private static readonly string[] MainScenes =
        {
            Extraction,
            Smelting,
            FinalIngot
        };

        public static IReadOnlyList<string> GameplayScenes => MainScenes;

        public static int IndexOfMainScene(string sceneName)
        {
            for (var index = 0; index < MainScenes.Length; index++)
            {
                if (string.Equals(MainScenes[index], sceneName, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        public static string GetNextMainScene(string currentSceneName)
        {
            var currentIndex = IndexOfMainScene(currentSceneName);

            if (currentIndex < 0)
            {
                return MainScenes[0];
            }

            var nextIndex = (currentIndex + 1) % MainScenes.Length;
            return MainScenes[nextIndex];
        }
    }
}
