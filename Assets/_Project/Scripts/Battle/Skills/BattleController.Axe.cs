using System;
using System.Collections.Generic;
using TMPro;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public sealed partial class BattleController
    {
        AxeSkillRunner _axe;
        AxeDebugPanel _axeDebug;
        BattleSkillData[] _debugAxe;
        TextMeshProUGUI _enemyDebuff;

        void SetupAxe()
        {
            _axe = new AxeSkillRunner(new BattleAxeBridge(this), AxeSkillConfig.Load());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_ui?.RootCard != null)
            {
                _axeDebug?.Destroy();
                _axeDebug = AxeDebugPanel.Create(_ui.RootCard, _axe.Config, DebugEquipAxe, DebugResetAxe, DebugAxeCommand);
                RefreshAxeDebug();
            }
#endif
            EnsureDebuffLabel();
            RefreshEnemyDebuff();
        }

        void TickAxe(float dt)
        {
            _axe?.Tick(dt);
            RefreshEnemyDebuff();
            RefreshAxeDebug();
        }

        bool ApplyAxeSkill(BattleSkillData skill, bool suppressLog, string levelNote)
        {
            var cast = _axe.OnSkillFired(skill);
            var shown = (float)Math.Round(cast.Damage, MidpointRounding.AwayFromZero);
            if (cast.HasDamage && !cast.AllowZero)
                shown = Mathf.Max(1f, shown);
            if (cast.HasDamage)
                shown = (float)Math.Round(shown * cast.IncomingAtHit, MidpointRounding.AwayFromZero);
            var detail = cast.HasDamage
                ? $"{shown:0}ダメ"
                : "発動";
            var msg = $"✨ {StarterCharacters.Shown(skill.CharacterName)}の{skill.DisplayName}！ {detail}";
            if (!string.IsNullOrEmpty(cast.Extra))
                msg += "\n" + cast.Extra;
            if (!string.IsNullOrEmpty(levelNote))
                msg += "\n" + levelNote;
            if (cast.HasDamage && (shown > 0f || cast.AllowZero))
                ApplyDamageToEnemy(shown, suppressLog ? null : msg, true, false);
            else if (!suppressLog)
                SetLog(msg);
            RefreshExView();
            RefreshEnemyDebuff();
            RefreshAxeDebug();
            return false;
        }

        void ApplyDefensiveMitigation(ref float damage, bool fromDot)
        {
            if (fromDot || _axe == null)
            {
                _swordsman?.OnEnemyHit(ref damage, fromDot);
                return;
            }

            _axe.ApplyAttackDown(ref damage);
            var sword = _swordsman != null ? _swordsman.PendingDefenseMultiplier(false) : 1f;
            var guard = _axe.PendingGuardMultiplier;
            var mode = _axe.Config?.options != null ? _axe.Config.options.nextHitWithTatenaoshi : "multiply";
            var combined = mode == "max" ? Mathf.Min(sword, guard) : sword * guard;
            damage *= combined;
            var swordPending = sword < 0.999f;
            var guardPending = guard < 0.999f;
            if (mode == "max")
            {
                if (swordPending || guardPending)
                {
                    _swordsman?.ConsumeDefense();
                    _axe.ConsumeGuard();
                }
            }
            else
            {
                if (swordPending)
                    _swordsman.ConsumeDefense();
                if (guardPending)
                    _axe.ConsumeGuard();
            }
        }

        float WithIncoming(float amount, bool apply)
        {
            if (!apply || _axe == null || amount <= 0f)
                return amount;
            var mul = _axe.Status.IncomingMultiplier;
            if (mul <= 0f)
                return amount;
            return (float)Math.Round(amount * mul, MidpointRounding.AwayFromZero);
        }

        bool AxeIncomingEnabled(bool word, bool card)
        {
            if (_axe?.Config?.options == null)
                return true;
            if (word)
                return _axe.Config.options.vulnAppliesToWord;
            if (card)
                return _axe.Config.options.vulnAppliesToExCards;
            return true;
        }

        void EnsureDebuffLabel()
        {
            if (_enemyDebuff != null || _ui?.RootCard == null)
                return;
            var go = new GameObject("EnemyDebuff", typeof(RectTransform));
            go.transform.SetParent(_ui.RootCard, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.6f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -128f);
            rt.sizeDelta = new Vector2(860f, 36f);
            _enemyDebuff = go.AddComponent<TextMeshProUGUI>();
            _enemyDebuff.font = BattleHudKit.Gothic;
            _enemyDebuff.fontSize = 22f;
            _enemyDebuff.alignment = TextAlignmentOptions.Center;
            _enemyDebuff.color = new Color(1f, 0.86f, 0.45f, 1f);
            _enemyDebuff.raycastTarget = false;
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void RefreshEnemyDebuff()
        {
            if (_enemyDebuff == null)
                return;
            var text = _axe != null ? _axe.Status.DisplayText() : "";
            _enemyDebuff.text = text ?? "";
            _enemyDebuff.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        void RebuildDebugSkills()
        {
            if (_partyState == null)
                return;
            _activeSkills = _partyState.BuildActiveSkills();
            OverlayDebugAlice();
            OverlayDebugAxe();
            RefreshCandidatesUI();
            RefreshSwordsmanDebug();
            RefreshAxeDebug();
        }

        void OverlayDebugAxe()
        {
            if (_debugAxe == null || _activeSkills == null)
                return;
            var indexes = new List<int>();
            for (var i = 0; i < _activeSkills.Count; i++)
            {
                if (IsAxeOwner(_activeSkills[i]))
                    indexes.Add(i);
            }

            for (var slot = 0; slot < _debugAxe.Length; slot++)
            {
                var replacement = _debugAxe[slot];
                if (replacement == null)
                    continue;
                if (slot < indexes.Count)
                {
                    _activeSkills[indexes[slot]] = replacement;
                }
                else
                {
                    var at = indexes.Count == 0 ? 0 : indexes[indexes.Count - 1] + 1;
                    at = Mathf.Min(at, _activeSkills.Count);
                    _activeSkills.Insert(at, replacement);
                    indexes.Add(at);
                }
            }
        }

        bool IsAxeOwner(BattleSkillData skill)
        {
            var name = _axe?.Config != null ? _axe.Config.targetCharacterName : "ルナ";
            return skill != null && StarterCharacters.Same(skill.CharacterName, name);
        }

        void RefreshAxeDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _axeDebug?.SetStatus(_axe != null ? _axe.StatusText() : "");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void DebugEquipAxe(int slot, int skillId, int level)
        {
            var asset = FindRewardSkill(skillId);
            if (asset == null || slot < 0 || slot > 2)
            {
                SetLog("斧スキルが見つかりません");
                return;
            }

            var attack = RunPartyState.GetAttack(AxeMember());
            var data = asset.ToRuntimeData(level, attack);
            data.CasterAttack = attack;
            data.CasterIndex = AxeOrFirstIndex();
            _debugAxe ??= new BattleSkillData[3];
            _debugAxe[slot] = data;
            RebuildDebugSkills();
            SetLog($"{data.DisplayName} をデバッグ装備（セーブしない）");
        }

        void DebugResetAxe()
        {
            _debugAxe = null;
            RebuildDebugSkills();
            SetLog("斧スキルのデバッグ装備を戻した");
        }

        void DebugAxeCommand(string command)
        {
            if (_axe == null || string.IsNullOrEmpty(command))
                return;
            if (command.StartsWith("set:"))
            {
                if (int.TryParse(command.Substring(4), out var value) && _madness != null)
                    _madness.ApplyDelta(value - _madness.CurrentMadness);
            }
            else
            {
                switch (command)
                {
                    case "vuln":
                        _axe.Status.AddVuln("debug", 0.20f, 20f);
                        break;
                    case "atk":
                        _axe.Status.AddAtkDown("debug", 0.20f, 20f);
                        break;
                    case "slow":
                        _axe.Status.AddAtkSlow("debug", 0.50f, 20f);
                        break;
                    case "berserk":
                        _axe.Status.AddBerserkVuln(0.30f);
                        break;
                    case "clear":
                        _axe.Status.Clear();
                        break;
                    case "once":
                        _axe.ResetOnce();
                        break;
                    case "reload":
                        _axe.UseConfig(AxeSkillConfig.Reload());
                        SetLog("斧スキルのJSONを読み直した");
                        break;
                }
            }

            RefreshEnemyDebuff();
            RefreshAxeDebug();
            RefreshDebugCombatReadout();
        }
#endif

        int AxeOrFirstIndex()
        {
            var name = _axe?.Config != null ? _axe.Config.targetCharacterName : "ルナ";
            if (_partyState?.Members == null)
                return 0;
            for (var i = 0; i < _partyState.Members.Count; i++)
            {
                var member = _partyState.Members[i];
                if (member?.character != null && StarterCharacters.Same(member.character.characterName, name))
                    return i;
            }

            return 0;
        }

        MemberRunState AxeMember() => CasterMember(AxeOrFirstIndex());

        sealed class BattleAxeBridge : IAxeBattle
        {
            readonly BattleController _owner;

            public BattleAxeBridge(BattleController owner) => _owner = owner;

            public int Madness => _owner._madness != null ? _owner._madness.CurrentMadness : 0;

            public void ChangeMadness(int delta) => _owner._madness?.ApplyDelta(delta);

            public float EnemyHp => _owner._enemyHp;

            public float EnemyHpRatio =>
                _owner._maxEnemyHp > 0f ? _owner._enemyHp / _owner._maxEnemyHp : 1f;

            public float PlayerDamageMultiplier =>
                (_owner._encounterMods.PlayerDamageMultiplier > 0f ? _owner._encounterMods.PlayerDamageMultiplier : 1f)
                * (_owner._enemyBreak?.DamageMultiplier ?? 1f);

            public int AxeBaseAttack
            {
                get
                {
                    var name = _owner._axe?.Config != null ? _owner._axe.Config.targetCharacterName : "ルナ";
                    if (_owner._partyState?.Members == null)
                        return 0;
                    foreach (var member in _owner._partyState.Members)
                    {
                        if (member?.character != null && StarterCharacters.Same(member.character.characterName, name))
                            return RunPartyState.GetAttack(member);
                    }

                    return 0;
                }
            }

            public bool AddFrenzy(int count)
            {
                if (_owner._ex == null || count <= 0)
                    return false;
                var before = _owner._ex.Stack(ExKind.Frenzy);
                _owner.AddExStatus(ExKind.Frenzy, count);
                return _owner._ex.Stack(ExKind.Frenzy) > before;
            }
        }
    }
}
