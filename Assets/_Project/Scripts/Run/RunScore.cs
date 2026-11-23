using UnityEngine;

namespace TypingRoguelike.Run
{
    /// <summary>
    /// 帰還と死亡の採点。一層の配点は文字40、雑魚25、マス15、ボス20。
    /// 深い層はその層番号を掛ける。効果の解放は空。
    /// </summary>
    public static class RunScore
    {
        public const int XpCap = 10000;
        const int TypedBudget = 40;
        const int TypedUnit = 100;
        const int TrashPerKill = 25;
        const int SquareBudget = 15;
        const int SquaresPerFloor = 4;
        const int BossPerKill = 20;

        public readonly struct Breakdown
        {
            public readonly int TypedChars;
            public readonly int TypedPoints;
            public readonly int TrashKills;
            public readonly int TrashPoints;
            public readonly int Squares;
            public readonly int SquarePoints;
            public readonly int BossKills;
            public readonly int BossPoints;
            public readonly int Subtotal;

            public Breakdown(
                int typedChars, int typedPoints, int trashKills, int trashPoints,
                int squares, int squarePoints, int bossKills, int bossPoints)
            {
                TypedChars = typedChars;
                TypedPoints = typedPoints;
                TrashKills = trashKills;
                TrashPoints = trashPoints;
                Squares = squares;
                SquarePoints = squarePoints;
                BossKills = bossKills;
                BossPoints = bossPoints;
                Subtotal = typedPoints + trashPoints + squarePoints + bossPoints;
            }
        }

        public static void AddTyped(MetaProgressData data, int floor, int count)
        {
            if (data == null || count <= 0)
                return;
            data.runTypedChars += count;
            data.runTypedRaw += count * Mathf.Max(1, floor);
        }

        public static void AddTrash(MetaProgressData data, int floor)
        {
            if (data == null)
                return;
            data.runTrashKills += 1;
            data.runTrashPoints += TrashPerKill * Mathf.Max(1, floor);
        }

        public static void AddSquare(MetaProgressData data, int floor)
        {
            if (data == null)
                return;
            data.runSquares += 1;
            data.runSquareRaw += Mathf.Max(1, floor);
        }

        public static void AddBoss(MetaProgressData data, int floor)
        {
            if (data == null)
                return;
            data.runBossKills += 1;
            data.runBossPoints += BossPerKill * Mathf.Max(1, floor);
        }

        public static Breakdown Read(MetaProgressData data)
        {
            if (data == null)
                return default;
            return new Breakdown(
                data.runTypedChars,
                data.runTypedRaw * TypedBudget / TypedUnit,
                data.runTrashKills,
                data.runTrashPoints,
                data.runSquares,
                data.runSquareRaw * SquareBudget / SquaresPerFloor,
                data.runBossKills,
                data.runBossPoints);
        }

        /// <summary>採点を経験値へ足し、この区間の記録を空にする。死亡は50%。10,000 を超えても切らない。</summary>
        public static int Bank(MetaProgressData data, bool death)
        {
            var score = Read(data);
            var kept = death ? score.Subtotal / 2 : score.Subtotal;
            if (data != null)
            {
                var sum = (long)data.playerXp + kept;
                data.playerXp = sum > int.MaxValue ? int.MaxValue : (int)sum;
                ClearOuting(data);
            }

            return kept;
        }

        public static void ClearOuting(MetaProgressData data)
        {
            if (data == null)
                return;
            data.runTypedChars = 0;
            data.runTypedRaw = 0;
            data.runTrashKills = 0;
            data.runTrashPoints = 0;
            data.runSquares = 0;
            data.runSquareRaw = 0;
            data.runBossKills = 0;
            data.runBossPoints = 0;
        }
    }
}
