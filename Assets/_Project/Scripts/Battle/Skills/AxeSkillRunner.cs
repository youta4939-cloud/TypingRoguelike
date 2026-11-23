using System;
using System.Collections.Generic;
using System.Globalization;

namespace TypingRoguelike.Battle
{
    public interface IAxeBattle
    {
        int Madness { get; }
        void ChangeMadness(int delta);
        float EnemyHp { get; }
        float EnemyHpRatio { get; }
        float PlayerDamageMultiplier { get; }
        int AxeBaseAttack { get; }
        bool AddFrenzy(int count);
    }

    public sealed class AxeCast
    {
        public float Damage;
        public bool HasDamage;
        public bool AllowZero;
        public float IncomingAtHit = 1f;
        public string Extra = "";
        public bool IsCrit;
    }

    /// <summary>斧スキルの効果。数字は JSON。Unity には依存しない。</summary>
    public sealed class AxeSkillRunner
    {
        readonly IAxeBattle _battle;
        readonly HashSet<string> _used = new HashSet<string>();
        float _berserkAtkPct;
        int _berserkNeed = 500;
        bool _berserkAtkLatched;
        int _guardCharges;
        float _guardReduction;

        public AxeSkillRunner(IAxeBattle battle, AxeSkillConfig config)
        {
            _battle = battle;
            Config = config ?? new AxeSkillConfig();
            Status = new EnemyStatusState();
            SyncModes();
        }

        public AxeSkillConfig Config { get; private set; }
        public EnemyStatusState Status { get; }

        public void UseConfig(AxeSkillConfig config)
        {
            if (config == null)
                return;
            Config = config;
            SyncModes();
        }

        public bool Owns(BattleSkillData skill) => Entry(skill) != null;

        public bool CanTrigger(BattleSkillData skill)
        {
            var entry = Entry(skill);
            if (entry == null)
                return true;
            return !entry.oncePerBattle || !_used.Contains(entry.key);
        }

        public bool BerserkUsed => _used.Contains("berserk");

        public bool BerserkAtkActive()
        {
            if (_berserkAtkPct <= 0f)
                return false;
            var check = Config.options != null ? Config.options.berserkAtkCheck : "continuous";
            if (check == "onCast")
                return _berserkAtkLatched;
            var madness = _battle != null ? _battle.Madness : 0;
            return madness >= _berserkNeed;
        }

        public int WordAttackBonus()
        {
            if (Config.options != null && !Config.options.berserkAtkAppliesToWord)
                return 0;
            if (!BerserkAtkActive() || _battle == null)
                return 0;
            return (int)Math.Round(_battle.AxeBaseAttack * (double)_berserkAtkPct, MidpointRounding.AwayFromZero);
        }

        public float PendingGuardMultiplier =>
            _guardCharges > 0 ? Math.Max(0f, 1f - _guardReduction) : 1f;

        public void ConsumeGuard()
        {
            if (_guardCharges > 0)
                _guardCharges--;
        }

        public void ApplyAttackDown(ref float damage) => damage *= Status.AttackMultiplier;

        public void ApplyOwnHit(ref float damage)
        {
            ApplyAttackDown(ref damage);
            var guard = PendingGuardMultiplier;
            damage *= guard;
            if (guard < 0.999f)
                ConsumeGuard();
        }

        public void Tick(float dt) => Status.Tick(dt);

        public void ResetOnce() => _used.Remove("berserk");

