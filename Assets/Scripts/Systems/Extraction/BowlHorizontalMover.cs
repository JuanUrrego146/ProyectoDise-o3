using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LingoteRush.Systems.Extraction
{
    [DisallowMultipleComponent]
    public sealed class BowlHorizontalMover : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float moveSpeed = 1.2f;
        [SerializeField] private float minX = -0.2f;
        [SerializeField] private float maxX = 0.2f;

        private Rigidbody cachedRigidbody;
        private float horizontalInput;
        private float fixedY;
        private float fixedZ;
        private Quaternion fixedRotation;

        private bool HasRigidbody => cachedRigidbody != null;

        private void Awake()
        {
            CacheComponents();
            CacheFixedTransformState();
            ConfigureRigidbody();
            ClampImmediately();
        }

        private void OnValidate()
        {
            if (maxX < minX)
            {
                maxX = minX;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            CacheComponents();
            CacheFixedTransformState();
            ConfigureRigidbody();
            ClampImmediately();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            horizontalInput = ReadHorizontalInput();

            if (!HasRigidbody)
            {
                ApplyMovement(Time.deltaTime, useRigidbody: false);
            }
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying || !HasRigidbody)
            {
                return;
            }

            ApplyMovement(Time.fixedDeltaTime, useRigidbody: true);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || HasRigidbody)
            {
                return;
            }

            transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), fixedY, fixedZ);
            transform.rotation = fixedRotation;
        }

        private void CacheComponents()
        {
            TryGetComponent(out cachedRigidbody);
        }

        private void CacheFixedTransformState()
        {
            var currentPosition = transform.position;
            fixedY = currentPosition.y;
            fixedZ = currentPosition.z;
            fixedRotation = transform.rotation;
        }

        private void ConfigureRigidbody()
        {
            if (!HasRigidbody)
            {
                return;
            }

            cachedRigidbody.isKinematic = true;
            cachedRigidbody.useGravity = false;
            cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            cachedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            cachedRigidbody.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
        }

        private void ClampImmediately()
        {
            var clampedPosition = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), fixedY, fixedZ);

            if (HasRigidbody && Application.isPlaying)
            {
                cachedRigidbody.position = clampedPosition;
                cachedRigidbody.rotation = fixedRotation;
                return;
            }

            transform.position = clampedPosition;
            transform.rotation = fixedRotation;
        }

        private void ApplyMovement(float deltaTime, bool useRigidbody)
        {
            var currentX = useRigidbody ? cachedRigidbody.position.x : transform.position.x;
            var targetX = Mathf.Clamp(currentX + (horizontalInput * moveSpeed * deltaTime), minX, maxX);
            var targetPosition = new Vector3(targetX, fixedY, fixedZ);

            if (useRigidbody)
            {
                cachedRigidbody.MovePosition(targetPosition);
                cachedRigidbody.MoveRotation(fixedRotation);
                return;
            }

            transform.position = targetPosition;
            transform.rotation = fixedRotation;
        }

        private static float ReadHorizontalInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            if (keyboard != null)
            {
                var input = 0f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    input -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    input += 1f;
                }

                return input;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }
    }
}
