using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public static class BattleUITemplateLoader
    {
        const string DefaultResourcePath = "Battle/UI/DefaultBattleUITemplate";

        static BattleUITemplateDefinition _cached;

        public static BattleUITemplateDefinition Default
        {
            get
            {
                if (_cached)
                    return _cached;

                _cached = Resources.Load<BattleUITemplateDefinition>(DefaultResourcePath);
                return _cached;
            }
        }

        public static void ClearCache() => _cached = null;
    }
}
