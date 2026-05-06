using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using LingoteRush.Managers;
using LingoteRush.Systems.Extraction;
using UnityEngine;

namespace LingoteRush.Input
{
    public sealed class ArduinoPiezoInput : MonoBehaviour
    {
        [Header("Serial")]
        [SerializeField] private string portName = "COM3";
        [SerializeField, Min(1)] private int baudRate = 9600;
        [SerializeField, Min(1)] private int readTimeoutMilliseconds = 50;
        [SerializeField, Min(1)] private int maxLinesPerFrame = 4;
        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private bool dtrEnable = true;

        [Header("Piezo Calibration")]
        [SerializeField, Min(0f)] private float weakThreshold = 50f;
        [SerializeField, Min(0f)] private float mediumThreshold = 250f;
        [SerializeField, Min(0f)] private float strongThreshold = 600f;
        [SerializeField, Min(0f)] private float cooldownBetweenHits = 0.15f;
        [SerializeField, Min(0f)] private float parseMultiplier = 1f;

        [Header("Impact Mapping")]
        [SerializeField, Min(0f)] private float weakImpactForce = 1f;
        [SerializeField, Min(0f)] private float mediumImpactForce = 2f;
        [SerializeField, Min(0f)] private float strongImpactForce = 3f;

        [Header("Integration")]
        [SerializeField] private InputManager inputManager;
        [SerializeField] private ExtractionController extractionController;
        [SerializeField] private bool preferInputManager = true;

        [Header("Debug Logs")]
        [SerializeField] private bool logRawValues = true;
        [SerializeField] private bool logInvalidLines;

        private SerialPortBridge serialPort;
        private float nextAllowedHitTime;
        private bool connectionFailureLogged;

        public bool IsConnected => serialPort != null && serialPort.IsOpen;

        private void Awake()
        {
            ResolveIntegrationReferences();
        }

        private void OnEnable()
        {
            ResolveIntegrationReferences();

            if (connectOnStart)
            {
                OpenSerialPort();
            }
        }

        private void OnDisable()
        {
            CloseSerialPort(logDisconnected: true);
        }

        private void OnApplicationQuit()
        {
            CloseSerialPort(logDisconnected: true);
        }

        private void Update()
        {
            if (!IsConnected)
            {
                return;
            }

            ReadAvailableSerialLines();
        }

        public void OpenSerialPort()
        {
            if (IsConnected)
            {
                return;
            }

            CloseSerialPort(logDisconnected: false);

            try
            {
                if (!SerialPortBridge.TryCreate(portName, baudRate, out serialPort, out var creationError))
                {
                    throw new InvalidOperationException(creationError);
                }

                serialPort.DtrEnable = dtrEnable;
                serialPort.ReadTimeout = readTimeoutMilliseconds;
                serialPort.NewLine = "\n";
                serialPort.Open();
                serialPort.DiscardInBuffer();
                connectionFailureLogged = false;
                Debug.Log($"Serial port opened: {portName}");
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException)
            {
                LogConnectionFailure(exception);
                CloseSerialPort(logDisconnected: false);
            }
        }

        public void CloseSerialPort(bool logDisconnected)
        {
            if (serialPort == null)
            {
                return;
            }

            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is InvalidOperationException)
            {
                Debug.LogWarning($"Serial disconnected: {exception.Message}");
            }
            finally
            {
                serialPort.Dispose();
                serialPort = null;

                if (logDisconnected)
                {
                    Debug.Log("Serial disconnected");
                }
            }
        }

        private void ReadAvailableSerialLines()
        {
            var linesRead = 0;

            while (IsConnected && linesRead < maxLinesPerFrame)
            {
                if (!TryGetBytesToRead(out var bytesToRead) || bytesToRead <= 0)
                {
                    return;
                }

                linesRead++;

                try
                {
                    var line = serialPort.ReadLine();
                    ProcessSerialLine(line);
                }
                catch (TimeoutException)
                {
                    return;
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is InvalidOperationException)
                {
                    Debug.LogWarning($"Serial disconnected: {exception.Message}");
                    CloseSerialPort(logDisconnected: false);
                    return;
                }
            }
        }

