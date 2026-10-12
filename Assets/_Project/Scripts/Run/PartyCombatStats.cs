using System.Text;
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

        public float WordClearDamage { get; }
        public float DamageReduction { get; }
        /// <summary>1語完了ごとに JACKPOT 抽選へ加算（0〜1）。</summary>
        public float JackpotLuckBonusPerWord { get; }

        PartyCombatStats(
            int atk,
            int def,
            int luck,
            float wordDamage,
            float damageReduction,
            float jackpotBonus)
        {
            TotalAttack = atk;
            TotalDefense = def;
            TotalLuck = luck;
            WordClearDamage = wordDamage;
            DamageReduction = damageReduction;
            JackpotLuckBonusPerWord = jackpotBonus;
        }

        public static PartyCombatStats Compute(RunPartyState party, DungeonRunDefinition runConfig)
        {
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
                    atk += c.attack;
                    def += c.defense;
                    luck += c.luck;
                }
            }

            var atkCoeff = runConfig != null ? runConfig.partyAttackCoefficient : 1f;
            var defCoeff = runConfig != null ? runConfig.partyDefenseCoefficient : 0.5f;
            var luckPerPoint = runConfig != null ? runConfig.partyLuckJackpotBonusPerPoint : 0.0005f;

            var wordDamage = Mathf.Max(1f, atk * atkCoeff);
            var reduction = Mathf.Max(0f, def * defCoeff);
            var jackpotBonus = Mathf.Max(0f, luck * luckPerPoint);

            return new PartyCombatStats(atk, def, luck, wordDamage, reduction, jackpotBonus);
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
                        $"{c.characterName}（{c.roleLabel}）　攻 {c.attack}　防 {c.defense}　運 {c.luck}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("【パーティ合計 → 戦闘】");
            sb.AppendLine($"攻撃　{TotalAttack} × {Coeff(runConfig?.partyAttackCoefficient, 1f):0.##} → 単語 {(int)WordClearDamage} ダメ");
            sb.AppendLine(
                $"防御　{TotalDefense} × {Coeff(runConfig?.partyDefenseCoefficient, 0.5f):0.##} → 被ダメ -{(int)DamageReduction}");
            sb.AppendLine(
                $"運　　{TotalLuck} × {Coeff(runConfig?.partyLuckJackpotBonusPerPoint, 0.0005f):0.####} → JACKPOT +{JackpotLuckBonusPerWord * 100f:0.1}% / 語");

            if (relicMods.PlayerDamageBonusPercent > 0 ||
                relicMods.DangerEncounterReductionPercent > 0 ||
                relicMods.MadnessMaxBonus > 0 ||
                relicMods.JackpotMadnessThreshold > 0 ||
                relicMods.RevealsAllNodes ||
                relicMods.OpeningEnemyStopSeconds > 0f)
            {
                sb.AppendLine();
                sb.AppendLine("【装備レリック（塔内）】");
                if (relicMods.PlayerDamageBonusPercent > 0)
                    sb.AppendLine($"与ダメ +{relicMods.PlayerDamageBonusPercent}%");
                if (relicMods.DangerEncounterReductionPercent > 0)
                    sb.AppendLine($"遭遇率 -{relicMods.DangerEncounterReductionPercent}%");
                if (relicMods.MadnessMaxBonus > 0)
                    sb.AppendLine($"狂気上限 +{relicMods.MadnessMaxBonus}");
                if (relicMods.JackpotMadnessThreshold > 0)
                {
                    var mul = relicMods.JackpotDamageMultiplier > 0f ? relicMods.JackpotDamageMultiplier : 1f;
                    sb.AppendLine(
                        $"狂気{relicMods.JackpotMadnessThreshold}以上 JACKPOT +{relicMods.JackpotChanceBonus * 100f:0.#}% / 威力×{mul:0.##}");
                }

                if (relicMods.RevealsAllNodes)
                    sb.AppendLine(relicMods.ExclusiveEquipped ? "マップ全開示（単独装備）" : "マップ全開示");
                if (relicMods.OpeningEnemyStopSeconds > 0f)
                    sb.AppendLine($"戦闘開始時 敵停止 {relicMods.OpeningEnemyStopSeconds:0}秒");
            }

            return sb.ToString().TrimEnd();
        }

        static float Coeff(float? value, float fallback) => value ?? fallback;
    }
}