        public AxeCast OnSkillFired(BattleSkillData skill)
        {
            SyncModes();
            var entry = Entry(skill);
            var cast = new AxeCast { IncomingAtHit = Status.IncomingMultiplier };
            if (entry == null || skill == null)
                return cast;

            var level = skill.Level <= 0 ? 1 : skill.Level;
            switch (entry.key)
            {
                case "yoroi_kudaki":
                    Deal(cast, skill, entry, 1f);
                    var yoroi = AxeSkillConfig.At(entry.vuln, level);
                    var yoroiSec = Seconds(entry.vulnDurationSec, level);
                    Status.AddVuln(entry.key, yoroi, yoroiSec);
                    cast.Extra = $"被ダメ+{Pct(yoroi)}%（{yoroiSec:0}秒）";
                    break;
                case "ken_tachi":
                    var down = AxeSkillConfig.At(entry.enemyAtkDown, level);
                    var downSec = DownSeconds(entry, level);
                    Status.AddAtkDown(entry.key, down, downSec);
                    cast.Extra = $"攻撃-{Pct(down)}%（{downSec:0}秒）";
                    break;
                case "jinarashi":
                    var slow = entry.enemyAtkSpeedDown > 0f ? entry.enemyAtkSpeedDown : 0.5f;
                    var slowSec = SlowSeconds(entry, level);
                    Status.AddAtkSlow(entry.key, slow, slowSec);
                    cast.Extra = $"速度{Pct(slow)}%DOWN（{slowSec:0}秒）";
                    break;
                case "kabuto_wari":
                    var bonus = Status.HasAnyDebuff ? AxeSkillConfig.At(entry.debuffBonus, level) : 0f;
                    Deal(cast, skill, entry, 1f + bonus);
                    cast.Extra = bonus > 0f ? $"デバフあり +{Pct(bonus)}%" : "デバフなし";
                    break;
                case "tsuige":
                    Deal(cast, skill, entry, 1f);
                    var extend = AxeSkillConfig.At(entry.extendDebuffSec, level);
                    var extendBerserk = Config.options != null && Config.options.tsuigeExtendsBerserk;
                    Status.ExtendTimed(extend, extendBerserk);
                    cast.Extra = $"デバフ時間+{extend:0}秒";
                    break;
                case "bougyo_taisei":
                    _guardReduction = AxeSkillConfig.At(entry.nextHitReduction, level);
                    _guardCharges = 1;
                    cast.Extra = $"次の攻撃を{Pct(_guardReduction)}%軽減";
                    break;
                case "kyogeki":
                    Deal(cast, skill, entry, 1f);
                    var stacks = entry.addDenpa > 0 ? entry.addDenpa : 1;
                    var added = _battle != null && _battle.AddFrenzy(stacks);
                    cast.Extra = added ? "伝播+" + stacks : "Exエリアがいっぱいで伝播が入らない";
                    break;
                case "goufu_ichigeki":
                    var per = AxeSkillConfig.At(entry.percentPerDebuff, level);
                    var kinds = Status.DebuffKindCount;
                    Deal(cast, skill, entry, 1f + per * kinds);
                    cast.Extra = $"デバフ{kinds}種";
                    break;
                case "kishu":
                    var ratio = _battle != null ? _battle.EnemyHpRatio : 1f;
                    var threshold = entry.hpThreshold > 0f ? entry.hpThreshold : 0.8f;
                    var hpBonus = ratio + 0.0001f >= threshold ? AxeSkillConfig.At(entry.hpBonus, level) : 0f;
                    Deal(cast, skill, entry, 1f + hpBonus);
                    cast.Extra = hpBonus > 0f ? "HP80%以上" : "HP80%未満";
                    break;
                case "juso":
                    var jAtk = AxeSkillConfig.At(entry.enemyAtkDown, level);
                    var jAtkSec = DownSeconds(entry, level);
                    var jSlow = entry.enemyAtkSpeedDown > 0f ? entry.enemyAtkSpeedDown : 0.5f;
                    var jSlowSec = SlowSeconds(entry, level);
                    var jVuln = AxeSkillConfig.At(entry.vuln, level);
                    var jVulnSec = Seconds(entry.vulnDurationSec, level);
                    Status.AddAtkDown(entry.key, jAtk, jAtkSec);
                    Status.AddAtkSlow(entry.key, jSlow, jSlowSec);
                    Status.AddVuln(entry.key, jVuln, jVulnSec);
                    cast.Extra = $"攻撃-{Pct(jAtk)}%（{jAtkSec:0}秒）　速度{Pct(jSlow)}%DOWN（{jSlowSec:0}秒）　被ダメ+{Pct(jVuln)}%（{jVulnSec:0}秒）";
                    break;
                case "danzai":
                    Deal(cast, skill, entry, 1f);
                    cast.AllowZero = true;
                    cast.HasDamage = true;
                    TryDanzaiKill(cast, entry, level);
                    break;
                case "berserk":
                    _used.Add(entry.key);
                    var bVuln = AxeSkillConfig.At(entry.vuln, level);
                    _berserkAtkPct = AxeSkillConfig.At(entry.atkUp, level);
                    _berserkNeed = entry.atkUpMadnessNeed > 0 ? entry.atkUpMadnessNeed : 500;
                    var check = Config.options != null ? Config.options.berserkAtkCheck : "continuous";
                    _berserkAtkLatched = check == "onCast" && _battle != null && _battle.Madness >= _berserkNeed;
                    Status.AddBerserkVuln(bVuln);
                    cast.Extra = BerserkAtkActive()
                        ? $"被ダメ+{Pct(bVuln)}%（戦闘中）　ATK+{Pct(_berserkAtkPct)}%"
                        : $"被ダメ+{Pct(bVuln)}%（戦闘中）　ATK+は狂気不足";
                    break;
            }

            return cast;
        }

