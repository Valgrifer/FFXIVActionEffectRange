using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.Enums;
using System.Collections.Generic;
using System.Linq;

using Sheets = Lumina.Excel.Sheets;

namespace ActionEffectRange.UI
{
    public static class ActionDataInterfacing
    {
        public static IEnumerable<Sheets.Action>? GetAllPartialMatchActionExcelRows(
            string input, bool alsoMatchId, int maxCount, 
            bool playerCombatActionOnly, Func<Sheets.Action, bool>? filter)
            => ActionData.ActionExcelSheet.Where(row =>
                (row.Name.ExtractText().Contains(input, 
                        StringComparison.CurrentCultureIgnoreCase)
                    || alsoMatchId && row.RowId.ToString().Contains(input))
                && (!playerCombatActionOnly || ActionData.IsPlayerCombatAction(row)) 
                && (filter == null || filter(row)))
            .Take(maxCount);

        // Name with class/job and PvP tags, without the id; role actions
        // (no single class/job) show their class/job category instead
        public static string GetActionLabel(Sheets.Action row)
        {
            var classjobRow = row.ClassJob.ValueNullable;
            var category = row.ClassJobCategory.RowId > 1
                ? row.ClassJobCategory.ValueNullable?.Name.ExtractText() : null;
            var classjob = classjobRow != null && classjobRow.Value.RowId > 0
                ? $" [{classjobRow.Value.Abbreviation.ExtractText()}]"
                : !string.IsNullOrEmpty(category) ? $" [{category}]" : string.Empty;
            var pvp = row.IsPvP ? " [PvP]" : string.Empty;
            return $"{row.Name.ExtractText()}{classjob}{pvp}";
        }

        public static string GetActionDescription(Sheets.Action row)
        {
            var classjobRow = row.ClassJob.ValueNullable;
            var classjob = classjobRow != null && classjobRow.Value.RowId > 0
                ? $" [{classjobRow.Value.Abbreviation.ExtractText()}]" : string.Empty;
            var pvp = row.IsPvP ? " [PvP]" : string.Empty;
            return $"#{row.RowId} {row.Name.ExtractText()}{classjob}{pvp}";
        }

        public static string GetAoETypeLabel(ActionAoEType type)
            => type switch
            {
                ActionAoEType.None => "N/A",
                ActionAoEType.Circle or ActionAoEType.Circle2 => "Circle",
                ActionAoEType.Cone or ActionAoEType.Cone2 => "Cone",
                ActionAoEType.Line or ActionAoEType.Line2 => "Line",
                ActionAoEType.GT => "Circle (GT)",
                ActionAoEType.DashAoE => "Dash (Line)",
                ActionAoEType.Donut => "Donut",
                _ => "?"
            };

        public static string GetAoETypeLabel(byte castType)
            => GetAoETypeLabel((ActionAoEType)castType);

        public static ActionAoEType[] AoETypeSelections
            => new ActionAoEType[]
            {
                ActionAoEType.Circle,
                ActionAoEType.Cone,
                ActionAoEType.Line,
                ActionAoEType.DashAoE,
                ActionAoEType.Donut,
                ActionAoEType.GT
            };

        public static ActionHarmfulness[] ActionHarmfulnessesSelections
            => Enum.GetValues<ActionHarmfulness>();

        public static float DegToCycle(float deg) => deg / 360;
        
        public static float CycleToDeg(float cycle) => cycle * 360;

        public static float DegToRad(float deg)
            => MathF.PI * deg / 180;

        public static float RadToDeg(float rad)
            => 180 * rad / MathF.PI;
    }
}
