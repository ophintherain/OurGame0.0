using UnityEngine;

namespace Spotlight
{
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform followTarget;
        [SerializeField] private float minX = -6f;
        [SerializeField] private float maxX = 6f;
        [SerializeField, Min(0.001f)] private float smoothTime = 0.2f;
        [SerializeField, Range(0f, 0.49f)] private float leftViewportEdge = 0.25f;
        [SerializeField, Range(0.51f, 1f)] private float rightViewportEdge = 0.75f;

        private float targetX;
        private float currentVelocity;

        public float TargetX => targetX;
        public float MinX => minX;
        public float MaxX => maxX;

        public void Configure(Transform controlledTransform, float minimumX, float maximumX, float smoothing)
        {
            cameraTransform = controlledTransform;
            minX = Mathf.Min(minimumX, maximumX);
            maxX = Mathf.Max(minimumX, maximumX);
            smoothTime = Mathf.Max(0.001f, smoothing);
            Transform targetTransform = cameraTransform != null ? cameraTransform : transform;
            targetX = Mathf.Clamp(targetTransform.position.x, minX, maxX);
        }

        public void SetFollowTarget(Transform target, Camera camera)
        {
            followTarget = target;
            controlledCamera = camera;
        }

        private void Awake()
        {
            cameraTransform ??= transform;
            controlledCamera ??= GetComponent<Camera>();
            float minimum = Mathf.Min(minX, maxX);
            float maximum = Mathf.Max(minX, maxX);
            minX = minimum;
            maxX = maximum;
            targetX = Mathf.Clamp(cameraTransform.position.x, minX, maxX);
        }

        private void LateUpdate()
        {
            UpdateFollowTarget();
            SimulateStep(Time.deltaTime);
        }

        public void UpdateFollowTarget()
        {
            if (followTarget == null || controlledCamera == null)
            {
                return;
            }

            Vector3 viewportPosition = controlledCamera.WorldToViewportPoint(followTarget.position);
            if (viewportPosition.z <= 0f)
            {
                return;
            }

            float boundary = viewportPosition.x < leftViewportEdge
                ? leftViewportEdge
                : viewportPosition.x > rightViewportEdge
                    ? rightViewportEdge
                    : -1f;
            if (boundary < 0f)
            {
                return;
            }

            Vector3 boundaryWorld = controlledCamera.ViewportToWorldPoint(
                new Vector3(boundary, viewportPosition.y, viewportPosition.z));
            float desiredCameraX = cameraTransform.position.x + followTarget.position.x - boundaryWorld.x;
            SetTargetX(desiredCameraX);
        }

        public void SetTargetX(float worldX)
        {
            targetX = Mathf.Clamp(worldX, minX, maxX);
        }

        public void SimulateStep(float deltaTime)
        {
            cameraTransform ??= transform;
            Vector3 current = cameraTransform.position;
            float clampedCurrentX = Mathf.Clamp(current.x, minX, maxX);
            float nextX = Mathf.SmoothDamp(clampedCurrentX, targetX, ref currentVelocity,
                smoothTime, Mathf.Infinity, Mathf.Max(0.0001f, deltaTime));
            cameraTransform.position = new Vector3(Mathf.Clamp(nextX, minX, maxX), current.y, current.z);
        }
    }
}
