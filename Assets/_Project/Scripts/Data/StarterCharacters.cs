using System;

namespace TypingRoguelike.Data
{
    /// <summary>
    /// 初期3人の内部ID。IDはセーブと絵の対応に使うので変えない。
    /// 画面に出す名前を差し替えるときは DisplayName だけを変える。
    /// </summary>
    public static class StarterCharacters
    {
        public const string Chara01 = "chara01";
        public const string Chara02 = "chara02";
        public const string Chara03 = "chara03";

        readonly struct Entry
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string ArtKey;
            public readonly string PortraitKey;

            public Entry(string id, string displayName, string artKey, string portraitKey)
            {
                Id = id;
                DisplayName = displayName;
                ArtKey = artKey;
                PortraitKey = portraitKey;
            }
        }

        static readonly Entry[] Roster =
        {
            new(Chara01, "chara01", "アリス", "akane"),
            new(Chara02, "chara02", "エレン", "momo"),
            new(Chara03, "chara03", "ルナ", "nagi"),
        };

        public static bool IsStarter(string token)
        {
            var id = CanonicalId(token);
            return id == Chara01 || id == Chara02 || id == Chara03;
        }

        public static bool Same(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            if (IsStarter(a) || IsStarter(b))
                return CanonicalId(a) == CanonicalId(b);
            return a == b;
        }

        public static string CanonicalId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            var key = token.Trim();
            foreach (var entry in Roster)
            {
                if (entry.Id == key || entry.ArtKey == key || entry.PortraitKey == key)
                    return entry.Id;
            }

            switch (key)
            {
                case "アカネ":
                case "ALICE":
                case "Alice":
                    return Chara01;
                case "モモ":
                case "ELLEN":
                case "Ellen":
                    return Chara02;
                case "ナギ":
                case "LUNA":
                case "Luna":
                    return Chara03;
            }

            return key;
        }

        public static string Shown(string token)
        {
            if (string.IsNullOrEmpty(token))
                return "";
            if (!IsStarter(token))
                return token;
            var id = CanonicalId(token);
            foreach (var entry in Roster)
            {
                if (entry.Id == id)
                    return entry.DisplayName;
            }

            return token;
        }

        public static string ArtKey(string token)
        {
            if (!IsStarter(token))
                return token ?? "";
            var id = CanonicalId(token);
            foreach (var entry in Roster)
            {
                if (entry.Id == id)
                    return entry.ArtKey;
            }

            return token;
        }

        public static string PortraitKey(string token)
        {
            if (!IsStarter(token))
                return token ?? "";
            var id = CanonicalId(token);
            foreach (var entry in Roster)
            {
                if (entry.Id == id)
                    return entry.PortraitKey;
            }

            return token;
        }
    }
}
