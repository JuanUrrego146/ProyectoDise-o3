using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LingoteRush.Systems.Smelting
{
    public sealed class MicrophoneInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Text statusText;

        [Header("Microphone Settings")]
        [SerializeField, Min(0.1f)] private float inputSensitivity = 1.25f;
        [SerializeField, Range(0f, 0.99f)] private float smoothing = 0.85f;
        [SerializeField, Min(0f)] private float noiseGate = 0.0025f;
        [SerializeField, Min(0.0001f)] private float maxExpectedLevel = 0.05f;
        [SerializeField, Min(64)] private int sampleWindow = 256;
        [SerializeField, Min(1)] private int recordingLengthSeconds = 1;
        [SerializeField, Min(8000)] private int sampleRate = 44100;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField, Min(0.1f)] private float debugLogInterval = 0.35f;

        [Header("Runtime Debug")]
        [SerializeField] private string selectedDevice;
        [SerializeField] private float currentRawLevel;
        [SerializeField] private float currentNormalizedLevel;
        [SerializeField] private bool hasValidMicrophone;

        private string[] availableDevices = Array.Empty<string>();
        private AudioClip microphoneClip;
        private float[] sampleBuffer = Array.Empty<float>();
        private float nextDebugLogTime;

        public float CurrentRawLevel => currentRawLevel;

        public float CurrentNormalizedLevel => currentNormalizedLevel;

        public bool HasValidMicrophone => hasValidMicrophone;

        private void Awake()
        {
            AutoResolveUiReferences();
            WarnAboutMissingReferences();
            ConfigureDropdown();
            ConfigureSensitivitySlider();
            RefreshDevices();
        }

        private void OnEnable()
        {
            if (availableDevices.Length == 0)
            {
                RefreshDevices();
            }
            else if (!string.IsNullOrWhiteSpace(selectedDevice))
            {
                StartCapture(selectedDevice);
            }
        }

        private void OnDisable()
        {
            StopCapture();
        }

        private void WarnAboutMissingReferences()
        {
            if (deviceDropdown == null)
            {
                Debug.LogWarning("Missing DeviceDropdown reference", this);
            }

            if (sensitivitySlider == null)
            {
                Debug.LogWarning("Missing SensitivitySlider reference", this);
            }

            if (statusText == null)
            {
                Debug.LogWarning("Missing StatusText reference", this);
            }
        }

        private void AutoResolveUiReferences()
        {
            if (deviceDropdown == null)
            {
                deviceDropdown = FindComponentByName<Dropdown>("DeviceDropdown");
            }

            if (sensitivitySlider == null)
            {
                sensitivitySlider = FindComponentByName<Slider>("SensitivitySlider");
            }

            if (statusText == null)
            {
                statusText = FindComponentByName<Text>("StatusText");
            }
        }

        private void Update()
        {
            UpdateInputLevel();
        }

        public void RefreshDevices()
        {
            availableDevices = Microphone.devices ?? Array.Empty<string>();
            PopulateDropdown();

            if (availableDevices.Length == 0)
            {
                selectedDevice = string.Empty;
                hasValidMicrophone = false;
                currentRawLevel = 0f;
                currentNormalizedLevel = 0f;
                SetStatus("Esperando microfono");
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedDevice) || Array.IndexOf(availableDevices, selectedDevice) < 0)
            {
                selectedDevice = availableDevices[0];
            }

            ApplyDropdownSelection();
            StartCapture(selectedDevice);
        }

        public bool SelectDevice(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                return false;
            }

            RefreshDevices();

            var deviceIndex = Array.IndexOf(availableDevices, deviceName);

            if (deviceIndex < 0)
            {
                return false;
            }

            selectedDevice = availableDevices[deviceIndex];
            ApplyDropdownSelection();
            StartCapture(selectedDevice);
            return true;
        }

        private void ConfigureDropdown()
        {
            if (deviceDropdown == null)
            {
                return;
            }

            deviceDropdown.onValueChanged.RemoveListener(HandleDropdownValueChanged);
            deviceDropdown.onValueChanged.AddListener(HandleDropdownValueChanged);
        }

        private void ConfigureSensitivitySlider()
        {
            if (sensitivitySlider == null)
            {
                return;
            }

            sensitivitySlider.onValueChanged.RemoveListener(HandleSensitivityChanged);
            sensitivitySlider.SetValueWithoutNotify(inputSensitivity);
            sensitivitySlider.onValueChanged.AddListener(HandleSensitivityChanged);
        }

        private void PopulateDropdown()
        {
            if (deviceDropdown == null)
            {
                return;
            }

            deviceDropdown.ClearOptions();

            var options = new System.Collections.Generic.List<Dropdown.OptionData>(availableDevices.Length);

            foreach (var device in availableDevices)
            {
                options.Add(new Dropdown.OptionData(device));
            }

            deviceDropdown.AddOptions(options);
        }

        private void ApplyDropdownSelection()
        {
            if (deviceDropdown == null || availableDevices.Length == 0)
            {
                return;
            }

            var selectedIndex = Mathf.Max(0, Array.IndexOf(availableDevices, selectedDevice));
            deviceDropdown.SetValueWithoutNotify(selectedIndex);
        }

        private void HandleDropdownValueChanged(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= availableDevices.Length)
            {
                return;
            }

            selectedDevice = availableDevices[selectedIndex];
            StartCapture(selectedDevice);
        }

        private void HandleSensitivityChanged(float newSensitivity)
        {
            inputSensitivity = Mathf.Max(0.1f, newSensitivity);
        }

        private void StartCapture(string deviceName)
        {
            StopCapture();

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                hasValidMicrophone = false;
                SetStatus("Esperando microfono");
                return;
            }

            microphoneClip = Microphone.Start(deviceName, true, recordingLengthSeconds, sampleRate);

            if (microphoneClip == null)
            {
                hasValidMicrophone = false;
                currentRawLevel = 0f;
                currentNormalizedLevel = 0f;
                SetStatus("No se pudo iniciar la captura del microfono.");
                return;
            }

            selectedDevice = deviceName;
            hasValidMicrophone = true;
            var clipSampleCount = microphoneClip.samples > 0 ? microphoneClip.samples : sampleWindow;
            sampleBuffer = new float[Mathf.Clamp(sampleWindow, 64, clipSampleCount)];
            nextDebugLogTime = Time.unscaledTime;
            SetStatus($"Microfono activo: {selectedDevice}");
        }

        private void StopCapture()
        {
            if (!string.IsNullOrWhiteSpace(selectedDevice) && Microphone.IsRecording(selectedDevice))
            {
                Microphone.End(selectedDevice);
            }

            microphoneClip = null;
            hasValidMicrophone = false;
        }

        private void UpdateInputLevel()
        {
            if (!hasValidMicrophone || microphoneClip == null || string.IsNullOrWhiteSpace(selectedDevice))
            {
                currentRawLevel = 0f;
                currentNormalizedLevel = Mathf.Lerp(currentNormalizedLevel, 0f, 1f - smoothing);
                return;
            }

            if (!Microphone.IsRecording(selectedDevice))
            {
                hasValidMicrophone = false;
                currentRawLevel = 0f;
                currentNormalizedLevel = 0f;
                SetStatus("La captura del microfono se detuvo.");
                return;
            }

            var microphonePosition = Microphone.GetPosition(selectedDevice);
            var clipSamples = microphoneClip.samples;

            if (microphonePosition < 0 || clipSamples <= 0)
            {
                currentRawLevel = 0f;
                currentNormalizedLevel = Mathf.Lerp(currentNormalizedLevel, 0f, 1f - smoothing);
                return;
            }

            var readableSamples = Mathf.Clamp(sampleWindow, 64, clipSamples);

            if (sampleBuffer.Length != readableSamples)
            {
                sampleBuffer = new float[readableSamples];
            }

            if (microphonePosition == 0)
            {
                currentRawLevel = 0f;
                currentNormalizedLevel = Mathf.Lerp(currentNormalizedLevel, 0f, 1f - smoothing);
                return;
            }

            var readStart = microphonePosition - readableSamples;

            if (readStart < 0)
            {
                readStart += clipSamples;
            }

            if (!microphoneClip.GetData(sampleBuffer, readStart))
            {
                currentRawLevel = 0f;
                currentNormalizedLevel = Mathf.Lerp(currentNormalizedLevel, 0f, 1f - smoothing);
                return;
            }

            var squaredSum = 0f;

            for (var index = 0; index < readableSamples; index++)
            {
                squaredSum += sampleBuffer[index] * sampleBuffer[index];
            }

            currentRawLevel = Mathf.Sqrt(squaredSum / readableSamples);

            var amplifiedLevel = currentRawLevel * inputSensitivity;
            var gatedLevel = amplifiedLevel <= noiseGate ? 0f : amplifiedLevel - noiseGate;
            var normalizedTarget = Mathf.Clamp01(gatedLevel / maxExpectedLevel);
            currentNormalizedLevel = Mathf.Lerp(currentNormalizedLevel, normalizedTarget, 1f - smoothing);
            LogLevelsIfNeeded();
        }

        private void LogLevelsIfNeeded()
        {
            if (!enableDebugLogs || Time.unscaledTime < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.unscaledTime + debugLogInterval;
            Debug.Log($"Mic raw level: {currentRawLevel:F4}", this);
            Debug.Log($"Mic normalized level: {currentNormalizedLevel:F4}", this);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private static T FindComponentByName<T>(string targetName) where T : Component
        {
            var activeScene = SceneManager.GetActiveScene();

            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                var match = FindComponentRecursive<T>(rootObject.transform, targetName);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentRecursive<T>(Transform current, string targetName) where T : Component
        {
            if (current.name == targetName && current.TryGetComponent<T>(out var component))
            {
                return component;
            }

            for (var index = 0; index < current.childCount; index++)
            {
                var childMatch = FindComponentRecursive<T>(current.GetChild(index), targetName);

                if (childMatch != null)
                {
                    return childMatch;
                }
            }

            return null;
        }
    }
}
