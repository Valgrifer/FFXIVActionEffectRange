using KamiToolKit.Controls.Search;
using Lumina.Text.ReadOnly;
using System.Linq;

namespace ActionEffectRange.UI
{
    // Native search window listing player combat actions
    public class ActionSearchAddon : AbstractSearchAddon<ActionEntry, ActionListItemNode>
    {
        protected override unsafe void OnSetup(
            FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase* addon,
            Span<FFXIVClientStructs.FFXIV.Component.GUI.AtkValue> atkValueSpan)
        {
            base.OnSetup(addon, atkValueSpan);
            // The default placeholder is a game text id, which takes precedence
            SearchInputNode?.PlaceholderStringTextId = 0;
            SearchInputNode?.PlaceholderString = "Search by action name, job or id";
            ResultsListNode?.PlaceholderString = "No matching action.";
        }

        // The list builds its rows and scroll params before it has its final size;
        // rebuild once the window is shown so the first display is laid out properly.
        protected override unsafe void OnShow(FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase* addon)
        {
            base.OnShow(addon);
            ResultsListNode?.FullRebuild();
            ListHelpers.EnsureMinScrollThumb(ResultsListNode);
        }

        protected override void OnSearchInputReceived(ReadOnlySeString searchString)
        {
            var input = searchString.ExtractText().Trim();
            ResultsListNode?.Options = input.Length == 0
                ? OptionsList
                : OptionsList.Where(e => e.SearchString.Contains(input,
                    StringComparison.CurrentCultureIgnoreCase)).ToList();

            base.OnSearchInputReceived(searchString);
            ListHelpers.EnsureMinScrollThumb(ResultsListNode);
        }
    }
}
