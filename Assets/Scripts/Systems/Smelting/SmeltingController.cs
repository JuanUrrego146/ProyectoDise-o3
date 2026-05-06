using System.Collections;
using System.Collections.Generic;
using LingoteRush.Systems.Extraction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LingoteRush.Systems.Smelting
{
    public sealed class SmeltingController : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] private Transform entryPoint;
        [SerializeField] private Transform insideTarget;
        [SerializeField] private Transform fireVfxRoot;
        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer furnaceGlowRenderer;
        [SerializeField] private Renderer moltenGoldRenderer;
        [SerializeField] private Image heatBarFill;
        [SerializeField] private Image micLevelFill;
        [SerializeField] private Text statusText;
        [SerializeField] private MicrophoneInputController microphoneInput;

        [Header("Smelting Settings")]
        [SerializeField, Min(0.01f)] private float moveDuration = 2f;
        [SerializeField, Min(0.01f)] private float heatGainRate = 0.85f;
        [SerializeField, Min(0.01f)] private float heatDecayRate = 1.35f;
        [SerializeField, Min(0.1f)] private float requiredHeatToMelt = 24f;
        [SerializeField, Min(0.1f)] private float fireIntensityMultiplier = 2f;
        [SerializeField, Min(0f)] private float minFireLightIntensity = 0.8f;
        [SerializeField, Min(0f)] private float maxFireLightIntensity = 4.5f;
        [SerializeField, Min(0f)] private float minFireLightRange = 2f;
        [SerializeField, Min(0f)] private float maxFireLightRange = 6f;
        [SerializeField, Min(0f)] private float minFireVfxSpeed = 0.65f;
        [SerializeField, Min(0f)] private float maxFireVfxSpeed = 2f;
        [SerializeField, Min(0.01f)] private float micToFireResponse = 4f;
        [SerializeField, Min(0.01f)] private float smeltingBowlScaleMultiplier = 1.8f;
        [SerializeField, Min(0.01f)] private float smeltingNuggetScaleMultiplier = 1.2f;
        [SerializeField, Range(0f, 0.95f)] private float micThresholdForMelting = 0.5f;
        [SerializeField, Range(0f, 0.95f)] private float heatProgressThreshold = 0.65f;
        [SerializeField, Min(0.01f)] private float micRiseSustainSpeed = 2.4f;
        [SerializeField, Min(0.01f)] private float micFallSustainSpeed = 2.8f;
        [SerializeField] private Vector3 moltenVisualLocalPosition = new Vector3(0f, 0.03f, 0f);
        [SerializeField] private Vector3 moltenVisualLocalEulerAngles = Vector3.zero;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField, Min(0.1f)] private float debugLogInterval = 0.35f;

        [Header("Runtime Debug")]
        [SerializeField] private float currentHeat;
        [SerializeField] private float currentSmeltingProgress;
        [SerializeField] private float sustainedMicLevel;
        [SerializeField] private float currentFireResponseLevel;
        [SerializeField] private float currentLightIntensity;
        [SerializeField] private float currentLightRange;
        [SerializeField] private float currentParticleSpeed;
        [SerializeField] private float currentMoltenGoldIntensity;
        [SerializeField] private bool smeltingActive;
        [SerializeField] private bool smeltingComplete;
        [SerializeField] private bool smeltingScaleApplied;

        private PersistentCrucibleCarrier activeCarrier;
        private Vector3 fireBaseScale = Vector3.one;
        private Vector3 fireBaseLocalPosition = Vector3.zero;
        private Quaternion fireBaseLocalRotation = Quaternion.identity;
        private float fireBaseLightIntensity = 1f;
        private float fireBaseLightRange = 1f;
        private ParticleSystem[] fireParticles = System.Array.Empty<ParticleSystem>();
        private float[] fireBaseSimulationSpeeds = System.Array.Empty<float>();
        private float[] fireBaseEmissionRates = System.Array.Empty<float>();
        private float[] fireBaseStartSpeedMultipliers = System.Array.Empty<float>();
        private bool fireParticleUpdateLogged;
        private MaterialPropertyBlock furnaceGlowBlock;
        private MaterialPropertyBlock moltenGoldBlock;
        private Color furnaceBaseColor = Color.black;
        private Color furnaceBaseEmission = Color.black;
        private Color moltenBaseColor = Color.black;
        private Color moltenBaseEmission = Color.black;
        private Vector3 moltenBaseScale = Vector3.one;
        private bool furnaceHasBaseColor;
        private bool furnaceHasEmissionColor;
        private bool moltenHasBaseColor;
        private bool moltenHasEmissionColor;
        private float nextDebugLogTime;

        public bool IsSmeltingComplete => smeltingComplete;

        public float CurrentHeat => currentHeat;

        public float CurrentProgressNormalized => currentSmeltingProgress;

        private void Awake()
        {
            furnaceGlowBlock = new MaterialPropertyBlock();
            moltenGoldBlock = new MaterialPropertyBlock();
            WarnAboutMissingReferences();
            CacheVisualDefaults();
            LogHeatTuningSettings();
            UpdateUi(0f, 0f);
            UpdateHeatDrivenVisuals(0f, 0f, 0f);
        }

        private void Start()
        {
            StartCoroutine(BeginSmeltingSequence());
        }

        private void WarnAboutMissingReferences()
        {
            if (heatBarFill == null)
            {
                Debug.LogWarning("Missing HeatBarFill reference", this);
            }

            if (micLevelFill == null)
            {
                Debug.LogWarning("Missing MicLevelFill reference", this);
            }

            if (statusText == null)
            {
                Debug.LogWarning("Missing StatusText reference", this);
            }

            if (microphoneInput == null)
            {
                Debug.LogWarning("Missing MicrophoneInput reference", this);
            }

            if (moltenGoldRenderer == null)
            {
                Debug.LogWarning("Missing MoltenGoldRenderer reference. A runtime molten gold visual will be created for Bowl 2.", this);
            }
        }

        private void Update()
        {
            var micLevel = microphoneInput != null ? microphoneInput.CurrentNormalizedLevel : 0f;
            var hasMicrophone = microphoneInput != null && microphoneInput.HasValidMicrophone;
            UpdateUi(GetHeatNormalized(), smeltingActive ? sustainedMicLevel : micLevel);

            if (!smeltingActive)
            {
                return;
            }

            var sustainedTarget = hasMicrophone ? micLevel : 0f;
            var sustainSpeed = sustainedTarget >= sustainedMicLevel ? micRiseSustainSpeed : micFallSustainSpeed;
            sustainedMicLevel = Mathf.MoveTowards(sustainedMicLevel, sustainedTarget, sustainSpeed * Time.deltaTime);

            if (!smeltingComplete)
            {
                var sustainedMicAboveThreshold = Mathf.InverseLerp(micThresholdForMelting, 1f, sustainedMicLevel);
                var heatDelta = sustainedMicAboveThreshold > 0f
                    ? Mathf.Lerp(heatGainRate * 0.45f, heatGainRate * 2.35f, sustainedMicAboveThreshold)
                    : -heatDecayRate;
                currentHeat = Mathf.Clamp(currentHeat + (heatDelta * Time.deltaTime), 0f, requiredHeatToMelt);

                var heatNormalized = GetHeatNormalized();
                UpdateSmeltingProgress(sustainedMicAboveThreshold);
                UpdateUi(heatNormalized, sustainedMicLevel);
                UpdateHeatDrivenVisuals(heatNormalized, sustainedMicLevel, currentSmeltingProgress);
                UpdateStatus(hasMicrophone, currentSmeltingProgress, sustainedMicLevel);
                LogRuntimeState();

                if (currentSmeltingProgress >= 1f)
                {
                    CompleteSmelting();
                }
                return;
            }

            UpdateHeatDrivenVisuals(1f, micLevel, 1f);
        }

        private IEnumerator BeginSmeltingSequence()
        {
            if (!ValidateReferences())
            {
                yield break;
            }

            activeCarrier = PersistentCrucibleCarrier.ActiveCarrier;

            if (activeCarrier == null)
            {
                Debug.LogWarning("Persistent Bowl 2 was not found in Scene_Smelting.", this);
                SetStatus("Bowl 2 was not found from Scene_Extraction.");
                yield break;
            }

            Debug.Log("Persistent Bowl 2 carried to Scene_Smelting", activeCarrier);
            activeCarrier.PrepareForSmeltingTransport();
            ApplySmeltingScaleIfNeeded();
            AttachMoltenGoldVisualToCarrier();
            activeCarrier.transform.SetParent(null, true);
            activeCarrier.transform.position = entryPoint.position;
            activeCarrier.transform.rotation = entryPoint.rotation;
            Debug.Log("Persistent Bowl 2 placed at entry point", activeCarrier);

            SetStatus("Moving Bowl 2 into the furnace...");

            if (moveDuration > 0f)
            {
                var elapsed = 0f;
                var startPosition = entryPoint.position;
                var startRotation = entryPoint.rotation;
                var targetPosition = insideTarget.position;
                var targetRotation = insideTarget.rotation;

                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    var normalizedTime = Mathf.Clamp01(elapsed / moveDuration);
                    activeCarrier.transform.position = Vector3.Lerp(startPosition, targetPosition, normalizedTime);
                    activeCarrier.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, normalizedTime);
                    yield return null;
                }
            }

            activeCarrier.transform.position = insideTarget.position;
            activeCarrier.transform.rotation = insideTarget.rotation;

            smeltingActive = true;
            SetStatus("Blow to keep the heat up");
        }

        private void CacheVisualDefaults()
        {
            if (fireVfxRoot != null)
            {
                fireBaseLocalPosition = fireVfxRoot.localPosition;
                fireBaseLocalRotation = fireVfxRoot.localRotation;
                fireBaseScale = fireVfxRoot.localScale;
                fireParticles = fireVfxRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                fireBaseSimulationSpeeds = new float[fireParticles.Length];
                fireBaseEmissionRates = new float[fireParticles.Length];
                fireBaseStartSpeedMultipliers = new float[fireParticles.Length];

                LockFireVfxRootInPlace();

                for (var index = 0; index < fireParticles.Length; index++)
                {
                    EnsureCompatibleFireParticleSettings(fireParticles[index], index);
                    var main = fireParticles[index].main;
                    var emission = fireParticles[index].emission;
                    fireBaseSimulationSpeeds[index] = main.simulationSpeed;
                    fireBaseStartSpeedMultipliers[index] = main.startSpeedMultiplier;
                    fireBaseEmissionRates[index] = emission.rateOverTimeMultiplier;
                }
            }

            if (fireLight != null)
            {
                fireBaseLightIntensity = Mathf.Max(0.01f, fireLight.intensity);
                fireBaseLightRange = Mathf.Max(0.01f, fireLight.range);
                fireLight.enabled = true;
            }

            CacheRendererDefaults(
                furnaceGlowRenderer,
                ref furnaceBaseColor,
                ref furnaceBaseEmission,
                ref furnaceHasBaseColor,
                ref furnaceHasEmissionColor);

            CacheRendererDefaults(
                moltenGoldRenderer,
                ref moltenBaseColor,
                ref moltenBaseEmission,
                ref moltenHasBaseColor,
                ref moltenHasEmissionColor);

            if (moltenGoldRenderer != null)
            {
                moltenBaseScale = moltenGoldRenderer.transform.localScale;
                moltenGoldRenderer.enabled = false;
            }
        }

        private void CacheRendererDefaults(
            Renderer targetRenderer,
            ref Color baseColor,
            ref Color baseEmission,
            ref bool hasBaseColor,
            ref bool hasEmissionColor)
        {
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                return;
            }

            hasBaseColor = targetRenderer.sharedMaterial.HasProperty(BaseColorId);
            hasEmissionColor = targetRenderer.sharedMaterial.HasProperty(EmissionColorId);

            if (hasBaseColor)
            {
                baseColor = targetRenderer.sharedMaterial.GetColor(BaseColorId);
            }

            if (hasEmissionColor)
            {
                baseEmission = targetRenderer.sharedMaterial.GetColor(EmissionColorId);
            }
        }

        private bool ValidateReferences()
        {
            if (entryPoint == null || insideTarget == null)
            {
                SetStatus("Assign CrucibleEntryPoint and CrucibleInsideTarget in the Inspector.");
                return false;
            }

            return true;
        }

        private void UpdateUi(float heatNormalized, float micLevel)
        {
            if (heatBarFill != null)
            {
                UpdateBarVisual(heatBarFill, heatNormalized);
            }

            if (micLevelFill != null)
            {
                UpdateBarVisual(micLevelFill, micLevel);
            }
        }

        private void UpdateSmeltingProgress(float sustainedMicAboveThreshold)
        {
            if (sustainedMicAboveThreshold > 0f)
            {
                var meltingRateMultiplier = Mathf.Lerp(0.3f, 3.2f, sustainedMicAboveThreshold * sustainedMicAboveThreshold);
                currentSmeltingProgress = Mathf.Clamp01(
                    currentSmeltingProgress + ((heatGainRate * meltingRateMultiplier) / requiredHeatToMelt) * Time.deltaTime);
                return;
            }

            currentSmeltingProgress = Mathf.Max(0f, currentSmeltingProgress - (heatDecayRate * 0.015f * Time.deltaTime));
        }

        private void UpdateHeatDrivenVisuals(float heatNormalized, float micLevel, float smeltingProgress)
        {
            var fireLerpFactor = 1f - Mathf.Exp(-micToFireResponse * Time.deltaTime);
            currentFireResponseLevel = Mathf.Lerp(currentFireResponseLevel, micLevel, fireLerpFactor);
            var fireLevel = Mathf.Clamp01(currentFireResponseLevel);
            var particleSpeedMultiplier = Mathf.Lerp(minFireVfxSpeed, maxFireVfxSpeed, fireLevel);
            var emissionMultiplier = Mathf.Lerp(0.6f, Mathf.Max(0.6f, fireIntensityMultiplier), fireLevel);

            for (var index = 0; index < fireParticles.Length; index++)
            {
                var main = fireParticles[index].main;
                var emission = fireParticles[index].emission;
                main.simulationSpeed = fireBaseSimulationSpeeds[index] * particleSpeedMultiplier;
                main.startSpeedMultiplier = fireBaseStartSpeedMultipliers[index] * Mathf.Lerp(0.8f, 1.35f, fireLevel);
                emission.rateOverTimeMultiplier = fireBaseEmissionRates[index] * emissionMultiplier;
            }

            if (!fireParticleUpdateLogged)
            {
                Debug.Log("Fire particle parameters updated without moving transform", this);
                fireParticleUpdateLogged = true;
            }

            if (fireLight != null)
            {
                currentLightIntensity = Mathf.Lerp(minFireLightIntensity, maxFireLightIntensity, fireLevel);
                currentLightRange = Mathf.Lerp(minFireLightRange, maxFireLightRange, fireLevel);
                fireLight.intensity = currentLightIntensity;
                fireLight.range = currentLightRange;
            }
            else
            {
                currentLightIntensity = 0f;
                currentLightRange = 0f;
            }

            currentParticleSpeed = particleSpeedMultiplier;

            ApplyRendererGlow(
                furnaceGlowRenderer,
                furnaceGlowBlock,
                furnaceBaseColor,
                furnaceBaseEmission,
                furnaceHasBaseColor,
                furnaceHasEmissionColor,
                new Color(1f, 0.45f, 0.12f, 1f),
                heatNormalized);

            UpdateMoltenGoldVisual(Mathf.Clamp01((heatNormalized * 0.4f) + (smeltingProgress * 0.6f)));
        }

        private void ApplySmeltingScaleIfNeeded()
        {
            if (activeCarrier == null)
            {
                return;
            }

            if (smeltingScaleApplied)
            {
                Debug.Log("Smelting scale already applied, skipping", activeCarrier);
                return;
            }

            activeCarrier.transform.localScale *= smeltingBowlScaleMultiplier;
            Debug.Log("Applied larger smelting scale to Bowl 2", activeCarrier);

            var uniqueNuggets = new HashSet<GoldNugget>();

            foreach (var carriedNugget in activeCarrier.CarriedNuggets)
            {
                if (carriedNugget != null)
                {
                    uniqueNuggets.Add(carriedNugget);
                }
            }

            foreach (var childNugget in activeCarrier.GetComponentsInChildren<GoldNugget>(includeInactive: true))
            {
                if (childNugget != null)
                {
                    uniqueNuggets.Add(childNugget);
                }
            }

            foreach (var nugget in uniqueNuggets)
            {
                nugget.transform.localScale *= smeltingNuggetScaleMultiplier;
            }

            Debug.Log("Applied smelting scale to nuggets", activeCarrier);
            smeltingScaleApplied = true;
        }

        private void LogHeatTuningSettings()
        {
            Debug.Log("Heat gain tuned for slower melting", this);
            Debug.Log($"Required heat increased to {requiredHeatToMelt:F2}", this);
            Debug.Log($"Mic threshold for melting set to {micThresholdForMelting:F2}", this);
            Debug.Log("Melting progress begins once the mic bar crosses the configured threshold", this);
        }

        private void LockFireVfxRootInPlace()
        {
            if (fireVfxRoot == null)
            {
                return;
            }

            fireVfxRoot.localPosition = fireBaseLocalPosition;
            fireVfxRoot.localRotation = fireBaseLocalRotation;
            fireVfxRoot.localScale = fireBaseScale;
            Debug.Log("FireVFX root locked in place", fireVfxRoot);
        }

        private void EnsureCompatibleFireParticleSettings(ParticleSystem particleSystem, int particleIndex)
        {
            if (particleSystem == null)
            {
                return;
            }

            var main = particleSystem.main;

            if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                Debug.LogWarning(
                    $"FireVFX child '{particleSystem.name}' has incompatible simulation space '{main.simulationSpace}' at index {particleIndex}. Switching to Local.",
                    particleSystem);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private static void ApplyRendererGlow(
            Renderer targetRenderer,
            MaterialPropertyBlock propertyBlock,
            Color baseColor,
            Color baseEmission,
            bool hasBaseColor,
            bool hasEmissionColor,
            Color glowTint,
            float intensity)
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (!targetRenderer.enabled)
            {
                targetRenderer.enabled = true;
            }

            propertyBlock.Clear();

            if (hasBaseColor)
            {
                var blendedBaseColor = Color.Lerp(baseColor * 0.35f, glowTint, intensity);
                blendedBaseColor.a = baseColor.a;
                propertyBlock.SetColor(BaseColorId, blendedBaseColor);
            }

            if (hasEmissionColor)
            {
                var emissionColor = baseEmission + (glowTint * (0.25f + intensity * 2f));
                propertyBlock.SetColor(EmissionColorId, emissionColor);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateStatus(bool hasMicrophone, float smeltingProgress, float micDriveLevel)
        {
            if (!hasMicrophone)
            {
                SetStatus("Waiting for microphone");
                return;
            }

            if (smeltingProgress < 1f && micDriveLevel < micThresholdForMelting)
            {
                SetStatus($"Keep the mic bar above {Mathf.RoundToInt(micThresholdForMelting * 100f)}%");
                return;
            }

            SetStatus($"Melting... {Mathf.RoundToInt(smeltingProgress * 100f)}%");
        }

        private void CompleteSmelting()
        {
            smeltingComplete = true;
            currentHeat = requiredHeatToMelt;
            currentSmeltingProgress = 1f;
            UpdateUi(1f, microphoneInput != null ? microphoneInput.CurrentNormalizedLevel : 0f);
            UpdateHeatDrivenVisuals(1f, 1f, 1f);
            SetStatus("Melting complete");
        }

        private void AttachMoltenGoldVisualToCarrier()
        {
            if (activeCarrier == null)
            {
                return;
            }

            EnsureMoltenGoldVisualExists();

            if (moltenGoldRenderer == null)
            {
                return;
            }

            var visualTransform = moltenGoldRenderer.transform;
            visualTransform.SetParent(activeCarrier.transform, false);
            visualTransform.localPosition = moltenVisualLocalPosition;
            visualTransform.localRotation = Quaternion.Euler(moltenVisualLocalEulerAngles);
            Debug.Log("Molten gold visual initialized", moltenGoldRenderer);
        }

        private void EnsureMoltenGoldVisualExists()
        {
            if (moltenGoldRenderer != null || activeCarrier == null)
            {
                return;
            }

            var existingVisual = activeCarrier.transform.Find("SmeltingMoltenGoldVisual");

            if (existingVisual != null)
            {
                moltenGoldRenderer = existingVisual.GetComponent<Renderer>();
            }

            if (moltenGoldRenderer == null)
            {
                var moltenVisualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moltenVisualObject.name = "SmeltingMoltenGoldVisual";
                moltenVisualObject.transform.SetParent(activeCarrier.transform, false);

                if (moltenVisualObject.TryGetComponent<Collider>(out var collider))
                {
                    Destroy(collider);
                }

                moltenGoldRenderer = moltenVisualObject.GetComponent<Renderer>();
                moltenGoldRenderer.sharedMaterial = BuildRuntimeMoltenMaterial();
                moltenGoldRenderer.shadowCastingMode = ShadowCastingMode.Off;
                moltenGoldRenderer.receiveShadows = true;
            }

            CacheRendererDefaults(
                moltenGoldRenderer,
                ref moltenBaseColor,
                ref moltenBaseEmission,
                ref moltenHasBaseColor,
                ref moltenHasEmissionColor);

            moltenBaseScale = new Vector3(0.18f, 0.045f, 0.18f);
            moltenGoldRenderer.transform.localScale = moltenBaseScale;
            moltenGoldRenderer.enabled = false;
        }

        private static Material BuildRuntimeMoltenMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var moltenMaterial = new Material(shader)
            {
                name = "M_Runtime_SmeltingMoltenGold"
            };

            if (moltenMaterial.HasProperty(BaseColorId))
            {
                moltenMaterial.SetColor(BaseColorId, new Color(1f, 0.56f, 0.08f, 1f));
            }

            if (moltenMaterial.HasProperty(EmissionColorId))
            {
                moltenMaterial.SetColor(EmissionColorId, new Color(1.65f, 0.7f, 0.16f, 1f) * 1.85f);
                moltenMaterial.EnableKeyword("_EMISSION");
            }

            if (moltenMaterial.HasProperty("_Metallic"))
            {
                moltenMaterial.SetFloat("_Metallic", 0.72f);
            }

            if (moltenMaterial.HasProperty("_Smoothness"))
            {
                moltenMaterial.SetFloat("_Smoothness", 0.92f);
            }

            return moltenMaterial;
        }

        private void UpdateMoltenGoldVisual(float intensity)
        {
            if (moltenGoldRenderer == null)
            {
                currentMoltenGoldIntensity = 0f;
                return;
            }

            currentMoltenGoldIntensity = Mathf.Clamp01(intensity);
            moltenGoldRenderer.enabled = currentMoltenGoldIntensity > 0.01f;
            moltenGoldRenderer.transform.localScale = Vector3.Scale(
                moltenBaseScale,
                new Vector3(
                    Mathf.Lerp(0.72f, 1.08f, currentMoltenGoldIntensity),
                    Mathf.Lerp(0.35f, 1f, currentMoltenGoldIntensity),
                    Mathf.Lerp(0.72f, 1.08f, currentMoltenGoldIntensity)));

            ApplyRendererGlow(
                moltenGoldRenderer,
                moltenGoldBlock,
                moltenBaseColor,
                moltenBaseEmission,
                moltenHasBaseColor,
                moltenHasEmissionColor,
                new Color(1f, 0.73f, 0.18f, 1f),
                currentMoltenGoldIntensity);
        }

        private void LogRuntimeState()
        {
            if (!enableDebugLogs || Time.unscaledTime < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.unscaledTime + debugLogInterval;
            Debug.Log($"Heat progress: {GetHeatNormalized():F4}", this);
            Debug.Log($"Fire response level: {currentFireResponseLevel:F4}", this);
            Debug.Log($"Light intensity set to: {currentLightIntensity:F4}", this);
            Debug.Log($"Particle speed set to: {currentParticleSpeed:F4}", this);
            Debug.Log($"Molten gold intensity: {currentMoltenGoldIntensity:F4}", this);
        }

        private float GetHeatNormalized()
        {
            return requiredHeatToMelt <= 0f ? 0f : Mathf.Clamp01(currentHeat / requiredHeatToMelt);
        }

        private static void UpdateBarVisual(Image barImage, float normalizedValue)
        {
            if (barImage == null)
            {
                return;
            }

            var clampedValue = Mathf.Clamp01(normalizedValue);

            if (barImage.type == Image.Type.Filled)
            {
                barImage.fillAmount = clampedValue;
                return;
            }

            var barRect = barImage.rectTransform;
            var parentRect = barRect.parent as RectTransform;

            if (parentRect == null)
            {
                barImage.fillAmount = clampedValue;
                return;
            }

            barRect.anchorMin = new Vector2(0f, barRect.anchorMin.y);
            barRect.anchorMax = new Vector2(0f, barRect.anchorMax.y);
            barRect.pivot = new Vector2(0f, barRect.pivot.y);
            barRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentRect.rect.width * clampedValue);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
