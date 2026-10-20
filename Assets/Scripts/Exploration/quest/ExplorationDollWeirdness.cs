using System;
using Spotlight.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 第二车厢「操纵人偶」怪异：
    /// 靠近人偶按 F 开始操纵并播第一句；连续操纵满设定秒数后翻第二句；
    /// 结束操纵后点击红色开关完成怪异。完成只派发事件，不自动加载场景。
    /// </summary>
    public sealed class ExplorationDollWeirdness : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("场景里的 StoryDirector，用于播提示 / 怪异广播。留空会自动查找")]
        private StoryDirector storyDirector;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController，用于锁定 / 恢复本体控制。留空会自动查找")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("场景相机上的 ExplorationCameraController，操纵人偶时临时跟随人偶。留空会自动查找")]
        private ExplorationCameraController cameraController;

        [SerializeField]
        [Tooltip("红色开关上的 RuleClickTarget，点击它完成怪异。必填")]
        private RuleClickTarget switchTarget;

        [SerializeField]
        [Tooltip("背包面板，操纵期间需要关闭并禁用开关。留空会自动查找")]
        private BackpackPanel backpackPanel;

        [SerializeField]
        [Tooltip("任务面板，操纵期间需要关闭并禁用开关。留空会自动查找")]
        private ExplorationTaskPanel taskPanel;

        [SerializeField]
        [Tooltip("背包，完成怪异前检查是否已收集本车厢武器。留空会自动查找")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("完成怪异前必须至少拥有其中一件武器。留空时默认使用第二车厢的 1003 / 1004 / 1007")]
        private int[] requiredWeaponIds = { 1003, 1004, 1007 };

        [SerializeField]
        [Tooltip("怪异广播 ID。默认 ch2_rule_05（操纵人偶，打开开关 / 禁止人偶打开机关）")]
        private string storyId = "ch2_rule_05";

        [SerializeField]
        [Tooltip("进入场景时播放的提示广播 ID。默认 ch2_hint_doll（按 F 可与人偶交互）")]
        private string entryHintStoryId = "ch2_hint_doll";

        [SerializeField, Min(0f)]
        [Tooltip("进入场景后延迟多少秒播放提示广播。默认 1.5 秒")]
        private float entryHintDelaySeconds = 1.5f;

        [SerializeField, Min(0.1f)]
        [Tooltip("需要连续操纵人偶多少秒才翻到第二句。中途退出会重新计时，默认 5 秒")]
        private float operateSeconds = 5f;

        [SerializeField, Min(1)]
        [Tooltip("车厢号，怪异完成时写入 RunProgress。第二车厢填 2")]
        private int carriage = 2;

        [SerializeField]
        [Tooltip("怪异完成时触发，供外部接战斗 / 下一场景转场；这里不会自动加载场景")]
        private UnityEvent onCompleted;

        /// <summary>怪异完成事件，参数为车厢号。</summary>
        public event Action<int> Completed;

        /// <summary>怪异是否已经完成。</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>当前是否有玩家正在操纵人偶。</summary>
        public bool IsOperating => operatingDoll != null;

        private ExplorationDollOperable operatingDoll;
        private float operateTimer;
        private bool secondLineShown;
        private float hintTimer;
        private bool hintPlayed;

        private void Awake()
        {
            if (storyDirector == null)
            {
                storyDirector = FindObjectOfType<StoryDirector>();
            }

            if (player == null)
            {
                player = FindObjectOfType<ExplorationPlayerController>();
            }

            if (cameraController == null)
            {
                cameraController = FindObjectOfType<ExplorationCameraController>();
            }

            if (backpackPanel == null)
            {
                backpackPanel = FindObjectOfType<BackpackPanel>();
            }

            if (taskPanel == null)
            {
                taskPanel = FindObjectOfType<ExplorationTaskPanel>();
            }

            if (inventory == null)
            {
                inventory = FindObjectOfType<ExplorationInventory>();
            }

            if (requiredWeaponIds == null || requiredWeaponIds.Length == 0)
            {
                requiredWeaponIds = new[] { 1003, 1004, 1007 };
            }
        }

        private void OnEnable()
        {
            if (switchTarget != null)
            {
                switchTarget.Clicked += OnSwitchClicked;
            }

            if (storyDirector != null)
            {
                storyDirector.StoryFinished += OnStoryFinished;
            }
        }

        private void OnDisable()
        {
            if (switchTarget != null)
            {
                switchTarget.Clicked -= OnSwitchClicked;
            }

            if (storyDirector != null)
            {
                storyDirector.StoryFinished -= OnStoryFinished;
            }
        }

        private void Start()
        {
            hintTimer = Mathf.Max(0f, entryHintDelaySeconds);
            hintPlayed = string.IsNullOrWhiteSpace(entryHintStoryId);
        }

        private void Update()
        {
            if (!hintPlayed)
            {
                hintTimer -= Time.deltaTime;
                if (hintTimer <= 0f)
                {
                    TryPlayEntryHint();
                }
            }

            if (operatingDoll == null || secondLineShown)
            {
                return;
            }

            operateTimer += Time.deltaTime;

            if (operateTimer >= operateSeconds)
            {
                secondLineShown = true;
                storyDirector?.RequestContinue();
            }
        }

        /// <summary>尝试开始操纵人偶；成功返回 true，并接管本体控制与相机。</summary>
        public bool TryBeginOperate(ExplorationDollOperable doll)
        {
            if (!CanBeginOperate() || doll == null)
            {
                return false;
            }

            if (!storyDirector.Play(storyId, true))
            {
                return false;
            }

            storyDirector.SetAutoAdvance(0f);
            storyDirector.SetManualAdvanceEnabled(false);

            backpackPanel?.SetOpen(false);
            taskPanel?.SetOpen(false);
            backpackPanel?.SetInteractionEnabled(false);
            taskPanel?.SetInteractionEnabled(false);

            player.SetControlEnabled(false);
            cameraController?.SetFollowTarget(doll.transform);

            operatingDoll = doll;
            operateTimer = 0f;
            secondLineShown = false;
            doll.SetOperating(true);
            return true;
        }

        /// <summary>结束当前操纵：恢复本体控制与相机；未满 5 秒时收起广播并清零计时。</summary>
        public void EndOperate()
        {
            if (operatingDoll == null)
            {
                return;
            }

            ExplorationDollOperable doll = operatingDoll;
            operatingDoll = null;
            doll.SetOperating(false);

            if (player != null)
            {
                player.SetControlEnabled(true);
            }

            if (cameraController != null)
            {
                cameraController.ClearFollowTarget();
            }

            if (backpackPanel != null)
            {
                backpackPanel.SetInteractionEnabled(true);
            }

            if (taskPanel != null)
            {
                taskPanel.SetInteractionEnabled(true);
            }

            if (secondLineShown)
            {
                // 第二句已经出现：保留广播，等玩家去点开关
                return;
            }

            operateTimer = 0f;

            if (storyDirector != null)
            {
                storyDirector.HideCurrent();
                storyDirector.SetManualAdvanceEnabled(true);
            }
        }

        /// <summary>当前是否可以开始操纵。</summary>
        public bool CanBeginOperate()
        {
            return !IsCompleted && hintPlayed && operatingDoll == null &&
                   storyDirector != null && !storyDirector.IsPlaying &&
                   player != null && player.ControlEnabled && !player.IsMoving;
        }

        /// <summary>被操纵的人偶被隐藏 / 销毁时由人偶回调，强制结束操纵。</summary>
        public void NotifyDollUnavailable(ExplorationDollOperable doll)
        {
            if (operatingDoll == doll)
            {
                EndOperate();
            }
        }

        private void TryPlayEntryHint()
        {
            if (storyDirector == null)
            {
                hintPlayed = true;
                return;
            }

            if (storyDirector.IsPlaying)
            {
                hintTimer = 1f;
                return;
            }

            if (storyDirector.Play(entryHintStoryId))
            {
                storyDirector.SetAutoAdvance(3f);
                hintPlayed = true;
            }
            else
            {
                hintTimer = 1f;
            }
        }

        private void OnStoryFinished(string finishedId)
        {
            if (string.Equals(finishedId, entryHintStoryId, StringComparison.Ordinal))
            {
                storyDirector?.SetAutoAdvance(0f);
            }
        }

        private void OnSwitchClicked(string key, Vector3 worldPosition)
        {
            if (IsCompleted || operatingDoll != null || !secondLineShown)
            {
                return;
            }

            // 红色开关是“完成怪异并准备转场”的出口：本车厢还没收集到武器时不能完成
            if (!HasRequiredWeapon())
            {
                Debug.Log("[Weirdness] 还没有收集到本车厢武器，红色开关不会完成怪异。", this);
                return;
            }

            Complete();
        }

        private bool HasRequiredWeapon()
        {
            if (inventory == null || requiredWeaponIds == null)
            {
                return false;
            }

            for (int index = 0; index < requiredWeaponIds.Length; index++)
            {
                if (inventory.Contains(requiredWeaponIds[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private void Complete()
        {
            IsCompleted = true;

            storyDirector?.HideCurrent();
            storyDirector?.SetManualAdvanceEnabled(true);
            storyDirector?.SetAutoAdvance(0f);

            if (switchTarget != null)
            {
                switchTarget.Clicked -= OnSwitchClicked;
            }

            RunProgress.CompleteExploration(carriage);
            Completed?.Invoke(carriage);
            onCompleted?.Invoke();
        }
    }
}
