using Dalamud.Bindings.ImGui;

namespace ActionEffectRange.Drawing.Types
{
    public class CircleAoEDrawData : DrawData
    {
        public readonly Vector3 Centre;
        public readonly int Radius;

        public CircleAoEDrawData(Vector3 centre, byte baseEffectRange, 
            byte xAxisModifier, uint ringColour, uint fillColour)
            : base(ringColour, fillColour)
        {
            Centre = centre;
            Radius = baseEffectRange + xAxisModifier;
        }

        public override void Draw(ImDrawListPtr drawList)
        {
            if (Config.LargeDrawOpt == 1 && Radius >= Config.LargeThreshold) 
                return;  // no draw large

            var points = new Vector2[Config.NumSegments];
            var seg = 2 * MathF.PI / Config.NumSegments;
            for (int i = 0; i < Config.NumSegments; i++)
            {
                Projection.WorldToScreen(
                    new(Centre.X + Radius * MathF.Sin(i * seg),
                        Centre.Y,
                        Centre.Z + Radius * MathF.Cos(i * seg)),
                    out var p, out var pr);

                // Dont add points that may be projected to weird positions
                points[i] = pr.Z < -.1f ? new(float.NaN, float.NaN) : p;
            }

            if (Style.Filled 
                && (Config.LargeDrawOpt == 0 || Radius < Config.LargeThreshold))
            {
                foreach (var p in points)
                    if (!float.IsNaN(p.X)) drawList.PathLineTo(p);
                drawList.PathFillConvex(FillColour);
            }
            if (Style.OuterRing)
                StrokeInside(drawList, points);
            drawList.PathClear();
        }
    }
}
