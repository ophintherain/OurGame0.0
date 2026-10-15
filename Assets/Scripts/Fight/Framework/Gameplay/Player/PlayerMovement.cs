using UnityEngine;

namespace Spotlight
{
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 5f;
        [SerializeField] private float minX = -8f;
        [SerializeField] private float maxX = 8f;

        private float targetX;

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

        public void SimulateStep(float deltaTime)
        {
            Vector3 current = transform.position;
            float clampedCurrentX = Mathf.Clamp(current.x, minX, maxX);
            float nextX = Mathf.MoveTowards(clampedCurrentX, targetX, speed * Mathf.Max(0f, deltaTime));
            transform.position = new Vector3(Mathf.Clamp(nextX, minX, maxX), current.y, current.z);
        }
    }
}
