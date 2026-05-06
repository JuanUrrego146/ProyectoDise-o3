using System;
using UnityEngine;

namespace LingoteRush.Systems.Extraction
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class GoldNugget : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float mass = 0.0075f;
        [SerializeField, Min(0.001f)] private float diameter = 0.02f;
        [SerializeField] private int sequenceIndex;
        [SerializeField] private bool hasReportedLanding;
        [SerializeField] private bool isCollected;

        private Rigidbody cachedRigidbody;
        private SphereCollider cachedCollider;

        public event Action<GoldNugget, Collision> Landed;

        public Rigidbody Rigidbody => cachedRigidbody;

        public int SequenceIndex => sequenceIndex;

        public bool IsCollected => isCollected;

        private void Awake()
        {
            CacheComponents();
        }

        public void Configure(
            int nuggetSequence,
            float nuggetMass,
            float nuggetDiameter,
            PhysicsMaterial physicsMaterial = null,
            Material renderMaterial = null)
        {
            CacheComponents();

            sequenceIndex = nuggetSequence;
            mass = Mathf.Clamp(nuggetMass, 0.005f, 0.01f);
            diameter = Mathf.Clamp(nuggetDiameter, 0.015f, 0.025f);
            hasReportedLanding = false;
            isCollected = false;
            name = $"GoldNugget_{sequenceIndex:000}";
            transform.localScale = Vector3.one * diameter;

            cachedCollider.center = Vector3.zero;
            cachedCollider.radius = 0.5f;

            if (physicsMaterial != null)
            {
                cachedCollider.sharedMaterial = physicsMaterial;
            }

            cachedCollider.contactOffset = 0.001f;

            cachedRigidbody.mass = mass;
            cachedRigidbody.useGravity = true;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            cachedRigidbody.linearDamping = 0f;
            cachedRigidbody.angularDamping = 0.05f;
            cachedRigidbody.maxLinearVelocity = 4f;
            cachedRigidbody.maxAngularVelocity = 30f;
            cachedRigidbody.solverIterations = 12;
            cachedRigidbody.solverVelocityIterations = 6;

            if (renderMaterial != null && TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = renderMaterial;
            }
        }

        public void MarkCollected()
        {
            isCollected = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasReportedLanding || collision.contactCount == 0)
            {
                return;
            }

            if (collision.relativeVelocity.magnitude < 0.04f)
            {
                return;
            }

            hasReportedLanding = true;
            Landed?.Invoke(this, collision);
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
    }
}
