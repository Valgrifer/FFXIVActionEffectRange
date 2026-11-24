using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

namespace ActionEffectRange.Drawing
{
    public abstract class DrawData
    {
        public readonly uint RingColour;
        public readonly uint FillColour;
        public DrawStyle Style { get; set; } = DrawStyle.Global;

        public DrawData(uint ringColour, uint fillColour)
        {
            RingColour = ringColour;
            FillColour = fillColour;
        }


        private readonly DateTime createTime = DateTime.Now;
        public float ElapsedSeconds 
            => (float)(DateTime.Now - createTime).TotalSeconds;


        public abstract void Draw(ImDrawListPtr drawList);


        protected static float CalcRotation(Vector3 origin, Vector3 target)
            => MathF.Atan2(target.X - origin.X, target.Z - origin.Z);

        protected static float ArcSegmentAngle 
            => 2 * MathF.PI / Config.NumSegments;


        protected static Vector3 CalcDirection(Vector3 origin, Vector3 target, 
            float rotationOffset = 0, bool ignoreY = true)
        {
            var dir = Vector3.Normalize(target - origin);
            // Further rotate by offset
            if (rotationOffset > .0001f || rotationOffset < -.0001f)
                dir = new(
                MathF.Cos(rotationOffset) * dir.X
                        - MathF.Sin(rotationOffset) * dir.Z,
                dir.Y,
                MathF.Cos(rotationOffset) * dir.Z
                        + MathF.Sin(rotationOffset) * dir.X);
            if (ignoreY) dir.Y = 0;
            return dir;
        }

        // Return the world position of the far-end of the AoE shape;
        //  for rectangular shape this is the midpoint on the farthest side
        protected static Vector3 CalcFarEndWorldPos(
            Vector3 origin, Vector3 direction, float length)
            => direction * length + origin;

        protected static (Vector3 P1, Vector3 P2, Vector3 P3, Vector3 P4)
            CalcRectCornersWorldPos(
                Vector3 nearEnd, Vector3 farEnd, Vector3 direction, float halfWidth)
            => (Vector3.Normalize(new(direction.Z, 0, -direction.X)) 
                    * halfWidth + nearEnd,
                Vector3.Normalize(new(direction.Z, 0, -direction.X)) 
                    * halfWidth + farEnd,
                Vector3.Normalize(new(-direction.Z, 0, direction.X)) 
                    * halfWidth + farEnd,
                Vector3.Normalize(new(-direction.Z, 0, direction.X)) 
                    * halfWidth + nearEnd);


        #region DrawComponent

        protected void DrawRect(ImDrawListPtr drawList, float width, 
            Vector3 nearEndWorldPos, Vector3 farEndWorldPos, Vector3 direction)
        {
#if DEBUG
            Projection.WorldToScreen(farEndWorldPos, out var pe, out var per);
            drawList.AddCircleFilled(pe, Style.Thickness * 2, RingColour);
#endif

            var w2 = width / 2;
            var (p1w, p2w, p3w, p4w) = CalcRectCornersWorldPos(
                nearEndWorldPos, farEndWorldPos, direction, w2);

            Projection.WorldToScreen(p1w, out var p1s, out var p1r);
            Projection.WorldToScreen(p2w, out var p2s, out var p2r);
            Projection.WorldToScreen(p3w, out var p3s, out var p3r);
            Projection.WorldToScreen(p4w, out var p4s, out var p4r);

            // don't draw the whole range if some of the points
            // may be projected to a weird position
            if (p1r.Z < -1 || p2r.Z < -1 || p3r.Z < -1 || p4r.Z < -1) return;

            if (Style.Filled)
                drawList.AddQuadFilled(p1s, p2s, p3s, p4s, FillColour);
            if (Style.OuterRing)
                StrokeInside(drawList, [p1s, p2s, p3s, p4s]);
        }

        // Stroke a closed polygon (screen space) with the outline lying entirely
        //  inside the shape, so that its outer edge is the border of the area.
        // With outward, the outline lies outside the polygon instead
        //  (e.g. the hole of a donut).
        protected void StrokeInside(ImDrawListPtr drawList,
            IReadOnlyList<Vector2> polygon, bool outward = false)
        {
            // Drop invalid and duplicate points
            var points = new List<Vector2>(polygon.Count);
            foreach (var p in polygon)
                if (!float.IsNaN(p.X) && !float.IsNaN(p.Y)
                    && (points.Count == 0 || Vector2.DistanceSquared(points[^1], p) > .0001f))
                    points.Add(p);
            if (points.Count > 1 && Vector2.DistanceSquared(points[0], points[^1]) <= .0001f)
                points.RemoveAt(points.Count - 1);
            var n = points.Count;
            if (n < 2) return;

            var area = 0f;
            for (var i = 0; i < n; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            // Interior is on the left of the edges (-dy, dx) when area > 0
            var sign = area >= 0 ? 1f : -1f;
            if (outward) sign = -sign;
            var half = Style.Thickness / 2f;

            for (var i = 0; i < n; i++)
            {
                var prev = points[(i - 1 + n) % n];
                var cur = points[i];
                var next = points[(i + 1) % n];
                var e1 = Vector2.Normalize(cur - prev);
                var e2 = Vector2.Normalize(next - cur);
                var n1 = sign * new Vector2(-e1.Y, e1.X);
                var n2 = sign * new Vector2(-e2.Y, e2.X);
                var m = n1 + n2;
                m = m.LengthSquared() < 1e-6f ? n1 : Vector2.Normalize(m);
                // Miter offset, limited at very sharp corners
                var dist = half / MathF.Max(Vector2.Dot(m, n1), .25f);
                drawList.PathLineTo(cur + m * dist);
            }
            drawList.PathStroke(RingColour, ImDrawFlags.Closed, Style.Thickness);
        }

        #endregion
    }
}
