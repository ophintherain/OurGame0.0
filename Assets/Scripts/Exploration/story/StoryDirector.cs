using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 剧情调度：按 storyId 播放独立段落，处理锁玩家、面板互斥、音频与三种触发。
    /// 不负责决定下一段；下一段由玩法 / 触发源再次调用 Play。
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("必填。全部剧情段落的清单资产，由 Excel 导入工具生成 / 更新")]
        private StoryLibrary library;

        [SerializeField]
        [Tooltip("剧情表现层。留空会自动取同一个物体上的 StoryOverlayView")]
        private StoryOverlayView view;

        [SerializeField]
        [Tooltip("必填。Player 上的 ExplorationPlayerController，用于锁移动与读取 MoveCompleted")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("背包面板。叙事段落播放时自动关闭并禁用开关；留空会自动查找")]
        private BackpackPanel backpackPanel;

        [SerializeField]
        [Tooltip("任务面板。叙事段落播放时自动关闭并禁用开关；留空会自动查找")]
        private ExplorationTaskPanel taskPanel;

        [SerializeField]
        [Tooltip("探索点交互源。留空会自动查找，用于订阅 StoryTriggered")]
        private ExplorationInteractables interactables;

        [SerializeField]
        [Tooltip("立绘映射表。留空则所有立绘都不显示")]
        private StoryPortraitSet portraitSet;

        [SerializeField]
        [Tooltip("音效映射表。留空则所有音效都静音")]
        private StoryAudioSet audioSet;

        [SerializeField]
        [Tooltip("播放音效的 AudioSource。留空会自动取同物体上的组件，没有则自动添加")]
        private AudioSource audioSource;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("音效音量 0~1。0 = 静音，1 = 原始音量")]
        private float audioVolume = 1f;

        [SerializeField]
        [Tooltip("替换文本里的 #master name#。留空则直接移除该占位符")]
        private string playerDisplayName = string.Empty;

        [SerializeField]
        [Tooltip("替换文本里的 **文明用语**。默认 ***，留空则直接移除")]
        private string censorReplacement = "***";

        [SerializeField]
        [Tooltip("玩家走到指定列区间时触发的剧情列表。只在 MoveCompleted 时判定")]
        private StoryAreaTrigger[] areaTriggers = Array.Empty<StoryAreaTrigger>();

        [SerializeField]
        [Tooltip("任务完成时触发的剧情列表。taskIndex 0 = 收集武器，1 = 观测怪异")]
        private StoryTaskTrigger[] taskTriggers = Array.Empty<StoryTaskTrigger>();

        [SerializeField]
        [Tooltip("场景加载后自动播放的剧情ID列表，按顺序连续播放。留空则不在场景开始时自动播放")]
        private string[] autoPlayOnStart = Array.Empty<string>();

        [SerializeField, Min(0f)]
        [Tooltip("场景加载后延迟多少秒开始自动播放，避免和入场动画 / UI 初始化抢时序。0 = 下一帧开始")]
        private float autoPlayDelaySeconds = 0.5f;

        private readonly Dictionary<string, StoryDefinition> definitions =
            new Dictionary<string, StoryDefinition>();

        private readonly HashSet<string> playedOnce = new HashSet<string>();
        private readonly Queue<string> autoPlayQueue = new Queue<string>();
        private StoryDefinition current;
        private int lineIndex;
        private bool autoPlayChaining;

        /// <summary>段落开始播放（参数为 storyId）。</summary>
        public event Action<string> StoryStarted;

        /// <summary>段落结束或被 HideCurrent 收起（参数为 storyId）。</summary>
        public event Action<string> StoryFinished;

        /// <summary>当前是否有段落正在播放。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>当前播放的 storyId，没有则为 null。</summary>
        public string CurrentStoryId => current != null ? current.storyId : null;

        private void Awake()
        {
            view ??= GetComponent<StoryOverlayView>();
            player ??= FindObjectOfType<ExplorationPlayerController>();
            interactables ??= FindObjectOfType<ExplorationInteractables>();
            backpackPanel ??= FindObjectOfType<BackpackPanel>();
            taskPanel ??= FindObjectOfType<ExplorationTaskPanel>();

            if (library == null || view == null || player == null)
            {
                Debug.LogError(
                    "[Story] StoryDirector 缺少 library / view / player 引用，剧情系统已禁用。",
                    this);
                enabled = false;
                return;
            }

            BuildDefinitions();
            view.SetTextReplacements(playerDisplayName, censorReplacement);
            view.Hide();

            audioSource ??= GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Start()
        {
            if (autoPlayOnStart == null || autoPlayOnStart.Length == 0)
            {
                return;
            }

            StartCoroutine(AutoPlayStartRoutine());
        }

        private IEnumerator AutoPlayStartRoutine()
        {
            if (autoPlayDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(autoPlayDelaySeconds);
            }
            else
            {
                yield return null;
            }

            PlaySequence(autoPlayOnStart);
        }

        /// <summary>按 ID 播放一个段落。找不到 ID、正在播放叙事段或已播过一次时返回 false。</summary>
        public bool Play(string storyId)
        {
            if (string.IsNullOrWhiteSpace(storyId))
            {
                return false;
            }

            if (!definitions.TryGetValue(storyId.Trim(), out StoryDefinition definition))
            {
                Debug.LogWarning($"[Story] 找不到剧情ID：{storyId}", this);
                return false;
            }

            return Play(definition);
        }

        /// <summary>播放一个段落资产。</summary>
        public bool Play(StoryDefinition definition)
        {
            if (definition == null || definition.lines == null || definition.lines.Length == 0)
            {
                Debug.LogWarning("[Story] 要播放的剧情为空。", this);
                return false;
            }

            if (definition.playOnce &&
                !string.IsNullOrEmpty(definition.storyId) &&
                playedOnce.Contains(definition.storyId))
            {
                return false;
            }

            if (IsPlaying)
            {
                if (current == definition)
                {
                    return false;
                }

                // 叙事段落播放中不接受其他请求；广播 / 引导可以被新请求打断
                if (current != null && current.lockPlayer)
                {
                    Debug.LogWarning(
                        $"[Story] 叙事段落 {current.storyId} 播放中，忽略 {definition.storyId}。",
                        this);
                    return false;
                }

                autoPlayQueue.Clear();
                autoPlayChaining = false;
                FinishCurrent(false);
            }

            current = definition;
            lineIndex = 0;
            IsPlaying = true;

            if (current.lockPlayer)
            {
                backpackPanel?.SetOpen(false);
                taskPanel?.SetOpen(false);
                backpackPanel?.SetInteractionEnabled(false);
                taskPanel?.SetInteractionEnabled(false);
                player?.SetControlEnabled(false);
            }

            StoryStarted?.Invoke(current.storyId);
            ShowCurrentLine();
            return true;
        }

        /// <summary>玩法系统收起当前广播 / 引导。等价于结束当前段落并派发 StoryFinished。</summary>
        public void HideCurrent()
        {
            FinishCurrent();
        }

        /// <summary>
        /// 按顺序连续播放一组剧情ID。用于场景加载、转场结束或纯叙事连播。
        /// 中间如果要插入探索 / 战斗，不要把后续段落放进同一个序列，等玩法结束时再调用 Play。
        /// </summary>
        public void PlaySequence(IEnumerable<string> storyIds)
        {
            autoPlayQueue.Clear();
            autoPlayChaining = false;

            if (storyIds != null)
            {
                foreach (string storyId in storyIds)
                {
                    if (!string.IsNullOrWhiteSpace(storyId))
                    {
                        autoPlayQueue.Enqueue(storyId.Trim());
                    }
                }
            }

            PlayNextAuto();
        }

        private void PlayNextAuto()
        {
            if (autoPlayQueue.Count == 0)
            {
                autoPlayChaining = false;
                return;
            }

            if (IsPlaying)
            {
                // 广播 / 引导可以让位给自动剧情；叙事段则等它播完再继续
                if (current != null && !current.lockPlayer)
                {
                    FinishCurrent(false);

                    // 结束广播时如果有订阅者立刻开启了新剧情，就把队列留到之后
                    if (IsPlaying)
                    {
                        autoPlayChaining = true;
                        return;
                    }
                }
                else
                {
                    autoPlayChaining = true;
                    return;
                }
            }

            while (autoPlayQueue.Count > 0)
            {
                string storyId = autoPlayQueue.Dequeue();
                if (Play(storyId))
                {
                    autoPlayChaining = true;
                    return;
                }
            }

            autoPlayChaining = false;
        }

        private void ShowCurrentLine()
        {
            if (!IsPlaying || current == null)
            {
                return;
            }

            if (current.lines == null || lineIndex >= current.lines.Length)
            {
                FinishCurrent();
                return;
            }

            StoryLine line = current.lines[lineIndex];
            if (line == null)
            {
                Advance();
                return;
            }

            if (line.kind == StoryLineKind.Choice &&
                (line.choices == null || line.choices.Length == 0))
            {
                Debug.LogWarning($"[Story] {current.storyId} 有空的选项行，已自动跳过。", this);
                Advance();
                return;
            }

            Sprite portrait = null;
            if (portraitSet != null && !string.IsNullOrWhiteSpace(line.portraitKey))
            {
                portraitSet.TryGet(line.portraitKey, out portrait);
            }

            view.ShowLine(line, portrait);

            if (audioSet != null &&
                audioSource != null &&
                !string.IsNullOrWhiteSpace(line.audioCue) &&
                audioSet.TryGet(line.audioCue, out AudioClip clip) &&
                clip != null)
            {
                audioSource.PlayOneShot(clip, Mathf.Clamp01(audioVolume));
            }
        }

        private void Advance()
        {
            if (!IsPlaying || current == null)
            {
                return;
            }

            lineIndex++;
            if (lineIndex >= current.lines.Length)
            {
                FinishCurrent();
                return;
            }

            ShowCurrentLine();
        }

        private void FinishCurrent(bool continueAutoPlay = true)
        {
            if (!IsPlaying || current == null)
            {
                return;
            }

            string finishedId = current.storyId;
            bool shouldUnlock = current.lockPlayer;
            bool markPlayed = current.playOnce;
            bool completeObserveTask = current.completeObserveTaskOnFinish;

            view.Hide();

            if (shouldUnlock)
            {
                player?.SetControlEnabled(true);
                backpackPanel?.SetInteractionEnabled(true);
                taskPanel?.SetInteractionEnabled(true);
            }

            if (markPlayed && !string.IsNullOrEmpty(finishedId))
            {
                playedOnce.Add(finishedId);
            }

            IsPlaying = false;
            current = null;
            lineIndex = 0;

            StoryFinished?.Invoke(finishedId);

            if (completeObserveTask)
            {
                taskPanel?.CompleteObserveTask();
            }

            if (continueAutoPlay && !IsPlaying && autoPlayChaining)
            {
                autoPlayChaining = false;
                PlayNextAuto();
            }
        }

        private void BuildDefinitions()
        {
            definitions.Clear();

            if (library == null || library.stories == null)
            {
                return;
            }

            for (int index = 0; index < library.stories.Count; index++)
            {
                StoryDefinition definition = library.stories[index];
                if (definition == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.storyId))
                {
                    Debug.LogWarning($"[Story] 剧情库第 {index} 项没有 storyId，已跳过。", this);
                    continue;
                }

                string id = definition.storyId.Trim();
                if (definitions.ContainsKey(id))
                {
                    Debug.LogWarning($"[Story] 剧情库存在重复 storyId：{id}，保留第一个。", this);
                    continue;
                }

                definitions.Add(id, definition);
            }
        }

        private void Subscribe()
        {
            if (interactables != null)
            {
                interactables.StoryTriggered += OnStoryTriggered;
            }

            if (taskPanel != null)
            {
                taskPanel.TaskCompleted += OnTaskCompleted;
            }

            if (player != null)
            {
                player.MoveCompleted += OnMoveCompleted;
            }

            if (view != null)
            {
                view.ContinueRequested += OnContinueRequested;
                view.ChoiceSelected += OnChoiceSelected;
            }
        }

        private void Unsubscribe()
        {
            if (interactables != null)
            {
                interactables.StoryTriggered -= OnStoryTriggered;
            }

            if (taskPanel != null)
            {
                taskPanel.TaskCompleted -= OnTaskCompleted;
            }

            if (player != null)
            {
                player.MoveCompleted -= OnMoveCompleted;
            }

            if (view != null)
            {
                view.ContinueRequested -= OnContinueRequested;
                view.ChoiceSelected -= OnChoiceSelected;
            }
        }

        private void OnStoryTriggered(string storyId)
        {
            Play(storyId);
        }

        private void OnContinueRequested()
        {
            Advance();
        }

        private void OnChoiceSelected(int index)
        {
            // 本期所有选项汇合到同一段后续，索引只用于将来扩展分支
            Advance();
        }

        private void OnTaskCompleted(int taskIndex)
        {
            if (taskTriggers == null)
            {
                return;
            }

            for (int index = 0; index < taskTriggers.Length; index++)
            {
                StoryTaskTrigger trigger = taskTriggers[index];
                if (trigger == null || trigger.taskIndex != taskIndex ||
                    string.IsNullOrWhiteSpace(trigger.storyId))
                {
                    continue;
                }

                if (Play(trigger.storyId))
                {
                    return;
                }
            }
        }

        private void OnMoveCompleted(int column)
        {
            if (areaTriggers == null)
            {
                return;
            }

            for (int index = 0; index < areaTriggers.Length; index++)
            {
                StoryAreaTrigger trigger = areaTriggers[index];
                if (trigger == null || string.IsNullOrWhiteSpace(trigger.storyId))
                {
                    continue;
                }

                int minimum = Mathf.Min(trigger.startColumn, trigger.endColumn);
                int maximum = Mathf.Max(trigger.startColumn, trigger.endColumn);
                if (column < minimum || column > maximum)
                {
                    continue;
                }

                if (Play(trigger.storyId))
                {
                    return;
                }
            }
        }
    }
}
