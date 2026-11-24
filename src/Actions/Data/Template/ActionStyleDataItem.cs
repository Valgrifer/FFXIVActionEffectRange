namespace ActionEffectRange.Actions.Data.Template
{
    // Custom drawing style of one action, replacing the global style options
    public class ActionStyleDataItem : IDataItem
    {
        public uint ActionId { get; set; }
        public Vector4 Colour { get; set; } = new(1, .5f, .5f, 1);
        public bool Filled { get; set; } = true;
        public float FillAlpha { get; set; } = .1f;
        public bool OuterRing { get; set; } = true;
        public int Thickness { get; set; } = 2;
        public float PersistSeconds { get; set; } = 1;

        public ActionStyleDataItem CopyFor(uint actionId)
        {
            var copy = (ActionStyleDataItem)MemberwiseClone();
            copy.ActionId = actionId;
            return copy;
        }
    }
}
