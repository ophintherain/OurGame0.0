using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private float explorationSize = 5f;
        [SerializeField] private float battleSize = 6.2f;
        [SerializeField] private float overviewSize = 7.4f;
        [SerializeField] private float smoothTime = 0.25f;
        private float targetSize;
        private float velocity;
        private bool battleActive;

        public bool IsOverviewZoom { get; private set; }
        public float TargetSize => targetSize;

        public void Configure(Camera camera, float normalSize, float combatSize)
        {
            controlledCamera = camera;
            explorationSize = normalSize;
            battleSize = combatSize;
            overviewSize = Mathf.Max(combatSize + 0.1f, combatSize + 1.2f);
            targetSize = normalSize;
        }

        public void SetBattleView(bool active)
        {
            battleActive = active;
            IsOverviewZoom = false;
            targetSize = active ? battleSize : explorationSize;
        }

        public bool ToggleOverviewZoom()
        {
            if (!battleActive)
            {
                return false;
            }

            IsOverviewZoom = !IsOverviewZoom;
            targetSize = IsOverviewZoom ? overviewSize : battleSize;
            return true;
        }

        private void Awake()
        {
            controlledCamera ??= Camera.main;
            targetSize = controlledCamera != null ? controlledCamera.orthographicSize : explorationSize;
        }

        private void LateUpdate()
        {
            if (controlledCamera == null) return;
            controlledCamera.orthographicSize = Mathf.SmoothDamp(controlledCamera.orthographicSize,
                targetSize, ref velocity, smoothTime);
        }
    }
}
