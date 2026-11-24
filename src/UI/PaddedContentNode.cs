using KamiToolKit.Controls.Builders;
using KamiToolKit.UI.Nodes;
using KamiToolKit.UI.Nodes.Layout;

namespace ActionEffectRange.UI
{
    // Wraps a vertical layout so that it is inset by a padding on every side;
    // keeps the inner layout sized to this node minus the padding.
    public class PaddedContentNode : ResNode
    {
        private readonly VerticalListNode inner;
        private readonly float padding;

        public PaddedContentNode(Action<NodeBuilder> build, float padding, float itemSpacing)
        {
            this.padding = padding;
            var builder = new NodeBuilder<VerticalListNode>();
            build(builder);
            inner = builder.Build();
            inner.ItemSpacing = itemSpacing;
            inner.Position = new Vector2(padding, padding);
            inner.AttachNode(this);
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            inner.Size = new Vector2(
                MathF.Max(0, Width - 2 * padding),
                MathF.Max(0, Height - 2 * padding));
            inner.UpdateLayout();
        }
    }
}
