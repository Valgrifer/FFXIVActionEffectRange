using ActionEffectRange.Actions.Data.Template;

namespace ActionEffectRange.Drawing
{
    // Shape style of a drawing: global options, or an action's custom style
    public sealed record DrawStyle(bool Filled, bool OuterRing, int Thickness, float PersistSeconds)
    {
        public static DrawStyle Global
            => new(Config.Filled, Config.OuterRing, Config.Thickness, Config.PersistSeconds);

        public static DrawStyle From(ActionStyleDataItem item)
            => new(item.Filled, item.OuterRing, item.Thickness, item.PersistSeconds);
    }
}
