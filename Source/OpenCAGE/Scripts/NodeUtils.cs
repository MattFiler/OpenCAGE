using CATHODE;
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

namespace CommandsEditor
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
                                foreach (CAGEAnimation.EventTrack track in cageAnim.events)
                                {
                                    foreach (CAGEAnimation.EventTrack.Keyframe keyframe in track.keyframes)
                                    {
                                        if (keyframe.track_type != CAGEAnimation.TrackType.STRING)
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
                                foreach (TriggerSequence.MethodEntry method in triggerSeq.methods)
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
                                
                                HashSet<ShortGuid> newTopOptions = new HashSet<ShortGuid>();
                                HashSet<ShortGuid> checkedFunctionTypes = new HashSet<ShortGuid>();
                                HashSet<ShortGuid> checkedEntityGuids = new HashSet<ShortGuid>();
                                foreach (TriggerSequence.SequenceEntry entry in triggerSeq.sequence)
                                {
                                    ShortGuid entryEntityGuid = entry.connectedEntity.GetPointedEntityID();
                                    if (checkedEntityGuids.Contains(entryEntityGuid))
                                        continue;
                                    checkedEntityGuids.Add(entryEntityGuid);

                                    (Composite entryComp, Entity entryEnt) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(entry.connectedEntity, composite));
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
                sourceNode.Recompute();
            }
            if (addedTargetPin)
            {
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
                                    foreach (CAGEAnimation.EventTrack track in cageAnim.events)
                                    {
                                        foreach (CAGEAnimation.EventTrack.Keyframe keyframe in track.keyframes)
                                        {
                                            if (keyframe.track_type != CAGEAnimation.TrackType.STRING)
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

                                        (Composite entryComp, Entity entryEnt) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(entry.connectedEntity, composite));
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
        public static void RemoveUnusedPins(this STNode node)
        {
            //Variable entities only ever have the right pins added
            if (node.Entity.variant == EntityVariant.VARIABLE)
                return;

            STNodeOption[] ins = node.GetInputOptions();
            for (int i = 0; i < ins.Length; i++)
                if (ins[i].ConnectionCount == 0)
                    node.RemoveInputOption(ins[i].ShortGUID);
            STNodeOption[] outs = node.GetOutputOptions();
            for (int i = 0; i < outs.Length; i++)
                if (outs[i].ConnectionCount == 0)
                    node.RemoveOutputOption(outs[i].ShortGUID);
            STNodeOption[] ups = node.GetTopOptions();
            for (int i = 0; i < ups.Length; i++)
                if (ups[i].ConnectionCount == 0)
                    node.RemoveTopOption(ups[i].ShortGUID);
            STNodeOption[] downs = node.GetBottomOptions();
            for (int i = 0; i < downs.Length; i++)
                if (downs[i].ConnectionCount == 0)
                    node.RemoveBottomOption(downs[i].ShortGUID);
        }
    }
}
