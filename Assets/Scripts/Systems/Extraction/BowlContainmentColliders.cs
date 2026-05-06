using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class BowlContainmentColliders : MonoBehaviour
    {
        private const int WallColliderCount = 8;

        [SerializeField] private BoxCollider managedBaseCollider;
        [SerializeField] private BoxCollider[] managedWallColliders = new BoxCollider[WallColliderCount];
        [SerializeField] private PhysicsMaterial colliderMaterial;
        [SerializeField, Range(0.55f, 0.9f)] private float innerRadiusFactor = 0.72f;
        [SerializeField, Range(0.1f, 0.35f)] private float floorThicknessFactor = 0.18f;
        [SerializeField, Range(0.6f, 1.5f)] private float wallHeightFactor = 1.05f;
        [SerializeField, Range(0.08f, 0.35f)] private float wallThicknessFactor = 0.18f;
        [SerializeField, Min(0f)] private float rimPadding = 0.012f;
        [SerializeField] private bool disableMeshCollider = true;

        private void OnEnable()
        {
            ConfigureContainment();
        }

        private void OnValidate()
        {
            ConfigureContainment();
        }

        private void ConfigureContainment()
        {
            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            if (!TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            {
                return;
            }

            if (disableMeshCollider && TryGetComponent<MeshCollider>(out var meshCollider))
            {
                meshCollider.enabled = false;
            }

            EnsureManagedColliders();
            ApplyLayout(meshFilter.sharedMesh.bounds);
        }

        private void EnsureManagedColliders()
        {
            if (managedBaseCollider == null)
            {
                managedBaseCollider = gameObject.AddComponent<BoxCollider>();
            }

            ConfigureColliderDefaults(managedBaseCollider);

            if (managedWallColliders == null || managedWallColliders.Length != WallColliderCount)
            {
                managedWallColliders = new BoxCollider[WallColliderCount];
            }

            for (var index = 0; index < managedWallColliders.Length; index++)
            {
                if (managedWallColliders[index] == null)
                {
                    managedWallColliders[index] = gameObject.AddComponent<BoxCollider>();
                }

                ConfigureColliderDefaults(managedWallColliders[index]);
            }
        }

        private void ConfigureColliderDefaults(BoxCollider collider)
        {
            collider.enabled = true;
            collider.isTrigger = false;
            collider.sharedMaterial = colliderMaterial;
        }

        private void ApplyLayout(Bounds bounds)
        {
            var innerRadiusX = Mathf.Max(bounds.extents.x * innerRadiusFactor, 0.03f);
            var innerRadiusZ = Mathf.Max(bounds.extents.z * innerRadiusFactor, 0.03f);
            var floorThickness = Mathf.Max(bounds.size.y * floorThicknessFactor, 0.01f);
            var wallHeight = Mathf.Max(bounds.size.y * wallHeightFactor + rimPadding, 0.03f);
            var wallThickness = Mathf.Max(Mathf.Min(bounds.extents.x, bounds.extents.z) * wallThicknessFactor, 0.008f);

            var floorY = bounds.min.y + floorThickness * 0.6f;
            var wallCenterY = floorY + wallHeight * 0.5f;
            var northSouthLength = innerRadiusX * 1.78f;
            var eastWestLength = innerRadiusZ * 1.78f;
            var cardinalRadiusX = innerRadiusX + wallThickness * 0.5f;
            var cardinalRadiusZ = innerRadiusZ + wallThickness * 0.5f;
            var cornerCenterX = innerRadiusX * 0.78f;
            var cornerCenterZ = innerRadiusZ * 0.78f;
            var cornerThickness = wallThickness * 1.22f;

            ConfigureCollider(
                managedBaseCollider,
                new Vector3(0f, floorY, 0f),
                new Vector3(innerRadiusX * 1.92f, floorThickness, innerRadiusZ * 1.92f));

            ConfigureCollider(
                managedWallColliders[0],
                new Vector3(0f, wallCenterY, cardinalRadiusZ),
                new Vector3(northSouthLength, wallHeight, wallThickness));

            ConfigureCollider(
                managedWallColliders[1],
                new Vector3(0f, wallCenterY, -cardinalRadiusZ),
                new Vector3(northSouthLength, wallHeight, wallThickness));

            ConfigureCollider(
                managedWallColliders[2],
                new Vector3(cardinalRadiusX, wallCenterY, 0f),
                new Vector3(wallThickness, wallHeight, eastWestLength));

            ConfigureCollider(
                managedWallColliders[3],
                new Vector3(-cardinalRadiusX, wallCenterY, 0f),
                new Vector3(wallThickness, wallHeight, eastWestLength));

            ConfigureCollider(
                managedWallColliders[4],
                new Vector3(cornerCenterX, wallCenterY, cornerCenterZ),
                new Vector3(cornerThickness, wallHeight, cornerThickness));

            ConfigureCollider(
                managedWallColliders[5],
                new Vector3(-cornerCenterX, wallCenterY, cornerCenterZ),
                new Vector3(cornerThickness, wallHeight, cornerThickness));

            ConfigureCollider(
                managedWallColliders[6],
                new Vector3(cornerCenterX, wallCenterY, -cornerCenterZ),
                new Vector3(cornerThickness, wallHeight, cornerThickness));

            ConfigureCollider(
                managedWallColliders[7],
                new Vector3(-cornerCenterX, wallCenterY, -cornerCenterZ),
                new Vector3(cornerThickness, wallHeight, cornerThickness));
        }

        private static void ConfigureCollider(BoxCollider collider, Vector3 center, Vector3 size)
        {
            collider.center = center;
            collider.size = size;
        }
    }
}
