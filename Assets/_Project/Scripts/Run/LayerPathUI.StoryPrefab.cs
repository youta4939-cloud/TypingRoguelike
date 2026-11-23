using System;
using System.Collections.Generic;
using UnityEngine;

namespace TypingRoguelike.Run
{
    public sealed partial class LayerPathUI
    {
        // Bind data and callbacks only. Do not reset RectTransforms, fonts or authored colors.
        void BindStoryPrefab(StoryEventPrefabView ui, StoryEventView view, Action<int> onPick)
        {
            ui.badge.text = view.Badge ?? "";
            ui.title.text = string.IsNullOrEmpty(view.Title) ? "出来事" : view.Title;
            ui.coins.text = "塔片 " + FormatCount(view.Coins);
            ui.materials.text = "素材 " + FormatCount(view.Materials);
            ui.body.text = view.Body ?? "";
            ui.aside.text = view.Aside ?? "";
            ui.aside.gameObject.SetActive(!string.IsNullOrEmpty(view.Aside));
            ui.hp.text = $"HP {view.Hp}/{Mathf.Max(1, view.HpMax)}";
            ui.madness.text = $"狂気 {view.Madness}/{Mathf.Max(1, view.MadnessMax)}";
            for (var i = 0; i < ui.party.Length; i++)
            {
                var slot = ui.party[i];
                var member = i < _storyParty.Length ? _storyParty[i] : null;
                slot.root.SetActive(member != null);
                if (member == null) continue;
                slot.name.text = member.Name ?? "";
                slot.affinity.text = "好感度 " + member.Trust;
                slot.delta.text = "";
                slot.portrait.sprite = StoryPortrait(member);
                _storyTrust.Add(new StoryTrustReadout { Id = member.CharacterId, Name = member.Name, Label = slot.delta });
            }
            var choices = view.Choices ?? Array.Empty<StoryEventChoiceView>();
            for (var i = 0; i < ui.choices.Length; i++)
            {
                var slot = ui.choices[i];
                var choice = i < choices.Length ? choices[i] : null;
                var marks = choice?.Marks ?? Array.Empty<StoryEventTrustMark>();
                slot.label.text = choice?.Label ?? "……";
                var index = i;
                slot.button.onClick.RemoveAllListeners();
                slot.button.onClick.AddListener(() =>
                {
                    if (_storyChoiceSubmitted || (_storyTransition != null && _storyTransition.IsRunning)) return;
                    _storyChoiceSubmitted = true;
                    foreach (var other in ui.choices) other.button.interactable = false;
                    onPick?.Invoke(index);
                });
                slot.hover.Bind(slot.border, () => PreviewTrust(index, marks), () => EndTrustPreview(index));
                BindStoryEffects(slot, choice?.Effects, view.ShowChance);
                var used = new HashSet<string>();
                var next = 0;
                foreach (var supporter in slot.supporters)
                {
                    StoryEventTrustMark mark = null;
                    TrustGaugeView member = null;
                    while (next < marks.Length)
                    {
                        var candidate = marks[next++];
                        if (candidate == null || !candidate.Supporter) continue;
                        member = FindStoryMember(candidate);
                        if (member == null || !used.Add(member.CharacterId ?? member.Name ?? "")) continue;
                        mark = candidate; break;
                    }
                    foreach (var visual in supporter.visuals) visual.SetActive(mark != null);
                    if (mark == null) continue;
                    supporter.portrait.sprite = StoryPortrait(member);
                    supporter.name.text = member.Name ?? "";
                    supporter.speech.text = mark.Line ?? "";
                }
            }
        }

        static void BindStoryEffects(StoryEventPrefabView.Choice slot, StoryEventEffectLine[] effects, bool showChance)
        {
            var reward = false;
            var index = 0;
            foreach (var line in effects ?? Array.Empty<StoryEventEffectLine>())
            {
                if (line == null) continue;
                if (line.Kind == "note") { reward = true; continue; }
                if (line.Kind != "gap" && string.IsNullOrEmpty(line.Text)) continue;
                var gap = line.Kind == "gap";
                var row = UnityEngine.Object.Instantiate(gap ? slot.gapTemplate : slot.rowTemplate, slot.effects);
                row.name = gap ? "OutcomeGap" : "EffectRow" + index++;
                row.SetActive(true);
                if (gap) continue;
                var fields = row.GetComponent<StoryEventEffectRow>();
                fields.effect.text = (reward ? "勝利後：" : "") + EventText.DisplayLine(line);
                fields.chance.text = (line.Chance ?? "").TrimEnd(':');
                fields.chance.gameObject.SetActive(showChance);
                if (reward) fields.layout.padding.left = slot.rewardIndent;
                reward = false;
            }
        }
    }
}
