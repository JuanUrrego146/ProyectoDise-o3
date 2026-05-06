using System.Collections;
using LingoteRush.Input;
using LingoteRush.Managers;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class ExtractionController : MonoBehaviour
    {
        [Header("References")]
        public Transform goldSpawnPoint;
        public GameObject goldNuggetPrefab;
        public Transform rockMena;

        [Header("Spawn")]
        [SerializeField, Min(0f)] private float spawnOffsetRange = 0.02f;
        [SerializeField, Min(0.001f)] private float configuredNuggetMass = 0.0075f;
        [SerializeField, Min(0.001f)] private float configuredNuggetDiameter = 0.02f;
        [SerializeField, Range(30, 50)] private int maxTotalNuggets = 30;
        [SerializeField, Min(0)] private int currentSpawnedNuggets;

        [Header("Impact Thresholds")]
        [SerializeField, Min(0f)] private float mediumImpactThreshold = 1.5f;
        [SerializeField, Min(0f)] private float strongImpactThreshold = 2.5f;

        [Header("Rock Shake")]
        [SerializeField, Range(0.1f, 0.2f)] private float vibrationDuration = 0.15f;
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.006f;
        [SerializeField, Min(1f)] private float vibrationFrequency = 40f;

        private Coroutine shakeRoutine;
        private Vector3 rockOriginalLocalPosition;
        private bool hasCachedRockPosition;
        private int spawnedSequence;
        private InputManager inputManager;
        private bool missingInputWarningShown;

        private void Start()
        {
            CacheRockPosition();
            BindInputManager();
            currentSpawnedNuggets = 0;
            Debug.Log("Using static ExtractionController in scene");
            ValidateReferences(logWarnings: true);
        }

        private void OnEnable()
        {
            CacheRockPosition();
            BindInputManager();
        }

        private void OnDisable()
        {
            UnbindInputManager();

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            RestoreRockPosition();
        }

        private void BindInputManager()
        {
            if (inputManager != null)
            {
                inputManager.ImpactReceived -= HandleImpactReceived;
            }

            inputManager = ResolveInputManager();

            if (inputManager == null)
            {
                if (!missingInputWarningShown)
                {
                    Debug.LogWarning("ExtractionController could not resolve the global Input Manager.");
                    missingInputWarningShown = true;
                }

                return;
            }

            missingInputWarningShown = false;

            if (inputManager.PressureSensor != null)
            {
                inputManager.PressureSensor.UseDebugKeys = true;
                inputManager.PressureSensor.ConfigureDebugImpacts(1f, 2f, 3f);
            }

            inputManager.ImpactReceived -= HandleImpactReceived;
            inputManager.ImpactReceived += HandleImpactReceived;
        }

        private void UnbindInputManager()
        {
            if (inputManager == null)
            {
                return;
            }

            inputManager.ImpactReceived -= HandleImpactReceived;
        }

        private void HandleImpactReceived(float impactForce)
        {
            RegisterImpact(impactForce);
        }

        public void RegisterImpact(float impactValue)
        {
            GenerateFromImpactForce(impactValue);
        }

        public void TriggerWeakHit()
        {
            RegisterImpact(1f);
        }

        public void TriggerMediumHit()
        {
            RegisterImpact(2f);
        }

        public void TriggerStrongHit()
        {
            RegisterImpact(3f);
        }

        public void GenerateFromImpactForce(float impactForce)
        {
            Debug.Log($"Impact received: {impactForce}");

            var requestedNuggetCount = ResolveNuggetCount(impactForce);

            if (requestedNuggetCount <= 0)
            {
                return;
            }

            var remainingNuggets = Mathf.Max(0, maxTotalNuggets - currentSpawnedNuggets);

            if (remainingNuggets <= 0)
            {
                Debug.Log("Max nuggets reached");
                Debug.Log($"Current nuggets: {currentSpawnedNuggets} / {maxTotalNuggets}");
                TriggerRockShake();
                return;
            }

            var nuggetCount = Mathf.Min(requestedNuggetCount, remainingNuggets);

            SpawnNuggets(nuggetCount);
            TriggerRockShake();
        }

        public void SetReferences(GoldNuggetSpawner nuggetSpawner, CrucibleController crucible = null)
        {
            if (nuggetSpawner != null)
            {
                if (goldSpawnPoint == null)
                {
                    goldSpawnPoint = nuggetSpawner.spawnPoint;
                }

                if (goldNuggetPrefab == null)
                {
                    goldNuggetPrefab = nuggetSpawner.goldPrefab;
                }
            }
        }

        private void SpawnNuggets(int nuggetCount)
        {
            Debug.Log($"Spawning {nuggetCount} nuggets");

            if (!ValidateSpawnReferences(logWarnings: true))
            {
                return;
            }

            var appliedSpawnOffsetRange = Mathf.Clamp(spawnOffsetRange, 0f, 0.02f);
            var appliedNuggetMass = Mathf.Clamp(configuredNuggetMass, 0.005f, 0.01f);
            var appliedNuggetDiameter = Mathf.Clamp(configuredNuggetDiameter, 0.015f, 0.025f);

            for (var index = 0; index < nuggetCount; index++)
            {
                var offset = new Vector3(
                    Random.Range(-appliedSpawnOffsetRange, appliedSpawnOffsetRange),
                    0f,
                    Random.Range(-appliedSpawnOffsetRange, appliedSpawnOffsetRange));

                var spawnPosition = goldSpawnPoint.position + offset;
                var nuggetInstance = Instantiate(goldNuggetPrefab, spawnPosition, Quaternion.identity);

                if (nuggetInstance == null)
                {
                    Debug.LogWarning("Instantiate returned null while generating a nugget.");
                    continue;
                }

                if (nuggetInstance.TryGetComponent<GoldNugget>(out var nugget))
                {
                    nugget.Configure(++spawnedSequence, appliedNuggetMass, appliedNuggetDiameter);
                }

                currentSpawnedNuggets++;
                Debug.Log($"Spawned nugget {index + 1}/{nuggetCount} at {spawnPosition}");
            }

            Debug.Log($"Current nuggets: {currentSpawnedNuggets} / {maxTotalNuggets}");

            if (currentSpawnedNuggets >= maxTotalNuggets)
            {
                Debug.Log("Max nuggets reached");
            }
        }

        private void TriggerRockShake()
        {
            if (rockMena == null)
            {
                Debug.LogWarning("Missing TechoRocas reference");
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

        private int ResolveNuggetCount(float impactForce)
        {
            if (impactForce <= 0f)
            {
                return 0;
            }

            if (impactForce < mediumImpactThreshold)
            {
                return 1;
            }

            if (impactForce < strongImpactThreshold)
            {
                return 2;
            }

            return 3;
        }

        private bool ValidateReferences(bool logWarnings)
        {
            var isValid = ValidateSpawnReferences(logWarnings);

            if (rockMena == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing TechoRocas reference");
                }

                isValid = false;
            }

            return isValid;
        }

        private bool ValidateSpawnReferences(bool logWarnings)
        {
            var isValid = true;

            if (goldSpawnPoint == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing GoldSpawnPoint reference");
                }

                isValid = false;
            }

            if (goldNuggetPrefab == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("Missing GoldNugget reference");
                }

                isValid = false;
            }

            if (goldNuggetPrefab != null && !goldNuggetPrefab.TryGetComponent<Collider>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldNuggetPrefab is missing a Collider component");
                }

                isValid = false;
            }

            if (goldNuggetPrefab != null && !goldNuggetPrefab.TryGetComponent<Rigidbody>(out _))
            {
                if (logWarnings)
                {
                    Debug.LogWarning("goldNuggetPrefab is missing a Rigidbody component");
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
                    Mathf.Cos(timeSample * 1.19f) * amplitude * 0.65f,
                    Mathf.Sin(timeSample * 0.93f) * amplitude * 0.85f);

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

        private static InputManager ResolveInputManager()
        {
            if (GameManager.HasInstance && GameManager.Instance.InputManager != null)
            {
                return GameManager.Instance.InputManager;
            }

            return Object.FindAnyObjectByType<InputManager>();
        }
    }
}
