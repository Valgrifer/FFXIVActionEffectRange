using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Core.Interfaces;
using KamiToolKit.UI;
using KamiToolKit.UI.Nodes;
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
                ActionDataInterfacing.GetActionDescription(row), detail);

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
        public static float ItemHeight => 40.0f;

        private readonly IconImageNode iconNode;
        private readonly TextNode nameNode;
        private readonly TextNode detailNode;
        private readonly TextNode idNode;

        public ActionListItemNode()
        {
            iconNode = new IconImageNode { FitTexture = true };
            iconNode.AttachNode(this);

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
            iconNode.IconId = itemData.IconId;
            iconNode.ActionTooltip = itemData.IconId != 0 ? itemData.ActionId : 0;
            iconNode.IsVisible = itemData.IconId != 0;
            nameNode.String = itemData.Description;
            detailNode.String = itemData.Detail;
            idNode.String = $"#{itemData.ActionId}";
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            iconNode.Size = new Vector2(Height - 4.0f, Height - 4.0f);
            iconNode.Position = new Vector2(2.0f, 2.0f);

            idNode.Size = new Vector2(56.0f, Height / 2.0f);
            idNode.Position = new Vector2(Width - idNode.Width - 4.0f, 0.0f);

            nameNode.Size = new Vector2(Width - iconNode.Width - idNode.Width - 12.0f, Height / 2.0f);
            nameNode.Position = new Vector2(iconNode.Width + 8.0f, 0.0f);

            detailNode.Size = nameNode.Size with { X = Width - iconNode.Width - 12.0f };
            detailNode.Position = nameNode.Position with { Y = Height / 2.0f };
        }
    }
}
