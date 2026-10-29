using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static CathodeLib.CathodeEnumTable;

namespace OpenCAGE
{
    /// <summary>
    /// The value each top pin has in the inspector, drawn over the pin as the delays are beside the side pins
    /// (STNodeOption.TopText). An input pin shows what the entity holds for it - on an alias or proxy what it overrides,
    /// else what the entity it points at holds - or else the default the inspector gives it. A pin a link comes in to, on
    /// any page of the composite, shows nothing: the link sets the value, so the inspector's is beside the point (the
    /// inspector marks those parameters as fed by the flowgraph by the same rule). Output pins show nothing - the inspector
    /// never shows a value for one - and nor do pins that only take a link (an object, a zone), which have no value to show.
    /// </summary>
    public partial class Flowgraph
    {
        private bool _pinValueRefreshQueued = false;
        private bool _pinValuesStale = false;

        private void SubscribePinValueEvents()
        {
            Singleton.OnEntityParameterModified += OnEntityParameterModifiedGlobally;
            Singleton.OnParameterModified += QueuePinValueRefresh;
            Singleton.OnEntityDefaultsApplied += OnEntityDefaultsAppliedGlobally;
            this.HandleCreated += OnHandleCreatedRefreshPinValues;
            //A page disposed without closing must not be kept alive by the static events
            this.Disposed += (s, e) => UnsubscribePinValueEvents();
        }

        private void UnsubscribePinValueEvents()
        {
            Singleton.OnEntityParameterModified -= OnEntityParameterModifiedGlobally;
            Singleton.OnParameterModified -= QueuePinValueRefresh;
            Singleton.OnEntityDefaultsApplied -= OnEntityDefaultsAppliedGlobally;
            this.HandleCreated -= OnHandleCreatedRefreshPinValues;
        }

        private void OnEntityParameterModifiedGlobally(Entity entity, Parameter parameter, bool removed) => QueuePinValueRefresh();
        private void OnEntityDefaultsAppliedGlobally(Entity entity) => QueuePinValueRefresh();

