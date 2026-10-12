using System;

namespace TypingRoguelike.Run
{
    /// <summary>
    /// イベント JSON の lines[].char を、ゲーム内の characterName へ変換する唯一の表。
    /// キャラ定義に Akane / Momo / Nagi という ID は無く、表示名は アリス / エレン / ルナ。
    /// 口調で対応させている。茜（荒い口調）=アリス、桃（明るい口調）=ルナ、凪（静かな口調）=エレン。
    /// </summary>
    public static class EventCharacterIds
    {
        static readonly (string JsonId, string CharacterName)[] Table =
        {
            ("Akane", "アリス"),
            ("Momo", "ルナ"),
            ("Nagi", "エレン"),
        };

        public static string ToCharacterName(string jsonId)
        {
            if (string.IsNullOrEmpty(jsonId))
                return string.Empty;

            foreach (var row in Table)
            {
                if (string.Equals(row.JsonId, jsonId, StringComparison.OrdinalIgnoreCase))
                    return row.CharacterName;
            }

            return jsonId;
        }
    }
}
