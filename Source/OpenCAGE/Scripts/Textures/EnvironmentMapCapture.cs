using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using AlienPAK;
using CathodeLib;
using OpenCAGE.MCP;
using OpenCAGE.RuntimeUtilsConnection;
using OpenCAGE.TextureTools;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AssetKind = CathodeLib.AssetModificationInfoTable.AssetKind;
using static CATHODE.Textures;

namespace OpenCAGE
{
    /// <summary>
    /// Capturing an EnvironmentMap entity's cubemap in the running game (Live Link): the game renders the six faces from
    /// where the entity is placed (LiveLink.CaptureCubemap, CUBEMAP_CAPTURE in the runtime utils), they are built into a
    /// cube texture shaped as the game's own (the size and format of the cubemap it replaces, else 128 x 128 faces and
    /// DXT1, unless Misc &gt; Cubemap Capture Size picks a size; levels down to 2 x 2, each blurred more widely than the
    /// last - see <see cref="CubemapFilter"/>), imported into the level's textures, and the entity's Texture is pointed at it, as
    /// one undo step. The cubemap it used before is deleted when nothing else uses it: no other script names it, no
    /// material binds it and it is not one of GLOBAL's (which every level carries a copy of, and would bring it back).
    /// Movers (the static models drawn with the level) carry the cubemap they were given at the last Save &amp; Build; the
    /// ones still carrying the old one when nothing else names it are given the new one in the same step, so a plain save
    /// writes them right. Otherwise their cubemaps are worked out again by the next Save &amp; Build.
    ///
    /// The game reads cubemaps when it loads the level, so it shows the new one once the level is saved and loaded again.
    /// </summary>
    internal static class EnvironmentMapCapture
    {
        /// <summary>The shape new cubemaps take when there is none to follow: the game's own (every level's are 128 x 128, nearly all DXT1, seven levels).</summary>
        public const int DefaultFaceSize = 128;
        public const TextureFormat DefaultFormat = TextureFormat.DXT1;
        /// <summary>The game's cubemaps stop at 2 x 2.</summary>
        public const int SmallestMip = 2;
        /// <summary>The fewest frames a face is held before it is taken: fewer can catch the view before.</summary>
        public const int MinimumSettleFrames = 4;

        /// <summary>
        /// The face sizes Misc &gt; Cubemap Capture Size offers, 0 being the size of the cubemap each capture replaces. Any
        /// size works in the game, which picks a level by how glossy the surface is, counting from the top: a bigger cubemap
        /// adds a sharper top level for mirror-like surfaces and leaves the blurrier ones as they were.
        /// </summary>
        public static readonly int[] FaceSizeChoices = { 0, 64, 128, 256, 512 };

        /// <summary>Misc &gt; Cubemap Capture Size: the face size to capture at, 0 for the size of the cubemap being replaced.</summary>
        public static int ConfiguredFaceSize
        {
            get
            {
                int size = SettingsManager.GetInteger(Settings.CubemapCaptureSize);
                return IsFaceSize(size) ? size : 0;
            }
        }

        /// <summary>Whether a face size can be captured at: a power of two from 16 to 1024.</summary>
        public static bool IsFaceSize(int size)
        {
            return size >= 16 && size <= 1024 && (size & (size - 1)) == 0;
        }

        public sealed class Options
        {
            /// <summary>Face size: 0 takes the size of the cubemap being replaced (or <see cref="DefaultFaceSize"/>).</summary>
            public int FaceSize = 0;
            /// <summary>Null takes the format of the cubemap being replaced (or <see cref="DefaultFormat"/>).</summary>
            public TextureFormat? Format = null;
            /// <summary>Frames the game holds each face before taking it.</summary>
            public int SettleFrames = 8;
            /// <summary>The longest the game waits for the zones around the point to finish streaming in.</summary>
            public int StreamTimeoutMs = 30000;
            public bool KeepHud = false;
            public bool KeepPostEffects = false;
            /// <summary>Delete the cubemap the entity used before when nothing else uses it.</summary>
            public bool DeleteOld = true;
            /// <summary>A folder to keep the six faces the game took in, as PNGs (posx.png, negx.png... as they came), or null.</summary>
            public string FacesFolder = null;
        }

