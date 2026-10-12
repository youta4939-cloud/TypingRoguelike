using System;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [Serializable]
    public struct PartyMemberLoadout
    {
        public CharacterDefinition character;
        [Tooltip("フェーズ3までの暫定：装備スキルカード最大2")]
        public SkillDefinition[] equippedCards;
    }

    [CreateAssetMenu(fileName = "PartyLoadout", menuName = "TypingRoguelike/Party Loadout")]
    public sealed class PartyLoadoutDefinition : ScriptableObject
    {
        public PartyMemberLoadout[] members = Array.Empty<PartyMemberLoadout>();
    }
}
