using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Gives a viewer that has just connected the script edits made here since the level was last saved.
    ///
    /// A viewer that connects part way through a session - restarted from the menu or by an assistant, the viewport turned
    /// back on, or back after a crash - reads the level from disk, and what was sent to the viewer before it went with that
    /// one. Entities added, removed or edited since the save were missing in the viewport until the next save and load, so
    /// the editor's paths and the viewer's tree disagreed about the scene, and the selection and the grey-out with them (a
    /// composite the editor had placed fourteen times was placed six times in the viewer).
    ///
    /// So every script packet is noted as it goes out (Send.SendData) - only which composite it was about and which of its
    /// entities, never what it said: a packet names models and materials by the indexes of its moment. Once the new viewer
    /// has built its scene, and the resource sync has caught it up on the models and materials (ViewerResourceSync), it is
    /// sent the editor's own copy of everything noted, in one scene batch: a composite added or removed here goes whole, and
    /// in any other composite the noted entities are taken out and put back as they stand. The batch ends with one rebuild
    /// of the scene the editor is in. Material mapping sets edited since the save go again with it.
    /// </summary>
    internal static class ViewerScriptResync
    {
        /* Which composites were added or removed, which entities of the others were added, removed or edited, and which
           material mapping sets were edited - keys only, so it stays small however long the session runs */
        private sealed class Record
        {
            public readonly List<uint> Composites = new List<uint>();
            public readonly Dictionary<uint, HashSet<uint>> Entities = new Dictionary<uint, HashSet<uint>>();
            public readonly List<MaterialMappings.MaterialMapping> Mappings = new List<MaterialMappings.MaterialMapping>();
            private readonly HashSet<uint> _composites = new HashSet<uint>();

            public bool IsEmpty => Composites.Count == 0 && Entities.Count == 0 && Mappings.Count == 0;
            public int EntityCount => Entities.Values.Sum(o => o.Count);

            public void AddComposite(uint composite)
            {
                if (composite != 0 && _composites.Add(composite))
                    Composites.Add(composite);
            }

            public void AddEntity(uint composite, uint entity)
            {
                if (composite == 0 || entity == 0)
                    return;
                if (!Entities.TryGetValue(composite, out HashSet<uint> entities))
                    Entities[composite] = entities = new HashSet<uint>();
                entities.Add(entity);
            }

            public void AddMapping(MaterialMappings.MaterialMapping mapping)
            {
                if (mapping != null && !Mappings.Any(o => ReferenceEquals(o, mapping)))
                    Mappings.Add(mapping);
            }

            public Record Clone()
            {
                Record copy = new Record();
                foreach (uint composite in Composites)
                    copy.AddComposite(composite);
                foreach (KeyValuePair<uint, HashSet<uint>> entry in Entities)
                    copy.Entities[entry.Key] = new HashSet<uint>(entry.Value);
                copy.Mappings.AddRange(Mappings);
                return copy;
            }
        }

        private static readonly object _lock = new object();
        private static bool _initialised;
        //The level being noted for: the one open, from its load until it closes
        private static LevelContent _content;
        //Everything sent since the level was loaded or last saved
        private static Record _record = new Record();
        //What a viewer that has connected since is owed: the record of that moment and everything noted after it, until it gets it
        private static Record _owed;
        //The resend is waiting on the resource sync (ViewerResourceSync.AfterNextSync)
        private static bool _queued;
        //The resend's own packets are only what is noted already
        [ThreadStatic] private static bool _resending;

        public static void Initialise()
        {
            if (_initialised)
                return;
            _initialised = true;

            //Raised on the level loader's thread, before anything can be edited: what the viewer reads is what was just loaded
            Singleton.OnLevelLoaded += content => Reset(content);
            Singleton.OnLevelClosing += content =>
            {
                lock (_lock)
                {
                    if (ReferenceEquals(content, _content))
                        Reset(null);
                }
            };
        }

        private static void Reset(LevelContent content)
        {
            lock (_lock)
            {
                _content = content;
                _record = new Record();
                _owed = null;
                _queued = false;
            }
        }

        /// <summary>
        /// A packet on its way to the viewer (Send.SendData): what it changes in the viewer's copy of the script is noted,
        /// whether or not there is a viewer to hear it - one that comes later reads the level from disk. Any thread.
        /// </summary>
        internal static void Note(Packet packet)
        {
            if (packet == null || _resending)
                return;
            switch (packet.packet_event)
            {
                case PacketEvent.COMPOSITE_ADDED:
                case PacketEvent.COMPOSITE_DELETED:
                case PacketEvent.COMPOSITE_CONTENTS:
                case PacketEvent.ENTITY_ADDED:
                case PacketEvent.ENTITY_DELETED:
                case PacketEvent.ENTITY_MOVED:
                case PacketEvent.ENTITY_PARAMETER_MODIFIED:
                case PacketEvent.ENTITY_RESOURCE_MODIFIED:
                    break;
                default:
                    return;
            }

            lock (_lock)
            {
                if (_content == null)
                    return;
                NoteInto(_record, packet);
                if (_owed != null)
                    NoteInto(_owed, packet);
            }
        }

        private static void NoteInto(Record record, Packet packet)
        {
            switch (packet.packet_event)
            {
                case PacketEvent.COMPOSITE_ADDED:
                case PacketEvent.COMPOSITE_DELETED:
                    record.AddComposite(packet.composite);
                    break;
                case PacketEvent.COMPOSITE_CONTENTS:
                    if (packet.composite_entities != null)
                    {
                        foreach (EntityRecord entity in packet.composite_entities)
                        {
                            if (entity != null)
                                record.AddEntity(packet.composite, entity.entity);
                        }
                    }
                    break;
                case PacketEvent.ENTITY_DELETED:
                    //One entity, or the set a batch deletion carries
                    if (packet.batch_entities != null && packet.batch_entities.Count != 0)
                    {
                        foreach (uint entity in packet.batch_entities)
                            record.AddEntity(packet.composite, entity);
                    }
                    else
                        record.AddEntity(packet.composite, packet.entity);
                    break;
                default:
                    record.AddEntity(packet.composite, packet.entity);
                    break;
            }
        }

        /// <summary>An edit the viewport made itself (a gizmo move, ViewerParameterSync): applied here with no packet going back to be noted.</summary>
        internal static void NoteEntityEdited(Composite composite, Entity entity)
        {
            if (composite == null || entity == null)
                return;
            lock (_lock)
            {
                if (_content == null)
                    return;
                _record.AddEntity(composite.shortGUID.AsUInt32, entity.shortGUID.AsUInt32);
                _owed?.AddEntity(composite.shortGUID.AsUInt32, entity.shortGUID.AsUInt32);
            }
        }

        /// <summary>A material mapping set was edited (Send.NotifyMaterialMappingModified), with or without a viewer to tell.</summary>
        internal static void NoteMaterialMapping(MaterialMappings.MaterialMapping mapping)
        {
            if (mapping == null || _resending)
                return;
            lock (_lock)
            {
                if (_content == null)
                    return;
                _record.AddMapping(mapping);
                _owed?.AddMapping(mapping);
            }
        }

        /// <summary>
        /// A viewer has connected and been told to read the open level from disk (Send.SyncClient). It is owed what has been
        /// noted, and what is noted until it gets it: an edit made while it reads can reach it before it has a level to put
        /// it in. Socket thread.
        /// </summary>
        internal static void NotifyViewerConnected()
        {
            lock (_lock)
            {
                //A level still loading is read whole once it is done, and nothing is owed for it
                if (_content == null || !ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content, _content))
                    return;
                //A viewer that went before it got what it was owed leaves all of it to this one, with what was noted since
                if (_owed == null)
                    _owed = _record.Clone();
            }
        }

        /// <summary>
        /// The viewer has finished a populate (ViewerPopulateSync). One that is owed the session's edits has built its scene
        /// from disk: they go once the resource sync has given it the models and materials they name. UI thread.
        /// </summary>
        internal static void NotifyViewerPopulated()
        {
            lock (_lock)
            {
                if (_owed == null || _queued)
                    return;
                _queued = true;
            }
            ViewerResourceSync.AfterNextSync(Resend);
        }

        /// <summary>
        /// The level has been saved, so nothing is unsaved any more. True when a viewer was still owed edits: it read the
        /// level before this save, and the LEVEL_LOADED a save sends is one it skips (it has the level), so it has to be told
        /// to read it again - what is on disk now is what is here. UI thread.
        /// </summary>
        internal static bool NotifySaved()
        {
            lock (_lock)
            {
                bool owed = _owed != null && !_owed.IsEmpty;
                _record = new Record();
                _owed = null;
                _queued = false;
                return owed;
            }
        }

        private static void Resend()
        {
            Record owed;
            LevelContent content;
            lock (_lock)
            {
                _queued = false;
                /* No viewer: the next one to connect is owed it. A build writing the level (it pumps messages, so this can
                   come due inside it): the save that ends it has the viewer read the level again (NotifySaved). Either way it
                   stays owed, and the next populate asks again. */
                if (_owed == null || !Send.Connected || ViewerInboundDispatcher.IsHeldForSave)
                    return;
                owed = _owed;
                _owed = null;
                content = _content;
            }

            Commands commands = content?.Level?.Commands;
            if (owed.IsEmpty || commands == null || !ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content, content))
                return;

            Debug.Log("Script Resync", "Sending a reconnected viewer what was edited since the last save: " + owed.Composites.Count + " composites whole, "
                + owed.EntityCount + " entities in " + owed.Entities.Count + " others, " + owed.Mappings.Count + " material mapping sets");
            _resending = true;
            try
            {
                //Ahead of the rebuild the script ends with, which draws with them
                foreach (MaterialMappings.MaterialMapping mapping in owed.Mappings)
                    Send.NotifyMaterialMappingModified(mapping);
                if (owed.Composites.Count != 0 || owed.Entities.Count != 0)
                    Send.ResendScript(commands, owed.Composites, owed.Entities);
            }
            catch (Exception e)
            {
                Debug.Log("Script Resync", "Failed: " + e);
            }
            finally
            {
                _resending = false;
            }
        }
    }
}
