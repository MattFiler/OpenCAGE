using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ANIM_TRACK_TYPE = CATHODE.Enums.ANIM_TRACK_TYPE;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// A lossless JSON form for a level's script (parameter values, entity paths, resource references, CAGEAnimation and TriggerSequence
    /// data), used by Compare Levels and recipes. Values are typed ({"float":0.5}, {"int":3}) and floats round-trip to the same bits.
    /// Level objects are named, not indexed, so the form survives renumbered tables (<see cref="ResourceNames"/>).
    /// </summary>
    public static class CathodeJson
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Regex ByteId = new Regex("^[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}$", RegexOptions.Compiled);

        #region Plain values
        /// <summary>A float as the shortest text that reads back as the same float (NaN / infinities as text).</summary>
        public static JToken Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return new JValue(value.ToString("R", Invariant));
            return new JValue(double.Parse(value.ToString("R", Invariant), Invariant));
        }

        public static float ToFloat(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return 0;
            if (token.Type == JTokenType.String) return float.Parse((string)token, NumberStyles.Float, Invariant);
            return (float)(double)token;
        }

        public static JArray Vector(Vector3 v) => new JArray(Float(v.X), Float(v.Y), Float(v.Z));
        public static Vector3 ToVector(JToken token)
        {
            JArray a = token as JArray;
            if (a == null || a.Count < 3) return new Vector3();
            return new Vector3(ToFloat(a[0]), ToFloat(a[1]), ToFloat(a[2]));
        }
        public static JArray Vector(Vector2 v) => new JArray(Float(v.X), Float(v.Y));
        public static Vector2 ToVector2(JToken token)
        {
            JArray a = token as JArray;
            if (a == null || a.Count < 2) return new Vector2();
            return new Vector2(ToFloat(a[0]), ToFloat(a[1]));
        }

        /// <summary>An id: its 4-byte text ("01-AB-23-CD"). Ids are never names: a name could be another id's text.</summary>
        public static string Id(ShortGuid id) => id.ToByteString();
        public static ShortGuid ToId(JToken token)
        {
            string text = (string)token;
            if (string.IsNullOrEmpty(text)) return ShortGuid.Invalid;
            return new ShortGuid(text);
        }

        /// <summary>
        /// A name's guid (a parameter, an enum, a function type): the name when the editor knows one that gives this guid back, else
        /// the 4-byte text. <see cref="ToName"/> reads either.
        /// </summary>
        public static string Name(ShortGuid guid)
        {
            string name = ShortGuidUtils.FindString(guid);
            if (string.IsNullOrEmpty(name) || ByteId.IsMatch(name)) return guid.ToByteString();
            return name;
        }
        public static ShortGuid ToName(JToken token)
        {
            string text = (string)token;
            if (text == null) return ShortGuid.Invalid;
            if (ByteId.IsMatch(text)) return new ShortGuid(text);
            return ShortGuidUtils.Generate(text);
        }

        public static JArray Path(ShortGuid[] path, IdMap map = null)
        {
            JArray a = new JArray();
            if (path == null) return a;
            foreach (ShortGuid id in path)
                if (!id.IsInvalid) a.Add(Id(map == null ? id : map.Map(id)));
            return a;
        }
        /// <summary>A path as the editor keeps it: the ids, then the trailing invalid id.</summary>
        public static ShortGuid[] ToPath(JToken token, IdMap map = null)
        {
            List<ShortGuid> path = new List<ShortGuid>();
            if (token is JArray a)
                foreach (JToken t in a)
                {
                    ShortGuid id = ToId(t);
                    path.Add(map == null ? id : map.Map(id));
                }
            path.Add(ShortGuid.Invalid);
            return path.ToArray();
        }

        /// <summary>Compact, stable text of a token (the canonical form compared and hashed).</summary>
        public static string Text(JToken token) => token == null ? null : token.ToString(Formatting.None);

        /// <summary>Parse JSON without Newtonsoft turning date-like strings into dates or numbers into decimals.</summary>
        public static JToken Parse(string text)
        {
            if (text == null) return null;
            using (JsonTextReader reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
                return JToken.ReadFrom(reader);
        }
        #endregion

        #region Parameter values
        public static JObject Value(ParameterData data, ResourceNames names, IdMap map = null)
        {
            switch (data)
            {
                case null:
                    return new JObject() { ["none"] = true };
                case cTransform t:
                    return new JObject() { ["transform"] = new JObject() { ["position"] = Vector(t.position), ["rotation"] = Vector(t.rotation) } };
                case cInteger i:
                    return new JObject() { ["int"] = i.value };
                case cEnumString es:
                    return new JObject() { ["enum_string"] = new JArray(Name(es.enumID), es.value ?? "") };
                case cString s:
                    return new JObject() { ["string"] = s.value ?? "" };
                case cBool b:
                    return new JObject() { ["bool"] = b.value };
                case cFloat f:
                    return new JObject() { ["float"] = Float(f.value) };
                case cVector3 v:
                    return new JObject() { ["vector"] = Vector(v.value) };
                case cEnum e:
                    return new JObject() { ["enum"] = new JArray(Name(e.enumID), e.enumIndex) };
                case cSpline sp:
                    JArray points = new JArray();
                    foreach (cTransform p in sp.splinePoints ?? new List<cTransform>())
                        points.Add(new JArray(Float(p.position.X), Float(p.position.Y), Float(p.position.Z), Float(p.rotation.X), Float(p.rotation.Y), Float(p.rotation.Z)));
                    return new JObject() { ["spline"] = points };
                case cResource r:
                    return new JObject() { ["resource"] = new JObject() { ["id"] = Id(map == null ? r.shortGUID : map.Map(r.shortGUID)), ["refs"] = References(r.value, names, map) } };
                default:
                    return new JObject() { ["type"] = data.dataType.ToString() };
            }
        }

        /// <summary>The value a <see cref="Value"/> form describes, made for <paramref name="target"/>. Throws <see cref="RecipeException"/> when it can't be.</summary>
        public static ParameterData ToValue(JToken token, ResourceNames target, IdMap map = null)
        {
            JObject o = token as JObject;
            if (o == null || o.Count == 0) throw new RecipeException("not a value: " + Text(token));
            JProperty p = o.Properties().First();
            switch (p.Name)
            {
                case "none": return null;
                case "transform":
                    return new cTransform(ToVector(p.Value["position"]), ToVector(p.Value["rotation"]));
                case "int": return new cInteger((int)p.Value);
                case "enum_string": return new cEnumString(ToName(p.Value[0]), (string)p.Value[1] ?? "");
                case "string": return new cString((string)p.Value ?? "");
                case "bool": return new cBool((bool)p.Value);
                case "float": return new cFloat(ToFloat(p.Value));
                case "vector": return new cVector3(ToVector(p.Value));
                case "enum": return new cEnum(ToName(p.Value[0]), (int)p.Value[1]);
                case "spline":
                    List<cTransform> points = new List<cTransform>();
                    foreach (JToken t in (JArray)p.Value)
                        points.Add(new cTransform(new Vector3(ToFloat(t[0]), ToFloat(t[1]), ToFloat(t[2])), new Vector3(ToFloat(t[3]), ToFloat(t[4]), ToFloat(t[5]))));
                    return new cSpline(points);
                case "resource":
                    return new cResource(ToReferences(p.Value["refs"], target, map), map == null ? ToId(p.Value["id"]) : map.Map(ToId(p.Value["id"])));
                default:
                    throw new RecipeException("a value of type " + p.Name + " can't be written");
            }
        }

        /// <summary>Where two JSON forms differ: "refs[0].position: [0,0,0] -> [0.25,0,-0.5]", at most <paramref name="max"/>.</summary>
        public static List<string> Differences(JToken a, JToken b, int max = 4)
        {
            List<string> found = new List<string>();
            Walk(a, b, "", found, max);
            return found;
        }
        private static void Walk(JToken a, JToken b, string path, List<string> found, int max)
        {
            if (found.Count >= max) return;
            if (a is JObject oa && b is JObject ob)
            {
                foreach (string key in oa.Properties().Select(o => o.Name).Union(ob.Properties().Select(o => o.Name)))
                    Walk(oa[key], ob[key], path.Length == 0 ? key : path + "." + key, found, max);
                return;
            }
            if (a is JArray aa && b is JArray ab && aa.Count == ab.Count && !(aa.All(o => o is JValue) && aa.Count <= 6))
            {
                for (int i = 0; i < aa.Count; i++) Walk(aa[i], ab[i], path + "[" + i + "]", found, max);
                return;
            }
            if (!JToken.DeepEquals(a, b))
            {
                string x = a == null ? "(none)" : Text(a), y = b == null ? "(none)" : Text(b);
                if (x.Length > 80) x = x.Substring(0, 80) + "..."; if (y.Length > 80) y = y.Substring(0, 80) + "...";
                found.Add((path.Length == 0 ? "value" : path) + ": " + x + " -> " + y);
            }
        }

        /// <summary>A short, readable text of a value form for reports ("0.5", "(1, 2, 3)", "TRUE").</summary>
        public static string Show(JToken value, int max = 160)
        {
            string text = ShowCore(value);
            return text.Length > max ? text.Substring(0, max) + "..." : text;
        }
        private static string ShowCore(JToken value)
        {
            JObject o = value as JObject;
            if (o == null || o.Count == 0) return Text(value) ?? "(none)";
            JProperty p = o.Properties().First();
            switch (p.Name)
            {
                case "none": return "(none)";
                case "float": return ShowNumber(p.Value);
                case "int": return Text(p.Value);
                case "bool": return (bool)p.Value ? "true" : "false";
                case "string": return "\"" + (string)p.Value + "\"";
                case "vector": return ShowVector(p.Value);
                case "transform": return "position " + ShowVector(p.Value["position"]) + ", rotation " + ShowVector(p.Value["rotation"]);
                case "enum": return (string)p.Value[0] + " #" + Text(p.Value[1]);
                case "enum_string": return (string)p.Value[0] + " \"" + (string)p.Value[1] + "\"";
                case "spline": return ((JArray)p.Value).Count + " spline points";
                case "resource": return ShowReferences(p.Value["refs"] as JArray);
                default: return Text(value);
            }
        }
        private static string ShowNumber(JToken t) => t.Type == JTokenType.String ? (string)t : ((double)t).ToString("0.#####", Invariant);
        private static string ShowVector(JToken t) => t is JArray a ? "(" + string.Join(", ", a.Select(ShowNumber)) + ")" : Text(t);
        public static string ShowReferences(JArray refs)
        {
            if (refs == null || refs.Count == 0) return "no resources";
            return string.Join("; ", refs.Select(r =>
            {
                string type = (string)r["type"];
                if (r["elements"] is JArray els)
                    return type + " [" + string.Join(", ", els.Select(e => (string)e["model"] + "#" + Text(e["sub"]) + "/" + (string)e["material"])) + "]";
                if (r["collision"] != null) return type + " proxy " + Text(r["collision"]["proxy"]) + " " + (string)r["collision"]["material"];
                if (r["physics_system"] != null) return type + " " + Text(r["physics_system"]);
                if (r["animated_model"] != null) return type + " " + Text(r["animated_model"]);
                return type;
            }));
        }
        #endregion

        #region Resource references
        public static JArray References(List<ResourceReference> references, ResourceNames names, IdMap map = null)
        {
            JArray list = new JArray();
            if (references == null) return list;
            foreach (ResourceReference r in references)
            {
                if (r == null) { list.Add(JValue.CreateNull()); continue; }
                JObject o = new JObject()
                {
                    ["type"] = r.resource_type.ToString(),
                    ["id"] = Id(map == null ? r.resource_id : map.Map(r.resource_id)),
                };
                if (r.entityID != new ShortGuid("FF-FF-FF-FF")) o["entity"] = Id(map == null ? r.entityID : map.Map(r.entityID));
                if (r.position != Vector3.Zero) o["position"] = Vector(r.position);
                if (r.rotation != Vector3.Zero) o["rotation"] = Vector(r.rotation);
                switch (r.resource_type)
                {
                    case ResourceType.RENDERABLE_INSTANCE:
                        o["elements"] = Elements(r.RenderableInstance, names);
                        break;
                    case ResourceType.COLLISION_MAPPING:
                        if (r.CollisionMapping != null) o["collision"] = names?.Collision(r.CollisionMapping) ?? new JObject();
                        break;
                    case ResourceType.DYNAMIC_PHYSICS_SYSTEM:
                        o["physics_system"] = names?.PhysicsSystem(r) ?? new JValue(r.PhysicsSystemIndex);
                        break;
                    case ResourceType.ANIMATED_MODEL:
                        if (r.AnimatedModel != null) o["animated_model"] = names?.AnimatedModel(r.AnimatedModel) ?? new JValue(r.AnimatedModel.ID);
                        break;
                }
                list.Add(o);
            }
            return list;
        }

        public static List<ResourceReference> ToReferences(JToken token, ResourceNames target, IdMap map = null)
        {
            List<ResourceReference> list = new List<ResourceReference>();
            if (!(token is JArray a)) return list;
            foreach (JToken t in a)
            {
                if (t == null || t.Type == JTokenType.Null) { list.Add(null); continue; }
                ResourceType type;
                if (!Enum.TryParse((string)t["type"], out type)) throw new RecipeException("unknown resource type " + (string)t["type"]);
                ResourceReference r = new ResourceReference(type);
                r.resource_id = map == null ? ToId(t["id"]) : map.Map(ToId(t["id"]));
                if (t["entity"] != null) r.entityID = map == null ? ToId(t["entity"]) : map.Map(ToId(t["entity"]));
                if (t["position"] != null) r.position = ToVector(t["position"]);
                if (t["rotation"] != null) r.rotation = ToVector(t["rotation"]);
                switch (type)
                {
                    case ResourceType.RENDERABLE_INSTANCE:
                        r.RenderableInstance = ToElements(t["elements"], target);
                        break;
                    case ResourceType.COLLISION_MAPPING:
                        if (t["collision"] != null && ((JObject)t["collision"]).Count != 0)
                            r.CollisionMapping = Need(target).FindCollision((JObject)t["collision"], map);
                        break;
                    case ResourceType.DYNAMIC_PHYSICS_SYSTEM:
                        if (t["physics_system"] != null) Need(target).SetPhysicsSystem(r, t["physics_system"]);
                        break;
                    case ResourceType.ANIMATED_MODEL:
                        if (t["animated_model"] != null) r.AnimatedModel = Need(target).FindAnimatedModel(t["animated_model"]);
                        break;
                }
                list.Add(r);
            }
            return list;
        }

        private static ResourceNames Need(ResourceNames names)
        {
            if (names == null) throw new RecipeException("the level's resources are not loaded");
            return names;
        }

        public static JArray Elements(List<RenderableElements.Element> elements, ResourceNames names)
        {
            JArray list = new JArray();
            if (elements == null) return list;
            foreach (RenderableElements.Element e in elements)
            {
                if (e == null) { list.Add(JValue.CreateNull()); continue; }
                JObject o = names == null ? new JObject() { ["model"] = "?", ["sub"] = new JArray(), ["material"] = e.Material?.Name } : names.Element(e);
                if (e.ModelLocation != PakLocation.LEVEL) o["model_at"] = e.ModelLocation.ToString();
                if (e.MaterialLocation != PakLocation.LEVEL) o["material_at"] = e.MaterialLocation.ToString();
                if (e.ModelSubplatformDependent) o["model_subplatform"] = true;
                if (e.MaterialSubplatformDependent) o["material_subplatform"] = true;
                if (e.LODs != null && e.LODs.Count != 0) o["lods"] = Elements(e.LODs, names);
                list.Add(o);
            }
            return list;
        }

        public static List<RenderableElements.Element> ToElements(JToken token, ResourceNames target)
        {
            List<RenderableElements.Element> list = new List<RenderableElements.Element>();
            if (!(token is JArray a)) return list;
            foreach (JToken t in a)
            {
                if (t == null || t.Type == JTokenType.Null) { list.Add(null); continue; }
                RenderableElements.Element e = new RenderableElements.Element();
                Need(target).Resolve(e, (JObject)t);
                if (t["model_at"] != null) e.ModelLocation = (PakLocation)Enum.Parse(typeof(PakLocation), (string)t["model_at"]);
                if (t["material_at"] != null) e.MaterialLocation = (PakLocation)Enum.Parse(typeof(PakLocation), (string)t["material_at"]);
                e.ModelSubplatformDependent = t["model_subplatform"] != null && (bool)t["model_subplatform"];
                e.MaterialSubplatformDependent = t["material_subplatform"] != null && (bool)t["material_subplatform"];
                if (t["lods"] != null) e.LODs = ToElements(t["lods"], target);
                list.Add(e);
            }
            return list;
        }
        #endregion

        #region Special entities
        public static JObject Animation(CAGEAnimation animation, IdMap map = null)
        {
            JArray connections = new JArray();
            foreach (CAGEAnimation.Connection c in animation.connections ?? new List<CAGEAnimation.Connection>())
                connections.Add(new JObject()
                {
                    ["binding"] = Id(c.binding_guid),
                    ["track"] = Id(c.target_track),
                    ["binding_type"] = c.binding_type.ToString(),
                    ["param"] = Name(c.target_param),
                    ["param_type"] = c.target_param_type.ToString(),
                    ["sub_param"] = Name(c.target_sub_param),
                    ["path"] = Path(c.connectedEntity?.path, map),
                });
            JArray floats = new JArray();
            foreach (CAGEAnimation.FloatTrack track in animation.floatTracks ?? new List<CAGEAnimation.FloatTrack>())
            {
                JArray keys = new JArray();
                foreach (CAGEAnimation.FloatTrack.Keyframe k in track.keyframes ?? new List<CAGEAnimation.FloatTrack.Keyframe>())
                    keys.Add(new JArray((int)k.mode, Float(k.time), Float(k.value.X), Float(k.value.Y), Float(k.tan_in.X), Float(k.tan_in.Y), Float(k.tan_out.X), Float(k.tan_out.Y)));
                floats.Add(new JObject() { ["id"] = Id(track.shortGUID), ["keys"] = keys });
            }
            JArray events = new JArray();
            foreach (CAGEAnimation.EventTrack track in animation.eventTracks ?? new List<CAGEAnimation.EventTrack>())
            {
                JArray keys = new JArray();
                foreach (CAGEAnimation.EventTrack.Keyframe k in track.keyframes ?? new List<CAGEAnimation.EventTrack.Keyframe>())
                    keys.Add(new JArray(Float(k.time), KeyId(k.forward, k.track_type, map), KeyId(k.reverse, k.track_type, map), k.track_type.ToString(), Float(k.duration)));
                events.Add(new JObject() { ["id"] = Id(track.shortGUID), ["type"] = track.track_type.ToString(), ["keys"] = keys });
            }
            return new JObject() { ["connections"] = connections, ["float_tracks"] = floats, ["event_tracks"] = events };
        }

        //An event key names an event (a name's guid) or, typed T_GUID, an entity (an id)
        private static string KeyId(ShortGuid id, ANIM_TRACK_TYPE type, IdMap map) => type == ANIM_TRACK_TYPE.T_GUID ? "#" + Id(map == null ? id : map.Map(id)) : Name(id);
        private static ShortGuid ToKeyId(JToken token, IdMap map)
        {
            string text = (string)token;
            if (text != null && text.StartsWith("#")) { ShortGuid id = new ShortGuid(text.Substring(1)); return map == null ? id : map.Map(id); }
            return ToName(token);
        }

        public static void ApplyAnimation(CAGEAnimation animation, JToken data, IdMap map = null)
        {
            List<CAGEAnimation.Connection> connections = new List<CAGEAnimation.Connection>();
            foreach (JToken c in (JArray)data["connections"] ?? new JArray())
                connections.Add(new CAGEAnimation.Connection()
                {
                    binding_guid = ToId(c["binding"]),
                    target_track = ToId(c["track"]),
                    binding_type = (ObjectType)Enum.Parse(typeof(ObjectType), (string)c["binding_type"]),
                    target_param = ToName(c["param"]),
                    target_param_type = (DataType)Enum.Parse(typeof(DataType), (string)c["param_type"]),
                    target_sub_param = ToName(c["sub_param"]),
                    connectedEntity = new EntityPath(ToPath(c["path"], map)),
                });
            List<CAGEAnimation.FloatTrack> floats = new List<CAGEAnimation.FloatTrack>();
            foreach (JToken f in (JArray)data["float_tracks"] ?? new JArray())
            {
                CAGEAnimation.FloatTrack track = new CAGEAnimation.FloatTrack() { shortGUID = ToId(f["id"]) };
                foreach (JToken k in (JArray)f["keys"] ?? new JArray())
                    track.keyframes.Add(new CAGEAnimation.FloatTrack.Keyframe()
                    {
                        mode = (CAGEAnimation.InterpolationMode)(int)k[0],
                        time = ToFloat(k[1]),
                        value = new Vector2(ToFloat(k[2]), ToFloat(k[3])),
                        tan_in = new Vector2(ToFloat(k[4]), ToFloat(k[5])),
                        tan_out = new Vector2(ToFloat(k[6]), ToFloat(k[7])),
                    });
                floats.Add(track);
            }
            List<CAGEAnimation.EventTrack> events = new List<CAGEAnimation.EventTrack>();
            foreach (JToken ev in (JArray)data["event_tracks"] ?? new JArray())
            {
                CAGEAnimation.EventTrack track = new CAGEAnimation.EventTrack() { shortGUID = ToId(ev["id"]), track_type = (ANIM_TRACK_TYPE)Enum.Parse(typeof(ANIM_TRACK_TYPE), (string)ev["type"]) };
                foreach (JToken k in (JArray)ev["keys"] ?? new JArray())
                    track.keyframes.Add(new CAGEAnimation.EventTrack.Keyframe()
                    {
                        time = ToFloat(k[0]),
                        forward = ToKeyId(k[1], map),
                        reverse = ToKeyId(k[2], map),
                        track_type = (ANIM_TRACK_TYPE)Enum.Parse(typeof(ANIM_TRACK_TYPE), (string)k[3]),
                        duration = ToFloat(k[4]),
                    });
                events.Add(track);
            }
            animation.connections = connections;
            animation.floatTracks = floats;
            animation.eventTracks = events;
        }

        public static JObject Sequence(List<TriggerSequence.SequenceEntry> sequence, List<TriggerSequence.MethodEntry> methods, IdMap map = null)
        {
            JArray entries = new JArray();
            foreach (TriggerSequence.SequenceEntry e in sequence ?? new List<TriggerSequence.SequenceEntry>())
                entries.Add(new JArray(Float(e.timing), Path(e.connectedEntity?.path, map)));
            JArray events = new JArray();
            foreach (TriggerSequence.MethodEntry m in methods ?? new List<TriggerSequence.MethodEntry>())
                events.Add(new JArray(Name(m.method), Name(m.relay), Name(m.finished)));
            return new JObject() { ["sequence"] = entries, ["methods"] = events };
        }

        public static void ToSequence(JToken data, IdMap map, out List<TriggerSequence.SequenceEntry> sequence, out List<TriggerSequence.MethodEntry> methods)
        {
            sequence = new List<TriggerSequence.SequenceEntry>();
            methods = new List<TriggerSequence.MethodEntry>();
            foreach (JToken e in (JArray)data["sequence"] ?? new JArray())
                sequence.Add(new TriggerSequence.SequenceEntry() { timing = ToFloat(e[0]), connectedEntity = new EntityPath(ToPath(e[1], map)) });
            foreach (JToken m in (JArray)data["methods"] ?? new JArray())
                methods.Add(new TriggerSequence.MethodEntry(ToName(m[0]), ToName(m[1]), ToName(m[2])));
        }
        #endregion
    }

    /// <summary>A recipe (or a value in it) that can't be applied to this level, and why.</summary>
    public sealed class RecipeException : Exception
    {
        public RecipeException(string message) : base(message) { }
    }

    /// <summary>
    /// Fresh ids for a replay in remap mode: every id the recipe made gets a new one in the target level and every reference to it
    /// follows. Ids not in the map are the target's own and stay as they are.
    /// </summary>
    public sealed class IdMap
    {
        private readonly Dictionary<ShortGuid, ShortGuid> _map = new Dictionary<ShortGuid, ShortGuid>();
        public int Count => _map.Count;
        public void Add(ShortGuid from, ShortGuid to) => _map[from] = to;
        public bool Contains(ShortGuid id) => _map.ContainsKey(id);
        public ShortGuid Map(ShortGuid id) => _map.TryGetValue(id, out ShortGuid to) ? to : id;
        public IEnumerable<KeyValuePair<ShortGuid, ShortGuid>> Pairs => _map;
    }
}
