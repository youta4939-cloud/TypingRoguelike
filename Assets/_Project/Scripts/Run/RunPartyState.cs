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
        public int innateLevel = 1;
        public int slot0Xp;
        public int slot1Xp;
        public int innateXp;

        public SkillDefinition GetSlot(int index) => index switch
        {
            0 => slot0,
            1 => slot1,
            InnateSlotIndex => innateSlot,
            _ => null
        };

        public int GetSlotXp(int index) => index switch
        {
            0 => Mathf.Max(0, slot0Xp),
            1 => Mathf.Max(0, slot1Xp),
            InnateSlotIndex => Mathf.Max(0, innateXp),
            _ => 0
        };

        public int GetSlotLevel(int index) => SkillProgression.LevelFromXp(GetSlotXp(index));

        public void SetSlot(int index, SkillDefinition skill, int level = 1)
        {
            var xp = skill == null ? 0 : SkillProgression.XpForLevel(level);
            AssignSlot(index, skill, xp);
        }

        public void RestoreSlot(int index, SkillDefinition skill, int savedXp, int legacyLevel)
        {
            var xp = skill == null
                ? 0
                : savedXp > 0 ? savedXp : SkillProgression.XpForLevel(legacyLevel);
            AssignSlot(index, skill, xp);
        }

        public bool TryAddXpToSlot(int index, int amount, out int previousLevel, out int newLevel, out int xp)
        {
            previousLevel = GetSlotLevel(index);
            xp = GetSlotXp(index);
            newLevel = previousLevel;
            if (GetSlot(index) == null || amount == 0)
                return false;

            xp = Mathf.Max(0, xp + amount);
            AssignSlot(index, GetSlot(index), xp);
            newLevel = GetSlotLevel(index);
            return true;
        }

        void AssignSlot(int index, SkillDefinition skill, int xp)
        {
            var storedXp = skill == null ? 0 : Mathf.Max(0, xp);
            var level = skill == null ? 1 : SkillProgression.LevelFromXp(storedXp);
            if (index == 0)
            {
                slot0 = skill;
                slot0Xp = storedXp;
                slot0Level = level;
            }
            else if (index == 1)
            {
                slot1 = skill;
                slot1Xp = storedXp;
                slot1Level = level;
            }
            else if (index == InnateSlotIndex)
            {
                innateSlot = skill;
                innateXp = storedXp;
                innateLevel = level;
            }
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

            if (SameSkill(innateSlot, card))
            {
                level = GetSlotLevel(InnateSlotIndex);
                return true;
            }

            return false;
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
        AlreadyOwned
    }

    public readonly struct SkillXpGrant
    {
        public bool Applied { get; }
        public bool Leveled { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public int Xp { get; }
        public string SkillName { get; }

        public SkillXpGrant(bool applied, bool leveled, int previousLevel, int newLevel, int xp, string skillName)
        {
            Applied = applied;
            Leveled = leveled;
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            Xp = xp;
            SkillName = skillName ?? string.Empty;
        }
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
        List<string> _deckCardIds;
        public List<string> DeckCardIds => _deckCardIds ??= TypingRoguelike.Battle.Deck.DeckCardCatalog.BuildStarterDeckIds();
        public List<string> RewardHistory { get; } = new();

        public void RestoreDeck(List<string> ids, List<string> history)
        {
            _deckCardIds = ids != null && ids.Count > 0 ? new List<string>(ids) : null;
            RewardHistory.Clear();
            if (history != null)
                RewardHistory.AddRange(history.GetRange(0, Mathf.Min(history.Count, 2)));
        }

        public int RunCoins { get; private set; }
        public int Blood { get; private set; }
        public int CarriedFocus { get; private set; }

        public void ResetBlood() => Blood = 0;

        public void SetBlood(int blood) => Blood = blood < 0 ? 0 : blood;

        public void ResetCarriedFocus() => CarriedFocus = 0;

        public void SetCarriedFocus(int focus) => CarriedFocus = focus < 0 ? 0 : focus;

        public int AddBlood(int amount, int cap)
        {
            Blood = SpecialCardMath.AddBlood(Blood, amount, cap);
            return Blood;
        }

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
            RestoreDeck(null, null);
            CurrentHp = 0;
            MaxHp = 0;
            Blood = 0;
            CarriedFocus = 0;
            if (party?.members == null)
                return;

            var solo = party.members.FirstOrDefault(m => m.character != null &&
                StarterCharacters.Same(m.character.characterName, StarterCharacters.Chara03)).character;
            solo ??= party.members.FirstOrDefault(m => m.character != null).character;
            if (solo != null)
                _members.Add(new MemberRunState { character = solo });
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
                member.slot0Xp = 0;
                member.slot1Xp = 0;
                member.innateLevel = 1;
                member.innateXp = 0;
                member.innateSlot = member.character != null ? member.character.innateSkill : member.innateSlot;
            }
        }

        public void WriteMemberSaves(List<TowerRunMemberSave> target)
        {
            target ??= new List<TowerRunMemberSave>();
            target.Clear();
            foreach (var member in _members)
            {
                var originalId = member.character?.innateSkill != null ? member.character.innateSkill.skillId : -1;
                var innateId = member.innateSlot != null ? member.innateSlot.skillId : -1;
                target.Add(new TowerRunMemberSave
                {
                    slot0SkillId = member.slot0 != null ? member.slot0.skillId : -1,
                    slot1SkillId = member.slot1 != null ? member.slot1.skillId : -1,
                    slot0Level = member.slot0 != null ? member.GetSlotLevel(0) : 1,
                    slot1Level = member.slot1 != null ? member.GetSlotLevel(1) : 1,
                    slot0Xp = member.slot0 != null ? member.GetSlotXp(0) : 0,
                    slot1Xp = member.slot1 != null ? member.GetSlotXp(1) : 0,
                    innateOverridden = innateId != originalId,
                    innateSkillId = innateId,
                    innateLevel = member.GetSlotLevel(MemberRunState.InnateSlotIndex),
                    innateXp = member.GetSlotXp(MemberRunState.InnateSlotIndex)
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
                var member = _members[i];
                member.RestoreSlot(0, ResolveSkill(skillCatalog, save.slot0SkillId), save.slot0Xp, save.slot0Level);
                member.RestoreSlot(1, ResolveSkill(skillCatalog, save.slot1SkillId), save.slot1Xp, save.slot1Level);
                var innate = save.innateOverridden
                    ? ResolveSkill(skillCatalog, save.innateSkillId) ?? member.innateSlot
                    : member.innateSlot;
                member.RestoreSlot(MemberRunState.InnateSlotIndex, innate, save.innateXp, save.innateLevel);
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
                if (!StarterCharacters.Same(name, card.characterName))
                    continue;

                memberIndex = i;
                var member = _members[i];
                if (member.Holds(card))
                {
                    member.TryGetEquippedLevel(card, out newLevel);
                    return RewardEquipOutcome.AlreadyOwned;
                }

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

            _members[memberIndex].SetSlot(slotIndex, card);
        }

        public void InitRunHp(float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;
            RunHpInitialized = true;
        }

        public void SetMaxHpPreservingCurrent(float newMax)
        {
            if (!RunHpInitialized)
            {
                InitRunHp(newMax);
                return;
            }

            var clamped = Mathf.Max(1f, newMax);
            CurrentHp = Mathf.Min(CurrentHp, clamped);
            MaxHp = clamped;
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

        public bool TryGrantSkillXp(int skillId, int amount, out SkillXpGrant grant)
        {
            grant = default;
            if (skillId < 0 || amount == 0)
                return false;

            foreach (var member in _members)
            {
                if (GrantIfMatch(member, MemberRunState.InnateSlotIndex, skillId, amount, out grant))
                    return true;
                if (GrantIfMatch(member, 0, skillId, amount, out grant))
                    return true;
                if (GrantIfMatch(member, 1, skillId, amount, out grant))
                    return true;
            }

            return false;
        }

        public bool TryUpgradeRandomSkill(System.Random rng, out string skillName, out int newLevel)
        {
            skillName = "";
            newLevel = 1;
            var options = new List<(int member, int slot)>();
            for (var i = 0; i < _members.Count; i++)
            {
                var member = _members[i];
                TryAddUpgradeCandidate(options, i, member, 0);
                TryAddUpgradeCandidate(options, i, member, 1);
                TryAddUpgradeCandidate(options, i, member, MemberRunState.InnateSlotIndex);
            }

            if (options.Count == 0 || rng == null)
                return false;

            var pick = options[rng.Next(options.Count)];
            var skill = _members[pick.member].GetSlot(pick.slot);
            var xp = _members[pick.member].GetSlotXp(pick.slot);
            var need = SkillProgression.NextLevelXp(xp) - xp;
            if (skill == null || need <= 0)
                return false;
            if (!TryGrantSlotXp(pick.member, pick.slot, need, out var grant) || !grant.Leveled)
                return false;

            skillName = string.IsNullOrEmpty(skill.displayName) ? "刻印" : skill.displayName;
            newLevel = grant.NewLevel;
            return true;
        }

        static void TryAddUpgradeCandidate(List<(int member, int slot)> options, int memberIndex, MemberRunState member, int slot)
        {
            if (member == null || member.GetSlot(slot) == null)
                return;
            if (member.GetSlotLevel(slot) >= SkillProgression.MaxLevel)
                return;
            options.Add((memberIndex, slot));
        }

        public bool TryGrantSlotXp(int memberIndex, int slotIndex, int amount, out SkillXpGrant grant)
        {
            grant = default;
            if (!IsValidSlot(memberIndex, slotIndex))
                return false;

            var member = _members[memberIndex];
            var skill = member.GetSlot(slotIndex);
            if (skill == null || !member.TryAddXpToSlot(slotIndex, amount, out var previous, out var next, out var xp))
                return false;

            grant = new SkillXpGrant(true, next > previous, previous, next, xp, skill.displayName);
            return true;
        }

        static bool GrantIfMatch(MemberRunState member, int slotIndex, int skillId, int amount, out SkillXpGrant grant)
        {
            grant = default;
            var skill = member.GetSlot(slotIndex);
            if (skill == null || skill.skillId != skillId)
                return false;
            if (!member.TryAddXpToSlot(slotIndex, amount, out var previous, out var next, out var xp))
                return false;

            grant = new SkillXpGrant(true, next > previous, previous, next, xp, skill.displayName);
            return true;
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
                if (member.innateSlot != null && member.GetSlotLevel(MemberRunState.InnateSlotIndex) >= SkillProgression.MaxLevel)
                    result.Add(member.innateSlot.skillId);
            }

            return result;
        }

        public static int GetAttack(CharacterDefinition character) =>
            character != null ? character.attack : 0;

        public static int GetAttack(MemberRunState member) =>
            GetAttack(member?.character);

        public int AttackOf(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
                return 0;
            foreach (var member in _members)
            {
                var character = member?.character;
                if (character != null && StarterCharacters.Same(character.characterName, characterName))
                    return GetAttack(member);
            }

            return 0;
        }

        public void ApplySwordsmanInnate(SkillDefinition swordsmanInnate, bool replace)
        {
            foreach (var member in _members)
            {
                if (member?.character == null)
                    continue;
                if (!StarterCharacters.Same(member.character.characterName, StarterCharacters.Chara01))
                    continue;
                if (replace)
                {
                    if (swordsmanInnate != null && (member.innateSlot == null || member.innateSlot.skillId < 100))
                        member.innateSlot = swordsmanInnate;
                }
                else if (member.innateSlot != null && member.innateSlot.skillId >= 100 && member.innateSlot.skillId <= 111)
                {
                    member.innateSlot = member.character.innateSkill;
                }
            }
        }

        public void ApplyAxeInnate(SkillDefinition axeInnate, bool replace, string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
                targetName = StarterCharacters.Chara03;
            foreach (var member in _members)
            {
                if (member?.character == null)
                    continue;
                if (!StarterCharacters.Same(member.character.characterName, targetName))
                    continue;
                if (replace)
                {
                    var current = member.innateSlot != null ? member.innateSlot.skillId : -1;
                    if (axeInnate != null && (current < 200 || current > 211))
                        member.innateSlot = axeInnate;
                }
                else if (member.innateSlot != null && member.innateSlot.skillId >= 200 && member.innateSlot.skillId <= 211)
                {
                    member.innateSlot = member.character.innateSkill;
                }
            }
        }

        public List<BattleSkillData> BuildActiveSkills() =>
            BuildActiveSkillsWithMeta().Select(entry => entry.skill).ToList();

        public IEnumerable<(BattleSkillData skill, bool isInnate)> BuildActiveSkillsWithMeta()
        {
            for (var index = 0; index < _members.Count; index++)
            {
                var m = _members[index];
                if (m == null)
                    continue;
                var attack = GetAttack(m);
                if (m.innateSlot != null)
                    yield return (BindCaster(m.innateSlot.ToRuntimeData(m.GetSlotLevel(MemberRunState.InnateSlotIndex), attack), attack, index), true);
                if (m.slot0 != null)
                    yield return (BindCaster(m.slot0.ToRuntimeData(m.GetSlotLevel(0), attack), attack, index), false);
                if (m.slot1 != null)
                    yield return (BindCaster(m.slot1.ToRuntimeData(m.GetSlotLevel(1), attack), attack, index), false);
            }
        }

        static BattleSkillData BindCaster(BattleSkillData skill, int attack, int index)
        {
            if (skill == null)
                return null;
            skill.CasterAttack = attack;
            skill.CasterIndex = index;
            return skill;
        }

        bool IsValidSlot(int memberIndex, int slotIndex) =>
            memberIndex >= 0 && memberIndex < _members.Count &&
            (slotIndex is 0 or 1 || slotIndex == MemberRunState.InnateSlotIndex);
    }
}
