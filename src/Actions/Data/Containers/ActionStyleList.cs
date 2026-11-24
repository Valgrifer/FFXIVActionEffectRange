using ActionEffectRange.Actions.Data.Template;
using System.Collections.Generic;
using System.Linq;

namespace ActionEffectRange.Actions.Data.Containers
{
    // Per-action custom drawing styles (user-defined only)
    public class ActionStyleList : IActionDataContainer<ActionStyleDataItem>
    {
        private readonly Dictionary<uint, ActionStyleDataItem> customisedDict = new();
        private readonly Configuration config;

        public int PredefinedCount => 0;
        public int CustomisedCount => customisedDict.Count;

        public ActionStyleList(Configuration config)
        {
            this.config = config;
            Reload();
        }

        public void Reload()
        {
            customisedDict.Clear();
            foreach (var item in config.ActionStyleList)
                Add(item);
        }

        public bool Add(ActionStyleDataItem item)
            => item.ActionId > 0 && customisedDict.TryAdd(item.ActionId, item);

        public void Set(ActionStyleDataItem item)
        {
            if (item.ActionId > 0) customisedDict[item.ActionId] = item;
        }

        public bool Remove(uint actionId)
            => customisedDict.Remove(actionId);

        public bool Contains(uint actionId)
            => customisedDict.ContainsKey(actionId);

        public bool TryGet(uint actionId, out ActionStyleDataItem? item)
            => customisedDict.TryGetValue(actionId, out item);

        public IEnumerable<ActionStyleDataItem> CopyCustomised()
            => customisedDict.Values.Select(i => i.CopyFor(i.ActionId)).ToList();

        public void Save(bool writeToFile = false)
        {
            config.ActionStyleList = CopyCustomised().ToArray();
            if (writeToFile) config.Save();
        }
    }
}
