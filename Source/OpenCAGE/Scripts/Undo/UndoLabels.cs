using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;

namespace OpenCAGE.Undo
{
    /// <summary>The words the Edit menu uses for a change.</summary>
    internal static class UndoLabels
    {
        public static string Entity(Composite composite, Entity entity)
        {
            string name = null;
            try
            {
                name = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands?.Utils?.GetEntityName(composite, entity);
            }
            catch
            {
                //A name is a nicety; the id below will do
            }
            if (string.IsNullOrEmpty(name))
                name = entity == null ? "entity" : entity.shortGUID.ToByteString();
            return name;
        }

        public static string Parameter(Parameter parameter)
        {
            return parameter == null ? "parameter" : parameter.name.ToString();
        }

        public static string ChangeParameter(Composite composite, Entity entity, Parameter parameter)
        {
            if (parameter != null && parameter.name == ShortGuids.position)
                return "Move " + Entity(composite, entity);
            if (parameter != null && parameter.name == ShortGuids.name)
                return "Rename " + Entity(composite, entity);
            return "Change " + Parameter(parameter) + " on " + Entity(composite, entity);
        }

        public static string Count(int count, string singular, string plural)
        {
            return count == 1 ? singular : count + " " + plural;
        }
    }
}
