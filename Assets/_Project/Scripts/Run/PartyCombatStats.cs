using System.Text;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>在籍メンバーの攻撃・防御・運を合算し、戦闘用に係数を掛ける。</summary>
    public readonly struct PartyCombatStats
    {
        public int TotalAttack { get; }
        public int TotalDefense { get; }
        public int TotalLuck { get; }

        public int TotalHp { get; }
        public float WordDamageCoefficient { get; }
        public int WordDamage { get; }
        public float DefenseConstant { get; }
        /// <summary>蓄積の伸び。運120で1、上限220で 220/120。</summary>
        public float JackpotAccumulationScale { get; }
        public float BaseCritChance { get; }
        public float CritMultiplier { get; }
        public float GlobalEnemyHpScale { get; }

        public const int LuckReference = 120;
        public const int LuckCap = 220;

        PartyCombatStats(
            int hp,
            int atk,
            int def,
            int luck,
            float wordDamageCoefficient,
            int wordDamage,
            float defenseConstant,
            float jackpotScale,
            float baseCritChance,
            float critMultiplier,
            float globalEnemyHpScale)
        {
            TotalHp = hp;
            TotalAttack = atk;
            TotalDefense = def;
            TotalLuck = luck;
            WordDamageCoefficient = wordDamageCoefficient;
            WordDamage = wordDamage;
            DefenseConstant = defenseConstant;
            JackpotAccumulationScale = jackpotScale;
            BaseCritChance = baseCritChance;
            CritMultiplier = critMultiplier;
            GlobalEnemyHpScale = globalEnemyHpScale;
        }

        public static PartyCombatStats Compute(RunPartyState party, DungeonRunDefinition runConfig)
        {
            var hp = 0;
            var atk = 0;
            var def = 0;
            var luck = 0;
            if (party?.Members != null)
            {
                foreach (var member in party.Members)
                {
                    var c = member?.character;
                    if (c == null)
                        continue;
                    hp += c.hp;
                    atk += RunPartyState.GetAttack(member);
                    def += c.defense;
                    luck += c.luck;
                }
            }

            var coefficient = runConfig != null && runConfig.partyAttackCoefficient > 0f
                ? runConfig.partyAttackCoefficient
                : 0.04f;
            var wordDamage = (int)CombatMath.WordDamage(atk, coefficient);
            var defenseConstant = runConfig != null && runConfig.partyDefenseConstant > 0f
                ? runConfig.partyDefenseConstant
                : 400f;
            var cappedLuck = Mathf.Clamp(luck, 0, LuckCap);
            var jackpotScale = LuckReference > 0 ? cappedLuck / (float)LuckReference : 0f;
            var baseCrit = runConfig != null ? Mathf.Clamp01(runConfig.baseCritChance) : 0f;
            var critMultiplier = runConfig != null && runConfig.critMultiplier > 0f
                ? runConfig.critMultiplier
                : 1.5f;
            var hpScale = runConfig != null && runConfig.globalEnemyHpScale > 0f
                ? runConfig.globalEnemyHpScale
                : 1f;

            return new PartyCombatStats(
                hp, atk, def, luck, coefficient, wordDamage, defenseConstant, jackpotScale,
                baseCrit, critMultiplier, hpScale);
        }

        public float DamageTakenRatio
        {
            get
            {
                var k = DefenseConstant > 0f ? DefenseConstant : 400f;
                var denom = TotalDefense + k;
                return denom > 0f ? k / denom : 1f;
            }
        }

        public string BuildDetailText(RunPartyState party, DungeonRunDefinition runConfig, RelicRunModifiers relicMods)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【メンバー】");
            if (party?.Members != null)
            {
                foreach (var member in party.Members)
                {
                    var c = member?.character;
                    if (c == null)
                        continue;
                    sb.AppendLine(
                        $"{StarterCharacters.Shown(c.characterName)}（{c.roleLabel}）　HP {c.hp}　攻 {RunPartyState.GetAttack(member)}　防 {c.defense}　運 {c.luck}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("【パーティ合計 → 戦闘】");
            sb.AppendLine($"HP　{TotalHp}");
            sb.AppendLine(
                $"攻撃　{TotalAttack} × {WordDamageCoefficient:0.####} → 単語 {WordDamage} ダメ");
            var takenPercent = Mathf.RoundToInt(DamageTakenRatio * 100f);
            sb.AppendLine(
                $"防御　{TotalDefense}（定数 {DefenseConstant:0}）→ 攻撃の {takenPercent}% を受ける");
            sb.AppendLine(
                $"運　　{Mathf.Min(TotalLuck, LuckCap)}（上限 {LuckCap}）→ 蓄積の伸び ×{JackpotAccumulationScale:0.00}");

            if (relicMods.PlayerDamageBonusPercent > 0 ||
                relicMods.MadnessMaxBonus > 0 ||
                relicMods.JackpotMadnessThreshold > 0 ||
                relicMods.OpeningEnemyStopSeconds > 0f)
            {
                sb.AppendLine();
                sb.AppendLine("【装備レリック（塔内）】");
                if (relicMods.PlayerDamageBonusPercent > 0)
                    sb.AppendLine($"与ダメ +{relicMods.PlayerDamageBonusPercent}%");
                if (relicMods.MadnessMaxBonus > 0)
                    sb.AppendLine($"狂気上限 +{relicMods.MadnessMaxBonus}");
                if (relicMods.JackpotMadnessThreshold > 0)
                {
                    var mul = relicMods.JackpotDamageMultiplier > 0f ? relicMods.JackpotDamageMultiplier : 1f;
                    sb.AppendLine(
                        $"狂気{relicMods.JackpotMadnessThreshold}以上 JACKPOT +{relicMods.JackpotChanceBonus * 100f:0.#}% / 威力×{mul:0.##}");
                }

                if (relicMods.OpeningEnemyStopSeconds > 0f)
                    sb.AppendLine($"戦闘開始時 敵停止 {relicMods.OpeningEnemyStopSeconds:0}秒");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
