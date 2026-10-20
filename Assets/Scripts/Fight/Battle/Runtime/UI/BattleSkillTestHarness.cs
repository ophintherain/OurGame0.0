using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spotlight.Battle
{
    // Only the dedicated fight_test scene receives these shortcuts.
    public sealed class BattleSkillTestHarness : MonoBehaviour
    {
        private static readonly KeyCode[] PlayerKeys =
        {
            KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B,
            KeyCode.N, KeyCode.M, KeyCode.J, KeyCode.K
        };
        private static readonly WeaponSkill[] BorrowedSkills =
        {
            WeaponSkill.Truce, WeaponSkill.Needle, WeaponSkill.Barrage,
            WeaponSkill.Leap, WeaponSkill.Mimic, WeaponSkill.Servitude,
            WeaponSkill.Poet, WeaponSkill.Edge, WeaponSkill.Recorder
        };

        private BattleCoordinator battle;
        private WeaponController weapons;
        private BossController boss;
        private string lastAction = "";
        private int borrowedIndex = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "fight_test") return;
            new GameObject("BattleSkillTestHarness").AddComponent<BattleSkillTestHarness>();
        }

        private void Start()
        {
            battle = FindObjectOfType<BattleCoordinator>();
            weapons = FindObjectOfType<WeaponController>();
            boss = FindObjectOfType<BossController>();
            boss?.SetManualSkillTesting(true);
        }

        private void Update()
        {
            if (battle == null || weapons == null || boss == null) return;
            if (battle.IsLoadoutSelectionOpen && !battle.HasPendingWeaponDiscard)
            {
                weapons.PrepareSelection();
                weapons.TrySelect(0);
                battle.TryConfirmLoadoutAndStart();
            }
            if (!battle.IsBattleActive) return;

            for (int index = 0; index < PlayerKeys.Length; index++)
            {
                if (!Input.GetKeyDown(PlayerKeys[index])) continue;
                WeaponDefinition definition = index < weapons.AvailableWeapons.Count
                    ? weapons.AvailableWeapons[index] : null;
                bool used = weapons.TryUseForTesting(index);
                lastAction = (definition != null ? definition.DisplayName : "武器") +
                    (used ? "：已施放" : "：未施放（检查站姿、距离或 Boss 隐身）");
            }

            if (Input.GetKeyDown(KeyCode.F1)) { boss.CastTripleStrikeForTesting(); lastAction = "Boss：三连击"; }
            if (Input.GetKeyDown(KeyCode.F2)) { boss.SummonCrow(); lastAction = "Boss：召唤乌鸦"; }
            if (Input.GetKeyDown(KeyCode.F3)) { boss.ApplyInvisibility(8f); lastAction = "Boss：隐身 8 秒"; }
            if (Input.GetKeyDown(KeyCode.F4))
                lastAction = boss.TryCastBorrowedSkill() ? "Boss：随机武器技能" : "Boss：当前随机技能不能施放";
            if (Input.GetKeyDown(KeyCode.F5)) { boss.ConfigureCarriage(1); lastAction = "Boss：第一车厢规则"; }
            if (Input.GetKeyDown(KeyCode.F6)) { boss.ConfigureCarriage(2); lastAction = "Boss：第二车厢规则"; }
            if (Input.GetKeyDown(KeyCode.F7)) { boss.ConfigureCarriage(3); lastAction = "Boss：第三车厢规则（攻击附带流血）"; }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                borrowedIndex = (borrowedIndex + 1) % BorrowedSkills.Length;
                boss.SetBorrowedSkillForTesting(BorrowedSkills[borrowedIndex]);
                lastAction = "Boss 随机武器技能切换为：" + BorrowedSkills[borrowedIndex];
            }
        }

        private void OnGUI()
        {
            if (Spotlight.Shared.SpotlightSceneTransition.IsTransitioning) return;
            if (battle == null) return;
            int oldDepth = GUI.depth;
            GUI.depth = -30;
            GUILayout.BeginArea(new Rect((Screen.width - 430f) * 0.5f,
                Screen.height - 195f, 430f, 178f), GUI.skin.box);
            GUILayout.Label("技能测试场景 fight_test");
            GUILayout.Label("主角：Z X C V B N M J K = 九个武器技能");
            GUILayout.Label("Boss：F1 三连击 / F2 乌鸦 / F3 隐身 / F4 随机武器技能");
            GUILayout.Label("F5 / F6 / F7 = 切换车厢规则；F8 = 切换 Boss 武器技能");
            GUILayout.Label("移动与桌子：A D / W S / 1 2 / Shift");
            GUILayout.Label("Boss 自动攻击已关闭，方便逐项测试");
            GUILayout.Label(lastAction);
            GUILayout.EndArea();
            GUI.depth = oldDepth;
        }
    }
}
