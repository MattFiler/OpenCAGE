using Newtonsoft.Json.Linq;
using OpenCAGE.Audio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// tune_sound_event: how a sound event plays on the open level - volume, pitch, start delay, fades, what its stop reaches, looping,
    /// how far away it can be heard - read, changed in the level's own sound package (WORLD/LEVEL_SOUND_OVERRIDE.PCK), or put back, as the
    /// Level Sound Override window's Tune Event does. Registered by McpSoundTools.Tools().
    /// </summary>
    internal static class McpSoundTuneTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "tune_sound_event",
                Title = "Tune sound event",
                Description = "How a sound event plays on the open level, changed for this level only in its own sound package (WORLD/LEVEL_SOUND_OVERRIDE.PCK, as replace_sound level_only uses): " +
                    "volume_db (or volume_change_db), pitch_cents, delay_ms (start delay of its Play actions; a random delay range is cleared), fade_in_ms, fade_out_ms and stops " +
                    "('own', 'mixer' = everything under the stopped sound's actor-mixer, 'bus' = everything on its bus) - on a Play event these go to its Stop_ event - loop ('forever', " +
                    "'once' or a count) and max_distance_m (how far away it can be heard: the attenuation is copied for this event, the shared curve is never edited). " +
                    "action get (default) reads the event as the level plays it and as the game ships it; set changes it (dry_run says what would change); revert puts the base game's " +
                    "back; list gives every event the level's package tunes. Events in permanent soundbanks (Init, UI, LIST_OF_PERMANENT_SOUND_BANKS.TXT) are refused: their copy is " +
                    "loaded before any level's. PC, Mac and Linux soundbanks only. A fade out only plays when the event is stopped by its stop event (a Sound's stop_event), not by Sound.stop alone.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event (a SOUND_EVENT name), e.g. Play_Music_TwoTeams_ExploreLoop. Not needed for list."),
                    McpSchema.String("action", "get (default), set, revert or list.", options: new[] { "get", "set", "revert", "list" }),
                    McpSchema.Number("volume_db", "set: the volume of what it plays, in dB (its own; containers above add to it). -96 to +24."),
                    McpSchema.Number("volume_change_db", "set: dB added to the volume it has now on this level (instead of volume_db)."),
                    McpSchema.Number("pitch_cents", "set: pitch in cents (1200 = an octave up). -2400 to +2400."),
                    McpSchema.Integer("delay_ms", "set: start delay of its Play actions in ms (of every action, for an event that plays nothing)."),
                    McpSchema.Integer("fade_in_ms", "set: fade in of its Play actions, in ms."),
                    McpSchema.Integer("fade_out_ms", "set: fade out of its Stop actions in ms (a Play event's Stop_ event's)."),
                    McpSchema.String("stops", "set: what its Stop actions stop (a Play event's Stop_ event's): own, mixer or bus.", options: new[] { "own", "mixer", "bus" }),
                    McpSchema.Any("loop", "set: 'forever', 'once', or how many times every sound it plays repeats (0 = forever, 1 = once)."),
                    McpSchema.Number("max_distance_m", "set: how far away it can be heard, in metres (0.5 to 10000)."),
                    McpSchema.Boolean("dry_run", "set/revert: say what would change, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds the package open)."),
                    McpSchema.Limit(100, "events listed", 2000),
                    McpSchema.Offset("events listed")),
                Destructive = true,
                Run = TuneSoundEvent,
            };
        }

        private static object TuneSoundEvent(McpCall call)
        {
            string action = (call.Str("action") ?? "get").Trim().ToLowerInvariant();
            if (action != "get" && action != "set" && action != "revert" && action != "list") throw new McpError("'action' is get, set, revert or list.");
            string package = McpEditor.UI(() => LevelSoundOverride.PathForOpenLevel());
            if (package == null) throw new McpError("Open a level first: the sound package is the level's own.");
            JObject result = new JObject() { ["package"] = McpAnimationAssetTools.Relative(package) };

            if (action == "list")
            {
                List<LevelSoundOverride.TunedEvent> tuned;
                using (McpEditorTools.Heartbeat(call, "Comparing the level's soundbanks with the base game's"))
                    tuned = LevelSoundOverride.TunedEvents(package);
                result["count"] = tuned.Count;
                McpPaging.Page(call, tuned, result, "events", o => new JObject() { ["event"] = o.Name, ["id"] = o.EventId.ToString(), ["bank"] = o.Bank, ["changes"] = new JArray(o.Changes) }, 100, 2000);
                return result;
            }

            string eventName = call.Str("event", required: true).Trim();
            bool dryRun = call.Bool("dry_run");
            LevelSoundOverride.Tuning tuning;
            if (action == "get")
            {
                using (McpEditorTools.Heartbeat(call, "Reading the event"))
                    tuning = LevelSoundOverride.Inspect(package, eventName);
            }
            else if (action == "revert")
            {
                if (!dryRun) McpAnimationAssetTools.CloseOrRefuse(call, "it holds the level's sound package open");
                using (McpEditorTools.Heartbeat(call, "Putting the event back"))
                    tuning = LevelSoundOverride.Untune(package, eventName, !dryRun);
            }
            else
            {
                SoundEventTuning.Settings settings = ReadSettings(call);
                if (!dryRun) McpAnimationAssetTools.CloseOrRefuse(call, "it holds the level's sound package open");
                using (McpEditorTools.Heartbeat(call, "Tuning the event in the level's sound package"))
                    tuning = LevelSoundOverride.Tune(package, eventName, settings, !dryRun);
            }

            LevelSoundOverride.Plan plan = tuning.Plan;
            if (!plan.Ok)
                throw new McpError((action == "get" ? "" : "Nothing was changed: ") + plan.Problem + (plan.HeldPermanently ? " (permanent soundbank)" : ""));
            if (action != "get" && !dryRun)
                McpEditor.UI(() => LevelSoundOverride.RaiseChanged());

            result["event"] = eventName;
            if (action != "get") result["dry_run"] = dryRun;
            if (action == "revert")
            {
                if (tuning.StopEvent != null) result["stop_event"] = tuning.StopEvent;
            }
            else
            {
                result["tuned_on_this_level"] = tuning.Tuned;
                result["banks"] = new JArray(tuning.Banks);
                if (tuning.After != null) result["now"] = Describe(tuning.After);
                if (tuning.Shipped != null) result["shipped"] = Describe(tuning.Shipped);
                List<string> changes = tuning.Changes;
                if (changes.Count != 0) result["changes_from_shipped"] = new JArray(changes);
                if (tuning.StopEvent != null)
                {
                    result["stop_event"] = tuning.StopEvent;
                    if (tuning.StopAfter != null) result["stop_event_now"] = Describe(tuning.StopAfter);
                }
                if (tuning.AlsoPlay.Count != 0) result["also_played_by"] = new JArray(tuning.AlsoPlay.Take(30));
            }
            if (plan.BanksCopied.Count != 0) result["banks_copied_in"] = new JArray(plan.BanksCopied);
            if (plan.BanksChanged.Count != 0) result["banks_changed"] = new JArray(plan.BanksChanged);
            if (plan.BanksRemoved.Count != 0) result["banks_taken_out"] = new JArray(plan.BanksRemoved);
            if (action != "get") result["package_bytes"] = plan.SizeAfter;
            if (plan.Notes.Count != 0) result["notes"] = new JArray(plan.Notes);
            if (action == "get" && !tuning.Tuned)
                call.Note("set changes it for this level; replace_sound level_only swaps its audio; describe_sound_event says which SoundLoadBank loads its bank.");
            return result;
        }

        private static SoundEventTuning.Settings ReadSettings(McpCall call)
        {
            SoundEventTuning.Settings s = new SoundEventTuning.Settings();
            if (call.Has("volume_db")) s.VolumeDb = (float)call.Num("volume_db");
            if (call.Has("volume_change_db")) s.VolumeChangeDb = (float)call.Num("volume_change_db");
            if (call.Has("pitch_cents")) s.PitchCents = (float)call.Num("pitch_cents");
            if (call.Has("delay_ms")) s.DelayMs = call.Int("delay_ms");
            if (call.Has("fade_in_ms")) s.FadeInMs = call.Int("fade_in_ms");
            if (call.Has("fade_out_ms")) s.FadeOutMs = call.Int("fade_out_ms");
            if (call.Has("max_distance_m")) s.MaxDistance = (float)call.Num("max_distance_m");
            if (call.Has("stops"))
            {
                switch ((call.Str("stops") ?? "").Trim().ToLowerInvariant())
                {
                    case "own": s.Stops = SoundEventTuning.StopReach.Own; break;
                    case "mixer": s.Stops = SoundEventTuning.StopReach.Mixer; break;
                    case "bus": s.Stops = SoundEventTuning.StopReach.Bus; break;
                    default: throw new McpError("'stops' is own, mixer or bus.");
                }
            }
            if (call.Has("loop"))
            {
                string loop = call.Str("loop").Trim().ToLowerInvariant();
                int count;
                if (loop == "forever" || loop == "for ever" || loop == "infinite" || loop == "true") s.Loop = 0;
                else if (loop == "once" || loop == "false" || loop == "off") s.Loop = 1;
                else if (int.TryParse(loop, NumberStyles.Integer, CultureInfo.InvariantCulture, out count)) s.Loop = count;
                else throw new McpError("'loop' is 'forever', 'once' or a count.");
            }
            string invalid = s.Check();
            if (invalid != null) throw new McpError(invalid);
            if (!s.Any) throw new McpError("set needs at least one of volume_db, volume_change_db, pitch_cents, delay_ms, fade_in_ms, fade_out_ms, stops, loop, max_distance_m.");
            return s;
        }

        /// <summary>An event as one bank has it, for a client.</summary>
        internal static JObject Describe(SoundEventTuning.EventState e)
        {
            JObject o = new JObject();
            JArray actions = new JArray();
            foreach (SoundEventTuning.ActionState a in e.Actions)
            {
                if (a.Missing) { actions.Add(new JObject() { ["id"] = a.Id.ToString(), ["missing"] = true }); continue; }
                JObject row = new JObject() { ["kind"] = a.Kind, ["id"] = a.Id.ToString(), ["target"] = a.Target.ToString() };
                if (a.TargetIsBus) row["target_is_bus"] = true;
                if (a.IsPlay || a.IsStop || a.DelayMs.HasValue) row["delay_ms"] = a.DelayMs ?? 0;
                if (a.DelayRange != null && (a.DelayRange[0] != 0 || a.DelayRange[1] != 0)) row["delay_random_ms"] = new JArray(a.DelayRange[0], a.DelayRange[1]);
                if (a.IsPlay || a.IsStop || a.FadeMs.HasValue) row[a.IsStop ? "fade_out_ms" : a.IsPlay ? "fade_in_ms" : "fade_ms"] = a.FadeMs ?? 0;
                if (a.IsStop)
                {
                    row["on"] = a.OnOneObject ? "the object that plays the stop" : "every object";
                    if (a.Mixer != 0) row["actor_mixer_above"] = a.Mixer.ToString();
                    if (a.Bus != 0) row["bus"] = a.Bus.ToString();
                }
                actions.Add(row);
            }
            o["actions"] = actions;
            JArray targets = new JArray();
            foreach (SoundEventTuning.TargetState t in e.Targets)
            {
                JObject row = new JObject() { ["id"] = t.Id.ToString(), ["type"] = t.TypeName };
                if (t.NotTunable) { row["tunable"] = false; targets.Add(row); continue; }
                row["volume_db"] = Math.Round(t.VolumeDb, 2);
                row["pitch_cents"] = Math.Round(t.PitchCents, 1);
                row["sounds"] = t.Sounds.Count;
                List<int?> loops = t.Sounds.Select(s => s.Value).Distinct().ToList();
                row["loop"] = loops.Count == 0 ? "-" : loops.Count > 1 ? "mixed" : loops[0] == null || loops[0] == 1 ? "once" : loops[0] == 0 ? "forever" : loops[0] + " times";
                row["positioning"] = t.Positioning;
                if (t.MaxDistance.HasValue) row["max_distance_m"] = Math.Round(t.MaxDistance.Value, 2);
                targets.Add(row);
            }
            o["plays"] = targets;
            if (e.MaxDistance.HasValue) o["max_distance_m"] = Math.Round(e.MaxDistance.Value, 2);
            return o;
        }
    }
}
