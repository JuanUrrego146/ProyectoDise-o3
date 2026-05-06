using System;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class GoldNuggetSpawner : MonoBehaviour
    {
        [Header("References")]
        public Transform spawnPoint;
        public GameObject goldPrefab;
        [SerializeField] private Transform nuggetsParent;

        [Header("Impact Thresholds")]
        [SerializeField, Min(0f)] private float mediumImpactThreshold = 1.5f;
        [SerializeField, Min(0f)] private float strongImpactThreshold = 2.5f;

        [Header("Nugget Ranges")]
        [SerializeField, Min(0.001f)] private float minDiameter = 0.008f;
        [SerializeField, Min(0.001f)] private float maxDiameter = 0.014f;
        [SerializeField, Min(0.001f)] private float minMassKg = 0.01f;
        [SerializeField, Min(0.001f)] private float maxMassKg = 0.05f;

        [Header("Spawn Scatter")]
        [SerializeField, Min(0f)] private float spawnHeightOffset = 0.018f;
        [SerializeField, Min(0f)] private float burstRingRadius = 0.012f;
        [SerializeField, Min(0f)] private float positionJitter = 0.0035f;
        [SerializeField, Min(0f)] private float minimumSpawnSeparation = 0.012f;
        [SerializeField, Min(0f)] private float minimumUpwardImpulse = 0.0012f;
        [SerializeField, Min(0f)] private float maximumUpwardImpulse = 0.0032f;
        [SerializeField, Min(0f)] private float maximumSideImpulse = 0.001f;
        [SerializeField, Min(0f)] private float maximumSpinImpulse = 0.00008f;
        [SerializeField, Min(1)] private int spawnOverlapAttempts = 8;

        private int spawnedSequence;

        public event Action<GoldNugget> NuggetSpawned;

        public Transform NuggetsParent => nuggetsParent;

        public void SetReferences(Transform resolvedSpawnPoint, GameObject resolvedGoldPrefab, Transform resolvedParent = null)
        {
            spawnPoint = resolvedSpawnPoint;
            goldPrefab = resolvedGoldPrefab;

            if (resolvedParent != null || nuggetsParent == null)
            {
                nuggetsParent = resolvedParent;
            }
        }

        public void Configure(
            GoldOreSource resolvedOreSource,
            Transform resolvedSpawnRoot,
            PhysicsMaterial resolvedPhysicsMaterial,
            Material resolvedRenderMaterial)
        {
            if (resolvedOreSource != null)
            {
                spawnPoint = resolvedOreSource.SpawnPoint;
            }

            if (resolvedSpawnRoot != null)
            {
                nuggetsParent = resolvedSpawnRoot;
            }
        }

        public int SpawnForImpact(float impactForce, int remainingCapacity)
        {
            return SpawnNuggets(ResolveNuggetCount(impactForce), impactForce, remainingCapacity);
        }

        public int SpawnNuggets(int nuggetCount, float impactForce = 0f, int remainingCapacity = int.MaxValue)
        {
            if (remainingCapacity <= 0 || nuggetCount <= 0)
            {
                return 0;
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning("GoldNuggetSpawner requires a spawnPoint assigned from the Inspector.");
                return 0;
            }

            if (goldPrefab == null)
            {
                Debug.LogWarning("GoldNuggetSpawner requires a goldPrefab assigned from the Inspector.");
                return 0;
            }

            var requestedCount = Mathf.Min(nuggetCount, remainingCapacity);
            var burstAngleOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var spawnedCount = 0;

            for (var index = 0; index < requestedCount; index++)
            {
                if (SpawnSingleNugget(impactForce, index, requestedCount, burstAngleOffset))
                {
                    spawnedCount++;
                }
            }

            return spawnedCount;
        }

        public int ResolveNuggetCount(float impactForce)
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

        private bool SpawnSingleNugget(float impactForce, int spawnIndex, int spawnCount, float burstAngleOffset)
        {
            var nuggetObject = nuggetsParent != null
                ? Instantiate(goldPrefab, Vector3.zero, Quaternion.identity, nuggetsParent)
                : Instantiate(goldPrefab);

            nuggetObject.transform.position = FindSpawnPosition(spawnIndex, spawnCount, burstAngleOffset);
            nuggetObject.transform.rotation = Quaternion.identity;

            if (!nuggetObject.TryGetComponent<GoldNugget>(out var nugget))
            {
                Debug.LogWarning($"Gold prefab '{goldPrefab.name}' must contain a GoldNugget component.");
                Destroy(nuggetObject);
                return false;
            }

            var mass = UnityEngine.Random.Range(minMassKg, maxMassKg);
            var diameter = UnityEngine.Random.Range(minDiameter, maxDiameter);
            nugget.Configure(++spawnedSequence, mass, diameter);

            var impulseStrength = Mathf.InverseLerp(0f, strongImpactThreshold, impactForce);
            var upwardImpulse = Mathf.Lerp(minimumUpwardImpulse, maximumUpwardImpulse, impulseStrength);
            var sideImpulse = UnityEngine.Random.insideUnitSphere * Mathf.Lerp(0.0002f, maximumSideImpulse, impulseStrength);
            var totalImpulse = new Vector3(sideImpulse.x, upwardImpulse, sideImpulse.z);

            nugget.Rigidbody.AddForce(totalImpulse, ForceMode.Impulse);

            if (maximumSpinImpulse > 0f)
            {
                nugget.Rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * maximumSpinImpulse, ForceMode.Impulse);
            }

            NuggetSpawned?.Invoke(nugget);
            return true;
        }

        private Vector3 FindSpawnPosition(int spawnIndex, int spawnCount, float burstAngleOffset)
        {
            var spawnOrigin = spawnPoint.position;
            var sampledDiameter = UnityEngine.Random.Range(minDiameter, maxDiameter);
            var radius = sampledDiameter * 0.5f;
            var baseRingRadius = Mathf.Max(burstRingRadius, minimumSpawnSeparation + radius);

            for (var attempt = 0; attempt < spawnOverlapAttempts; attempt++)
            {
                var angle = burstAngleOffset + ((Mathf.PI * 2f) * spawnIndex / Mathf.Max(1, spawnCount)) + (attempt * 0.43f);
                var ringRadius = baseRingRadius + (attempt * radius * 0.35f);
                var plannedOffset = new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    spawnHeightOffset + UnityEngine.Random.Range(0f, radius * 0.6f),
                    Mathf.Sin(angle) * ringRadius);

                var jitter = new Vector3(
                    UnityEngine.Random.Range(-positionJitter, positionJitter),
                    0f,
                    UnityEngine.Random.Range(-positionJitter, positionJitter));

                var candidatePosition = spawnOrigin + plannedOffset + jitter;

                if (!Physics.CheckSphere(candidatePosition, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return candidatePosition;
                }
            }

            return spawnOrigin + new Vector3(
                Mathf.Cos(burstAngleOffset + spawnIndex) * baseRingRadius,
                spawnHeightOffset + (radius * 0.5f),
                Mathf.Sin(burstAngleOffset + spawnIndex) * baseRingRadius);
        }
    }
}
