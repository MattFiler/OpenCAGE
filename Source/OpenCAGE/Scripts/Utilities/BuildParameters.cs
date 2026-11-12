using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Parameters OpenCAGE's level build honours on an entity type that the game's own definition of the type doesn't declare. The build
    /// reads them while it writes the level's draw lists (CathodeLib Instancing), so the game never sees the parameter itself, only what it
    /// does. They are offered like any other parameter (an inspector checkbox, MCP set_parameters).
    /// </summary>
    public static class BuildParameters
    {
        public sealed class Definition
        {
            public FunctionType Function;
            public string Name;
            public ShortGuid Id;
            public bool Default;
            /// <summary>What it does, for the inspector's description line and MCP.</summary>
            public string Description;
        }

        public static readonly IReadOnlyList<Definition> All = new List<Definition>()
        {
            new Definition()
            {
                Function = FunctionType.SimpleWater,
                Name = "disable_size_culling",
                Id = ShortGuids.disable_size_culling,
                Default = false,
                Description = "Don't cull when far away: draw this water however small it is on screen (big seas otherwise vanish ~200 m out). " +
                    "Applied by OpenCAGE's Save & Build (the draw list's NO_SIZE_CULLING flag), as retail ModelReferences carry it.",
            },
        };

        /// <summary>The build parameter of this entity with this name, or null.</summary>
        public static Definition Find(Entity entity, ShortGuid parameter)
        {
            if (!(entity is FunctionEntity function) || !function.function.IsFunctionType)
                return null;
            FunctionType type = function.function.AsFunctionType;
            return All.FirstOrDefault(o => o.Function == type && o.Id == parameter);
        }

        /// <summary>The build parameters an entity's type takes.</summary>
        public static IEnumerable<Definition> For(Entity entity)
        {
            if (!(entity is FunctionEntity function) || !function.function.IsFunctionType)
                return Enumerable.Empty<Definition>();
            FunctionType type = function.function.AsFunctionType;
            return All.Where(o => o.Function == type);
        }

        /// <summary>
        /// Give the entity each build parameter its type takes and doesn't have yet, at its default, so the inspector shows it as a
        /// row of its own. Only adds what is missing. Returns how many were added.
        /// </summary>
        public static int Ensure(Entity entity)
        {
            int added = 0;
            foreach (Definition definition in For(entity))
            {
                if (entity.GetParameter(definition.Id) != null)
                    continue;
                entity.AddParameter(definition.Id, new cBool(definition.Default), ParameterVariant.PARAMETER, false);
                added++;
            }
            return added;
        }

        /// <summary>The description line for a build parameter, or null for any other.</summary>
        public static string Describe(Entity entity, ShortGuid parameter) => Find(entity, parameter)?.Description;

        /// <summary>The value a build parameter has on the entity (its own, else the default); false when it isn't one.</summary>
        public static bool IsOn(Entity entity, ShortGuid parameter)
        {
            Definition definition = Find(entity, parameter);
            if (definition == null) return false;
            switch (entity.GetParameter(parameter)?.content)
            {
                case cBool b: return b.value;
                case cInteger i: return i.value == 1;
                case cFloat f: return f.value == 1.0f;
                case cString s: return s.value != null && s.value.ToUpperInvariant() == "TRUE";
            }
            return definition.Default;
        }
    }
}
