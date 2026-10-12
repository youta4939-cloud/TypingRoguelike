using System;
using System.Collections.Generic;
using System.Linq;
using TypingRoguelike.Battle;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Run
{
    [Serializable]
    public sealed class MemberRunState
    {
        public const int InnateSlotIndex = 2;

        public CharacterDefinition character;
        public SkillDefinition slot0;
        public SkillDefinition slot1;
        public SkillDefinition innateSlot;
        public int slot0Level = 1;
        public int slot1Level = 1;

        public SkillDefinition GetSlot(int index) => index switch
        {
            0 => slot0,
            1 => slot1,
            InnateSlotIndex => innateSlot,
            _ => null
        };

        public int GetSlotLevel(int index) => index switch
        {
            0 => SkillProgression.ClampLevel(slot0Level),
            1 => SkillProgression.ClampLevel(slot1Level),
            _ => 1
        };

        public void SetSlot(int index, SkillDefinition skill, int level = 1)
        {
            var clamped = skill == null ? 1 : SkillProgression.ClampLevel(level);
            if (index == 0)
            {
                slot0 = skill;
                slot0Level = clamped;
            }
            else if (index == 1)
            {
                slot1 = skill;
                slot1Level = clamped;
            }
            else if (index == InnateSlotIndex)
                innateSlot = skill;
        }

        public bool Holds(SkillDefinition card) =>
            card != null && (SameSkill(slot0, card) || SameSkill(slot1, card) || SameSkill(innateSlot, card));

        public bool TryGetEquippedLevel(SkillDefinition card, out int level)
        {
            level = 1;
            if (card == null)
                return false;
            if (SameSkill(slot0, card))
            {
                level = GetSlotLevel(0);
                return true;
            }

            if (SameSkill(slot1, card))
            {
                level = GetSlotLevel(1);
                return true;
            }

            return false;
        }

        public bool TryLevelUp(SkillDefinition card, out int newLevel)
        {
            newLevel = 1;
            if (card == null)
                return false;

            if (SameSkill(slot0, card))
                return Raise(ref slot0Level, out newLevel);
            if (SameSkill(slot1, card))
                return Raise(ref slot1Level, out newLevel);
            return false;
        }

        static bool Raise(ref int level, out int newLevel)
        {
            var current = SkillProgression.ClampLevel(level);
            if (current >= SkillProgression.MaxLevel)
            {
                newLevel = SkillProgression.MaxLevel + 1;
                return true;
            }

            level = current + 1;
            newLevel = level;
            return true;
        }

        static bool SameSkill(SkillDefinition equipped, SkillDefinition card) =>
            equipped != null && (equipped == card || equipped.skillId == card.skillId);

        public IEnumerable<SkillDefinition> EquippedCards()
        {
            if (innateSlot != null) yield return innateSlot;
            if (slot0 != null) yield return slot0;
            if (slot1 != null) yield return slot1;
        }
    }

    public enum RewardEquipOutcome
    {
        Equipped,
        NeedReplace,
        NoMatchingMember,
        LeveledUp,
        AlreadyMax
    }

    /// <summary>ラン中のパーティ装備。スキルカードはバックパック不可（v1.2）。</summary>
    public sealed class RunPartyState
    {
        readonly List<MemberRunState> _members = new();
        static readonly SkillDefinition[] EmptyInventory = Array.Empty<SkillDefinition>();

        public IReadOnlyList<MemberRunState> Members => _members;
        /// <summary>v1.2: スキルカード用バックパック廃止。常に空。</summary>
        public IReadOnlyList<SkillDefinition> Inventory => EmptyInventory;

        public float CurrentHp { get; private set; }
        public float MaxHp { get; private set; }
        public bool RunHpInitialized { get; private set; }

        public bool IsRunDead => RunHpInitialized && CurrentHp <= 0f;
        public int BackpackCount => 0;
        public bool IsBackpackFull => true;
        public int EquippedSkillCardCount =>
            _members.Sum(m => (m.slot0 != null ? 1 : 0) + (m.slot1 != null ? 1 : 0));
        public int TotalCarriedCount => EquippedSkillCardCount + MemberInnateCount;

        int MemberInnateCount => _members.Count(m => m.innateSlot != null);
        public int RunCoins { get; private set; }

        public void ClearRunCoins() => RunCoins = 0;

        public void InitializeFrom(PartyLoadoutDefinition party)
        {
            _members.Clear();
            RunHpInitialized = false;
            CurrentHp = 0;
            MaxHp = 0;
            if (party?.members == null)
                return;

            foreach (var m in party.members)
            {
                var state = new MemberRunState { character = m.character, innateSlot = m.character?.innateSkill };
                if (m.equippedCards != null)
                {
                    if (m.equippedCards.Length > 0) state.slot0 = m.equippedCards[0];
                    if (m.equippedCards.Length > 1) state.slot1 = m.equippedCards[1];
                }

                _members.Add(state);
            }
        }

        /// <summary>ダンジョン開始: キャラのみ（初期スキルのみ、カードスロット空）。</summary>
        public void InitializeForNewDungeonRun(PartyLoadoutDefinition party)
        {
            _members.Clear();
            RunHpInitialized = false;
            RunCoins = 0;
            CurrentHp = 0;
            MaxHp = 0;
            if (party?.members == null)
                return;

            foreach (var m in party.members)
                _members.Add(new MemberRunState { character = m.character, innateSlot = m.character?.innateSkill });
        }

        /// <summary>死亡など: 獲得カードのみ外し、各キャラの初期スキルだけ残す。</summary>
        public void ResetAcquiredSkillCards()
        {
            foreach (var member in _members)
            {
                member.slot0 = null;
                member.slot1 = null;
                member.slot0Level = 1;
                member.slot1Level = 1;
            }
        }

        public void WriteMemberSaves(List<TowerRunMemberSave> target)
        {
            target ??= new List<TowerRunMemberSave>();
            target.Clear();
            foreach (var member in _members)
            {
                target.Add(new TowerRunMemberSave
                {
                    slot0SkillId = member.slot0 != null ? member.slot0.skillId : -1,
                    slot1SkillId = member.slot1 != null ? member.slot1.skillId : -1,
                    slot0Level = member.slot0 != null ? member.GetSlotLevel(0) : 1,
                    slot1Level = member.slot1 != null ? member.GetSlotLevel(1) : 1
                });
            }
        }

        public void ApplyMemberSaves(IReadOnlyList<TowerRunMemberSave> saves, SkillDefinition[] skillCatalog)
        {
            if (saves == null || skillCatalog == null)
                return;

            for (var i = 0; i < _members.Count && i < saves.Count; i++)
            {
                var save = saves[i];
                _members[i].slot0 = ResolveSkill(skillCatalog, save.slot0SkillId);
                _members[i].slot1 = ResolveSkill(skillCatalog, save.slot1SkillId);
                _members[i].slot0Level = _members[i].slot0 != null ? SkillProgression.ClampLevel(save.slot0Level) : 1;
                _members[i].slot1Level = _members[i].slot1 != null ? SkillProgression.ClampLevel(save.slot1Level) : 1;
            }
        }

        static SkillDefinition ResolveSkill(SkillDefinition[] catalog, int skillId)
        {
            if (skillId < 0 || catalog == null)
                return null;

            foreach (var skill in catalog)
            {
                if (skill != null && skill.skillId == skillId)
                    return skill;
            }

            return null;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;
            RunCoins += amount;
        }

        public bool TrySpendCoins(int cost)
        {
            if (cost <= 0)
                return true;
            if (RunCoins < cost)
                return false;
            RunCoins -= cost;
            return true;
        }

        public RewardEquipOutcome TryEquipReward(SkillDefinition card, out int memberIndex, out int newLevel)
        {
            memberIndex = -1;
            newLevel = 1;
            if (card == null)
                return RewardEquipOutcome.NoMatchingMember;

            for (var i = 0; i < _members.Count; i++)
            {
                var name = _members[i].character?.characterName;
                if (name != card.characterName)
                    continue;

                memberIndex = i;
                var member = _members[i];
                if (member.TryLevelUp(card, out newLevel))
                    return newLevel > SkillProgression.MaxLevel
                        ? RewardEquipOutcome.AlreadyMax
                        : RewardEquipOutcome.LeveledUp;

                if (member.slot0 == null)
                {
                    member.SetSlot(0, card, 1);
                    return RewardEquipOutcome.Equipped;
                }

                if (member.slot1 == null)
                {
                    member.SetSlot(1, card, 1);
                    return RewardEquipOutcome.Equipped;
                }

                return RewardEquipOutcome.NeedReplace;
            }

            return RewardEquipOutcome.NoMatchingMember;
        }

        public void ReplaceEquippedSlot(int memberIndex, int slotIndex, SkillDefinition card)
        {
            if (card == null || !IsValidSlot(memberIndex, slotIndex))
                return;
            if (slotIndex == MemberRunState.InnateSlotIndex)
                return;

            _members[memberIndex].SetSlot(slotIndex, card);
        }

        public void InitRunHp(float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;
            RunHpInitialized = true;
        }

        public void ApplyHpDelta(int delta)
        {
            if (!RunHpInitialized)
                return;
            CurrentHp = Mathf.Clamp(CurrentHp + delta, 0f, MaxHp);
        }

        public void ClearEquippedSlot(int memberIndex, int slotIndex)
        {
            if (!IsValidSlot(memberIndex, slotIndex) || slotIndex == MemberRunState.InnateSlotIndex)
                return;
            _members[memberIndex].SetSlot(slotIndex, null);
        }

        public void SetCurrentHp(float hp)
        {
            if (!RunHpInitialized)
                return;
            CurrentHp = Mathf.Clamp(hp, 0f, MaxHp);
        }

        public void HealFraction(float fraction)
        {
            if (!RunHpInitialized)
                return;
            var amount = MaxHp * Mathf.Clamp01(fraction);
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        }

        public bool AddToInventory(SkillDefinition card) => false;

        public bool ReplaceInventoryCard(SkillDefinition discarded, SkillDefinition replacement) => false;

        public bool TryEquip(int memberIndex, int slotIndex, SkillDefinition card) => false;

        public bool Unequip(int memberIndex, int slotIndex) => false;

        public HashSet<int> GetOwnedSkillIds()
        {
            var result = new HashSet<int>();
            foreach (var member in _members)
            {
                foreach (var card in member.EquippedCards())
                    result.Add(card.skillId);
            }

            return result;
        }

        public bool TryGetEquippedLevel(SkillDefinition card, out int level)
        {
            level = 1;
            foreach (var member in _members)
            {
                if (member.TryGetEquippedLevel(card, out level))
                    return true;
            }

            return false;
        }

        public HashSet<int> GetMaxedSkillIds()
        {
            var result = new HashSet<int>();
            foreach (var member in _members)
            {
                if (member.slot0 != null && member.GetSlotLevel(0) >= SkillProgression.MaxLevel)
                    result.Add(member.slot0.skillId);
                if (member.slot1 != null && member.GetSlotLevel(1) >= SkillProgression.MaxLevel)
                    result.Add(member.slot1.skillId);
            }

            return result;
        }

        public List<BattleSkillData> BuildActiveSkills() =>
            BuildActiveSkillsWithMeta().Select(entry => entry.skill).ToList();

        public IEnumerable<(BattleSkillData skill, bool isInnate)> BuildActiveSkillsWithMeta()
        {
            foreach (var m in _members)
            {
                if (m.innateSlot != null)
                    yield return (m.innateSlot.ToRuntimeData(), true);
                if (m.slot0 != null)
                    yield return (m.slot0.ToRuntimeData(m.GetSlotLevel(0)), false);
                if (m.slot1 != null)
                    yield return (m.slot1.ToRuntimeData(m.GetSlotLevel(1)), false);
            }
        }

        bool IsValidSlot(int memberIndex, int slotIndex) =>
            memberIndex >= 0 && memberIndex < _members.Count &&
            (slotIndex is 0 or 1 || slotIndex == MemberRunState.InnateSlotIndex);
    }
}
