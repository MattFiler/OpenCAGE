using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Markup
{
    /// <summary>
    /// The markup OpenCAGE ships, by composite path: simple shapes in each composite's own space showing what it is and how it
    /// is meant to be placed. Cathode space: metres, Y up, the composite's forward +Z.
    /// </summary>
    public static class BuiltInMarkup
    {
        public static readonly Vector3 Green = new Vector3(0.537f, 0.776f, 0.380f);
        public static readonly Vector3 Blue = new Vector3(0.557f, 0.737f, 0.878f);
        public static readonly Vector3 Red = new Vector3(1f, 0.106f, 0.106f);
        public static readonly Vector3 White = new Vector3(1f, 1f, 1f);
        public static readonly Vector3 Yellow = new Vector3(1f, 0.969f, 0.455f);
        public static readonly Vector3 Orange = new Vector3(1f, 0.45f, 0.08f);
        public static readonly Vector3 Glass = new Vector3(0.82f, 0.91f, 0.97f);
        public static readonly Vector3 Grey = new Vector3(0.451f, 0.451f, 0.451f);
        public static readonly Vector3 Dark = new Vector3(0.09f, 0.09f, 0.09f);
        public static readonly Vector3 TraversalBlue = new Vector3(0.1f, 0.25f, 1f);

        /* How far in front of a melee context the surface the android throws the player against stands, measured to its face:
           high cover (a wall) and low cover (a desk or crate). */
        public const float HighCoverWallDistance = 2.5f;
        public const float LowCoverDistance = 1.25f;

        public sealed class Entry
        {
            public string Path;
            /// <summary>Other paths the same composite goes by in the shipped levels.</summary>
            public string[] Also = new string[0];
            public string Description;
            public Func<MarkupMesh> Build;
        }

        public static readonly Entry[] All = new Entry[]
        {
            new Entry() { Path = @"Archetypes\Jobs\MeleeContext\MeleeContext_HighCover", Description = "Android high cover grapple: the wall it throws the player against stands 2.5 m in front (the line and T).", Build = () => MeleeContext(HighCoverWallDistance, 0.175f) },
            new Entry() { Path = @"Archetypes\Jobs\MeleeContext\MeleeContext", Description = "Android low cover grapple: the surface it throws the player onto stands 1.25 m in front (the line and T).", Build = () => MeleeContext(LowCoverDistance, 0.5f) },
            new Entry() { Path = @"Archetypes\Jobs\MeleeContext\MeleeContext_CorpseTrap", Description = "Android corpse trap: where the player is grabbed by an android playing dead. It faces away from the android, which lies about 0.9 m behind it.", Build = CorpseTrap },
            new Entry() { Path = @"Archetypes\Jobs\Base\BaseSearchJob", Description = "Search job: a spot an NPC searches at.", Build = SearchJob },
            new Entry() { Path = @"Archetypes\Jobs\Base\BaseAreaSweepJob", Description = "Area sweep job: a spot an NPC sweeps over.", Build = AreaSweepJob },
            new Entry() { Path = @"Archetypes\Jobs\Base\BaseIdleJob", Description = "Idle job: a spot an NPC waits at.", Build = IdleJob },
            new Entry() { Path = @"Archetypes\Jobs\Base\BaseFollow", Description = "Follow job: one NPC following another.", Build = Follow },
            new Entry() { Path = @"Archetypes\Gameplay\Container", Also = new[] { @"Archetypes\Gameplay\Container_Script" }, Description = "Container: something the player loots.", Build = Container },
            new Entry() { Path = @"Archetypes\Gameplay\Fire_Volume", Description = "Fire volume.", Build = FireVolume },
            new Entry() { Path = @"CageOnly\front_marker", Description = "Front marker (in door packages): the word FRONT on the floor, reading the right way round from the front.", Build = () => FrontLabel(TraversalBlue) },

            //Who an NPC composite spawns, named on the floor
            Label(@"Archetypes\NPCs\Alien\Xenomorph_NPC", "XENOMORPH", null),
            Label(@"Archetypes\NPCs\Alien\Facehugger", "FACEHUGGER", null),
            Label(@"Archetypes\NPCs\Alien\Egg", "EGG", null),
            Label(@"Archetypes\NPCs\Android\Android_NPC", "ANDROID", null),
            Label(@"Archetypes\NPCs\Android\Android_Heavy_NPC", "HEAVY ANDROID", null),
            Label(@"Archetypes\NPCs\Human\Civilian\Worker_NPC", "WORKER", null),
            Label(@"Archetypes\NPCs\Human\Civilian\Worker_NPC_Pistol", "WORKER", "PISTOL"),
            Label(@"Archetypes\NPCs\Human\Civilian\Worker_NPC_Shotgun", "WORKER", "SHOTGUN"),
            Label(@"Archetypes\NPCs\Human\Security\Grunt_NPC_Pistol", "SECURITY", "PISTOL"),
            Label(@"Archetypes\NPCs\Human\Security\Grunt_NPC_Shotgun", "SECURITY", "SHOTGUN"),
            Label(@"Archetypes\NPCs\Human\Riot\Riot_NPC_Pistol", "RIOT", "PISTOL"),
            Label(@"Archetypes\NPCs\Human\Riot\Riot_NPC_Shotgun", "RIOT", "SHOTGUN"),
            Label(@"Archetypes\NPCs\Human\Riot\Civilian_Riot_NPC_Pistol", "RIOT CIVILIAN", "PISTOL"),
            Label(@"Archetypes\NPCs\Human\Riot\Civilian_Riot_NPC_Shotgun", "RIOT CIVILIAN", "SHOTGUN"),
            Label(@"Archetypes\NPCs\Actors\Axel", "AXEL", null),
            Label(@"Archetypes\NPCs\Actors\Connor", "CONNOR", null),
            Label(@"Archetypes\NPCs\Actors\Marlow", "MARLOW", null),
            Label(@"Archetypes\NPCs\Actors\Ricardo", "RICARDO", null),
            Label(@"Archetypes\NPCs\Actors\Samuels", "SAMUELS", null),
            Label(@"Archetypes\NPCs\Actors\Taylor", "TAYLOR", null),
            Label(@"Archetypes\NPCs\Actors\Verlaine", "VERLAINE", null),
            Label(@"Archetypes\NPCs\Actors\Waits", "WAITS", null),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_FOSTER", "FOSTER", "SPACESUIT"),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_HEYST", "HEYST", "SPACESUIT"),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_LEWIS", "LEWIS", "SPACESUIT"),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_MARLOW", "MARLOW", "SPACESUIT"),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_MEEKS", "MEEKS", "SPACESUIT"),
            Label(@"Archetypes\NPCs\Actors\SPACESUIT_PLAYER", "PLAYER", "SPACESUIT"),
            Label(@"Archetypes\Script\Mission\SpawnPositionSelect", "PLAYER", null),
        };

        public static readonly Vector3 TextBlue = new Vector3(0.30f, 0.60f, 1f);

        /* The ring every marker stands on, and who the composite spawns written on the floor behind it */
        private static Entry Label(string path, string title, string subtitle) => new Entry()
        {
            Path = path,
            Description = "Labelled " + title + (subtitle == null ? "" : " " + subtitle) + ": who this composite spawns.",
            Build = () =>
            {
                MarkupMesh m = new MarkupMesh(), puck = new MarkupMesh();
                BasePuck(puck);
                //Turned round so the bar points the way the character faces
                m.Append(puck, Matrix4x4.CreateRotationY((float)Math.PI));
                MarkupText.FloorText(m, TextBlue, title, subtitle, -0.26f);
                return m;
            },
        };

        private static readonly Dictionary<string, Entry> _byPath = All.SelectMany(o => new[] { o.Path }.Concat(o.Also).Select(p => (Path: Normalise(p), Entry: o))).ToDictionary(o => o.Path, o => o.Entry, StringComparer.OrdinalIgnoreCase);

        public static string Normalise(string path) => (path ?? "").Replace('/', '\\').Trim('\\', ' ');

        public static Entry Find(string compositePath) => compositePath != null && _byPath.TryGetValue(Normalise(compositePath), out Entry entry) ? entry : null;

        #region Shapes
        private static readonly Vector3 X = Vector3.UnitX, Y = Vector3.UnitY, Z = Vector3.UnitZ;

        /* The icons stand across the composite, facing its front and back: drawn along +X and +Y (up), seen from either side */
        private const float IconHalf = 0.0145f;
        private static readonly Vector3 IconN = -Z;
        private static void Icon(MarkupMesh m, Vector3 colour, IList<Vector2> outline, float half = IconHalf) => m.Extrude(colour, Vector3.Zero, X, Y, IconN, outline, -half, half);
        private static void IconStroke(MarkupMesh m, Vector3 colour, Vector2 a, Vector2 b, float width, float half = IconHalf) => m.Stroke(colour, Vector3.Zero, X, Y, IconN, a, b, width, -half, half);
        private static void IconDisc(MarkupMesh m, Vector3 colour, Vector2 centre, float radius, float half = IconHalf) => m.Prism(colour, Vector3.Zero, X, Y, IconN, MarkupMesh.Polygon(centre, radius, 16), -half, half);

        /// <summary>
        /// The ring every job and context stands on: 0.42 m across, a bar from its back edge to just past the middle.
        /// </summary>
        public static void BasePuck(MarkupMesh m)
        {
            m.Annulus(Green, Vector3.Zero, X, -Z, Y, Vector2.Zero, 0.225f, 0.182f, 8, -0.015f, 0.015f, 22.5f);
            m.Box(Green, new Vector3(-0.0177f, -0.015f, -0.185f), new Vector3(0.0177f, 0.0106f, -0.141f));
            m.Box(Green, new Vector3(-0.0304f, 0.0089f, -0.141f), new Vector3(0.0304f, 0.0289f, 0.034f));
            m.Prism(Green, Vector3.Zero, X, -Z, Y, MarkupMesh.Polygon(new Vector2(0, -0.034f), 0.0304f, 8), 0.0089f, 0.0289f);
        }

        /// <summary>
        /// A melee context: a line along the floor to a T whose near face is where the surface the android throws the player
        /// against stands. These two distances must stay exact.
        /// </summary>
        public static MarkupMesh MeleeContext(float distance, float height)
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            Fist(m);

            const float half = 0.020422f;
            m.Box(Green, new Vector3(-half, 0, 0.17116f), new Vector3(half, 0.010606f, distance - 0.01f));
            //The T, 1 cm thick: its near face at the distance less a centimetre, its far face at the distance
            float bar = height - 0.027364f;
            m.Box(Green, new Vector3(-half, 0, distance - 0.01f), new Vector3(half, bar, distance));
            m.Prism(Green, Vector3.Zero, X, Y, Z, new[] { new Vector2(-0.137636f, bar), new Vector2(0.137636f, bar), new Vector2(0.165f, height), new Vector2(-0.165f, height) }, distance - 0.01f, distance);
            return m;
        }

        /* A raised fist: four fingers curled over the back of the hand, the thumb across them, the wrist below */
        private static void Fist(MarkupMesh m)
        {
            Icon(m, Blue, new[] { new Vector2(-0.095f, 0.30f), new Vector2(0.095f, 0.30f), new Vector2(0.10f, 0.42f), new Vector2(-0.10f, 0.42f) });
            float[] tops = { 0.485f, 0.50f, 0.495f, 0.47f };
            for (int i = 0; i < 4; i++)
            {
                float u0 = -0.10f + i * 0.05f, u1 = u0 + 0.045f;
                Icon(m, Blue, new[] { new Vector2(u0, 0.41f), new Vector2(u1, 0.41f), new Vector2(u1, tops[i] - 0.012f), new Vector2(u1 - 0.008f, tops[i]), new Vector2(u0 + 0.008f, tops[i]), new Vector2(u0, tops[i] - 0.012f) });
            }
            IconStroke(m, White, new Vector2(-0.105f, 0.395f), new Vector2(0.035f, 0.38f), 0.042f, IconHalf * 1.25f);
            Icon(m, Blue, new[] { new Vector2(-0.065f, 0.20f), new Vector2(0.065f, 0.20f), new Vector2(0.07f, 0.305f), new Vector2(-0.07f, 0.305f) });
        }

        /* An android playing dead: a figure lying on its back, and a warning mark over it */
        public static MarkupMesh CorpseTrap()
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            const float y = 0.13f;
            IconDisc(m, Blue, new Vector2(-0.175f, y + 0.012f), 0.042f);
            IconStroke(m, Blue, new Vector2(-0.125f, y), new Vector2(0.045f, y), 0.068f);
            IconStroke(m, Blue, new Vector2(0.04f, y), new Vector2(0.20f, y + 0.035f), 0.046f);
            IconStroke(m, Blue, new Vector2(0.04f, y - 0.01f), new Vector2(0.19f, y - 0.03f), 0.046f);
            IconStroke(m, Blue, new Vector2(-0.10f, y + 0.02f), new Vector2(0.0f, y + 0.07f), 0.03f);
            //The warning mark: a bar and its dot
            IconStroke(m, Red, new Vector2(0, 0.47f), new Vector2(0, 0.31f), 0.042f);
            IconDisc(m, Red, new Vector2(0, 0.255f), 0.024f);
            return m;
        }

        /* A magnifying glass: a round lens up front, its handle down and back */
        public static MarkupMesh SearchJob()
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            Vector2 lens = new Vector2(0.045f, 0.40f);
            m.Annulus(Blue, Vector3.Zero, X, Y, IconN, lens, 0.120f, 0.090f, 24, -IconHalf, IconHalf);
            IconDisc(m, Glass, lens, 0.091f, 0.006f);
            //The handle: a neck from the rim, then the grip with a rounded end
            Vector2 down = Vector2.Normalize(new Vector2(-0.6f, -0.8f));
            Vector2 rim = lens + down * 0.118f, neck = rim + down * 0.045f, end = neck + down * 0.14f;
            IconStroke(m, Blue, rim, neck, 0.034f);
            IconStroke(m, Blue, neck, end, 0.058f);
            IconDisc(m, Blue, end, 0.029f);
            return m;
        }

        /* A broom leaning back: the handle, the binding, and a fan of bristles at the front */
        public static MarkupMesh AreaSweepJob()
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            Vector2 top = new Vector2(-0.14f, 0.66f), foot = new Vector2(0.03f, 0.25f);
            Vector2 along = Vector2.Normalize(foot - top), across = new Vector2(-along.Y, along.X);
            IconStroke(m, Blue, top, foot, 0.026f);
            IconDisc(m, Blue, top, 0.013f);
            //The binding, then the bristles widening to the floor
            Vector2 band = foot + along * 0.035f, tip = band + along * 0.17f;
            Icon(m, Blue, new[] { foot - across * 0.045f, foot + across * 0.045f, band + across * 0.05f, band - across * 0.05f });
            Icon(m, Yellow, new[] { band - across * 0.05f, band + across * 0.05f, tip + across * 0.115f, tip - across * 0.115f }, IconHalf * 0.8f);
            for (int i = -2; i <= 2; i++)
                IconStroke(m, Blue, band + across * (0.02f * i), tip + across * (0.045f * i), 0.006f, IconHalf);
            return m;
        }

        /* An hourglass: two plates, two posts, the glass between and the sand running through */
        public static MarkupMesh IdleJob()
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            const float y0 = 0.07f, y1 = 0.56f, w = 0.13f;
            Icon(m, Blue, new[] { new Vector2(-w, y1 - 0.035f), new Vector2(w, y1 - 0.035f), new Vector2(w, y1), new Vector2(-w, y1) });
            Icon(m, Blue, new[] { new Vector2(-w, y0), new Vector2(w, y0), new Vector2(w, y0 + 0.035f), new Vector2(-w, y0 + 0.035f) });
            IconStroke(m, Blue, new Vector2(-0.112f, y0 + 0.035f), new Vector2(-0.112f, y1 - 0.035f), 0.018f);
            IconStroke(m, Blue, new Vector2(0.112f, y0 + 0.035f), new Vector2(0.112f, y1 - 0.035f), 0.018f);

            float mid = (y0 + y1) / 2, g0 = y0 + 0.035f, g1 = y1 - 0.035f;
            Vector2[] glass =
            {
                new Vector2(-0.085f, g1), new Vector2(-0.085f, g1 - 0.045f), new Vector2(-0.016f, mid + 0.012f), new Vector2(-0.016f, mid - 0.012f),
                new Vector2(-0.085f, g0 + 0.045f), new Vector2(-0.085f, g0), new Vector2(0.085f, g0), new Vector2(0.085f, g0 + 0.045f),
                new Vector2(0.016f, mid - 0.012f), new Vector2(0.016f, mid + 0.012f), new Vector2(0.085f, g1 - 0.045f), new Vector2(0.085f, g1),
            };
            Icon(m, Glass, glass, 0.007f);
            //Sand: what's left at the top, the stream, and the pile below
            Icon(m, Yellow, new[] { new Vector2(-0.055f, mid + 0.10f), new Vector2(0.055f, mid + 0.10f), new Vector2(0.010f, mid + 0.02f), new Vector2(-0.010f, mid + 0.02f) }, 0.0095f);
            IconStroke(m, Yellow, new Vector2(0, mid + 0.02f), new Vector2(0, g0 + 0.07f), 0.005f, 0.0095f);
            Icon(m, Yellow, new[] { new Vector2(-0.080f, g0 + 0.004f), new Vector2(0.080f, g0 + 0.004f), new Vector2(0.025f, g0 + 0.065f), new Vector2(-0.025f, g0 + 0.065f) }, 0.0095f);
            return m;
        }

        /// <summary>A person walking towards +X, seen from the front, standing on the floor at <paramref name="x"/>.</summary>
        private static void Walker(MarkupMesh m, Vector3 colour, float x, float scale)
        {
            Vector2 P(float u, float v) => new Vector2(x + u * scale, 0.06f + v * scale);
            IconDisc(m, colour, P(0.01f, 0.47f), 0.046f * scale);
            IconStroke(m, colour, P(0.0f, 0.41f), P(-0.012f, 0.25f), 0.072f * scale);
            IconStroke(m, colour, P(-0.012f, 0.26f), P(0.07f, 0.02f), 0.05f * scale);
            IconStroke(m, colour, P(-0.012f, 0.26f), P(-0.085f, 0.02f), 0.05f * scale);
            IconStroke(m, colour, P(0.0f, 0.39f), P(0.075f, 0.27f), 0.034f * scale);
            IconStroke(m, colour, P(0.0f, 0.39f), P(-0.07f, 0.28f), 0.034f * scale);
        }

        /* One person following another, and an arrow over them showing the way they walk */
        public static MarkupMesh Follow()
        {
            MarkupMesh m = new MarkupMesh();
            BasePuck(m);
            Walker(m, White, 0.11f, 1f);
            Walker(m, Blue, -0.12f, 0.92f);
            const float y = 0.63f;
            m.Box(Red, new Vector3(-0.13f, y - 0.012f, -0.012f), new Vector3(0.08f, y + 0.012f, 0.012f));
            m.Append(ArrowHead(Red, 0.035f, 0.07f), Matrix4x4.CreateRotationY((float)Math.PI / 2) * Matrix4x4.CreateTranslation(0.08f, y, 0));
            return m;
        }

        /// <summary>A four-sided arrowhead with its base at the origin and its tip at +Z length.</summary>
        private static MarkupMesh ArrowHead(Vector3 colour, float halfWidth, float length)
        {
            MarkupMesh head = new MarkupMesh();
            MarkupMesh.Surface s = head.In(colour);
            s.Vertices.AddRange(new[] { new Vector3(-halfWidth, -halfWidth, 0), new Vector3(halfWidth, -halfWidth, 0), new Vector3(halfWidth, halfWidth, 0), new Vector3(-halfWidth, halfWidth, 0), new Vector3(0, 0, length) });
            s.Indices.AddRange(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 });
            return head;
        }

        /* A closed box held up in front, a line and arrow from the composite's origin pointing at it */
        public static MarkupMesh Container()
        {
            MarkupMesh m = new MarkupMesh();
            m.Stroke(Green, Vector3.Zero, Z, Y, X, new Vector2(0.0106f, 0.0106f), new Vector2(0.21f, 0.21f), 0.03f, -0.015f, 0.015f);
            m.Append(ArrowHead(Green, 0.03f, 0.075f), Matrix4x4.CreateRotationX(-(float)Math.PI / 4) * Matrix4x4.CreateTranslation(0, 0.21f, 0.21f));
            //The box, its lid's edge round the top and a strip of tape over it
            const float x = 0.13f, y0 = 0.28f, y1 = 0.50f, z0 = 0.30f, z1 = 0.54f;
            m.Box(Green, new Vector3(-x, y0, z0), new Vector3(x, y1, z1));
            m.Box(Yellow, new Vector3(-x - 0.004f, y1 - 0.035f, z0 - 0.004f), new Vector3(x + 0.004f, y1 - 0.025f, z1 + 0.004f));
            m.Box(Yellow, new Vector3(-0.025f, y1, z0 - 0.004f), new Vector3(0.025f, y1 + 0.005f, z1 + 0.004f));
            m.Box(Yellow, new Vector3(-0.025f, y1 - 0.06f, z0 - 0.005f), new Vector3(0.025f, y1, z0));
            m.Box(Yellow, new Vector3(-0.025f, y1 - 0.06f, z1), new Vector3(0.025f, y1, z1 + 0.005f));
            return m;
        }

        /* A flame: an orange tongue curling at its tip, a yellow core, drawn across both side planes so it reads from all round */
        public static MarkupMesh FireVolume()
        {
            MarkupMesh m = new MarkupMesh();
            Vector2[] Tongue(float width, float height, float bottom, float lean)
            {
                const int steps = 32;
                Vector2[] points = new Vector2[steps];
                for (int i = 0; i < steps; i++)
                {
                    double t = 2 * Math.PI * i / steps;
                    float y = (float)(1 - Math.Cos(t)) / 2;
                    float xx = width * (float)(Math.Sin(t) * Math.Pow(Math.Sin(t / 2), 1.3));
                    float yy = 1 - y;
                    points[i] = new Vector2(xx + lean * yy * yy * yy, bottom + height * yy);
                }
                return points;
            }
            Vector2[] outer = Tongue(0.13f, 0.52f, 0f, 0.07f);
            Vector2[] inner = Tongue(0.07f, 0.29f, 0.012f, 0.035f);
            foreach ((Vector3 u, Vector3 n) in new[] { (X, Z), (Z, X) })
            {
                m.Extrude(Orange, Vector3.Zero, u, Y, n, outer, -0.010f, 0.010f);
                m.Extrude(Yellow, Vector3.Zero, u, Y, n, inner, -0.014f, 0.014f);
            }
            return m;
        }

        /// <summary>
        /// The word FRONT standing on the floor, 0.42 m long and 0.09 m tall, along +X: it reads the right way round from -Z.
        /// </summary>
        public static MarkupMesh FrontLabel(Vector3 colour)
        {
            MarkupMesh m = new MarkupMesh();
            const float height = 0.09f, bottom = 0.003f;
            MarkupText.Write(m, colour, "FRONT", new Vector2(0, bottom + height / 2), height, X, Y, Z, -0.0025f, 0.0025f);
            return m;
        }
        #endregion
    }
}
