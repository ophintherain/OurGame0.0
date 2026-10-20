using System;
using Spotlight.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 第三车厢「跟随鼓点」怪异：拿到本车厢武器后，按固定间隔点击屏幕 5 次完成。
    /// 间隔超出允许误差时连续计数清零，本次点击作为新的第 1 次。
    /// </summary>
    public sealed class ExplorationBeatWeirdness : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("场景里的 ExplorationInventory，用于判断是否已经拿到本车厢武器。留空会自动查找")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("场景里的 StoryDirector。剧情播放期间暂停节拍判定，避免误触")]
        private StoryDirector storyDirector;

        [SerializeField]
        [Tooltip("Player 上的 ExplorationPlayerController。玩家不可控时不处理点击")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("本车厢武器 ID。拿到其中任意一件后开始节拍判定，默认 1005 / 1008 / 1009")]
        private int[] requiredWeaponIds = { 1005, 1008, 1009 };

        [SerializeField, Min(0.05f)]
        [Tooltip("相邻两次点击的目标间隔（秒）。默认 0.714，约等于 84 BPM 的鼓点")]
        private float targetInterval = 0.714f;

        [SerializeField, Min(0f)]
        [Tooltip("允许的间隔误差（秒）。默认 0.1，即实际间隔需要在 0.614 ~ 0.814 秒之间")]
        private float tolerance = 0.1f;

        [SerializeField, Min(1)]
        [Tooltip("需要连续点击多少次完成。默认 5；第 1 次开始计时，之后 4 次间隔正确即可")]
        private int requiredClicks = 5;

        [SerializeField, Min(1)]
        [Tooltip("车厢号，完成时写入 RunProgress。第三车厢填 3")]
        private int carriage = 3;

        [SerializeField]
        [Tooltip("怪异完成时触发，供外部接战斗 / 下一场景转场；这里不会自动加载场景")]
        private UnityEvent onCompleted;

        /// <summary>怪异完成事件，参数为车厢号。</summary>
        public event Action<int> Completed;

        /// <summary>是否已经完成。</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>是否已经拿到本车厢武器、开始接受节拍点击。</summary>
        public bool IsArmed { get; private set; }

        private int clickCount;
        private float lastClickTime;

        private void Awake()
        {
            if (inventory == null)
            {
                inventory = FindObjectOfType<ExplorationInventory>();
            }

            if (storyDirector == null)
            {
                storyDirector = FindObjectOfType<StoryDirector>();
            }

            if (player == null)
            {
                player = FindObjectOfType<ExplorationPlayerController>();
            }

            if (inventory != null)
            {
                inventory.Changed += OnInventoryChanged;
            }
        }

        private void Start()
        {
            EvaluateWeapon();
        }

        private void OnDestroy()
        {
            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }
        }

        private void Update()
        {
            if (!IsArmed || IsCompleted)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // 点 UI 不算节拍
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (storyDirector != null && storyDirector.IsPlaying)
            {
                return;
            }

            if (player != null && !player.ControlEnabled)
            {
                return;
            }

            float now = Time.unscaledTime;
            Spotlight.UISoundPlayer.PlayClickOncePerFrame();

            if (clickCount == 0)
            {
                clickCount = 1;
                lastClickTime = now;
                return;
            }

            float interval = now - lastClickTime;
            lastClickTime = now;

            if (interval >= targetInterval - tolerance && interval <= targetInterval + tolerance)
            {
                clickCount++;

                if (clickCount >= requiredClicks)
                {
                    Complete();
                }

                return;
            }

            // 间隔不对：这一次作为新的第 1 次
            clickCount = 1;
        }

        private void OnInventoryChanged()
        {
            EvaluateWeapon();
        }

        private void EvaluateWeapon()
        {
            if (IsArmed || IsCompleted || inventory == null || requiredWeaponIds == null)
            {
                return;
            }

            for (int index = 0; index < requiredWeaponIds.Length; index++)
            {
                if (inventory.Contains(requiredWeaponIds[index]))
                {
                    IsArmed = true;
                    return;
                }
            }
        }

        private void Complete()
        {
            IsCompleted = true;

            if (inventory != null)
            {
                inventory.Changed -= OnInventoryChanged;
            }

            Debug.Log(
                $"[Weirdness] 第三车厢节拍怪异完成：连续点击 {requiredClicks} 次，" +
                $"目标间隔 {targetInterval:0.###} 秒（±{tolerance:0.###} 秒）。",
                this);

            RunProgress.CompleteExploration(carriage);
            Completed?.Invoke(carriage);
            onCompleted?.Invoke();
        }
    }
}
