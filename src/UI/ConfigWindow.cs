using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controls.Builders;
using KamiToolKit.Core.Interfaces;
using KamiToolKit.Core.Primitives.NativeAddon;
using KamiToolKit.Core.Primitives.NodeBase;
using KamiToolKit.UI.Nodes.ComponentNode;
using KamiToolKit.UI.Nodes.Layout;
using System.Collections.Generic;
using System.Linq;

namespace ActionEffectRange.UI
{
    // Native (in-game addon) configuration window, built with KamiToolKit
    public class ConfigWindow : NativeAddon
    {
        private const float TabPadding = 10.0f;
        private const float ItemSpacing = 3.0f;
        private const float CategorySpacing = 14.0f;
        private static readonly float[] RowRatios = [0.55f, 0.45f];

        private ActionSearchAddon? searchAddon;
        private List<ActionEntry>? allActions;

        private ListNode<ActionEntry, ActionListItemNode>? blacklistNode;
        private ActionEntry? selectedBlacklisted;

        private ListNode<ActionEntry, ActionListItemNode>? coneListNode;
        private ActionEntry? selectedCone;
        private NumericInputNode? coneAngleInput;
        private NumericInputNode? coneOffsetInput;
        private int coneAngleDeg = 90;
        private int coneOffsetDeg = 0;

        protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
        {
            base.OnSetup(addon, atkValueSpan);

            var root = NativeUiBuilder.Create(ContentSize, ContentStartPosition, b => b
                .TabbedContent(tabs => tabs
                    .AddTab("General", Padded(BuildGeneralTab))
                    .AddTab("Style", Padded(BuildStyleTab))
                    .AddTab("Casting", Padded(BuildCastingTab))
                    .AddTab("Blacklist", Padded(BuildBlacklistTab))
                    .AddTab("Cone AoEs", Padded(BuildConeTab))));
            ((NodeBase)root).AttachNode(this);
        }

        protected override unsafe void OnShow(AtkUnitBase* addon)
        {
            base.OnShow(addon);
            // Lists build their rows before they have their final size
            blacklistNode?.FullRebuild();
            coneListNode?.FullRebuild();
        }

        protected override unsafe void OnFinalize(AtkUnitBase* addon)
        {
            blacklistNode = null;
            coneListNode = null;
            coneAngleInput = null;
            coneOffsetInput = null;
            selectedBlacklisted = null;
            selectedCone = null;
            ActionData.SaveCustomisedData(writeToFile: true);
            base.OnFinalize(addon);
        }

