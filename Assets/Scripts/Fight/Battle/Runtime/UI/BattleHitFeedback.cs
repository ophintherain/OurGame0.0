using System.Collections;
using UnityEngine;

namespace Spotlight.Battle
{
    public sealed class BattleHitFeedback : MonoBehaviour
    {
        [SerializeField] private BattleCoordinator battle;
        [SerializeField] private PlayerBattleController player;
        [SerializeField] private BossController boss;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private BattleCameraRig cameraRig;

        private SpriteRenderer playerArtwork;
        private SpriteRenderer bossArtwork;
        private Color playerBaseColor;
        private Color bossBaseColor;
        private float playerFlash;
        private float bossFlash;
        private Coroutine hitPause;
        private float pauseOriginalScale = 1f;
        private float lastDamageSoundTime = -100f;
        private float lastBossDamageSoundTime = -100f;

        public void Configure(BattleCoordinator coordinator, PlayerBattleController battlePlayer,
            BossController battleBoss, WeaponController weaponController, BattleCameraRig rig)
        {
            Unsubscribe();
            if (hitPause != null)
            {
                StopCoroutine(hitPause);
                Time.timeScale = pauseOriginalScale;
                hitPause = null;
            }
            if (playerArtwork != null) playerArtwork.color = playerBaseColor;
            if (bossArtwork != null && (boss == null || !boss.IsInvisible))
                bossArtwork.color = bossBaseColor;
            playerFlash = 0f;
            bossFlash = 0f;
            battle = coordinator;
            player = battlePlayer;
            boss = battleBoss;
            weapons = weaponController;
            cameraRig = rig;
            playerArtwork = FindArtwork(player != null ? player.transform : null);
            bossArtwork = FindArtwork(boss != null ? boss.transform : null);
            if (playerArtwork != null) playerBaseColor = playerArtwork.color;
            if (bossArtwork != null) bossBaseColor = bossArtwork.color;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (playerArtwork != null) playerArtwork.color = playerBaseColor;
            if (bossArtwork != null && (boss == null || !boss.IsInvisible))
                bossArtwork.color = bossBaseColor;
            if (hitPause != null)
            {
                StopCoroutine(hitPause);
                Time.timeScale = pauseOriginalScale;
                hitPause = null;
            }
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime;
            if (playerFlash > 0f)
            {
                playerFlash = Mathf.Max(0f, playerFlash - step);
                if (playerArtwork != null)
                    playerArtwork.color = Color.Lerp(playerBaseColor, new Color(1f, 0.35f, 0.35f),
                        playerFlash / 0.16f);
            }
            if (bossFlash > 0f)
            {
                bossFlash = Mathf.Max(0f, bossFlash - step);
                if (bossArtwork != null && boss != null && !boss.IsInvisible)
                    bossArtwork.color = Color.Lerp(bossBaseColor, new Color(1f, 0.65f, 0.35f),
                        bossFlash / 0.12f);
            }
        }

        private void Subscribe()
        {
            if (player != null && player.Health != null)
                player.Health.HealthChanged += OnPlayerHealthChanged;
            if (boss != null)
            {
                if (boss.Health != null) boss.Health.HealthChanged += OnBossHealthChanged;
                boss.AttackResolved += OnAttackResolved;
            }
            if (weapons != null) weapons.WeaponUsed += OnWeaponUsed;
            if (player != null) player.PostureChanged += OnPostureChanged;
        }

        private void Unsubscribe()
        {
            if (player != null && player.Health != null)
                player.Health.HealthChanged -= OnPlayerHealthChanged;
            if (boss != null)
            {
                if (boss.Health != null) boss.Health.HealthChanged -= OnBossHealthChanged;
                boss.AttackResolved -= OnAttackResolved;
            }
            if (weapons != null) weapons.WeaponUsed -= OnWeaponUsed;
            if (player != null) player.PostureChanged -= OnPostureChanged;
        }

        private static void OnPostureChanged(PlayerPosture previous, PlayerPosture current)
        {
            UISoundPlayer.Play(UISoundCue.PageTurn);
            UISoundPlayer.Play(UISoundCue.SwitchAction);
        }

