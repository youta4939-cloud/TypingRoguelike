using System.Collections.Generic;
using System.Text;
using TypingRoguelike.Battle.Deck;
using TypingRoguelike.Data;
using UnityEngine;

namespace TypingRoguelike.Battle
{
    public sealed partial class BattleController
    {
        DeckBattleState _deck;
        DeckHandUIView _deckHandUi;
        int _deckAttackBuff;
        ElementColor? _deckPreviousColor;

        bool DeckMode => GameModeConfig.UseDeckMode;

        void SetupDeckBattle()
        {
            if (!DeckMode)
                return;
            var ids = _partyState?.DeckCardIds ?? DeckCardCatalog.BuildStarterDeckIds();
            _deck = new DeckBattleState(ids);
            _deckHandUi = _ui?.DeckHand;
            _deckAttackBuff = 0;
            if (_ui?.SkillListRoot != null)
                _ui.SkillListRoot.gameObject.SetActive(false);
            _deckPreviousColor = null;
            _ui?.DebugCombatReadout?.gameObject.SetActive(false);
            _ui?.DebugCritTestButton?.gameObject.SetActive(false);
            _ui?.DebugAliceSlashButton?.gameObject.SetActive(false);
            RefreshDeckHandUI();
        }

        void TryTriggerDeckCards()
        {
            if (!DeckMode || _deck == null || _ex != null && _ex.Stack(ExKind.Paralysis) > 0)
            {
                if (DeckMode && _deck != null && _ex != null && _ex.Stack(ExKind.Paralysis) > 0)
                {
                    var pending = DeckCardResolver.ResolveTriggered(_deck.Hand, _stock);
                    if (pending.Count > 0)
                        SetLog("麻痺でカードが出ない");
                }

                return;
            }

            var triggered = DeckCardResolver.ResolveTriggered(_deck.Hand, _stock);
            if (triggered.Count == 0)
                return;

            _deckHandUi?.PrepareHandChange(_deck.Hand, _stock, _deckPreviousColor);
            var log = new StringBuilder();
            var replacements = new List<string>();
            foreach (var slot in triggered)
            {
                var def = slot.Def;
                if (def == null)
                    continue;
                if (log.Length > 0)
                    log.Append('\n');
                log.Append(ResolveDeckCard(slot, def));
                if (def.Consumable && !string.IsNullOrEmpty(def.AddCardId))
                    replacements.Add(def.AddCardId);
            }

            if (log.Length > 0)
                SetLog(log.ToString());
            foreach (var cardId in replacements)
                _deck.InsertCardToHand(cardId);
            _deck.FillHandToSize();
            _deckHandUi?.PlayHandChange(_deck.Hand, _stock, _deckPreviousColor, RefreshDeckHandUI);
            RefreshDeckHandUI();
            RefreshSkillCooldownUI();
        }

        string ResolveDeckCard(DeckHandSlot slot, DeckCardDefinition def)
        {
            var scaled = slot.ScaledValue(def);
            string msg;
            switch (def.Type)
            {
                case DeckCardType.Buff:
                    _deckAttackBuff += scaled;
                    msg = $"✨ {def.Name}（バフ +{scaled}）";
                    break;
                case DeckCardType.Shield:
                    _deck.AddShield(scaled);
                    msg = $"✨ {def.Name}（シールド +{scaled}）";
                    break;
                case DeckCardType.Attack:
                {
                    var dmg = Mathf.Max(1, scaled + _deckAttackBuff);
                    _deckAttackBuff = 0;
                    ApplyDamageToEnemy(dmg, null, true, false);
                    msg = $"✨ {def.Name}！ {dmg}ダメ";
                    break;
                }
                default:
                    msg = $"✨ {def.Name}";
                    break;
            }

            _deck.RemoveSlot(slot, def.Consumable);
            _deckPreviousColor = def.Colors[^1];
            return msg;
        }

        void RefreshDeckHandUI()
        {
            if (!DeckMode || _deckHandUi == null || _deck == null)
                return;
            _deckHandUi.Refresh(_deck.Hand, _stock, _deckPreviousColor, _deck.DrawCount, _deck.DiscardCount,
                _deck.Shield, Mathf.RoundToInt(_playerHp), Mathf.RoundToInt(_maxPlayerHp),
                _madness?.CurrentMadness ?? 0, _madness?.MaxMadness ?? 0);
        }

        void PaintDeckWordGuide(WordBoxUI box, WordSlotState slot)
        {
            var predictions = DeckCardResolver.PredictWord(slot.Active ? _deck?.Hand : null, _stock, slot.Color);
            box.DeckGuide.Paint(predictions);
            if (slot.Active && predictions.Count == 0)
            {
                box.Frame.color = DimWordColor(box.Frame.color);
                box.Fill.color = DimWordColor(box.Fill.color);
                box.WordText.color = DimWordColor(box.WordText.color);
            }
            box.RubyText.alpha = slot.Active && predictions.Count == 0 ? 0.7f : 1f;
        }

        static Color DimWordColor(Color color) => new(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, color.a);

        void RefreshWordGuideHighlights()
        {
            if (_typing == null || _ui == null || _deckHandUi == null)
                return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            var mask = 0;
            for (var i = 0; i < _ui.WordBoxes.Length; i++)
            {
                var box = _ui.WordBoxes[i];
                var predictions = box?.DeckGuide?.Predictions;
                if (predictions == null || predictions.Count == 0)
                    continue;
                var slot = _typing.Slots[i];
                var hovered = mouse != null && RectTransformUtility.RectangleContainsScreenPoint(box.FrameRoot, pointer);
                if (!slot.Active || (slot.Progress == 0 && !hovered))
                    continue;
                foreach (var prediction in predictions)
                    mask |= 1 << prediction.HandIndex;
            }
            _deckHandUi.SetWordGuideHighlights(mask);
        }
    }
}