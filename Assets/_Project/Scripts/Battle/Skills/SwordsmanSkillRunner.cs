using System.Collections.Generic;
using TypingRoguelike.Data;

namespace TypingRoguelike.Battle
{
    /// <summary>戦闘中の剣士スキルが読む相手。テストは仮の実装を渡す。</summary>
    public interface ISwordsmanBattle
    {
        int FocusStacks { get; }
        int FocusMax { get; }
        int AddFocusStacks(int count);
        int ConsumeFocus(int count);
        int ConsumeAllFocus();
        void SetFocusStacks(int count);
        bool AddCard(string cardId, int count, int casterIndex, int bloodOnKill);
        int CardCount(string cardId);
        SkillDamageResult Compute(BattleSkillData skill, float percent, float skillOwnMultiplier, bool forceCrit);
        void SetSkillBonus(float bonus);
        void SetCritOverride(float multiplier);
        void AddReroll(int amount, bool ignoreCap);
        int RerollCount { get; }
        void Log(string message);
    }

    public sealed class SwordsmanSkillState
    {
        public string LastFiredKey = "";
        public int StartFocus;
        public int FocusGainedThisBattle;
        public readonly HashSet<string> Used = new HashSet<string>();
        public float KobuBonus;
        public float KobuRemaining;
        public int TateCharges;
        public float TateReduction;
        public bool IaiActive;
        public int IaiLevel = 1;
        public int IaiCaster = -1;
        public bool KataActive;
        public int KataRequired = 20;
        public float KataCrit = 2f;
        public string KataCard = "issen_kiwami";
        public int KataCardCount = 5;
        public int KataCaster = -1;
        public bool Kiwami;
    }

    public readonly struct SwordsmanCast
    {
        public bool Handled { get; }
        public bool HasDamage { get; }
        public bool AllowZero { get; }
        public float Damage { get; }
        public bool IsCrit { get; }
        public string Extra { get; }

        public SwordsmanCast(bool handled, bool hasDamage, bool allowZero, float damage, bool isCrit, string extra)
        {
            Handled = handled;
            HasDamage = hasDamage;
            AllowZero = allowZero;
            Damage = damage;
            IsCrit = isCrit;
            Extra = extra ?? "";
        }
    }

    /// <summary>剣士スキルの条件と効果。戦闘ごとに1つ作る。</summary>
    public sealed class SwordsmanSkillRunner
    {
        readonly ISwordsmanBattle _battle;

        public SwordsmanSkillRunner(ISwordsmanBattle battle, SwordsmanSkillConfig config)
        {
            _battle = battle;
            Config = config ?? new SwordsmanSkillConfig();
            State = new SwordsmanSkillState();
        }

        public SwordsmanSkillConfig Config { get; private set; }
        public SwordsmanSkillState State { get; }

        public void UseConfig(SwordsmanSkillConfig config)
        {
            if (config != null)
                Config = config;
        }

        public bool Owns(BattleSkillData skill)
        {
            var entry = Entry(skill);
            return entry != null;
        }

        public bool CanTrigger(BattleSkillData skill)
        {
            var entry = Entry(skill);
            if (entry == null)
                return true;
            if (entry.oncePerBattle && State.Used.Contains(entry.key))
                return false;
            var options = Config.options;
            if (entry.key == "tsui_no_tachi"
                && options != null
                && !options.tsuiFiresWithZeroFocus
                && _battle.FocusStacks <= 0)
                return false;
            return true;
        }

        public bool HasGainedFocus(int amount)
        {
            if (amount <= 0)
                return true;
            var mode = Config.options != null && !string.IsNullOrEmpty(Config.options.focusGainMode)
                ? Config.options.focusGainMode
                : "startPlusGain";
            if (mode == "current")
                return _battle.FocusStacks >= amount;
            if (mode == "battleTotal")
                return State.FocusGainedThisBattle >= amount;
            return State.StartFocus + State.FocusGainedThisBattle >= amount;
        }

        public SwordsmanCast OnSkillFired(BattleSkillData skill)
        {
            var entry = Entry(skill);
            if (entry == null)
                return default;
            var level = skill.Level <= 0 ? 1 : skill.Level;
            var cast = Cast(skill, entry, level);
            if (entry.oncePerBattle)
                State.Used.Add(entry.key);
            State.LastFiredKey = entry.key;
            return cast;
        }

