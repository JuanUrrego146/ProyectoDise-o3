using System;
using System.Collections.Generic;
using UnityEngine;

namespace LingoteRush.Input
{
    public sealed class MicrophoneInputPlaceholder : MonoBehaviour
    {
        [SerializeField] private string selectedDevice;
        [SerializeField] private string[] availableDevices = Array.Empty<string>();
        [SerializeField, Range(0f, 1f)] private float simulatedVolume;
        [SerializeField, Min(0.1f)] private float sensitivity = 1f;
        [SerializeField] private bool useSimulatedVolume = true;

        public string SelectedDevice => selectedDevice;

        public IReadOnlyList<string> AvailableDevices => availableDevices;

        public float CurrentVolume => Mathf.Clamp01((useSimulatedVolume ? simulatedVolume : 0f) * sensitivity);

        private void Awake()
        {
            RefreshDevices();
        }

        public void RefreshDevices()
        {
            availableDevices = Microphone.devices ?? Array.Empty<string>();

            if (string.IsNullOrEmpty(selectedDevice) && availableDevices.Length > 0)
            {
                selectedDevice = availableDevices[0];
            }
        }

        public bool SelectDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            RefreshDevices();

            foreach (var device in availableDevices)
            {
                if (string.Equals(device, deviceName, StringComparison.Ordinal))
                {
                    selectedDevice = device;
                    return true;
                }
            }

            return false;
        }

        public void SetSimulatedVolume(float value)
        {
            simulatedVolume = Mathf.Clamp01(value);
        }

        public void SetSensitivity(float value)
        {
            sensitivity = Mathf.Max(0.1f, value);
        }
    }
}
