using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Keeps the viewport's models, materials, textures and shaders in step with the ones being edited
    /// here, without a save or a level reload.
    ///
    /// The viewer reads the level's paks from disk with its own CathodeLib, so an import or an edit
    /// exists only in this process until the level is saved. Rather than push the bytes over the
    /// websocket, the tables that changed are written to a scratch folder with CathodeLib's own writer
    /// (a save to another path, which is also what gives new entries their write index) and the viewer
    /// is told where they are; it loads them and patches what it already has in place.
    ///
    /// Every editor raises OnResourceModified without saying what it touched, and a material slider
    /// raises it per keystroke, so this fingerprints the four tables and works out what changed itself,
    /// coalescing bursts on a short timer. LEVEL_TEXTURES alone can be half a gigabyte, so only the
    /// tables that changed are written. The fingerprints describe what the viewer has: they start from
    /// the level as loaded (what the viewer reads from disk) and only move on once a snapshot has been
    /// sent, so edits made while the viewer was closed are caught up when it next finishes a populate.
    /// </summary>
    public static class ViewerResourceSync
    {
        private const int CoalesceMilliseconds = 300;

        private static bool _initialised;
        private static Timer _timer;
        private static int _snapshotSequence;

        //What the viewer has, keyed the way the viewer matches entries: textures and models by name,
        //materials and shaders by the object (those two are only ever appended to)
        private static LevelContent _baselineContent;
        private static Dictionary<string, ulong> _textureFingerprints = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, ulong> _modelFingerprints = new Dictionary<string, ulong>();
        private static Dictionary<Materials.Material, ulong> _materialFingerprints = NewMaterialFingerprints();
        private static Dictionary<Shaders.Shader, ulong> _shaderFingerprints = NewShaderFingerprints();

        public static string ScratchRoot => Path.Combine(Path.GetTempPath(), "OpenCAGE", "ViewportSync");

        /// <summary>
        /// The viewer has populated the level it was last told to load. Until then nothing is sent: a
        /// snapshot arriving while it is still reading the level from disk would describe a level it
        /// has not got yet, and the baseline would have moved on regardless. Edits made in the meantime
        /// are caught up when the populate finishes.
        /// </summary>
        public static bool ViewerReady { get; private set; }

        /// <summary>LEVEL_LOADED has just been sent: the viewer is about to read the level from disk.</summary>
        internal static void NotifyViewerReloading()
        {
            ViewerReady = false;
        }

        /// <summary>The viewer finished a populate: it holds what is on disk plus every snapshot sent since.</summary>
        internal static void NotifyViewerPopulated()
        {
            ViewerReady = true;
            ScheduleSync();
        }

        public static void Initialise()
        {
            if (_initialised)
                return;
            _initialised = true;

            Singleton.OnLevelLoaded += content => ResetBaseline(content, deleteScratch: true);
            Singleton.OnSaved += () => ResetBaseline(Singleton.Editor?.CompositeBrowser?.Content, deleteScratch: false);
            Singleton.OnResourceModified += ScheduleSync;

            TryDeleteDirectory(ScratchRoot); //whatever a previous run left behind
        }

        /// <summary>
        /// Something in the level's resources may have changed. Coalesced, then compared against what
        /// the viewer has - a call that turns out to have changed nothing costs a fingerprint pass.
        /// </summary>
        public static void ScheduleSync()
        {
            if (!_initialised)
                return;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;
            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(ScheduleSync));
                }
                catch
                {
                }
                return;
            }

            if (_timer == null)
            {
                _timer = new Timer { Interval = CoalesceMilliseconds };
                _timer.Tick += (sender, e) =>
                {
                    _timer.Stop();
                    SyncNow();
                };
            }
            _timer.Stop();
            _timer.Start();
        }

        /* The viewer is about to read the level from disk (a load, or the reload it does after a save),
           so what it will have is what is in memory now. */
        private static void ResetBaseline(LevelContent content, bool deleteScratch)
        {
            _timer?.Stop();
            _baselineContent = content;

            Level level = content?.Level;
            _textureFingerprints = FingerprintTextures(level?.Textures);
            _modelFingerprints = FingerprintModels(level?.Models);
            _materialFingerprints = FingerprintMaterials(level?.Materials);
            _shaderFingerprints = FingerprintShaders(level?.Shaders);

            if (deleteScratch && level != null)
                TryDeleteDirectory(LevelScratchFolder(level));
        }

        private static void SyncNow()
        {
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            Level level = content?.Level;
            if (level == null || !ReferenceEquals(content, _baselineContent))
                return;

            //Nothing to send to, or a viewer that has not got the level yet. The baseline stays where the
            //viewer is, so this is caught up when it next finishes a populate.
            if (!Send.Connected || !ViewerReady)
                return;

            try
            {
                Sync(level);
            }
            catch (Exception ex)
            {
                Debug.Log("ResourceSync", "Failed: " + ex);
            }
        }

        private static void Sync(Level level)
        {
            Dictionary<string, ulong> textures = FingerprintTextures(level.Textures);
            Dictionary<string, ulong> models = FingerprintModels(level.Models);
            Dictionary<Materials.Material, ulong> materials = FingerprintMaterials(level.Materials);
            Dictionary<Shaders.Shader, ulong> shaders = FingerprintShaders(level.Shaders);

            List<string> changedTextures;
            List<string> changedModels;
            List<Materials.Material> changedMaterials;
            List<Shaders.Shader> changedShaders;
            bool texturesChanged = Differs(_textureFingerprints, textures, out changedTextures);
            bool modelsChanged = Differs(_modelFingerprints, models, out changedModels);
            bool materialsChanged = Differs(_materialFingerprints, materials, out changedMaterials);
            bool shadersChanged = Differs(_shaderFingerprints, shaders, out changedShaders);
            if (!texturesChanged && !modelsChanged && !materialsChanged && !shadersChanged)
                return;

            //Each table refers to the ones below it by write index, so they are all brought up to date
            //from their entries, in dependency order, before anything is written. The viewer does the
            //same to its copies, which is what keeps the indexes in a snapshot meaningful to it.
            level.Textures.RebuildWriteList();
            level.Shaders.RebuildWriteList();
            level.Materials.RebuildWriteList();
            level.Models.RebuildWriteList();

            string folder = Path.Combine(LevelScratchFolder(level), (++_snapshotSequence).ToString());
            Directory.CreateDirectory(folder);

            Packet packet = new Packet(PacketEvent.LEVEL_RESOURCES_MODIFIED)
            {
                level_name = level.Name,
                system_folder = Singleton.PathToAI,
                resource_changed_textures = changedTextures,
                resource_changed_models = changedModels,
            };

            //A material's texture and shader indexes only mean anything against the tables it was written with
            bool writeMaterials = materialsChanged || texturesChanged || shadersChanged;
            bool wroteTextures = texturesChanged && TryWrite(level.Textures, folder, out packet.resource_sync_textures);
            bool wroteShaders = shadersChanged && TryWrite(level.Shaders, folder, out packet.resource_sync_shaders);
            bool wroteMaterials = writeMaterials && TryWrite(level.Materials, folder, out packet.resource_sync_materials);
            bool wroteModels = modelsChanged && TryWrite(level.Models, folder, out packet.resource_sync_models);

            //Half a snapshot would leave the viewer holding tables that disagree with each other, so a
            //failed write drops the lot and leaves the baseline where it was for the next attempt
            if ((texturesChanged && !wroteTextures)
                || (shadersChanged && !wroteShaders)
                || (writeMaterials && !wroteMaterials)
                || (modelsChanged && !wroteModels))
            {
                Debug.Log("ResourceSync", "Snapshot write failed, not sent: " + folder);
                TryDeleteDirectory(folder);
                return;
            }

            Send.SendData(packet);

            _textureFingerprints = textures;
            _modelFingerprints = models;
            _materialFingerprints = materials;
            _shaderFingerprints = shaders;

            Debug.Log("ResourceSync", "Sent snapshot " + _snapshotSequence
                + (wroteTextures ? " textures(" + changedTextures.Count + " replaced)" : "")
                + (wroteShaders ? " shaders(" + changedShaders.Count + " changed)" : "")
                + (wroteMaterials ? " materials(" + changedMaterials.Count + " changed)" : "")
                + (wroteModels ? " models(" + changedModels.Count + " replaced)" : ""));

            //Anything the selected entity was sent with before now was resolved against the old indexes
            //(a model imported this session had none at all), so send it again now that they resolve
            Send.SendSelectedEntityResource();
        }

        /* Save to the scratch folder under the file's own name, so the writer's sibling files (the models
           BIN, the texture headers, the material constants) land beside it as they do in the level. */
        private static bool TryWrite(CathodeFile file, string folder, out string path)
        {
            path = null;
            if (file == null || string.IsNullOrEmpty(file.Filepath))
                return false;

            string target = Path.Combine(folder, Path.GetFileName(file.Filepath));
            if (!file.Save(target, false))
                return false;

            path = target;
            return true;
        }

        private static string LevelScratchFolder(Level level)
        {
            string name = level.Name ?? "level";
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return Path.Combine(ScratchRoot, name);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
                //Something (the viewer, most likely) still has a file open; the next run clears it
            }
        }

        #region FINGERPRINTS
        /* Which entries of a table differ from the baseline. An entry that is in one but not the other
           counts as a difference without being listed: the viewer finds additions and removals for
           itself, what it cannot see is an existing entry whose binary was swapped for another. */
        private static bool Differs<TKey>(Dictionary<TKey, ulong> baseline, Dictionary<TKey, ulong> current, out List<TKey> changed)
        {
            changed = new List<TKey>();
            bool differs = baseline.Count != current.Count;
            foreach (KeyValuePair<TKey, ulong> entry in current)
            {
                ulong was;
                if (!baseline.TryGetValue(entry.Key, out was))
                {
                    //Added - or, when the counts match, standing in for something removed
                    differs = true;
                    continue;
                }
                if (was != entry.Value)
                {
                    differs = true;
                    changed.Add(entry.Key);
                }
            }
            return differs;
        }

        private static Dictionary<string, ulong> FingerprintTextures(Textures textures)
        {
            Dictionary<string, ulong> result = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            if (textures?.Entries == null)
                return result;

            foreach (Textures.TEX4 texture in textures.Entries)
            {
                if (texture == null)
                    continue;

                Fingerprint fingerprint = new Fingerprint();
                fingerprint.Add((int)texture.Format);
                fingerprint.Add((int)texture.StateFlags);
                fingerprint.Add((int)texture.UsageFlags);
                AddTexturePart(fingerprint, texture.TexturePersistent);
                AddTexturePart(fingerprint, texture.TextureStreamed);
                result[NormaliseTextureName(texture.Name)] = fingerprint.Value;
            }
            return result;
        }

        private static void AddTexturePart(Fingerprint fingerprint, Textures.TEX4.Texture part)
        {
            if (part == null)
            {
                fingerprint.Add(-1);
                return;
            }
            fingerprint.Add(part.Width);
            fingerprint.Add(part.Height);
            fingerprint.Add(part.Depth);
            fingerprint.Add(part.MipLevels);
            fingerprint.AddArray(part.Content);
        }

        private static Dictionary<string, ulong> FingerprintModels(Models models)
        {
            Dictionary<string, ulong> result = new Dictionary<string, ulong>();
            if (models?.Entries == null)
                return result;

            foreach (Models.CS2 model in models.Entries)
            {
                if (model?.Name == null)
                    continue;

                Fingerprint fingerprint = new Fingerprint();
                fingerprint.Add(model.Components.Count);
                foreach (Models.CS2.Component component in model.Components)
                {
                    fingerprint.Add(component.LODs.Count);
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                    {
                        fingerprint.Add(lod.Name);
                        fingerprint.Add(lod.Submeshes.Count);
                        foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                            AddSubmesh(fingerprint, submesh);
                    }
                }
                result[model.Name] = fingerprint.Value;
            }
            return result;
        }

        private static void AddSubmesh(Fingerprint fingerprint, Models.CS2.Component.LOD.Submesh submesh)
        {
            fingerprint.AddReference(submesh.Material);
            fingerprint.Add((int)submesh.RenderFlags);
            fingerprint.Add(submesh.VertexScale);
            fingerprint.Add(submesh.VertexCount);
            fingerprint.Add(submesh.IndexCount);
            fingerprint.Add(submesh.MinBounds.X);
            fingerprint.Add(submesh.MinBounds.Y);
            fingerprint.Add(submesh.MinBounds.Z);
            fingerprint.Add(submesh.MaxBounds.X);
            fingerprint.Add(submesh.MaxBounds.Y);
            fingerprint.Add(submesh.MaxBounds.Z);
            fingerprint.Add(submesh.MinLODRange);
            fingerprint.Add(submesh.MaxLODRange);
            fingerprint.Add(submesh.CollisionProxyIndex);
            fingerprint.AddReference(submesh.WeightedCollision);
            fingerprint.AddReference(submesh.MorphAnimSet);
            fingerprint.AddReference(submesh.VertexFormatFull);
            fingerprint.AddReference(submesh.VertexFormatPartial);
            fingerprint.Add(submesh.Bones);
            fingerprint.AddArray(submesh.Data);
        }

        private static Dictionary<Materials.Material, ulong> FingerprintMaterials(Materials materials)
        {
            Dictionary<Materials.Material, ulong> result = NewMaterialFingerprints();
            if (materials?.Entries == null)
                return result;

            foreach (Materials.Material material in materials.Entries)
            {
                if (material == null)
                    continue;

                Fingerprint fingerprint = new Fingerprint();
                fingerprint.Add(material.Name);
                fingerprint.AddReference(material.Shader);
                fingerprint.Add(material.EngineConstants);
                fingerprint.Add(material.VertexShaderConstants);
                fingerprint.Add(material.PixelShaderConstants);
                fingerprint.Add(material.HullShaderConstants);
                fingerprint.Add(material.DomainShaderConstants);
                fingerprint.Add(material.TextureReferences == null ? -1 : material.TextureReferences.Count);
                if (material.TextureReferences != null)
                {
                    foreach (TexturePtr reference in material.TextureReferences)
                    {
                        fingerprint.AddReference(reference?.Texture);
                        fingerprint.Add(reference == null ? -2 : (int)reference.Location);
                    }
                }
                fingerprint.Add(material.PhysicalMaterialIndex);
                fingerprint.Add(material.EnvironmentMapIndex);
                fingerprint.Add(material.Priority);
                fingerprint.Add(material.OfflineLightFeatures == null ? -1 : material.OfflineLightFeatures.GetHashCode());
                result[material] = fingerprint.Value;
            }
            return result;
        }

        private static Dictionary<Shaders.Shader, ulong> FingerprintShaders(Shaders shaders)
        {
            Dictionary<Shaders.Shader, ulong> result = NewShaderFingerprints();
            if (shaders?.Entries == null)
                return result;

            foreach (Shaders.Shader shader in shaders.Entries)
            {
                if (shader == null)
                    continue;

                Fingerprint fingerprint = new Fingerprint();
                fingerprint.Add((int)shader.Ubershader);
                fingerprint.Add((int)shader.RequiredShaderModel);
                fingerprint.Add(shader.UbershaderFeatureFlags);
                fingerprint.Add(shader.UbershaderRequirementFlags);
                fingerprint.Add(shader.CycleCount);
                fingerprint.Add(shader.RegisterCount);
                fingerprint.Add((long)shader.PermutationHash);
                fingerprint.Add(shader.Samplers == null ? -1 : shader.Samplers.Count);
                if (shader.Samplers != null)
                {
                    foreach (Shaders.StateBlock sampler in shader.Samplers)
                        fingerprint.Add(sampler == null ? -1 : sampler.GetHashCode());
                }
                fingerprint.Add(shader.SamplerStageBindings);
                fingerprint.Add(shader.SamplerRemaps);
                fingerprint.Add(shader.EngineParameterRemaps);
                fingerprint.Add(shader.VertexShaderParameterRemaps);
                fingerprint.Add(shader.PixelShaderParameterRemaps);
                fingerprint.Add(shader.HullShaderParameterRemaps);
                fingerprint.Add(shader.DomainShaderParameterRemaps);
                fingerprint.Add(shader.RenderStates == null ? -1 : shader.RenderStates.GetHashCode());
                fingerprint.AddArray(shader.VertexShader);
                fingerprint.AddArray(shader.PixelShader);
                fingerprint.AddArray(shader.HullShader);
                fingerprint.AddArray(shader.DomainShader);
                fingerprint.AddArray(shader.GeometryShader);
                fingerprint.AddArray(shader.ComputeShader);
                result[shader] = fingerprint.Value;
            }
            return result;
        }

        private static Dictionary<Materials.Material, ulong> NewMaterialFingerprints() =>
            new Dictionary<Materials.Material, ulong>(ReferenceComparer<Materials.Material>.Instance);

        private static Dictionary<Shaders.Shader, ulong> NewShaderFingerprints() =>
            new Dictionary<Shaders.Shader, ulong>(ReferenceComparer<Shaders.Shader>.Instance);

        /// <summary>Texture names are matched on both sides with the slashes and case folded.</summary>
        public static string NormaliseTextureName(string name)
        {
            return (name ?? "").Replace('\\', '/').ToUpperInvariant();
        }

        /* FNV-1a over the fields that decide what the viewer would build. Binaries and the objects a
           table entry points at go in by identity rather than content: every edit path replaces the
           array or the reference, and hashing every texture on each keystroke of a material slider is
           not something to do on the UI thread. */
        private sealed class Fingerprint
        {
            private ulong _value = 14695981039346656037UL;

            public ulong Value => _value;

            public void Add(long value)
            {
                _value = (_value ^ (ulong)value) * 1099511628211UL;
            }

            public void Add(float value)
            {
                Add(value.GetHashCode());
            }

            public void Add(string value)
            {
                Add(value == null ? -1 : value.GetHashCode());
            }

            public void Add(IList<int> values)
            {
                if (values == null)
                {
                    Add(-1);
                    return;
                }
                Add(values.Count);
                for (int i = 0; i < values.Count; i++)
                    Add(values[i]);
            }

            public void Add(IList<float> values)
            {
                if (values == null)
                {
                    Add(-1);
                    return;
                }
                Add(values.Count);
                for (int i = 0; i < values.Count; i++)
                    Add(values[i]);
            }

            public void AddReference(object value)
            {
                Add(value == null ? 0 : RuntimeHelpers.GetHashCode(value));
            }

            public void AddArray(byte[] value)
            {
                AddReference(value);
                Add(value == null ? -1 : value.Length);
            }
        }

        /* Material and Shader compare by value, which is the wrong question for "is this the same entry". */
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            public bool Equals(T x, T y) => ReferenceEquals(x, y);

            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }
        #endregion
    }
}
