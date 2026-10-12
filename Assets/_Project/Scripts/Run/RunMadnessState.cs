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
        [SerializeField] int configuredMadnessCap = DefaultMaxMadness;
        [SerializeField] int deviceCapPenalty;
        [SerializeField] float bonusJackpotChance;
        [SerializeField] float accumulatedJackpotChance;

        public int CurrentMadness => currentMadness;
        public int MaxMadness => Mathf.Max(1, maxMadness);
        public int ConfiguredMadnessCap => Mathf.Max(1, configuredMadnessCap);
        public int DeviceCapPenalty => Mathf.Max(0, deviceCapPenalty);
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
            configuredMadnessCap = Mathf.Max(1, max);
            deviceCapPenalty = 0;
            maxMadness = configuredMadnessCap;
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

        public void RestoreCurrentAndJackpot(int current, float bonusJackpot, float accumulatedJackpot = 0f)
        {
            currentMadness = Mathf.Clamp(current, 0, MaxMadness);
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

        /// <summary>レリック・永続強化による上限（装置の傷みは別）。</summary>
        public void SetConfiguredMadnessCap(int cap)
        {
            configuredMadnessCap = Mathf.Max(1, cap);
            ApplyEffectiveMadnessCap();
        }

        /// <summary>ジャックポット等によるラン中の上限低下。帰還・全滅で 0 に戻す。</summary>
        public void SetDeviceCapPenalty(int penalty)
        {
            deviceCapPenalty = Mathf.Max(0, penalty);
            ApplyEffectiveMadnessCap();
        }

        public void ClearDeviceCapPenalty() => SetDeviceCapPenalty(0);

        void ApplyEffectiveMadnessCap()
        {
            var effective = Mathf.Max(1, configuredMadnessCap - deviceCapPenalty);
            SetMaxMadness(effective);
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
