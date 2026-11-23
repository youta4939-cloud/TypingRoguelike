using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TypingRoguelike.Run
{
    /// <summary>Serialized UI references. Runtime binding never rewrites the authored layout.</summary>
    public sealed class StoryEventPrefabView : MonoBehaviour
    {
        public Text badge, title, coins, materials, hp, madness;
        public TMP_Text body, aside;
        public Choice[] choices;
        public Member[] party;

        [Serializable] public sealed class Choice
        {
            public Button button;
            public Text label;
            public EventChoiceHover hover;
            public Image[] border;
            public RectTransform effects;
            public GameObject rowTemplate, gapTemplate;
            [Tooltip("勝利後の効果行の左余白")]
            public int rewardIndent = 24;
            public Supporter[] supporters;
        }
        [Serializable] public sealed class Member
        {
            public GameObject root;
            public Image portrait;
            public Text name, affinity, delta;
        }
        [Serializable] public sealed class Supporter
        {
            public Image portrait;
            public Text name;
            public TMP_Text speech;
            public GameObject[] visuals;
        }

        // Used only to create/migrate the prefab. References survive renaming/reparenting afterwards.
        public void CaptureReferences()
        {
            Text T(string path) => transform.Find(path).GetComponent<Text>();
            badge = T("Badge"); title = T("EventTitle"); coins = T("Coins"); materials = T("Materials");
            hp = T("StatusBar/Hp"); madness = T("StatusBar/Madness");
            body = transform.Find("BodyPlate/Narrative/Viewport/Content/Body").GetComponent<TMP_Text>();
            aside = transform.Find("BodyPlate/Narrative/Viewport/Content/Aside").GetComponent<TMP_Text>();
            party = new Member[3]; choices = new Choice[2];
            for (var i = 0; i < 3; i++)
            {
                var root = transform.Find("StatusBar/AffinitySlot" + i);
                party[i] = new Member { root = root.gameObject,
                    portrait = root.Find("Face/Frame/Portrait").GetComponent<Image>(),
                    name = root.Find("Who" + i).GetComponent<Text>(),
                    affinity = root.Find("Affinity" + i).GetComponent<Text>(),
                    delta = root.Find("AffinityDelta" + i).GetComponent<Text>() };
            }
            for (var i = 0; i < 2; i++)
            {
                var root = transform.Find("Choice" + i);
                var content = root.Find("ChoiceDetails/Viewport/Content") as RectTransform;
                var row = content.GetChild(0).gameObject;
                var gap = content.GetChild(1).gameObject;
                var rowView = row.AddComponent<StoryEventEffectRow>();
                rowView.effect = row.transform.Find("Effect").GetComponent<Text>();
                rowView.chance = row.transform.Find("Chance").GetComponent<Text>();
                rowView.layout = row.GetComponent<HorizontalLayoutGroup>();
                row.name = "EffectRowTemplate"; gap.name = "OutcomeGapTemplate";
                row.SetActive(false); gap.SetActive(false);
                var choice = new Choice { button = root.GetComponent<Button>(),
                    label = root.Find("Label").GetComponent<Text>(), hover = root.GetComponent<EventChoiceHover>(),
                    border = root.Find("Border").GetComponentsInChildren<Image>(), effects = content,
                    rowTemplate = row, gapTemplate = gap, supporters = new Supporter[3] };
                for (var j = 0; j < 3; j++)
                {
                    var slot = root.Find("SupporterLane/SupporterSlot" + j);
                    choice.supporters[j] = new Supporter {
                        portrait = slot.Find("Face/Frame/Portrait").GetComponent<Image>(),
                        name = slot.Find("SupporterName").GetComponent<Text>(),
                        speech = slot.Find("SupporterSpeech/Viewport/Content/SupporterLine").GetComponent<TMP_Text>(),
                        visuals = slot.Cast<Transform>().Select(t => t.gameObject).ToArray() };
                }
                choices[i] = choice;
            }
        }
    }
}
