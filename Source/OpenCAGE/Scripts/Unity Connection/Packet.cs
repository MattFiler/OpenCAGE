using CATHODE.Scripting;
using System;
using System.Collections.Generic;

namespace CommandsEditor.UnityConnection
{
    public enum PacketEvent
    {
        LEVEL_LOADED,

        COMPOSITE_SELECTED,
        COMPOSITE_RELOADED,
        COMPOSITE_DELETED,
        COMPOSITE_ADDED,

        ENTITY_SELECTED,
        ENTITY_MOVED,
        ENTITY_DELETED,
        ENTITY_ADDED,
        ENTITY_RESOURCE_MODIFIED,

        GENERIC_DATA_SYNC,
    }

    public class Packet
    {
        public Packet(PacketEvent packet_event = PacketEvent.GENERIC_DATA_SYNC)
        {
            this.packet_event = packet_event;
        }

        //Packet metadata
        public PacketEvent packet_event;
        public const int version = 4;

        //Setup metadata
        public string level_name = "";
        public string system_folder = "";

        //Selection metadata
        public List<uint> path_entities = new List<uint>();
        public List<uint> path_composites = new List<uint>();
        public uint entity;
        public uint composite;

        //Transform
        public bool has_transform = false;
        public System.Numerics.Vector3 position = new System.Numerics.Vector3();
        public System.Numerics.Vector3 rotation = new System.Numerics.Vector3();

        //Renderable resource
        public List<Tuple<int, int>> renderable = new List<Tuple<int, int>>(); //Model Index, Material Index

        //Modified entity info
        public EntityVariant entity_variant;
        public uint entity_function; //For function entities
        public List<uint> entity_pointed; //For alias/proxy entities

        //Track if things have changed
        public bool dirty = false;

        //Settings
        public bool focus_object = false;
    }
}