        private bool TryGetBytesToRead(out int bytesToRead)
        {
            bytesToRead = 0;

            try
            {
                bytesToRead = serialPort.BytesToRead;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is InvalidOperationException)
            {
                Debug.LogWarning($"Serial disconnected: {exception.Message}");
                CloseSerialPort(logDisconnected: false);
                return false;
            }
        }

        private void ProcessSerialLine(string line)
        {
            if (!TryParsePiezoValue(line, out var rawValue))
            {
                return;
            }

            var scaledValue = rawValue * parseMultiplier;

            if (logRawValues)
            {
                Debug.Log($"Piezo raw value: {rawValue}");
            }

            if (scaledValue < weakThreshold)
            {
                return;
            }

            if (Time.unscaledTime < nextAllowedHitTime)
            {
                return;
            }

            if (scaledValue >= strongThreshold)
            {
                Debug.Log("Strong hit detected");
                DispatchImpact(strongImpactForce);
            }
            else if (scaledValue >= mediumThreshold)
            {
                Debug.Log("Medium hit detected");
                DispatchImpact(mediumImpactForce);
            }
            else
            {
                Debug.Log("Weak hit detected");
                DispatchImpact(weakImpactForce);
            }

            nextAllowedHitTime = Time.unscaledTime + cooldownBetweenHits;
        }

