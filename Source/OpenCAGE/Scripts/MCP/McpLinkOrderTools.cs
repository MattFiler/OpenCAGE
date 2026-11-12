using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>The order of a pin's links (<see cref="LinkOrder"/>): the order a relay fires them in, and for links that set a
    /// value, which one wins (the last). Kept whatever pages the links are drawn on.</summary>
    internal static class McpLinkOrderTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "get_link_order",
                Title = "Get link order",
                Description = "The order an entity's pins hold their links in: the order a relay pin fires them, and for links that set a value, the last one wins. " +
                    "The order is kept whatever flowgraph pages the links are drawn on (tidying nodes onto pages does not change it); new links go last. Indexes are what set_link_order takes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity whose pins' links to list (id or name).", required: true),
                    McpSchema.String("pin", "Only this pin (default: every pin with links).")),
                ReadOnly = true,
                Run = call => McpEditor.UI(() => Get(call)),
            };

            yield return new McpTool()
            {
                Name = "set_link_order",
                Title = "Set link order",
                Description = "Put one pin's links in a new order: 'order' lists the pin's link indexes (from get_link_order) in the order wanted - the first fires first. " +
                    "Links left out keep their order after the listed ones. One undo step; written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity owning the links (id or name).", required: true),
                    McpSchema.String("pin", "Its pin the links leave from.", required: true),
                    McpSchema.Array("order", "Link indexes (0-based, as get_link_order lists them) in the new order.", new JObject() { ["type"] = "integer" }, required: true)),
                Idempotent = true,
                Run = call => McpEditor.UI(() => Set(call)),
            };
        }

        private static (Commands, Composite, Entity) Find(McpCall call, bool forEditing)
        {
            Commands commands = McpEditor.RequireCommands(forEditing);
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            //Links drawn on the open composite's pages since its last compile are stored first (in their kept order)
            McpBrowseTools.CompileIfShown(composite);
            Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
            return (commands, composite, entity);
        }

        private static JObject Describe(Commands commands, Composite composite, Entity owner, ShortGuid pin)
        {
            JArray links = new JArray();
            List<EntityConnector> stored = LinkOrder.PinLinks(owner, pin);
            for (int i = 0; i < stored.Count; i++)
            {
                Entity target = composite.GetEntityByID(stored[i].linkedEntityID);
                links.Add(new JObject()
                {
                    ["index"] = i,
                    ["to"] = McpScript.Id(stored[i].linkedEntityID),
                    ["to_name"] = target != null ? McpScript.EntityName(commands, composite, target) : null,
                    ["to_param"] = McpScript.ParamName(stored[i].linkedParamID),
                });
            }
            return new JObject() { ["pin"] = McpScript.ParamName(pin), ["links"] = links };
        }

        private static object Get(McpCall call)
        {
            (Commands commands, Composite composite, Entity owner) = Find(call, forEditing: false);
            JArray pins = new JArray();
            IEnumerable<ShortGuid> wanted = call.Has("pin")
                ? new[] { McpScript.ParamId(call.Str("pin")) }
                : owner.childLinks.Select(o => o.thisParamID).Distinct();
            foreach (ShortGuid pin in wanted)
                pins.Add(Describe(commands, composite, owner, pin));
            JObject result = new JObject()
            {
                ["composite"] = McpScript.Id(composite.shortGUID),
                ["entity"] = McpScript.Id(owner.shortGUID),
                ["name"] = McpScript.EntityName(commands, composite, owner),
                ["pins"] = pins,
            };
            //Its pages' last compile this session: how many pins they drew in another order (kept as stored)
            int putBack = LinkOrder.LastPutBack(composite);
            if (putBack >= 0)
                result["pages_reordered_pins_last_compile"] = putBack;
            return result;
        }

        private static object Set(McpCall call)
        {
            (Commands commands, Composite composite, Entity owner) = Find(call, forEditing: true);
            ShortGuid pin = McpScript.ParamId(call.Str("pin", required: true));
            List<EntityConnector> stored = LinkOrder.PinLinks(owner, pin);
            if (stored.Count == 0)
                throw McpError.Invalid("'" + call.Str("pin") + "' on " + McpScript.EntityName(commands, composite, owner) + " has no links (get_link_order lists the pins that have).");

            List<EntityConnector> order = new List<EntityConnector>();
            HashSet<int> used = new HashSet<int>();
            foreach (JToken token in call.Array("order", required: true))
            {
                int index = token.Type == JTokenType.Integer ? token.Value<int>() : -1;
                if (index < 0 || index >= stored.Count)
                    throw McpError.Invalid("order: " + token + " is not a link index of that pin (0 to " + (stored.Count - 1) + ").");
                if (!used.Add(index))
                    throw McpError.Invalid("order: index " + index + " is listed twice.");
                order.Add(stored[index]);
            }

            bool moved = LinkOrder.Apply(composite, owner, pin, order, "Reorder the links of " + McpScript.ParamName(pin) + " on " + McpScript.EntityName(commands, composite, owner));
            JObject result = Describe(commands, composite, owner, pin);
            result["changed"] = moved;
            return result;
        }
    }
}
