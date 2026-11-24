using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.Data.Template;
using ActionEffectRange.Actions.Enums;
using ActionEffectRange.Drawing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controls.Builders;
using KamiToolKit.Core.Primitives.NativeAddon;
using KamiToolKit.UI.Nodes;
using KamiToolKit.UI.Nodes.ComponentNode;
using KamiToolKit.UI.Nodes.Layout;

namespace ActionEffectRange.UI
{
    // Native window editing the custom drawing style of a single action
    public class ActionStyleEditorWindow : NativeAddon
    {
        private static readonly float[] RowRatios = [0.45f, 0.55f];

        public required uint ActionId { get; init; }

        private ActionStyleDataItem style = null!;
        private ColorEditNode? colourNode;
        private CheckboxNode? filledNode;
        private SliderInputNode? fillAlphaNode;
        private CheckboxNode? outerRingNode;
        private SliderInputNode? thicknessNode;
        private SliderInputNode? persistNode;

        // Global style, with the beneficial/harmful colour matching the action
        public static ActionStyleDataItem DefaultStyle(uint actionId)
        {
            var row = ActionData.GetActionExcelRow(actionId);
            var beneficial = row != null
                && ActionData.GetActionHarmfulness(row.Value) == ActionHarmfulness.Beneficial;
            return new()
            {
                ActionId = actionId,
                Colour = beneficial ? Config.BeneficialColour : Config.HarmfulColour,
                Filled = Config.Filled,
                FillAlpha = MathF.Min(Config.FillAlpha, MaxFillAlpha),
                OuterRing = Config.OuterRing,
                Thickness = Config.Thickness,
                PersistSeconds = Config.PersistSeconds,
            };
        }

        public const float MaxFillAlpha = .5f;

        protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
        {
            base.OnSetup(addon, atkValueSpan);

            style = ActionData.TryGetActionStyle(ActionId, out var item) && item != null
                ? item.CopyFor(ActionId) : DefaultStyle(ActionId);

            var content = new PaddedContentNode(Build, 10.0f, 3.0f)
            {
                Position = ContentStartPosition,
                Size = ContentSize,
            };
            content.AttachNode(this);
        }

        protected override unsafe void OnFinalize(AtkUnitBase* addon)
        {
            colourNode = null;
            filledNode = null;
            fillAlphaNode = null;
            outerRingNode = null;
            thicknessNode = null;
            persistNode = null;
            base.OnFinalize(addon);
        }

        private void Build(NodeBuilder b) => b
            .ColorEdit4("Colour", style.Colour, DefaultStyle(ActionId).Colour,
                v => Edit(s => s.Colour = v), out colourNode)
            .Checkbox("Fill", style.Filled, v => Edit(s => s.Filled = v), out filledNode)
            .Row("Fill opacity", r => r.SliderInput(
                    style.FillAlpha, 0, MaxFillAlpha, .01f, 2,
                    v => Edit(s => s.FillAlpha = v), out fillAlphaNode),
                RowRatios)
            .Checkbox("Draw outline (outer ring)", style.OuterRing,
                v => Edit(s => s.OuterRing = v), out outerRingNode)
            .Row("Outline thickness", r => r.SliderInput(
                    style.Thickness, 1, 50, 1, 0,
                    v => Edit(s => s.Thickness = (int)v), out thicknessNode),
                RowRatios)
            .Row("Keep drawn for (s)", r => r.SliderInput(
                    style.PersistSeconds, .1f, 5, .1f, 1,
                    v => Edit(s => s.PersistSeconds = v), out persistNode),
                RowRatios)
            .Spacer(10)
            .HorizontalList(h => h
                .TextButton("Test", Test, n =>
                {
                    n.Width = 110;
                    if (n is KamiToolKit.Core.Interfaces.ITextTooltip t) t.TextTooltip = "Draw the effect range as if you had just used\n" +
                        "the action yourself, centred on / facing from you.";
                })
                .TextButton("Reset to global", ResetToGlobal, n => n.Width = 130)
                .TextButton("Close", Close, n => n.Width = 90),
                n => { n.Height = 26; ((LayoutListNode)n).ItemSpacing = 6; });

        private void Edit(Action<ActionStyleDataItem> apply)
        {
            apply(style);
            ActionData.SetActionStyle(style.CopyFor(ActionId));
            ActionData.SaveCustomisedData(writeToFile: true);
        }

        private void Test()
        {
            if (!EffectRangeDrawing.DrawTest(ActionId))
                PluginLog.Warning($"Could not test-draw action #{ActionId}");
        }

        private void ResetToGlobal()
        {
            style = DefaultStyle(ActionId);
            ActionData.SetActionStyle(style.CopyFor(ActionId));
            ActionData.SaveCustomisedData(writeToFile: true);
            colourNode?.CurrentColor = style.Colour;
            filledNode?.IsChecked = style.Filled;
            fillAlphaNode?.Value = style.FillAlpha;
            outerRingNode?.IsChecked = style.OuterRing;
            thicknessNode?.Value = style.Thickness;
            persistNode?.Value = style.PersistSeconds;
        }
    }
}