        public void OnAllySkillFired(BattleSkillData skill)
        {
            if (skill == null)
                return;
            var alice = IsAlice(skill);
            var scope = Config.options != null ? Config.options.chainScope : "self";
            if (!alice && scope == "party")
            {
                State.LastFiredKey = string.IsNullOrEmpty(skill.LogicKey)
                    ? "skill:" + skill.Id
                    : skill.LogicKey;
            }

            if (!State.IaiActive || alice)
                return;
            var entry = Config.Find("iai_no_kamae");
            var count = SwordsmanSkillConfig.At(entry != null ? entry.allySkillBattou : null, State.IaiLevel);
            if (count <= 0)
                return;
            _battle.AddCard("battou", count, State.IaiCaster, 1);
            _battle.Log($"居合ノ構え：抜刀+{count}（×{_battle.CardCount("battou")}）");
        }

        public void NoteExCard(string cardId)
        {
            if (Config.options == null || !Config.options.chainCountsExCards || string.IsNullOrEmpty(cardId))
                return;
            State.LastFiredKey = cardId;
        }

        public void OnEnemyHit(ref float damage, bool fromDot)
        {
            var multiplier = PendingDefenseMultiplier(fromDot);
            if (multiplier >= 0.999f)
                return;
            damage *= multiplier;
            ConsumeDefense();
        }

        public float PendingDefenseMultiplier(bool fromDot)
        {
            if (State.TateCharges <= 0)
                return 1f;
            if (fromDot && (Config.options == null || !Config.options.tateAppliesToDot))
                return 1f;
            var reduction = State.TateReduction;
            if (reduction < 0f)
                reduction = 0f;
            if (reduction > 0.95f)
                reduction = 0.95f;
            return 1f - reduction;
        }

        public void ConsumeDefense()
        {
            if (State.TateCharges > 0)
                State.TateCharges--;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || State.KobuRemaining <= 0f)
                return;
            State.KobuRemaining -= dt;
            if (State.KobuRemaining > 0f)
                return;
            State.KobuRemaining = 0f;
            State.KobuBonus = 0f;
            _battle.SetSkillBonus(0f);
        }

        public void BeginBattle(int carried)
        {
            State.FocusGainedThisBattle = 0;
            var amount = carried < 0 ? 0 : carried;
            var max = _battle.FocusMax > 0 ? _battle.FocusMax : 20;
            if (amount > max)
                amount = max;
            if (amount > 0)
            {
                var added = _battle.AddFocusStacks(amount);
                if (added < amount)
                    _battle.Log("Exエリアがいっぱいで集中が入らない");
            }

            State.StartFocus = _battle.FocusStacks;
        }

        public int ComputeCarry(int held)
        {
            if (held < 0)
                held = 0;
            var decay = Config.options != null ? Config.options.focusCarryDecay : 0;
            var cap = Config.options != null ? Config.options.focusCarryCap : 0;
            var next = held - (decay > 0 ? decay : 0);
            if (next < 0)
                next = 0;
            if (cap > 0 && next > cap)
                next = cap;
            return next;
        }

        public string CarryMessage(int kept)
        {
            var decay = Config.options != null ? Config.options.focusCarryDecay : 0;
            var cap = Config.options != null ? Config.options.focusCarryCap : 0;
            return $"集中を{kept}残した（-{decay}、上限{cap}）";
        }

        public bool AddFocus(int amount, bool countGain, bool announceKiwami)
        {
            if (amount <= 0)
                return false;
            var before = _battle.FocusStacks;
            var added = _battle.AddFocusStacks(amount);
            if (added <= 0 && before <= 0)
                _battle.Log("Exエリアがいっぱいで集中が入らない");
            else if (countGain)
            {
                var overflow = Config.options == null || Config.options.focusGainCountsOverflow;
                State.FocusGainedThisBattle += overflow ? amount : added;
            }

            return TryEnterKiwami(announceKiwami);
        }

        public void SetFocus(int count)
        {
            _battle.SetFocusStacks(count);
        }

