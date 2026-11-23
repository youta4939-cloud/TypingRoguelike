using UnityEngine.UI;
namespace TypingRoguelike.Run
{
    // ScrollRect otherwise shows AutoHide scrollbars unconditionally in editor
    // previews. Use real overflow in both preview and play mode, without resizing
    // the viewport or moving text as the scrollbar appears/disappears.
    public sealed class StoryReadingScroll : ScrollRect
    {
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (verticalScrollbar != null && content != null && viewport != null)
                verticalScrollbar.gameObject.SetActive(content.rect.height > viewport.rect.height + 0.5f);
        }
    }

}