        public string StatusText()
        {
            var atk = _battle != null ? _battle.AxeBaseAttack : 0;
            var bonus = WordAttackBonus();
            var madness = _battle != null ? _battle.Madness : 0;
            var danzai = Config.Find("danzai");
            var preview = danzai != null ? madness * AxeSkillConfig.At(danzai.madnessDamagePercent, 1) : 0f;
            return string.Format(
                CultureInfo.InvariantCulture,
                "被ダメ×{0:0.00}\n{1}\nバーサーク{2}　ATK+{3}\n斧ATK {4}（+{5}）　断罪Lv1見込み {6:0}\n{7}",
                Status.IncomingMultiplier,
                string.IsNullOrEmpty(Status.DisplayText()) ? "デバフなし" : Status.DisplayText(),
                BerserkUsed ? "使用済み" : "未使用",
                BerserkAtkActive() ? "あり" : "なし",
                atk,
                bonus,
                preview,
                Status.Breakdown());
        }

        void Deal(AxeCast cast, BattleSkillData skill, AxeSkillEntry entry, float own)
        {
            var level = skill.Level <= 0 ? 1 : skill.Level;
            var attack = skill.CasterAttack;
            var percent = AxeSkillConfig.At(entry.damagePercent, level);
            var flat = 0f;
            if (entry.key == "danzai")
            {
                var madness = _battle != null ? _battle.Madness : 0;
                flat = madness * AxeSkillConfig.At(entry.madnessDamagePercent, level);
                if (Config.options != null && Config.options.berserkAtkAppliesToDanzai && BerserkAtkActive())
                    flat *= 1f + _berserkAtkPct;
                percent = 0f;
                attack = 0;
            }
            else
            {
                attack += AttackBonus();
            }

            var result = CombatMath.ComputeSkillDamage(new SkillDamageRequest
            {
                CasterAttack = attack,
                Percent = percent,
                FlatDamage = flat,
                SkillOwnMultiplier = own > 0f ? own : 1f,
                FinalMultiplier = 1f,
                CritChance = 0f,
                CritMultiplier = 1f,
                Roll = 1f
            });
            cast.Damage = result.Damage;
            cast.IsCrit = result.IsCrit;
            cast.HasDamage = entry.key == "danzai" || percent > 0f;
            cast.AllowZero = entry.key == "danzai";
        }

        int AttackBonus()
        {
            if (!BerserkAtkActive() || _battle == null)
                return 0;
            return (int)Math.Round(_battle.AxeBaseAttack * (double)_berserkAtkPct, MidpointRounding.AwayFromZero);
        }

        void TryDanzaiKill(AxeCast cast, AxeSkillEntry entry, int level)
        {
            if (Config.options != null && !Config.options.danzaiReducesMadness)
                return;
            if (_battle == null || _battle.EnemyHp <= 0f)
                return;
            var delta = AxeSkillConfig.At(entry.madnessOnKill, level);
            if (delta == 0)
                return;
            var shown = Math.Round(cast.Damage, MidpointRounding.AwayFromZero);
            var dealt = Math.Round(shown * cast.IncomingAtHit, MidpointRounding.AwayFromZero);
            var relic = _battle.PlayerDamageMultiplier > 0f ? _battle.PlayerDamageMultiplier : 1f;
            if (dealt * relic < _battle.EnemyHp)
                return;
            _battle.ChangeMadness(delta);
            var line = $"狂気 {delta}（残り{_battle.Madness}）";
            cast.Extra = string.IsNullOrEmpty(cast.Extra) ? line : cast.Extra + "\n" + line;
        }

        void SyncModes()
        {
            Status.AtkDownMode = Config.options != null && !string.IsNullOrEmpty(Config.options.atkDownStacking)
                ? Config.options.atkDownStacking
                : "add";
            Status.SlowMode = Config.options != null && !string.IsNullOrEmpty(Config.options.aslowStacking)
                ? Config.options.aslowStacking
                : "max";
            Status.GoufuMode = Config.options != null && !string.IsNullOrEmpty(Config.options.goufuVulnCountsAs)
                ? Config.options.goufuVulnCountsAs
                : "separate";
        }

        AxeSkillEntry Entry(BattleSkillData skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.LogicKey))
                return null;
            return Config?.Find(skill.LogicKey);
        }

        static float Seconds(float[] values, int level)
        {
            var sec = AxeSkillConfig.At(values, level);
            return sec > 0f ? sec : 0f;
        }

        static float DownSeconds(AxeSkillEntry entry, int level)
        {
            if (entry.atkDownDurationSec != null && entry.atkDownDurationSec.Length > 0)
                return Seconds(entry.atkDownDurationSec, level);
            return Seconds(entry.durationSec, level);
        }

        static float SlowSeconds(AxeSkillEntry entry, int level)
        {
            if (entry.aslowDurationSec != null && entry.aslowDurationSec.Length > 0)
                return Seconds(entry.aslowDurationSec, level);
            return Seconds(entry.durationSec, level);
        }

        static int Pct(float ratio) =>
            (int)Math.Round(ratio * 100d, MidpointRounding.AwayFromZero);
    }
}
