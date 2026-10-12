using System;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>塔探索ラン中の狂気度。戦闘・マップイベントで増減し、セーブに載せる。</summary>
    [Serializable]
    public sealed class RunMadnessState
    {
        public const int DefaultMaxMadness = 1000;

        [SerializeField] int currentMadness;
        [SerializeField] int maxMadness = DefaultMaxMadness;
        [SerializeField] float bonusJackpotChance;
        [SerializeField] float accumulatedJackpotChance;

        public int CurrentMadness => currentMadness;
        public int MaxMadness => Mathf.Max(1, maxMadness);
        public float BonusJackpotChance => bonusJackpotChance;
        /// <summary>塔探索中の Jackpot 当選率蓄積（戦闘をまたいで持ち越し）。</summary>
        public float AccumulatedJackpotChance => accumulatedJackpotChance;

        public enum MadnessStage
        {
            Calm = 0,
            Caution = 1,
            Danger = 2,
            Critical = 3
        }

        public MadnessStage Stage
        {
            get
            {
                var ratio = CurrentMadness / (float)MaxMadness;
                if (ratio <= 0.3f) return MadnessStage.Calm;
                if (ratio <= 0.6f) return MadnessStage.Caution;
                if (ratio <= 0.9f) return MadnessStage.Danger;
                return MadnessStage.Critical;
            }
        }

        public void ResetForNewRun(int max = DefaultMaxMadness)
        {
            currentMadness = 0;
            maxMadness = Mathf.Max(1, max);
            bonusJackpotChance = 0f;
            accumulatedJackpotChance = 0f;
        }

        public void Restore(int current, int max, float bonusJackpot, float accumulatedJackpot = 0f)
        {
            maxMadness = Mathf.Max(1, max);
            currentMadness = Mathf.Clamp(current, 0, maxMadness);
            bonusJackpotChance = Mathf.Max(0f, bonusJackpot);
            accumulatedJackpotChance = Mathf.Max(0f, accumulatedJackpot);
        }

        public void SetAccumulatedJackpotChance(float value) =>
            accumulatedJackpotChance = Mathf.Max(0f, value);

        public void SetMaxMadness(int max)
        {
            maxMadness = Mathf.Max(1, max);
            if (currentMadness > maxMadness)
                currentMadness = maxMadness;
        }

        public void SetBonusJackpotChance(float value) => bonusJackpotChance = Mathf.Max(0f, value);

        public void AddBonusJackpotChance(float delta) => bonusJackpotChance = Mathf.Max(0f, bonusJackpotChance + delta);

        /// <summary>単語完了攻撃成功時。最大到達（発狂）なら true。</summary>
        public bool AddFromWordAttack(int inputLength, float gainMultiplier = 1f)
        {
            var add = Mathf.Max(0, Mathf.RoundToInt(inputLength * Mathf.Max(0f, gainMultiplier)));
            currentMadness += add;
            if (currentMadness >= MaxMadness)
            {
                currentMadness = MaxMadness;
                return true;
            }

            return false;
        }

        public void ApplyDelta(int delta) => currentMadness = Mathf.Clamp(currentMadness + delta, 0, MaxMadness);

        /// <summary>加算後に最大到達（発狂）なら true。到達時は current を Max にクランプ。</summary>
        public bool ApplyDeltaAndCheckInsanity(int delta)
        {
            currentMadness += delta;
            if (currentMadness >= MaxMadness)
            {
                currentMadness = MaxMadness;
                return true;
            }

            if (currentMadness < 0)
                currentMadness = 0;
            return false;
        }

        public bool IsAtOrOverMax() => currentMadness >= MaxMadness;

        public int ComputeJackpotDamage(float jackpotMultiplier) =>
            Mathf.Max(0, Mathf.RoundToInt(currentMadness * Mathf.Max(0f, jackpotMultiplier)));

        public void ResetMadnessAfterJackpot() => currentMadness = 0;

        public static Color StageColor(MadnessStage stage) => stage switch
        {
            MadnessStage.Calm => new Color(0.45f, 0.72f, 0.95f, 1f),
            MadnessStage.Caution => new Color(0.95f, 0.78f, 0.35f, 1f),
            MadnessStage.Danger => new Color(0.95f, 0.45f, 0.28f, 1f),
            _ => new Color(0.92f, 0.18f, 0.38f, 1f)
        };
    }
}
