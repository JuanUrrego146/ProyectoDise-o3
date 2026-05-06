using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LingoteRush.Systems.FinalIngot
{
    [DisallowMultipleComponent]
    public sealed class GoldLiquidPool : MonoBehaviour
    {
        [SerializeField, Min(1)] private int initialSize = 96;
        [SerializeField] private Vector3 dropletScale = new Vector3(0.02f, 0.02f, 0.02f);
        [SerializeField] private Material dropletMaterial;

        private readonly Queue<GoldLiquidDroplet> availableDroplets = new Queue<GoldLiquidDroplet>();
        private readonly List<GoldLiquidDroplet> allDroplets = new List<GoldLiquidDroplet>();

        private void Awake()
        {
            EnsurePoolReady();
        }

        public void EnsurePoolReady()
        {
            while (allDroplets.Count < initialSize)
            {
                CreateDropletInstance();
            }
        }

        public GoldLiquidDroplet Rent()
        {
            if (availableDroplets.Count == 0)
            {
                CreateDropletInstance();
            }

            return availableDroplets.Dequeue();
        }

        public void Return(GoldLiquidDroplet droplet)
        {
            if (droplet == null)
            {
                return;
            }

            droplet.transform.SetParent(transform, true);
            droplet.gameObject.SetActive(false);
            availableDroplets.Enqueue(droplet);
        }

        private void CreateDropletInstance()
        {
            var dropletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dropletObject.name = "GoldLiquidDroplet";
            dropletObject.transform.SetParent(transform, false);
            dropletObject.transform.localScale = dropletScale;

            var droplet = dropletObject.AddComponent<GoldLiquidDroplet>();

            if (dropletObject.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = dropletMaterial != null
                    ? dropletMaterial
                    : FinalIngotRuntimeMaterials.GetMoltenGoldMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            var rigidbody = dropletObject.GetComponent<Rigidbody>();
            rigidbody.mass = 0.01f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.linearDamping = 0.02f;
            rigidbody.angularDamping = 0.08f;

            droplet.Initialize(this);
            droplet.SetVisual(renderer != null ? renderer.sharedMaterial : null, dropletScale);

            dropletObject.SetActive(false);

            allDroplets.Add(droplet);
            availableDroplets.Enqueue(droplet);
        }
    }
}
