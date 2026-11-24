using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace ActionEffectRange.Drawing
{
    internal unsafe static class Projection
    {
        public static bool WorldToScreen(Vector3 worldPos, out Vector2 screenPos)
            => WorldToScreen(worldPos, out screenPos, out _);

        // Same as Dalamud's IGameGui.WorldToScreen, but also returns the raw
        // clip-space coords so callers can discard points behind the camera.
        internal static bool WorldToScreen(
            Vector3 worldPos, out Vector2 screenPos, out Vector3 pCoordsRaw)
        {
            var control = Control.Instance();
            var device = Device.Instance();
            if (control == null || device == null)
            {
                screenPos = default;
                pCoordsRaw = default;
                return false;
            }

            var windowPos = ImGuiHelpers.MainViewport.Pos;
            var viewProjectionMatrix = control->ViewProjectionMatrix;

            float width = device->Width;
            float height = device->Height;

            var pCoords = Vector3.Transform(worldPos, viewProjectionMatrix);
            pCoordsRaw = pCoords;

            screenPos = new Vector2(
                pCoords.X / MathF.Abs(pCoords.Z),
                pCoords.Y / MathF.Abs(pCoords.Z));

            screenPos.X = (0.5f * width * (screenPos.X + 1f)) + windowPos.X;
            screenPos.Y = (0.5f * height * (1f - screenPos.Y)) + windowPos.Y;

            return pCoords.Z > 0
                && screenPos.X > windowPos.X && screenPos.X < windowPos.X + width
                && screenPos.Y > windowPos.Y && screenPos.Y < windowPos.Y + height;
        }
    }
}
