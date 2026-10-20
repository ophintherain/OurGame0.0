using System;
using System.Collections.Generic;
using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 规则怪谈调度：随机抽一组未完成的规则，按「两段式」推进。
    /// 第一段满足后才播第二句并进入第二段；两段都完成后才记为完成。
    /// 违规时按秒扣血，6 组全部完成后重新开始新一轮随机不重复。
    /// </summary>
    public sealed class RuleHorrorDirector : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("必填。场景里 StorySystem 上的 StoryDirector，用于播广播和扣血音效")]
        private StoryDirector storyDirector;

        [SerializeField]
        [Tooltip("必填。Player 上的 ExplorationHealth，用于扣血")]
        private ExplorationHealth health;

        [SerializeField]
        [Tooltip("必填。Player 上的 ExplorationPlayerController，用于读取位置、姿势和移动状态")]
        private ExplorationPlayerController player;

        [SerializeField]
        [Tooltip("必填。场景里的 ExplorationInventory，用于判断背包是否拥有规则要求的物品")]
        private ExplorationInventory inventory;

        [SerializeField]
        [Tooltip("必填。本场景的规则集资产（第一车厢 6 组规则）")]
        private RuleHorrorSet ruleSet;

        [SerializeField]
        [Tooltip("熄灯黑遮罩。1-1 第一段会调用它熄灯，第二段开灯；留空则不做灯光表现")]
        private RuleDarkMask darkMask;

        [SerializeField]
        [Tooltip("可选的 Boss 占位物。1-6 第二段开始时激活，规则完成时关闭；留空时只用右侧火区做判定")]
        private GameObject bossObject;

        [SerializeField, Min(0f)]
        [Tooltip("进入场景后等待多少秒触发第一条规则。0 = 下一帧就开始")]
        private float initialDelaySeconds = 15f;

        [SerializeField, Min(1f)]
        [Tooltip("一组规则完成后，间隔多少秒再抽下一组。单位秒，数值越小规则越密集")]
        private float intervalSeconds = 20f;

        [SerializeField, Min(0.1f)]
        [Tooltip("违规时每隔多少秒结算一次伤害。每次扣血量由每段的 damagePerTick 决定")]
        private float damageTickSeconds = 1f;

        [SerializeField]
        [Tooltip("扣血时播放的音效 key，对应 StoryAudioSet 里的条目，默认「受击扣血声」。留空则不播")]
        private string damageCueKey = "受击扣血声";

        [SerializeField]
        [Tooltip("是否自动开始调度。取消勾选会完全停掉规则系统（调试用）")]
        private bool autoStart = true;

        private enum Phase
        {
            Waiting,
            WaitingForItem,
            Grace,
            Active
        }

        private Phase phase;
        private RuleHorrorRule currentRule;
        private RuleHorrorRule pendingRule;
        private RuleStage currentStage;
        private int currentStageIndex;
        private float phaseTimer;
        private float holdTimer;
        private float damageTimer;
        private bool waitingItemLogged;
        private Vector3 lastClickPosition;
        private bool hasLastClickPosition;
        private readonly HashSet<string> completedIds = new HashSet<string>();
        private readonly List<RuleClickTarget> clickTargets = new List<RuleClickTarget>();

        /// <summary>一组规则完成（参数为 storyId）。后续接 Boss 出现等演出时可订阅。</summary>
        public event Action<string> RuleCompleted;

        /// <summary>当前是否有规则正在宽限或判定。</summary>
        public bool IsRuleRunning => phase == Phase.Grace || phase == Phase.Active;

        /// <summary>两条规则之间的间隔（秒）。运行时修改从下一次等待开始生效。</summary>
        public float IntervalSeconds
        {
            get => intervalSeconds;
            set => intervalSeconds = Mathf.Max(1f, value);
        }

        /// <summary>伤害结算间隔（秒）。每次实际扣血 = 当前段的每秒伤害 × 该值。</summary>
        public float DamageTickSeconds
        {
            get => damageTickSeconds;
            set => damageTickSeconds = Mathf.Max(0.1f, value);
        }

        /// <summary>规则集资产，可直接读写每组规则及其两段配置。</summary>
        public RuleHorrorSet RuleSet => ruleSet;

        /// <summary>按 storyId 找一组规则；找不到返回 null。</summary>
        public RuleHorrorRule GetRule(string storyId)
        {
            if (ruleSet == null || ruleSet.rules == null || string.IsNullOrWhiteSpace(storyId))
            {
                return null;
            }

            string targetId = storyId.Trim();
            for (int index = 0; index < ruleSet.rules.Count; index++)
            {
                RuleHorrorRule rule = ruleSet.rules[index];
                if (rule != null && string.Equals(rule.storyId, targetId, StringComparison.Ordinal))
                {
                    return rule;
                }
            }

            return null;
        }

        /// <summary>设置某组规则两段「每次结算扣多少血」（两段都改成同一个值）。</summary>
        public bool SetDamagePerTick(string storyId, float amountPerTick)
        {
            return SetDamagePerTick(storyId, -1, amountPerTick);
        }

        /// <summary>设置某组规则指定段「每次结算扣多少血」；stageIndex = -1 表示两段都改。</summary>
        public bool SetDamagePerTick(string storyId, int stageIndex, float amountPerTick)
        {
            RuleHorrorRule rule = GetRule(storyId);
            if (rule == null || rule.stages == null)
            {
                return false;
            }

            float damage = Mathf.Max(0f, amountPerTick);
            for (int index = 0; index < rule.stages.Length; index++)
            {
                if (rule.stages[index] == null)
                {
                    continue;
                }

                if (stageIndex < 0 || index == stageIndex)
                {
                    rule.stages[index].damagePerTick = damage;
                }
            }

            return true;
        }

        /// <summary>读取某组规则指定段每次结算扣多少血；找不到返回 0。</summary>
        public float GetDamagePerTick(string storyId, int stageIndex)
        {
            RuleHorrorRule rule = GetRule(storyId);
            if (rule == null || rule.stages == null ||
                stageIndex < 0 || stageIndex >= rule.stages.Length ||
                rule.stages[stageIndex] == null)
            {
                return 0f;
            }

            return rule.stages[stageIndex].damagePerTick;
        }

        private void Awake()
        {
            if (storyDirector == null)
            {
                storyDirector = FindObjectOfType<StoryDirector>();
            }

            if (health == null)
            {
                health = FindObjectOfType<ExplorationHealth>();
            }

            if (player == null)
            {
                player = FindObjectOfType<ExplorationPlayerController>();
            }

            if (inventory == null)
            {
                inventory = FindObjectOfType<ExplorationInventory>();
            }

            if (storyDirector == null || health == null || player == null || ruleSet == null)
            {
                Debug.LogError(
                    "[Rule] RuleHorrorDirector 缺少 storyDirector / health / player / ruleSet 引用，规则系统已禁用。",
                    this);
                enabled = false;
                return;
            }

            if (inventory == null)
            {
                Debug.LogWarning("[Rule] 场景里没有 ExplorationInventory，1-3 的玩偶判定无法生效。", this);
            }

            CollectClickTargets();

            if (bossObject != null)
            {
                bossObject.SetActive(false);
            }

            phase = Phase.Waiting;
            phaseTimer = initialDelaySeconds;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < clickTargets.Count; index++)
            {
                if (clickTargets[index] != null)
                {
                    clickTargets[index].Clicked -= OnTargetClicked;
                }
            }

            clickTargets.Clear();
        }

        private void Update()
        {
            if (!autoStart || health.IsDead)
            {
                return;
            }

            // 剧情锁输入（叙事 / 背包 / 任务）时暂停；规则广播自身不锁输入，所以不会被这里暂停
            if (!player.ControlEnabled)
            {
                return;
            }

            // 等待期间如果别的剧情在播，先不要抢
            if ((phase == Phase.Waiting || phase == Phase.WaitingForItem) && storyDirector.IsPlaying)
            {
                return;
            }

            switch (phase)
            {
                case Phase.Waiting:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        TryStartNextRule();
                    }
                    break;

                case Phase.WaitingForItem:
                    if (pendingRule == null)
                    {
                        phase = Phase.Waiting;
                        phaseTimer = intervalSeconds;
                        break;
                    }

                    if (CanStartRule(pendingRule))
                    {
                        RuleHorrorRule rule = pendingRule;
                        pendingRule = null;
                        StartRule(rule);
                    }
                    break;

                case Phase.Grace:
                    phaseTimer -= Time.deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        phase = Phase.Active;
                        holdTimer = 0f;
                        damageTimer = 0f;
                    }
                    break;

                case Phase.Active:
                    UpdateActiveStage(Time.deltaTime);
                    break;
            }
        }

        private void TryStartNextRule()
        {
            List<RuleHorrorRule> allRules = CollectValidRules();
            if (allRules.Count == 0)
            {
                Debug.LogWarning("[Rule] 规则集里没有可用的规则，调度已暂停。", this);
                phaseTimer = intervalSeconds;
                return;
            }

            if (completedIds.Count >= allRules.Count)
            {
                completedIds.Clear();
            }

            RuleHorrorRule blockedRule = null;
            List<RuleHorrorRule> candidates = new List<RuleHorrorRule>();

            for (int index = 0; index < allRules.Count; index++)
            {
                RuleHorrorRule rule = allRules[index];
                if (completedIds.Contains(rule.storyId))
                {
                    continue;
                }

                if (!CanStartRule(rule))
                {
                    if (blockedRule == null)
                    {
                        blockedRule = rule;
                    }

                    continue;
                }

                candidates.Add(rule);
            }

            if (candidates.Count == 0)
            {
                // 可能刚好全部完成，先清空再试一轮
                if (completedIds.Count >= allRules.Count)
                {
                    completedIds.Clear();
                    for (int index = 0; index < allRules.Count; index++)
                    {
                        if (CanStartRule(allRules[index]))
                        {
                            candidates.Add(allRules[index]);
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    if (blockedRule != null)
                    {
                        pendingRule = blockedRule;
                        phase = Phase.WaitingForItem;

                        if (!waitingItemLogged)
                        {
                            waitingItemLogged = true;
                            Debug.Log(
                                $"[Rule] 规则 {blockedRule.storyId} 需要物品 {blockedRule.requiredItemId}，等待玩家拾取。",
                                this);
                        }

                        return;
                    }

                    phaseTimer = intervalSeconds;
                    return;
                }
            }

            waitingItemLogged = false;
            RuleHorrorRule selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            StartRule(selected);
        }

        private bool CanStartRule(RuleHorrorRule rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.storyId))
            {
                return false;
            }

            if (rule.stages == null || rule.stages.Length == 0)
            {
                return false;
            }

            if (rule.requiredItemId > 0)
            {
                return inventory != null && inventory.Contains(rule.requiredItemId);
            }

            return true;
        }

        private List<RuleHorrorRule> CollectValidRules()
        {
            List<RuleHorrorRule> result = new List<RuleHorrorRule>();
            if (ruleSet == null || ruleSet.rules == null)
            {
                return result;
            }

            for (int index = 0; index < ruleSet.rules.Count; index++)
            {
                RuleHorrorRule rule = ruleSet.rules[index];
                if (rule != null && !string.IsNullOrWhiteSpace(rule.storyId) &&
                    rule.stages != null && rule.stages.Length > 0)
                {
                    result.Add(rule);
                }
            }

            return result;
        }

        private void StartRule(RuleHorrorRule rule)
        {
            currentRule = rule;
            currentStage = null;
            currentStageIndex = 0;
            holdTimer = 0f;
            damageTimer = 0f;
            hasLastClickPosition = false;
            lastClickPosition = Vector3.zero;

            storyDirector.SetManualAdvanceEnabled(false);

            if (!storyDirector.Play(rule.storyId, true))
            {
                Debug.LogWarning(
                    $"[Rule] 广播 {rule.storyId} 播放失败（剧情库找不到或正在播叙事段），稍后重试。",
                    this);
                storyDirector.SetManualAdvanceEnabled(true);
                currentRule = null;
                phase = Phase.Waiting;
                phaseTimer = 2f;
                return;
            }

            Debug.Log($"[Rule] 触发规则 {rule.storyId}（{rule.title}）", this);
            StartStage(0);
        }

        private void StartStage(int stageIndex)
        {
            if (currentRule == null || currentRule.stages == null ||
                stageIndex < 0 || stageIndex >= currentRule.stages.Length ||
                currentRule.stages[stageIndex] == null)
            {
                FinishRule();
                return;
            }

            currentStageIndex = stageIndex;
            currentStage = currentRule.stages[stageIndex];

            ExecuteAction(currentStage.action);

            if (stageIndex > 0)
            {
                if (storyDirector.IsPlaying)
                {
                    storyDirector.RequestContinue();
                }
                else
                {
                    Debug.LogWarning("[Rule] 第二段开始时剧情已不在播放，无法翻到第二句。", this);
                }
            }

            holdTimer = 0f;
            damageTimer = 0f;
            phaseTimer = Mathf.Max(0f, currentStage.graceSeconds);
            phase = phaseTimer > 0f ? Phase.Grace : Phase.Active;
        }

        private void ExecuteAction(RuleStageAction action)
        {
            switch (action)
            {
                case RuleStageAction.LightsOff:
                    darkMask?.SetDark(true);
                    break;

                case RuleStageAction.LightsOn:
                    darkMask?.SetDark(false);
                    break;

                case RuleStageAction.SpawnBoss:
                    if (bossObject != null)
                    {
                        // 点哪只眼睛就在哪只眼睛的 X 处出现，Y / Z 保持 Boss 物体当前配置
                        if (hasLastClickPosition)
                        {
                            Vector3 bossPosition = bossObject.transform.position;
                            bossPosition.x = lastClickPosition.x;
                            bossObject.transform.position = bossPosition;
                        }

                        bossObject.SetActive(true);
                    }

                    break;
            }
        }

        private void UpdateActiveStage(float deltaTime)
        {
            if (currentStage == null)
            {
                return;
            }

            if (currentStage.HasCompletionClick)
            {
                // 点击完成的段：在点到目标之前持续扣血
                TickDamage(deltaTime);
                return;
            }

            bool satisfied = currentStage.IsSatisfied(
                player.transform.position, player.Posture, player.IsMoving,
                IsMovementKeyHeld(), inventory,
                bossObject != null ? bossObject.transform.position : Vector3.zero,
                bossObject != null);

            if (satisfied)
            {
                holdTimer += deltaTime;
                damageTimer = 0f;

                if (holdTimer >= currentStage.requiredHoldSeconds)
                {
                    CompleteStage();
                }

                return;
            }

            holdTimer = 0f;
            TickDamage(deltaTime);
        }

        private void TickDamage(float deltaTime)
        {
            if (currentStage == null || currentStage.damagePerTick <= 0f)
            {
                return;
            }

            damageTimer += deltaTime;

            while (damageTimer >= damageTickSeconds)
            {
                damageTimer -= damageTickSeconds;

                health.ApplyDamage(currentStage.damagePerTick);
                PlayDamageCue();

                if (health.IsDead)
                {
                    return;
                }
            }
        }

        private void CompleteStage()
        {
            if (currentRule == null || currentRule.stages == null)
            {
                FinishRule();
                return;
            }

            int nextStage = currentStageIndex + 1;
            if (nextStage < currentRule.stages.Length)
            {
                StartStage(nextStage);
                return;
            }

            FinishRule();
        }

        private void FinishRule()
        {
            string finishedId = currentRule != null ? currentRule.storyId : null;

            if (storyDirector.IsPlaying)
            {
                storyDirector.HideCurrent();
            }

            storyDirector.SetManualAdvanceEnabled(true);
            darkMask?.SetDark(false);

            if (bossObject != null)
            {
                bossObject.SetActive(false);
            }

            currentRule = null;
            currentStage = null;
            currentStageIndex = 0;
            holdTimer = 0f;
            damageTimer = 0f;

            if (!string.IsNullOrEmpty(finishedId))
            {
                completedIds.Add(finishedId);
                Debug.Log($"[Rule] 完成规则 {finishedId}", this);
                RuleCompleted?.Invoke(finishedId);
            }

            phase = Phase.Waiting;
            phaseTimer = intervalSeconds;
        }

        private void AbortRule()
        {
            if (storyDirector.IsPlaying)
            {
                storyDirector.HideCurrent();
            }

            storyDirector.SetManualAdvanceEnabled(true);
            darkMask?.SetDark(false);

            if (bossObject != null)
            {
                bossObject.SetActive(false);
            }

            currentRule = null;
            currentStage = null;
            currentStageIndex = 0;
            pendingRule = null;
            holdTimer = 0f;
            damageTimer = 0f;
        }

        private void CollectClickTargets()
        {
            clickTargets.Clear();
            RuleClickTarget[] targets = FindObjectsOfType<RuleClickTarget>(true);

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null)
                {
                    continue;
                }

                targets[index].Clicked += OnTargetClicked;
                clickTargets.Add(targets[index]);
            }
        }

        private void OnTargetClicked(string key, Vector3 worldPosition)
        {
            if (phase != Phase.Active || currentStage == null || health.IsDead)
            {
                return;
            }

            if (currentStage.HasCompletionClick &&
                string.Equals(key, currentStage.completionClickKey, StringComparison.Ordinal))
            {
                lastClickPosition = worldPosition;
                hasLastClickPosition = true;
                CompleteStage();
                return;
            }

            if (!string.IsNullOrWhiteSpace(currentStage.forbiddenClickKey) &&
                string.Equals(key, currentStage.forbiddenClickKey, StringComparison.Ordinal))
            {
                holdTimer = 0f;
                health.ApplyDamage(currentStage.forbiddenClickDamage);
                PlayDamageCue();
            }
        }

        private void PlayDamageCue()
        {
            if (string.IsNullOrWhiteSpace(damageCueKey) || storyDirector == null)
            {
                return;
            }

            storyDirector.PlayCue(damageCueKey);
        }

        private static bool IsMovementKeyHeld()
        {
            return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                   Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
                   Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
                   Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
        }

        [ContextMenu("调试：立即触发下一条规则")]
        private void DebugTriggerNextRule()
        {
            AbortRule();
            phase = Phase.Waiting;
            phaseTimer = 0f;
        }

        [ContextMenu("调试：完成当前段")]
        private void DebugCompleteStage()
        {
            if (phase == Phase.Grace || phase == Phase.Active)
            {
                CompleteStage();
            }
        }

        [ContextMenu("调试：清空完成记录")]
        private void DebugClearCompleted()
        {
            completedIds.Clear();
        }
    }
}
