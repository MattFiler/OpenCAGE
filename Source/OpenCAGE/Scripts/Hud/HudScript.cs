using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.Hud
{
    /// <summary>
    /// The level script that runs a HUD overlay: a composite HUD\&lt;Name&gt; made (or remade) from the design, as one undo step.
    ///
    ///   Overlay                     FlashScript: the level's movie (DATA/ENV/&lt;level&gt;/UI/OVERLAYS/&lt;NAME&gt;.GFX) on the ChallengeMapOverlay layer (HudDesign.LayerName)
    ///   pins show / hide            the whole overlay (the FlashScript's show / hide)
    ///   pin &lt;slot&gt; (float)          a value slot fed from outside: link any number to it on the instance
    ///   pins Show / Hide &lt;slot&gt;     one slot (when the design asks for them)
    ///   Push values                 Thinker, every 0.1 s: each value slot's FlashInvoke hudSet(slot, value) (INT_FLOAT; INT_INT for whole numbers)
    ///   player health               TriggerBindCharacter on the player -> CHR_GetHealth -> a VariableInt
    ///   counting up                 FloatAdd + 0.1 on every push into a VariableFloat (paused with the game, as the script is)
    ///   Keep game HUD parts off     Thinker, every 0.5 s: FlashInvokes on the game's 'weaponstuff' layer - HideAmmoCounter, then the
    ///                               ammo counter (child 1 of 6) swapped to the end and removed; the health bar the same way. The
    ///                               game re-shows the counter after every shot, so a plain hide never sticks.
    ///
    /// Remaking replaces the composite's entities but keeps pins still in the design, so links to them on the instance survive. An
    /// instance is placed in the level's root the first time.
    /// </summary>
    internal static class HudScript
    {
        public const string Folder = "HUD";
        public const string ShowPin = "show", HidePin = "hide";
        public const string SetName = "Set ", ShowName = "Show ", HideName = "Hide ";

        public static string CompositePath(string name) => Folder + "\\" + name;

        /// <summary>The design's composite in the level, or null.</summary>
        public static Composite Find(Commands commands, string name) => commands?.GetComposite(CompositePath(name));

        /// <summary>The pins a design's composite has, with their kinds.</summary>
        public static List<KeyValuePair<string, CompositePinType>> Pins(HudDesign design)
        {
            List<KeyValuePair<string, CompositePinType>> pins = new List<KeyValuePair<string, CompositePinType>>()
            {
                new KeyValuePair<string, CompositePinType>(ShowPin, CompositePinType.CompositeMethodPin),
                new KeyValuePair<string, CompositePinType>(HidePin, CompositePinType.CompositeMethodPin),
            };
            foreach (HudSlot slot in design.Slots)
            {
                if (slot.TakesValue && slot.Source == HudSource.Pin)
                    pins.Add(new KeyValuePair<string, CompositePinType>(slot.Name, CompositePinType.CompositeInputFloatVariablePin));
                if (slot.ShowHidePins)
                {
                    pins.Add(new KeyValuePair<string, CompositePinType>(ShowName + slot.Name, CompositePinType.CompositeMethodPin));
                    pins.Add(new KeyValuePair<string, CompositePinType>(HideName + slot.Name, CompositePinType.CompositeMethodPin));
                }
            }
            return pins;
        }

        /// <summary>What making the script did.</summary>
        public sealed class Made
        {
            public Composite Composite;
            public FunctionEntity Instance;
            /// <summary>The composite holding the instance.</summary>
            public Composite InstanceIn;
            public bool CreatedComposite, PlacedInstance;
            public int Entities;
            public List<string> PinsAdded = new List<string>(), PinsRemoved = new List<string>();
            public List<string> Notes = new List<string>();
        }

        /// <summary>
        /// Make or remake the design's composite in an edit (<see cref="McpScriptEdit.Run(string, Composite, Action{McpScriptEdit}, McpScriptEdit.RunOptions)"/>),
        /// with the movie's path for <paramref name="level"/>; place an instance in <paramref name="placeIn"/> (default: the root) when
        /// the level has none.
        /// </summary>
        public static Made Apply(McpScriptEdit edit, HudDesign design, string level, Composite placeIn = null)
        {
            Commands commands = edit.Commands;
            Made made = new Made();
            Composite composite = Find(commands, design.Name);
            if (composite == null)
            {
                composite = edit.AddComposite(CompositePath(design.Name));
                made.CreatedComposite = true;
            }
            made.Composite = composite;

            //The builder's entities go; pins stay when still wanted (their outside links with them)
            foreach (FunctionEntity old in composite.functions.ToList())
                edit.Delete(composite, old);
            List<KeyValuePair<string, CompositePinType>> pins = Pins(design);
            Dictionary<string, VariableEntity> pinEntities = new Dictionary<string, VariableEntity>(StringComparer.OrdinalIgnoreCase);
            foreach (VariableEntity variable in composite.variables.ToList())
            {
                string name = ShortGuidUtils.FindString(variable.name);
                KeyValuePair<string, CompositePinType> wanted = pins.FirstOrDefault(o => string.Equals(o.Key, name, StringComparison.OrdinalIgnoreCase));
                bool keep = wanted.Key != null && PinKind(commands, composite, variable) == wanted.Value;
                if (keep)
                {
                    pinEntities[wanted.Key] = variable;
                    continue;
                }
                edit.Delete(composite, variable);
                made.PinsRemoved.Add(name);
            }
            foreach (KeyValuePair<string, CompositePinType> pin in pins.Where(o => !pinEntities.ContainsKey(o.Key)))
            {
                pinEntities[pin.Key] = edit.AddVariable(composite, pin.Key, pin.Value, null);
                made.PinsAdded.Add(pin.Key);
            }

            Func<FunctionType, string, FunctionEntity> add = (type, name) => { made.Entities++; return edit.AddFunction(composite, type, name); };
            Action<Entity, string, JToken> set = (entity, parameter, value) => edit.SetParameter(composite, entity, parameter, value);
            Action<Entity, string, Entity, string> link = (from, parameter, to, toParameter) => edit.AddLink(composite, from, parameter, to, toParameter);

            //The overlay
            FunctionEntity overlay = add(FunctionType.FlashScript, "Overlay");
            set(overlay, "filename", HudOverlays.GamePath(level, design.Name));
            set(overlay, "layer_name", design.LayerName);
            set(overlay, "target_texture_name", design.LayerName);
            //Loaded and shown with the level (the layer is the game's ChallengeMapOverlay layer, which shows itself whenever the game isn't
            //paused): whether the overlay can be seen is the movie's own - hidden from the start when the design says so, and the
            //show / hide pins call hudShow(-1, on)
            set(overlay, "show_on_reset", true);
            foreach (bool on in new[] { true, false })
            {
                FunctionEntity whole = add(FunctionType.FlashInvoke, on ? "Show overlay" : "Hide overlay");
                set(whole, "layer_name", design.LayerName);
                set(whole, "method", HudMovieBuilder.ShowMethod);
                set(whole, "invoke_type", "INT_INT");
                set(whole, "int_argument_0", -1);
                set(whole, "int_argument_1", on ? 1 : 0);
                link(pinEntities[on ? ShowPin : HidePin], on ? ShowPin : HidePin, whole, "trigger");
            }

            //The values, pushed every tenth of a second
            List<HudSlot> pushed = design.Slots.Where(o => o.TakesValue && o.Source != HudSource.None).ToList();
            FunctionEntity push = null;
            if (pushed.Count != 0)
            {
                push = add(FunctionType.Thinker, "Push values");
                SetThinker(set, push, 0.1);
            }
            FunctionEntity health = null;
            foreach (HudSlot slot in pushed)
            {
                int index = design.Slots.IndexOf(slot);
                FunctionEntity invoke = add(FunctionType.FlashInvoke, SetName + slot.Name);
                set(invoke, "layer_name", design.LayerName);
                set(invoke, "method", HudMovieBuilder.SetMethod);
                set(invoke, "int_argument_0", index);
                link(push, "on_think", invoke, "trigger");
                switch (slot.Source)
                {
                    case HudSource.Pin:
                        set(invoke, "invoke_type", "INT_FLOAT");
                        set(invoke, "float_argument_0", slot.Initial);
                        link(invoke, "float_argument_0", pinEntities[slot.Name], slot.Name);
                        break;
                    case HudSource.CountUp:
                        {
                            set(invoke, "invoke_type", "INT_FLOAT");
                            FunctionEntity seconds = add(FunctionType.VariableFloat, slot.Name + " seconds");
                            set(seconds, "initial_value", slot.Initial);
                            FunctionEntity tick = add(FunctionType.FloatAdd, slot.Name + " tick");
                            set(tick, "RHS", 0.1);
                            link(push, "on_think", tick, "trigger");
                            link(tick, "LHS", seconds, "reference");
                            link(tick, "Result", seconds, "reference");
                            link(invoke, "float_argument_0", seconds, "reference");
                            break;
                        }
                    case HudSource.PlayerHealth:
                        {
                            //Whole numbers: hudSet(slot, health) as INT_INT
                            set(invoke, "invoke_type", "INT_INT");
                            if (health == null)
                            {
                                FunctionEntity player = add(FunctionType.VariableThePlayer, "Player");
                                FunctionEntity bind = add(FunctionType.TriggerBindCharacter, "Bind the player");
                                FunctionEntity get = add(FunctionType.CHR_GetHealth, "Get player health");
                                health = add(FunctionType.VariableInt, "Player health");
                                set(health, "initial_value", 0);
                                link(push, "on_think", bind, "trigger");
                                link(bind, "characters", player, "reference");
                                link(bind, "bound_trigger", get, "trigger");
                                link(get, "Health", health, "reference");
                            }
                            link(invoke, "int_argument_1", health, "reference");
                            break;
                        }
                }
            }

            //Slots shown and hidden from pins
            foreach (HudSlot slot in design.Slots.Where(o => o.ShowHidePins))
            {
                int index = design.Slots.IndexOf(slot);
                foreach (bool on in new[] { true, false })
                {
                    FunctionEntity invoke = add(FunctionType.FlashInvoke, (on ? ShowName : HideName) + slot.Name);
                    set(invoke, "layer_name", design.LayerName);
                    set(invoke, "method", HudMovieBuilder.ShowMethod);
                    set(invoke, "invoke_type", "INT_INT");
                    set(invoke, "int_argument_0", index);
                    set(invoke, "int_argument_1", on ? 1 : 0);
                    string pin = (on ? ShowName : HideName) + slot.Name;
                    link(pinEntities[pin], pin, invoke, "trigger");
                }
            }

            //The game's own HUD parts kept off
            if (design.HideAmmoCounter || design.HideHealthBar)
            {
                FunctionEntity keep = add(FunctionType.Thinker, "Keep game HUD parts off");
                SetThinker(set, keep, 0.5);
                Func<string, string, string, int, int, FunctionEntity> weaponStuff = (name, method, type, a0, a1) =>
                {
                    FunctionEntity invoke = add(FunctionType.FlashInvoke, name);
                    set(invoke, "layer_name", "weaponstuff");
                    set(invoke, "method", method);
                    if (type != null)
                    {
                        set(invoke, "invoke_type", type);
                        set(invoke, "int_argument_0", a0);
                        if (type == "INT_INT") set(invoke, "int_argument_1", a1);
                    }
                    return invoke;
                };
                //The weapon HUD's root holds crosshair, ammo, two grapple prompts, breathing game, health bar (children 0-5); each part
                //goes by swapping it with the last child and removing the last. Once gone the indices are out of range: a no-op.
                List<FunctionEntity> chain = new List<FunctionEntity>();
                if (design.HideAmmoCounter)
                {
                    chain.Add(weaponStuff("Hide ammo counter", "HideAmmoCounter", null, 0, 0));
                    chain.Add(weaponStuff("Ammo counter to the end", "swapChildrenAt", "INT_INT", 1, 5));
                    chain.Add(weaponStuff("Remove ammo counter", "removeChildAt", "INT", 5, 0));
                }
                if (design.HideHealthBar)
                {
                    if (design.HideAmmoCounter)
                    {
                        chain.Add(weaponStuff("Health bar to the end", "swapChildrenAt", "INT_INT", 1, 4));
                        chain.Add(weaponStuff("Remove health bar", "removeChildAt", "INT", 4, 0));
                    }
                    else
                        chain.Add(weaponStuff("Remove health bar", "removeChildAt", "INT", 5, 0));
                }
                link(keep, "on_think", chain[0], "trigger");
                for (int i = 1; i < chain.Count; i++)
                    link(chain[i - 1], "triggered", chain[i], "trigger");
            }

            //An instance in the level the first time
            List<KeyValuePair<Composite, FunctionEntity>> instances = Instances(commands, composite);
            if (instances.Count != 0)
            {
                made.InstanceIn = instances[0].Key;
                made.Instance = instances[0].Value;
            }
            else
            {
                made.InstanceIn = placeIn ?? commands.EntryPoints[0];
                made.Instance = edit.AddInstance(made.InstanceIn, composite, design.Name);
                made.PlacedInstance = true;
            }
            if (made.PinsRemoved.Count != 0)
                made.Notes.Add("Pins no longer in the design were removed (with any links to them): " + string.Join(", ", made.PinsRemoved) + ".");
            return made;
        }

        private static void SetThinker(Action<Entity, string, JToken> set, FunctionEntity thinker, double every)
        {
            set(thinker, "start_on_reset", true);
            set(thinker, "pause_on_reset", false);
            set(thinker, "is_continuous", true);
            set(thinker, "delay_between_triggers", every);
            set(thinker, "use_random_start", false);
            set(thinker, "random_start_delay", 0.0);
            set(thinker, "total_thinking_time", 0.0);
        }

        private static CompositePinType? PinKind(Commands commands, Composite composite, VariableEntity variable)
        {
            CompositePinInfoTable.PinInfo info = commands.Utils.GetPinInfo(composite, variable);
            return info == null ? (CompositePinType?)null : (CompositePinType)info.PinTypeGUID.AsUInt32;
        }

        /// <summary>Instances of a composite in the level: (the composite holding it, the instance).</summary>
        public static List<KeyValuePair<Composite, FunctionEntity>> Instances(Commands commands, Composite composite)
        {
            List<KeyValuePair<Composite, FunctionEntity>> found = new List<KeyValuePair<Composite, FunctionEntity>>();
            if (commands == null || composite == null) return found;
            foreach (Composite holder in commands.Entries.Where(o => o != null))
                foreach (FunctionEntity entity in holder.functions.Where(o => !o.function.IsFunctionType && o.function == composite.shortGUID))
                    found.Add(new KeyValuePair<Composite, FunctionEntity>(holder, entity));
            return found;
        }

        /// <summary>
        /// Why the design can't go in this level, or null: the game's ChallengeMapOverlay layer is one per level, so another FlashScript on
        /// it (a challenge map's own overlay, or another HUD overlay) is a clash.
        /// </summary>
        public static string Clash(Commands commands, string name)
        {
            List<string> others = HudOverlays.ChallengeLayerUsers(commands, Find(commands, name));
            if (others.Count == 0) return null;
            return "The level already loads a movie on the game's " + HudMovieCheck.ChallengeMapLayer + " layer, which a HUD overlay needs and a level has only one of: " +
                string.Join("; ", others) + ". A level shows one overlay (put all its elements in one design), and not in a challenge map with its own overlay.";
        }

        /// <summary>Point every FlashScript that loads a level overlay at this level's own folder (after the level was copied or renamed).</summary>
        public static int FixPaths(McpScriptEdit edit, string pathToAI, string level)
        {
            int fixedCount = 0;
            foreach (OverlayLoader loader in HudOverlays.Loaders(edit.Commands, pathToAI, level).Where(o => !o.PathRight))
            {
                edit.SetParameter(loader.Composite, loader.Entity, "filename", loader.Expected);
                fixedCount++;
            }
            return fixedCount;
        }

        /// <summary>Take a design's composite and its instances out of the level.</summary>
        public static int Remove(McpScriptEdit edit, string name)
        {
            Composite composite = Find(edit.Commands, name);
            if (composite == null) return 0;
            int removed = 0;
            foreach (KeyValuePair<Composite, FunctionEntity> instance in Instances(edit.Commands, composite))
            {
                edit.Delete(instance.Key, instance.Value);
                removed++;
            }
            foreach (FunctionEntity entity in composite.functions.ToList())
                edit.Delete(composite, entity);
            return removed;
        }
    }
}