        public sealed class Outcome
        {
            public bool Ok;
            /// <summary>What happened, for the status bar or a message box.</summary>
            public string Message;
            public TEX4 Texture;
            public TEX4 Previous;
            public bool PreviousDeleted;
            public int MoversRepointed;
            /// <summary>Models will only reflect it after a Save &amp; Build (they carry the cubemap the last build gave them).</summary>
            public bool NeedsBuild;
            public Vector3 Position;
            public LiveLink.CubemapCapture Capture;
        }

        /// <summary>Whether an entity is an EnvironmentMap (a function entity of that type).</summary>
        public static bool IsEnvironmentMap(Entity entity)
        {
            return entity is FunctionEntity function && function.function == FunctionType.EnvironmentMap;
        }

        #region WHERE
        /// <summary>
        /// Where the entity is in the world: down the instance path from the level's root (composite instance entity ids,
        /// as the composite on screen was reached), aliases that move what they point at included. With no path, the one
        /// place the composite is placed in the level, if it is placed exactly once.
        /// </summary>
        public static bool TryWorldPosition(Commands commands, Composite composite, Entity entity, IList<ShortGuid> path, out Vector3 position, out string problem)
        {
            position = Vector3.Zero;
            problem = null;
            Composite root = commands?.EntryPoints?[0];
            if (root == null || composite == null || entity == null)
            {
                problem = "No level is open";
                return false;
            }

            List<Entity> chain = new List<Entity>();
            if (path != null || composite == root)
            {
                Composite at = root;
                foreach (ShortGuid id in path ?? new List<ShortGuid>())
                {
                    Entity step = at.GetEntityByID(id);
                    Composite next = step == null ? null : McpScript.InstancedComposite(commands, step);
                    if (next == null)
                    {
                        problem = "The way this composite was opened no longer leads to it from the level's root";
                        return false;
                    }
                    chain.Add(step);
                    at = next;
                }
                if (at != composite)
                {
                    problem = "The way this composite was opened does not lead to it from the level's root";
                    return false;
                }
            }
            else
            {
                List<List<Entity>> placements = Placements(commands, root, composite, 2);
                if (placements.Count == 0)
                {
                    problem = composite.name + " is not placed in the level, so it has nowhere to capture from";
                    return false;
                }
                if (placements.Count > 1)
                {
                    problem = composite.name + " is placed more than once in the level: open it from the level's root (stepping into the instance you want) to say which one to capture from";
                    return false;
                }
                chain = placements[0];
            }
            chain.Add(entity);

            McpPlacements.Placement placement = new McpPlacements(commands) { PlaceUnpositioned = true }.Evaluate(root, chain);
            if (placement.World == null)
            {
                problem = "The entity has no position to capture from";
                return false;
            }
            position = placement.World.position;
            return true;
        }

        /// <summary>The instance chains from the root to each placement of a composite (at most max of them).</summary>
        private static List<List<Entity>> Placements(Commands commands, Composite root, Composite target, int max)
        {
            //Only descend into composites that hold the target somewhere below them
            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite, HashSet<Composite> visiting)
            {
                if (composite == target) return true;
                if (holds.TryGetValue(composite, out bool known)) return known;
                if (!visiting.Add(composite)) return false;
                bool found = false;
                foreach (FunctionEntity function in composite.functions)
                {
                    Composite child = function.function.IsFunctionType ? null : commands.GetComposite(function.function);
                    if (child != null && Holds(child, visiting)) { found = true; break; }
                }
                visiting.Remove(composite);
                holds[composite] = found;
                return found;
            }

            List<List<Entity>> found_ = new List<List<Entity>>();
            void Walk(Composite composite, List<Entity> chain, int depth)
            {
                if (found_.Count >= max || depth > 64) return;
                foreach (FunctionEntity function in composite.functions)
                {
                    Composite child = function.function.IsFunctionType ? null : commands.GetComposite(function.function);
                    if (child == null || !Holds(child, new HashSet<Composite>())) continue;
                    List<Entity> next = new List<Entity>(chain) { function };
                    if (child == target) found_.Add(next);
                    else Walk(child, next, depth + 1);
                    if (found_.Count >= max) return;
                }
            }
            Walk(root, new List<Entity>(), 0);
            return found_;
        }
        #endregion

        #region THE CUBEMAP
        /// <summary>The cubemap an EnvironmentMap's Texture names (the level's own copy first, then GLOBAL's), or null.</summary>
        public static TEX4 CurrentCubemap(Level level, Entity entity, out bool global)
        {
            global = false;
            string path = (entity?.GetParameter(ShortGuids.Texture)?.content as cString)?.value;
            if (string.IsNullOrWhiteSpace(path) || level == null)
                return null;
            TEX4 texture = level.Textures?.GetEnvironmentMapByPath(path);
            if (texture != null)
                return texture;
            texture = level.Global?.Textures?.GetEnvironmentMapByPath(path);
            global = texture != null;
            return texture;
        }

