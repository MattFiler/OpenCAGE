using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CathodeLib.CompositeFlowgraphTable;
using static CathodeLib.CompositePinInfoTable;

namespace OpenCAGE
{
    public static class NodeUtils
    {
        public class PinPositionInfo
        {
            public ShortGuid ParameterGUID { get; set; }
            public PinLocation Location { get; set; }
            public PinStyle Style { get; set; }
            public ParameterVariant Variant { get; set; }
            public int Index { get; set; } // Position within its collection
        }

        /* Collects the trigger method pins a TriggerSequence-like entity should show. Proxies are
           stand-ins for the same entity, so this is the union of the methods on the base
           TriggerSequence and the methods carried by every proxy that resolves to it - whichever of
           those entities is passed in. Returns an empty list for anything not TriggerSequence-related. */
        public static List<TriggerSequence.MethodEntry> CollectTriggerSequenceMethods(Entity entity, Composite composite, Commands commands)
        {
            List<TriggerSequence.MethodEntry> methods = new List<TriggerSequence.MethodEntry>();
            HashSet<ShortGuid> seenMethods = new HashSet<ShortGuid>();
            void Include(List<TriggerSequence.MethodEntry> source)
            {
                if (source == null)
                    return;
                foreach (TriggerSequence.MethodEntry method in source)
                    if (seenMethods.Add(method.method))
                        methods.Add(method);
            }

            if (entity == null || commands == null)
                return methods;

            //Proxies carry their own trigger method data
            if (entity is ProxyEntity sourceProxy)
                Include(sourceProxy.methods);

            //Find the base TriggerSequence this entity represents (itself, or a proxy/alias target)
            TriggerSequence baseTrigger = entity as TriggerSequence;
            if (baseTrigger == null && (entity.variant == EntityVariant.PROXY || entity.variant == EntityVariant.ALIAS))
            {
                (Composite targetComp, Entity targetEnt) = commands.Utils.GetResolvedTarget(
                    commands.Utils.ResolveAliasOrProxy(entity, composite));
                baseTrigger = targetEnt as TriggerSequence;
            }
            if (baseTrigger == null)
                return methods;

            Include(baseTrigger.methods);

            //Union in the methods carried by every proxy that resolves to the base TriggerSequence
            foreach (Composite comp in commands.Entries)
            {
                foreach (ProxyEntity proxy in comp.proxies)
                {
                    if (proxy == entity || proxy.methods.Count == 0)
                        continue;
                    if (proxy.proxy.GetPointedEntityID() != baseTrigger.shortGUID)
                        continue;
                    if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy)).Item2 != baseTrigger)
                        continue;
                    Include(proxy.methods);
                }
            }

            return methods;
        }

        /* Gets the parameter GUIDs that act as dynamically generated pins for an entity: TriggerSequence
           trigger methods (name, name_relay, name_finished) and CAGEAnimation event keyframes. Delay
           values for these pins are stored as parameters on the entity, so parameter UIs should hide
           them when the composite is edited via flowgraphs. Resolves proxies/aliases to their target. */
        public static HashSet<ShortGuid> GetDynamicPinParameters(Entity entity, Composite composite, Commands commands)
        {
            HashSet<ShortGuid> pins = new HashSet<ShortGuid>();
            if (entity == null || commands == null)
                return pins;

            //Trigger methods: unioned across the base TriggerSequence and all proxies standing in for it
            foreach (TriggerSequence.MethodEntry method in CollectTriggerSequenceMethods(entity, composite, commands))
            {
                pins.Add(method.method);
                pins.Add(method.relay);
                pins.Add(method.finished);
            }

            Entity resolved = entity;
            if (entity.variant == EntityVariant.PROXY || entity.variant == EntityVariant.ALIAS)
            {
                (Composite targetComp, Entity targetEnt) = commands.Utils.GetResolvedTarget(
                    commands.Utils.ResolveAliasOrProxy(entity, composite));
                if (targetEnt != null)
                    resolved = targetEnt;
            }

            if (resolved is CAGEAnimation cageAnimation)
            {
                foreach (CAGEAnimation.EventTrack track in cageAnimation.eventTracks)
                {
                    foreach (CAGEAnimation.EventTrack.Keyframe keyframe in track.keyframes)
                    {
                        if (keyframe.track_type != ANIM_TRACK_TYPE.T_STRING)
                            continue;
                        pins.Add(keyframe.forward);
                        pins.Add(keyframe.reverse);
                    }
                }
            }
            return pins;
        }

        /* Gets all possible pin positions for a node without creating the actual pins. */
        public static List<PinPositionInfo> GetAllPinPositions(this STNode node, Composite composite, Commands commands)
        {
            var pinPositions = new List<PinPositionInfo>();
            var addedGuids = new HashSet<ShortGuid>(); 
            
            switch (node.Entity.variant)
            {
                case EntityVariant.VARIABLE:
                    VariableEntity varEnt = (VariableEntity)node.Entity;
                    if (addedGuids.Add(varEnt.name))
                    {
                        PinInfo info = commands.Utils.GetPinInfo(composite, varEnt);
                        switch (info.PinTypeGUID.AsCompositePinType)
                        {
                            case CompositePinType.CompositeInputAnimationInfoVariablePin:
                            case CompositePinType.CompositeInputBoolVariablePin:
                            case CompositePinType.CompositeInputDirectionVariablePin:
                            case CompositePinType.CompositeInputFloatVariablePin:
                            case CompositePinType.CompositeInputIntVariablePin:
                            case CompositePinType.CompositeInputObjectVariablePin:
                            case CompositePinType.CompositeInputPositionVariablePin:
                            case CompositePinType.CompositeInputStringVariablePin:
                            case CompositePinType.CompositeInputVariablePin:
                            case CompositePinType.CompositeInputZoneLinkPtrVariablePin:
                            case CompositePinType.CompositeInputZonePtrVariablePin:
                            case CompositePinType.CompositeInputEnumVariablePin:
                            case CompositePinType.CompositeInputEnumStringVariablePin:
                            case CompositePinType.CompositeOutputAnimationInfoVariablePin:
                            case CompositePinType.CompositeOutputBoolVariablePin:
                            case CompositePinType.CompositeOutputDirectionVariablePin:
                            case CompositePinType.CompositeOutputFloatVariablePin:
                            case CompositePinType.CompositeOutputIntVariablePin:
                            case CompositePinType.CompositeOutputObjectVariablePin:
                            case CompositePinType.CompositeOutputPositionVariablePin:
                            case CompositePinType.CompositeOutputStringVariablePin:
                            case CompositePinType.CompositeOutputVariablePin:
                            case CompositePinType.CompositeOutputZoneLinkPtrVariablePin:
                            case CompositePinType.CompositeOutputZonePtrVariablePin:
                            case CompositePinType.CompositeOutputEnumVariablePin:
                            case CompositePinType.CompositeOutputEnumStringVariablePin:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = varEnt.name, 
                                    Location = PinLocation.Bottom, 
                                    Style = PinStyle.ArrowDown,
                                    Variant = ParameterVariant.REFERENCE_PIN,
                                    Index = 0
                                });
                                break;
                            case CompositePinType.CompositeMethodPin:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = varEnt.name, 
                                    Location = PinLocation.Right, 
                                    Style = PinStyle.ArrowRight,
                                    Variant = ParameterVariant.METHOD_PIN,
                                    Index = 0
                                });
                                break;
                            case CompositePinType.CompositeTargetPin:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = varEnt.name, 
                                    Location = PinLocation.Left, 
                                    Style = PinStyle.ArrowRight,
                                    Variant = ParameterVariant.TARGET_PIN,
                                    Index = 0
                                });
                                break;
                            case CompositePinType.CompositeReferencePin:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = varEnt.name, 
                                    Location = PinLocation.Top, 
                                    Style = PinStyle.ArrowDown,
                                    Variant = ParameterVariant.REFERENCE_PIN,
                                    Index = 0
                                });
                                break;
                            }
                        }
                    break;
                default:
                    List<(ShortGuid, ParameterVariant, DataType)> allParameters = commands.Utils.GetAllParameters(node.Entity, composite);
                    int topIndex = 0, bottomIndex = 0, leftIndex = 0, rightIndex = 0;

                    //Trigger method pins: method in on the left, relay/finished out on the right
                    void AddTriggerMethodPins(List<TriggerSequence.MethodEntry> triggerMethods)
                    {
                        foreach (TriggerSequence.MethodEntry method in triggerMethods)
                        {
                            if (addedGuids.Add(method.method))
                            {
                                pinPositions.Add(new PinPositionInfo
                                {
                                    ParameterGUID = method.method,
                                    Location = PinLocation.Left,
                                    Style = PinStyle.ArrowRight,
                                    Variant = ParameterVariant.METHOD_PIN,
                                    Index = leftIndex++
                                });
                            }
                            if (addedGuids.Add(method.relay))
                            {
                                pinPositions.Add(new PinPositionInfo
                                {
                                    ParameterGUID = method.relay,
                                    Location = PinLocation.Right,
                                    Style = PinStyle.ArrowRight,
                                    Variant = ParameterVariant.TARGET_PIN,
                                    Index = rightIndex++
                                });
                            }
                            if (addedGuids.Add(method.finished))
                            {
                                pinPositions.Add(new PinPositionInfo
                                {
                                    ParameterGUID = method.finished,
                                    Location = PinLocation.Right,
                                    Style = PinStyle.ArrowRight,
                                    Variant = ParameterVariant.TARGET_PIN,
                                    Index = rightIndex++
                                });
                            }
                        }
                    }

                    foreach ((ShortGuid, ParameterVariant, DataType) parameter in allParameters)
                    {
                        if (!addedGuids.Add(parameter.Item1))
                            continue;

                        switch (parameter.Item2)
                        {
                            case ParameterVariant.INPUT_PIN:
                            case ParameterVariant.PARAMETER:
                            case ParameterVariant.STATE_PARAMETER:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = parameter.Item1, 
                                    Location = PinLocation.Top, 
                                    Style = PinStyle.ArrowDown,
                                    Variant = parameter.Item2,
                                    Index = topIndex++
                                });
                                break;
                            case ParameterVariant.METHOD_PIN:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = parameter.Item1, 
                                    Location = PinLocation.Left, 
                                    Style = PinStyle.ArrowRight,
                                    Variant = parameter.Item2,
                                    Index = leftIndex++
                                });
                                
                                ShortGuid relay = commands.Utils.GetRelay(parameter.Item1);
                                if (relay != ShortGuid.Invalid && addedGuids.Add(relay))
                                {
                                    pinPositions.Add(new PinPositionInfo 
                                    { 
                                        ParameterGUID = relay, 
                                        Location = PinLocation.Right, 
                                        Style = PinStyle.ArrowRight,
                                        Variant = ParameterVariant.TARGET_PIN,
                                        Index = rightIndex++
                                    });
                                }
                                break;
                            case ParameterVariant.OUTPUT_PIN:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = parameter.Item1, 
                                    Location = PinLocation.Top, 
                                    Style = PinStyle.ArrowUp,
                                    Variant = parameter.Item2,
                                    Index = topIndex++
                                });
                                break;
                            case ParameterVariant.TARGET_PIN:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = parameter.Item1, 
                                    Location = PinLocation.Right, 
                                    Style = PinStyle.ArrowRight,
                                    Variant = parameter.Item2,
                                    Index = rightIndex++
                                });
                                break;
                            case ParameterVariant.REFERENCE_PIN:
                                pinPositions.Add(new PinPositionInfo 
                                { 
                                    ParameterGUID = parameter.Item1, 
                                    Location = PinLocation.Bottom, 
                                    Style = PinStyle.ArrowDown,
                                    Variant = parameter.Item2,
                                    Index = bottomIndex++
                                });
                                break;
                        }
                    }

                    if (node.Entity.variant == EntityVariant.FUNCTION)
                    {
                        FunctionEntity func = (FunctionEntity)node.Entity;
                        switch (func.function.AsFunctionType)
                        {
                            case FunctionType.CAGEAnimation:
                                CAGEAnimation cageAnim = (CAGEAnimation)func;
                                foreach (CAGEAnimation.EventTrack track in cageAnim.eventTracks)
                                {
                                    foreach (CAGEAnimation.EventTrack.Keyframe keyframe in track.keyframes)
                                    {
                                        if (keyframe.track_type != ANIM_TRACK_TYPE.T_STRING)
                                            continue;

                                        if (addedGuids.Add(keyframe.forward))
                                        {
                                            pinPositions.Add(new PinPositionInfo 
                                            { 
                                                ParameterGUID = keyframe.forward, 
                                                Location = PinLocation.Right, 
                                                Style = PinStyle.ArrowRight,
                                                Variant = ParameterVariant.TARGET_PIN,
                                                Index = rightIndex++
                                            });
                                        }
                                        if (addedGuids.Add(keyframe.reverse))
                                        {
                                            pinPositions.Add(new PinPositionInfo 
                                            { 
                                                ParameterGUID = keyframe.reverse, 
                                                Location = PinLocation.Right, 
                                                Style = PinStyle.ArrowRight,
                                                Variant = ParameterVariant.TARGET_PIN,
                                                Index = rightIndex++
                                            });
                                        }
                                    }
                                }
                                break;
                            case FunctionType.TriggerSequence:
                                TriggerSequence triggerSeq = (TriggerSequence)func;

                                //Include methods carried by proxies standing in for this TriggerSequence too
                                AddTriggerMethodPins(CollectTriggerSequenceMethods(triggerSeq, composite, commands));

                                HashSet<ShortGuid> newTopOptions = new HashSet<ShortGuid>();
                                HashSet<ShortGuid> checkedFunctionTypes = new HashSet<ShortGuid>();
                                HashSet<ShortGuid> checkedEntityGuids = new HashSet<ShortGuid>();
                                foreach (TriggerSequence.SequenceEntry entry in triggerSeq.sequence)
                                {
                                    ShortGuid entryEntityGuid = entry.connectedEntity.GetPointedEntityID();
                                    if (checkedEntityGuids.Contains(entryEntityGuid))
                                        continue;
                                    checkedEntityGuids.Add(entryEntityGuid);

                                    (Composite entryComp, Entity entryEnt) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(entry.connectedEntity, composite));
                                    if (entryEnt == null) continue;

                                    if (entryEnt.variant == EntityVariant.FUNCTION)
                                    {
                                        ShortGuid entryFunction = ((FunctionEntity)entryEnt).function;
                                        if (checkedFunctionTypes.Contains(entryFunction))
                                            continue;
                                        checkedFunctionTypes.Add(entryFunction);
                                    }

                                    List<(ShortGuid, ParameterVariant, DataType)> allParametersEntry = commands.Utils.GetAllParameters(entryEnt, entryComp);
                                    foreach ((ShortGuid, ParameterVariant, DataType) parameterEntry in allParametersEntry)
                                    {
                                        if (!newTopOptions.Add(parameterEntry.Item1))
                                            continue;

                                        switch (parameterEntry.Item2)
                                        {
                                            case ParameterVariant.INPUT_PIN:
                                            case ParameterVariant.PARAMETER:
                                            case ParameterVariant.STATE_PARAMETER:
                                                pinPositions.Add(new PinPositionInfo 
                                                { 
                                                    ParameterGUID = parameterEntry.Item1, 
                                                    Location = PinLocation.Top, 
                                                    Style = PinStyle.ArrowDown,
                                                    Variant = parameterEntry.Item2,
                                                    Index = topIndex++
                                                });
                                                break;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    else if (node.Entity.variant == EntityVariant.PROXY || node.Entity.variant == EntityVariant.ALIAS)
                    {
                        //Proxies/aliases are stand-ins for their target entity: a TriggerSequence target
                        //means showing the unioned trigger method pins here too
                        AddTriggerMethodPins(CollectTriggerSequenceMethods(node.Entity, composite, commands));
                    }
                    break;
            }

            return pinPositions;
        }

        /* Adds a pin at the specified position, ensuring it's placed correctly in the collection. */
        public static STNodeOption AddPinAtPosition(this STNode node, PinPositionInfo pinInfo)
        {
            STNodeOption existingPin = node.GetOption(pinInfo.ParameterGUID);
            if (existingPin != null)
                return existingPin;

            STNodeOption newPin = null;
            switch (pinInfo.Location)
            {
                case PinLocation.Left:
                    newPin = node.AddInputOption(pinInfo.ParameterGUID);
                    break;
                case PinLocation.Right:
                    newPin = node.AddOutputOption(pinInfo.ParameterGUID);
                    break;
                case PinLocation.Top:
                    newPin = node.AddTopOption(pinInfo.ParameterGUID, pinInfo.Style);
                    break;
                case PinLocation.Bottom:
                    newPin = node.AddBottomOption(pinInfo.ParameterGUID);
                    break;
            }

            return newPin;
        }

        /* Arrange the left/right pins into rows so a method pin and its relay always share a line.
           A method with no relay pin present keeps a blank right side, a relay with no method pin a
           blank left side (STNodeOption.Empty holds the row open), and a trigger method's "finished"
           pin follows directly under its relay. Idempotent, and safe to call after any pin change. */
        public static void AlignRelayRows(this STNode node, Composite composite, Commands commands)
        {
            if (node.Entity == null || node.Entity.variant == EntityVariant.VARIABLE)
                return;

            List<STNodeOption> lefts = new List<STNodeOption>();
            foreach (STNodeOption op in node.GetInputOptions())
                if (op != STNodeOption.Empty)
                    lefts.Add(op);
            List<STNodeOption> rights = new List<STNodeOption>();
            foreach (STNodeOption op in node.GetOutputOptions())
                if (op != STNodeOption.Empty)
                    rights.Add(op);

            if (lefts.Count == 0 && rights.Count == 0)
                return;

            //The relay relationships, from the same two sources the pins were created from: the
            //vanilla method->relay table, and this entity's trigger sequence methods (which also say
            //which relay each "finished" pin belongs after)
            Dictionary<ShortGuid, ShortGuid> relayOfMethod = new Dictionary<ShortGuid, ShortGuid>();
            Dictionary<ShortGuid, ShortGuid> trailsRelay = new Dictionary<ShortGuid, ShortGuid>();
            foreach (TriggerSequence.MethodEntry method in CollectTriggerSequenceMethods(node.Entity, composite, commands))
            {
                relayOfMethod[method.method] = method.relay;
                trailsRelay[method.finished] = method.relay;
            }
            foreach (STNodeOption left in lefts)
            {
                if (relayOfMethod.ContainsKey(left.ShortGUID))
                    continue;
                ShortGuid relay = commands.Utils.GetRelay(left.ShortGUID);
                if (relay != ShortGuid.Invalid)
                    relayOfMethod[left.ShortGUID] = relay;
            }

            //Build the rows: every input in order, paired with its relay where that pin exists...
            List<STNodeOption[]> rows = new List<STNodeOption[]>();
            HashSet<STNodeOption> usedRights = new HashSet<STNodeOption>();
            foreach (STNodeOption left in lefts)
            {
                STNodeOption paired = null;
                ShortGuid relayGuid;
                if (relayOfMethod.TryGetValue(left.ShortGUID, out relayGuid))
                    paired = rights.FirstOrDefault(r => r.ShortGUID == relayGuid && !usedRights.Contains(r));

                rows.Add(new STNodeOption[] { left, paired });
                if (paired != null)
                    usedRights.Add(paired);
            }

            //...then the remaining outputs, each keeping its place except a "finished" pin, which
            //slots in under the relay it belongs to
            foreach (STNodeOption right in rights)
            {
                if (usedRights.Contains(right))
                    continue;

                int at = rows.Count;
                ShortGuid relayGuid;
                if (trailsRelay.TryGetValue(right.ShortGUID, out relayGuid))
                {
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (rows[i][1] == null || rows[i][1].ShortGUID != relayGuid)
                            continue;

                        at = i + 1;
                        while (at < rows.Count && rows[at][0] == null && trailsRelay.ContainsKey(rows[at][1].ShortGUID))
                            at++;
                        break;
                    }
                }
                rows.Insert(at, new STNodeOption[] { null, right });
            }

            //Turn the rows back into the two option lists, not padding past either side's last pin
            int lastLeft = -1, lastRight = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i][0] != null) lastLeft = i;
                if (rows[i][1] != null) lastRight = i;
            }

            List<STNodeOption> newLefts = new List<STNodeOption>();
            List<STNodeOption> newRights = new List<STNodeOption>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i <= lastLeft) newLefts.Add(rows[i][0] ?? STNodeOption.Empty);
                if (i <= lastRight) newRights.Add(rows[i][1] ?? STNodeOption.Empty);
            }

            //Only touch the node when something actually moves
            STNodeOption[] currentLefts = node.GetInputOptions();
            STNodeOption[] currentRights = node.GetOutputOptions();
            if (newLefts.SequenceEqual(currentLefts) && newRights.SequenceEqual(currentRights))
                return;

            node.ArrangePinRows(newLefts, newRights);
        }

        /* Add only the pins needed for connections. */
        public static void AddPinsForConnections(this STNode node, Composite composite, Commands commands, List<FlowgraphMeta.NodeMeta.ConnectionMeta> connectionsOut, List<FlowgraphMeta.NodeMeta.UnlinkedPinMeta> unlinkedPins)
        {
            node.AutoSize = false;
            
            try
            {
                var allPinPositions = node.GetAllPinPositions(composite, commands);
                var pinLookup = new Dictionary<ShortGuid, PinPositionInfo>();
                foreach (var pinInfo in allPinPositions)
                {
                    if (!pinLookup.ContainsKey(pinInfo.ParameterGUID))
                    {
                        pinLookup[pinInfo.ParameterGUID] = pinInfo;
                    }
                }
                
                var pinsToAdd = new List<PinPositionInfo>();
                foreach (var connection in connectionsOut)
                {
                    if (pinLookup.TryGetValue(connection.ParameterGUID, out var outputPinInfo))
                    {
                        pinsToAdd.Add(outputPinInfo);
                    }
                    //Input pins will be handled when processing the connected node, to prevent duplicate pin addition
                }
                
                //Add user-added unlinked pins
                foreach (var unlinkedPin in unlinkedPins)
                {
                    var pinInfo = new PinPositionInfo
                    {
                        ParameterGUID = unlinkedPin.ParameterGUID,
                        Location = (PinLocation)unlinkedPin.PinLocation,
                        Style = (PinStyle)unlinkedPin.PinStyle,
                        Index = 0 // Will be positioned correctly by the node editor
                    };
                    pinsToAdd.Add(pinInfo);
                }
                
                foreach (var pinInfo in pinsToAdd)
                {
                    node.AddPinAtPosition(pinInfo);
                }

                node.AlignRelayRows(composite, commands);
            }
            finally
            {
                node.AutoSize = true;
                node.Recompute();
                if (node.Owner != null)
                {
                    node.SetOptionsLocation();
                    node.BuildSize(true, true, false);
                }
            }
        }

        /* Add pins for both nodes in a connection. */
        public static void AddPinsForConnection(this STNode sourceNode, STNode targetNode, ShortGuid sourcePinGUID, ShortGuid targetPinGUID, Composite composite, Commands commands)
        {
            var sourcePinPositions = sourceNode.GetAllPinPositions(composite, commands);
            var targetPinPositions = targetNode.GetAllPinPositions(composite, commands);
            
            var sourcePinInfo = sourcePinPositions.FirstOrDefault(p => p.ParameterGUID == sourcePinGUID);
            var targetPinInfo = targetPinPositions.FirstOrDefault(p => p.ParameterGUID == targetPinGUID);
            
            bool addedSourcePin = false;
            bool addedTargetPin = false;
            
            if (sourcePinInfo != null && sourceNode.GetOption(sourcePinGUID) == null)
            {
                sourceNode.AddPinAtPosition(sourcePinInfo);
                addedSourcePin = true;
            }
            if (targetPinInfo != null && targetNode.GetOption(targetPinGUID) == null)
            {
                targetNode.AddPinAtPosition(targetPinInfo);
                addedTargetPin = true;
            }
            
            if (addedSourcePin)
            {
                sourceNode.AlignRelayRows(composite, commands);
                sourceNode.Recompute();
            }
            if (addedTargetPin)
            {
                targetNode.AlignRelayRows(composite, commands);
                targetNode.Recompute();
            }
        }

        /* Add all pins for a node (slow) */
        public static void AddAllPins(this STNode node, Composite composite, Commands commands)
        {
            switch (node.Entity.variant)
            {
                case EntityVariant.VARIABLE:
                    VariableEntity varEnt = (VariableEntity)node.Entity;
                    PinInfo info = commands.Utils.GetPinInfo(composite, varEnt);
                    switch (info.PinTypeGUID.AsCompositePinType)
                    {
                        case CompositePinType.CompositeInputAnimationInfoVariablePin:
                        case CompositePinType.CompositeInputBoolVariablePin:
                        case CompositePinType.CompositeInputDirectionVariablePin:
                        case CompositePinType.CompositeInputFloatVariablePin:
                        case CompositePinType.CompositeInputIntVariablePin:
                        case CompositePinType.CompositeInputObjectVariablePin:
                        case CompositePinType.CompositeInputPositionVariablePin:
                        case CompositePinType.CompositeInputStringVariablePin:
                        case CompositePinType.CompositeInputVariablePin:
                        case CompositePinType.CompositeInputZoneLinkPtrVariablePin:
                        case CompositePinType.CompositeInputZonePtrVariablePin:
                        case CompositePinType.CompositeInputEnumVariablePin:
                        case CompositePinType.CompositeInputEnumStringVariablePin:
                        case CompositePinType.CompositeOutputAnimationInfoVariablePin:
                        case CompositePinType.CompositeOutputBoolVariablePin:
                        case CompositePinType.CompositeOutputDirectionVariablePin:
                        case CompositePinType.CompositeOutputFloatVariablePin:
                        case CompositePinType.CompositeOutputIntVariablePin:
                        case CompositePinType.CompositeOutputObjectVariablePin:
                        case CompositePinType.CompositeOutputPositionVariablePin:
                        case CompositePinType.CompositeOutputStringVariablePin:
                        case CompositePinType.CompositeOutputVariablePin:
                        case CompositePinType.CompositeOutputZoneLinkPtrVariablePin:
                        case CompositePinType.CompositeOutputZonePtrVariablePin:
                        case CompositePinType.CompositeOutputEnumVariablePin:
                        case CompositePinType.CompositeOutputEnumStringVariablePin:
                            node.AddBottomOption(varEnt.name);
                            break;
                        case CompositePinType.CompositeMethodPin:
                            node.AddOutputOption(varEnt.name);
                            break;
                        case CompositePinType.CompositeTargetPin:
                            node.AddInputOption(varEnt.name);
                            break;
                        case CompositePinType.CompositeReferencePin:
                            node.AddTopOption(varEnt.name, PinStyle.ArrowDown);
                            break;
                    }
                    break;
                default:
                    List<(ShortGuid, ParameterVariant, DataType)> allParameters = commands.Utils.GetAllParameters(node.Entity, composite);
                    foreach ((ShortGuid, ParameterVariant, DataType) parameter in allParameters)
                    {
                        switch (parameter.Item2)
                        {
                            case ParameterVariant.INPUT_PIN:
                            case ParameterVariant.PARAMETER:
                            case ParameterVariant.STATE_PARAMETER:
                                node.AddTopOption(parameter.Item1, PinStyle.ArrowDown);
                                break;
                            case ParameterVariant.METHOD_PIN:
                                node.AddInputOption(parameter.Item1);
                                ShortGuid relay = commands.Utils.GetRelay(parameter.Item1);
                                if (relay != ShortGuid.Invalid)
                                    node.AddOutputOption(relay);
                                break;
                            case ParameterVariant.OUTPUT_PIN:
                                node.AddTopOption(parameter.Item1, PinStyle.ArrowUp);
                                break;
                            case ParameterVariant.TARGET_PIN:
                                node.AddOutputOption(parameter.Item1);
                                break;
                            case ParameterVariant.REFERENCE_PIN:
                                node.AddBottomOption(parameter.Item1);
                                break;
                        }

                        if (node.Entity.variant == EntityVariant.FUNCTION)
                        {
                            FunctionEntity func = (FunctionEntity)node.Entity;
                            switch (func.function.AsFunctionType)
                            {
                                case FunctionType.CAGEAnimation:
                                    CAGEAnimation cageAnim = (CAGEAnimation)func;
                                    foreach (CAGEAnimation.EventTrack track in cageAnim.eventTracks)
                                    {
                                        foreach (CAGEAnimation.EventTrack.Keyframe keyframe in track.keyframes)
                                        {
                                            if (keyframe.track_type != ANIM_TRACK_TYPE.T_STRING)
                                                continue;

                                            node.AddOutputOption(keyframe.forward);
                                            node.AddOutputOption(keyframe.reverse);
                                        }
                                    }
                                    break;
                                case FunctionType.TriggerSequence:
                                    TriggerSequence triggerSeq = (TriggerSequence)func;
                                    foreach (TriggerSequence.MethodEntry method in triggerSeq.methods)
                                    {
                                        node.AddInputOption(method.method);
                                        node.AddOutputOption(method.relay);
                                        node.AddOutputOption(method.finished);
                                    }
                                    HashSet<ShortGuid> newTopOptions = new HashSet<ShortGuid>();
                                    HashSet<ShortGuid> checkedFunctionTypes = new HashSet<ShortGuid>();
                                    HashSet<ShortGuid> checkedEntityGuids = new HashSet<ShortGuid>();
                                    foreach (TriggerSequence.SequenceEntry entry in triggerSeq.sequence)
                                    {
                                        ShortGuid entryEntityGuid = entry.connectedEntity.GetPointedEntityID();
                                        if (checkedEntityGuids.Contains(entryEntityGuid))
                                            continue;
                                        checkedEntityGuids.Add(entryEntityGuid);

                                        (Composite entryComp, Entity entryEnt) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(entry.connectedEntity, composite));
                                        if (entryEnt == null) continue;

                                        if (entryEnt.variant == EntityVariant.FUNCTION)
                                        {
                                            ShortGuid entryFunction = ((FunctionEntity)entryEnt).function;
                                            if (checkedFunctionTypes.Contains(entryFunction))
                                                continue;
                                            checkedFunctionTypes.Add(entryFunction);
                                        }

                                        List<(ShortGuid, ParameterVariant, DataType)> allParametersEntry = commands.Utils.GetAllParameters(entryEnt, entryComp);
                                        foreach ((ShortGuid, ParameterVariant, DataType) parameterEntry in allParametersEntry)
                                        {
                                            switch (parameterEntry.Item2)
                                            {
                                                //TODO: need to verify it is actually these three, and not just parameters
                                                case ParameterVariant.INPUT_PIN:
                                                case ParameterVariant.PARAMETER:
                                                case ParameterVariant.STATE_PARAMETER:
                                                    newTopOptions.Add(parameterEntry.Item1);
                                                    break;
                                            }
                                        }
                                    }
                                    foreach (ShortGuid topOption in newTopOptions)
                                        node.AddTopOption(topOption, PinStyle.ArrowDown);
                                    break;
                            }
                        }
                    }
                    node.AlignRelayRows(composite, commands);
                    break;
            }
        }

        /* Ensures a node is properly sized and all pins are positioned correctly. */
        public static void EnsureProperNodeSizing(this STNode node)
        {
            if (node.Owner == null) return;
            
            node.Recompute();
            if (node.AutoSize)
            {
                node.SetOptionsLocation();
                node.BuildSize(true, true, false);
            }
        }

        /* Removes all pins with no connections */
        public static void RemoveUnusedPins(this STNode node, Composite composite, Commands commands)
        {
            //Variable entities only ever have the right pins added
            if (node.Entity.variant == EntityVariant.VARIABLE)
                return;

            STNodeOption[] ins = node.GetInputOptions();
            for (int i = 0; i < ins.Length; i++)
                if (ins[i] != STNodeOption.Empty && ins[i].ConnectionCount == 0)
                    node.RemoveInputOption(ins[i].ShortGUID);
            STNodeOption[] outs = node.GetOutputOptions();
            for (int i = 0; i < outs.Length; i++)
                if (outs[i] != STNodeOption.Empty && outs[i].ConnectionCount == 0)
                    node.RemoveOutputOption(outs[i].ShortGUID);
            STNodeOption[] ups = node.GetTopOptions();
            for (int i = 0; i < ups.Length; i++)
                if (ups[i].ConnectionCount == 0)
                    node.RemoveTopOption(ups[i].ShortGUID);
            STNodeOption[] downs = node.GetBottomOptions();
            for (int i = 0; i < downs.Length; i++)
                if (downs[i].ConnectionCount == 0)
                    node.RemoveBottomOption(downs[i].ShortGUID);

            //The removals may have broken pairs apart or left stale blank rows
            node.AlignRelayRows(composite, commands);
        }

        /* Applies a manual pin selection to a node, disconnecting links on removed pins. */
        public static void ApplyManagedPinSelection(STNode node, Composite composite, Commands commands, HashSet<ShortGuid> selectedPinGuids)
        {
            if (node == null || composite == null || commands == null || selectedPinGuids == null)
                return;

            bool wasAutoSize = node.AutoSize;
            node.AutoSize = false;
            try
            {
                foreach (STNodeOption existing in node.GetAllOptions())
                {
                    if (existing == STNodeOption.Empty)
                        continue;
                    if (selectedPinGuids.Contains(existing.ShortGUID))
                        continue;

                    existing.DisconnectAll();
                    RemovePinOption(node, existing);
                }

                List<PinPositionInfo> allPinPositions = node.GetAllPinPositions(composite, commands);
                foreach (PinPositionInfo pinInfo in allPinPositions)
                {
                    if (!selectedPinGuids.Contains(pinInfo.ParameterGUID))
                        continue;

                    node.AddPinAtPosition(pinInfo);
                }

                node.AlignRelayRows(composite, commands);
            }
            finally
            {
                node.AutoSize = wasAutoSize;
                node.EnsureProperNodeSizing();
            }
        }

        private static void RemovePinOption(STNode node, STNodeOption opt)
        {
            switch (opt.Location)
            {
                case PinLocation.Left:
                    node.RemoveInputOption(opt.ShortGUID);
                    break;
                case PinLocation.Right:
                    node.RemoveOutputOption(opt.ShortGUID);
                    break;
                case PinLocation.Top:
                    node.RemoveTopOption(opt.ShortGUID);
                    break;
                case PinLocation.Bottom:
                    node.RemoveBottomOption(opt.ShortGUID);
                    break;
            }
        }
    }
}
