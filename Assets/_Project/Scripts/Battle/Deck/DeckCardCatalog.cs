using System.Collections.Generic;
using UnityEngine;

namespace TypingRoguelike.Battle.Deck
{
    public static class DeckCardCatalog
    {
        const string CardsPath = "Deck/cards";

        static Dictionary<string, DeckCardDefinition> _byId;

        public static IReadOnlyDictionary<string, DeckCardDefinition> All
        {
            get
            {
                LoadIfNeeded();
                return _byId;
            }
        }

        public static DeckCardDefinition Get(string id)
        {
            LoadIfNeeded();
            if (string.IsNullOrEmpty(id) || _byId == null || !_byId.TryGetValue(id, out var def))
                return null;
            return def;
        }

        public static List<string> BuildStarterDeckIds(IEnumerable<string> extraIds = null)
        {
            LoadIfNeeded();
            var list = new List<string>();
            var asset = Resources.Load<TextAsset>("Deck/starter_deck");
            var rows = DeckCsv.ReadRows(asset);
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length < 2)
                    continue;
                var id = row[0];
                var count = ParseInt(row[1]);
                if (Get(id) == null || count <= 0)
                    continue;
                for (var i = 0; i < count; i++)
                    list.Add(id);
            }

            if (extraIds != null)
            {
                foreach (var id in extraIds)
                {
                    if (Get(id) != null)
                        list.Add(id);
                }
            }

            return list;
        }

        static void LoadIfNeeded()
        {
            if (_byId != null)
                return;
            _byId = new Dictionary<string, DeckCardDefinition>();
            var asset = Resources.Load<TextAsset>(CardsPath);
            var rows = DeckCsv.ReadRows(asset);
            if (rows.Count < 2)
                return;
            var rewardColumn = System.Array.IndexOf(rows[0], "reward");
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Length < 9)
                    continue;
                var def = new DeckCardDefinition
                {
                    Id = row[0],
                    Name = row[1],
                    Colors = DeckColorParser.ParsePattern(row[2]),
                    Type = ParseType(row[3]),
                    Value = ParseInt(row[4]),
                    Consumable = row[5] == "1",
                    AddCardId = string.IsNullOrWhiteSpace(row[6]) ? null : row[6],
                    Rarity = row[7],
                    Text = row[8],
                    Reward = rewardColumn < 0 || rewardColumn >= row.Length || row[rewardColumn] != "0"
                };
                if (!string.IsNullOrEmpty(def.Id))
                    _byId[def.Id] = def;
            }
        }

        static DeckCardType ParseType(string token) => token?.ToLowerInvariant() switch
        {
            "attack" => DeckCardType.Attack,
            "shield" => DeckCardType.Shield,
            "buff" => DeckCardType.Buff,
            _ => DeckCardType.Attack
        };

        static int ParseInt(string token) =>
            int.TryParse(token, out var value) ? value : 0;
    }
}