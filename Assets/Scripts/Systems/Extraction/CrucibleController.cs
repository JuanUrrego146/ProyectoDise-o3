using System;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Systems.Extraction
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CrucibleController : MonoBehaviour
    {
        [SerializeField, Range(1, 50)] private int maximumSupportedNuggets = 50;
        [SerializeField, Min(0.05f)] private float moveSpeed = 0.22f;
        [SerializeField, Min(0.05f)] private float moveLimit = 0.14f;
        [SerializeField] private bool useMovementKeys = true;
        [SerializeField] private Vector3 localStartPosition = new Vector3(0f, 0.02f, 0f);
        [SerializeField] private Vector3 innerSize = new Vector3(0.24f, 0.14f, 0.2f);
        [SerializeField, Min(0.005f)] private float wallThickness = 0.018f;
        [SerializeField, Min(0.005f)] private float floorThickness = 0.018f;
        [SerializeField] private CrucibleCollectionZone collectionZone;

        private Transform visualRoot;

        private readonly HashSet<GoldNugget> collectedNuggets = new HashSet<GoldNugget>();
        private Rigidbody cachedRigidbody;

        public event Action<GoldNugget> NuggetCollected;

        public int CollectedCount => collectedNuggets.Count;

        public void Configure(
            int maximumNuggets,
            float resolvedMoveSpeed,
            float resolvedMoveLimit,
            PhysicsMaterial surfaceMaterial,
            Material renderMaterial,
            GameObject visualPrefab)
        {
            maximumSupportedNuggets = Mathf.Clamp(maximumNuggets, 1, 50);
            moveSpeed = Mathf.Max(0.05f, resolvedMoveSpeed);
            moveLimit = Mathf.Max(0.05f, resolvedMoveLimit);
            innerSize = CalculateInnerSize(maximumSupportedNuggets);
            EnsureBody();
            transform.localPosition = localStartPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            BuildStructure(surfaceMaterial, renderMaterial, visualPrefab);
            collectedNuggets.Clear();
        }

        private void Awake()
        {
            EnsureBody();
        }

        private void FixedUpdate()
        {
            if (!useMovementKeys)
            {
                return;
            }

            var horizontalInput = ReadHorizontalInput();

            if (Mathf.Approximately(horizontalInput, 0f))
            {
                return;
            }

            var currentLocalPosition = transform.localPosition;
            var targetLocalX = Mathf.Clamp(
                currentLocalPosition.x + horizontalInput * moveSpeed * Time.fixedDeltaTime,
                localStartPosition.x - moveLimit,
                localStartPosition.x + moveLimit);

            var targetLocalPosition = new Vector3(targetLocalX, localStartPosition.y, localStartPosition.z);
            var targetWorldPosition = transform.parent != null
                ? transform.parent.TransformPoint(targetLocalPosition)
                : targetLocalPosition;

            cachedRigidbody.MovePosition(targetWorldPosition);
        }

        public bool TryRegisterCollectedNugget(GoldNugget nugget)
        {
            if (nugget == null)
            {
                return false;
            }

            if (!collectedNuggets.Add(nugget))
            {
                return false;
            }

            nugget.MarkCollected();
            NuggetCollected?.Invoke(nugget);
            return true;
        }

        private void EnsureBody()
        {
            if (!TryGetComponent<Rigidbody>(out cachedRigidbody))
            {
                cachedRigidbody = gameObject.AddComponent<Rigidbody>();
            }

            cachedRigidbody.isKinematic = true;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void BuildStructure(PhysicsMaterial surfaceMaterial, Material renderMaterial, GameObject visualPrefab)
        {
            ConfigurePhysicsPart(
                EnsurePrimitiveChild("Floor"),
                new Vector3(0f, floorThickness * 0.5f, 0f),
                new Vector3(innerSize.x + (wallThickness * 2f), floorThickness, innerSize.z + (wallThickness * 2f)),
                surfaceMaterial);

            ConfigurePhysicsPart(
                EnsurePrimitiveChild("WallLeft"),
                new Vector3((-innerSize.x * 0.5f) - (wallThickness * 0.5f), floorThickness + (innerSize.y * 0.5f), 0f),
                new Vector3(wallThickness, innerSize.y, innerSize.z + (wallThickness * 2f)),
                surfaceMaterial);

            ConfigurePhysicsPart(
                EnsurePrimitiveChild("WallRight"),
                new Vector3((innerSize.x * 0.5f) + (wallThickness * 0.5f), floorThickness + (innerSize.y * 0.5f), 0f),
                new Vector3(wallThickness, innerSize.y, innerSize.z + (wallThickness * 2f)),
                surfaceMaterial);

            ConfigurePhysicsPart(
                EnsurePrimitiveChild("WallFront"),
                new Vector3(0f, floorThickness + (innerSize.y * 0.5f), (innerSize.z * 0.5f) + (wallThickness * 0.5f)),
                new Vector3(innerSize.x, innerSize.y, wallThickness),
                surfaceMaterial);

            ConfigurePhysicsPart(
                EnsurePrimitiveChild("WallBack"),
                new Vector3(0f, floorThickness + (innerSize.y * 0.5f), (-innerSize.z * 0.5f) - (wallThickness * 0.5f)),
                new Vector3(innerSize.x, innerSize.y, wallThickness),
                surfaceMaterial);

            EnsureCollectionZone();
            BuildVisual(renderMaterial, visualPrefab);
        }

        private void EnsureCollectionZone()
        {
            var zoneTransform = transform.Find("CollectionZone");

            if (zoneTransform == null)
            {
                var zoneObject = new GameObject("CollectionZone");
                zoneObject.transform.SetParent(transform, false);
                collectionZone = zoneObject.AddComponent<CrucibleCollectionZone>();
            }
            else if (!zoneTransform.TryGetComponent<CrucibleCollectionZone>(out collectionZone))
            {
                collectionZone = zoneTransform.gameObject.AddComponent<CrucibleCollectionZone>();
            }

            var zoneSize = new Vector3(innerSize.x * 0.92f, innerSize.y * 0.95f, innerSize.z * 0.92f);
            var zonePosition = new Vector3(0f, floorThickness + (innerSize.y * 0.5f), 0f);
            collectionZone.Configure(this, zoneSize, zonePosition);
        }

        private GameObject EnsurePrimitiveChild(string childName)
        {
            var existing = transform.Find(childName);

            if (existing != null)
            {
                return existing.gameObject;
            }

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = childName;
            primitive.transform.SetParent(transform, false);
            return primitive;
        }

        public Bounds GetViewBounds()
        {
            if (visualRoot != null)
            {
                var renderers = visualRoot.GetComponentsInChildren<Renderer>();

                if (renderers.Length > 0)
                {
                    var combinedBounds = renderers[0].bounds;

                    for (var index = 1; index < renderers.Length; index++)
                    {
                        combinedBounds.Encapsulate(renderers[index].bounds);
                    }

                    return combinedBounds;
                }
            }

            return new Bounds(
                transform.position + Vector3.up * (innerSize.y * 0.35f),
                new Vector3(innerSize.x + 0.05f, innerSize.y + 0.05f, innerSize.z + 0.05f));
        }

        private void BuildVisual(Material renderMaterial, GameObject visualPrefab)
        {
            visualRoot = EnsureChild("VisualRoot").transform;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
            RemoveLegacyVisuals();

            if (visualPrefab != null)
            {
                BuildPrefabVisual(visualPrefab);
                return;
            }

            BuildFallbackVisual(renderMaterial);
        }

        private void BuildPrefabVisual(GameObject visualPrefab)
        {
            var visualInstance = Instantiate(visualPrefab, visualRoot);
            visualInstance.name = "Bowl_Visual";
            visualInstance.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            visualInstance.transform.localRotation = Quaternion.identity;
            visualInstance.transform.localScale = Vector3.one;

            foreach (var collider in visualInstance.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            FitPrefabVisualToColliderFootprint(visualInstance);
        }

        private void BuildFallbackVisual(Material renderMaterial)
        {
            var outerWidth = innerSize.x + (wallThickness * 3f);
            var outerDepth = innerSize.z + (wallThickness * 3f);
            var visualHeight = innerSize.y + floorThickness;

            ConfigureVisualPart(
                EnsureVisualPrimitive("BowlBody", PrimitiveType.Sphere),
                new Vector3(0f, -visualHeight * 0.06f, 0f),
                new Vector3(outerWidth, visualHeight * 0.82f, outerDepth),
                renderMaterial);

            ConfigureVisualPart(
                EnsureVisualPrimitive("RimFront", PrimitiveType.Cube),
                new Vector3(0f, floorThickness + visualHeight * 0.5f, outerDepth * 0.46f),
                new Vector3(innerSize.x * 0.92f, wallThickness * 0.8f, wallThickness * 1.2f),
                renderMaterial);

            ConfigureVisualPart(
                EnsureVisualPrimitive("RimBack", PrimitiveType.Cube),
                new Vector3(0f, floorThickness + visualHeight * 0.5f, -outerDepth * 0.46f),
                new Vector3(innerSize.x * 0.92f, wallThickness * 0.8f, wallThickness * 1.2f),
                renderMaterial);

            ConfigureVisualPart(
                EnsureVisualPrimitive("RimLeft", PrimitiveType.Cube),
                new Vector3(-outerWidth * 0.46f, floorThickness + visualHeight * 0.5f, 0f),
                new Vector3(wallThickness * 1.2f, wallThickness * 0.8f, innerSize.z * 0.92f),
                renderMaterial);

            ConfigureVisualPart(
                EnsureVisualPrimitive("RimRight", PrimitiveType.Cube),
                new Vector3(outerWidth * 0.46f, floorThickness + visualHeight * 0.5f, 0f),
                new Vector3(wallThickness * 1.2f, wallThickness * 0.8f, innerSize.z * 0.92f),
                renderMaterial);
        }

        private static void ConfigurePhysicsPart(
            GameObject part,
            Vector3 localPosition,
            Vector3 localScale,
            PhysicsMaterial surfaceMaterial)
        {
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.enabled = false;
            }

            if (part.TryGetComponent<Collider>(out var collider))
            {
                collider.sharedMaterial = surfaceMaterial;
            }
        }

        private static void ConfigureVisualPart(GameObject part, Vector3 localPosition, Vector3 localScale, Material renderMaterial)
        {
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            if (part.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.enabled = true;

                if (renderMaterial != null)
                {
                    renderer.sharedMaterial = renderMaterial;
                }
            }

            if (part.TryGetComponent<Collider>(out var collider))
            {
                collider.enabled = false;
            }
        }

        private static Vector3 CalculateInnerSize(int maxNuggets)
        {
            var ratio = Mathf.InverseLerp(1f, 50f, maxNuggets);
            var width = Mathf.Lerp(0.16f, 0.24f, ratio);
            var height = Mathf.Lerp(0.11f, 0.14f, ratio);
            var depth = Mathf.Lerp(0.14f, 0.2f, ratio);
            return new Vector3(width, height, depth);
        }

        private GameObject EnsureVisualPrimitive(string childName, PrimitiveType primitiveType)
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
            var existing = transform.Find(childName);

            if (existing != null)
            {
                return existing.gameObject;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            return childObject;
        }

        private void RemoveLegacyVisuals()
        {
            if (visualRoot == null)
            {
                return;
            }

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
        }

        private void FitPrefabVisualToColliderFootprint(GameObject visualInstance)
        {
            var renderers = visualInstance.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;

            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var targetWidth = innerSize.x + (wallThickness * 2.8f);
            var targetDepth = innerSize.z + (wallThickness * 2.8f);
            var currentWidth = Mathf.Max(0.001f, bounds.size.x);
            var currentDepth = Mathf.Max(0.001f, bounds.size.z);
            var uniformScale = Mathf.Min(targetWidth / currentWidth, targetDepth / currentDepth);

            visualInstance.transform.localScale = Vector3.one * uniformScale;
            visualInstance.transform.localPosition = new Vector3(0f, -floorThickness * 0.2f, 0f);
        }

        private static float ReadHorizontalInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                var input = 0f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    input -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    input += 1f;
                }

                return input;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }
    }
}
