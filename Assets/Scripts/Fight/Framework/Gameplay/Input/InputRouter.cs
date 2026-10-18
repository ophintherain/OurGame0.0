using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Spotlight
{
    public sealed class InputRouter : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private InteractionController interactionController;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private LayerMask interactableMask;
        [SerializeField] private LayerMask walkableMask;
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private bool worldInputEnabled = true;
        [SerializeField] private MonoBehaviour commandSinkSource;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private IGameplayInputSink commandSink;

        public event Action<InputRouteResult> RouteResolved;
        public bool InputEnabled => inputEnabled;
        public bool WorldInputEnabled => worldInputEnabled;
        public InputRouteResult LastRoute { get; private set; } = InputRouteResult.Ignored;

        public void Configure(Camera camera, InteractionController interactions,
            PlayerMovement movement, LayerMask interactionLayers, LayerMask walkableLayers)
        {
            worldCamera = camera;
            interactionController = interactions;
            playerMovement = movement;
            interactableMask = interactionLayers;
            walkableMask = walkableLayers;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                RoutePointerDown(Input.mousePosition);
            }

            RouteKeyboardCommands();
            RouteExplorationMovement();
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                playerMovement?.SetHorizontalInput(0f);
            }
        }

        public void SetWorldInputEnabled(bool enabled)
        {
            worldInputEnabled = enabled;
            if (!enabled)
            {
                playerMovement?.SetHorizontalInput(0f);
            }
        }

        public void SetCommandSink(IGameplayInputSink sink)
        {
            commandSink = sink;
            commandSinkSource = sink as MonoBehaviour;
        }

        public bool RouteCommand(GameplayInputCommand command)
        {
            ResolveCommandSink();
            return inputEnabled && commandSink != null && commandSink.CanHandleInput &&
                   commandSink.TryHandle(command);
        }

        public InputRouteResult RoutePointerDown(Vector2 screenPosition)
        {
            if (!inputEnabled)
            {
                return Resolve(InputRouteResult.Disabled);
            }

            if (IsPointerOverUI(screenPosition))
            {
                UISoundPlayer.Play(UISoundCue.Click);
                return Resolve(InputRouteResult.UI);
            }

            if (!worldInputEnabled)
            {
                return Resolve(InputRouteResult.Disabled);
            }

            worldCamera ??= Camera.main;
            if (worldCamera == null)
            {
                return Resolve(InputRouteResult.Ignored);
            }

            float depth = Mathf.Abs(worldCamera.transform.position.z);
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            Vector2 worldPoint = new Vector2(world.x, world.y);

            Collider2D interactionTarget = Physics2D.OverlapPoint(worldPoint, interactableMask);
            if (interactionTarget != null)
            {
                interactionController?.TryInteract(interactionTarget);
                return Resolve(InputRouteResult.Interaction);
            }

            return Resolve(InputRouteResult.Ignored);
        }

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            uiHits.Clear();
            PointerEventData eventData = new PointerEventData(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(eventData, uiHits);
            return uiHits.Count > 0;
        }

        private InputRouteResult Resolve(InputRouteResult result)
        {
            LastRoute = result;
            RouteResolved?.Invoke(result);
            return result;
        }


        private void ResolveCommandSink()
        {
            if (commandSink == null && commandSinkSource != null)
            {
                commandSink = commandSinkSource as IGameplayInputSink;
            }
        }

        private void RouteKeyboardCommands()
        {
            if (!inputEnabled)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) RouteCommand(GameplayInputCommand.MoveLeft);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) RouteCommand(GameplayInputCommand.MoveRight);
            if (Input.GetKeyDown(KeyCode.Alpha1)) RouteCommand(GameplayInputCommand.LaneFront);
            if (Input.GetKeyDown(KeyCode.Alpha2)) RouteCommand(GameplayInputCommand.LaneBack);
            if (Input.GetKeyDown(KeyCode.S)) RouteCommand(GameplayInputCommand.Prone);
            if (Input.GetKeyDown(KeyCode.W)) RouteCommand(GameplayInputCommand.OnTable);
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) RouteCommand(GameplayInputCommand.Stand);
            if (Input.GetKeyDown(KeyCode.F)) RouteCommand(GameplayInputCommand.UseWeapon1);
            if (Input.GetKeyDown(KeyCode.G)) RouteCommand(GameplayInputCommand.UseWeapon2);
            if (Input.GetKeyDown(KeyCode.H)) RouteCommand(GameplayInputCommand.UseWeapon3);
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) RouteCommand(GameplayInputCommand.ToggleOverviewZoom);
            if (Input.GetKeyDown(KeyCode.E)) RouteCommand(GameplayInputCommand.Escape);
            if (Input.GetKeyDown(KeyCode.Return)) RouteCommand(GameplayInputCommand.StartOrRestart);
        }

        private void RouteExplorationMovement()
        {
            if (playerMovement == null || !playerMovement.enabled)
            {
                return;
            }

            if (!inputEnabled || !worldInputEnabled)
            {
                playerMovement.SetHorizontalInput(0f);
                return;
            }

            float left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f;
            float right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
            playerMovement.SetHorizontalInput(left + right);
        }
    }
}
