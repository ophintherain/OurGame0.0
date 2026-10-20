using UnityEngine;

namespace Spotlight.Exploration
{
    /// <summary>
    /// 第三车厢的电视机：点击循环切换背景音乐音量档位，并把档位写给规则条件。
    /// 0 = 关，1 = 低音量，2 = 高音量。
    /// </summary>
    public sealed class ExplorationTelevision : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("电视机上的 RuleClickTarget，点击它切换档位。留空会自动取同一个物体上的组件")]
        private RuleClickTarget clickTarget;

        [SerializeField]
        [Tooltip("场景里的背景音乐播放器，电视档位会直接改它的音量。留空会自动查找")]
        private ExplorationBgmPlayer bgm;

        [SerializeField]
        [Tooltip("规则状态目标，档位会写进它的 value。留空会自动取同一个物体上的组件")]
        private RuleStateTarget stateTarget;

        [SerializeField]
        [Tooltip("注册给规则条件的 key，默认 TV。必须与规则资产里的 targetKey 一致")]
        private string stateKey = "TV";

        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 档（电视关）对应的背景音乐音量，默认 0.2，与低档相同。电视关着时音乐仍然保持低音量")]
        private float offVolume = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("1 档（低音量）对应的背景音乐音量，默认 0.2")]
        private float lowVolume = 0.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("2 档（高音量）对应的背景音乐音量，默认 0.45")]
        private float highVolume = 0.45f;

        [SerializeField]
        [Tooltip("进入场景时的初始档位。0 = 电视关着、背景音乐静音；1 = 低音量；2 = 高音量")]
        private int startLevel;

        private int level;

        /// <summary>当前档位：0 = 关，1 = 低，2 = 高。</summary>
        public int Level => level;

        private void Awake()
        {
            if (clickTarget == null)
            {
                clickTarget = GetComponent<RuleClickTarget>();
            }

            if (bgm == null)
            {
                bgm = FindObjectOfType<ExplorationBgmPlayer>();
            }

            if (stateTarget == null)
            {
                stateTarget = GetComponent<RuleStateTarget>();
            }

            if (stateTarget != null && !string.IsNullOrWhiteSpace(stateKey))
            {
                // 保证规则条件能用约定好的 key 找到它
                stateTarget.SetKey(stateKey);
            }
        }

        private void OnEnable()
        {
            if (clickTarget != null)
            {
                clickTarget.Clicked += OnClicked;
            }
        }

        private void OnDisable()
        {
            if (clickTarget != null)
            {
                clickTarget.Clicked -= OnClicked;
            }
        }

        private void Start()
        {
            level = Mathf.Clamp(startLevel, 0, 2);
            ApplyLevel();
        }

        private void OnClicked(string key, Vector3 worldPosition)
        {
            level = (level + 1) % 3;
            ApplyLevel();
        }

        private void ApplyLevel()
        {
            float volume = level switch
            {
                0 => offVolume,
                1 => lowVolume,
                _ => highVolume
            };

            bgm?.SetVolume(volume);
            stateTarget?.SetValue(level);
        }
    }
}
