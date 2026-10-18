using TypingRoguelike.Run;
using UnityEngine;

namespace TypingRoguelike.Data
{
    [CreateAssetMenu(fileName = "BoardPreset", menuName = "TypingRoguelike/Board Preset")]
    public sealed class BoardPreset : ScriptableObject
    {
        public string presetId = "balance";
        public string displayName = "バランス";

        [Tooltip("1列目。9文字。B戦闘 R回復 S店 T宝 Eイベント X罠。空いた文字は罠。")]
        public string column1 = "BBBRRSTEE";
        [Tooltip("2列目。9文字。")]
        public string column2 = "BBBRRSTEE";
        [Tooltip("3列目。9文字。")]
        public string column3 = "BBBRRSTEE";

        public BoardGrid ToGrid() => BoardGrid.Parse(column1, column2, column3);
    }
}
