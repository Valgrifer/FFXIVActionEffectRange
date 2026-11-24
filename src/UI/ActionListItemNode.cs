using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Core.Interfaces;
using KamiToolKit.UI;
using KamiToolKit.UI.Nodes;
using KamiToolKit.UI.Nodes.Simplified;
using Sheets = Lumina.Excel.Sheets;

namespace ActionEffectRange.UI
{
    // An action shown in the native lists, with an optional extra line
    // (e.g. customised cone angle).
    public sealed record ActionEntry(uint ActionId, string Name, ushort IconId,
        string Description, string Detail)
    {
        public static ActionEntry FromRow(Sheets.Action row, string detail = "")
            => new(row.RowId, row.Name.ExtractText(), row.Icon,
                ActionDataInterfacing.GetActionLabel(row), detail);

        public static ActionEntry FromId(uint actionId, string detail = "")
        {
            var row = Actions.Data.ActionData.GetActionExcelRow(actionId);
            return row != null
                ? FromRow(row.Value, detail)
                : new(actionId, $"Unknown action", 0, $"#{actionId}", detail);
        }

        public string SearchString => $"{ActionId} {Description}";
    }

    public class ActionListItemNode : ListItemWithFocusNav<ActionEntry>, IListItemNode
    {
        public static float ItemHeight => 44.0f;
        private const float NativeIconSize = 48.0f;

        // Icon drawn as plain images (icon + IconA_Frame on top) inside a scaled
        // container: unlike the icon component they do not catch mouse events,
        // so hovering/clicking the icon acts on the whole row.
        private readonly ResNode iconNode;
        private readonly IconImageNode iconImageNode;
        private readonly TextNode nameNode;
        private readonly TextNode detailNode;
        private readonly TextNode idNode;

        public ActionListItemNode()
        {
            iconNode = new ResNode { Size = new Vector2(NativeIconSize, NativeIconSize) };
            iconNode.AttachNode(this);

            iconImageNode = new IconImageNode
            {
                Size = new Vector2(40.0f, 40.0f),
                Position = new Vector2(4.0f, 3.0f),
                FitTexture = true,
            };
            iconImageNode.AttachNode(iconNode);

            new SimpleImageNode
            {
                TexturePath = "ui/uld/IconA_Frame.tex",
                TextureCoordinates = Vector2.Zero,
                TextureSize = new Vector2(NativeIconSize, NativeIconSize),
                Size = new Vector2(NativeIconSize, NativeIconSize),
            }.AttachNode(iconNode);

            nameNode = new TextNode
            {
                TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
                FontSize = 14,
                AlignmentType = AlignmentType.BottomLeft,
            };
            nameNode.AttachNode(this);

            detailNode = new TextNode
            {
                TextFlags = TextFlags.Ellipsis | TextFlags.Emboss,
                FontSize = 12,
                AlignmentType = AlignmentType.TopLeft,
                TextColor = ColorHelper.GetColor(3),
            };
            detailNode.AttachNode(this);

            idNode = new TextNode
            {
                TextFlags = TextFlags.Emboss,
                FontSize = 10,
                AlignmentType = AlignmentType.BottomRight,
                TextColor = ColorHelper.GetColor(3),
            };
            idNode.AttachNode(this);
        }

        protected override void SetNodeData(ActionEntry itemData)
        {
            iconImageNode.IconId = itemData.IconId;
            iconNode.IsVisible = itemData.IconId != 0;
            // Action tooltip when hovering anywhere on the row
            ActionTooltip = itemData.ActionId;
            nameNode.String = itemData.Description;
            detailNode.String = itemData.Detail;
            idNode.String = $"#{itemData.ActionId}";
            LayoutChildren(itemData);
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            LayoutChildren();
        }

        // KamiToolKit ignores negative sizes and only calls OnSizeChanged once both
        // width and height are positive, while list items can be created before the
        // list has its final width; so lay out from the current size whenever the
        // node is (re)populated, never producing negative sizes.
        private void LayoutChildren(ActionEntry? data = null)
        {
            data ??= ItemData;
            if (Height <= 0) Height = ItemHeight;
            var height = Height;
            var width = Width;
            if (width <= 0) return;

            // The icon is laid out at its native 48px size, then scaled to fit the row
            var iconSize = height - 4.0f;
            var iconScale = iconSize / NativeIconSize;
            iconNode.Scale = new Vector2(iconScale, iconScale);
            iconNode.Position = new Vector2(4.0f, 2.0f);

            var textX = iconNode.Position.X + iconSize + 8.0f;
            var idWidth = 60.0f;

            // Without a detail line, the name and id are centred vertically
            var hasDetail = !string.IsNullOrEmpty(data?.Detail);
            var lineHeight = hasDetail ? height / 2.0f : height;
            nameNode.AlignmentType = hasDetail ? AlignmentType.BottomLeft : AlignmentType.Left;
            idNode.AlignmentType = hasDetail ? AlignmentType.BottomRight : AlignmentType.Right;
            detailNode.IsVisible = hasDetail;

            idNode.Size = new Vector2(idWidth, lineHeight);
            idNode.Position = new Vector2(MathF.Max(textX, width - idWidth - 6.0f), 0.0f);

            nameNode.Size = new Vector2(MathF.Max(1.0f, idNode.Position.X - textX - 4.0f), lineHeight);
            nameNode.Position = new Vector2(textX, 0.0f);

            detailNode.Size = new Vector2(MathF.Max(1.0f, width - textX - 6.0f), height / 2.0f);
            detailNode.Position = new Vector2(textX, height / 2.0f);
        }
    }
}