        private void OnPlayerHealthChanged(float previous, float current)
        {
            if (battle == null || !battle.IsBattleActive || current >= previous) return;
            if (Time.unscaledTime - lastDamageSoundTime > 0.2f)
            {
                BattleSoundPlayer.Play("角色受击");
                lastDamageSoundTime = Time.unscaledTime;
            }
            playerFlash = 0.16f;
            cameraRig?.RequestImpact(0.12f);
            SpawnBurst(player.transform.position, new Color(1f, 0.3f, 0.3f));
            if (previous - current >= player.Health.MaxHealth * 0.08f) PauseBriefly();
        }

        private void OnBossHealthChanged(float previous, float current)
        {
            if (battle == null || !battle.IsBattleActive || current >= previous) return;
            if (Time.unscaledTime - lastBossDamageSoundTime > 0.2f)
            {
                BattleSoundPlayer.Play("boss受击");
                lastBossDamageSoundTime = Time.unscaledTime;
            }
            bossFlash = 0.12f;
            cameraRig?.RequestImpact(previous - current >= boss.Health.MaxHealth * 0.05f ? 0.1f : 0.035f);
            SpawnBurst(boss.transform.position, new Color(1f, 0.75f, 0.35f));
            if (previous - current >= boss.Health.MaxHealth * 0.08f) PauseBriefly();
        }

        private void OnAttackResolved(BattleTelegraph telegraph, DamageResolution result)
        {
            if (battle == null || !battle.IsBattleActive || !result.Blocked) return;
            cameraRig?.RequestImpact(0.055f);
            SpawnBurst(player.transform.position, new Color(0.7f, 0.9f, 1f));
        }

        private void OnWeaponUsed(int slot, DamageResolution result)
        {
            if (battle == null || !battle.IsBattleActive || weapons == null ||
                slot < 0 || slot >= weapons.Loadout.Weapons.Count) return;
            if (weapons.Loadout.Weapons[slot].Definition.Skill == WeaponSkill.Truce)
            {
                cameraRig?.RequestImpact(0.09f);
                SpawnBurst(boss.transform.position, new Color(1f, 0.85f, 0.5f));
            }
        }

        private void PauseBriefly()
        {
            if (hitPause == null && Time.timeScale > 0f)
                hitPause = StartCoroutine(HitPause());
        }

        private IEnumerator HitPause()
        {
            pauseOriginalScale = Time.timeScale;
            Time.timeScale = pauseOriginalScale * 0.12f;
            yield return new WaitForSecondsRealtime(0.055f);
            Time.timeScale = pauseOriginalScale;
            hitPause = null;
        }

        private static SpriteRenderer FindArtwork(Transform root)
        {
            if (root == null) return null;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.enabled && renderer.sprite != null) return renderer;
            return null;
        }

        private static void SpawnBurst(Vector3 position, Color color)
        {
            GameObject burst = new GameObject("BattleHitBurst");
            burst.transform.position = position + Vector3.up * 1.2f;
            ParticleSystem particles = burst.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.22f;
            main.startSpeed = 2.5f;
            main.startSize = 0.12f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            ParticleSystemRenderer renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 30;
            particles.Emit(9);
            Object.Destroy(burst, 0.6f);
        }
    }

    // These clips are shared by all three fights and the isolated skill test scene.
    internal static class BattleSoundPlayer
    {
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> clips =
            new System.Collections.Generic.Dictionary<string, AudioClip>();
        private static AudioSource source;

        public static void PlayWeapon(WeaponSkill skill)
        {
            string clip = skill switch
            {
                WeaponSkill.Truce => "1 调停v4",
                WeaponSkill.Needle => "2 针刺",
                WeaponSkill.Leap => "3 跃迁",
                WeaponSkill.Mimic => "4 归我",
                WeaponSkill.Poet => "5 诗者",
                WeaponSkill.Barrage => "6弹雨",
                WeaponSkill.Servitude => "7 附庸",
                WeaponSkill.Edge => "8 夺锋",
                WeaponSkill.Recorder => "9 记录者",
                _ => null
            };
            Play(clip);
        }

        public static void PlayBossSkill(int carriage)
        {
            Play(carriage == 1 ? "车厢1 boss技能" : carriage == 2 ? "boss2技能" : "车厢3boss技能");
        }

        public static void Play(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!clips.TryGetValue(name, out AudioClip clip) || clip == null)
            {
                clip = Resources.Load<AudioClip>("BattleAudio/" + name);
                clips[name] = clip;
            }
            if (clip == null) return;
            if (source == null)
            {
                var holder = new GameObject("BattleSoundPlayer");
                Object.DontDestroyOnLoad(holder);
                source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0.7f;
            }
            source.PlayOneShot(clip);
        }
    }
}
