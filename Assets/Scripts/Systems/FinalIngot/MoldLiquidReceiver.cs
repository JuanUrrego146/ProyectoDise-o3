using System;
using UnityEngine;

namespace LingoteRush.Systems.FinalIngot
{
    [DisallowMultipleComponent]
    public sealed class MoldLiquidReceiver : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Collider liquidReceiveTrigger;
        [SerializeField] private Transform moldInnerGoldVisual;
        [SerializeField] private Renderer moldInnerGoldRenderer;
        [SerializeField, Min(1f)] private float requiredLiquidAmount = 36f;
        [SerializeField] private Vector3 emptyScale = new Vector3(0.16f, 0.002f, 0.1f);
        [SerializeField] private Vector3 fullScale = new Vector3(0.22f, 0.055f, 0.14f);
        [SerializeField] private float innerGoldBottomOffset = -0.008f;
        [SerializeField] private Vector3 receiveTriggerCenter = new Vector3(0f, 0.03f, 0f);
        [SerializeField] private Vector3 receiveTriggerSize = new Vector3(0.24f, 0.08f, 0.16f);

        private MaterialPropertyBlock innerGoldBlock;
        private float currentLiquidAmount;
        private bool fillReached;

        public event Action Filled;

        public Collider LiquidReceiveTrigger => liquidReceiveTrigger;

        public Transform MoldInnerGoldVisual => moldInnerGoldVisual;

        public Renderer MoldInnerGoldRenderer => moldInnerGoldRenderer;

        public float FillNormalized => requiredLiquidAmount <= 0f
            ? 0f
            : Mathf.Clamp01(currentLiquidAmount / requiredLiquidAmount);

        public bool IsFull => FillNormalized >= 0.999f;

        private void Awake()
        {
            EnsureMaterialBlock();
            EnsureSetup();
            ResetReceiver();
        }

        public bool TryReceiveDroplet(GoldLiquidDroplet droplet)
        {
            if (droplet == null)
            {
                return false;
            }

            EnsureSetup();

            currentLiquidAmount = Mathf.Clamp(currentLiquidAmount + droplet.LiquidAmount, 0f, requiredLiquidAmount);
            UpdateInnerGoldVisual();

            if (!fillReached && IsFull)
            {
                fillReached = true;
                Filled?.Invoke();
            }

            return true;
        }

        public void ResetReceiver()
        {
            EnsureSetup();
            currentLiquidAmount = 0f;
            fillReached = false;
            UpdateInnerGoldVisual();
            SetHeatNormalized(1f);
        }

        public void CompleteFill()
        {
            currentLiquidAmount = requiredLiquidAmount;
            UpdateInnerGoldVisual();

            if (!fillReached)
            {
                fillReached = true;
                Filled?.Invoke();
            }
        }

        public void HideInnerGold()
        {
            if (moldInnerGoldRenderer != null)
            {
                moldInnerGoldRenderer.enabled = false;
            }
        }

        public void SetHeatNormalized(float normalizedHeat)
        {
            if (moldInnerGoldRenderer == null)
            {
                return;
            }

            EnsureMaterialBlock();

            var clampedHeat = Mathf.Clamp01(normalizedHeat);
            var baseColor = Color.Lerp(new Color(0.72f, 0.49f, 0.12f, 1f), new Color(1f, 0.68f, 0.1f, 1f), clampedHeat);
            var emission = Color.Lerp(new Color(0.15f, 0.09f, 0.02f, 1f), new Color(1.8f, 0.72f, 0.1f, 1f), clampedHeat);

            innerGoldBlock.Clear();
            innerGoldBlock.SetColor(BaseColorId, baseColor);
            innerGoldBlock.SetColor(EmissionColorId, emission);
            moldInnerGoldRenderer.SetPropertyBlock(innerGoldBlock);
        }

        private void EnsureMaterialBlock()
        {
            if (innerGoldBlock == null)
            {
                innerGoldBlock = new MaterialPropertyBlock();
            }
        }

        private void EnsureSetup()
        {
            if (moldInnerGoldVisual == null)
            {
                moldInnerGoldVisual = transform.Find("SceneFinal_Mold_InnerGold");

                if (moldInnerGoldVisual == null)
                {
                    var innerGoldObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    innerGoldObject.name = "SceneFinal_Mold_InnerGold";
                    innerGoldObject.transform.SetParent(transform, false);

                    if (innerGoldObject.TryGetComponent<Collider>(out var collider))
                    {
                        Destroy(collider);
                    }

                    moldInnerGoldVisual = innerGoldObject.transform;
                    moldInnerGoldRenderer = innerGoldObject.GetComponent<Renderer>();
                }
            }

            if (moldInnerGoldRenderer == null && moldInnerGoldVisual != null)
            {
                moldInnerGoldRenderer = moldInnerGoldVisual.GetComponent<Renderer>();
            }

            if (moldInnerGoldRenderer != null)
            {
                moldInnerGoldRenderer.sharedMaterial = FinalIngotRuntimeMaterials.GetMoltenGoldMaterial();
            }

            if (liquidReceiveTrigger == null)
            {
                var triggerTransform = transform.Find("LiquidReceiveTrigger");

                if (triggerTransform == null)
                {
                    var triggerObject = new GameObject("LiquidReceiveTrigger");
                    triggerObject.transform.SetParent(transform, false);
                    triggerTransform = triggerObject.transform;
                }

                liquidReceiveTrigger = triggerTransform.GetComponent<BoxCollider>();

                if (liquidReceiveTrigger == null)
                {
                    liquidReceiveTrigger = triggerTransform.gameObject.AddComponent<BoxCollider>();
                }
            }

            liquidReceiveTrigger.isTrigger = true;
            liquidReceiveTrigger.transform.localPosition = receiveTriggerCenter;
            liquidReceiveTrigger.transform.localRotation = Quaternion.identity;

            if (liquidReceiveTrigger is BoxCollider boxCollider)
            {
                boxCollider.size = receiveTriggerSize;
                boxCollider.center = Vector3.zero;
            }
        }

        private void UpdateInnerGoldVisual()
        {
            if (moldInnerGoldVisual == null)
            {
                return;
            }

            var fillNormalized = FillNormalized;
            var resolvedScale = Vector3.Lerp(emptyScale, fullScale, fillNormalized);
            moldInnerGoldVisual.localScale = resolvedScale;
            moldInnerGoldVisual.localPosition = new Vector3(
                0f,
                innerGoldBottomOffset + (resolvedScale.y * 0.5f),
                0f);
            moldInnerGoldVisual.localRotation = Quaternion.identity;

            if (moldInnerGoldRenderer != null)
            {
                moldInnerGoldRenderer.enabled = fillNormalized > 0.005f;
            }
        }
    }
}