        public override void Dispose()
        {
            searchAddon?.Dispose();
            searchAddon = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override async System.Threading.Tasks.ValueTask DisposeAsync()
        {
            if (searchAddon != null) await searchAddon.DisposeAsync();
            searchAddon = null;
            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        #region Tabs

        private void BuildGeneralTab(NodeBuilder b) => b
            .Checkbox("Enable plugin", Config.Enabled, v => Set(() => Config.Enabled = v))
            .Checkbox("Enable in PvP zones", Config.EnabledPvP, v => Set(() => Config.EnabledPvP = v))
            .Spacer(CategorySpacing)
            .CategoryLabel("Actions to draw")
            .Checkbox("Beneficial actions", Config.DrawBeneficial,
                v => Set(() => Config.DrawBeneficial = v),
                n => Tooltip(n, "Draw the effect range of actions with beneficial effects,\nsuch as heals and buffs."))
            .Checkbox("Harmful actions", Config.DrawHarmful,
                v => Set(() => Config.DrawHarmful = v),
                n => Tooltip(n, "Draw the effect range of actions with harmful effects,\nsuch as attacks and debuffs."))
            .Checkbox("ACN/SMN/SCH pets' actions", Config.DrawACNPets,
                v => Set(() => Config.DrawACNPets = v),
                n => Tooltip(n, "Also draw the AoE actions used by your Arcanist/Summoner/Scholar pet."))
            .Checkbox("Other summoned companions' actions", Config.DrawSummonedCompanions,
                v => Set(() => Config.DrawSummonedCompanions = v),
                n => Tooltip(n, "Also draw the actions of companions with a model summoned\n" +
                    "while not Arcanist/Summoner/Scholar, e.g. MCH's autoturrets."))
            .Checkbox("Ground-targeted actions", Config.DrawGT,
                v => Set(() => Config.DrawGT = v))
            .Checkbox("Special/Artillery actions", Config.DrawEx,
                v => Set(() => Config.DrawEx = v),
                n => Tooltip(n, "Also draw actions of category " +
                    $"\"{ActionData.GetActionCategoryName(ActionCategory.Special)}\" or " +
                    $"\"{ActionData.GetActionCategoryName(ActionCategory.Artillery)}\"\n" +
                    "(duty actions, mounts, transformations...).\n" +
                    "Drawing for these actions may be very inaccurate."))
            .Spacer(CategorySpacing)
            .CategoryLabel("Large effect ranges")
            .Row("Circle/donut AoEs", r => r.Combo(
                    Enumerable.Range(0, Configuration.LargeDrawOptions.Length).ToList(),
                    Config.LargeDrawOpt,
                    v => Set(() => Config.LargeDrawOpt = v),
                    v => Configuration.LargeDrawOptions[v]),
                RowRatios)
            .Row("Apply to effect range >=", r => r.InputInt(
                    Config.LargeThreshold, 5, 55,
                    v => Set(() => Config.LargeThreshold = v),
                    n => Tooltip(n, "E.g. with 15, Medica and Medica II are affected, but not Cure III.")),
                RowRatios)
            .Spacer(CategorySpacing)
            .CategoryLabel("Debug")
            .Checkbox("Log debug info to the Dalamud log", Config.LogDebug,
                v => Set(() => Config.LogDebug = v));

        private void BuildStyleTab(NodeBuilder b) => b
            .CategoryLabel("Colours")
            .ColorEdit4("Beneficial actions", Config.BeneficialColour, new Vector4(.5f, .5f, 1, 1),
                v => Set(() => Config.BeneficialColour = v))
            .ColorEdit4("Harmful actions", Config.HarmfulColour, new Vector4(1, .5f, .5f, 1),
                v => Set(() => Config.HarmfulColour = v))
            .Spacer(CategorySpacing)
            .CategoryLabel("Shape")
            .Checkbox("Draw outline (outer ring)", Config.OuterRing,
                v => Set(() => Config.OuterRing = v))
            .Row("Outline thickness", r => r.IntSlider(
                    Config.Thickness, 1, 50, v => Set(() => Config.Thickness = v)),
                RowRatios)
            .Checkbox("Fill", Config.Filled, v => Set(() => Config.Filled = v))
            .Row("Fill opacity", r => r.FloatSlider(
                    Config.FillAlpha, 0, 1, v => Set(() => Config.FillAlpha = v),
                    n => n.Step = .01f),
                RowRatios)
            .Row("Smoothness (segments)", r => r.IntSlider(
                    Config.NumSegments, 40, 500, v => Set(() => Config.NumSegments = v),
                    n => Tooltip(n, "The larger the number, the smoother the shapes.")),
                RowRatios)
            .Spacer(CategorySpacing)
            .CategoryLabel("Timing")
            .Row("Delay before drawing (s)", r => r.FloatSlider(
                    Config.DrawDelay, 0, 2, v => Set(() => Config.DrawDelay = v),
                    n => n.Step = .05f),
                RowRatios)
            .Row("Keep drawn for (s)", r => r.FloatSlider(
                    Config.PersistSeconds, .1f, 5, v => Set(() => Config.PersistSeconds = v),
                    n => n.Step = .1f),
                RowRatios);

        private void BuildCastingTab(NodeBuilder b) => b
            .Checkbox("Draw while casting", Config.DrawWhenCasting,
                v => Set(() => Config.DrawWhenCasting = v),
                n => Tooltip(n, "Also draw the effect range while you are casting an AoE action.\n" +
                    "Only in PvE areas."))
            .ColorEdit4("Casting colour", Config.DrawWhenCastingColour, new Vector4(1, 1, .5f, 1),
                v => Set(() => Config.DrawWhenCastingColour = v))
            .Checkbox("Keep drawn until the cast ends", Config.DrawWhenCastingUntilCastEnd,
                v => Set(() => Config.DrawWhenCastingUntilCastEnd = v),
                n => Tooltip(n, "Otherwise the drawing is removed after the duration set in Style > Timing."));

        // Lists fill the remaining height below them, so they are always added last
        private void BuildBlacklistTab(NodeBuilder b) => b
            .TextWrapped("Actions in this list are never drawn.", 1)
            .Spacer(4)
            .HorizontalList(h => h
                .TextButton("Add actions...", () => OpenActionSearch("Blacklist actions", AddToBlacklist,
                        row => row.EffectRange > 0),
                    n => n.Width = 140)
                .TextButton("Remove selected", RemoveFromBlacklist, n => n.Width = 140),
                n => { n.Height = 26; ((LayoutListNode)n).ItemSpacing = 6; })
            .Spacer(4)
            .List<ActionEntry, ActionListItemNode>(BlacklistEntries(),
                e => selectedBlacklisted = e, out blacklistNode,
                n => n.PlaceholderString = "No blacklisted action.");

        private void BuildConeTab(NodeBuilder b) => b
            .TextWrapped("Override the central angle and rotation of cone AoEs\n" +
                "whose default drawing is wrong.", 2)
            .Spacer(4)
            .Row("Central angle (degrees)", r => r.InputInt(
                    coneAngleDeg, 1, 360, v => coneAngleDeg = v, out coneAngleInput),
                RowRatios)
            .Row("Rotation offset (degrees)", r => r.InputInt(
                    coneOffsetDeg, 0, 359, v => coneOffsetDeg = v, out coneOffsetInput,
                    n => Tooltip(n, "Rotation of the cone relative to the direction it is facing.")),
                RowRatios)
            .Spacer(4)
            .HorizontalList(h => h
                .TextButton("Add actions...", () => OpenActionSearch("Cone AoE actions", AddCones,
                        row => row.CastType is (byte)ActionAoEType.Cone or (byte)ActionAoEType.Cone2),
                    n => n.Width = 120)
                .TextButton("Apply to selected", ApplyToSelectedCone, n => n.Width = 140)
                .TextButton("Remove selected", RemoveCone, n => n.Width = 140),
                n => { n.Height = 26; ((LayoutListNode)n).ItemSpacing = 6; })
            .Spacer(4)
            .List<ActionEntry, ActionListItemNode>(ConeEntries(),
                OnConeSelected, out coneListNode,
                n => n.PlaceholderString = "No customised cone AoE.");

        #endregion

        #region Blacklist / cone data

        private static List<ActionEntry> BlacklistEntries()
            => ActionData.GetCustomisedActionBlacklistCopy()
                .Select(i => ActionEntry.FromId(i.ActionId))
                .OrderBy(e => e.ActionId).ToList();

        private static List<ActionEntry> ConeEntries()
            => ActionData.GetCustomisedConeAoEAngleListCopy()
                .Select(i => ActionEntry.FromId(i.ActionId,
                    $"Angle {ActionDataInterfacing.CycleToDeg(i.CentralAngleCycles):0}°, " +
                    $"offset {ActionDataInterfacing.RadToDeg(i.RotationOffset):0}°"))
                .OrderBy(e => e.ActionId).ToList();

        private void AddToBlacklist(List<ActionEntry> entries)
        {
            foreach (var e in entries) ActionData.AddToActionBlacklist(e.ActionId);
            SaveCustomised();
            RefreshLists();
        }

        private void RemoveFromBlacklist()
        {
            if (selectedBlacklisted == null) return;
            ActionData.RemoveFromActionBlacklist(selectedBlacklisted.ActionId);
            selectedBlacklisted = null;
            SaveCustomised();
            RefreshLists();
        }

        private void OnConeSelected(ActionEntry? entry)
        {
            selectedCone = entry;
            if (entry == null) return;
            var item = ActionData.GetCustomisedConeAoEAngleListCopy()
                .FirstOrDefault(i => i.ActionId == entry.ActionId);
            if (item == null) return;
            coneAngleDeg = (int)MathF.Round(ActionDataInterfacing.CycleToDeg(item.CentralAngleCycles));
            coneOffsetDeg = (int)MathF.Round(ActionDataInterfacing.RadToDeg(item.RotationOffset));
            coneAngleInput?.Value = coneAngleDeg;
            coneOffsetInput?.Value = coneOffsetDeg;
        }

        private void AddCones(List<ActionEntry> entries)
        {
            foreach (var e in entries) SetCone(e.ActionId);
            SaveCustomised();
            RefreshLists();
        }

        private void ApplyToSelectedCone()
        {
            if (selectedCone == null) return;
            SetCone(selectedCone.ActionId);
            SaveCustomised();
            RefreshLists();
        }

        private void SetCone(uint actionId)
        {
            ActionData.RemoveFromConeAoEAngleList(actionId);
            ActionData.AddToConeAoEAngleList(actionId,
                ActionDataInterfacing.DegToCycle(coneAngleDeg),
                ActionDataInterfacing.DegToRad(coneOffsetDeg));
        }

        private void RemoveCone()
        {
            if (selectedCone == null) return;
            ActionData.RemoveFromConeAoEAngleList(selectedCone.ActionId);
            selectedCone = null;
            SaveCustomised();
            RefreshLists();
        }

        private void RefreshLists()
        {
            blacklistNode?.Options = BlacklistEntries();
            coneListNode?.Options = ConeEntries();
        }

        private static void SaveCustomised()
            => ActionData.SaveCustomisedData(writeToFile: true);

        #endregion

        #region Helpers

        private void OpenActionSearch(string title,
            Action<List<ActionEntry>> onConfirmed, Func<Lumina.Excel.Sheets.Action, bool>? filter = null)
        {
            allActions ??= ActionData.ActionExcelSheet
                .Where(row => row.RowId > 0 && ActionData.IsPlayerCombatAction(row))
                .Select(row => ActionEntry.FromRow(row))
                .ToList();

            searchAddon?.Dispose();
            searchAddon = new ActionSearchAddon
            {
                InternalName = "AERActionSearch",
                Title = title,
                Size = new Vector2(420, 520),
                AllowMultiselect = true,
                OptionsList = filter == null ? allActions
                    : allActions.Where(e => ActionData.GetActionExcelRow(e.ActionId) is { } row
                        && filter(row)).ToList(),
                ConfirmedSelections = onConfirmed,
            };
            searchAddon.Open();
        }

        private static PaddedContentNode Padded(Action<NodeBuilder> build)
            => new(build, TabPadding, ItemSpacing);

        private static void Set(Action apply)
        {
            apply();
            Config.Save();
            RefreshConfig();
        }

        private static void Tooltip(object node, string text)
        {
            if (node is ITextTooltip t) t.TextTooltip = text;
        }

        #endregion
    }
}
