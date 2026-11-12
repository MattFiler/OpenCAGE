using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// A small software picture of a body at its bind pose, front and side, with what a first-person cut takes in red and what it keeps in
    /// grey - the preview "Make First-Person Body" shows. No GPU, no viewport.
    /// </summary>
    public static class BodyPicture
    {
        private struct Tri { public Vector3 A, B, C; public bool Cut; }

        /// <summary>Several models drawn together at the rig's rest pose (a display model's parts: the body and what it holds).</summary>
        public static Bitmap DrawTogether(IEnumerable<Models.CS2> models, Skeleton rig, int width = 900, int height = 620, string caption = null)
        {
            Models.CS2 all = new Models.CS2() { Name = "together" };
            foreach (Models.CS2 model in models) if (model != null) all.Components.AddRange(model.Components);
            return Draw(all, null, rig, width, height, caption);
        }

        /// <summary>Front (looking at the face: -Z towards the viewer) and side (from the character's left) views side by side.</summary>
        public static Bitmap Draw(Models.CS2 model, FirstPersonCutResult plan, Skeleton rig, int width = 900, int height = 620, string caption = null)
        {
            List<Tri> tris = new List<Tri>();
            Dictionary<Models.CS2.Component.LOD.Submesh, FirstPersonCutResult.Piece> pieces = plan?.Pieces.ToDictionary(o => o.Source, o => o) ?? new Dictionary<Models.CS2.Component.LOD.Submesh, FirstPersonCutResult.Piece>();
            foreach (Models.CS2.Component component in model.Components)
            {
                if (component.LODs.Count == 0) continue;
                foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[0].Submeshes)
                {
                    cMesh mesh;
                    try { mesh = submesh.ToMesh(); } catch { continue; }
                    pieces.TryGetValue(submesh, out FirstPersonCutResult.Piece piece);
                    for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                    {
                        int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                        if (a >= mesh.Vertices.Count || b >= mesh.Vertices.Count || c >= mesh.Vertices.Count) continue;
                        bool cut = piece != null && (piece.Dropped || FirstPersonCut.Goes(piece.RemovedVertices, a, b, c, plan.Lenient));
                        tris.Add(new Tri() { A = mesh.Vertices[a], B = mesh.Vertices[b], C = mesh.Vertices[c], Cut = cut });
                    }
                }
            }
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) g.Clear(Color.FromArgb(28, 30, 34));
            if (tris.Count == 0) return bmp;

            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Tri t in tris) foreach (Vector3 p in new[] { t.A, t.B, t.C }) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            int top = caption == null ? 8 : 30;
            int panelW = width / 2, panelH = height - top - 8;
            float span = Math.Max(max.Y - min.Y, Math.Max(max.X - min.X, max.Z - min.Z) * 1.1f);
            float scale = Math.Min(panelW * 0.9f / Math.Max(Math.Max(max.X - min.X, max.Z - min.Z), 0.01f), panelH * 0.95f / Math.Max(max.Y - min.Y, 0.01f));

            int[] pixels = new int[width * height];
            float[] depth = Enumerable.Repeat(float.MaxValue, width * height).ToArray();
            BitmapData lockedRead = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(lockedRead.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(lockedRead);

            //front: screen x = +X (the character's left, +X, on the right of the picture, as when facing them), depth = Z (-Z is towards us)
            //side:  screen x = Z (front to the left), depth = -X (seen from the character's left)
            for (int view = 0; view < 2; view++)
            {
                float cx = view * panelW + panelW / 2f, baseY = top + panelH * 0.975f;
                float centreH = view == 0 ? (min.X + max.X) / 2f : (min.Z + max.Z) / 2f;
                Vector3 light = Vector3.Normalize(new Vector3(0.4f, 0.7f, -0.6f));
                foreach (Tri t in tris)
                {
                    Vector2 P(Vector3 v) => view == 0 ? new Vector2(cx + (v.X - centreH) * scale, baseY - (v.Y - min.Y) * scale) : new Vector2(cx + (v.Z - centreH) * scale, baseY - (v.Y - min.Y) * scale);
                    float D(Vector3 v) => view == 0 ? v.Z : -v.X;
                    Vector3 n = Vector3.Cross(t.B - t.A, t.C - t.A);
                    float len = n.Length();
                    float shade = len > 1e-12f ? Math.Abs(Vector3.Dot(n / len, light)) : 0.5f;
                    shade = 0.35f + 0.65f * shade;
                    Color col = t.Cut ? Color.FromArgb(255, (int)(235 * shade), (int)(60 * shade), (int)(50 * shade)) : Color.FromArgb(255, (int)(190 * shade), (int)(195 * shade), (int)(205 * shade));
                    Raster(P(t.A), P(t.B), P(t.C), D(t.A), D(t.B), D(t.C), col.ToArgb(), pixels, depth, width, height, view * panelW, (view + 1) * panelW);
                }
            }

            BitmapData locked = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
            bmp.UnlockBits(locked);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using (Font font = new Font("Segoe UI", 9f))
                using (Brush text = new SolidBrush(Color.FromArgb(220, 220, 225)))
                {
                    if (caption != null) g.DrawString(caption, font, text, 8, 6);
                    g.DrawString("front", font, text, 8, top);
                    g.DrawString("side", font, text, panelW + 8, top);
                }
                //the rule's torso heights, on both views
                if (plan != null)
                {
                    float baseY = top + panelH * 0.975f;
                    using (Pen front = new Pen(Color.FromArgb(160, 255, 200, 60)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                    using (Pen mid = new Pen(Color.FromArgb(120, 120, 200, 255)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                    {
                        float yFront = baseY - (plan.FrontY - min.Y) * scale, yMid = baseY - (plan.MidY - min.Y) * scale;
                        g.DrawLine(front, panelW, yFront, width, yFront);
                        g.DrawLine(mid, panelW, yMid, width, yMid);
                    }
                }
                if (rig != null)
                {
                    int head = FirstPersonCut.BoneIndex(rig, "HEAD");
                    if (head >= 0)
                    {
                        Vector3 h = rig.GetBindPose()[head].Translation;
                        float baseY = top + panelH * 0.975f;
                        for (int view = 0; view < 2; view++)
                        {
                            float cx = view * panelW + panelW / 2f;
                            float centreH = view == 0 ? (min.X + max.X) / 2f : (min.Z + max.Z) / 2f;
                            float x = view == 0 ? cx + (h.X - centreH) * scale : cx + (h.Z - centreH) * scale, y = baseY - (h.Y - min.Y) * scale;
                            using (Pen eye = new Pen(Color.FromArgb(255, 90, 220, 120), 2f)) g.DrawEllipse(eye, x - 5, y - 5, 10, 10);
                        }
                    }
                }
            }
            return bmp;
        }

        private static void Raster(Vector2 a, Vector2 b, Vector2 c, float da, float db, float dc, int colour, int[] pixels, float[] depth, int width, int height, int xMin, int xMax)
        {
            int minX = Math.Max(xMin, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)))), maxX = Math.Min(xMax - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)))), maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
            float area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (Math.Abs(area) < 1e-6f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / area;
                    float w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float d = w0 * da + w1 * db + w2 * dc;
                    int i = y * width + x;
                    if (d >= depth[i]) continue;
                    depth[i] = d;
                    pixels[i] = colour;
                }
        }
    }
}
