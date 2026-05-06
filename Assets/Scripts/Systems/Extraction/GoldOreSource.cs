using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    public sealed class GoldOreSource : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject[] instantiatedRockVisuals;
        [SerializeField, Min(0f)] private float shakeDuration = 0.14f;
        [SerializeField, Min(0f)] private float minimumShakeAmplitude = 0.003f;
        [SerializeField, Min(0f)] private float maximumShakeAmplitude = 0.009f;
        [SerializeField, Min(1f)] private float shakeFrequency = 48f;

        private float shakeTimeRemaining;
        private float currentShakeAmplitude;
        private float shakeSeed;

        public Transform SpawnPoint => spawnPoint;

        public void Configure(
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 spawnOffset,
            Material renderMaterial,
            GameObject[] rockPrefabs)
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            visualRoot = EnsureChild("RockMena").transform;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;

            RebuildRockMena(localScale, renderMaterial, rockPrefabs);

            if (spawnPoint == null)
            {
                spawnPoint = new GameObject("GoldSpawnPoint").transform;
                spawnPoint.SetParent(transform, false);
            }
            else
            {
                spawnPoint.name = "GoldSpawnPoint";
                spawnPoint.SetParent(transform, false);
            }

            spawnPoint.localPosition = spawnOffset;
            spawnPoint.localRotation = Quaternion.identity;
            spawnPoint.localScale = Vector3.one;
        }

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (shakeTimeRemaining <= 0f)
            {
                visualRoot.localPosition = Vector3.zero;
                return;
            }

            shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.deltaTime);
            var normalizedTime = shakeDuration > 0f ? (shakeTimeRemaining / shakeDuration) : 0f;
            var dampedAmplitude = currentShakeAmplitude * normalizedTime;
            var oscillationTime = (Time.time + shakeSeed) * shakeFrequency;
            var shakeOffset = new Vector3(
                Mathf.Sin(oscillationTime) * dampedAmplitude,
                Mathf.Sin(oscillationTime * 1.35f) * dampedAmplitude * 0.42f,
                Mathf.Cos(oscillationTime * 0.92f) * dampedAmplitude * 0.55f);

            visualRoot.localPosition = shakeOffset;
        }

        public void PlayImpactFeedback(float impactForce)
        {
            shakeTimeRemaining = shakeDuration;
            shakeSeed = Random.Range(0f, 100f);
            var shakeStrength = Mathf.InverseLerp(1f, 3f, impactForce);
            currentShakeAmplitude = Mathf.Lerp(minimumShakeAmplitude, maximumShakeAmplitude, shakeStrength);
        }

        public Bounds GetViewBounds()
        {
            if (visualRoot == null)
            {
                return new Bounds(transform.position, new Vector3(0.12f, 0.1f, 0.1f));
            }

            var renderers = visualRoot.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(visualRoot.position, new Vector3(0.12f, 0.1f, 0.1f));
            }

            var combinedBounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                combinedBounds.Encapsulate(renderers[index].bounds);
            }

            return combinedBounds;
        }

        private void RebuildRockMena(Vector3 targetSize, Material fallbackMaterial, GameObject[] rockPrefabs)
        {
            ClearVisualRoot();

            if (rockPrefabs == null || rockPrefabs.Length == 0)
            {
                BuildFallbackOre(targetSize, fallbackMaterial);
                return;
            }

            var layout = new[]
            {
                new RockPlacement(0, new Vector3(-0.11f, 0.015f, -0.015f), new Vector3(12f, 28f, 6f), new Vector3(0.24f, 0.17f, 0.17f)),
                new RockPlacement(1, new Vector3(0.09f, 0.02f, 0.02f), new Vector3(-8f, -34f, -4f), new Vector3(0.22f, 0.16f, 0.18f)),
                new RockPlacement(2, new Vector3(-0.015f, -0.01f, 0.05f), new Vector3(18f, 74f, -10f), new Vector3(0.2f, 0.15f, 0.16f)),
                new RockPlacement(3, new Vector3(0.02f, -0.018f, -0.06f), new Vector3(-16f, 40f, 12f), new Vector3(0.23f, 0.16f, 0.18f)),
                new RockPlacement(4, new Vector3(0f, 0.055f, -0.005f), new Vector3(24f, -16f, 18f), new Vector3(0.19f, 0.14f, 0.14f))
            };

            instantiatedRockVisuals = new GameObject[layout.Length];

            for (var index = 0; index < layout.Length; index++)
            {
                var placement = layout[index];
                var prefab = rockPrefabs[Mathf.Abs(placement.prefabIndex) % rockPrefabs.Length];

                if (prefab == null)
                {
                    continue;
                }

                var instance = Instantiate(prefab, visualRoot);
                instance.name = $"Rock_{index + 1:00}_{prefab.name}";
                instance.transform.localPosition = placement.localPosition;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                FitInstanceToSize(instance, placement.targetSize);
                instance.transform.localRotation = Quaternion.Euler(placement.localEulerAngles);
                DisableColliders(instance);
                instantiatedRockVisuals[index] = instance;
            }
        }

        private void BuildFallbackOre(Vector3 localScale, Material renderMaterial)
        {
            ConfigureFallbackPiece("Core", PrimitiveType.Sphere, Vector3.zero, localScale, new Vector3(0f, 0f, 0f), renderMaterial);
            ConfigureFallbackPiece("LobeTopLeft", PrimitiveType.Sphere, new Vector3(-0.032f, 0.022f, 0.015f), localScale * 0.55f, new Vector3(0f, 0f, 18f), renderMaterial);
            ConfigureFallbackPiece("LobeBottomRight", PrimitiveType.Sphere, new Vector3(0.028f, -0.018f, -0.014f), localScale * 0.5f, new Vector3(0f, 20f, -16f), renderMaterial);
            ConfigureFallbackPiece("LobeFront", PrimitiveType.Capsule, new Vector3(0.016f, 0.01f, 0.03f), new Vector3(localScale.x * 0.35f, localScale.y * 0.3f, localScale.z * 0.25f), new Vector3(24f, 8f, 34f), renderMaterial);
            ConfigureFallbackPiece("LobeBack", PrimitiveType.Cube, new Vector3(-0.02f, -0.006f, -0.028f), new Vector3(localScale.x * 0.28f, localScale.y * 0.32f, localScale.z * 0.24f), new Vector3(-14f, 30f, -12f), renderMaterial);
        }

        private void ConfigureFallbackPiece(
            string pieceName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material renderMaterial)
        {
            var piece = EnsurePrimitiveChild(pieceName, primitiveType);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.Euler(localEulerAngles);
            piece.transform.localScale = localScale;

            if (piece.TryGetComponent<Renderer>(out var renderer) && renderMaterial != null)
            {
                renderer.sharedMaterial = renderMaterial;
            }

            if (piece.TryGetComponent<Collider>(out var collider))
            {
                collider.enabled = false;
            }
        }

        private void ClearVisualRoot()
        {
            for (var index = visualRoot.childCount - 1; index >= 0; index--)
            {
                var child = visualRoot.GetChild(index);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            instantiatedRockVisuals = System.Array.Empty<GameObject>();
        }

        private static void DisableColliders(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        private static void FitInstanceToSize(GameObject instance, Vector3 targetSize)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return;
            }

            var combinedBounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                combinedBounds.Encapsulate(renderers[index].bounds);
            }

            var currentSize = combinedBounds.size;
            var widthScale = currentSize.x > 0.0001f ? targetSize.x / currentSize.x : 1f;
            var heightScale = currentSize.y > 0.0001f ? targetSize.y / currentSize.y : 1f;
            var depthScale = currentSize.z > 0.0001f ? targetSize.z / currentSize.z : 1f;
            var uniformScale = Mathf.Min(widthScale, heightScale, depthScale);

            instance.transform.localScale = Vector3.one * uniformScale;
        }

        private GameObject EnsurePrimitiveChild(string childName, PrimitiveType primitiveType)
        {
            var childTransform = visualRoot != null ? visualRoot.Find(childName) : null;

            if (childTransform != null)
            {
                return childTransform.gameObject;
            }

            var primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = childName;
            primitive.transform.SetParent(visualRoot, false);
            return primitive;
        }

        private GameObject EnsureChild(string childName)
        {
            var childTransform = transform.Find(childName);

            if (childTransform != null)
            {
                return childTransform.gameObject;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            return childObject;
        }

        private readonly struct RockPlacement
        {
            public readonly int prefabIndex;
            public readonly Vector3 localPosition;
            public readonly Vector3 localEulerAngles;
            public readonly Vector3 targetSize;

            public RockPlacement(int prefabIndex, Vector3 localPosition, Vector3 localEulerAngles, Vector3 targetSize)
            {
                this.prefabIndex = prefabIndex;
                this.localPosition = localPosition;
                this.localEulerAngles = localEulerAngles;
                this.targetSize = targetSize;
            }
        }
    }
}
