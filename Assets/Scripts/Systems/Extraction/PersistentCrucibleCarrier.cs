using System.Collections.Generic;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PersistentCrucibleCarrier : MonoBehaviour
    {
        private const int MaxOverlapResults = 64;

        private static readonly Collider[] OverlapResults = new Collider[MaxOverlapResults];

        [Header("Contained Nugget Capture")]
        [SerializeField, Min(0f)] private float captureInset = 0.01f;
        [SerializeField, Min(0f)] private float captureFloorLift = 0.01f;
        [SerializeField, Min(0f)] private float captureTopPadding = 0.005f;
        [SerializeField] private bool logCaptureDetails;

        private readonly List<GoldNugget> carriedNuggets = new List<GoldNugget>();

        private BoxCollider[] cachedContainmentColliders;
        private Rigidbody cachedRigidbody;
        private BowlHorizontalMover cachedHorizontalMover;

        public static PersistentCrucibleCarrier ActiveCarrier { get; private set; }

        public IReadOnlyList<GoldNugget> CarriedNuggets => carriedNuggets;

        public int CarriedNuggetCount => carriedNuggets.Count;

        public static PersistentCrucibleCarrier EnsureRegistered(GameObject bowlObject)
        {
            if (ActiveCarrier != null)
            {
                return ActiveCarrier;
            }

            if (bowlObject == null)
            {
                Debug.LogWarning("Persistent Bowl 2 could not be registered because Bowl 2 was not found.");
                return null;
            }

            var carrier = bowlObject.GetComponent<PersistentCrucibleCarrier>();

            if (carrier == null)
            {
                carrier = bowlObject.AddComponent<PersistentCrucibleCarrier>();
            }

            return carrier;
        }

        private void Awake()
        {
            if (ActiveCarrier != null && ActiveCarrier != this)
            {
                Debug.LogWarning(
                    $"A duplicate PersistentCrucibleCarrier was found on '{gameObject.name}'. The existing Bowl 2 carrier will remain active.");
                gameObject.SetActive(false);
                enabled = false;
                return;
            }

            CacheComponents();
            RegisterExistingChildNuggets();

            ActiveCarrier = this;
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            Debug.Log("Persistent Bowl 2 registered", this);
        }

        private void OnDestroy()
        {
            if (ActiveCarrier == this)
            {
                ActiveCarrier = null;
            }
        }

        public static void PrepareActiveCarrierForSceneTransition()
        {
            ActiveCarrier?.PrepareForSceneTransition();
        }

        public void PrepareForSceneTransition()
        {
            CacheComponents();
            RegisterExistingChildNuggets();
            CaptureContainedNuggets();
            FreezeCarrier();
        }

        public void PrepareForSmeltingTransport()
        {
            PrepareForSceneTransition();

            if (cachedHorizontalMover != null)
            {
                cachedHorizontalMover.enabled = false;
            }
        }

        private void CacheComponents()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            if (cachedHorizontalMover == null)
            {
                TryGetComponent(out cachedHorizontalMover);
            }

            cachedContainmentColliders = GetComponents<BoxCollider>();
        }

        private void RegisterExistingChildNuggets()
        {
            CleanupMissingNuggets();

            foreach (var nugget in GetComponentsInChildren<GoldNugget>(includeInactive: true))
            {
                RegisterCarriedNugget(nugget);
            }
        }

        private void CaptureContainedNuggets()
        {
            CleanupMissingNuggets();

            if (!TryGetCaptureBox(out var worldCenter, out var halfExtents, out var orientation))
            {
                return;
            }

            var overlapCount = Physics.OverlapBoxNonAlloc(
                worldCenter,
                halfExtents,
                OverlapResults,
                orientation,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (var index = 0; index < overlapCount; index++)
            {
                var overlappedCollider = OverlapResults[index];
                OverlapResults[index] = null;

                if (!TryResolveNugget(overlappedCollider, out var nugget))
                {
                    continue;
                }

                CarryNugget(nugget);
            }

            if (logCaptureDetails)
            {
                Debug.Log($"PersistentCrucibleCarrier captured {carriedNuggets.Count} nugget(s) for Bowl 2.");
            }
        }

        private void CarryNugget(GoldNugget nugget)
        {
            if (nugget == null)
            {
                return;
            }

            if (nugget.Rigidbody != null)
            {
                nugget.Rigidbody.linearVelocity = Vector3.zero;
                nugget.Rigidbody.angularVelocity = Vector3.zero;
                nugget.Rigidbody.useGravity = false;
                nugget.Rigidbody.isKinematic = true;
            }

            nugget.MarkCollected();
            nugget.transform.SetParent(transform, true);
            RegisterCarriedNugget(nugget);
        }

        private void RegisterCarriedNugget(GoldNugget nugget)
        {
            if (nugget == null || carriedNuggets.Contains(nugget))
            {
                return;
            }

            carriedNuggets.Add(nugget);
        }

        private void CleanupMissingNuggets()
        {
            for (var index = carriedNuggets.Count - 1; index >= 0; index--)
            {
                if (carriedNuggets[index] == null)
                {
                    carriedNuggets.RemoveAt(index);
                }
            }
        }

        private void FreezeCarrier()
        {
            if (cachedRigidbody == null)
            {
                return;
            }

            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.isKinematic = true;
        }

        private bool TryGetCaptureBox(out Vector3 worldCenter, out Vector3 halfExtents, out Quaternion orientation)
        {
            worldCenter = transform.position;
            halfExtents = Vector3.one * 0.05f;
            orientation = transform.rotation;

            if (cachedContainmentColliders == null || cachedContainmentColliders.Length == 0)
            {
                return false;
            }

            var hasBounds = false;
            var localMin = Vector3.zero;
            var localMax = Vector3.zero;

            foreach (var containedCollider in cachedContainmentColliders)
            {
                if (containedCollider == null || !containedCollider.enabled)
                {
                    continue;
                }

                var colliderExtents = containedCollider.size * 0.5f;
                var colliderMin = containedCollider.center - colliderExtents;
                var colliderMax = containedCollider.center + colliderExtents;

                if (!hasBounds)
                {
                    localMin = colliderMin;
                    localMax = colliderMax;
                    hasBounds = true;
                    continue;
                }

                localMin = Vector3.Min(localMin, colliderMin);
                localMax = Vector3.Max(localMax, colliderMax);
            }

            if (!hasBounds)
            {
                return false;
            }

            var localSize = localMax - localMin;
            var localCenter = (localMin + localMax) * 0.5f;

            localSize.x = Mathf.Max(0.02f, localSize.x - (captureInset * 2f));
            localSize.z = Mathf.Max(0.02f, localSize.z - (captureInset * 2f));
            localSize.y = Mathf.Max(0.02f, localSize.y - captureFloorLift - captureTopPadding);
            localCenter.y += (captureTopPadding - captureFloorLift) * 0.5f;

            worldCenter = transform.TransformPoint(localCenter);
            halfExtents = Vector3.Scale(localSize * 0.5f, GetAbsoluteLossyScale());
            orientation = transform.rotation;
            return true;
        }

        private static bool TryResolveNugget(Collider overlappedCollider, out GoldNugget nugget)
        {
            nugget = null;

            if (overlappedCollider == null)
            {
                return false;
            }

            if (overlappedCollider.TryGetComponent(out nugget))
            {
                return true;
            }

            if (overlappedCollider.attachedRigidbody != null
                && overlappedCollider.attachedRigidbody.TryGetComponent(out nugget))
            {
                return true;
            }

            return false;
        }

        private Vector3 GetAbsoluteLossyScale()
        {
            var scale = transform.lossyScale;
            return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
    }
}
