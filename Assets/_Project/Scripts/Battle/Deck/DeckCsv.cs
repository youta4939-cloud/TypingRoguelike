using System.Collections.Generic;
using System.Text;

namespace TypingRoguelike.Battle.Deck
{
    static class DeckCsv
    {
        public static List<string[]> ReadRows(UnityEngine.TextAsset asset)
        {
            var rows = new List<string[]>();
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return rows;
            var text = asset.text;
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            var line = new StringBuilder();
            var fields = new List<string>();
            var inQuotes = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            line.Append('"');
                            i++;
                        }
                        else
                            inQuotes = false;
                    }
                    else
                        line.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (c == ',')
                {
                    fields.Add(line.ToString());
                    line.Clear();
                    continue;
                }

                if (c == '\n')
                {
                    fields.Add(line.ToString());
                    line.Clear();
                    if (fields.Count > 0)
                        rows.Add(fields.ToArray());
                    fields.Clear();
                    continue;
                }

                if (c != '\r')
                    line.Append(c);
            }

            if (line.Length > 0 || fields.Count > 0)
            {
                fields.Add(line.ToString());
                rows.Add(fields.ToArray());
            }

            return rows;
        }
    }
}