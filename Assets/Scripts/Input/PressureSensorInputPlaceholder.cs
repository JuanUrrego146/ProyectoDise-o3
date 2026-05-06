using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Input
{
    public sealed class PressureSensorInputPlaceholder : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float simulatedImpactForce;
        [SerializeField] private bool useDebugKeys;
        [SerializeField, Min(0f)] private float weakImpact = 1f;
        [SerializeField, Min(0f)] private float mediumImpact = 2f;
        [SerializeField, Min(0f)] private float strongImpact = 3f;

        public event Action<float> ImpactReceived;

        public float CurrentImpactForce => simulatedImpactForce;

        public bool UseDebugKeys
        {
            get => useDebugKeys;
            set => useDebugKeys = value;
        }

        private void Update()
        {
            if (!useDebugKeys)
            {
                return;
            }

            if (TryGetDebugImpact(out var debugImpact))
            {
                SubmitImpact(debugImpact);
            }
        }

        public void SetSimulatedImpactForce(float value)
        {
            simulatedImpactForce = Mathf.Max(0f, value);
        }

        public void SubmitImpact(float value)
        {
            simulatedImpactForce = Mathf.Max(0f, value);
            ImpactReceived?.Invoke(simulatedImpactForce);
        }

        public void ConfigureDebugImpacts(float weak, float medium, float strong)
        {
            weakImpact = Mathf.Max(0f, weak);
            mediumImpact = Mathf.Max(weakImpact, medium);
            strongImpact = Mathf.Max(mediumImpact, strong);
        }

        public void ClearImpact()
        {
            simulatedImpactForce = 0f;
        }

        private bool TryGetDebugImpact(out float value)
        {
            value = simulatedImpactForce;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame)
                {
                    value = weakImpact;
                    return true;
                }

                if (keyboard.digit2Key.wasPressedThisFrame)
                {
                    value = mediumImpact;
                    return true;
                }

                if (keyboard.digit3Key.wasPressedThisFrame)
                {
                    value = strongImpact;
                    return true;
                }

                if (keyboard.digit0Key.wasPressedThisFrame)
                {
                    value = 0f;
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
            {
                value = weakImpact;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
            {
                value = mediumImpact;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
            {
                value = strongImpact;
                return true;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0))
            {
                value = 0f;
                return true;
            }
#endif

            return false;
        }
    }
}
