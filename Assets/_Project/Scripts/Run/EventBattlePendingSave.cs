using System;
using TypingRoguelike.Data;

namespace TypingRoguelike.Run
{
    /// <summary>出来事の戦闘が終わるまで取っておく報酬。戦闘そのものの補正は active 中だけ使う。</summary>
    [Serializable]
    public sealed class EventBattlePendingSave
    {
        public bool active;
        public bool elite;
        public string enemyId = "";
        public int enemyHpPercent;
        public int openingSeconds;
        public string resultText = "";
        public string eventTitle = "";
        public string battleIntro = "";
        public string victoryText = "";
        public EventEffect[] rewards = Array.Empty<EventEffect>();
    }
}