        /* Edits come one parameter at a time (and an undo step or a multi-edit can be many), and an alias's pins show what
           the entity it points at holds - so the whole page is gone over once the edit is done, not once a parameter. Only
           the nodes whose text changed are repainted. */
        private void QueuePinValueRefresh()
        {
            if (IsDisposed || Disposing)
                return;
            if (!IsHandleCreated)
            {
                _pinValuesStale = true;
                return;
            }
            if (_pinValueRefreshQueued)
                return;
            _pinValueRefreshQueued = true;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    _pinValueRefreshQueued = false;
                    RefreshPinValueTexts();
                }));
            }
            catch (InvalidOperationException)
            {
                _pinValueRefreshQueued = false;
            }
        }

        private void OnHandleCreatedRefreshPinValues(object sender, EventArgs e)
        {
            if (_pinValuesStale)
                QueuePinValueRefresh();
        }

        /// <summary>A link made or broken on this page can start or stop a value showing on any page with the entity.</summary>
        private void QueuePinValueRefreshAllPages()
        {
            foreach (Flowgraph page in PinValuePages())
                page?.QueuePinValueRefresh();
        }

        /// <summary>Redraw the value over every top pin on the page.</summary>
        private void RefreshPinValueTexts()
        {
            _pinValuesStale = false;
            if (IsDisposed || Disposing || _composite == null)
                return;
            Dictionary<ShortGuid, HashSet<ShortGuid>> linked = LinkedInputPins();
            foreach (STNode node in stNodeEditor1.Nodes)
                UpdatePinValueTexts(node, linked);
        }

        /// <summary>Set the value text over each of a node's top pins.</summary>
        private void UpdatePinValueTexts(STNode node)
        {
            //While the page is built or an undo puts it back, its links are still coming: the whole page is done once after
            if (IsPopulating)
            {
                QueuePinValueRefresh();
                return;
            }
            UpdatePinValueTexts(node, null);
        }

        private void UpdatePinValueTexts(STNode node, Dictionary<ShortGuid, HashSet<ShortGuid>> linkedByEntity)
        {
            if (node?.Entity == null || _composite == null || _commands?.Utils == null)
                return;
            STNodeOption[] pins = node.GetTopOptions();
            if (pins == null || pins.Length == 0)
                return;

            //What the inspector shows a row for: a parameter of these kinds (the flowgraph's inspector hides pin delays and outputs)
            HashSet<ShortGuid> shown = null;
            //The entity's pins a link comes in to, on any page
            HashSet<ShortGuid> linked = null;
            //An alias or proxy shows the entity it points at, where it does not override it
            Entity target = null;
            bool targetResolved = false;
            foreach (STNodeOption pin in pins)
            {
                if (pin == null || pin == STNodeOption.Empty)
                    continue;
                if (pin.Style == PinStyle.ArrowUp)
                {
                    pin.TopText = "";
                    continue;
                }
                if (linked == null)
                {
                    if (linkedByEntity == null)
                        linked = LinkedInputPins(node.Entity);
                    else if (!linkedByEntity.TryGetValue(node.Entity.shortGUID, out linked))
                        linked = new HashSet<ShortGuid>();
                }
                if (pin.ConnectionCount > 0 || linked.Contains(pin.ShortGUID))
                {
                    pin.TopText = "";
                    continue;
                }

                string text = "";
                try
                {
                    if (shown == null)
                        shown = ShownParameters(node.Entity);
                    if (shown.Contains(pin.ShortGUID) && !EntityParameterVisibility.IsHiddenFromEditor(node.Entity, pin.ShortGUID))
                    {
                        ParameterData value = node.Entity.GetParameter(pin.ShortGUID)?.content;
                        if (value == null && (node.Entity.variant == EntityVariant.ALIAS || node.Entity.variant == EntityVariant.PROXY))
                        {
                            if (!targetResolved)
                            {
                                target = _commands.Utils.GetResolvedTarget(_commands.Utils.ResolveAliasOrProxy(node.Entity, _composite)).Item2;
                                targetResolved = true;
                            }
                            value = target?.GetParameter(pin.ShortGUID)?.content;
                        }
                        if (value == null)
                            value = _commands.Utils.CreateDefaultParameterData(node.Entity, _composite, pin.ShortGUID);
                        text = PinValueText(value, pin.ShortGUID);
                    }
                }
                catch (Exception ex)
                {
                    //A value that cannot be read is left off its pin, not the page
                    Debug.Log("Flowgraph", "Could not read the value of " + pin.Text + " on " + node.Title + ": " + ex.Message);
                }
                pin.TopText = text;
            }
        }

        //Every page of the composite open in the display (only this one while the page isn't among them)
        private List<Flowgraph> PinValuePages()
        {
            List<Flowgraph> pages = Singleton.Editor?.CompositeDisplay?.Flowgraphs;
            return pages != null && pages.Contains(this) ? pages.Where(o => o != null && !o.IsDisposed).ToList() : new List<Flowgraph>() { this };
        }

        //The pins a link comes in to, by entity, over every page - once for a whole page's refresh
        private Dictionary<ShortGuid, HashSet<ShortGuid>> LinkedInputPins()
        {
            Dictionary<ShortGuid, HashSet<ShortGuid>> linked = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
            foreach (Flowgraph page in PinValuePages())
            {
                foreach (STNode node in page.stNodeEditor1.Nodes)
                {
                    if (node?.Entity == null)
                        continue;
                    List<ShortGuid> ids = node.GetConnectedInputOptionIds();
                    if (ids.Count == 0)
                        continue;
                    if (!linked.TryGetValue(node.Entity.shortGUID, out HashSet<ShortGuid> pins))
                        linked.Add(node.Entity.shortGUID, pins = new HashSet<ShortGuid>());
                    pins.UnionWith(ids);
                }
            }
            return linked;
        }

        //The pins a link comes in to on one entity, over every page
        private HashSet<ShortGuid> LinkedInputPins(Entity entity)
        {
            HashSet<ShortGuid> linked = new HashSet<ShortGuid>();
            foreach (Flowgraph page in PinValuePages())
                page.CollectConnectedInputPins(entity, linked);
            return linked;
        }

        private HashSet<ShortGuid> ShownParameters(Entity entity)
        {
            HashSet<ShortGuid> shown = new HashSet<ShortGuid>();
            foreach ((ShortGuid, ParameterVariant, DataType) parameter in _commands.Utils.GetAllParameters(entity, _composite))
            {
                switch (parameter.Item2)
                {
                    case ParameterVariant.INTERNAL:
                    case ParameterVariant.INPUT_PIN:
                    case ParameterVariant.PARAMETER:
                    case ParameterVariant.STATE_PARAMETER:
                        shown.Add(parameter.Item1);
                        break;
                }
            }
            return shown;
        }

        /* A value as short as it reads: the inspector's text for it, with fewer decimal places */
        private string PinValueText(ParameterData value, ShortGuid name)
        {
            switch (value)
            {
                case cFloat number:
                    return NumberText(number.value);
                case cInteger number:
                    return number.value.ToString();
                case cBool flag:
                    return flag.value ? "True" : "False";
                case cString text: //an enum string too
                    return text.value ?? "";
                case cVector3 vector:
                    return VectorText(vector.value.X, vector.value.Y, vector.value.Z);
                case cTransform transform:
                    return VectorText(transform.position.X, transform.position.Y, transform.position.Z) + " / "
                        + VectorText(transform.rotation.X, transform.rotation.Y, transform.rotation.Z);
                case cEnum enumValue:
                    {
                        EnumDescriptor descriptor = enumValue.enumID == ShortGuid.Invalid ? null : _commands.Utils.GetEnum(enumValue.enumID);
                        return descriptor?.Entries.FirstOrDefault(o => o.Index == enumValue.enumIndex)?.Name ?? "";
                    }
                case cResource resource:
                    {
                        //A composite's material mapping, as the inspector names it
                        if (name.ToString() == "mapping")
                            return Singleton.Editor?.CompositeBrowser?.Content?.Level?.MaterialMappings?.Entries?.FirstOrDefault(o => o.ID == resource.shortGUID)?.Name ?? "";
                        int count = resource.value?.Count ?? 0;
                        return count == 1 ? "1 resource" : count + " resources";
                    }
                case cSpline spline:
                    {
                        int count = spline.splinePoints?.Count ?? 0;
                        return count == 1 ? "1 point" : count + " points";
                    }
            }
            return "";
        }

        //As the inspector writes a vector: with a full stop for the decimal point whatever the language, comma-separated
        private static string VectorText(float x, float y, float z)
        {
            return NumberText(x, CultureInfo.InvariantCulture) + ", " + NumberText(y, CultureInfo.InvariantCulture) + ", " + NumberText(z, CultureInfo.InvariantCulture);
        }

        //Up to three decimal places - but never a small value shown as 0
        private static string NumberText(float value, IFormatProvider format = null)
        {
            if (value != 0f && Math.Abs(value) < 0.0005f)
                return value.ToString("G3", format);
            string text = value.ToString("0.###", format);
            return text == "-0" ? "0" : text;
        }
    }
}
