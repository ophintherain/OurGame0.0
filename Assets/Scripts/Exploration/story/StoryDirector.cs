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
        [Tooltip("音源池的初始 AudioSource。留空会自动取同物体上的组件，没有则自动添加；后续不够用时脚本自动补建")]
        private AudioSource audioSource;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("音效音量 0~1。0 = 静音，1 = 原始音量")]
        private float audioVolume = 1f;

        [SerializeField]
        [Tooltip("音源池的第二个 AudioSource（环境音通常需要同时播放多段，会用到多个音源）。留空时全部按需自动创建")]
        private AudioSource ambientSource;

        [SerializeField, Min(0f)]
        [Tooltip("环境音的默认淡入淡出时长（秒）。音效条目里的 fadeIn / fadeOut 填 -1 时使用这个值；0 = 立即切换")]
        private float ambientFadeDuration = 0.5f;

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

        /// <summary>一组里的一层音频：音源 + 这一层的目标音量。</summary>
        private sealed class AudioLayer
        {
            public AudioSource source;
            public float volume;
        }

        /// <summary>一组同时播放的音频：一个音效 key 下的多个音频各占一个音源。</summary>
        private sealed class AudioGroup
        {
            public string key;
            public readonly List<AudioLayer> layers = new List<AudioLayer>();
            public float duration;
            public float fadeOut;
            public float scale = 1f;
            public bool stopping;
            public Coroutine routine;
        }

        private readonly List<AudioSource> layerPool = new List<AudioSource>();
        private readonly List<AudioSource> busyLayers = new List<AudioSource>();
        private readonly Dictionary<string, StoryDefinition> definitions =
            new Dictionary<string, StoryDefinition>();
        private readonly HashSet<string> playedOnce = new HashSet<string>();
        private readonly Queue<string> autoPlayQueue = new Queue<string>();
        private StoryDefinition current;
        private int lineIndex;
        private bool autoPlayChaining;
        private AudioGroup ambientGroup;
        private float autoAdvanceSeconds = -1f;
        private Coroutine autoAdvanceRoutine;
        private bool manualAdvanceEnabled = true;

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

            BuildLayerPool();

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
            return Play(storyId, false);
        }

        /// <summary>
        /// 按 ID 播放一个段落。ignorePlayOnce = true 时忽略 playOnce 限制，
        /// 供规则怪谈等需要循环播放的广播使用；其他行为与原 Play 一致。
        /// </summary>
        public bool Play(string storyId, bool ignorePlayOnce)
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

            return Play(definition, ignorePlayOnce);
        }

        /// <summary>播放一个段落资产。</summary>
        public bool Play(StoryDefinition definition)
        {
            return Play(definition, false);
        }

        private bool Play(StoryDefinition definition, bool ignorePlayOnce)
        {
            if (definition == null || definition.lines == null || definition.lines.Length == 0)
            {
                Debug.LogWarning("[Story] 要播放的剧情为空。", this);
                return false;
            }

            if (!ignorePlayOnce &&
                definition.playOnce &&
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
            autoAdvanceSeconds = -1f;
            StopAutoAdvance();

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
        /// 立即停止当前环境音。供规则怪谈等剧情外系统使用，避免依赖音效集里的「停止」key。
        /// </summary>
        public void StopAmbientCue()
        {
            StopAmbient();
        }

        /// <summary>
        /// 按 StoryAudioSet 的 key 播放一次音效，复用现有音源池与音量配置。
        /// 供规则怪谈的扣血音效等剧情外反馈使用。
        /// </summary>
        public bool PlayCue(string key)
        {
            if (audioSet == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (audioSet.IsStopAmbientCue(key))
            {
                StopAmbient();
                return true;
            }

            if (!audioSet.TryGetEntry(key, out StoryAudioEntry entry))
            {
                Debug.LogWarning($"[Story] 音效集里找不到「{key}」。", this);
                return false;
            }

            if (!HasPlayableClip(entry))
            {
                Debug.LogWarning($"[Story] 音效「{key}」还没有配置音频层。", this);
                return false;
            }

            if (entry.isAmbient)
            {
                PlayAmbient(key, entry);
            }
            else
            {
                PlayOneShotGroup(key, entry);
            }

            return true;
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

            view.ShowLine(line, ResolvePortraits(line.portraitKey));

            Debug.Log(
                $"[Story][AudioDiag] line={lineIndex} audioSet={(audioSet != null)} " +
                $"cue=\"{line.audioCue}\" audioVolume={audioVolume}",
                this);

            if (audioSet != null && !string.IsNullOrWhiteSpace(line.audioCue))
            {
                if (audioSet.IsStopAmbientCue(line.audioCue))
                {
                    StopAmbient();
                }
                else if (audioSet.TryGetEntry(line.audioCue, out StoryAudioEntry entry))
                {
                    if (!HasPlayableClip(entry))
                    {
                        Debug.LogWarning($"[Story] 音效「{line.audioCue}」还没有配置音频层，已跳过。", this);
                    }
                    else if (entry.isAmbient)
                    {
                        PlayAmbient(line.audioCue, entry);
                    }
                    else
                    {
                        PlayOneShotGroup(line.audioCue, entry);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Story] 音效集里找不到「{line.audioCue}」，请检查 key 是否完全一致。", this);
                }
            }

            RestartAutoAdvance(line);
        }

        /// <summary>
        /// 开启 / 关闭按行自动翻页。seconds &gt; 0 时，广播类行会停留该秒数后自动进入下一行；
        /// 选项行不自动翻页。传 0 或负数表示恢复为点击翻页。
        /// </summary>
        public void SetAutoAdvance(float seconds)
        {
            autoAdvanceSeconds = seconds;
            if (IsPlaying && current != null && current.lines != null &&
                lineIndex >= 0 && lineIndex < current.lines.Length)
            {
                RestartAutoAdvance(current.lines[lineIndex]);
            }
        }

        /// <summary>
        /// 开启 / 关闭玩家手动翻页（点击 / 空格 / 回车）。
        /// 规则怪谈的两段式广播会关掉手动翻页，由规则系统在第一段完成后调用 RequestContinue。
        /// </summary>
        public void SetManualAdvanceEnabled(bool enabled)
        {
            manualAdvanceEnabled = enabled;
        }

        /// <summary>由外部系统（如规则怪谈）翻到下一行。</summary>
        public void RequestContinue()
        {
            if (IsPlaying)
            {
                Advance();
            }
        }

        private void RestartAutoAdvance(StoryLine line)
        {
            StopAutoAdvance();

            if (autoAdvanceSeconds <= 0f || !IsPlaying || current == null || line == null)
            {
                return;
            }

            if (line.kind == StoryLineKind.Choice)
            {
                return;
            }

            int capturedLine = lineIndex;
            autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfter(autoAdvanceSeconds, capturedLine));
        }

        private void StopAutoAdvance()
        {
            if (autoAdvanceRoutine != null)
            {
                StopCoroutine(autoAdvanceRoutine);
                autoAdvanceRoutine = null;
            }
        }

        private IEnumerator AutoAdvanceAfter(float seconds, int capturedLine)
        {
            yield return new WaitForSecondsRealtime(seconds);
            autoAdvanceRoutine = null;

            if (!IsPlaying || current == null || lineIndex != capturedLine)
            {
                yield break;
            }

            Advance();
        }

        private static readonly char[] PortraitSeparators = { '|', '，', ',', '、', ';', '；' };

        /// <summary>
        /// 立绘列支持同时配多个角色，用 | ， 、 ; 分隔；逐个查立绘集，未配置的跳过。
        /// </summary>
        private Sprite[] ResolvePortraits(string portraitKey)
        {
            if (portraitSet == null || string.IsNullOrWhiteSpace(portraitKey))
            {
                return Array.Empty<Sprite>();
            }

            string[] keys = portraitKey.Split(PortraitSeparators, StringSplitOptions.RemoveEmptyEntries);
            List<Sprite> portraits = new List<Sprite>();

            for (int index = 0; index < keys.Length; index++)
            {
                string key = keys[index].Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                if (portraitSet.TryGet(key, out Sprite sprite))
                {
                    portraits.Add(sprite);
                }
            }

            return portraits.ToArray();
        }

        private static bool HasPlayableClip(StoryAudioEntry entry)
        {
            if (entry.clips == null)
            {
                return false;
            }

            for (int index = 0; index < entry.clips.Length; index++)
            {
                StoryAudioClip clipEntry = entry.clips[index];
                if (clipEntry != null && clipEntry.clip != null)
                {
                    return true;
                }
            }

            return false;
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
            StopAmbient();
            StopAutoAdvance();
            autoAdvanceSeconds = -1f;
            manualAdvanceEnabled = true;

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

        /// <summary>音源池初始化：audioSource / ambientSource 作为初始音源，不够时运行时补建。</summary>
        private void BuildLayerPool()
        {
            layerPool.Clear();
            busyLayers.Clear();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            AddLayer(audioSource);

            if (ambientSource != null && ambientSource != audioSource)
            {
                AddLayer(ambientSource);
            }
        }

        private void AddLayer(AudioSource source)
        {
            ConfigureLayer(source);

            if (!layerPool.Contains(source))
            {
                layerPool.Add(source);
            }
        }

        private static void ConfigureLayer(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        /// <summary>从池里取一个空闲音源，没有就新建。</summary>
        private AudioSource TakeLayer()
        {
            for (int index = 0; index < layerPool.Count; index++)
            {
                AudioSource candidate = layerPool[index];
                if (candidate != null && !busyLayers.Contains(candidate))
                {
                    busyLayers.Add(candidate);
                    return candidate;
                }
            }

            AudioSource created = gameObject.AddComponent<AudioSource>();
            ConfigureLayer(created);
            layerPool.Add(created);
            busyLayers.Add(created);
            return created;
        }

        private void ReleaseGroup(AudioGroup group)
        {
            for (int index = 0; index < group.layers.Count; index++)
            {
                AudioSource source = group.layers[index].source;
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                source.volume = 0f;
                busyLayers.Remove(source);
            }

            group.layers.Clear();
        }

        /// <summary>按统一比例缩放整组音量：每层实际音量 = 该层目标音量 × scale。</summary>
        private static void SetGroupScale(AudioGroup group, float scale)
        {
            group.scale = scale;

            for (int index = 0; index < group.layers.Count; index++)
            {
                AudioLayer layer = group.layers[index];
                if (layer.source != null)
                {
                    layer.source.volume = layer.volume * scale;
                }
            }
        }

        /// <summary>
        /// 把一个音效 key 下的所有音频各放一个音源同时播放。
        /// 整组时长 = 组内最短音频长度；没有任何音频时返回 null。
        /// </summary>
        private AudioGroup CreateGroup(string key, StoryAudioEntry entry, float volumeScale)
        {
            if (entry.clips == null || entry.clips.Length == 0)
            {
                return null;
            }

            AudioGroup group = new AudioGroup { key = key, scale = 0f };
            float shortest = float.MaxValue;
            float entryVolume = Mathf.Clamp01(volumeScale) * Mathf.Clamp01(entry.volume);

            for (int index = 0; index < entry.clips.Length; index++)
            {
                StoryAudioClip clipEntry = entry.clips[index];
                if (clipEntry == null || clipEntry.clip == null)
                {
                    continue;
                }

                AudioClip clip = clipEntry.clip;
                AudioSource source = TakeLayer();
                source.clip = clip;
                source.loop = false;
                source.volume = 0f;
                source.Play();

                float layerVolume = Mathf.Clamp01(entryVolume * Mathf.Clamp01(clipEntry.volume));
                group.layers.Add(new AudioLayer
                {
                    source = source,
                    volume = layerVolume
                });

                Debug.Log(
                    $"[Story][AudioDiag] {key} layer={index} clip={clip.name} " +
                    $"length={clip.length:0.##} volume={layerVolume:0.##}",
                    this);

                if (clip.length < shortest)
                {
                    shortest = clip.length;
                }
            }

            if (group.layers.Count == 0)
            {
                return null;
            }

            group.duration = Mathf.Max(0.01f, shortest);
            return group;
        }

        /// <summary>播放环境音：整组叠加，按最短时长循环，切换时淡入淡出。</summary>
        private void PlayAmbient(string key, StoryAudioEntry entry)
        {
            string normalizedKey = key.Trim();

            if (ambientGroup != null && ambientGroup.key == normalizedKey && !ambientGroup.stopping)
            {
                // 同一段环境音连续出现：不重播
                return;
            }

            if (ambientGroup != null)
            {
                FadeOutGroup(ambientGroup, ambientGroup.fadeOut);
            }

            AudioGroup group = CreateGroup(normalizedKey, entry, audioVolume);
            if (group == null)
            {
                ambientGroup = null;
                return;
            }

            group.fadeOut = ResolveFade(entry.fadeOut);
            ambientGroup = group;
            group.routine = StartCoroutine(AmbientRoutine(group, ResolveFade(entry.fadeIn)));
        }

        /// <summary>播放一次性音效：整组叠加，到最短音频结束时整组停止。</summary>
        private void PlayOneShotGroup(string key, StoryAudioEntry entry)
        {
            AudioGroup group = CreateGroup(key.Trim(), entry, audioVolume);
            if (group == null)
            {
                return;
            }

            SetGroupScale(group, 1f);
            group.routine = StartCoroutine(ReleaseAfterRoutine(group));
        }

        private IEnumerator ReleaseAfterRoutine(AudioGroup group)
        {
            yield return new WaitForSecondsRealtime(group.duration);
            ReleaseGroup(group);
        }

        private IEnumerator AmbientRoutine(AudioGroup group, float fadeIn)
        {
            if (fadeIn > 0f)
            {
                for (float elapsed = 0f; elapsed < fadeIn; elapsed += Time.unscaledDeltaTime)
                {
                    if (group.stopping)
                    {
                        yield break;
                    }

                    SetGroupScale(group, Mathf.Lerp(0f, 1f, elapsed / fadeIn));
                    yield return null;
                }
            }

            if (group.stopping)
            {
                yield break;
            }

            SetGroupScale(group, 1f);

            // 每层各播各的会慢慢失步，所以按最短音频的长度整体重播，让每一轮都从同一起点开始
            while (!group.stopping)
            {
                yield return new WaitForSecondsRealtime(group.duration);

                if (group.stopping)
                {
                    yield break;
                }

                for (int index = 0; index < group.layers.Count; index++)
                {
                    AudioSource source = group.layers[index].source;
                    if (source != null)
                    {
                        source.Play();
                    }
                }
            }
        }

        /// <summary>段落结束时淡出环境音。</summary>
        private void StopAmbient()
        {
            if (ambientGroup == null || ambientGroup.stopping)
            {
                return;
            }

            FadeOutGroup(ambientGroup, ambientGroup.fadeOut);
        }

        private void FadeOutGroup(AudioGroup group, float duration)
        {
            if (group == null || group.stopping)
            {
                return;
            }

            group.stopping = true;

            if (group.routine != null)
            {
                StopCoroutine(group.routine);
            }

            group.routine = StartCoroutine(FadeOutGroupRoutine(group, duration));
        }

        private IEnumerator FadeOutGroupRoutine(AudioGroup group, float duration)
        {
            float startScale = group.scale;

            if (duration > 0f)
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    SetGroupScale(group, Mathf.Lerp(startScale, 0f, elapsed / duration));
                    yield return null;
                }
            }

            ReleaseGroup(group);

            if (ambientGroup == group)
            {
                ambientGroup = null;
            }
        }

        private float ResolveFade(float configured)
        {
            return Mathf.Max(0f, configured >= 0f ? configured : ambientFadeDuration);
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
            if (!manualAdvanceEnabled)
            {
                return;
            }

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
