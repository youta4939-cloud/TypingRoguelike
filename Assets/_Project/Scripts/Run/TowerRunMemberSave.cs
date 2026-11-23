using System;

namespace TypingRoguelike.Run
{
    [Serializable]
    public sealed class TowerRunMemberSave
    {
        public int slot0SkillId = -1;
        public int slot1SkillId = -1;
        public int slot0Level = 1;
        public int slot1Level = 1;
        public int slot0Xp;
        public int slot1Xp;
        public bool innateOverridden;
        public int innateSkillId = -1;
        public int innateLevel = 1;
        public int innateXp;
    }
}
