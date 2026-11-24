using KamiToolKit.Controls.Search;
using Lumina.Text.ReadOnly;
using System.Linq;

namespace ActionEffectRange.UI
{
    // Native search window listing player combat actions
    public class ActionSearchAddon : AbstractSearchAddon<ActionEntry, ActionListItemNode>
    {
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
