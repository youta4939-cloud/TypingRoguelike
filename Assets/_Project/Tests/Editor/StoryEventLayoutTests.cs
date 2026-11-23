using System;
using System.IO;
using System.Linq;
using TMPro;
using NUnit.Framework;
using TypingRoguelike.Data;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using Object = UnityEngine.Object;

namespace TypingRoguelike.Tests
{
    public sealed class StoryEventLayoutTests
    {
        static TestRunnerApi _runner;

        [MenuItem("Tools/TypingRoguelike/Check Event Layout")]
        public static void RunLayoutChecks()
        {
            _runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            _runner.RegisterCallbacks(new LayoutResults());
            _runner.Execute(new ExecutionSettings(new Filter
                { testMode = TestMode.EditMode, testNames = new[] { typeof(StoryEventLayoutTests).FullName } }));
        }

        [MenuItem("Tools/TypingRoguelike/Preview Event Layout Without Scene Switch")]
        public static void PreviewWithoutSceneSwitch()
        {
            // The standard runner asks to save/reload scenes. These synchronous
            // assertions create and clean up only their own UI and camera instead.
            if (Application.isPlaying || GameObject.Find("LayerPathCanvas") != null)
                throw new InvalidOperationException("Stop play mode before previewing the event layout.");
            var existingSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Select(e => e.GetInstanceID()).ToArray();
            var fixture = new StoryEventLayoutTests();
            var checks = new Action[]
            {
                () => fixture.MirrorEventRemainsReadableAtDifferentAspectRatios(1920, 1080, true),
                () => fixture.MirrorEventRemainsReadableAtDifferentAspectRatios(1280, 720, true),
                () => fixture.MirrorEventRemainsReadableAtDifferentAspectRatios(1280, 1024, true),
                () => fixture.MirrorEventRemainsReadableAtDifferentAspectRatios(2560, 1080, true),
                fixture.PrefabEditsSurviveDataBindingAndEffectCloning,
                fixture.MistCoversTheScreenBeforeReplacingItAndRestoresInput,
                fixture.InterruptedMistDoesNotLeaveAnOverlayOrPreviousScreen,
                fixture.EveryImplementedEventFitsTheNarrativeArea,
                fixture.ReportedEventAndSupporterPunctuationRemainReadable,
                fixture.LongNarrativeAndEffectsCanScrollToTheirFinalLines,
                fixture.LongResultTextStaysAboveTheContinueButton,
                fixture.AChoiceCanOnlyResolveOnce,
                fixture.SupporterLaneKeepsThreeSlotsAndUsesTheStatusPortraits,
                fixture.HoverAndKeyboardPreviewUseSeparateDeltaSlotsAndNeverChangeCurrentAffinity,
                fixture.EffectDeltasUseMeaningRatherThanSignAndKeepMadnessDistinct
            };
            var passed = 0;
            var failures = new System.Collections.Generic.List<string>();
            foreach (var check in checks)
            {
                try { check(); passed++; }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { fixture.Cleanup(); }
            }
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (!existingSystems.Contains(system.GetInstanceID())) Object.DestroyImmediate(system.gameObject);
            var summary = $"Event layout assertions: {passed}/{checks.Length} passed\n" + string.Join("\n", failures);
            var folder = Path.Combine(Application.dataPath, "../Logs/StoryEventLayout");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "direct-checks.txt"), summary);
            if (failures.Count > 0) throw new Exception(summary);
            Debug.Log(summary);
        }

        sealed class LayoutResults : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                var folder = Path.Combine(Application.dataPath, "../Logs/StoryEventLayout");
                Directory.CreateDirectory(folder);
                TestRunnerApi.SaveResultToFile(result, Path.Combine(folder, "results.xml"));
                Debug.Log($"Event layout checks: {result.ResultState}, passed {result.PassCount}, failed {result.FailCount}");
            }
        }

        GameObject _root;
        Camera _camera;
        RenderTexture _target;

        [TearDown]
        public void Cleanup()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
            if (_target != null) { _target.Release(); Object.DestroyImmediate(_target); }
            LayerPathUI.SuppressPrefab = false;
        }

        StoryEventView View(string id = "mirror_shadow")
        {
            var ev = EventCatalog.Find(id);
            Assert.NotNull(ev);
            var chance = EventText.NeedsChanceLabel(ev.options[0], ev.options[1]);
            return new StoryEventView
            {
                Badge = "第6層 3rd ・ 出来事", Title = ev.title,
                Body = EventText.Fill(ev.body, "アリス"), Category = ev.category,
                Coins = 191, Hp = 445, HpMax = 1000, Madness = 371, MadnessMax = 1000,
                ShowChance = chance,
                Choices = ev.options.Select(o => new StoryEventChoiceView
                {
                    Label = EventText.Fill(o.label, "アリス"),
                    Effects = EventText.DescribeOptionLines(o, null, "アリス", chance)
                }).ToArray(),
                Party = new[]
                {
                    new TrustGaugeView { CharacterId = "chara01", Name = "アリス", Trust = 6 },
                    new TrustGaugeView { CharacterId = "chara02", Name = "エレン", Trust = 11 },
                    new TrustGaugeView { CharacterId = "chara03", Name = "ルナ", Trust = 6 }
                }
            };
        }

        void Build(StoryEventView view, int width, int height, bool prefab = true, Action<int> onPick = null, StoryEventPrefabView layout = null)
        {
            LayerPathUI.SuppressPrefab = !prefab;
            new LayerPathUI().ShowStoryEvent(view, onPick, layout);
            _root = GameObject.Find("LayerPathCanvas");
            Assert.NotNull(_root);
            _camera = new GameObject("StoryLayoutTestCamera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.cullingMask = 1 << 5;
            _camera.transform.position = new Vector3(0, 0, -10);
            _target = new RenderTexture(width, height, 24);
            _target.Create();
            _camera.targetTexture = _target;
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            foreach (var t in _root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
            Layout();
        }

        void Layout()
        {
            for (var i = 0; i < 4; i++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_root.GetComponent<RectTransform>());
                foreach (var scroll in _root.GetComponentsInChildren<ScrollRect>())
                    scroll.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
            }
        }

        void AssertReadable()
        {
            foreach (var text in _root.GetComponentsInChildren<Text>())
            {
                Assert.NotNull(text.font, text.name + " font");
                if (text.name != "Body" && text.name != "Effect" && text.name != "Trust" && text.name != "Aside" && text.name != "ModalBody") continue;
                Assert.Greater(text.rectTransform.rect.width, 0, text.name);
                Assert.That(text.rectTransform.rect.height + 2f, Is.GreaterThanOrEqualTo(text.preferredHeight),
                    text.name + " clips its own text: " + text.text);
            }
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>())
            {
                Assert.NotNull(text.font, text.name + " font");
                Assert.GreaterOrEqual(text.rectTransform.rect.height + 2f, text.preferredHeight, text.name);
                text.ForceMeshUpdate();
                foreach (var line in text.textInfo.lineInfo.Take(text.textInfo.lineCount))
                {
                    if (line.characterCount == 0) continue;
                    var c = text.textInfo.characterInfo[line.firstCharacterIndex].character;
                    Assert.That("。、！？）」』】".Contains(c.ToString()), Is.False, text.name + " starts with " + c);
                }
            }
            foreach (var content in _root.GetComponentsInChildren<VerticalLayoutGroup>())
            {
                var children = content.GetComponentsInChildren<Text>().Where(t => t.transform.parent == content.transform).ToArray();
                for (var i = 1; i < children.Length; i++)
                    Assert.LessOrEqual(children[i].rectTransform.anchoredPosition.y, children[i - 1].rectTransform.anchoredPosition.y);
            }
        }

        [TestCase(1920, 1080, true)]
        [TestCase(1280, 720, true)]
        [TestCase(1280, 1024, true)]
        [TestCase(2560, 1080, true)]
        public void MirrorEventRemainsReadableAtDifferentAspectRatios(int width, int height, bool prefab)
        {
            var view = View();
            view.Choices[0].Marks = view.Party.Select(p => new StoryEventTrustMark
                { CharacterId = p.CharacterId, Name = p.Name, Supporter = true, Delta = 5,
                    Line = "今なら、止められる。" }).ToArray();
            Build(view, width, height, prefab);
            AssertReadable();
            Assert.IsTrue(_root.GetComponentsInChildren<Image>().Single(i => i.name == "Picture").preserveAspect);
            var card = _root.transform.Find("StoryEvent/Choice0").gameObject;
            ExecuteEvents.Execute(card, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            SavePreview($"story-{width}x{height}.png");
        }

        [Test]
        public void SupporterLaneKeepsThreeSlotsAndUsesTheStatusPortraits()
        {
            var view = View();
            view.Choices[0].Marks = view.Party.Select(p => new StoryEventTrustMark
                { CharacterId = p.CharacterId, Name = p.Name, Supporter = true, Delta = 5, Line = "こっちで。" }).ToArray();
            // A non-speaking affinity effect still previews, but never becomes a supporter.
            view.Choices[1].Marks = new[] { new StoryEventTrustMark
                { CharacterId = "chara01", Name = "アリス", Delta = -2, Supporter = false } };
            Build(view, 1920, 1080);
            var a = _root.transform.Find("StoryEvent/Choice0/SupporterLane") as RectTransform;
            var b = _root.transform.Find("StoryEvent/Choice1/SupporterLane") as RectTransform;
            Assert.AreEqual(3, a.childCount);
            Assert.AreEqual(3, b.childCount);
            Assert.AreEqual(a.rect.size, b.rect.size);
            Assert.AreEqual(0, b.GetComponentsInChildren<Image>().Length);
            for (var i = 0; i < 3; i++)
            {
                var face = a.Find($"SupporterSlot{i}/Face/Frame/Portrait").GetComponent<Image>();
                var status = _root.transform.Find($"StoryEvent/StatusBar/AffinitySlot{i}/Face/Frame/Portrait").GetComponent<Image>();
                Assert.NotNull(face.sprite);
                Assert.AreSame(status.sprite, face.sprite);
            }
            Assert.IsFalse(_root.GetComponentsInChildren<Text>().Any(t => t.text.Contains("仲間の推し")));
        }

        [Test]
        public void HoverAndKeyboardPreviewUseSeparateDeltaSlotsAndNeverChangeCurrentAffinity()
        {
            var view = View();
            view.Choices[0].Marks = new[] { new StoryEventTrustMark { CharacterId = "chara01", Name = "別の表示名", Delta = 5 } };
            view.Choices[1].Marks = new[] { new StoryEventTrustMark { CharacterId = "chara02", Name = "エレン", Delta = -2 } };
            Build(view, 1920, 1080);
            var status = _root.transform.Find("StoryEvent/StatusBar");
            var before = status.Find("AffinitySlot0/Affinity0").GetComponent<Text>().text;
            var delta = status.Find("AffinitySlot0/AffinityDelta0").GetComponent<Text>();
            var a = _root.transform.Find("StoryEvent/Choice0").gameObject;
            var b = _root.transform.Find("StoryEvent/Choice1").gameObject;
            var fillBefore = a.GetComponent<Image>().color;
            var border = a.transform.Find("Border/Edge0").GetComponent<Image>();
            var borderBefore = border.color;
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(a, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(delta.text, Does.Contain("+5"));
            Assert.AreNotEqual(borderBefore, border.color);
            Assert.AreEqual(fillBefore, a.GetComponent<Image>().color, "hover must not tint the whole panel");
            Assert.AreEqual(before, status.Find("AffinitySlot0/Affinity0").GetComponent<Text>().text);
            ExecuteEvents.Execute(a, pointer, ExecuteEvents.pointerExitHandler);
            Assert.AreEqual("", delta.text);
            Assert.AreEqual(borderBefore, border.color);
            ExecuteEvents.Execute(b, new BaseEventData(EventSystem.current), ExecuteEvents.selectHandler);
            Assert.That(status.Find("AffinitySlot1/AffinityDelta1").GetComponent<Text>().text, Does.Contain("-2"));
            ExecuteEvents.Execute(b, new BaseEventData(EventSystem.current), ExecuteEvents.deselectHandler);
            Assert.AreEqual("", status.Find("AffinitySlot1/AffinityDelta1").GetComponent<Text>().text);
        }

        [Test]
        public void EffectDeltasUseMeaningRatherThanSignAndKeepMadnessDistinct()
        {
            var enemy = EventText.DescribeOptionLines(new EventOption { outcomes = new[] { new EventOutcome
                { effects = new[] { new EventEffect { type = "battle", kind = "normal" },
                    new EventEffect { type = "battle_enemy_hp_percent", value = -15 },
                    new EventEffect { type = "battle_opening_seconds", value = -2 },
                    new EventEffect { type = "madness", value = 50 } } } } }, null, "仲間", false);
            Assert.That(EventText.DisplayLine(enemy.Single(e => e.Kind == "battle_enemy_hp_percent")),
                Does.Contain($"<color={EventText.BenefitColor}>-15%</color>"));
            Assert.That(EventText.DisplayLine(enemy.Single(e => e.Kind == "battle_opening_seconds")),
                Does.Contain($"<color={EventText.RiskColor}>-2秒</color>"));
            Assert.That(EventText.DisplayLine(enemy.Single(e => e.Kind == "madness")),
                Does.Contain($"<color={EventText.MadnessColor}>+50</color>"));
        }

        [Test]
        public void PrefabEditsSurviveDataBindingAndEffectCloning()
        {
            var prefab = Resources.Load<GameObject>("UI/StoryEvent");
            Assert.NotNull(prefab.GetComponent<StoryEventPrefabView>());
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), t.name);
            var source = Object.Instantiate(prefab);
            try
            {
                var authored = source.GetComponent<StoryEventPrefabView>();
                authored.title.rectTransform.anchoredPosition += new Vector2(23, -11);
                authored.title.fontSize = authored.title.resizeTextMaxSize = 49;
                authored.title.color = Color.cyan;
                authored.body.name = "RenamedBody";
                var row = authored.choices[0].rowTemplate.GetComponent<StoryEventEffectRow>();
                row.effect.fontSize = 29;
                row.layout.spacing = 19;
                authored.choices[0].button.GetComponent<RectTransform>().anchorMin = new Vector2(0.07f, 0.14f);
                Build(View(), 1920, 1080, layout: authored);
                var actual = _root.GetComponentInChildren<StoryEventPrefabView>();
                Assert.AreEqual(authored.title.rectTransform.anchoredPosition, actual.title.rectTransform.anchoredPosition);
                Assert.AreEqual(49, actual.title.fontSize);
                Assert.AreEqual(Color.cyan, actual.title.color);
                Assert.AreEqual(View().Body, actual.body.text);
                Assert.AreEqual("RenamedBody", actual.body.name);
                Assert.AreEqual(new Vector2(0.07f, 0.14f), actual.choices[0].button.GetComponent<RectTransform>().anchorMin);
                var clones = actual.choices[0].effects.GetComponentsInChildren<StoryEventEffectRow>();
                Assert.IsNotEmpty(clones);
                Assert.IsTrue(clones.All(r => r.effect.fontSize == 29 && r.layout.spacing == 19));
                foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null) Assert.IsTrue(EditorUtility.IsPersistent(image.sprite), image.name);
                foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(true))
                    Assert.IsTrue(EditorUtility.IsPersistent(tmp.font), tmp.name);
            }
            finally { Object.DestroyImmediate(source); }
        }

        [Test]
        public void MistCoversTheScreenBeforeReplacingItAndRestoresInput()
        {
            Build(View(), 1280, 720);
            var panel = _root.transform.Find("StoryEvent").gameObject;
            var old = new GameObject("PreviousRoute", typeof(RectTransform));
            old.transform.SetParent(_root.transform, false);
            var effect = panel.AddComponent<StoryMistTransition>();
            effect.Begin(old, _root.transform);
            var group = panel.GetComponent<CanvasGroup>();
            Assert.IsFalse(group.interactable);
            Assert.AreEqual(0f, group.alpha);
            var shader = Resources.Load<Shader>("Run/StoryMist");
            Assert.NotNull(shader);
            Assert.IsTrue(shader.isSupported);
            effect.Advance(StoryMistTransition.Duration * 0.25f);
            SavePreview("story-mist-forming.png");
            effect.Advance(StoryMistTransition.Duration * 0.21f);
            Assert.IsTrue(old != null, "keep route until full cover");
            Assert.That(_root.transform.Find("EventMist").GetComponent<RawImage>().color.a, Is.EqualTo(1f).Within(0.001f));
            SavePreview("story-mist-covered.png");
            effect.Advance(StoryMistTransition.Duration * 0.05f);
            Assert.IsTrue(old == null);
            Assert.AreEqual(1f, group.alpha);
            Assert.IsFalse(group.interactable, "revealed choices must stay locked until mist clears");
            effect.Advance(StoryMistTransition.Duration);
            Assert.IsFalse(effect.IsRunning);
            Assert.IsTrue(group.interactable && group.blocksRaycasts);
            Assert.IsNull(_root.transform.Find("EventMist"));
        }

        [Test]
        public void InterruptedMistDoesNotLeaveAnOverlayOrPreviousScreen()
        {
            Build(View(), 1280, 720);
            var panel = _root.transform.Find("StoryEvent").gameObject;
            var old = new GameObject("PreviousRoute", typeof(RectTransform));
            old.transform.SetParent(_root.transform, false);
            var effect = panel.AddComponent<StoryMistTransition>();
            effect.Begin(old, _root.transform);
            // Simulate the play-mode disable callback before deactivating in edit mode;
            // SendMessage is not delivered to inactive behaviours.
            effect.SendMessage("OnDisable");
            panel.SetActive(false);
            Assert.IsFalse(effect.IsRunning);
            Assert.IsTrue(old == null);
            Assert.IsNull(_root.transform.Find("EventMist"));
        }

        [Test]
        public void EveryImplementedEventFitsTheNarrativeArea()
        {
            var data = JsonUtility.FromJson<EventFile>(Resources.Load<TextAsset>("Events/events").text);
            foreach (var ev in data.events)
            {
                var view = View(ev.id);
                view.Aside = "アリス（遠慮）：声の主を確かめたい。";
                Build(view, 1280, 720);
                AssertReadable();
                var narrative = _root.GetComponentsInChildren<ScrollRect>().Single(s => s.name == "Narrative");
                Assert.LessOrEqual(narrative.content.rect.height, narrative.viewport.rect.height + 1f,
                    ev.id + " should fit without scrolling");
                Cleanup();
            }
        }

        [Test]
        public void ReportedEventAndSupporterPunctuationRemainReadable()
        {
            var view = View("lower_floor_call");
            view.Aside = "アリス（遠慮）：声の主を確かめたい。";
            view.Choices[1].Marks = new[] { new StoryEventTrustMark {
                CharacterId = "chara02", Name = "chara02", Supporter = true, Line = "待った方がいい。" } };
            Build(view, 1660, 940);
            AssertReadable();
            var narrative = _root.GetComponentsInChildren<ScrollRect>().Single(s => s.name == "Narrative");
            Assert.LessOrEqual(narrative.content.rect.height, narrative.viewport.rect.height + 1f);
            Assert.AreEqual(1, _root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "SupporterLine").textInfo.lineCount,
                "short supporter speech should fit on one line");
            SavePreview("story-lower-floor-call.png");
        }

        [Test]
        public void LongNarrativeAndEffectsCanScrollToTheirFinalLines()
        {
            var view = View();
            view.Body = string.Concat(Enumerable.Repeat(view.Body + "\n", 18)) + "本文の最後。";
            view.Aside = "仲間の遠慮の言葉も最後まで読める。";
            view.Choices[0].Effects = Enumerable.Range(0, 24).Select(i => new StoryEventEffectLine
                { Text = "長い効果の説明を折り返して読めることを確認する行 " + i, Kind = "madness" }).ToArray();
            Build(view, 1280, 720);
            AssertReadable();
            foreach (var scroll in _root.GetComponentsInChildren<ScrollRect>().Where(s => s.name == "Narrative" || s.transform.IsChildOf(_root.transform.Find("StoryEvent/Choice0"))))
            {
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                scroll.verticalNormalizedPosition = 0;
                Layout();
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(0.01f));
                var last = scroll.content.GetChild(scroll.content.childCount - 1) as RectTransform;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, last);
                Assert.GreaterOrEqual(bounds.min.y + 2, scroll.viewport.rect.yMin, scroll.name + " last line inaccessible");
            }
            SavePreview("story-long-content.png");
        }

        [Test]
        public void LongResultTextStaysAboveTheContinueButton()
        {
            Build(View(), 1280, 720);
            var ui = new LayerPathUI();
            ui.ShowModal("出来事の結果", string.Concat(Enumerable.Repeat("傷は閉じたが、耳の奥にはまだ紙の音が残っている。\n", 20)),
                new[] { ("次へ", (Action)null) });
            // The modal belongs to this second presenter. Clean it up independently.
            try
            {
                var modal = Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).Single(s => s.name == "ModalReading");
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(modal.transform.root as RectTransform);
                Canvas.ForceUpdateCanvases();
                Assert.Greater(modal.content.rect.height, modal.viewport.rect.height);
                var body = modal.content.GetComponentInChildren<Text>();
                Assert.GreaterOrEqual(body.rectTransform.rect.height + 2f, body.preferredHeight);
                var button = modal.transform.parent.GetComponentInChildren<Button>();
                var viewportCorners = new Vector3[4];
                var buttonCorners = new Vector3[4];
                modal.viewport.GetWorldCorners(viewportCorners);
                ((RectTransform)button.transform).GetWorldCorners(buttonCorners);
                // Compare visible rectangles, excluding the intentionally overflowing
                // content descendants that RectTransformBounds would include.
                Assert.Greater(viewportCorners.Min(v => v.y), buttonCorners.Max(v => v.y));
            }
            finally
            {
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (canvas.name == "LayerPathCanvas" && canvas.gameObject != _root)
                        Object.DestroyImmediate(canvas.gameObject);
            }
        }

        [Test]
        public void AChoiceCanOnlyResolveOnce()
        {
            var calls = 0;
            Build(View(), 1920, 1080, onPick: _ => calls++);
            var buttons = _root.GetComponentsInChildren<Button>();
            buttons[0].onClick.Invoke();
            buttons[1].onClick.Invoke();
            Assert.AreEqual(1, calls);
            Assert.IsTrue(buttons.All(b => !b.interactable));
        }

        void SavePreview(string name)
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
                image.Apply();
                var folder = Path.Combine(Application.dataPath, "../Logs/StoryEventLayout");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
    }
}
