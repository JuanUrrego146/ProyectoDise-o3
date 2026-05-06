using System;
using UnityEngine;

namespace LingoteRush.Input
{
    public sealed class InputManager : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float impactForce;
        [SerializeField, Range(0f, 1f)] private float microphoneVolume;

        private PressureSensorInputPlaceholder pressureSensor;
        private MicrophoneInputPlaceholder microphoneInput;

        public event Action<float> ImpactReceived;

        public float ImpactForce => impactForce;

        public float MicrophoneVolume => microphoneVolume;

        public PressureSensorInputPlaceholder PressureSensor => pressureSensor;

        public MicrophoneInputPlaceholder MicrophoneInput => microphoneInput;

        private void Awake()
        {
            EnsureSources();
            RefreshState();
        }

        private void OnEnable()
        {
            BindSources();
            RefreshState();
        }

        private void OnDisable()
        {
            UnbindSources();
        }

        private void LateUpdate()
        {
            RefreshState();
        }

        public void SetImpactForce(float value)
        {
            if (pressureSensor == null)
            {
                return;
            }

            pressureSensor.SubmitImpact(value);
            RefreshState();
        }

        public void SetMicrophoneVolume(float value)
        {
            if (microphoneInput == null)
            {
                return;
            }

            microphoneInput.SetSimulatedVolume(value);
            RefreshState();
        }

        public void RefreshMicrophoneDevices()
        {
            microphoneInput?.RefreshDevices();
            RefreshState();
        }

        public bool SelectMicrophoneDevice(string deviceName)
        {
            return microphoneInput != null && microphoneInput.SelectDevice(deviceName);
        }

        private void EnsureSources()
        {
            pressureSensor = GetComponent<PressureSensorInputPlaceholder>();

            if (pressureSensor == null)
            {
                pressureSensor = gameObject.AddComponent<PressureSensorInputPlaceholder>();
            }

            microphoneInput = GetComponent<MicrophoneInputPlaceholder>();

            if (microphoneInput == null)
            {
                microphoneInput = gameObject.AddComponent<MicrophoneInputPlaceholder>();
            }
        }

        private void BindSources()
        {
            if (pressureSensor != null)
            {
                pressureSensor.ImpactReceived -= HandleImpactReceived;
                pressureSensor.ImpactReceived += HandleImpactReceived;
            }
        }

        private void UnbindSources()
        {
            if (pressureSensor != null)
            {
                pressureSensor.ImpactReceived -= HandleImpactReceived;
            }
        }

        private void HandleImpactReceived(float value)
        {
            impactForce = value;
            ImpactReceived?.Invoke(value);
        }

        private void RefreshState()
        {
            impactForce = pressureSensor != null ? pressureSensor.CurrentImpactForce : 0f;
            microphoneVolume = microphoneInput != null ? microphoneInput.CurrentVolume : 0f;
        }
    }
}
