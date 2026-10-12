using TypingRoguelike.Battle;
using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>
    /// Enter Play Mode Options で Domain Reload を切っていると、
    /// ランタイム生成スプライト等の static が破棄済み参照のまま残り UI が描画されない。
    /// </summary>
    static class TypingRoguelikePlayModeReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCachedUnityObjects()
        {
            BattleUISprites.ResetCache();
            BattleUITheme.ResetFontCache();
            BattleUITemplateLoader.ClearCache();
        }
    }
}