        public bool AddGained(int amount)
        {
            if (amount == 0)
                return false;
            State.FocusGainedThisBattle += amount;
            if (State.FocusGainedThisBattle < 0)
                State.FocusGainedThisBattle = 0;
            return TryEnterKiwami(true);
        }

        public void EnterKiwami()
        {
            if (State.Kiwami)
                return;
            var entry = Config.Find("kyokuchi_no_kata");
            State.KataCrit = entry != null && entry.kiwamiCritMultiplier > 0f ? entry.kiwamiCritMultiplier : 2f;
            State.KataCard = entry != null && !string.IsNullOrEmpty(entry.kiwamiAddCard) ? entry.kiwamiAddCard : "issen_kiwami";
            State.KataCardCount = entry != null && entry.kiwamiAddCount > 0 ? entry.kiwamiAddCount : 5;
            State.Kiwami = true;
            _battle.SetCritOverride(State.KataCrit);
            _battle.AddCard(State.KataCard, State.KataCardCount, State.KataCaster, 1);
            _battle.Log("極状態！ 会心ダメ200%");
        }

        public void ClearKiwami()
        {
            State.Kiwami = false;
            _battle.SetCritOverride(0f);
        }

        public void ResetOnce()
        {
            State.Used.Clear();
        }

        public int PreviewTsui(int level, int attack)
        {
            var entry = Config.Find("tsui_no_tachi");
            var per = SwordsmanSkillConfig.At(entry != null ? entry.percentPerFocus : null, level);
            var skill = new BattleSkillData(110, "アリス", "終ノ太刀", System.Array.Empty<ElementColor>(), 0, 0, "");
            skill.CasterAttack = attack;
            var result = _battle.Compute(skill, _battle.FocusStacks * per, 1f, true);
            return (int)System.Math.Round(result.Damage);
        }

        public string StatusText(int tsuiLevel, int attack)
        {
            var options = Config.options;
            var lines = new List<string>
            {
                "直前 " + (string.IsNullOrEmpty(State.LastFiredKey) ? "なし" : State.LastFiredKey),
                "開始時 " + State.StartFocus,
                "この戦闘の獲得 " + State.FocusGainedThisBattle,
                "今の所持 " + _battle.FocusStacks,
                "次に残る見込み " + ComputeCarry(_battle.FocusStacks),
                State.KataActive ? "極致ノ型 出した" : "極致ノ型 まだ",
                State.KobuRemaining > 0f
                    ? $"鼓舞 +{State.KobuBonus * 100f:0}% 残り{State.KobuRemaining:0}秒"
                    : "鼓舞 なし",
                State.TateCharges > 0 ? $"立て直し 待機×{State.TateCharges}" : "立て直し なし",
                "1回きり " + (State.Used.Count == 0 ? "なし" : string.Join("・", State.Used)),
                State.Kiwami ? "極状態" : "極状態 ではない",
                State.IaiActive ? $"居合ノ構え Lv{State.IaiLevel}" : "居合ノ構え なし",
                "終ノ太刀の見込み " + PreviewTsui(tsuiLevel, attack)
            };
            if (options != null)
                lines.Add("直前の範囲 " + options.chainScope);
            return string.Join("\n", lines);
        }

        SwordsmanCast Cast(BattleSkillData skill, SwordsmanSkillEntry entry, int level)
        {
            switch (entry.key)
            {
                case "ichi_no_tachi":
                case "ni_no_tachi":
                case "san_no_tachi":
                case "onagi":
                    return DamageCast(skill, entry, level);
                case "tatenaoshi":
                    return new SwordsmanCast(true, false, false, 0f, false, CastTate(entry, level));
                case "kobu":
                    return new SwordsmanCast(true, false, false, 0f, false, CastKobu(entry, level));
                case "meikyo_shisui":
                    return new SwordsmanCast(true, false, false, 0f, false, CastFocusAdd(entry, level));
                case "noutou":
                case "youtou_kaihou":
                case "iai_no_kamae":
                    return new SwordsmanCast(true, false, false, 0f, false, CastCard(skill, entry, level));
                case "tsui_no_tachi":
                    return CastTsui(skill, entry, level);
                case "kyokuchi_no_kata":
                    return new SwordsmanCast(true, false, false, 0f, false, CastKata(skill, entry, level));
                default:
                    return new SwordsmanCast(true, false, false, 0f, false, "");
            }
        }

