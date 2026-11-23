using System.Collections.Generic;
using TMPro;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public sealed partial class BattleController
    {
        List<BattleSkillData> EligibleSkills()
        {
            if (_activeSkills == null)
                return new List<BattleSkillData>();
            if (_swordsman == null && _axe == null)
                return _activeSkills;
            var list = new List<BattleSkillData>(_activeSkills.Count);
            foreach (var skill in _activeSkills)
            {
                if (skill == null)
                    continue;
                if (_swordsman != null && !_swordsman.CanTrigger(skill))
                    continue;
                if (_axe != null && !_axe.CanTrigger(skill))
                    continue;
                list.Add(skill);
            }
            return list;
        }

        bool ApplySwordsmanSkill(BattleSkillData skill, bool suppressLog, string levelNote)
        {
            var cast = _swordsman.OnSkillFired(skill);
            var shown = Mathf.RoundToInt(cast.Damage);
            if (cast.HasDamage && !cast.AllowZero)
                shown = Mathf.Max(1, shown);
            if (cast.HasDamage && shown > 0)
                shown = Mathf.RoundToInt(WithIncoming(shown, true));
            var detail = cast.HasDamage
                ? (cast.IsCrit ? $"会心！ {shown}ダメ" : $"{shown}ダメ")
                : "発動";
            var msg = $"✨ {StarterCharacters.Shown(skill.CharacterName)}の{skill.DisplayName}！ {detail}";
            if (!string.IsNullOrEmpty(cast.Extra))
                msg += "\n" + cast.Extra;
            if (!string.IsNullOrEmpty(levelNote))
                msg += "\n" + levelNote;
            if (!suppressLog)
                _recentSkillLog = msg;
            if (cast.HasDamage && shown > 0)
                ApplyDamageToEnemy(shown, suppressLog ? null : msg, true, false);
            else if (!suppressLog)
                SetLog(msg);
            RefreshExView();
            RefreshKiwamiMark();
            RefreshSwordsmanDebug();
            return cast.IsCrit;
        }

        float KobuFinal(BattleSkillData skill)
        {
            if (_kobuBonus <= 0f || !IsAliceSkill(skill))
                return 1f;
            return 1f + _kobuBonus;
        }

        float? KiwamiCrit(BattleSkillData skill)
        {
            if (_kiwamiCrit <= 0f || !IsAliceSkill(skill))
                return null;
            return _kiwamiCrit;
        }

        void RefreshKiwamiMark()
        {
            var show = _swordsman != null && _swordsman.State.Kiwami;
            if (show && _kiwamiMark == null && _ui?.RootCard != null)
            {
                var go = new GameObject("KiwamiMark", typeof(RectTransform));
                go.transform.SetParent(_ui.RootCard, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(36f, -210f);
                rt.sizeDelta = new Vector2(80f, 36f);
                _kiwamiMark = go.AddComponent<TextMeshProUGUI>();
                _kiwamiMark.font = BattleHudKit.Mincho;
                _kiwamiMark.fontSize = 28f;
                _kiwamiMark.color = new Color(0.95f, 0.82f, 0.35f, 1f);
                _kiwamiMark.alignment = TextAlignmentOptions.Center;
                _kiwamiMark.text = "極";
                _kiwamiMark.raycastTarget = false;
            }

            if (_kiwamiMark != null)
                _kiwamiMark.gameObject.SetActive(show);
        }

        void RefreshSwordsmanDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_swordsmanDebug == null || !_swordsmanDebug.IsOpen || _swordsman == null)
                return;
            var level = 1;
            if (_activeSkills != null)
            {
                foreach (var skill in _activeSkills)
                {
                    if (skill != null && skill.LogicKey == "tsui_no_tachi")
                        level = skill.Level;
                }
            }

            _swordsmanDebug.SetStatus(
                _swordsman.StatusText(level, RunPartyState.GetAttack(AliceMember()))
                + "\n保存中の持ち越し " + (_partyState != null ? _partyState.CarriedFocus : 0));
#endif
        }

        void DebugEquipSwordsman(int slot, int skillId, int level)
        {
            var asset = FindRewardSkill(skillId);
            if (asset == null || slot < 0 || slot > 2)
            {
                SetLog("剣士スキルが見つかりません");
                return;
            }

            var attack = RunPartyState.GetAttack(AliceMember());
            var data = asset.ToRuntimeData(level, attack);
            data.CasterAttack = attack;
            data.CasterIndex = AliceOrFirstIndex();
            _debugAlice ??= new BattleSkillData[3];
            _debugAlice[slot] = data;
            ApplyDebugAlice();
            SetLog($"{data.DisplayName} をデバッグ装備（セーブしない）");
        }

        void DebugResetSwordsman()
        {
            _debugAlice = null;
            RebuildDebugSkills();
            SetLog("剣士スキルのデバッグ装備を戻した");
        }

        void DebugSwordsmanCommand(string command)
        {
            if (_swordsman == null || string.IsNullOrEmpty(command))
                return;
            if (command.StartsWith("set:"))
            {
                if (int.TryParse(command.Substring(4), out var count))
                    _swordsman.SetFocus(count);
            }
            else if (command.StartsWith("carry:"))
            {
                if (int.TryParse(command.Substring(6), out var carry))
                    _partyState?.SetCarriedFocus(carry);
            }
            else
            {
                switch (command)
                {
                    case "kiwami":
                        _swordsman.EnterKiwami();
                        break;
                    case "clearkiwami":
                        _swordsman.ClearKiwami();
                        break;
                    case "gain":
                        _swordsman.AddGained(10);
                        break;
                    case "once":
                        _swordsman.ResetOnce();
                        break;
                    case "reload":
                        _swordsman.UseConfig(SwordsmanSkillConfig.Reload());
                        SetLog("剣士スキルのJSONを読み直した");
                        break;
                }
            }

            RefreshExView();
            RefreshKiwamiMark();
            RefreshSwordsmanDebug();
        }

        void ApplyDebugAlice() => RebuildDebugSkills();

        void OverlayDebugAlice()
        {
            if (_debugAlice == null || _activeSkills == null)
                return;
            var indexes = new List<int>();
            for (var i = 0; i < _activeSkills.Count; i++)
            {
                if (IsAliceSkill(_activeSkills[i]))
                    indexes.Add(i);
            }

            for (var slot = 0; slot < _debugAlice.Length; slot++)
            {
                var replacement = _debugAlice[slot];
                if (replacement == null)
                    continue;
                if (slot < indexes.Count)
                {
                    _activeSkills[indexes[slot]] = replacement;
                }
                else
                {
                    var at = indexes.Count == 0 ? 0 : indexes[indexes.Count - 1] + 1;
                    _activeSkills.Insert(Mathf.Min(at, _activeSkills.Count), replacement);
                    indexes.Add(at);
                }
            }
        }

        MemberRunState AliceMember() => CasterMember(AliceOrFirstIndex());

        static SkillDefinition FindRewardSkill(int skillId)
        {
            var pool = Resources.Load<SkillCardPoolDefinition>("Battle/PrototypeRewardCardPool");
            if (pool?.cards == null)
                return null;
            foreach (var card in pool.cards)
            {
                if (card != null && card.skillId == skillId)
                    return card;
            }
            return null;
        }

        sealed class BattleSwordsmanBridge : ISwordsmanBattle
        {
            readonly BattleController _owner;

            public BattleSwordsmanBridge(BattleController owner) => _owner = owner;

            public int FocusStacks => _owner._ex != null ? _owner._ex.Stack(ExKind.Focus) : 0;

            public int FocusMax =>
                _owner._exConfig?.focus != null && _owner._exConfig.focus.maxStacks > 0
                    ? _owner._exConfig.focus.maxStacks
                    : 20;

            public int RerollCount => _owner._typing != null ? _owner._typing.RerollCount : 0;

            public int AddFocusStacks(int count)
            {
                if (_owner._ex == null || count <= 0)
                    return 0;
                var added = _owner._ex.AddStacks(ExKind.Focus, count, FocusMax);
                _owner.RefreshExView();
                return added;
            }

            public int ConsumeFocus(int count)
            {
                if (_owner._ex == null)
                    return 0;
                var used = _owner._ex.ConsumeStacks(ExKind.Focus, count);
                _owner.RefreshExView();
                return used;
            }

            public int ConsumeAllFocus()
            {
                var had = FocusStacks;
                _owner._ex?.ConsumeAll(ExIds.Focus);
                _owner.RefreshExView();
                return had;
            }

            public void SetFocusStacks(int count)
            {
                _owner._ex?.SetStacks(ExKind.Focus, count, FocusMax);
                _owner.RefreshExView();
            }

            public bool AddCard(string cardId, int count, int casterIndex, int bloodOnKill)
            {
                var kind = ToKind(cardId);
                if (kind == null)
                    return false;
                return _owner.AddSpecialCard(kind.Value, count, casterIndex, bloodOnKill);
            }

            public int CardCount(string cardId)
            {
                var kind = ToKind(cardId);
                return kind == null ? 0 : _owner.GetCount(kind.Value);
            }

            public SkillDamageResult Compute(BattleSkillData skill, float percent, float skillOwnMultiplier, bool forceCrit) =>
                _owner.ComputeSkillHit(skill, true, percent, skillOwnMultiplier, forceCrit);

            public void SetSkillBonus(float bonus) => _owner._kobuBonus = bonus > 0f ? bonus : 0f;

            public void SetCritOverride(float multiplier) => _owner._kiwamiCrit = multiplier > 0f ? multiplier : 0f;

            public void AddReroll(int amount, bool ignoreCap)
            {
                _owner._typing?.AddReroll(amount, _owner._maxRerolls, ignoreCap);
                _owner.RefreshRerollBadge();
            }

            public void Log(string message)
            {
                if (!string.IsNullOrEmpty(message))
                    _owner.SetLog(message);
            }

            static SpecialCardKind? ToKind(string cardId)
            {
                switch (cardId)
                {
                    case "red_refill": return SpecialCardKind.RedRefill;
                    case "battou": return SpecialCardKind.Battou;
                    case "issen_kiwami": return SpecialCardKind.IssenKiwami;
                    case "hyakki_yakou": return SpecialCardKind.HyakkiYakou;
                    default: return null;
                }
            }
        }
    }
}
