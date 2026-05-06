using UnityEngine;

namespace LingoteRush.Systems.FinalIngot
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class GoldLiquidDroplet : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float liquidAmount = 1f;
        [SerializeField] private float recycleHeight = -5f;

        private Rigidbody cachedRigidbody;
        private SphereCollider cachedCollider;
        private GoldLiquidPool ownerPool;
        private float recycleTime;
        private bool isActiveDroplet;

        public float LiquidAmount => liquidAmount;

        private void Awake()
        {
            CacheComponents();
            PrepareForPooling();
        }

        private void Update()
        {
            if (!isActiveDroplet)
            {
                return;
            }

            if (Time.time >= recycleTime || transform.position.y <= recycleHeight)
            {
                Recycle();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveDroplet || other == null)
            {
                return;
            }

            var receiver = other.GetComponent<MoldLiquidReceiver>() ?? other.GetComponentInParent<MoldLiquidReceiver>();

            if (receiver == null)
            {
                return;
            }

            if (receiver.TryReceiveDroplet(this))
            {
                Recycle();
            }
        }

        internal void Initialize(GoldLiquidPool poolOwner)
        {
            ownerPool = poolOwner;
            CacheComponents();
            PrepareForPooling();
        }

        internal void Launch(Vector3 worldPosition, Vector3 initialVelocity, float lifetime, float amount)
        {
            CacheComponents();

            transform.SetParent(ownerPool != null ? ownerPool.transform : null, true);
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;

            liquidAmount = Mathf.Max(0.001f, amount);
            recycleTime = Time.time + Mathf.Max(0.1f, lifetime);
            isActiveDroplet = true;

            cachedCollider.enabled = true;
            cachedRigidbody.isKinematic = false;
            cachedRigidbody.useGravity = true;
            cachedRigidbody.linearVelocity = initialVelocity;
            cachedRigidbody.angularVelocity = Random.insideUnitSphere * 6f;

            gameObject.SetActive(true);
        }

        internal void SetVisual(Material sharedMaterial, Vector3 localScale)
        {
            if (TryGetComponent<Renderer>(out var renderer) && sharedMaterial != null)
            {
                renderer.sharedMaterial = sharedMaterial;
            }

            transform.localScale = localScale;
        }

        public void Recycle()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            PrepareForPooling();
            ownerPool?.Return(this);
        }

        private void CacheComponents()
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            if (cachedCollider == null)
            {
                cachedCollider = GetComponent<SphereCollider>();
            }
        }

        private void PrepareForPooling()
        {
            isActiveDroplet = false;

            if (cachedRigidbody != null)
            {
                cachedRigidbody.linearVelocity = Vector3.zero;
                cachedRigidbody.angularVelocity = Vector3.zero;
                cachedRigidbody.isKinematic = true;
                cachedRigidbody.useGravity = false;
            }

            if (cachedCollider != null)
            {
                cachedCollider.enabled = true;
                cachedCollider.isTrigger = false;
                cachedCollider.contactOffset = 0.001f;
            }
        }
    }
}