        /// <summary>
        /// A name for a new cubemap the way the game names its own: ayz\env\cubemaps\level\composite\composite_entity.dds,
        /// with a number on the end when that is taken.
        /// </summary>
        public static string NewName(Level level, Composite composite, Entity entity)
        {
            string levelName = (level?.Name ?? "level").Replace('/', '\\');
            levelName = levelName.Substring(levelName.LastIndexOf('\\') + 1);
            string compositeName = composite?.name ?? "composite";
            compositeName = compositeName.Replace('/', '\\');
            compositeName = compositeName.Substring(compositeName.LastIndexOf('\\') + 1);
            string entityName = level?.Commands?.Utils?.GetEntityName(composite, entity) ?? entity.shortGUID.ToByteString();
            string Clean(string part) => new string(part.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? char.ToLowerInvariant(c) : '_').ToArray());
            string stem = "ayz\\env\\cubemaps\\" + Clean(levelName) + "\\" + Clean(compositeName) + "\\" + Clean(compositeName) + "_" + Clean(entityName);
            HashSet<string> taken = new HashSet<string>((level?.Textures?.Entries ?? new List<TEX4>()).Where(o => o?.Name != null).Select(o => AssetName.Normalise(o.Name).ToLowerInvariant()));
            foreach (TEX4 texture in level?.Global?.Textures?.Entries ?? new List<TEX4>())
                if (texture?.Name != null) taken.Add(AssetName.Normalise(texture.Name).ToLowerInvariant());
            string name = stem + ".dds";
            for (int i = 2; taken.Contains(name); i++)
                name = stem + "_" + i + ".dds";
            return name;
        }

        /// <summary>
        /// A cube texture from six captured faces (+X, -X, +Y, -Y, +Z, -Z; size x size RGBA as the game showed them), in the
        /// game's shape: each face's mips blurred as the game's own are (<see cref="CubemapFilter"/>), down to 2 x 2, then
        /// compressed to the format. Null, with why, if it could not be made.
        /// </summary>
        public static TEX4 BuildTexture(string name, byte[][] faces, int size, TextureFormat format, out string problem)
        {
            problem = null;
            if (faces == null || faces.Length != 6 || size < SmallestMip || faces.Any(o => o == null || o.Length < size * size * 4))
            {
                problem = "The game did not send six whole faces";
                return null;
            }

            //Every level of every face, uncompressed: the faces one after another, each with its own chain, as DDS keeps a cube
            List<byte[][]> chain = CubemapFilter.MipChain(faces, size, SmallestMip);
            int levels = chain.Count;
            using (MemoryStream content = new MemoryStream())
            {
                for (int face = 0; face < 6; face++)
                    for (int level = 0; level < levels; level++)
                    {
                        byte[] rgba = chain[level][face];
                        //A8R8G8B8 is B, G, R, A in memory
                        byte[] bgra = new byte[rgba.Length];
                        for (int i = 0; i < rgba.Length; i += 4)
                        {
                            bgra[i] = rgba[i + 2];
                            bgra[i + 1] = rgba[i + 1];
                            bgra[i + 2] = rgba[i];
                            bgra[i + 3] = rgba[i + 3];
                        }
                        content.Write(bgra, 0, bgra.Length);
                    }

                TEX4 uncompressed = new TEX4() { Name = name, Format = TextureFormat.A8R8G8B8, StateFlags = TextureStateFlag.CUBE };
                TEX4.Texture part = new TEX4.Texture() { Width = (short)size, Height = (short)size, Depth = 1, MipLevels = (short)levels, Content = content.ToArray() };
                byte[] dds = DdsFile.Write(uncompressed, part);

                //Compressed by texconv, which keeps the chain it is given (the blurred levels) rather than making its own
                byte[] converted;
                string scratch = Path.Combine(Path.GetTempPath(), "OpenCAGE_cubemap_" + Guid.NewGuid().ToString("N") + ".dds");
                try
                {
                    File.WriteAllBytes(scratch, dds);
                    converted = TextureConverter.Convert(scratch, format, levels, out problem);
                }
                finally
                {
                    try { File.Delete(scratch); } catch { }
                }
                if (converted == null)
                    return null;

                TEX4 texture = new TEX4() { Name = name };
                TEX4.Texture read = converted.ToTEX4Part(out texture.Format, out texture.StateFlags, out texture.UsageFlags);
                if (read == null || !texture.StateFlags.HasFlag(TextureStateFlag.CUBE) || read.MipLevels != levels)
                {
                    problem = "The compressed cubemap came back in the wrong shape";
                    return null;
                }
                //As the game's own: sampled as sRGB, a cube, and marked see-through (every shipped cubemap is), in the level's pack
                texture.Format = format;
                texture.StateFlags = TextureStateFlag.ALLOW_SRGB | TextureStateFlag.CUBE | TextureStateFlag.NON_SOLID;
                texture.UsageFlags = TextureUsageFlag.IS_LEVEL_PACK;
                EditTexture.ApplyParts(texture, read, 0, false);
                return texture;
            }
        }
        #endregion

        #region CAPTURE
        /// <summary>
        /// Capture the entity's cubemap in the running game and use it (UI thread; the capture itself is awaited). The entity
        /// is found in the world down the given instance path (see <see cref="TryWorldPosition"/>).
        /// </summary>
        public static async Task<Outcome> CaptureAsync(Level level, Composite composite, Entity entity, IList<ShortGuid> path, Options options, Action<string> progress = null)
        {
            using (Hold())
                return await CaptureOneAsync(level, composite, entity, path, options, progress);
        }

        private static async Task<Outcome> CaptureOneAsync(Level level, Composite composite, Entity entity, IList<ShortGuid> path, Options options, Action<string> progress)
        {
            Prepared prepared = await PrepareAsync(level, composite, entity, path, options, progress);
            return prepared.Failed ?? Apply(prepared, options);
        }

        /// <summary>A cubemap captured and built, waiting to be put in the level (<see cref="Apply"/>).</summary>
        public sealed class Prepared
        {
            /// <summary>Set when it went no further: why.</summary>
            public Outcome Failed;
            public Level Level;
            public Composite Composite;
            public Entity Entity;
            public string EntityName;
            public Vector3 Position;
            public LiveLink.CubemapCapture Capture;
            public TEX4 Texture;
        }

        /// <summary>
        /// The capture and the building of the texture, which take a while (UI thread; awaits the game and a worker thread).
        /// Nothing in the level is changed here.
        /// </summary>
        public static async Task<Prepared> PrepareAsync(Level level, Composite composite, Entity entity, IList<ShortGuid> path, Options options, Action<string> progress = null)
        {
            options = options ?? new Options();
            Outcome outcome = new Outcome();
            Commands commands = level?.Commands;
            string entityName = commands?.Utils?.GetEntityName(composite, entity) ?? entity?.shortGUID.ToByteString() ?? "?";
            Prepared prepared = new Prepared() { Level = level, Composite = composite, Entity = entity, EntityName = entityName };
            if (!IsEnvironmentMap(entity))
                return Fails(prepared, outcome, entityName + " is not an EnvironmentMap");
            if (!LiveLink.Connected)
                return Fails(prepared, outcome, "The game is not connected over Live Link");
            if (!TryWorldPosition(commands, composite, entity, path, out Vector3 position, out string problem))
                return Fails(prepared, outcome, problem);
            outcome.Position = prepared.Position = position;

            TEX4 previous = CurrentCubemap(level, entity, out bool _);
            int size = options.FaceSize > 0 ? options.FaceSize : previous?.TextureStreamed?.Width > 0 ? previous.TextureStreamed.Width : DefaultFaceSize;
            if (!IsFaceSize(size))
                size = DefaultFaceSize;
            TextureFormat format = options.Format ?? (previous != null && DdsFile.DxgiFor(previous.Format) != null && !TextureConverter.IsAstc(previous.Format) ? previous.Format : DefaultFormat);

            progress?.Invoke("Capturing " + entityName + "'s cubemap in the game...");
            LiveLink.CubemapCaptureFlags flags = (options.KeepHud ? LiveLink.CubemapCaptureFlags.KeepHud : 0) | (options.KeepPostEffects ? LiveLink.CubemapCaptureFlags.KeepPostEffects : 0);
            LiveLink.CubemapCapture capture = await LiveLink.CaptureCubemap(LiveLink.RootOf(commands), position, size, Math.Max(MinimumSettleFrames, options.SettleFrames), options.StreamTimeoutMs, flags);
            outcome.Capture = prepared.Capture = capture;
            if (!capture.Valid)
            {
                if (LiveLink.IsUnknownRequest(capture.Reply))
                    return Fails(prepared, outcome, "The game's runtime utils are too old to capture cubemaps - launch the game from this version of OpenCAGE");
                return Fails(prepared, outcome, "The game could not capture the cubemap: " + (capture.Reply?.Message ?? "no answer"));
            }

            if (!string.IsNullOrEmpty(options.FacesFolder))
            {
                try { SaveFaces(options.FacesFolder, capture.Faces, capture.Size); }
                catch (Exception ex) { return Fails(prepared, outcome, "The faces could not be saved to " + options.FacesFolder + ": " + ex.Message); }
            }

            progress?.Invoke("Building " + entityName + "'s cubemap...");
            string name = NewName(level, composite, entity);
            TEX4 texture = await Task.Run(() => BuildTexture(name, capture.Faces, capture.Size, format, out problem));
            if (texture == null)
                return Fails(prepared, outcome, "The cubemap could not be built: " + problem);
            prepared.Texture = texture;
            return prepared;
        }

        private static Prepared Fails(Prepared prepared, Outcome outcome, string message)
        {
            prepared.Failed = Fail(outcome, message);
            return prepared;
        }

        /// <summary>
        /// Puts a prepared cubemap in the level and points the entity at it, as one undo step (UI thread) - after checking the
        /// editor is still where the capture left it: the level still open and not being saved, the entity still there.
        /// What it used before is worked out again now, in case it changed meanwhile.
        /// </summary>
        public static Outcome Apply(Prepared prepared, Options options, string labelPrefix = "")
        {
            options = options ?? new Options();
            Outcome outcome = new Outcome() { Position = prepared.Position, Capture = prepared.Capture };
            Level level = prepared.Level;
            if (prepared.Failed != null)
                return prepared.Failed;
            if (!ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content?.Level, level))
                return Fail(outcome, "The level was closed while the cubemap was being captured");
            if (UndoStack.Current?.Blocked == true)
                return Fail(outcome, "The level was being saved as the cubemap came in - capture it again once the save is done");
            if (level.Commands?.Entries?.Any(o => ReferenceEquals(o, prepared.Composite)) != true || !ReferenceEquals(prepared.Composite.GetEntityByID(prepared.Entity.shortGUID), prepared.Entity))
                return Fail(outcome, prepared.EntityName + " was deleted while its cubemap was being captured");

            //The name could have been taken meanwhile (an import)
            TEX4 texture = prepared.Texture;
            if (AssetName.Exists(texture.Name, (level.Textures?.Entries ?? new List<TEX4>()).Where(o => o?.Name != null).Select(o => o.Name)))
                texture.Name = NewName(level, prepared.Composite, prepared.Entity);

            TEX4 previous = CurrentCubemap(level, prepared.Entity, out bool previousGlobal);
            Use(level, prepared.Composite, prepared.Entity, texture, previous, previousGlobal, options.DeleteOld, outcome, labelPrefix);
            outcome.Ok = true;
            outcome.Texture = texture;
            outcome.Previous = previous;
            outcome.Message = "Captured " + prepared.EntityName + "'s cubemap at " + Describe(prepared.Position) + " as " + texture.Name
                + (outcome.PreviousDeleted ? " (deleted " + previous.Name + ", which nothing else used)" : previous != null ? " (kept " + previous.Name + ", which something else uses)" : "")
                + (prepared.Capture.ZonesStillStreaming ? " - the zones around it were still streaming in, so parts may be missing: capture it again" : "")
                + (prepared.Capture.FrameHeight > 0 && prepared.Capture.FrameHeight < prepared.Capture.Size ? " - the game's window is " + prepared.Capture.FrameHeight + " pixels tall, so its " + prepared.Capture.Size + " pixel faces were stretched up from that: make the window at least as tall for their full detail" : "")
                + (outcome.NeedsBuild ? ". Use File > Save and Build Level, then reload the level in the game to see it there." : ". Save, then reload the level in the game to see it there.");
            return outcome;
        }
        #region MANY AT ONCE
        //Captures running: the game takes one at a time, and buttons that start one wait while any runs
        private static int _busy = 0;

        /// <summary>Whether a capture (or a batch of them) is running.</summary>
        public static bool Busy => Volatile.Read(ref _busy) > 0;

        /// <summary>Raised when <see cref="Busy"/> changes, on whichever thread changed it.</summary>
        public static event Action BusyChanged;

        private sealed class Release : IDisposable
        {
            private int _done;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _done, 1) != 0) return;
                if (Interlocked.Decrement(ref _busy) == 0)
                    BusyChanged?.Invoke();
            }
        }

        private static IDisposable Hold()
        {
            if (Interlocked.Increment(ref _busy) == 1)
                BusyChanged?.Invoke();
            return new Release();
        }

        /// <summary>Marks a capture as running for as long as the result is held (a caller doing PrepareAsync and Apply itself).</summary>
        public static IDisposable HoldBusy() => Hold();

        /// <summary>An EnvironmentMap to capture, and the instance path to the one place it is in the level.</summary>
        public sealed class Target
        {
            public Composite Composite;
            public Entity Entity;
            public List<ShortGuid> Path;
        }

        /// <summary>An EnvironmentMap that can't be captured, and why.</summary>
        public sealed class Skipped
        {
            public Composite Composite;
            public Entity Entity;
            public string Reason;
        }

        /// <summary>
        /// Every EnvironmentMap in the level that is placed exactly once (a cubemap is taken from one point), with the path
        /// to it; the rest - not placed, or placed more than once - in <paramref name="skipped"/>.
        /// </summary>
        public static List<Target> LevelTargets(Commands commands, out List<Skipped> skipped)
        {
            List<Target> targets = new List<Target>();
            skipped = new List<Skipped>();
            Composite root = commands?.EntryPoints?[0];
            if (root == null)
                return targets;
            Dictionary<Composite, List<List<Entity>>> placements = new Dictionary<Composite, List<List<Entity>>>();
            foreach (Composite composite in commands.Entries)
            {
                List<FunctionEntity> maps = composite.functions.Where(o => IsEnvironmentMap(o)).ToList();
                if (maps.Count == 0)
                    continue;
                if (!placements.TryGetValue(composite, out List<List<Entity>> found))
                    placements[composite] = found = composite == root ? new List<List<Entity>>() { new List<Entity>() } : Placements(commands, root, composite, 2);
                foreach (FunctionEntity map in maps)
                {
                    if (found.Count == 1)
                        targets.Add(new Target() { Composite = composite, Entity = map, Path = found[0].Select(o => o.shortGUID).ToList() });
                    else
                        skipped.Add(new Skipped() { Composite = composite, Entity = map, Reason = found.Count == 0 ? "not placed in the level" : "placed in more than one place" });
                }
            }
            return targets;
        }

        /// <summary>
        /// Capture each target in turn (UI thread). <paramref name="progress"/> hears how many are done and what is happening;
        /// <paramref name="stop"/> is asked between captures. Stops early when the game goes or turns a capture away (it would
        /// turn the rest away too) or the level is closed. Each capture is its own undo step.
        /// </summary>
        public static async Task<List<(Target target, Outcome outcome)>> CaptureAllAsync(Level level, IList<Target> targets, Options options,
            Action<int, string> progress = null, Func<bool> stop = null)
        {
            List<(Target, Outcome)> results = new List<(Target, Outcome)>();
            using (Hold())
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (stop?.Invoke() == true || !LiveLink.Connected || !ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content?.Level, level))
                        break;
                    Target target = targets[i];
                    Outcome outcome;
                    try
                    {
                        outcome = await CaptureOneAsync(level, target.Composite, target.Entity, target.Path, options, text => progress?.Invoke(i, text));
                    }
                    catch (Exception ex)
                    {
                        outcome = new Outcome() { Ok = false, Message = ex.Message };
                    }
                    results.Add((target, outcome));
                    progress?.Invoke(i + 1, null);
                    //The game turned it away or stopped it (paused, another level, gone, too old): the rest would go the same way
                    if (!outcome.Ok && outcome.Capture != null)
                        break;
                }
            }
            return results;
        }
        #endregion

        /// <summary>The faces' file names, in the order the game sends them.</summary>
        public static readonly string[] FaceNames = { "posx", "negx", "posy", "negy", "posz", "negz" };

        /// <summary>Six RGBA faces as PNGs in a folder (made if need be).</summary>
        public static void SaveFaces(string folder, byte[][] faces, int size)
        {
            Directory.CreateDirectory(folder);
            for (int f = 0; f < 6; f++)
            {
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
                    byte[] bgra = new byte[size * size * 4];
                    for (int i = 0; i < bgra.Length; i += 4)
                    {
                        bgra[i] = faces[f][i + 2];
                        bgra[i + 1] = faces[f][i + 1];
                        bgra[i + 2] = faces[f][i];
                        bgra[i + 3] = faces[f][i + 3];
                    }
                    for (int y = 0; y < size; y++)
                        System.Runtime.InteropServices.Marshal.Copy(bgra, y * size * 4, data.Scan0 + y * data.Stride, size * 4);
                    bitmap.UnlockBits(data);
                    bitmap.Save(Path.Combine(folder, FaceNames[f] + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        private static Outcome Fail(Outcome outcome, string message)
        {
            outcome.Ok = false;
            outcome.Message = message;
            return outcome;
        }

        private static string Describe(Vector3 position)
        {
            System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
            return "(" + position.X.ToString("0.##", c) + ", " + position.Y.ToString("0.##", c) + ", " + position.Z.ToString("0.##", c) + ")";
        }

        //Cubemaps another entity also named when one of their entities had a new cubemap captured: the movers carrying them
        //are lit by more than one entity, so giving them all one entity's new cubemap is only a stand-in until a build
        private static readonly HashSet<TEX4> _sharedAtCapture = new HashSet<TEX4>(BulkExport.ByReference<TEX4>.Instance);

        static EnvironmentMapCapture()
        {
            //The closing level's cubemaps are let go; and a Save & Build gives every mover its cubemap afresh from the entities,
            //so the movers still carrying one of these afterwards are only those of the entities still naming it (UI thread)
            Singleton.OnLevelClosing += content => _sharedAtCapture.Clear();
            Singleton.OnSaved += () =>
            {
                if (BuildTracker.LastBuiltUtc != null && BuildTracker.LastBuiltUtc == BuildTracker.LastSavedUtc)
                    _sharedAtCapture.Clear();
            };
        }

        /// <summary>
        /// Puts the new cubemap in the level and points the entity at it, then clears up the old one, as one undo step
        /// (UI thread).
        /// </summary>
        private static void Use(Level level, Composite composite, Entity entity, TEX4 texture, TEX4 previous, bool previousGlobal, bool deleteOld, Outcome outcome, string labelPrefix)
        {
            string entityName = level.Commands.Utils.GetEntityName(composite, entity);
            List<TEX4> textures = level.Textures.Entries;
            //Who else names the old cubemap, before this entity stops naming it
            if (previous != null)
            {
                AssetUsage.TextureUsers(level, previous, out List<Materials.Material> _, out List<Entity> before);
                if (before.Any(o => !ReferenceEquals(o, entity)))
                    _sharedAtCapture.Add(previous);
            }
            //Models take a new cubemap from a build, unless the movers carrying the old one can all be given this one below
            outcome.NeedsBuild = true;
            using (UndoStack.Current?.BeginGroup(labelPrefix + "Capture cubemap for " + entityName))
            {
                //The new texture
                (Action add, Action remove) = AssetStep.ListChange(textures, texture, textures.Count, true);
                add();
                AssetStep.Record(labelPrefix + "Add cubemap " + texture.Name, add, remove, level, (AssetKind.TEXTURE, texture.Name));

                //The entity's Texture
                string value = McpAssets.EnvironmentMapPrefix + texture.Name;
                Parameter parameter = entity.GetParameter(ShortGuids.Texture);
                string label = labelPrefix + "Set Texture of " + entityName;
                if (parameter == null)
                {
                    parameter = entity.AddParameter(ShortGuids.Texture, new cString(value));
                    ParameterModificationTracker.SetParameterModified(composite.shortGUID, entity.shortGUID, ShortGuids.Texture);
                    UndoStack.Current?.Record(new ParameterPresenceEdit(composite, entity, parameter, entity.parameters.IndexOf(parameter), true, true, label));
                }
                else
                {
                    ParameterData before = ParameterValues.Clone(parameter.content);
                    bool modifiedBefore = ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, ShortGuids.Texture);
                    if (parameter.content is cString text)
                        text.value = value;
                    else
                        parameter.content = new cString(value);
                    ParameterModificationTracker.SetParameterModified(composite.shortGUID, entity.shortGUID, ShortGuids.Texture);
                    UndoStack.Current?.Record(new ParameterValueEdit(composite, entity, ShortGuids.Texture, before, ParameterValues.Clone(parameter.content), modifiedBefore, true, label));
                }
                ParameterValueEdit.Notify(entity, parameter);

                //The old cubemap: once nothing names it and no material binds it, the movers still carrying it are this
                //entity's (or stale), so they take the new one; and unless it is GLOBAL's, it goes
                if (previous != null && !previousGlobal && textures.Any(o => ReferenceEquals(o, previous)))
                {
                    AssetUsage.TextureUsers(level, previous, out List<Materials.Material> materials, out List<Entity> users);
                    if (materials.Count == 0 && users.Count == 0)
                    {
                        //Whichever movers carry the one cubemap or the other when the step is done or undone: a build in
                        //between makes new movers, and those are the ones to change then
                        int Swap(TEX4 from, TEX4 to)
                        {
                            int count = 0;
                            foreach (Movers.MOVER_DESCRIPTOR mover in level.Movers?.Entries ?? new List<Movers.MOVER_DESCRIPTOR>())
                                if (mover != null && ReferenceEquals(mover.EnvironmentMap, from)) { mover.EnvironmentMap = to; count++; }
                            return count;
                        }
                        int repointed = Swap(previous, texture);
                        if (repointed != 0)
                        {
                            AssetStep.Record(labelPrefix + "Give " + repointed + " movers the new cubemap", () => Swap(previous, texture), () => Swap(texture, previous), level);
                            outcome.MoversRepointed = repointed;
                        }
                        //Every model that reflected the old cubemap now reflects this one: right as it stands, unless another
                        //entity lit some of them with it too
                        outcome.NeedsBuild = _sharedAtCapture.Contains(previous);
                        bool globalCopy = level.Global?.Textures?.Entries?.Any(o => o?.Name != null && string.Equals(AssetName.Normalise(o.Name), AssetName.Normalise(previous.Name), StringComparison.OrdinalIgnoreCase)) == true;
                        if (deleteOld && !globalCopy)
                        {
                            (Action keep, Action drop) = AssetStep.ListChange(textures, previous, textures.FindIndex(o => ReferenceEquals(o, previous)), true);
                            drop();
                            AssetStep.Record(labelPrefix + "Delete cubemap " + previous.Name, drop, keep, level, (AssetKind.TEXTURE, previous.Name));
                            outcome.PreviousDeleted = true;
                        }
                    }
                }
            }
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnResourceModified?.Invoke();
        }
        #endregion
    }

    /// <summary>
    /// A change to the open level's assets (a texture added or deleted, movers given another cubemap) as a piece of an undo
    /// step: the actions that put it back and do it again. Assets live as long as the level, so they are held directly.
    /// </summary>
    internal sealed class AssetStep : IEdit
    {
        private readonly Action _apply, _revert;
        private readonly Level _level;
        private readonly (AssetKind kind, string name)[] _assets;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;

        private AssetStep(string label, Action apply, Action revert, Level level, (AssetKind kind, string name)[] assets)
        {
            Label = label;
            _apply = apply;
            _revert = revert;
            _level = level;
            _assets = assets;
        }

        public void Apply(UndoContext context) { _apply(); Changed(); }
        public void Revert(UndoContext context) { _revert(); Changed(); }
        public bool TryMerge(IEdit next) => false;

        private void Changed()
        {
            Stamp();
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnResourceModified?.Invoke();
        }

        private void Stamp()
        {
            if (_assets != null)
                foreach ((AssetKind kind, string name) in _assets)
                    EditHistory.AssetChanged(_level, kind, name);
        }

        /// <summary>Record a change already made (UI thread), stamping the assets it touches.</summary>
        public static void Record(string label, Action apply, Action revert, Level level, params (AssetKind kind, string name)[] assets)
        {
            AssetStep step = new AssetStep(label, apply, revert, level, assets);
            step.Stamp();
            UndoStack.Current?.Record(step);
        }

        /// <summary>An object put into (or taken out of) a level list at an index, as an undo pair: (do, undo).</summary>
        public static (Action, Action) ListChange<T>(List<T> list, T item, int index, bool present) where T : class
        {
            void Set(bool there)
            {
                int at = list.FindIndex(o => ReferenceEquals(o, item));
                if (there && at < 0) list.Insert(Math.Min(Math.Max(index, 0), list.Count), item);
                else if (!there && at >= 0) list.RemoveAt(at);
            }
            return (() => Set(present), () => Set(!present));
        }
    }
}
