using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Systems.Extraction
{
    public sealed class ExtractionDebugKeyboardInput : MonoBehaviour
    {
        [SerializeField] private ExtractionController extractionController;
        [SerializeField, Min(0f)] private float weakImpactForce = 1f;
        [SerializeField, Min(0f)] private float mediumImpactForce = 2f;
        [SerializeField, Min(0f)] private float strongImpactForce = 3f;

        private void Update()
        {
            if (extractionController == null)
            {
                return;
            }

            if (TryGetImpactForce(out var impactForce))
            {
                extractionController.GenerateFromImpactForce(impactForce);
            }
        }

        private bool TryGetImpactForce(out float impactForce)
        {
            impactForce = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    impactForce = weakImpactForce;
                    return true;
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    impactForce = mediumImpactForce;
                    return true;
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    impactForce = strongImpactForce;
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                impactForce = weakImpactForce;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                impactForce = mediumImpactForce;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                impactForce = strongImpactForce;
                return true;
            }
#endif

            return false;
        }
    }
}
