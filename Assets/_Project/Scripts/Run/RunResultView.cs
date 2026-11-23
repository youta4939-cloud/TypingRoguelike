using System;
using System.Globalization;
using TMPro;
using TypingRoguelike.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>
    /// 帰還と死亡のリザルト。位置はプレハブ側を使う。
    /// 文字と死亡行の表示は再生時に入る。
    /// </summary>
    public sealed class RunResultView : MonoBehaviour
    {
        public const string ResourcePath = "UI/RunResult";

        public TMP_Text subtitle;
        public RunResultRow[] rows = new RunResultRow[4];
        public TMP_Text subtotalPoints;
        public GameObject deathPenalty;
        public TMP_Text deathLabel;
        public TMP_Text deathRate;
        public TMP_Text deathPoints;
        public TMP_Text totalValue;
        public Button hubButton;

        public bool Ready =>
            subtitle != null && subtotalPoints != null && totalValue != null && hubButton != null &&
            deathPenalty != null && rows != null && rows.Length >= 4 && rows[0] != null && rows[0].label != null;

        public static RunResultView Load() => Resources.Load<RunResultView>(ResourcePath);

        public void Apply(string subtitleText, bool death, RunScore.Breakdown score, int totalXp, Action onHub)
        {
            RepairFonts();
            if (subtitle != null)
            {
                subtitle.text = subtitleText;
                subtitle.color = death
                    ? new Color(0.86f, 0.58f, 0.62f, 1f)
                    : new Color(0.965f, 0.945f, 0.925f, 1f);
            }

            Write(Row(0), $"{score.TypedChars.ToString("N0", CultureInfo.InvariantCulture)}文字", score.TypedPoints);
            Write(Row(1), $"{score.TrashKills.ToString("N0", CultureInfo.InvariantCulture)}体", score.TrashPoints);
            Write(Row(2), $"{score.Squares.ToString("N0", CultureInfo.InvariantCulture)}マス", score.SquarePoints);
            Write(Row(3), $"{score.BossKills.ToString("N0", CultureInfo.InvariantCulture)}体", score.BossPoints);
            if (subtotalPoints != null)
                subtotalPoints.text = score.Subtotal.ToString("N0", CultureInfo.InvariantCulture);
            if (deathPenalty != null)
                deathPenalty.SetActive(death);
            if (death)
            {
                if (deathRate != null)
                    deathRate.text = "−50%";
                if (deathPoints != null)
                    deathPoints.text = "−" + (score.Subtotal - score.Subtotal / 2).ToString("N0", CultureInfo.InvariantCulture);
            }

            if (totalValue != null)
            {
                totalValue.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:N0}/{1:N0}",
                    Mathf.Max(0, totalXp),
                    RunScore.XpCap);
                totalValue.fontSize = 56f;
            }
            if (hubButton != null)
            {
                hubButton.onClick.RemoveAllListeners();
                hubButton.onClick.AddListener(() => onHub?.Invoke());
            }
        }

        RunResultRow Row(int index) =>
            rows != null && index >= 0 && index < rows.Length ? rows[index] : null;

        static void Write(RunResultRow row, string actualText, int points)
        {
            if (row == null)
                return;
            if (row.actual != null)
                row.actual.text = actualText;
            if (row.points != null)
                row.points.text = "+" + points.ToString("N0", CultureInfo.InvariantCulture);
        }

        void RepairFonts()
        {
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                var size = text.fontSize;
                text.font = size >= 70f
                    ? BattleHudKit.SerifMedium
                    : size >= 26f ? BattleHudKit.SerifRegular : BattleHudKit.Sans;
                if (text.font == null)
                    text.font = BattleHudKit.SerifRegular;
                text.fontSize = size;
            }
        }
    }

    [Serializable]
    public sealed class RunResultRow
    {
        public TMP_Text label;
        public TMP_Text actual;
        public TMP_Text points;
    }
}
