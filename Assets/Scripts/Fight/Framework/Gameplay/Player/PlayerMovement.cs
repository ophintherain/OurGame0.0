using UnityEngine;

namespace Spotlight
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 5f;
        [SerializeField] private float minX = -8f;
        [SerializeField] private float maxX = 8f;

        private float targetX;
        private float horizontalInput;

        public float TargetX => targetX;
        public float MinX => minX;
        public float MaxX => maxX;

        public void Configure(float movementSpeed, float minimumX, float maximumX)
        {
            speed = Mathf.Max(0f, movementSpeed);
            minX = Mathf.Min(minimumX, maximumX);
            maxX = Mathf.Max(minimumX, maximumX);
            targetX = Mathf.Clamp(transform.position.x, minX, maxX);
        }

        private void Awake()
        {
            float minimum = Mathf.Min(minX, maxX);
            float maximum = Mathf.Max(minX, maxX);
            minX = minimum;
            maxX = maximum;
            targetX = Mathf.Clamp(transform.position.x, minX, maxX);
        }

        private void Update()
        {
            SimulateStep(Time.deltaTime);
        }

        public void SetTargetX(float worldX)
        {
            targetX = Mathf.Clamp(worldX, minX, maxX);
        }

        public void SetHorizontalInput(float direction)
        {
            float next = Mathf.Clamp(direction, -1f, 1f);
            if (horizontalInput != 0f && next == 0f)
            {
                targetX = transform.position.x;
            }
            horizontalInput = next;
        }

        public void SimulateStep(float deltaTime)
        {
            Vector3 current = transform.position;
            float clampedCurrentX = Mathf.Clamp(current.x, minX, maxX);
            float distance = speed * Mathf.Max(0f, deltaTime);
            float nextX = horizontalInput == 0f
                ? Mathf.MoveTowards(clampedCurrentX, targetX, distance)
                : clampedCurrentX + horizontalInput * distance;
            if (horizontalInput != 0f)
            {
                targetX = Mathf.Clamp(nextX, minX, maxX);
            }
            transform.position = new Vector3(Mathf.Clamp(nextX, minX, maxX), current.y, current.z);
        }
    }
}