        SwordsmanCast DamageCast(BattleSkillData skill, SwordsmanSkillEntry entry, int level)
        {
            var percent = SwordsmanSkillConfig.At(entry.damagePercent, level);
            var own = 1f;
            var extra = "";
            if (entry.key == "onagi")
            {
                var threshold = SwordsmanSkillConfig.At(entry.focusThreshold, level);
                var focus = _battle.FocusStacks;
                if (threshold > 0 && focus >= threshold)
                {
                    own = entry.multiplierWhenConsumed > 0f ? entry.multiplierWhenConsumed : 2f;
                    var boosted = _battle.Compute(skill, percent, own, false);
                    var mode = Config.options != null ? Config.options.onagiFocusConsume : "threshold";
                    var used = _battle.ConsumeFocus(mode == "all" ? focus : threshold);
                    return new SwordsmanCast(true, true, false, boosted.Damage, boosted.IsCrit, $"集中{used}を消費");
                }
            }

            var hit = _battle.Compute(skill, percent, own, false);
            if (entry.key == "ichi_no_tachi")
            {
                var gain = SwordsmanSkillConfig.At(entry.focusAdd, level);
                if (gain > 0)
                {
                    var before = _battle.FocusStacks;
                    var entered = AddFocus(gain, true, false);
                    extra = $"集中+{_battle.FocusStacks - before}（合計{_battle.FocusStacks}）";
                    if (entered)
                        extra += "\n極状態！ 会心ダメ200%";
                }
            }
            else if (entry.key == "ni_no_tachi" && State.LastFiredKey == entry.chainPrev)
            {
                var gain = SwordsmanSkillConfig.At(entry.chainFocusAdd, level);
                var before = _battle.FocusStacks;
                var entered = AddFocus(gain, true, false);
                extra = $"集中+{_battle.FocusStacks - before}（合計{_battle.FocusStacks}）";
                if (entered)
                    extra += "\n極状態！ 会心ダメ200%";
            }
            else if (entry.key == "san_no_tachi"
                     && State.LastFiredKey == entry.chainPrev
                     && HasGainedFocus(entry.focusGainRequired))
            {
                var count = SwordsmanSkillConfig.At(entry.addCount, level);
                _battle.AddCard(entry.addCard, count, skill.CasterIndex, 1);
                extra = $"一閃・極×{_battle.CardCount("issen_kiwami")}";
            }

            return new SwordsmanCast(true, true, false, hit.Damage, hit.IsCrit, extra);
        }

        string CastTate(SwordsmanSkillEntry entry, int level)
        {
            var reduction = SwordsmanSkillConfig.At(entry.nextHitReduction, level);
            if (Config.options != null && Config.options.tateStacks)
                State.TateCharges++;
            else
                State.TateCharges = 1;
            State.TateReduction = reduction;
            var reroll = SwordsmanSkillConfig.At(entry.reroll, level);
            var ignore = Config.options == null || Config.options.tateRerollIgnoresCap;
            _battle.AddReroll(reroll, ignore);
            var percent = (int)System.Math.Round(reduction * 100f);
            return $"立て直し！ 次の被ダメ-{percent}%・リロール+{reroll}";
        }

        string CastKobu(SwordsmanSkillEntry entry, int level)
        {
            var bonus = SwordsmanSkillConfig.At(entry.finalDamageBonus, level);
            var duration = entry.durationSec > 0f ? entry.durationSec : 30f;
            var mode = Config.options != null ? Config.options.kobuStacking : "refresh";
            if (mode == "stack")
                State.KobuBonus += bonus;
            else if (State.KobuBonus < bonus)
                State.KobuBonus = bonus;
            State.KobuRemaining = mode == "extend" ? State.KobuRemaining + duration : duration;
            _battle.SetSkillBonus(State.KobuBonus);
            return $"剣士のスキルダメージ+{State.KobuBonus * 100f:0}%（残り{State.KobuRemaining:0}秒）";
        }

