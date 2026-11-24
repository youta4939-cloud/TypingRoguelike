using NUnit.Framework;
using TypingRoguelike.Run;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace TypingRoguelike.Tests
{
    public sealed class HubElevatorSelectionTests
    {
        [Test]
        public void LockedAndUnlabelledFloorsCannotBeSelected()
        {
            var state = new HubElevatorSelection(new[] { 1, 3, 6, -1, 0, 6 });
            Assert.IsFalse(state.Select(2));
            Assert.IsFalse(state.Select(0));
            Assert.IsNull(state.SelectedFloor);
            Assert.AreEqual(6, state.HighestFloor);
            Assert.IsTrue(state.Select(6));
            Assert.AreEqual(6, state.SelectedFloor);
        }

        [Test]
        public void ChoosingDoesNotDepartAndClosingDispatchesOnlyOnce()
        {
            var state = new HubElevatorSelection(new[] { 3, 6 });
            Assert.IsFalse(state.TryDispatch(out _));
            Assert.IsTrue(state.Select(3));
            Assert.IsFalse(state.Dispatched);
            Assert.IsTrue(state.Select(6));
            Assert.IsTrue(state.TryDispatch(out var floor));
            Assert.AreEqual(6, floor);
            Assert.IsFalse(state.TryDispatch(out _));
            Assert.IsFalse(state.Select(3));
            state.OpenDoors();
            Assert.IsTrue(state.Dispatched);
        }

        [Test]
        public void OpeningBeforeDepartureClearsDestination()
        {
            var state = new HubElevatorSelection(new[] { 18, 99 });
            state.Select(99);
            state.OpenDoors();
            Assert.IsNull(state.SelectedFloor);
            Assert.IsFalse(state.TryDispatch(out _));
            Assert.IsTrue(state.Select(18));
            Assert.IsTrue(state.TryDispatch(out var floor));
            Assert.AreEqual(18, floor);
        }

        [Test]
        public void EmptyUnlockListDoesNotInventAnElevatorDestination()
        {
            var state = new HubElevatorSelection(null);
            Assert.IsFalse(state.IsUnlocked(1));
            Assert.IsFalse(state.Select(1));
            Assert.IsFalse(state.TryDispatch(out _));
        }
    }

    public sealed class HubOfficeLayoutTests
    {
        GameObject _root;

        HubOfficeView Build(System.Action repair = null)
        {
            _root = new GameObject("HubOfficeTest", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var view = _root.AddComponent<HubOfficeView>();
            view.Build(new MetaProgressData { towerRunActive = true, towerRunFloor = 6 },
                () => { }, () => { }, new[] { 3, 6, 99 }, _ => { }, () => { }, () => { }, repair);
            return view;
        }

        [TearDown]
        public void Cleanup()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SelectedDepartureDoesNotStayRedAfterPointerLeaves()
        {
            Build();
            var departure = _root.transform.Find("Office/Menu/Depart");
            var shape = departure.GetComponent<HubOfficeShape>();
            var focus = departure.GetComponent<HubOfficeFocus>();
            var restingColor = shape.color;
            focus.OnSelect(null);
            Assert.AreEqual(restingColor, shape.color, "Initial keyboard focus must not show a mouse highlight");
            focus.OnPointerEnter(null);
            Assert.AreNotEqual(restingColor, shape.color);
            focus.OnPointerExit(null);
            Assert.AreEqual(restingColor, shape.color, "Moving away must clear red even when EventSystem selection remains");
        }

        [Test]
        public void CustomSurfacesHaveRenderersAndMenuCanBeHitByThePointer()
        {
            Build();
            foreach (var shape in _root.GetComponentsInChildren<HubOfficeShape>())
                Assert.IsNotNull(shape.GetComponent<CanvasRenderer>(), shape.name + " must render and participate in raycasts");

            Canvas.ForceUpdateCanvases();
            var target = (RectTransform)_root.transform.Find("Office/Menu/Relic");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center))
            };
            var hits = new List<RaycastResult>();
            _root.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == target.gameObject), "Mouse must hit the equipment button");
        }

        [Test]
        public void PopupDrawsAnOpaqueSurfaceAndBlocksBackgroundInput()
        {
            Build();
            _root.transform.Find("Office/Menu/Record").GetComponent<Button>().onClick.Invoke();
            var overlay = _root.transform.Find("RunRecord");
            var surface = overlay.Find("Window/Surface").GetComponent<HubOfficeShape>();
            Assert.IsNotNull(surface.GetComponent<CanvasRenderer>());
            Assert.AreEqual(1f, surface.color.a, .001f);
            Assert.IsTrue(overlay.GetComponent<HubOfficeShape>().raycastTarget);
            Assert.Greater(overlay.GetSiblingIndex(), _root.transform.Find("Office").GetSiblingIndex());
            var home = _root.transform.Find("Office").GetComponent<CanvasGroup>();
            Assert.IsFalse(home.interactable);
            Assert.IsFalse(home.blocksRaycasts);
        }

        [Test]
        public void MenuUsesTheLeftHalfAndArtNeverBlocksButtons()
        {
            Build();
            var menu = (RectTransform)_root.transform.Find("Office/Menu");
            Assert.AreEqual(.48f, menu.anchorMax.x, .001f);
            Assert.IsNotNull(_root.transform.Find("Office/Menu/Relic").GetComponent<Button>());
            foreach (var image in _root.GetComponentsInChildren<RawImage>())
            {
                Assert.IsNotNull(image.texture);
                Assert.IsFalse(image.raycastTarget);
            }
        }

        [Test]
        public void PartyOpensRepairWithoutLeavingTheHubPrematurely()
        {
            var repairCount = 0;
            Build(() => repairCount++);
            _root.transform.Find("Office/Menu/Party").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(_root.transform.Find("Office").GetComponent<CanvasGroup>().interactable);
            _root.transform.Find("PartyMenu/Window/Repair").GetComponent<Button>().onClick.Invoke();
            _root.transform.Find("PartyMenu/Window/Repair").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, repairCount);
        }
    }
}
