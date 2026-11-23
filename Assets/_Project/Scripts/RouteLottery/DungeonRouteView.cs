using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.RouteLottery
{
    /// <summary>
    /// 進行画面の枠。位置はプレハブ側を使う。
    /// カード列は Cards を動かす。幅と間隔は下の数値。文字と絵は再生時に入る。
    /// </summary>
    public sealed class DungeonRouteView : MonoBehaviour
    {
        public const string ResourcePath = "UI/DungeonRoute";

        public TMP_Text floorTitle;
        public TMP_Text coins;
        public TMP_Text hpValue;
        public TMP_Text hpMax;
        public TMP_Text madValue;
        public TMP_Text madMax;
        public Image hpFill;
        public Image madFill;
        public RectTransform cards;
        public RectTransform[] cardSlots = new RectTransform[4];
        public TMP_Text[] trackNumbers = new TMP_Text[4];
        public Image[] dots = new Image[4];
        public RectTransform trackLine;
        public Image[] portraits = new Image[3];
        public TMP_Text[] portraitNames = new TMP_Text[3];
        public TMP_Text[] portraitTrust = new TMP_Text[3];
        public Button menuButton;
        public GameObject menuIcon;
        public GameObject menuRoot;
        public Button[] menuItems = new Button[4];

        [Header("カード列。Cards の位置を動かしたうえで、幅と間隔を変える")]
        public float gap = 20f;
        public float currentWidth = 424f;
        public float currentHeight = 632f;
        public float normalWidth = 380f;
        public float normalHeight = 620f;
        public float normalDrop = 8f;

        public bool Ready =>
            floorTitle != null && cards != null && cardSlots != null && cardSlots.Length >= 4 && cardSlots[0] != null;

        public static DungeonRouteView Load() => Resources.Load<DungeonRouteView>(ResourcePath);
    }
}
