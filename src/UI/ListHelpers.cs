using KamiToolKit.UI.Nodes;
using KamiToolKit.UI.Nodes.Layout;

namespace ActionEffectRange.UI
{
    internal static class ListHelpers
    {
        private const int MinScrollThumbLength = 24;

        // With long lists the scrollbar thumb shrinks to a few pixels; give it a
        // minimal length. EmptyLength (bar length - thumb length) is what the game
        // component uses to map the thumb position to the scroll position, so it
        // is adjusted together with the thumb to keep scrolling consistent.
        public static unsafe void EnsureMinScrollThumb<T, TU>(ListNode<T, TU>? list)
            where TU : ListItemNode<T>, KamiToolKit.Core.Interfaces.IListItemNode, new()
        {
            if (list == null) return;
            var scrollBar = list.ScrollBarNode;
            var component = scrollBar.Component;
            if (component == null || component->EmptyLength == 0) return;

            var barLength = (int)component->ScrollbarLength;
            var thumbLength = barLength - component->EmptyLength;
            if (thumbLength >= MinScrollThumbLength || barLength <= MinScrollThumbLength) return;

            component->EmptyLength = barLength - MinScrollThumbLength;
            scrollBar.ForegroundButtonNode.Height = MinScrollThumbLength;
        }
    }
}
