using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>Shared by the script checks.</summary>
    internal static class LevelCheckScript
    {
        public static string Key(Composite composite, Entity entity) => composite.shortGUID.ToByteString() + "/" + entity.shortGUID.ToByteString();

        /// <summary>Refuse a fix whose entity has gone since the check ran.</summary>
        public static void RequireStill(McpScriptEdit edit, Composite composite, Entity entity)
        {
            if (!edit.Commands.Entries.Contains(composite) || composite.GetEntityByID(entity.shortGUID) != entity)
                throw new McpError("That entity is no longer in the level (it was changed since the Level Check ran): run the Level Check again.");
        }

        /// <summary>
        /// The entity a path from a composite points at (its last step), following instances down; null when a step is missing. A
        /// path's trailing terminator is ignored; an empty path is the composite itself (no entity).
        /// </summary>
        public static Entity Resolve(Commands commands, Composite from, ShortGuid[] path, out Composite holder)
        {
            holder = from;
            if (path == null) return null;
            int count = path.Length;
            if (count > 0 && path[count - 1] == ShortGuid.Invalid) count--;
            Entity entity = null;
            Composite at = from;
            for (int i = 0; i < count; i++)
            {
                if (at == null) return null;
                entity = at.GetEntityByID(path[i]);
                if (entity == null) return null;
                holder = at;
                if (i == count - 1) break;
                if (!(entity is FunctionEntity function) || function.function.IsFunctionType) return null;
                at = commands.GetComposite(function.function);
            }
            return entity;
        }

        /// <summary>Whether a composite holds an entity of one of the types, itself or anywhere it places.</summary>
        public static bool HoldsAny(Commands commands, Composite composite, HashSet<FunctionType> types, Dictionary<Composite, bool> memo)
        {
            if (composite == null) return false;
            if (memo.TryGetValue(composite, out bool known)) return known;
            memo[composite] = false;
            bool result = composite.functions.Any(o => LevelCheckContext.IsAny(o, types));
            if (!result)
                foreach (FunctionEntity function in composite.functions)
                    if (!function.function.IsFunctionType && HoldsAny(commands, commands.GetComposite(function.function), types, memo)) { result = true; break; }
            memo[composite] = result;
            return result;
        }
    }

    /// <summary>A SoundLoadBank with its bank only typed on sound_bank loads nothing.</summary>
    internal sealed class SoundBankTypedRule : LevelCheckRule
    {
        public override string Id => "sound_bank_typed";
        public override string Name => "Sound bank never loads";
        public override string About => "A SoundLoadBank with the bank typed on its sound_bank pin and no VariableString linked to it loads nothing: the game only reads the linked string.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<McpSoundTools.BankLoader> loaders = McpSoundTools.BankLoaders(context.Commands).Where(o => context.Placed.Contains(o.Composite)).ToList();
            //A bank another loader of the level loads properly is loaded all the same
            HashSet<string> loaded = new HashSet<string>(loaders.Where(o => !o.TypedOnly).Select(o => o.Bank), StringComparer.OrdinalIgnoreCase);
            foreach (McpSoundTools.BankLoader loader in loaders)
            {
                if (!loader.TypedOnly || loaded.Contains(loader.Bank)) continue;
                Composite composite = loader.Composite;
                FunctionEntity entity = loader.Entity;
                string bank = loader.Bank;
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, entity),
                    Severity = LevelCheckSeverity.Broken,
                    Title = "Sound bank '" + bank + "' never loads",
                    Text = context.Where(composite, entity) + " has the bank typed on its sound_bank pin, which the game never reads - it loads the banks named by the " +
                           "VariableStrings linked to that pin (as retail does, up to 16 per loader). Until one is linked, every sound in '" + bank + "' stays silent.",
                    Sources = { context.Source(composite, entity, "sound_bank '" + bank + "'") },
                    Fix = new LevelCheckFix()
                    {
                        Label = "Link a VariableString '" + bank + "' to it",
                        Detail = "Adds a VariableString {initial_value: '" + bank + "'} beside the SoundLoadBank and links sound_bank -> reference, as retail does.",
                        Apply = fromTool => LevelCheckFix.Script("link bank string " + bank, composite, fromTool, edit =>
                        {
                            LevelCheckScript.RequireStill(edit, composite, entity);
                            if (entity.childLinks.Any(o => o.thisParamID == ShortGuidUtils.Generate("sound_bank")))
                                return "It already has a bank string linked.";
                            FunctionEntity text = edit.AddFunction(composite, FunctionType.VariableString, "SoundBank_" + bank);
                            edit.SetParameter(composite, text, "initial_value", new JValue(bank));
                            edit.AddLink(composite, entity, "sound_bank", text, "reference");
                            return "Linked a VariableString '" + bank + "' to " + context.Where(composite, entity) + ".sound_bank.";
                        }),
                    },
                });
            }
        }
    }

    /// <summary>Two placed particle emitters with one entity id corrupt each other in game: hard white quads (duplicate_composite keeps ids).</summary>
    internal sealed class SameIdEmittersRule : LevelCheckRule
    {
        internal static readonly FunctionType[] Emitters = { FunctionType.ParticleEmitterReference, FunctionType.RibbonEmitterReference, FunctionType.GPU_PFXEmitterReference };
        public override string Id => "emitter_same_id";
        public override string Name => "Emitters sharing an id";
        public override string About => "Particle emitters with the same entity id in two different placed composites (a duplicated composite placed beside its original) corrupt each other in game: hard white quads.";
        public override IEnumerable<FunctionType> Walks => Emitters;

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            //Real placements of each emitter id, and the distinct composites holding them
            Dictionary<ShortGuid, List<McpPlacements.Placement>> byId = new Dictionary<ShortGuid, List<McpPlacements.Placement>>();
            foreach (FunctionType type in Emitters)
                foreach (McpPlacements.Placement placement in context.RealPlacements(type))
                {
                    ShortGuid id = placement.Chain[placement.Chain.Count - 1].shortGUID;
                    if (!byId.TryGetValue(id, out List<McpPlacements.Placement> list)) byId[id] = list = new List<McpPlacements.Placement>();
                    list.Add(placement);
                }
            //One finding per pair of composites that share ids (a copy and its original), listing the emitters
            Dictionary<string, (Composite a, Composite b, List<Entity> emitters, List<McpPlacements.Placement> placements)> pairs = new Dictionary<string, (Composite, Composite, List<Entity>, List<McpPlacements.Placement>)>();
            foreach (KeyValuePair<ShortGuid, List<McpPlacements.Placement>> entry in byId)
            {
                List<(Composite holder, McpPlacements.Placement placement)> held = entry.Value.Select(o => (context.HolderOf(o), o)).Where(o => o.Item1 != null).ToList();
                List<Composite> holders = held.Select(o => o.holder).Distinct().OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();
                if (holders.Count < 2) continue;
                string key = string.Join("|", holders.Select(o => o.shortGUID.ToByteString()));
                if (!pairs.TryGetValue(key, out var pair))
                    pairs[key] = pair = (holders[0], holders[1], new List<Entity>(), new List<McpPlacements.Placement>());
                pair.emitters.Add(entry.Value[0].Chain[entry.Value[0].Chain.Count - 1]);
                foreach (Composite holder in holders)
                    pair.placements.Add(held.First(o => o.holder == holder).placement);
            }
            foreach (KeyValuePair<string, (Composite a, Composite b, List<Entity> emitters, List<McpPlacements.Placement> placements)> pair in pairs)
            {
                LevelCheckFinding finding = new LevelCheckFinding()
                {
                    Key = pair.Key,
                    Severity = LevelCheckSeverity.Broken,
                    Title = pair.Value.emitters.Count + " particle emitter" + (pair.Value.emitters.Count == 1 ? "" : "s") + " in " + EditorUtils.GetCompositeName(pair.Value.a) + " and " + EditorUtils.GetCompositeName(pair.Value.b) + " share entity ids",
                    Text = "Both composites are placed and hold emitters with the same entity ids - a duplicated composite (Duplicate / duplicate_composite keeps ids) placed beside its original. " +
                           "Emitters with one id corrupt each other in game and draw as hard white quads. Give one copy its own emitters: copy them in (Copy / copy_entities gives new ids), " +
                           "delete the old ones, and link the copies as the old ones were.",
                };
                foreach (McpPlacements.Placement placement in pair.Value.placements.Take(12))
                    finding.Sources.Add(context.Source(placement));
                found.Add(finding);
            }
        }
    }

    /// <summary>Emitters copied from one emitter share its material; without unique_material, what one sets bleeds into the others.</summary>
    internal sealed class SharedEmitterMaterialRule : LevelCheckRule
    {
        public override string Id => "emitter_shared_material";
        public override string Name => "Emitters sharing a material";
        public override string About => "Emitters that share one material but are coloured differently, without unique_material: their colours bleed into each other in game.";

        private static bool Colourish(ShortGuid name)
        {
            string text = ShortGuidUtils.FindString(name) ?? "";
            return text.IndexOf("COLOUR", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("TEXTURE_MAP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Signature(Entity entity) => string.Join(";", entity.parameters.Where(o => o?.content != null && Colourish(o.name))
            .OrderBy(o => o.name.AsUInt32).Select(o => o.name.AsUInt32 + "=" + LevelCheckValues.Text(o.content)));

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            List<(Composite composite, FunctionEntity entity)> emitters = SameIdEmittersRule.Emitters.SelectMany(o => context.InPlaced(o)).ToList();
            foreach (IGrouping<string, (Composite composite, FunctionEntity entity)> group in emitters.GroupBy(o => LevelCheckContext.Text(o.entity, "material") ?? "").Where(o => o.Key != ""))
            {
                List<(Composite composite, FunctionEntity entity)> members = group.ToList();
                if (members.Count < 2) continue;
                List<(Composite composite, FunctionEntity entity)> shared = members.Where(o => LevelCheckContext.Bool(o.entity, "unique_material") != true).ToList();
                if (shared.Count < 2) continue;
                /* Retail shares one material among differently coloured emitters inside one composite and they draw right; the bleed is
                   between copies in different composites. So: emitters in two or more composites, one of them the user's, whose looks differ */
                List<IGrouping<Composite, (Composite composite, FunctionEntity entity)>> byComposite = shared.GroupBy(o => o.composite).ToList();
                if (byComposite.Count < 2 || !byComposite.Any(o => context.IsUsers(o.Key))) continue;
                if (byComposite.Select(o => string.Join("|", o.Select(e => Signature(e.entity)).Distinct().OrderBy(s => s))).Distinct().Count() < 2) continue;
                shared = shared.Where(o => context.IsUsers(o.composite)).ToList();
                LevelCheckFinding finding = new LevelCheckFinding()
                {
                    Key = group.Key,
                    Severity = LevelCheckSeverity.Broken,
                    Title = shared.Count + " differently coloured emitters share one material",
                    Text = "These emitters were made from the same emitter (they share material " + group.Key + ") but have different colours or textures, and none of them has " +
                           "unique_material. They then draw with each other's colours in game. Tick unique_material on each.",
                };
                foreach ((Composite composite, FunctionEntity entity) in shared.Take(12))
                    finding.Sources.Add(context.Source(composite, entity));
                List<(Composite composite, FunctionEntity entity)> toFix = shared;
                finding.Fix = new LevelCheckFix()
                {
                    Label = "Tick unique_material on " + toFix.Count + " emitters",
                    Detail = "Sets unique_material on each, so the build gives each its own copy of the material.",
                    Apply = fromTool => LevelCheckFix.Script("unique_material on " + toFix.Count + " emitters", toFix[0].composite, fromTool, edit =>
                    {
                        int set = 0;
                        foreach ((Composite composite, FunctionEntity entity) in toFix)
                        {
                            if (!edit.Commands.Entries.Contains(composite) || composite.GetEntityByID(entity.shortGUID) != entity) continue;
                            edit.SetParameter(composite, entity, "unique_material", new JValue(true));
                            set++;
                        }
                        return "Ticked unique_material on " + set + " emitter" + (set == 1 ? "." : "s.");
                    }),
                };
                found.Add(finding);
            }
        }
    }

    /// <summary>CMD_PlayAnimation with location / orientation convergence and nothing to converge to: an error-log line, and the clip plays where the character stands.</summary>
    internal sealed class PlayAnimationMarkerRule : LevelCheckRule
    {
        private static readonly ShortGuid Marker = ShortGuidUtils.Generate("Marker");
        public override string Id => "play_animation_marker";
        public override string Name => "Animation converges to no marker";
        public override string About => "A CMD_PlayAnimation with LocationConvergence or OrientationConvergence on and no Marker linked: the game logs an error and the character doesn't move to where the clip should play.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            foreach ((Composite composite, FunctionEntity entity) in context.InUsers(FunctionType.CMD_PlayAnimation))
            {
                bool location = LevelCheckContext.Bool(entity, "LocationConvergence") == true;
                //OrientationConvergence is on unless the entity says otherwise
                bool orientation = LevelCheckContext.Bool(entity, "OrientationConvergence") ?? true;
                if (!location && !orientation) continue;
                if (entity.GetParameter(Marker) != null || LevelCheckContext.PinLinked(composite, entity, Marker) || context.PinLinkedByAlias(entity, Marker)) continue;
                string which = location && orientation ? "LocationConvergence and OrientationConvergence" : location ? "LocationConvergence" : "OrientationConvergence";
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, entity),
                    Severity = LevelCheckSeverity.Warning,
                    Title = context.Where(composite, entity) + " converges to no marker",
                    Text = "It has " + which + " on but nothing linked to Marker, so there is nowhere to move the character to: the game logs an error and the clip plays where the character " +
                           "happens to stand. Link Marker to a PositionMarker where the clip should play, or turn the convergence off.",
                    Sources = { context.Source(composite, entity, which) },
                    Fix = new LevelCheckFix()
                    {
                        Label = "Turn the convergence off",
                        Detail = "Sets LocationConvergence and OrientationConvergence false: the clip plays where the character stands (link a Marker instead if it should play somewhere).",
                        Apply = fromTool => LevelCheckFix.Script("convergence off", composite, fromTool, edit =>
                        {
                            LevelCheckScript.RequireStill(edit, composite, entity);
                            if (location) edit.SetParameter(composite, entity, "LocationConvergence", new JValue(false));
                            if (orientation) edit.SetParameter(composite, entity, "OrientationConvergence", new JValue(false));
                            return "Turned " + which + " off on " + context.Where(composite, entity) + ".";
                        }),
                    },
                });
            }
        }
    }

    /// <summary>NPC_FakeSense with FakePosition not linked: the NPC never reacts (every retail use links it).</summary>
    internal sealed class FakeSensePositionRule : LevelCheckRule
    {
        private static readonly ShortGuid FakePosition = ShortGuidUtils.Generate("FakePosition");
        public override string Id => "fake_sense_position";
        public override string Name => "Fake sense with no position";
        public override string About => "An NPC_FakeSense whose FakePosition is not linked: the NPC never reacts to it (a typed position isn't enough).";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            foreach ((Composite composite, FunctionEntity entity) in context.InUsers(FunctionType.NPC_FakeSense))
            {
                if (LevelCheckContext.PinLinked(composite, entity, FakePosition) || context.PinLinkedByAlias(entity, FakePosition)) continue;
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, entity),
                    Severity = LevelCheckSeverity.Broken,
                    Title = context.Where(composite, entity) + " senses nothing",
                    Text = "Its FakePosition isn't linked. Every retail NPC_FakeSense links it - to a PositionMarker, a camera or VariableThePlayer - and one left with a typed position " +
                           "(0,0,0 or anything else) left the alien idle. Link FakePosition to what the NPC should sense (VariableThePlayer to send it at the player).",
                    Sources = { context.Source(composite, entity) },
                });
            }
        }
    }

    /// <summary>RandomFloat.Result feeding LogicDelay.delay does nothing: retail uses Archetypes\Script\Logic\RandomDelay.</summary>
    internal sealed class RandomFloatDelayRule : LevelCheckRule
    {
        private static readonly ShortGuid Result = ShortGuidUtils.Generate("Result");
        private static readonly ShortGuid Delay = ShortGuidUtils.Generate("delay");
        public override string Id => "random_delay";
        public override string Name => "Random delay that never varies";
        public override string About => "RandomFloat.Result linked to LogicDelay.delay does not change the delay in game; retail uses the RandomDelay archetype.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            foreach ((Composite composite, FunctionEntity delay) in context.InUsers(FunctionType.LogicDelay))
            {
                foreach (Entity other in composite.GetEntities())
                {
                    if (!LevelCheckContext.Is(other, FunctionType.RandomFloat)) continue;
                    bool linked = delay.childLinks.Any(o => o.thisParamID == Delay && o.linkedEntityID == other.shortGUID && o.linkedParamID == Result)
                               || other.childLinks.Any(o => o.thisParamID == Result && o.linkedEntityID == delay.shortGUID && o.linkedParamID == Delay);
                    if (!linked) continue;
                    found.Add(new LevelCheckFinding()
                    {
                        Key = LevelCheckScript.Key(composite, delay),
                        Severity = LevelCheckSeverity.Broken,
                        Title = context.Where(composite, delay) + " gets its delay from a RandomFloat, which does nothing",
                        Text = "A RandomFloat's Result linked into LogicDelay.delay does not vary the delay in game. Use retail's Archetypes\\Script\\Logic\\RandomDelay " +
                               "(port it from ENG_TOWPLATFORM with Port Composites / port_composites) and trigger that instead.",
                        Sources = { context.Source(composite, delay), context.Source(composite, other) },
                    });
                    break;
                }
            }
        }
    }

    /// <summary>
    /// CAGEAnimation tracks left by deletes: bindings to entities that are gone, and float tracks nothing plays. Each delete and
    /// re-create of the animated entities adds more.
    /// </summary>
    internal sealed class DeadAnimationTracksRule : LevelCheckRule
    {
        public override string Id => "dead_animation_tracks";
        public override string Name => "Dead animation tracks";
        public override string About => "CAGEAnimation tracks bound to entities that no longer exist, and float tracks nothing plays: left by deletes, they grow the animation every time.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            foreach ((Composite composite, FunctionEntity function) in context.InUsers(FunctionType.CAGEAnimation))
            {
                if (!(function is CAGEAnimation animation)) continue;
                List<CAGEAnimation.Connection> dead = animation.connections.Where(o => o != null && o.binding_type == ObjectType.ENTITY && o.connectedEntity?.path != null &&
                    o.connectedEntity.path.Any(p => p != ShortGuid.Invalid) && LevelCheckScript.Resolve(context.Commands, composite, o.connectedEntity.path, out _) == null).ToList();
                HashSet<ShortGuid> used = new HashSet<ShortGuid>(animation.connections.Where(o => o != null && !dead.Contains(o)).Select(o => o.target_track));
                List<CAGEAnimation.FloatTrack> loose = animation.floatTracks.Where(o => o != null && !used.Contains(o.shortGUID)).ToList();
                if (dead.Count == 0 && loose.Count == 0) continue;
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, animation),
                    Severity = LevelCheckSeverity.Warning,
                    Title = context.Where(composite, animation) + " has " + (dead.Count != 0 ? dead.Count + " track binding" + (dead.Count == 1 ? "" : "s") + " to deleted entities" : "") +
                            (dead.Count != 0 && loose.Count != 0 ? " and " : "") + (loose.Count != 0 ? loose.Count + " float track" + (loose.Count == 1 ? "" : "s") + " nothing plays" : ""),
                    Text = "Deleting an animated entity leaves its tracks in the CAGEAnimation: they key nothing, and each delete and re-create adds more, so the animation grows. Remove them.",
                    Sources = { context.Source(composite, animation) },
                    Fix = new LevelCheckFix()
                    {
                        Label = "Remove the dead tracks",
                        Detail = "Removes the bindings to deleted entities and the float tracks no binding plays.",
                        Apply = fromTool => LevelCheckFix.Script("remove dead animation tracks", composite, fromTool, edit =>
                        {
                            LevelCheckScript.RequireStill(edit, composite, animation);
                            edit.Touch(composite, animation, look: false);
                            List<CAGEAnimation.Connection> deadNow = animation.connections.Where(o => dead.Contains(o)).ToList();
                            animation.connections = animation.connections.Where(o => !deadNow.Contains(o)).ToList();
                            HashSet<ShortGuid> usedNow = new HashSet<ShortGuid>(animation.connections.Where(o => o != null).Select(o => o.target_track));
                            //Float tracks are not in the transaction's snapshot: put back on undo, taken out again on redo
                            List<CAGEAnimation.FloatTrack> before = new List<CAGEAnimation.FloatTrack>(animation.floatTracks);
                            List<CAGEAnimation.FloatTrack> after = before.Where(o => o != null && usedNow.Contains(o.shortGUID)).ToList();
                            animation.floatTracks = new List<CAGEAnimation.FloatTrack>(after);
                            edit.OnEachApply(reverted => animation.floatTracks = new List<CAGEAnimation.FloatTrack>(reverted ? before : after));
                            edit.Changed(composite);
                            return "Removed " + deadNow.Count + " binding" + (deadNow.Count == 1 ? "" : "s") + " and " + (before.Count - after.Count) + " float track" + (before.Count - after.Count == 1 ? "" : "s") + " from " + context.Where(composite, animation) + ".";
                        }),
                    },
                });
            }
        }
    }

    /// <summary>
    /// A CAGEAnimation (or RotateInTime clock) inside a composite instance that another CAGEAnimation keys stays at time 0 for as long
    /// as the outer one plays.
    /// </summary>
    internal sealed class NestedAnimationRule : LevelCheckRule
    {
        private static readonly HashSet<FunctionType> Clocks = new HashSet<FunctionType>() { FunctionType.CAGEAnimation, FunctionType.RotateInTime, FunctionType.RotateAtSpeed };
        public override string Id => "nested_animation";
        public override string Name => "Animation inside a keyed instance";
        public override string About => "A CAGEAnimation (or RotateInTime) inside a composite instance that another CAGEAnimation keys freezes at time 0 while the outer one plays.";

        public override void Run(LevelCheckContext context, List<LevelCheckFinding> found)
        {
            Dictionary<Composite, bool> memo = new Dictionary<Composite, bool>();
            foreach ((Composite composite, FunctionEntity function) in context.InUsers(FunctionType.CAGEAnimation))
            {
                if (!(function is CAGEAnimation animation)) continue;
                HashSet<Entity> keyed = new HashSet<Entity>();
                foreach (CAGEAnimation.Connection connection in animation.connections)
                {
                    if (connection?.connectedEntity?.path == null) continue;
                    Entity target = LevelCheckScript.Resolve(context.Commands, composite, connection.connectedEntity.path, out _);
                    if (target is FunctionEntity instance && !instance.function.IsFunctionType)
                        keyed.Add(target);
                }
                List<string> frozen = new List<string>();
                foreach (FunctionEntity instance in keyed.OfType<FunctionEntity>())
                {
                    Composite inner = context.Commands.GetComposite(instance.function);
                    if (inner != null && LevelCheckScript.HoldsAny(context.Commands, inner, Clocks, memo))
                        frozen.Add(context.EntityName(composite, instance) + " (" + EditorUtils.GetCompositeName(inner) + ")");
                }
                if (frozen.Count == 0) continue;
                found.Add(new LevelCheckFinding()
                {
                    Key = LevelCheckScript.Key(composite, animation),
                    Severity = LevelCheckSeverity.Broken,
                    Title = context.Where(composite, animation) + " keys instances whose own animations will freeze",
                    Text = "It keys " + string.Join(", ", frozen) + ", which hold animations of their own (a CAGEAnimation or RotateInTime / RotateAtSpeed). While an outer " +
                           "CAGEAnimation plays, those inside the instance it keys stay at time 0 (a start link or an external clock does not help). Key the moving parts " +
                           "from the outer animation itself (in its own space), or move the inner animation out of the keyed instance.",
                    Sources = { context.Source(composite, animation) },
                });
            }
        }
    }

    /// <summary>Parameter values as text, to compare them.</summary>
    internal static class LevelCheckValues
    {
        public static string Text(ParameterData data)
        {
            switch (data)
            {
                case cBool b: return b.value ? "1" : "0";
                case cFloat f: return f.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                case cInteger i: return i.value.ToString();
                case cString s: return s.value ?? "";
                case cEnum e: return e.enumID.ToByteString() + "." + e.enumIndex;
                case cVector3 v: return v.value.X.ToString("R") + "," + v.value.Y.ToString("R") + "," + v.value.Z.ToString("R");
                case cTransform t: return t.position.ToString() + t.rotation.ToString();
                default: return data?.GetType().Name ?? "";
            }
        }
    }
}
