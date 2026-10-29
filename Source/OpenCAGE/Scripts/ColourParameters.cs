using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Undo;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Treat As Colour: a composite's vector parameters (its VariableEntities of type VECTOR) can be edited with a colour picker
    /// instead of X/Y/Z - on the parameter entity itself, and on every instance of the composite (and the aliases and proxies
    /// pointing at one). The choice is the level's ColourParameterTable (CommandsUtils.IsColourParameter), by composite and
    /// parameter entity, which is saved with the script and goes with the composite when it is ported. A vector whose name is a
    /// known colour (EntityParameterProxy.IsColourName) is shown as one anyway, and offers no switch.
    /// </summary>
    public static class ColourParameters
    {
        /// <summary>
        /// The composite parameter an inspector row stands for: on an instance of a composite, that composite's parameter entity
        /// of the row's name; on a parameter entity, itself (its one row is its value); on an alias or proxy, as for what it
        /// points at. Nulls for any other row.
        /// </summary>
        public static (Composite composite, VariableEntity variable) Resolve(Commands commands, Entity entity, Composite holder, ShortGuid parameter)
        {
            if (commands?.Utils == null || entity == null || holder == null)
                return (null, null);
            if (entity is AliasEntity alias)
                (holder, entity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, holder));
            else if (entity is ProxyEntity proxy)
                (holder, entity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy));

            switch (entity)
            {
                case VariableEntity variable:
                    return holder != null && variable.name == parameter ? (holder, variable) : (null, null);
                case FunctionEntity function when !function.function.IsFunctionType:
                    Composite instanced = commands.GetComposite(function.function);
                    VariableEntity found = instanced?.variables.FirstOrDefault(o => o.name == parameter);
                    return found == null ? (null, null) : (instanced, found);
            }
            return (null, null);
        }

        /// <summary>Whether a vector row is shown as a colour because its composite parameter was chosen to be.</summary>
        public static bool IsColour(Commands commands, Entity entity, Composite holder, ShortGuid parameter)
        {
            (Composite composite, VariableEntity variable) = Resolve(commands, entity, holder, parameter);
            return variable != null && variable.type == DataType.VECTOR && commands.Utils.IsColourParameter(composite, variable);
        }

        /// <summary>
        /// Whether a parameter entity can be switched between colour and vector: a vector one, bar one whose name makes it a
        /// colour whatever the table says (switching it would change nothing on screen).
        /// </summary>
        public static bool CanSwitch(Commands commands, Composite composite, VariableEntity variable)
        {
            if (commands?.Utils == null || composite == null || variable == null || variable.type != DataType.VECTOR)
                return false;
            return !EntityParameterProxy.IsColourName(variable.name.ToString());
        }

        /// <summary>
        /// Whether a switch can be made now: not while the level is being saved or written, which reads the table on another
        /// thread (the menu items are greyed out meanwhile).
        /// </summary>
        public static bool CanEdit => !UndoStack.Current.Blocked;

        /// <summary>
        /// Show the parameters as colours (true) or as vectors (false), as one undoable step. <paramref name="shownIn"/> and
        /// <paramref name="shownEntity"/> are what is on screen, for undo to go back to.
        /// </summary>
        public static void Set(Commands commands, IEnumerable<(Composite composite, VariableEntity variable)> targets, bool colour, Composite shownIn, Entity shownEntity)
        {
            List<(Composite composite, VariableEntity variable)> list = targets.Where(o => o.composite != null && o.variable != null).Distinct().ToList();
            if (commands?.Utils == null || list.Count == 0 || !CanEdit)
                return;
            string what = list.Count == 1 ? "'" + list[0].variable.name.ToString() + "'" : list.Count + " parameters";
            UndoStack.Current.Apply(new ColourParameterEdit(commands, list, colour, shownIn, shownEntity, "Treat " + what + (colour ? " As Colour" : " As Vector")));
        }

        /// <summary>
        /// A flowgraph node or entity list menu's item for the entities right-clicked: shown when every one is a parameter entity
        /// of the composite that can be switched, reading Treat As Vector when they are all colours already and Treat As Colour
        /// otherwise. Returns whether it is shown.
        /// </summary>
        public static bool ConfigureMenuItem(ToolStripMenuItem item, Commands commands, Composite composite, IList<Entity> entities)
        {
            bool show = commands?.Utils != null && composite != null && entities != null && entities.Count != 0
                && entities.All(o => o is VariableEntity variable && CanSwitch(commands, composite, variable));
            item.Visible = show;
            item.Enabled = CanEdit;
            if (show)
                item.Text = entities.All(o => commands.Utils.IsColourParameter(composite, (VariableEntity)o)) ? "Treat As Vector" : "Treat As Colour";
            return show;
        }

        /// <summary>That item clicked: switch the entities the way its text says.</summary>
        public static void MenuItemClicked(Commands commands, Composite composite, IList<Entity> entities, Entity shownEntity)
        {
            if (commands?.Utils == null || composite == null || entities == null)
                return;
            List<(Composite composite, VariableEntity variable)> targets = entities.OfType<VariableEntity>()
                .Where(o => CanSwitch(commands, composite, o)).Select(o => (composite, o)).ToList();
            if (targets.Count == 0)
                return;
            bool colour = !targets.All(o => commands.Utils.IsColourParameter(o.composite, o.variable));
            Set(commands, targets, colour, composite, shownEntity);
        }
    }
}
