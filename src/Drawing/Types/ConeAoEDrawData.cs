using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

namespace ActionEffectRange.Drawing.Types
{
    public abstract class ConeAoEDrawData : DrawData
    {
        public readonly Vector3 Origin;
        public readonly float Rotation;
        public readonly float Radius;
        public readonly byte Width;
        public readonly Vector3 End;
        public readonly float CentralAngleCycles;


        public ConeAoEDrawData(Vector3 origin, byte baseEffectRange, byte xAxisModifier, 
            float rotation, float centralAngleCycles, uint ringColour, uint fillColour)
            : base(ringColour, fillColour)
        {
            Origin = origin;
            Radius = baseEffectRange + .5f; 
            Width = xAxisModifier;
            Rotation = rotation;
            var direction = new Vector3(MathF.Sin(Rotation), 0, MathF.Cos(Rotation));
            End = CalcFarEndWorldPos(Origin, direction, Radius);

            CentralAngleCycles = centralAngleCycles;
        }

        // Fills a half cone and returns its arc points, or null if it cannot be drawn
        private Vector2[]? DrawHalfCone(ImDrawListPtr drawList, Vector2 projectedOrigin, 
            Vector2 projectedEnd, int numSegments, bool drawClockwise)
        {
            var points = new Vector2[numSegments];
            // rotation +/- (angleCycles * 2 * pi) / 2
            var rot = drawClockwise 
                ? Rotation - CentralAngleCycles * MathF.PI 
                : Rotation + CentralAngleCycles * MathF.PI;    
            drawList.PathLineTo(projectedOrigin);
            for (int i = 0; i < numSegments; i++)
            {
                var a = drawClockwise 
                    ? i * ArcSegmentAngle + rot : rot - i * ArcSegmentAngle;
                Projection.WorldToScreen(
                    new(Origin.X + Radius * MathF.Sin(a), Origin.Y, 
                        Origin.Z + Radius * MathF.Cos(a)),
                    out var p, out var pr);
                // Don't draw the whole range if some of the points may be
                //  projected to a weird position.
                // We cannot simply ignore it like when we draw Circle AoE
                //  because that may truncate part of the cone 
                if (pr.Z < -1)
                {
                    drawList.PathClear();
                    return null;
                }
                points[i] = p;
                drawList.PathLineTo(p);
            }
            drawList.PathLineTo(projectedEnd);
            if (Style.Filled)
                drawList.PathFillConvex(FillColour);
            drawList.PathClear();
            return points;
        }

        public override void Draw(ImDrawListPtr drawList)
        {
            Projection.WorldToScreen(Origin, out var p0);
            Projection.WorldToScreen(End, out var pe);
#if DEBUG
            drawList.AddCircleFilled(pe, Style.Thickness * 2, RingColour);
#endif      

            var numSegmentsHalf 
                = (int)(CentralAngleCycles * Config.NumSegments / 2);

            var left = DrawHalfCone(drawList, p0, pe, numSegmentsHalf, true);
            var right = DrawHalfCone(drawList, p0, pe, numSegmentsHalf, false);
            if (!Style.OuterRing || left == null || right == null) return;

            // Whole cone outline: origin, arc from one edge to the other
            var polygon = new List<Vector2>(left.Length + right.Length + 2) { p0 };
            polygon.AddRange(left);
            polygon.Add(pe);
            for (var i = right.Length - 1; i >= 0; i--)
                polygon.Add(right[i]);
            StrokeInside(drawList, polygon);
        }
    }
}
