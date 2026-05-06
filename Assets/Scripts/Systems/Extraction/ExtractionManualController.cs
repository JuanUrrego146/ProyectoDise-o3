using System.Collections;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class ExtractionManualController : MonoBehaviour
    {
        [Header("References")]
        public Transform spawnPoint;
        public GameObject goldPrefab;
        public Transform rockMena;

        [Header("Debug Input")]
        [SerializeField] private bool enableDebugKeys = true;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnOffsetRange = 0.05f;

        [Header("Rock Shake")]
        [SerializeField, Range(0.1f, 0.2f)] private float vibrationDuration = 0.15f;
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.006f;
        [SerializeField, Min(1f)] private float vibrationFrequency = 40f;

        private Coroutine shakeRoutine;
        private Vector3 rockOriginalLocalPosition;
        private bool hasCachedRockPosition;
        private bool inputBackendWarningShown;

        private void Start()
        {
            CacheRockPosition();
            Debug.Log($"ExtractionManualController active on '{gameObject.name}'.");
            ValidateReferences(logWarnings: true);
        }

        private void OnDisable()
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            RestoreRockPosition();
        }

        private void Update()
        {
            if (!Application.isPlaying || !enableDebugKeys)
            {
                return;
            }

            if (TryGetRequestedNuggetCount(out var nuggetCount, out var detectedKey))
            {
                HandleDebugRequest(detectedKey, nuggetCount);
            }
        }

        public void GenerateNuggets(int nuggetCount)
        {
            Debug.Log($"Generating {nuggetCount} nuggets");

            if (!ValidateSpawnReferences(logWarnings: true))
            {
                return;
            }

            for (var index = 0; index < nuggetCount; index++)
            {
                var offset = new Vector3(
                    Random.Range(-spawnOffsetRange, spawnOffsetRange),
                    0f,
                    Random.Range(-spawnOffsetRange, spawnOffsetRange));

                var spawnPosition = spawnPoint.position + offset;
                var nuggetInstance = Instantiate(goldPrefab, spawnPosition, Quaternion.identity);

                if (nuggetInstance == null)
                {
                    Debug.LogWarning("Instantiate returned null while generating a nugget.");
                    continue;
                }

                Debug.Log($"Spawned nugget {index + 1}/{nuggetCount} at {spawnPosition}");
            }
        }

        public void TriggerRockShake()
        {
            Debug.Log("Attempting rock shake");

            if (rockMena == null)
            {
                Debug.LogWarning("Missing rockMena reference");
                return;
            }

            CacheRockPosition();

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }

            Debug.Log("Rock shake triggered");
            shakeRoutine = StartCoroutine(RockShakeRoutine());
        }

        private void HandleDebugRequest(string detectedKey, int nuggetCount)
        {
            Debug.Log($"{detectedKey} detected");
            GenerateNuggets(nuggetCount);
            TriggerRockShake();
        }

        private bool TryGetRequestedNuggetCount(out int nuggetCount, out string detectedKey)
        {
            nuggetCount = 0;
            detectedKey = string.Empty;

            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                {
                    nuggetCount = 1;
                    detectedKey = "Alpha1";
                    return true;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                {
                    nuggetCount = 2;
                    detectedKey = "Alpha2";
                    return true;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                {
                    nuggetCount = 3;
                    detectedKey = "Alpha3";
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                if (!inputBackendWarningShown)
                {
                    Debug.LogWarning($"Legacy Input API is unavailable: {exception.Message}");
                    inputBackendWarningShown = true;
                }
            }

            return false;
        }

        private bool ValidateReferences(bool logWarnings)
        {
            var isValid = true;

            if (spawnPoint == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing spawnPoint reference");
                }

                isValid = false;
            }

            if (goldPrefab == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing goldPrefab reference");
                }

                isValid = false;
            }

            if (rockMena == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing rockMena reference");
                }

                isValid = false;
            }

            if (goldPrefab != null && !goldPrefab.TryGetComponent<Collider>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldPrefab is missing a Collider component");
                }

                isValid = false;
            }

            if (goldPrefab != null && !goldPrefab.TryGetComponent<Rigidbody>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldPrefab is missing a Rigidbody component");
                }

                isValid = false;
            }

            return isValid;
        }

        private bool ValidateSpawnReferences(bool logWarnings)
        {
            var isValid = true;

            if (spawnPoint == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing spawnPoint reference");
                }

                isValid = false;
            }

            if (goldPrefab == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing goldPrefab reference");
                }

                isValid = false;
            }

            if (goldPrefab != null && !goldPrefab.TryGetComponent<Collider>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldPrefab is missing a Collider component");
                }

                isValid = false;
            }

            if (goldPrefab != null && !goldPrefab.TryGetComponent<Rigidbody>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldPrefab is missing a Rigidbody component");
                }

                isValid = false;
            }

            return isValid;
        }

        private IEnumerator RockShakeRoutine()
        {
            var elapsed = 0f;

            while (elapsed < vibrationDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = 1f - Mathf.Clamp01(elapsed / vibrationDuration);
                var amplitude = vibrationAmplitude * normalized;
                var timeSample = Time.time * vibrationFrequency;
                var offset = new Vector3(
                    Mathf.Sin(timeSample) * amplitude,
                    Mathf.Cos(timeSample * 1.21f) * amplitude * 0.7f,
                    Mathf.Sin(timeSample * 0.91f) * amplitude * 0.85f);

                rockMena.localPosition = rockOriginalLocalPosition + offset;
                yield return null;
            }

            RestoreRockPosition();
            shakeRoutine = null;
        }

        private void CacheRockPosition()
        {
            if (rockMena == null)
            {
                return;
            }

            rockOriginalLocalPosition = rockMena.localPosition;
            hasCachedRockPosition = true;
        }

        private void RestoreRockPosition()
        {
            if (!hasCachedRockPosition || rockMena == null)
            {
                return;
            }

            rockMena.localPosition = rockOriginalLocalPosition;
        }
    }
}
