using ActionEffectRange.Actions.Data;
using ActionEffectRange.Actions.EffectRange;
using ActionEffectRange.Actions.Enums;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

namespace ActionEffectRange.Drawing.Workers
{
    public class DrawWorker : IDrawWorker
    {
        private readonly List<DrawData> drawDataQueue = new();

        private uint beneficialRingColour;
        private uint beneficialFillColour;
        private uint harmfulRingColour;
        private uint harmfulFillColour;

        public DrawTrigger Trigger => DrawTrigger.Used;

        public DrawWorker()
        {
            RefreshConfig();
        }

        public void Clear()
        {
            drawDataQueue.Clear();
        }

        public void CleanupOld()
        {
            // Each drawing may have its own duration (custom action styles)
            drawDataQueue.RemoveAll(data
                => data.ElapsedSeconds > Config.DrawDelay + data.Style.PersistSeconds);
        }

        public void Draw(ImDrawListPtr drawList)
        {
            foreach (var data in drawDataQueue)
            {
                if (data.ElapsedSeconds < Config.DrawDelay) continue;
                    data.Draw(ImGui.GetWindowDrawList());
            }
        }

        public bool HasDataToDraw() => drawDataQueue.Count > 0;

        public void QueueDrawing(uint sequence, EffectRangeData effectRangeData, 
            Vector3 originPos, Vector3 targetPos, float rotation)
        {
            if (!IsPlayerLoaded) return;
            LogUserDebug($"{GetType().Name}.QueueDrawing => " +
                $"{effectRangeData}, orig={originPos}, target={targetPos}, rotation={rotation}");

            var isHarmful = effectRangeData.Harmfulness.HasFlag(ActionHarmfulness.Harmful);
            var isBeneficial = effectRangeData.Harmfulness.HasFlag(ActionHarmfulness.Beneficial);

            // A custom action style replaces the harmful/beneficial colours: drawn once
            if (ActionData.TryGetActionStyle(effectRangeData.ActionId, out var custom)
                && custom != null)
            {
                if (isHarmful && Config.DrawHarmful || isBeneficial && Config.DrawBeneficial)
                    Enqueue(effectRangeData,
                        ImGui.ColorConvertFloat4ToU32(custom.Colour),
                        ImGui.ColorConvertFloat4ToU32(new(custom.Colour.X,
                            custom.Colour.Y, custom.Colour.Z, custom.FillAlpha)),
                        originPos, targetPos, rotation, DrawStyle.From(custom));
                return;
            }

            var style = DrawStyle.Global;
            if (isHarmful && Config.DrawHarmful)
                Enqueue(effectRangeData, harmfulRingColour, harmfulFillColour,
                    originPos, targetPos, rotation, style);
            if (isBeneficial && Config.DrawBeneficial)
                Enqueue(effectRangeData, beneficialRingColour, beneficialFillColour,
                    originPos, targetPos, rotation, style);
        }

        // Test drawing from the config UI: drawn whatever the harmful/beneficial
        // options, with the action's custom style if any
        public void QueueTestDrawing(EffectRangeData effectRangeData,
            Vector3 originPos, Vector3 targetPos, float rotation)
        {
            if (ActionData.TryGetActionStyle(effectRangeData.ActionId, out var custom)
                && custom != null)
            {
                Enqueue(effectRangeData,
                    ImGui.ColorConvertFloat4ToU32(custom.Colour),
                    ImGui.ColorConvertFloat4ToU32(new(custom.Colour.X,
                        custom.Colour.Y, custom.Colour.Z, custom.FillAlpha)),
                    originPos, targetPos, rotation, DrawStyle.From(custom));
                return;
            }
            var beneficial = effectRangeData.Harmfulness == ActionHarmfulness.Beneficial;
            Enqueue(effectRangeData,
                beneficial ? beneficialRingColour : harmfulRingColour,
                beneficial ? beneficialFillColour : harmfulFillColour,
                originPos, targetPos, rotation, DrawStyle.Global);
        }

        private void Enqueue(EffectRangeData effectRangeData, uint ringColour,
            uint fillColour, Vector3 originPos, Vector3 targetPos, float rotation,
            DrawStyle style)
        {
            var drawData = EffectRangeDrawing.GenerateDrawData(effectRangeData,
                ringColour, fillColour, originPos, targetPos, rotation, style);
            if (drawData != null) drawDataQueue.Add(drawData);
        }

        public void RefreshConfig()
        {
            beneficialRingColour = ImGui.ColorConvertFloat4ToU32(
                Config.BeneficialColour);
            beneficialFillColour = ImGui.ColorConvertFloat4ToU32(new(
                Config.BeneficialColour.X, Config.BeneficialColour.Y,
                Config.BeneficialColour.Z, Config.FillAlpha));
            harmfulRingColour = ImGui.ColorConvertFloat4ToU32(
                Config.HarmfulColour);
            harmfulFillColour = ImGui.ColorConvertFloat4ToU32(new(
                Config.HarmfulColour.X, Config.HarmfulColour.Y,
                Config.HarmfulColour.Z, Config.FillAlpha));
        }

        public void Reset() => Clear();
    }
}
