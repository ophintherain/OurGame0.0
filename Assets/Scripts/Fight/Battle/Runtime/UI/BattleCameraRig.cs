using System;
using System.Reflection;
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
        [SerializeField] private Transform battleVisualRoot;
        [SerializeField] private BattleArena arena;
        [SerializeField] private PlayerBattleController player;
        private float targetSize;
        private float velocity;
        private bool battleActive;
        private Spotlight.CameraController cameraController;
        private Transform shakeDriver;
        private object shakePresets;
        private MethodInfo shortShake;
        private Vector3 appliedShakeOffset;

        public bool IsOverviewZoom { get; private set; }
        public float TargetSize => targetSize;

        public void RequestImpact(float strength)
        {
            if (!battleActive || controlledCamera == null) return;
            EnsureShakeDriver();
            shortShake?.Invoke(shakePresets, new object[]
            {
                Mathf.Clamp(strength, 0.01f, 0.16f), 0f, 28f, 3
            });
        }

        public void Configure(Camera camera, float normalSize, float combatSize,
            Transform visualRoot = null, BattleArena battleArena = null,
            PlayerBattleController battlePlayer = null)
        {
            controlledCamera = camera;
            battleVisualRoot = visualRoot;
            arena = battleArena;
            player = battlePlayer;
            cameraController = camera != null ? camera.GetComponent<Spotlight.CameraController>() : null;
            explorationSize = normalSize;
            battleSize = combatSize;
            overviewSize = Mathf.Max(combatSize + 0.1f, combatSize + 1.2f);
            targetSize = normalSize;
        }

        public void SetBattleView(bool active)
        {
            if (!active && controlledCamera != null)
            {
                controlledCamera.transform.position -= appliedShakeOffset;
                appliedShakeOffset = Vector3.zero;
            }
            battleActive = active;
            IsOverviewZoom = false;
            targetSize = active ? battleSize : explorationSize;
            cameraController ??= controlledCamera != null
                ? controlledCamera.GetComponent<Spotlight.CameraController>() : null;
            if (cameraController != null)
            {
                cameraController.SetFollowTarget(active ? null : player?.transform, controlledCamera);
                cameraController.SetTargetX(0f);
                if (active && controlledCamera != null)
                {
                    Vector3 position = controlledCamera.transform.position;
                    controlledCamera.transform.position = new Vector3(0f, position.y, position.z);
                }
            }
            if (active) FitBattleArtwork();
            else
            {
                if (battleVisualRoot != null) battleVisualRoot.localScale = Vector3.one;
                arena?.SetVisualScale(1f, 1f);
            }
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
            controlledCamera.transform.position -= appliedShakeOffset;
            appliedShakeOffset = Vector3.zero;
            controlledCamera.orthographicSize = Mathf.SmoothDamp(controlledCamera.orthographicSize,
                targetSize, ref velocity, smoothTime);
            if (battleActive) FitBattleArtwork();
            if (battleActive && shakeDriver != null)
            {
                appliedShakeOffset = shakeDriver.localPosition;
                controlledCamera.transform.position += appliedShakeOffset;
            }
        }

        private void EnsureShakeDriver()
        {
            if (shakeDriver != null) return;
            Type shakerType = Type.GetType("CameraShake.CameraShaker, com.gasgiant.camera-shake");
            if (shakerType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    shakerType = assembly.GetType("CameraShake.CameraShaker");
                    if (shakerType != null) break;
                }
            }
            if (shakerType == null) return;
            GameObject driver = new GameObject("BattleHitShakeDriver");
            driver.transform.SetParent(transform, false);
            shakeDriver = driver.transform;
            Component shaker = driver.AddComponent(shakerType);
            shakePresets = shakerType.GetField("ShakePresets")?.GetValue(shaker);
            shortShake = shakePresets?.GetType().GetMethod("ShortShake2D");
        }

        private void FitBattleArtwork()
        {
            if (controlledCamera == null || battleVisualRoot == null || arena == null) return;
            float height = controlledCamera.orthographicSize * 2f;
            float width = height * controlledCamera.aspect;
            float horizontal = width / 17.78f;
            float vertical = height / 10f;
            battleVisualRoot.localScale = new Vector3(horizontal, vertical, 1f);
            arena.SetVisualScale(horizontal, vertical);
            player?.RefreshVisualPosition();
        }
    }
}
