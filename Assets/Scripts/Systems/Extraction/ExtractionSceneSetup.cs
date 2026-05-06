using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class ExtractionSceneSetup : MonoBehaviour
    {
        [Header("Manual References")]
        [SerializeField] private ExtractionController extractionController;
        [SerializeField] private GoldNuggetSpawner nuggetSpawner;
        [SerializeField] private CrucibleController crucible;

        private void Awake()
        {
            if (extractionController == null)
            {
                Debug.LogWarning(
                    "ExtractionSceneSetup is now passive. Assign an ExtractionController from the Inspector or remove this component.");
                return;
            }

            extractionController.SetReferences(nuggetSpawner, crucible);
        }
    }
}