        string CastFocusAdd(SwordsmanSkillEntry entry, int level)
        {
            var gain = SwordsmanSkillConfig.At(entry.focusAdd, level);
            var before = _battle.FocusStacks;
            var entered = AddFocus(gain, true, false);
            var line = $"集中+{_battle.FocusStacks - before}（合計{_battle.FocusStacks}）";
            if (entered)
                line += "\n極状態！ 会心ダメ200%";
            return line;
        }

        string CastCard(BattleSkillData skill, SwordsmanSkillEntry entry, int level)
        {
            var count = SwordsmanSkillConfig.At(entry.addCount, level);
            var blood = SwordsmanSkillConfig.At(entry.bloodOnKill, level);
            if (entry.key == "iai_no_kamae")
            {
                State.IaiActive = true;
                State.IaiLevel = level;
                State.IaiCaster = skill.CasterIndex;
            }

            _battle.AddCard(entry.addCard, count, skill.CasterIndex, blood > 0 ? blood : 1);
            if (entry.key == "noutou")
                return $"赤補充+{count}";
            if (entry.key == "youtou_kaihou")
                return $"百鬼夜行（とどめの怨血+{(blood > 0 ? blood : 1)}）";
            return $"抜刀+{count}（×{_battle.CardCount("battou")}）";
        }

        SwordsmanCast CastTsui(BattleSkillData skill, SwordsmanSkillEntry entry, int level)
        {
            var spent = _battle.ConsumeAllFocus();
            var per = SwordsmanSkillConfig.At(entry.percentPerFocus, level);
            if (spent <= 0)
                return new SwordsmanCast(true, true, true, 0f, false, "集中がない");
            var hit = _battle.Compute(skill, spent * per, 1f, true);
            var shown = (int)System.Math.Round(hit.Damage);
            return new SwordsmanCast(true, true, true, hit.Damage, hit.IsCrit, $"終ノ太刀！ 集中{spent}を消費 {shown}ダメ（会心）");
        }

        string CastKata(BattleSkillData skill, SwordsmanSkillEntry entry, int level)
        {
            State.KataActive = true;
            State.KataRequired = entry.kiwamiFocusGainRequired > 0 ? entry.kiwamiFocusGainRequired : 20;
            State.KataCrit = entry.kiwamiCritMultiplier > 0f ? entry.kiwamiCritMultiplier : 2f;
            State.KataCard = string.IsNullOrEmpty(entry.kiwamiAddCard) ? "issen_kiwami" : entry.kiwamiAddCard;
            State.KataCardCount = entry.kiwamiAddCount > 0 ? entry.kiwamiAddCount : 5;
            State.KataCaster = skill.CasterIndex;
            var gain = SwordsmanSkillConfig.At(entry.focusAdd, level);
            var before = _battle.FocusStacks;
            var entered = AddFocus(gain, true, false);
            var line = $"集中+{_battle.FocusStacks - before}（合計{_battle.FocusStacks}）";
            if (entered)
                line += "\n極状態！ 会心ダメ200%";
            return line;
        }

        bool TryEnterKiwami(bool announce)
        {
            if (State.Kiwami)
                return false;
            var options = Config.options;
            var modern = options != null && options.kyokuNeedsHeld > 0;
            if (!modern)
            {
                if (!State.KataActive || !HasGainedFocus(State.KataRequired))
                    return false;
            }
            else if (options.kyokuRequiresCast)
            {
                if (!State.KataActive || _battle.FocusStacks < options.kyokuNeedsHeld)
                    return false;
            }
            else if (_battle.FocusStacks < options.kyokuNeedsHeld)
            {
                return false;
            }

            State.Kiwami = true;
            _battle.SetCritOverride(State.KataCrit);
            _battle.AddCard(State.KataCard, State.KataCardCount, State.KataCaster, 1);
            if (announce)
                _battle.Log("極状態！ 会心ダメ200%");
            return true;
        }

        SwordsmanSkillEntry Entry(BattleSkillData skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.LogicKey))
                return null;
            return Config.Find(skill.LogicKey);
        }

        static bool IsAlice(BattleSkillData skill) =>
            skill != null && StarterCharacters.Same(skill.CharacterName, StarterCharacters.Chara01);
    }
}
