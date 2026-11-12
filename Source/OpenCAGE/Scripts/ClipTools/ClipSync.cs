using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.ClipTools
{
    /// <summary>
    /// Locks a character clip's clock to a CAGEAnimation's, so a cutscene's character never drifts from its camera and props. A
    /// CMD_PlayAnimation advances at most 1/24 s a frame (layerUpdate's current_capped_update_time) while a CAGEAnimation advances up to
    /// 0.25 s, so every frame over 41.7 ms leaves the character further behind. Linking ExternalTime (an entity pin) to the CAGEAnimation
    /// makes the clip's time the CAGEAnimation's time minus ExternalStartTime (a VariableFloat) every frame.
    /// </summary>
    internal static class ClipSync
    {
        public const string ExternalTime = "ExternalTime";
        public const string ExternalStartTime = "ExternalStartTime";

        public static bool IsPlayAnimation(Entity entity)
        {
            return entity is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.CMD_PlayAnimation;
        }

        public static bool IsSecondaryAnimation(Entity entity)
        {
            return entity is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.CHR_PlaySecondaryAnimation;
        }

        public static bool IsCageAnimation(Entity entity)
        {
            return entity is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.CAGEAnimation;
        }

        /// <summary>The CAGEAnimations of a composite, to sync to: links only join entities of one composite.</summary>
        public static List<Entity> CageAnimations(Composite composite)
        {
            return composite?.GetEntities().Where(IsCageAnimation).ToList() ?? new List<Entity>();
        }

        /// <summary>What the command's ExternalTime / ExternalStartTime are linked to now (null when not linked).</summary>
        public static Entity LinkedTo(Composite composite, Entity command, string pin)
        {
            EntityConnector link = command.GetLinksOut(pin).FirstOrDefault();
            return link.linkedEntityID.IsInvalid ? null : composite.GetEntityByID(link.linkedEntityID);
        }

        /// <summary>The clip a command plays (its AnimationSet + Animation), or null.</summary>
        public static Animation.ClipReference ClipOf(Entity command)
        {
            Animation.AnimationSet set = McpGlobalAssetChecks.FindSet(McpGlobalAssetChecks.TextOf(command, "AnimationSet"));
            return set == null ? null : McpGlobalAssetChecks.FindClip(set, McpGlobalAssetChecks.TextOf(command, "Animation"));
        }

        /// <summary>Why this can't be synced, or null.</summary>
        public static string Problem(Commands commands, Composite composite, Entity command, Entity cage)
        {
            if (command == null) return "There is no command to sync.";
            if (IsSecondaryAnimation(command))
                return McpScript.EntityName(commands, composite, command) + " is a CHR_PlaySecondaryAnimation: only CMD_PlayAnimation takes an ExternalTime clock.";
            if (!IsPlayAnimation(command)) return McpScript.EntityName(commands, composite, command) + " is not a CMD_PlayAnimation.";
            if (cage == null) return "There is no CAGEAnimation to sync to.";
            if (!IsCageAnimation(cage)) return McpScript.EntityName(commands, composite, cage) + " is not a CAGEAnimation.";
            if (!composite.GetEntities().Contains(command) || !composite.GetEntities().Contains(cage))
                return "The command and the CAGEAnimation have to be in the same composite (links only join entities of one composite).";
            return null;
        }

        /// <summary>What to tell someone syncing this command: the clip's length, an additive clip, how it finishes.</summary>
        public static List<string> Notes(Commands commands, Composite composite, Entity command, Entity cage, float offset)
        {
            List<string> notes = new List<string>();
            Animation.ClipReference clip = ClipOf(command);
            string name = McpScript.EntityName(commands, composite, command);
            if (clip == null)
                notes.Add(name + "'s AnimationSet / Animation don't name a clip in ANIMATION.PAK yet.");
            else
            {
                if (clip.Additive)
                    notes.Add("'" + clip.Name + "' is ADDITIVE (has_motion false: it holds changes to lay over a pose, not a pose): additive clips belong on CHR_PlaySecondaryAnimation, layered over what the character is doing, not on CMD_PlayAnimation.");
                notes.Add("The clip plays from " + offset.ToString("0.###") + " s into " + McpScript.EntityName(commands, composite, cage) + " (clip time = its time"
                    + (offset != 0 ? " - " + offset.ToString("0.###") : "") + ") to " + (offset + clip.Duration).ToString("0.###") + " s; the command only fires 'finished' once the CAGEAnimation's time passes the clip's end, so the CAGEAnimation has to run that long.");
            }
            notes.Add("The command still has to be started (apply_start, through its TriggerBindCharacter) when the CAGEAnimation starts: the link sets its clock, not when it plays.");
            return notes;
        }

        /// <summary>
        /// Link the command's ExternalTime to the CAGEAnimation (and its ExternalStartTime to a VariableFloat holding <paramref name="offset"/>,
        /// made or reused, when the clip starts later than the CAGEAnimation), as one undo step; or take the sync off (<paramref name="unsync"/>).
        /// Throws <see cref="McpError"/> to refuse.
        /// </summary>
        public static McpScriptEdit.Outcome Apply(Composite composite, Entity command, Entity cage, float offset, bool unsync, bool fromUser, bool dryRun)
        {
            string label = (unsync ? "Unsync " : "Sync ") + "clip";
            return McpScriptEdit.Run(label, composite, edit =>
            {
                Commands commands = edit.Commands;
                string problem = unsync ? (IsPlayAnimation(command) ? null : "That is not a CMD_PlayAnimation.") : Problem(commands, composite, command, cage);
                if (problem != null) throw McpError.Invalid(problem);
                string name = McpScript.EntityName(commands, composite, command);

                //Whatever the clocks were linked to before goes: a pin reads one entity
                foreach (string pin in new[] { ExternalTime, ExternalStartTime })
                    foreach (EntityConnector link in command.GetLinksOut(pin).ToList())
                    {
                        Entity was = composite.GetEntityByID(link.linkedEntityID);
                        if (was != null) edit.RemoveLinks(composite, command, pin, was, McpScript.ParamName(link.linkedParamID));
                    }
                if (unsync) return;

                edit.AddLink(composite, command, ExternalTime, cage, "reference");
                if (Math.Abs(offset) > 1e-6f)
                {
                    string startName = name + "_SyncStart";
                    Entity start = composite.GetEntities().FirstOrDefault(o => o is FunctionEntity f && f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.VariableFloat
                        && string.Equals(McpScript.EntityName(commands, composite, o), startName, StringComparison.OrdinalIgnoreCase));
                    if (start == null) start = edit.AddFunction(composite, FunctionType.VariableFloat, startName);
                    edit.SetParameter(composite, start, "initial_value", new JValue(offset));
                    edit.AddLink(composite, command, ExternalStartTime, start, "reference");
                }
                edit.Select.Add(command);
            }, new McpScriptEdit.RunOptions { Show = !fromUser, DryRun = dryRun, LabelPrefix = fromUser ? "" : "AI: " });
        }
    }
}
