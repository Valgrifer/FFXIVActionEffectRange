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
            SearchInputNode?.PlaceholderString = "Search by action name, job or id";
            ResultsListNode?.PlaceholderString = "No matching action.";
        }

        protected override void OnSearchInputReceived(ReadOnlySeString searchString)
        {
            var input = searchString.ExtractText().Trim();
            ResultsListNode?.Options = input.Length == 0
                ? OptionsList
                : OptionsList.Where(e => e.SearchString.Contains(input,
                    StringComparison.CurrentCultureIgnoreCase)).ToList();

            base.OnSearchInputReceived(searchString);
        }
    }
}
