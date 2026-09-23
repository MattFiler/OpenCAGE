using CATHODE.Scripting;
using CathodeLib;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Dropping a function type out of the entity palette onto the viewport, which creates an entity of it
    /// where it landed: the palette's counterpart of the composite browser's drop (ViewerCompositeDrop), and
    /// taken the same way. The drag source watches the cursor (see EntityBrowser), the placement is asked of
    /// the viewer - only it has the geometry to raycast - and the entity is created when the answer arrives
    /// as ENTITY_CREATE_REQUEST, through the same path the palette's double-click creates one by.
    ///
    /// Only a function with somewhere to put the dropped position takes a drop: one whose parameter table
    /// gives it a `position`. Anything else - a logic node, a variable pin - was meant for the flowgraph, and
    /// dropping it on the viewport does nothing at all; the drag shows the no-drop cursor over it instead.
    /// </summary>
    public static class ViewerFunctionDrop
    {
        //Whether a function type has a position, by type: read out of the parameter table, which never
        //changes, and asked on every mouse move of a drag
        private static readonly Dictionary<FunctionType, bool> _hasPosition = new Dictionary<FunctionType, bool>();

        /// <summary>Whether what the palette is dragging (a node's Tag) could be dropped on the viewport.</summary>
        public static bool CanDrop(object paletteTag)
        {
            return paletteTag is FunctionType function && HasPosition(function);
        }

        /// <summary>
        /// Whether the function has a `position` parameter (a TRANSFORM) - as CathodeLib's parameter table
        /// defines it, for the type or one it inherits from, which is the table Add Function Entity fills
        /// its defaults from. By the table, never by anything in the type's name.
        /// </summary>
        public static bool HasPosition(FunctionType function)
        {
            lock (_hasPosition)
            {
                if (_hasPosition.TryGetValue(function, out bool known))
                    return known;
            }

            //The table is read through a level's commands, so there is no answer to keep until one is loaded
            CommandsUtils utils = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands?.Utils;
            if (utils == null)
                return false;

            bool hasPosition = false;
            try
            {
                FunctionType? type = function;
                while (type.HasValue && !hasPosition)
                {
                    foreach ((ShortGuid guid, ParameterVariant variant, DataType dataType) in utils.GetAllParameters(type.Value))
                    {
                        if (guid != ShortGuids.position || dataType != DataType.TRANSFORM)
                            continue;

                        hasPosition = true;
                        break;
                    }
                    type = utils.GetInheritedFunction(type.Value);
                }
            }
            catch (KeyNotFoundException)
            {
                //A type the table doesn't describe has no position it can be given
                hasPosition = false;
            }

            lock (_hasPosition)
                _hasPosition[function] = hasPosition;
            return hasPosition;
        }

        /// <summary>
        /// Ask the viewer to place an entity of the function at the dropped screen point. False, and nothing
        /// sent, for a function with no position to place - the drop is meant to do nothing then.
        /// </summary>
        public static bool TryDrop(FunctionType function, Point screenPoint)
        {
            if (!Send.Connected || !HasPosition(function))
                return false;

            if (!ViewerCompositeDrop.TryGetViewportFraction(screenPoint, out float x, out float y))
                return false;

            Send.SendFunctionDropPacket(function, x, y);
            return true;
        }
    }
}
