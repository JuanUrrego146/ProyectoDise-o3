using System.Collections;
using System.Collections.Generic;
using LingoteRush.Systems.Extraction;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Systems.FinalIngot
{
    [DisallowMultipleComponent]
    public sealed class SceneFinalIngotController : MonoBehaviour
    {
        private enum FinalSequenceState
        {
            Idle,
            MovingToPour,
            Pouring,
            Cooling,
            Revealed,
            Demolding
        }

        [Header("Scene Anchors")]
        [SerializeField] private Transform workAreaRoot;
        [SerializeField] private Transform moldAreaRoot;
        [SerializeField] private Transform bowlPointsRoot;
        [SerializeField] private Transform pourPointsRoot;
        [SerializeField] private Transform vfxRoot;
        [SerializeField] private Transform lightingRoot;
        [SerializeField] private Transform cameraPointsRoot;
        [SerializeField] private Transform workTable;

        [Header("Bowl Points")]
        [SerializeField] private Transform bowlRestPoint;
        [SerializeField] private Transform bowlPourPoint;
        [SerializeField] private Transform bowlExitPoint;

        [Header("Pour Points")]
        [SerializeField] private Transform pourStartPoint;
        [SerializeField] private Transform pourEndPoint;
        [SerializeField] private Transform moldCenterPoint;

        [Header("Liquid System")]
        [SerializeField] private BowlLiquidSource bowlLiquidSource;
        [SerializeField] private GoldLiquidPool liquidPool;
        [SerializeField] private MoldLiquidReceiver moldLiquidReceiver;

        [Header("Scene Objects")]
        [SerializeField] private GameObject moldObject;
        [SerializeField] private GameObject moldInnerGoldObject;
        [SerializeField] private Renderer moldInnerGoldRenderer;
        [SerializeField] private GameObject finalIngotObject;

        [Header("VFX")]
        [SerializeField] private ParticleSystem steamCoolingParticles;
        [SerializeField] private ParticleSystem sparksPourParticles;
        [SerializeField] private ParticleSystem heatSmokeParticles;
        [SerializeField] private ParticleSystem revealGlowParticles;

        [Header("Lighting")]
        [SerializeField] private Light goldGlowLight;
        [SerializeField] private Light revealKeyLight;
        [SerializeField] private Light fillLight;

        [Header("Camera")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraStartPoint;
        [SerializeField] private Transform cameraRevealPoint;
        [SerializeField] private Transform cameraLookAtTarget;
        [SerializeField] private bool autoMoveCameraOnReveal = true;
        [SerializeField, Min(0f)] private float autoRevealCameraDelay = 0.25f;
        [SerializeField, Range(-180f, 180f)] private float topDownRollOffset = 0f;
        [SerializeField, Range(0.5f, 0.999f)] private float topDownRollThreshold = 0.92f;

        [Header("Point Layout")]
        [SerializeField] private bool alignScenePointsToMoldCenter = true;
        [SerializeField] private Vector3 bowlRestOffset = new Vector3(-0.52f, 0.16f, 0f);
        [SerializeField] private Vector3 bowlPourOffset = new Vector3(-0.36f, 0.13f, 0f);
        [SerializeField] private Vector3 bowlExitOffset = new Vector3(0.46f, 0.14f, 0.02f);
        [SerializeField] private Vector3 pourStartOffset = new Vector3(-0.2f, 0.34f, 0.02f);
        [SerializeField] private Vector3 pourEndOffset = new Vector3(0f, 0.1f, 0f);
        [SerializeField] private Vector3 cameraStartOffset = new Vector3(0.08f, 0.72f, 2.15f);
        [SerializeField] private Vector3 cameraRevealOffset = new Vector3(0.02f, 0.54f, 0.28f);
        [SerializeField] private Vector3 cameraLookAtOffset = new Vector3(0.02f, 0.01f, 0.02f);

        [Header("Debug")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private KeyCode startDebugKey = KeyCode.Return;
        [SerializeField] private KeyCode resetDebugKey = KeyCode.R;
        [SerializeField] private KeyCode demoldKey = KeyCode.M;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float bowlMoveDuration = 1.5f;
        [SerializeField] private bool tiltBowlTowardPourTarget = true;
        [SerializeField] private Vector3 bowlTiltAxis = Vector3.forward;
        [SerializeField, Min(0f)] private float bowlTiltAngle = 55f;
        [SerializeField, Min(0.1f)] private float pourDuration = 3f;
        [SerializeField, Min(0.1f)] private float coolingDuration = 4f;
        [SerializeField, Min(0.1f)] private float revealDuration = 1f;
        [SerializeField, Min(0.1f)] private float cameraMoveDuration = 1.5f;

        [Header("Light Tuning")]
        [SerializeField, Min(0f)] private float goldGlowMaxIntensity = 1.25f;
        [SerializeField, Min(0f)] private float goldGlowMinIntensity = 0f;

        private readonly HashSet<string> warnedMissingReferences = new HashSet<string>();
        private readonly List<Renderer> hiddenNuggetRenderers = new List<Renderer>();
        private readonly List<Collider> hiddenNuggetColliders = new List<Collider>();

        private Coroutine sequenceRoutine;
        private Coroutine cameraMoveRoutine;
        private Transform bowlTransform;
        private Rigidbody bowlRigidbody;
        private Quaternion bowlUprightRotation;
        private Quaternion bowlTiltedRotation;
        private float revealKeyBaseIntensity;
        private float fillBaseIntensity;
        private FinalSequenceState currentState = FinalSequenceState.Idle;
        private Transform runtimeCameraStartPoint;
        private Transform runtimeCameraRevealPoint;
        private Transform runtimeCameraLookAtTarget;

        private void Awake()
        {
            AutoResolveSceneReferences();
            AlignScenePointsFromMoldCenter();
            EnsureMoldSetup();
            EnsureBowlSetup();
            EnsureLightingSetup();
            CacheLightDefaults();
            NormalizeCameraPoints();
        }

        private void Start()
        {
            ResetFinalIngotScene();

            if (playOnStart)
            {
                StartFinalIngotSequence();
            }
        }

        private void OnDestroy()
        {
            RestoreBowlNuggetVisuals();
        }

        private void Update()
        {
            if (WasKeyPressedThisFrame(resetDebugKey))
            {
                ResetFinalIngotScene();
                return;
            }

            if (WasKeyPressedThisFrame(startDebugKey) && currentState == FinalSequenceState.Idle)
            {
                StartFinalIngotSequence();
            }

            if (WasKeyPressedThisFrame(demoldKey) && currentState == FinalSequenceState.Revealed)
            {
                DemoldIngot();
            }
        }

        public void StartFinalIngotSequence()
        {
            if (sequenceRoutine != null)
            {
                return;
            }

            AutoResolveSceneReferences();
            AlignScenePointsFromMoldCenter();
            EnsureMoldSetup();
            EnsureBowlSetup();
            EnsureLightingSetup();

            if (!ValidateCoreReferences())
            {
                return;
            }

            sequenceRoutine = StartCoroutine(RunFinalSequence());
        }

        public void StartPour()
        {
            bowlLiquidSource?.Configure(liquidPool, pourStartPoint, pourEndPoint);
            bowlLiquidSource?.StartPouring();
            PlayParticleSystem(sparksPourParticles);
            PlayParticleSystem(heatSmokeParticles);
            SetLightIntensity(goldGlowLight, goldGlowMaxIntensity);
            currentState = FinalSequenceState.Pouring;
        }

        public void StartCooling()
        {
            bowlLiquidSource?.StopPouring();
            bowlLiquidSource?.SetContainedLiquidNormalized(0.02f);
            StopParticleSystem(sparksPourParticles);
            PlayParticleSystem(steamCoolingParticles);
            currentState = FinalSequenceState.Cooling;
        }

        public void RevealIngot()
        {
            StopParticleSystem(heatSmokeParticles);
            StopParticleSystem(steamCoolingParticles);
            bowlLiquidSource?.SetContainedLiquidNormalized(0f);
            moldLiquidReceiver?.HideInnerGold();

            if (moldInnerGoldRenderer != null)
            {
                moldInnerGoldRenderer.enabled = false;
            }

            if (finalIngotObject != null)
            {
                finalIngotObject.SetActive(true);
            }

            PlayParticleSystem(revealGlowParticles);

            if (revealKeyLight != null)
            {
                revealKeyLight.intensity = revealKeyBaseIntensity * 1.08f;
            }

            currentState = FinalSequenceState.Revealed;

            if (autoMoveCameraOnReveal && cameraRevealPoint != null)
            {
                if (cameraMoveRoutine != null)
                {
                    StopCoroutine(cameraMoveRoutine);
                }

                cameraMoveRoutine = StartCoroutine(MoveCameraAfterDelayRoutine(
                    cameraRevealPoint,
                    autoRevealCameraDelay,
                    cameraMoveDuration,
                    changeStateToDemolding: false));
            }
        }

        public void DemoldIngot()
        {
            if (currentState != FinalSequenceState.Revealed || mainCamera == null || cameraRevealPoint == null)
            {
                return;
            }

            if (cameraMoveRoutine != null)
            {
                StopCoroutine(cameraMoveRoutine);
            }

            cameraMoveRoutine = StartCoroutine(MoveCameraRoutine(cameraRevealPoint, cameraMoveDuration, changeStateToDemolding: true));
        }

        public void ResetFinalIngotScene()
        {
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            if (cameraMoveRoutine != null)
            {
                StopCoroutine(cameraMoveRoutine);
                cameraMoveRoutine = null;
            }

            AutoResolveSceneReferences();
            AlignScenePointsFromMoldCenter();
            EnsureMoldSetup();
            EnsureBowlSetup();
            EnsureLightingSetup();
            NormalizeCameraPoints();

            currentState = FinalSequenceState.Idle;

            if (bowlTransform != null && bowlRestPoint != null)
            {
                bowlTransform.SetPositionAndRotation(bowlRestPoint.position, bowlRestPoint.rotation);
                bowlUprightRotation = bowlRestPoint.rotation;
                bowlTiltedRotation = CalculateTiltedRotation(bowlPourPoint != null ? bowlPourPoint.rotation : bowlRestPoint.rotation);
            }

            if (bowlRigidbody != null)
            {
                bowlRigidbody.linearVelocity = Vector3.zero;
                bowlRigidbody.angularVelocity = Vector3.zero;
                bowlRigidbody.useGravity = false;
                bowlRigidbody.isKinematic = true;
            }

            HideBowlNuggetVisuals();

            bowlLiquidSource?.ResetSource();
            moldLiquidReceiver?.ResetReceiver();

            moldInnerGoldObject = moldLiquidReceiver != null ? moldLiquidReceiver.MoldInnerGoldVisual?.gameObject : moldInnerGoldObject;
            moldInnerGoldRenderer = moldLiquidReceiver != null ? moldLiquidReceiver.MoldInnerGoldRenderer : moldInnerGoldRenderer;

            if (finalIngotObject != null)
            {
                finalIngotObject.SetActive(false);
            }

            StopParticleSystem(heatSmokeParticles);
            StopParticleSystem(sparksPourParticles);
            StopParticleSystem(steamCoolingParticles);
            StopParticleSystem(revealGlowParticles);

            SetLightIntensity(goldGlowLight, goldGlowMinIntensity);

            if (revealKeyLight != null)
            {
                revealKeyLight.intensity = revealKeyBaseIntensity;
            }

            if (fillLight != null)
            {
                fillLight.intensity = fillBaseIntensity;
            }

            SnapCameraToStartPoint();
        }

        private IEnumerator RunFinalSequence()
        {
            yield return MoveBowlRoutine(bowlPourPoint, bowlMoveDuration);
            yield return TiltBowlRoutine(bowlTiltedRotation, bowlMoveDuration * 0.35f);

            StartPour();

            var pourElapsed = 0f;

            while (pourElapsed < pourDuration && (moldLiquidReceiver == null || !moldLiquidReceiver.IsFull))
            {
                pourElapsed += Time.deltaTime;

                if (bowlLiquidSource != null)
                {
                    var remainingLiquid = 1f - Mathf.Clamp01(pourElapsed / pourDuration);
                    bowlLiquidSource.SetContainedLiquidNormalized(remainingLiquid);
                }

                yield return null;
            }

            if (moldLiquidReceiver != null && !moldLiquidReceiver.IsFull)
            {
                Debug.LogWarning("SceneFinal_Mold did not reach full liquid amount before the pour timer completed. Completing the fill to keep the sequence moving.", this);
                moldLiquidReceiver.CompleteFill();
            }

            yield return TiltBowlRoutine(bowlUprightRotation, bowlMoveDuration * 0.25f);
            StartCooling();

            var coolingElapsed = 0f;

            while (coolingElapsed < coolingDuration)
            {
                coolingElapsed += Time.deltaTime;
                var coolingNormalized = 1f - Mathf.Clamp01(coolingElapsed / coolingDuration);
                moldLiquidReceiver?.SetHeatNormalized(coolingNormalized);
                SetLightIntensity(goldGlowLight, Mathf.Lerp(goldGlowMinIntensity, goldGlowMaxIntensity, coolingNormalized));
                yield return null;
            }

            if (bowlExitPoint != null && bowlTransform != null)
            {
                yield return MoveBowlRoutine(bowlExitPoint, bowlMoveDuration * 0.7f);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, revealDuration * 0.15f));
            RevealIngot();
            sequenceRoutine = null;
        }

        private IEnumerator MoveBowlRoutine(Transform targetPoint, float duration)
        {
            if (bowlTransform == null || targetPoint == null)
            {
                yield break;
            }

            currentState = FinalSequenceState.MovingToPour;

            var startPosition = bowlTransform.position;
            var startRotation = bowlTransform.rotation;
            var targetRotation = targetPoint.rotation;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                bowlTransform.position = Vector3.Lerp(startPosition, targetPoint.position, normalized);
                bowlTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, normalized);
                yield return null;
            }

            bowlTransform.SetPositionAndRotation(targetPoint.position, targetRotation);
            bowlUprightRotation = targetRotation;
            bowlTiltedRotation = CalculateTiltedRotation(targetRotation);
        }

        private IEnumerator TiltBowlRoutine(Quaternion targetRotation, float duration)
        {
            if (bowlTransform == null)
            {
                yield break;
            }

            var startRotation = bowlTransform.rotation;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                bowlTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, normalized);
                yield return null;
            }

            bowlTransform.rotation = targetRotation;
        }

        private IEnumerator MoveCameraRoutine(Transform targetPoint, float duration)
        {
            yield return MoveCameraRoutine(targetPoint, duration, changeStateToDemolding: true);
        }

        private IEnumerator MoveCameraAfterDelayRoutine(Transform targetPoint, float delay, float duration, bool changeStateToDemolding)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            yield return MoveCameraRoutine(targetPoint, duration, changeStateToDemolding);
        }

        private IEnumerator MoveCameraRoutine(Transform targetPoint, float duration, bool changeStateToDemolding)
        {
            if (mainCamera == null || targetPoint == null)
            {
                yield break;
            }

            if (changeStateToDemolding)
            {
                currentState = FinalSequenceState.Demolding;
            }

            var startPosition = mainCamera.transform.position;
            var startRotation = mainCamera.transform.rotation;
            var targetRotation = ResolveCameraTargetRotation(targetPoint);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                mainCamera.transform.position = Vector3.Lerp(startPosition, targetPoint.position, normalized);
                mainCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, normalized);
                yield return null;
            }

            mainCamera.transform.SetPositionAndRotation(targetPoint.position, targetRotation);
            cameraMoveRoutine = null;
        }

        private void AutoResolveSceneReferences()
        {
            if (workAreaRoot == null)
            {
                workAreaRoot = FindTransformByName("SceneFinal_WorkArea");
            }

            if (moldAreaRoot == null)
            {
                moldAreaRoot = FindTransformByName("SceneFinal_MoldArea");
            }

            if (bowlPointsRoot == null)
            {
                bowlPointsRoot = FindTransformByName("SceneFinal_BowlPoints");
            }

            if (pourPointsRoot == null)
            {
                pourPointsRoot = FindTransformByName("SceneFinal_PourPoints");
            }

            if (vfxRoot == null)
            {
                vfxRoot = FindTransformByName("SceneFinal_VFX");
            }

            if (lightingRoot == null)
            {
                lightingRoot = FindTransformByName("SceneFinal_Lighting");
            }

            if (cameraPointsRoot == null)
            {
                cameraPointsRoot = FindTransformByName("SceneFinal_CameraPoints");
            }

            if (workTable == null)
            {
                workTable = FindTransformByName("SceneFinal_WorkTable");
            }

            if (bowlRestPoint == null)
            {
                bowlRestPoint = FindTransformByName("SceneFinal_BowlRestPoint");
            }

            if (bowlPourPoint == null)
            {
                bowlPourPoint = FindTransformByName("SceneFinal_BowlPourPoint");
            }

            if (bowlExitPoint == null)
            {
                bowlExitPoint = FindTransformByName("SceneFinal_BowlExitPoint");
            }

            if (pourStartPoint == null)
            {
                pourStartPoint = FindTransformByName("SceneFinal_PourStartPoint");
            }

            if (pourEndPoint == null)
            {
                pourEndPoint = FindTransformByName("SceneFinal_PourEndPoint");
            }

            if (moldCenterPoint == null)
            {
                moldCenterPoint = FindTransformByName("SceneFinal_MoldCenterPoint");
            }

            if (cameraStartPoint == null)
            {
                cameraStartPoint = FindTransformByName("SceneFinal_CameraStartPoint");
            }

            if (cameraRevealPoint == null)
            {
                cameraRevealPoint = FindTransformByName("SceneFinal_CameraRevealPoint");
            }

            if (cameraLookAtTarget == null)
            {
                cameraLookAtTarget = FindTransformByName("SceneFinal_CameraLookAtTarget");
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;

                if (mainCamera == null)
                {
                    var camerasInScene = FindObjectsOfType<Camera>(true);

                    if (camerasInScene.Length > 0)
                    {
                        mainCamera = camerasInScene[0];
                    }
                }
            }

            if (moldObject == null)
            {
                var moldTransform = FindTransformByName("SceneFinal_Mold");
                moldObject = moldTransform != null ? moldTransform.gameObject : null;
            }

            if (finalIngotObject == null)
            {
                var finalIngotTransform = FindTransformByName("SceneFinal_FinalIngot_Model");
                finalIngotObject = finalIngotTransform != null ? finalIngotTransform.gameObject : null;
            }

            if (heatSmokeParticles == null)
            {
                heatSmokeParticles = FindParticleSystemByName("SceneFinal_VFX_HeatSmoke");
            }

            if (sparksPourParticles == null)
            {
                sparksPourParticles = FindParticleSystemByName("SceneFinal_VFX_SparksPour");
            }

            if (steamCoolingParticles == null)
            {
                steamCoolingParticles = FindParticleSystemByName("SceneFinal_VFX_SteamCooling");
            }

            if (revealGlowParticles == null)
            {
                revealGlowParticles = FindParticleSystemByName("SceneFinal_VFX_RevealGlow");
            }

            if (goldGlowLight == null)
            {
                goldGlowLight = FindLightByName("SceneFinal_GoldGlowLight");
            }

            if (revealKeyLight == null)
            {
                revealKeyLight = FindLightByName("SceneFinal_RevealKeyLight");
            }

            if (fillLight == null)
            {
                fillLight = FindLightByName("SceneFinal_FillLight");
            }

            WarnIfMissingOptional(steamCoolingParticles, "SceneFinal_VFX_SteamCooling");
            WarnIfMissingOptional(revealGlowParticles, "SceneFinal_VFX_RevealGlow");
        }

        private void EnsureMoldSetup()
        {
            if (moldAreaRoot == null || moldCenterPoint == null)
            {
                return;
            }

            if (moldObject == null)
            {
                moldObject = new GameObject("SceneFinal_Mold");
                moldObject.transform.SetParent(moldAreaRoot, false);
            }

            moldObject.transform.SetPositionAndRotation(moldCenterPoint.position, moldCenterPoint.rotation);
            EnsureMoldGeometry(moldObject.transform);

            if (moldLiquidReceiver == null)
            {
                moldLiquidReceiver = moldObject.GetComponent<MoldLiquidReceiver>();
            }

            if (moldLiquidReceiver == null)
            {
                moldLiquidReceiver = moldObject.AddComponent<MoldLiquidReceiver>();
            }

            moldInnerGoldObject = moldLiquidReceiver.MoldInnerGoldVisual != null
                ? moldLiquidReceiver.MoldInnerGoldVisual.gameObject
                : moldInnerGoldObject;
            moldInnerGoldRenderer = moldLiquidReceiver.MoldInnerGoldRenderer;
        }

        private void EnsureMoldGeometry(Transform moldRoot)
        {
            if (moldRoot == null)
            {
                return;
            }

            var metalMaterial = FinalIngotRuntimeMaterials.GetDarkMetalMaterial();

            CreateMoldPart(moldRoot, "Mold_Base", new Vector3(0f, -0.035f, 0f), new Vector3(0.34f, 0.03f, 0.22f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_LeftWall", new Vector3(-0.13f, 0f, 0f), new Vector3(0.04f, 0.06f, 0.22f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_RightWall", new Vector3(0.13f, 0f, 0f), new Vector3(0.04f, 0.06f, 0.22f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_FrontWall", new Vector3(0f, 0f, 0.09f), new Vector3(0.22f, 0.06f, 0.04f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_BackWall", new Vector3(0f, 0f, -0.09f), new Vector3(0.22f, 0.06f, 0.04f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_Rim_Left", new Vector3(-0.11f, 0.028f, 0f), new Vector3(0.02f, 0.014f, 0.18f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_Rim_Right", new Vector3(0.11f, 0.028f, 0f), new Vector3(0.02f, 0.014f, 0.18f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_Rim_Front", new Vector3(0f, 0.028f, 0.07f), new Vector3(0.18f, 0.014f, 0.02f), metalMaterial);
            CreateMoldPart(moldRoot, "Mold_Rim_Back", new Vector3(0f, 0.028f, -0.07f), new Vector3(0.18f, 0.014f, 0.02f), metalMaterial);
        }

        private void EnsureBowlSetup()
        {
            if (PersistentCrucibleCarrier.ActiveCarrier != null)
            {
                bowlTransform = PersistentCrucibleCarrier.ActiveCarrier.transform;
            }
            else if (bowlTransform == null)
            {
                bowlTransform = FindTransformByName("Bowl 2");
            }

            if (bowlTransform == null)
            {
                WarnIfMissingOptional(null, "Bowl 2");
                return;
            }

            if (bowlRigidbody == null)
            {
                bowlTransform.TryGetComponent(out bowlRigidbody);
            }

            if (bowlLiquidSource == null)
            {
                bowlLiquidSource = bowlTransform.GetComponent<BowlLiquidSource>();
            }

            if (bowlLiquidSource == null)
            {
                bowlLiquidSource = bowlTransform.gameObject.AddComponent<BowlLiquidSource>();
            }

            if (liquidPool == null)
            {
                liquidPool = bowlTransform.GetComponentInChildren<GoldLiquidPool>(includeInactive: true);
            }

            if (liquidPool == null)
            {
                var poolRoot = bowlTransform.Find("SceneFinal_GoldLiquidPool");

                if (poolRoot == null)
                {
                    var poolObject = new GameObject("SceneFinal_GoldLiquidPool");
                    poolObject.transform.SetParent(bowlTransform, false);
                    poolRoot = poolObject.transform;
                }

                liquidPool = poolRoot.GetComponent<GoldLiquidPool>();

                if (liquidPool == null)
                {
                    liquidPool = poolRoot.gameObject.AddComponent<GoldLiquidPool>();
                }
            }

            bowlLiquidSource.Configure(liquidPool, pourStartPoint, pourEndPoint);
            bowlLiquidSource.SetContainedLiquidNormalized(1f);
        }

        private void EnsureLightingSetup()
        {
            if (goldGlowLight != null || lightingRoot == null || moldCenterPoint == null)
            {
                return;
            }

            var glowLightObject = new GameObject("SceneFinal_GoldGlowLight");
            glowLightObject.transform.SetParent(lightingRoot, false);
            glowLightObject.transform.position = moldCenterPoint.position + new Vector3(0f, 0.18f, 0f);
            glowLightObject.transform.rotation = Quaternion.identity;

            goldGlowLight = glowLightObject.AddComponent<Light>();
            goldGlowLight.type = LightType.Point;
            goldGlowLight.color = new Color(1f, 0.68f, 0.24f, 1f);
            goldGlowLight.range = 1.35f;
            goldGlowLight.intensity = goldGlowMinIntensity;
            goldGlowLight.shadows = LightShadows.None;
        }

        private void CacheLightDefaults()
        {
            revealKeyBaseIntensity = revealKeyLight != null ? revealKeyLight.intensity : 0f;
            fillBaseIntensity = fillLight != null ? fillLight.intensity : 0f;
        }

        private void NormalizeCameraPoints()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            cameraStartPoint = NormalizeCameraPoint(cameraStartPoint, "SceneFinal_CameraStartPoint_Runtime", keepCameraActive: true);
            cameraRevealPoint = NormalizeCameraPoint(cameraRevealPoint, "SceneFinal_CameraRevealPoint_Runtime", keepCameraActive: false);
            cameraLookAtTarget = NormalizeCameraPoint(cameraLookAtTarget, "SceneFinal_CameraLookAtTarget_Runtime", keepCameraActive: false);
        }

        private Transform NormalizeCameraPoint(Transform targetPoint, string runtimePointName, bool keepCameraActive)
        {
            if (targetPoint == null)
            {
                return null;
            }

            var targetCamera = targetPoint.GetComponent<Camera>();
            var audioListener = targetPoint.GetComponent<AudioListener>();
            var hasEmbeddedCamera = targetCamera != null || audioListener != null;

            if (!hasEmbeddedCamera)
            {
                return targetPoint;
            }

            if (targetCamera != null)
            {
                targetCamera.enabled = keepCameraActive && targetCamera == mainCamera;
            }

            if (audioListener != null)
            {
                audioListener.enabled = keepCameraActive && mainCamera != null && audioListener.gameObject == mainCamera.gameObject;
            }

            var runtimePoint = ResolveOrCreateRuntimeCameraPoint(runtimePointName, targetPoint);
            runtimePoint.SetPositionAndRotation(targetPoint.position, targetPoint.rotation);
            return runtimePoint;
        }

        private Transform ResolveOrCreateRuntimeCameraPoint(string runtimePointName, Transform sourcePoint)
        {
            var runtimePoint = FindTransformByName(runtimePointName);

            if (runtimePoint == null)
            {
                var runtimePointObject = new GameObject(runtimePointName);
                runtimePoint = runtimePointObject.transform;

                if (sourcePoint != null && sourcePoint.parent != null)
                {
                    runtimePoint.SetParent(sourcePoint.parent, worldPositionStays: false);
                }
                else if (cameraPointsRoot != null)
                {
                    runtimePoint.SetParent(cameraPointsRoot, worldPositionStays: false);
                }
            }

            if (runtimePointName == "SceneFinal_CameraStartPoint_Runtime")
            {
                runtimeCameraStartPoint = runtimePoint;
            }
            else if (runtimePointName == "SceneFinal_CameraRevealPoint_Runtime")
            {
                runtimeCameraRevealPoint = runtimePoint;
            }
            else if (runtimePointName == "SceneFinal_CameraLookAtTarget_Runtime")
            {
                runtimeCameraLookAtTarget = runtimePoint;
            }

            return runtimePoint;
        }

        private void AlignScenePointsFromMoldCenter()
        {
            if (!alignScenePointsToMoldCenter || moldCenterPoint == null)
            {
                return;
            }

            ApplyPointWorldPosition(bowlRestPoint, moldCenterPoint.position + bowlRestOffset);
            ApplyPointWorldPosition(bowlPourPoint, moldCenterPoint.position + bowlPourOffset);
            ApplyPointWorldPosition(bowlExitPoint, moldCenterPoint.position + bowlExitOffset);
            ApplyPointWorldPosition(pourStartPoint, moldCenterPoint.position + pourStartOffset);
            ApplyPointWorldPosition(pourEndPoint, moldCenterPoint.position + pourEndOffset);
            ApplyPointWorldPosition(cameraStartPoint, moldCenterPoint.position + cameraStartOffset);
            ApplyPointWorldPosition(cameraRevealPoint, moldCenterPoint.position + cameraRevealOffset);
            ApplyPointWorldPosition(cameraLookAtTarget, moldCenterPoint.position + cameraLookAtOffset);
            ApplyPointWorldPosition(runtimeCameraStartPoint, moldCenterPoint.position + cameraStartOffset);
            ApplyPointWorldPosition(runtimeCameraRevealPoint, moldCenterPoint.position + cameraRevealOffset);
            ApplyPointWorldPosition(runtimeCameraLookAtTarget, moldCenterPoint.position + cameraLookAtOffset);

            AlignBowlPointRotation(bowlRestPoint);
            AlignBowlPointRotation(bowlPourPoint);
            AlignBowlPointRotation(bowlExitPoint);
            AlignCameraPointRotation(cameraStartPoint);
            AlignCameraPointRotation(cameraRevealPoint);
            AlignCameraPointRotation(runtimeCameraStartPoint);
            AlignCameraPointRotation(runtimeCameraRevealPoint);
        }

        private void ApplyPointWorldPosition(Transform point, Vector3 worldPosition)
        {
            if (point != null)
            {
                point.position = worldPosition;
            }
        }

        private void AlignBowlPointRotation(Transform point)
        {
            if (point == null || moldCenterPoint == null)
            {
                return;
            }

            var flatDirection = moldCenterPoint.position - point.position;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            point.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
        }

        private void AlignCameraPointRotation(Transform point)
        {
            if (point == null)
            {
                return;
            }

            point.rotation = ResolveCameraTargetRotation(point);
        }

        private void HideBowlNuggetVisuals()
        {
            if (bowlTransform == null)
            {
                return;
            }

            foreach (var nugget in bowlTransform.GetComponentsInChildren<GoldNugget>(includeInactive: true))
            {
                if (nugget.TryGetComponent<Renderer>(out var renderer))
                {
                    if (!hiddenNuggetRenderers.Contains(renderer))
                    {
                        hiddenNuggetRenderers.Add(renderer);
                    }

                    renderer.enabled = false;
                }

                if (nugget.TryGetComponent<Collider>(out var collider))
                {
                    if (!hiddenNuggetColliders.Contains(collider))
                    {
                        hiddenNuggetColliders.Add(collider);
                    }

                    collider.enabled = false;
                }

                if (nugget.Rigidbody != null)
                {
                    nugget.Rigidbody.linearVelocity = Vector3.zero;
                    nugget.Rigidbody.angularVelocity = Vector3.zero;
                    nugget.Rigidbody.useGravity = false;
                    nugget.Rigidbody.isKinematic = true;
                }
            }
        }

        private void RestoreBowlNuggetVisuals()
        {
            foreach (var renderer in hiddenNuggetRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            foreach (var collider in hiddenNuggetColliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
        }

        private bool ValidateCoreReferences()
        {
            var hasAllRequiredReferences = true;

            hasAllRequiredReferences &= ValidateReference(bowlRestPoint, "SceneFinal_BowlRestPoint");
            hasAllRequiredReferences &= ValidateReference(bowlPourPoint, "SceneFinal_BowlPourPoint");
            hasAllRequiredReferences &= ValidateReference(pourStartPoint, "SceneFinal_PourStartPoint");
            hasAllRequiredReferences &= ValidateReference(pourEndPoint, "SceneFinal_PourEndPoint");
            hasAllRequiredReferences &= ValidateReference(moldCenterPoint, "SceneFinal_MoldCenterPoint");
            hasAllRequiredReferences &= ValidateReference(finalIngotObject, "SceneFinal_FinalIngot_Model");
            hasAllRequiredReferences &= ValidateReference(mainCamera, "Main Camera");

            if (bowlTransform == null)
            {
                WarnIfMissingOptional(null, "Bowl 2");
                hasAllRequiredReferences = false;
            }

            return hasAllRequiredReferences;
        }

        private bool ValidateReference(Object reference, string label)
        {
            if (reference != null)
            {
                return true;
            }

            WarnIfMissingOptional(null, label);
            return false;
        }

        private void SnapCameraToStartPoint()
        {
            if (mainCamera == null || cameraStartPoint == null)
            {
                return;
            }

            mainCamera.transform.SetPositionAndRotation(
                cameraStartPoint.position,
                ResolveCameraTargetRotation(cameraStartPoint));
        }

        private Quaternion ResolveCameraTargetRotation(Transform targetPoint)
        {
            if (targetPoint == null)
            {
                return Quaternion.identity;
            }

            if (cameraLookAtTarget == null)
            {
                return targetPoint.rotation;
            }

            var focusTargetPosition = cameraLookAtTarget.position;
            var lookDirection = focusTargetPosition - targetPoint.position;

            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return targetPoint.rotation;
            }

            var normalizedLookDirection = lookDirection.normalized;
            var isTopDownView = Mathf.Abs(Vector3.Dot(normalizedLookDirection, Vector3.down)) >= topDownRollThreshold;
            var upVector = isTopDownView
                ? ResolveTopDownCameraUpVector(normalizedLookDirection)
                : Vector3.up;

            var resolvedRotation = Quaternion.LookRotation(normalizedLookDirection, upVector);

            if (isTopDownView && Mathf.Abs(topDownRollOffset) > 0.01f)
            {
                resolvedRotation = Quaternion.AngleAxis(topDownRollOffset, normalizedLookDirection) * resolvedRotation;
            }

            return resolvedRotation;
        }

        private Vector3 ResolveTopDownCameraUpVector(Vector3 normalizedLookDirection)
        {
            if (TryProjectAsUpVector(finalIngotObject != null ? finalIngotObject.transform.up : Vector3.zero, normalizedLookDirection, out var resolvedUp))
            {
                return resolvedUp;
            }

            if (TryProjectAsUpVector(workTable != null ? workTable.forward : Vector3.zero, normalizedLookDirection, out resolvedUp))
            {
                return resolvedUp;
            }

            if (TryProjectAsUpVector(workTable != null ? workTable.right : Vector3.zero, normalizedLookDirection, out resolvedUp))
            {
                return resolvedUp;
            }

            if (TryProjectAsUpVector(Vector3.forward, normalizedLookDirection, out resolvedUp))
            {
                return resolvedUp;
            }

            if (TryProjectAsUpVector(Vector3.right, normalizedLookDirection, out resolvedUp))
            {
                return resolvedUp;
            }

            return Vector3.forward;
        }

        private static bool TryProjectAsUpVector(Vector3 candidateVector, Vector3 normalizedLookDirection, out Vector3 resolvedUp)
        {
            if (candidateVector.sqrMagnitude <= 0.0001f)
            {
                resolvedUp = Vector3.zero;
                return false;
            }

            resolvedUp = Vector3.ProjectOnPlane(candidateVector.normalized, normalizedLookDirection);

            if (resolvedUp.sqrMagnitude <= 0.0001f)
            {
                resolvedUp = Vector3.zero;
                return false;
            }

            resolvedUp.Normalize();
            return true;
        }

        private Quaternion CalculateTiltedRotation(Quaternion baseRotation)
        {
            if (tiltBowlTowardPourTarget && bowlTransform != null)
            {
                var targetPoint = pourEndPoint != null
                    ? pourEndPoint.position
                    : (moldCenterPoint != null ? moldCenterPoint.position : bowlTransform.position + (baseRotation * Vector3.left));

                var targetDirection = targetPoint - bowlTransform.position;
                var horizontalDirection = Vector3.ProjectOnPlane(targetDirection, Vector3.up);
                var bowlUp = baseRotation * Vector3.up;

                if (horizontalDirection.sqrMagnitude > 0.0001f)
                {
                    var worldAxis = Vector3.Cross(bowlUp.normalized, horizontalDirection.normalized);

                    if (worldAxis.sqrMagnitude > 0.0001f)
                    {
                        return Quaternion.AngleAxis(bowlTiltAngle, worldAxis.normalized) * baseRotation;
                    }
                }
            }

            var fallbackAxis = bowlTiltAxis.sqrMagnitude > 0.0001f ? bowlTiltAxis.normalized : Vector3.forward;
            var fallbackWorldAxis = baseRotation * fallbackAxis;
            return Quaternion.AngleAxis(-bowlTiltAngle, fallbackWorldAxis) * baseRotation;
        }

        private void CreateMoldPart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material sharedMaterial)
        {
            var existingPart = parent.Find(name);
            GameObject partObject;

            if (existingPart != null)
            {
                partObject = existingPart.gameObject;
            }
            else
            {
                partObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                partObject.name = name;
                partObject.transform.SetParent(parent, false);
            }

            partObject.transform.localPosition = localPosition;
            partObject.transform.localRotation = Quaternion.identity;
            partObject.transform.localScale = localScale;

            if (partObject.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private Transform FindTransformByName(string targetName)
        {
            var activeScene = SceneManager.GetActiveScene();

            foreach (var rootObject in activeScene.GetRootGameObjects())
            {
                var match = FindTransformRecursive(rootObject.transform, targetName);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform current, string targetName)
        {
            if (current == null)
            {
                return null;
            }

            if (current.name == targetName)
            {
                return current;
            }

            for (var index = 0; index < current.childCount; index++)
            {
                var match = FindTransformRecursive(current.GetChild(index), targetName);

                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private ParticleSystem FindParticleSystemByName(string targetName)
        {
            var targetTransform = FindTransformByName(targetName);
            return targetTransform != null ? targetTransform.GetComponent<ParticleSystem>() : null;
        }

        private Light FindLightByName(string targetName)
        {
            var targetTransform = FindTransformByName(targetName);
            return targetTransform != null ? targetTransform.GetComponent<Light>() : null;
        }

        private void WarnIfMissingOptional(Object reference, string label)
        {
            if (reference != null || !warnedMissingReferences.Add(label))
            {
                return;
            }

            Debug.LogWarning($"Missing optional Scene_FinalIngot reference: {label}. The sequence will continue without it.", this);
        }

        private static void PlayParticleSystem(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return;
            }

            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        private static void StopParticleSystem(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                return;
            }

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void SetLightIntensity(Light lightSource, float intensity)
        {
            if (lightSource != null)
            {
                lightSource.intensity = intensity;
            }
        }

        private static bool WasKeyPressedThisFrame(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null && TryMapKeyCodeToInputSystemKey(keyCode, out var inputSystemKey))
            {
                var keyControl = keyboard[inputSystemKey];

                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryMapKeyCodeToInputSystemKey(KeyCode keyCode, out Key inputSystemKey)
        {
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                inputSystemKey = (Key)((int)Key.A + (keyCode - KeyCode.A));
                return true;
            }

            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
            {
                inputSystemKey = (Key)((int)Key.Digit0 + (keyCode - KeyCode.Alpha0));
                return true;
            }

            if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
            {
                inputSystemKey = (Key)((int)Key.Numpad0 + (keyCode - KeyCode.Keypad0));
                return true;
            }

            switch (keyCode)
            {
                case KeyCode.Return:
                    inputSystemKey = Key.Enter;
                    return true;
                case KeyCode.KeypadEnter:
                    inputSystemKey = Key.NumpadEnter;
                    return true;
                case KeyCode.Space:
                    inputSystemKey = Key.Space;
                    return true;
                case KeyCode.Escape:
                    inputSystemKey = Key.Escape;
                    return true;
                case KeyCode.Backspace:
                    inputSystemKey = Key.Backspace;
                    return true;
                case KeyCode.Tab:
                    inputSystemKey = Key.Tab;
                    return true;
                case KeyCode.LeftArrow:
                    inputSystemKey = Key.LeftArrow;
                    return true;
                case KeyCode.RightArrow:
                    inputSystemKey = Key.RightArrow;
                    return true;
                case KeyCode.UpArrow:
                    inputSystemKey = Key.UpArrow;
                    return true;
                case KeyCode.DownArrow:
                    inputSystemKey = Key.DownArrow;
                    return true;
                case KeyCode.LeftShift:
                    inputSystemKey = Key.LeftShift;
                    return true;
                case KeyCode.RightShift:
                    inputSystemKey = Key.RightShift;
                    return true;
                case KeyCode.LeftControl:
                    inputSystemKey = Key.LeftCtrl;
                    return true;
                case KeyCode.RightControl:
                    inputSystemKey = Key.RightCtrl;
                    return true;
                case KeyCode.LeftAlt:
                    inputSystemKey = Key.LeftAlt;
                    return true;
                case KeyCode.RightAlt:
                    inputSystemKey = Key.RightAlt;
                    return true;
                case KeyCode.Delete:
                    inputSystemKey = Key.Delete;
                    return true;
                default:
                    inputSystemKey = Key.None;
                    return false;
            }
        }
#endif
    }

    internal static class FinalIngotRuntimeMaterials
    {
        private static Material moltenGoldMaterial;
        private static Material darkMetalMaterial;

        internal static Material GetMoltenGoldMaterial()
        {
            if (moltenGoldMaterial != null)
            {
                return moltenGoldMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            moltenGoldMaterial = new Material(shader)
            {
                name = "M_Runtime_MoltenGold"
            };
            moltenGoldMaterial.SetColor("_BaseColor", new Color(1f, 0.62f, 0.08f, 1f));
            moltenGoldMaterial.SetColor("_EmissionColor", new Color(1.15f, 0.46f, 0.08f, 1f));

            if (moltenGoldMaterial.HasProperty("_Metallic"))
            {
                moltenGoldMaterial.SetFloat("_Metallic", 0.82f);
            }

            if (moltenGoldMaterial.HasProperty("_Smoothness"))
            {
                moltenGoldMaterial.SetFloat("_Smoothness", 0.94f);
            }

            moltenGoldMaterial.EnableKeyword("_EMISSION");
            return moltenGoldMaterial;
        }

        internal static Material GetDarkMetalMaterial()
        {
            if (darkMetalMaterial != null)
            {
                return darkMetalMaterial;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            darkMetalMaterial = new Material(shader)
            {
                name = "M_Runtime_MoldMetal"
            };
            darkMetalMaterial.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.2f, 1f));

            if (darkMetalMaterial.HasProperty("_Metallic"))
            {
                darkMetalMaterial.SetFloat("_Metallic", 0.9f);
            }

            if (darkMetalMaterial.HasProperty("_Smoothness"))
            {
                darkMetalMaterial.SetFloat("_Smoothness", 0.48f);
            }

            return darkMetalMaterial;
        }
    }
}
