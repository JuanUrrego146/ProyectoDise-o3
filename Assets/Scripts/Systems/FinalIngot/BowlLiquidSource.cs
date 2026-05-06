using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LingoteRush.Systems.FinalIngot
{
    [DisallowMultipleComponent]
    public sealed class BowlLiquidSource : MonoBehaviour
    {
        [Header("Emission")]
        [SerializeField] private GoldLiquidPool liquidPool;
        [SerializeField] private Transform pourOrigin;
        [SerializeField] private Transform pourReferencePoint;
        [SerializeField] private Transform pourTarget;
        [SerializeField, Min(1f)] private float emissionRate = 42f;
        [SerializeField, Min(0.1f)] private float dropletForce = 2.2f;
        [SerializeField, Min(0.1f)] private float dropletLifetime = 2.35f;
        [SerializeField, Min(0f)] private float dropletSpread = 0.08f;
        [SerializeField, Min(0.001f)] private float dropletAmount = 1f;

        [Header("Contained Gold")]
        [SerializeField] private Transform moltenSurfaceTransform;
        [SerializeField] private Renderer moltenSurfaceRenderer;
        [SerializeField, Min(0f)] private float liquidInset = 0.01f;
        [SerializeField, Range(0.2f, 0.95f)] private float fullLiquidHeightNormalized = 0.68f;
        [SerializeField, Min(0.002f)] private float surfaceThickness = 0.018f;
        [SerializeField, Range(0.9f, 1.25f)] private float surfaceDiameterMultiplier = 1.14f;
        [SerializeField, Min(0f)] private float pourLipHeightOffset = 0.014f;
        [SerializeField, Range(0.15f, 0.65f)] private float pourLipXNormalized = 0.52f;
        [SerializeField, Range(0f, 0.35f)] private float pourLipZNormalized = 0.1f;

        private readonly List<BoxCollider> containmentColliders = new List<BoxCollider>();
        private float emissionAccumulator;
        private bool isPouring;
        private float containedLiquidNormalized = 1f;
        private Vector3 localFloorCenter;
        private Vector3 localInteriorSize = new Vector3(0.14f, 0.06f, 0.14f);
        private float localFloorTopY;
        private float localRimTopY = 0.08f;

        private void Awake()
        {
            EnsureRuntimeSetup();
        }

        private void Update()
        {
            if (!isPouring || liquidPool == null || pourOrigin == null)
            {
                return;
            }

            emissionAccumulator += emissionRate * Time.deltaTime;

            while (emissionAccumulator >= 1f)
            {
                emissionAccumulator -= 1f;
                EmitDroplet();
            }
        }

        public void Configure(GoldLiquidPool pool, Transform origin, Transform target)
        {
            liquidPool = pool;
            pourReferencePoint = origin;
            pourTarget = target;
            EnsureRuntimeSetup();
        }

        public void StartPouring()
        {
            EnsureRuntimeSetup();
            isPouring = true;
            emissionAccumulator = 0f;
            SetContainedLiquidVisible(true);
        }

        public void StopPouring()
        {
            isPouring = false;
            emissionAccumulator = 0f;
        }

        public void ResetSource()
        {
            StopPouring();
            SetContainedLiquidNormalized(1f);
        }

        public void SetContainedLiquidNormalized(float normalizedValue)
        {
            containedLiquidNormalized = Mathf.Clamp01(normalizedValue);
            EnsureRuntimeSetup();
            UpdateContainedLiquidVisual();
        }

        private void EmitDroplet()
        {
            var droplet = liquidPool.Rent();

            if (droplet == null)
            {
                return;
            }

            var origin = pourOrigin.position;
            var direction = ResolvePourDirection(origin);
            var spreadOffset = Random.insideUnitSphere * dropletSpread;
            spreadOffset.y = Mathf.Abs(spreadOffset.y) * 0.2f;

            var launchVelocity = (direction + spreadOffset).normalized * dropletForce;
            droplet.Launch(origin, launchVelocity, dropletLifetime, dropletAmount);
        }

        private Vector3 ResolvePourDirection(Vector3 origin)
        {
            if (pourTarget != null)
            {
                var toTarget = pourTarget.position - origin;

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            var fallbackDirection = (transform.forward * 0.35f) + Vector3.down;
            return fallbackDirection.normalized;
        }

        private void EnsureRuntimeSetup()
        {
            if (liquidPool == null)
            {
                liquidPool = GetComponentInChildren<GoldLiquidPool>(includeInactive: true);

                if (liquidPool == null)
                {
                    var poolObject = new GameObject("SceneFinal_GoldLiquidPool");
                    poolObject.transform.SetParent(transform, false);
                    liquidPool = poolObject.AddComponent<GoldLiquidPool>();
                }
            }

            liquidPool.EnsurePoolReady();

            if (pourOrigin == null)
            {
                pourOrigin = transform.Find("SceneFinal_BowlPourOrigin");

                if (pourOrigin == null)
                {
                    var pourOriginObject = new GameObject("SceneFinal_BowlPourOrigin");
                    pourOriginObject.transform.SetParent(transform, false);
                    pourOrigin = pourOriginObject.transform;
                }
            }

            if (moltenSurfaceTransform == null)
            {
                moltenSurfaceTransform = transform.Find("SceneFinal_BowlMoltenSurface");

                if (moltenSurfaceTransform == null)
                {
                    var moltenSurfaceObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    moltenSurfaceObject.name = "SceneFinal_BowlMoltenSurface";
                    moltenSurfaceObject.transform.SetParent(transform, false);

                    if (moltenSurfaceObject.TryGetComponent<Collider>(out var collider))
                    {
                        Destroy(collider);
                    }

                    moltenSurfaceTransform = moltenSurfaceObject.transform;
                    moltenSurfaceRenderer = moltenSurfaceObject.GetComponent<Renderer>();
                }
            }

            if (moltenSurfaceRenderer == null && moltenSurfaceTransform != null)
            {
                moltenSurfaceRenderer = moltenSurfaceTransform.GetComponent<Renderer>();
            }

            if (moltenSurfaceRenderer != null)
            {
                moltenSurfaceRenderer.sharedMaterial = FinalIngotRuntimeMaterials.GetMoltenGoldMaterial();
                moltenSurfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
                moltenSurfaceRenderer.receiveShadows = true;
            }

            RefreshContainedVolumeLayout();
            CalibratePourOrigin();
            UpdateContainedLiquidVisual();
        }

        private void UpdateContainedLiquidVisual()
        {
            if (moltenSurfaceTransform == null)
            {
                return;
            }

            var fillDiameterX = Mathf.Max(0.035f, localInteriorSize.x * Mathf.Lerp(0.62f, 0.99f, containedLiquidNormalized) * surfaceDiameterMultiplier);
            var fillDiameterZ = Mathf.Max(0.035f, localInteriorSize.z * Mathf.Lerp(0.62f, 0.99f, containedLiquidNormalized) * surfaceDiameterMultiplier);
            var fillThickness = Mathf.Max(0.004f, Mathf.Lerp(surfaceThickness * 0.55f, surfaceThickness, containedLiquidNormalized));
            var surfaceCenterY = ResolveSurfaceCenterY(fillThickness);

            moltenSurfaceTransform.localPosition = new Vector3(
                localFloorCenter.x,
                surfaceCenterY,
                localFloorCenter.z);
            moltenSurfaceTransform.localRotation = Quaternion.identity;
            moltenSurfaceTransform.localScale = new Vector3(fillDiameterX, fillThickness, fillDiameterZ);

            SetContainedLiquidVisible(containedLiquidNormalized > 0.01f);
            CalibratePourOrigin();
        }

        private void SetContainedLiquidVisible(bool shouldBeVisible)
        {
            if (moltenSurfaceRenderer != null)
            {
                moltenSurfaceRenderer.enabled = shouldBeVisible;
            }
        }

        private void RefreshContainedVolumeLayout()
        {
            containmentColliders.Clear();
            containmentColliders.AddRange(GetComponents<BoxCollider>());

            var floorCollider = ResolveFloorCollider();

            if (floorCollider != null)
            {
                var rimTop = ResolveRimTopY();
                var floorTop = floorCollider.center.y + (floorCollider.size.y * 0.5f) + liquidInset;
                var interiorHeight = Mathf.Max(0.04f, rimTop - floorTop);

                localFloorCenter = new Vector3(floorCollider.center.x, 0f, floorCollider.center.z);
                localFloorTopY = floorTop;
                localRimTopY = rimTop;
                localInteriorSize = new Vector3(
                    Mathf.Max(0.06f, floorCollider.size.x - (liquidInset * 2f)),
                    interiorHeight,
                    Mathf.Max(0.06f, floorCollider.size.z - (liquidInset * 2f)));

                return;
            }

            if (TryGetComponent<MeshFilter>(out var meshFilter) && meshFilter.sharedMesh != null)
            {
                var meshBounds = meshFilter.sharedMesh.bounds;
                localFloorCenter = new Vector3(meshBounds.center.x, 0f, meshBounds.center.z);
                localFloorTopY = meshBounds.min.y + (meshBounds.size.y * 0.22f);
                localRimTopY = meshBounds.min.y + (meshBounds.size.y * 0.7f);
                localInteriorSize = new Vector3(
                    Mathf.Max(0.06f, meshBounds.size.x * 0.7f),
                    Mathf.Max(0.04f, localRimTopY - localFloorTopY),
                    Mathf.Max(0.06f, meshBounds.size.z * 0.7f));
            }
        }

        private void CalibratePourOrigin()
        {
            if (pourOrigin == null)
            {
                return;
            }

            var fillThickness = Mathf.Max(0.004f, Mathf.Lerp(surfaceThickness * 0.55f, surfaceThickness, containedLiquidNormalized));
            var surfaceCenterY = ResolveSurfaceCenterY(fillThickness);
            var defaultLocalPosition = new Vector3(
                localFloorCenter.x + (localInteriorSize.x * Mathf.Max(pourLipXNormalized, 0.5f)),
                localRimTopY + pourLipHeightOffset,
                localFloorCenter.z + (localInteriorSize.z * pourLipZNormalized));

            if (pourReferencePoint != null)
            {
                var referenceLocalPoint = transform.InverseTransformPoint(pourReferencePoint.position);
                var maxX = localFloorCenter.x + (localInteriorSize.x * 0.6f);
                var minX = localFloorCenter.x + (localInteriorSize.x * 0.28f);
                var minY = Mathf.Max(surfaceCenterY, localRimTopY - 0.006f);
                var zHalfExtent = localInteriorSize.z * 0.28f;

                defaultLocalPosition = new Vector3(
                    Mathf.Clamp(referenceLocalPoint.x, minX, maxX),
                    Mathf.Clamp(referenceLocalPoint.y, minY, localRimTopY + pourLipHeightOffset),
                    Mathf.Clamp(referenceLocalPoint.z, localFloorCenter.z - zHalfExtent, localFloorCenter.z + zHalfExtent));
            }

            pourOrigin.localPosition = defaultLocalPosition;
            pourOrigin.localRotation = Quaternion.identity;
        }

        private BoxCollider ResolveFloorCollider()
        {
            BoxCollider bestCandidate = null;
            var bestScore = float.MinValue;

            foreach (var collider in containmentColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                var footprint = collider.size.x * collider.size.z;
                var thinness = 1f / Mathf.Max(0.001f, collider.size.y);
                var score = footprint * thinness;

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestCandidate = collider;
            }

            return bestCandidate;
        }

        private float ResolveRimTopY()
        {
            var rimTop = float.MinValue;

            foreach (var collider in containmentColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                rimTop = Mathf.Max(rimTop, collider.center.y + (collider.size.y * 0.5f));
            }

            return rimTop > float.MinValue
                ? rimTop - liquidInset
                : 0.08f;
        }

        private float ResolveSurfaceCenterY(float fillThickness)
        {
            var minSurfaceCenterY = localFloorTopY + (fillThickness * 0.5f);
            var maxSurfaceCenterY = localFloorTopY + (localInteriorSize.y * fullLiquidHeightNormalized);
            return Mathf.Lerp(minSurfaceCenterY, maxSurfaceCenterY, containedLiquidNormalized);
        }
    }
}