        private bool TryParsePiezoValue(string line, out float value)
        {
            value = 0f;

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var trimmedLine = line.Trim();

            if (float.TryParse(trimmedLine, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (float.TryParse(trimmedLine, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            if (logInvalidLines)
            {
                Debug.LogWarning($"Invalid piezo serial line ignored: {trimmedLine}");
            }

            return false;
        }

        private void DispatchImpact(float impactForce)
        {
            ResolveIntegrationReferences();

            if (preferInputManager && inputManager != null)
            {
                inputManager.SetImpactForce(impactForce);
                return;
            }

            if (extractionController != null)
            {
                extractionController.RegisterImpact(impactForce);
                return;
            }

            if (inputManager != null)
            {
                inputManager.SetImpactForce(impactForce);
                return;
            }

            Debug.LogWarning("ArduinoPiezoInput detected a hit, but no InputManager or ExtractionController is assigned.");
        }

        private void ResolveIntegrationReferences()
        {
            if (inputManager == null && GameManager.HasInstance)
            {
                inputManager = GameManager.Instance.InputManager;
            }

            if (inputManager == null)
            {
                inputManager = UnityEngine.Object.FindAnyObjectByType<InputManager>();
            }

            if (extractionController == null)
            {
                extractionController = UnityEngine.Object.FindAnyObjectByType<ExtractionController>();
            }
        }

        private void LogConnectionFailure(Exception exception)
        {
            if (connectionFailureLogged)
            {
                return;
            }

            connectionFailureLogged = true;
            Debug.LogWarning($"Serial connection failed: {portName} ({exception.Message})");
        }

        private void OnValidate()
        {
            baudRate = Mathf.Max(1, baudRate);
            readTimeoutMilliseconds = Mathf.Max(1, readTimeoutMilliseconds);
            maxLinesPerFrame = Mathf.Max(1, maxLinesPerFrame);
            weakThreshold = Mathf.Max(0f, weakThreshold);
            mediumThreshold = Mathf.Max(weakThreshold, mediumThreshold);
            strongThreshold = Mathf.Max(mediumThreshold, strongThreshold);
            cooldownBetweenHits = Mathf.Max(0f, cooldownBetweenHits);
            parseMultiplier = Mathf.Max(0f, parseMultiplier);
            weakImpactForce = Mathf.Max(0f, weakImpactForce);
            mediumImpactForce = Mathf.Max(weakImpactForce, mediumImpactForce);
            strongImpactForce = Mathf.Max(mediumImpactForce, strongImpactForce);
        }

        private sealed class SerialPortBridge : IDisposable
        {
            private const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;

            private readonly object instance;
            private readonly PropertyInfo isOpenProperty;
            private readonly PropertyInfo bytesToReadProperty;
            private readonly PropertyInfo dtrEnableProperty;
            private readonly PropertyInfo readTimeoutProperty;
            private readonly PropertyInfo newLineProperty;
            private readonly MethodInfo openMethod;
            private readonly MethodInfo closeMethod;
            private readonly MethodInfo discardInBufferMethod;
            private readonly MethodInfo readLineMethod;

            private SerialPortBridge(object instance, Type serialPortType)
            {
                this.instance = instance;
                isOpenProperty = RequireProperty(serialPortType, "IsOpen");
                bytesToReadProperty = RequireProperty(serialPortType, "BytesToRead");
                dtrEnableProperty = RequireProperty(serialPortType, "DtrEnable");
                readTimeoutProperty = RequireProperty(serialPortType, "ReadTimeout");
                newLineProperty = RequireProperty(serialPortType, "NewLine");
                openMethod = RequireMethod(serialPortType, "Open");
                closeMethod = RequireMethod(serialPortType, "Close");
                discardInBufferMethod = RequireMethod(serialPortType, "DiscardInBuffer");
                readLineMethod = RequireMethod(serialPortType, "ReadLine");
            }

            public bool IsOpen => GetProperty<bool>(isOpenProperty);

            public int BytesToRead => GetProperty<int>(bytesToReadProperty);

            public bool DtrEnable
            {
                set => SetProperty(dtrEnableProperty, value);
            }

            public int ReadTimeout
            {
                set => SetProperty(readTimeoutProperty, value);
            }

            public string NewLine
            {
                set => SetProperty(newLineProperty, value);
            }

            public static bool TryCreate(string portName, int baudRate, out SerialPortBridge bridge, out string error)
            {
                bridge = null;
                error = string.Empty;

                var serialPortType = ResolveSerialPortType();

                if (serialPortType == null)
                {
                    error = "System.IO.Ports.SerialPort is not available in the current Unity runtime.";
                    return false;
                }

                try
                {
                    var instance = Activator.CreateInstance(serialPortType, portName, baudRate);
                    bridge = new SerialPortBridge(instance, serialPortType);
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            public void Open()
            {
                Invoke(openMethod);
            }

            public void Close()
            {
                Invoke(closeMethod);
            }

            public void DiscardInBuffer()
            {
                Invoke(discardInBufferMethod);
            }

            public string ReadLine()
            {
                return (string)Invoke(readLineMethod);
            }

            public void Dispose()
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            private static Type ResolveSerialPortType()
            {
                var serialPortType = Type.GetType("System.IO.Ports.SerialPort, System", throwOnError: false)
                    ?? Type.GetType("System.IO.Ports.SerialPort, System.IO.Ports", throwOnError: false);

                if (serialPortType != null)
                {
                    return serialPortType;
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    serialPortType = assembly.GetType("System.IO.Ports.SerialPort", throwOnError: false);

                    if (serialPortType != null)
                    {
                        return serialPortType;
                    }
                }

                serialPortType = TryLoadSerialPortAssembly("System.IO.Ports");

                if (serialPortType != null)
                {
                    return serialPortType;
                }

                var editorExtensionPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Data",
                    "NetStandard",
                    "EditorExtensions",
                    "System.IO.Ports.dll");

                return TryLoadSerialPortAssembly(editorExtensionPath);
            }

            private static Type TryLoadSerialPortAssembly(string assemblyNameOrPath)
            {
                try
                {
                    var assembly = File.Exists(assemblyNameOrPath)
                        ? Assembly.LoadFrom(assemblyNameOrPath)
                        : Assembly.Load(assemblyNameOrPath);

                    return assembly.GetType("System.IO.Ports.SerialPort", throwOnError: false);
                }
                catch
                {
                    return null;
                }
            }

            private static PropertyInfo RequireProperty(Type type, string propertyName)
            {
                var property = type.GetProperty(propertyName, InstancePublic);

                if (property == null)
                {
                    throw new MissingMemberException(type.FullName, propertyName);
                }

                return property;
            }

            private static MethodInfo RequireMethod(Type type, string methodName)
            {
                var method = type.GetMethod(methodName, InstancePublic, null, Type.EmptyTypes, null);

                if (method == null)
                {
                    throw new MissingMethodException(type.FullName, methodName);
                }

                return method;
            }

            private T GetProperty<T>(PropertyInfo property)
            {
                try
                {
                    return (T)property.GetValue(instance);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }

            private void SetProperty(PropertyInfo property, object value)
            {
                try
                {
                    property.SetValue(instance, value);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                }
            }

            private object Invoke(MethodInfo method)
            {
                try
                {
                    return method.Invoke(instance, null);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            }
        }
    }
}
