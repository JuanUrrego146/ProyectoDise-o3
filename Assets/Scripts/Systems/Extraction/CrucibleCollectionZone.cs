using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CrucibleCollectionZone : MonoBehaviour
    {
        private CrucibleController owner;
        private BoxCollider triggerCollider;

        private void Awake()
        {
            EnsureTriggerCollider();
        }

        public void Configure(CrucibleController resolvedOwner, Vector3 triggerSize, Vector3 localPosition)
        {
            owner = resolvedOwner;
            EnsureTriggerCollider();
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            triggerCollider.isTrigger = true;
            triggerCollider.size = triggerSize;
            triggerCollider.center = Vector3.zero;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (owner == null || !other.TryGetComponent<GoldNugget>(out var nugget))
            {
                return;
            }

            owner.TryRegisterCollectedNugget(nugget);
        }

        private void EnsureTriggerCollider()
        {
            if (!TryGetComponent<BoxCollider>(out triggerCollider))
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
            }

            triggerCollider.isTrigger = true;
        }
    }
}
