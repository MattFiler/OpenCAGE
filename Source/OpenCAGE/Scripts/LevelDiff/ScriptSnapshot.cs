using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// A level's script as plain, comparable text: per entity its type, name, parameter values (<see cref="CathodeJson"/> form), links,
    /// resources and animation / sequence data, plus the editor's pin types and brushes. It holds nothing the level owns, so it is safe
    /// to keep while the level is edited. Take it on the thread that owns the level (the UI thread for the open one).
    /// </summary>
    public sealed class ScriptSnapshot
    {
        public string LevelName;
        public ShortGuid Root = ShortGuid.Invalid;
        public readonly Dictionary<ShortGuid, CompositeSnap> Composites = new Dictionary<ShortGuid, CompositeSnap>();
        public readonly List<ShortGuid> Order = new List<ShortGuid>();
        public int EntityCount, ParameterCount;

        public static ScriptSnapshot Take(Commands commands, ResourceNames names, string levelName = null)
        {
            ScriptSnapshot snapshot = new ScriptSnapshot() { LevelName = levelName };
            if (commands == null) return snapshot;
            Composite[] entry = null;
            try { entry = commands.EntryPoints; } catch { }
            if (entry != null && entry.Length > 0 && entry[0] != null) snapshot.Root = entry[0].shortGUID;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null || snapshot.Composites.ContainsKey(composite.shortGUID)) continue;
                CompositeSnap c = CompositeSnap.Take(commands, composite, names);
                snapshot.Composites.Add(composite.shortGUID, c);
                snapshot.Order.Add(composite.shortGUID);
                snapshot.EntityCount += c.Entities.Count;
                snapshot.ParameterCount += c.Entities.Values.Sum(o => o.Params.Count);
            }
            return snapshot;
        }

        public CompositeSnap Find(ShortGuid id) => Composites.TryGetValue(id, out CompositeSnap c) ? c : null;
    }

    public sealed class CompositeSnap
    {
        public ShortGuid Id;
        public string Name;
        public readonly Dictionary<ShortGuid, EntitySnap> Entities = new Dictionary<ShortGuid, EntitySnap>();
        /// <summary>The editor's pin types for its variables (CompositePinInfoTable), as text; null when it has none.</summary>
        public string Pins;
        /// <summary>Which of its variables the editor shows as colours, as text; null when none.</summary>
        public string Colours;

        public static CompositeSnap Take(Commands commands, Composite composite, ResourceNames names)
        {
            CompositeSnap c = new CompositeSnap() { Id = composite.shortGUID, Name = composite.name };
            foreach (Entity entity in composite.GetEntities())
            {
                if (entity == null || c.Entities.ContainsKey(entity.shortGUID)) continue;
                c.Entities.Add(entity.shortGUID, EntitySnap.Take(commands, composite, entity, names));
            }
            try
            {
                List<CompositePinInfoTable.PinInfo> pins = commands.Utils.GetAllCustomPinInfo(composite.shortGUID);
                if (pins != null && pins.Count != 0)
                    c.Pins = CathodeJson.Text(PinsJson(pins));
                HashSet<ShortGuid> colours = commands.Utils.GetColourParameters(composite.shortGUID);
                if (colours != null && colours.Count != 0)
                    c.Colours = CathodeJson.Text(new JArray(colours.Select(CathodeJson.Id).OrderBy(o => o, StringComparer.Ordinal)));
            }
            catch { }
            return c;
        }

        public static JArray PinsJson(List<CompositePinInfoTable.PinInfo> pins)
        {
            JArray list = new JArray();
            foreach (CompositePinInfoTable.PinInfo pin in pins)
                if (pin != null) list.Add(new JArray(CathodeJson.Id(pin.VariableGUID), CathodeJson.Name(pin.PinTypeGUID), CathodeJson.Name(pin.PinEnumTypeGUID)));
            return list;
        }
    }

    public sealed class EntitySnap
    {
        public ShortGuid Id;
        public EntityVariant Variant;
        /// <summary>What it is, as text: "function TriggerSimple", "instance 01-02-03-04", "variable name FLOAT", "alias [path]", "proxy [path] fn".</summary>
        public string Kind;
        /// <summary>For an instance: the placed composite's id.</summary>
        public ShortGuid Instanced = ShortGuid.Invalid;
        public string Name;
        /// <summary>Every parameter: name -> <see cref="CathodeJson.Value"/> text.</summary>
        public readonly Dictionary<ShortGuid, string> Params = new Dictionary<ShortGuid, string>();
        /// <summary>Its links, in the order they fire: "param>entity.param".</summary>
        public readonly List<string> Links = new List<string>();
        public readonly List<ShortGuid> LinkIds = new List<ShortGuid>();
        /// <summary>A function entity's own resources (not its 'resource' parameter), as text; null when none.</summary>
        public string Resources;
        /// <summary>A CAGEAnimation's tracks or a TriggerSequence's (or a proxy's) sequence, as text; null for other entities.</summary>
        public string Special;
        /// <summary>A brush's definition (the brush tool's table), null for anything else.</summary>
        public string Brush;
        /// <summary>The entity's creation spec (kind, type, path) as JSON text.</summary>
        public string Spec;

        public static string LinkText(EntityConnector link) => CathodeJson.Name(link.thisParamID) + ">" + CathodeJson.Id(link.linkedEntityID) + "." + CathodeJson.Name(link.linkedParamID);

        public static EntitySnap Take(Commands commands, Composite composite, Entity entity, ResourceNames names)
        {
            EntitySnap e = new EntitySnap() { Id = entity.shortGUID, Variant = entity.variant };
            JObject spec = SpecOf(entity);
            e.Spec = CathodeJson.Text(spec);
            e.Kind = KindText(spec);
            if (entity is FunctionEntity fn && !fn.function.IsFunctionType) e.Instanced = fn.function;
            try { e.Name = commands.Utils.GetEntityName(composite, entity); } catch { e.Name = null; }
            //A variable is its pin's name
            if (entity is VariableEntity variable && (e.Name == null || e.Name == entity.shortGUID.ToByteString())) e.Name = CathodeJson.Name(variable.name);
            foreach (Parameter p in entity.parameters)
            {
                if (p == null || e.Params.ContainsKey(p.name)) continue;
                string value;
                try { value = CathodeJson.Text(CathodeJson.Value(p.content, names)); }
                catch (Exception ex) { value = "{\"unreadable\":\"" + ex.GetType().Name + "\"}"; }
                e.Params.Add(p.name, value);
            }
            foreach (EntityConnector link in entity.childLinks)
            {
                e.Links.Add(LinkText(link));
                e.LinkIds.Add(link.ID);
            }
            if (entity is FunctionEntity function && function.resources != null && function.resources.Count != 0)
                e.Resources = CathodeJson.Text(CathodeJson.References(function.resources, names));
            switch (entity)
            {
                case CAGEAnimation animation:
                    e.Special = CathodeJson.Text(CathodeJson.Animation(animation));
                    break;
                case TriggerSequence trigger:
                    e.Special = CathodeJson.Text(CathodeJson.Sequence(trigger.sequence, trigger.methods));
                    break;
                case ProxyEntity proxy:
                    if ((proxy.sequence != null && proxy.sequence.Count != 0) || (proxy.methods != null && proxy.methods.Count != 0))
                        e.Special = CathodeJson.Text(CathodeJson.Sequence(proxy.sequence, proxy.methods));
                    break;
            }
            try { e.Brush = commands.Utils.GetBrush(composite.shortGUID, entity.shortGUID); } catch { }
            return e;
        }

        /// <summary>How to make it again: {"kind":"function","function":"TriggerSimple"} and the like.</summary>
        public static JObject SpecOf(Entity entity, IdMap map = null)
        {
            switch (entity)
            {
                case FunctionEntity f when f.function.IsFunctionType:
                    return new JObject() { ["kind"] = "function", ["function"] = f.function.AsFunctionType.ToString() };
                case FunctionEntity f:
                    return new JObject() { ["kind"] = "instance", ["composite"] = CathodeJson.Id(map == null ? f.function : map.Map(f.function)) };
                case VariableEntity v:
                    return new JObject() { ["kind"] = "variable", ["name"] = CathodeJson.Name(v.name), ["type"] = v.type.ToString() };
                case AliasEntity a:
                    return new JObject() { ["kind"] = "alias", ["path"] = CathodeJson.Path(a.alias?.path, map) };
                case ProxyEntity p:
                    return new JObject() { ["kind"] = "proxy", ["path"] = CathodeJson.Path(p.proxy?.path, map), ["function"] = CathodeJson.Name(p.function) };
                default:
                    return new JObject() { ["kind"] = entity.variant.ToString().ToLowerInvariant() };
            }
        }

        public static string KindText(JObject spec)
        {
            switch ((string)spec["kind"])
            {
                case "function": return "function " + (string)spec["function"];
                case "instance": return "instance " + (string)spec["composite"];
                case "variable": return "variable " + (string)spec["name"] + " " + (string)spec["type"];
                case "alias": return "alias " + CathodeJson.Text(spec["path"]);
                case "proxy": return "proxy " + CathodeJson.Text(spec["path"]) + " " + (string)spec["function"];
                default: return CathodeJson.Text(spec);
            }
        }

        /// <summary>What it is, for people: "TriggerSimple", "instance of AYZ\...\Door", "variable 'pin' (FLOAT)".</summary>
        public string Describe(Func<ShortGuid, string> compositeName)
        {
            JObject spec = (JObject)CathodeJson.Parse(Spec);
            switch ((string)spec["kind"])
            {
                case "function": return (string)spec["function"];
                case "instance": return "instance of " + (compositeName?.Invoke(Instanced) ?? (string)spec["composite"]);
                case "variable": return "variable (" + (string)spec["type"] + ")";
                case "alias": return "alias";
                case "proxy": return "proxy (" + (string)spec["function"] + ")";
                default: return Kind;
            }
        }
    }
}
