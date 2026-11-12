using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Hud
{
    /// <summary>A method the movie's root object (its document class) has, as the game can call it.</summary>
    public sealed class MovieMethod
    {
        public string Name;
        /// <summary>Arguments it must be given, and at most (int.MaxValue with ...rest).</summary>
        public int Required, Most;
        public override string ToString() => Name + "(" + (Required == Most ? Required.ToString() : Most == int.MaxValue ? Required + "+" : Required + "-" + Most) + ")";
    }

    /// <summary>A call the game makes on a movie's root: a method name and the number of arguments it passes.</summary>
    public sealed class MovieCall
    {
        public string Method;
        public int Arguments;
        /// <summary>Who makes it, in words ("FlashInvoke 'HUD_Set_Time' in HUD\\Hud", "the game, as the layer loads").</summary>
        public string From;
        /// <summary>The game crashes on this call when the movie's root object doesn't exist (every call through a layer does).</summary>
        public bool Engine;
    }

    /// <summary>What checking a movie found.</summary>
    public sealed class MovieCheckResult
    {
        /// <summary>Faults that stop the movie working: the game would get no root object from it (and crash on its first call).</summary>
        public List<string> Errors = new List<string>();
        /// <summary>Things that won't crash the game but won't work (a call to a method the movie doesn't have does nothing).</summary>
        public List<string> Warnings = new List<string>();
        public bool Ok => Errors.Count == 0;
        /// <summary>The class the movie's root is made from, or null (a plain timeline, or an ActionScript 2 movie).</summary>
        public string DocumentClass;
        public bool ActionScript3;
        public int MethodBodies, Instructions;
        public double StageWidth, StageHeight;
        /// <summary>Public methods of the root object (its document class and the classes of the movie it extends).</summary>
        public List<MovieMethod> RootMethods = new List<MovieMethod>();
        /// <summary>The design an OpenCAGE overlay keeps in the movie (null for any other movie).</summary>
        public string OverlayDesign;

        public string Summary()
        {
            StringBuilder text = new StringBuilder();
            text.Append(Ok ? "OK" : "REFUSED (" + Errors.Count + " fault" + (Errors.Count == 1 ? "" : "s") + ")");
            text.Append(": " + (ActionScript3 ? "ActionScript 3" : "ActionScript 2 / no code") + ", stage " + StageWidth + "x" + StageHeight);
            if (DocumentClass != null) text.Append(", root class " + DocumentClass + " (" + RootMethods.Count + " methods)");
            if (ActionScript3) text.Append(", " + MethodBodies + " method bodies / " + Instructions + " instructions checked");
            return text.ToString();
        }
    }

    /// <summary>
    /// Checks a Scaleform movie before the game is given it. The game makes a movie's root object as it loads a layer, then calls
    /// methods on it without checking it exists (UI::LAYER::call*, FlashInvoke, and init_image_substitution on the ChallengeMapOverlay
    /// layer as the level opens). The ActionScript VM rejects any class with a fault (such as two members with the same name), leaving
    /// no root object, and the game crashes on that first call. So this refuses a movie the VM would reject: the file and its tags, the
    /// ActionScript tables, every class's members, and every method's code (instructions, branches, stack and scope depths on every
    /// path, locals). It then checks the calls the game will make on the root; a missing method just does nothing, so those are warnings.
    /// </summary>
    public static class HudMovieCheck
    {
        /// <summary>The layer name the game gives its own challenge map overlay class, which calls init_image_substitution, resizeListener and more on the root.</summary>
        public const string ChallengeMapLayer = "ChallengeMapOverlay";

        /// <summary>Classes of the Flash player a root may extend (the timeline needs a Sprite or a MovieClip).</summary>
        private static readonly HashSet<string> _displayRoots = new HashSet<string>() { "flash.display.MovieClip", "flash.display.Sprite" };

        /// <summary>Methods every root (a MovieClip) has from the player itself, which FlashInvoke can call (hiding the ammo counter uses swapChildrenAt / removeChildAt).</summary>
        public static readonly Dictionary<string, int> BuiltInRootMethods = new Dictionary<string, int>()
        {
            { "gotoAndStop", 1 }, { "gotoAndPlay", 1 }, { "play", 0 }, { "stop", 0 }, { "nextFrame", 0 }, { "prevFrame", 0 },
            { "swapChildrenAt", 2 }, { "removeChildAt", 1 }, { "getChildAt", 1 }, { "setChildIndex", 2 },
        };

        /// <summary>
        /// Check a movie. <paramref name="calls"/>: what the game will call on its root (the level's FlashInvokes on its layer and
        /// the game's own calls); <paramref name="layerName"/>: the layer it is loaded into (null: not known).
        /// </summary>
        public static MovieCheckResult Check(byte[] movie, IEnumerable<MovieCall> calls = null, string layerName = null)
        {
            MovieCheckResult result = new MovieCheckResult();
            SwfFile swf;
            try
            {
                swf = SwfFile.Read(movie);
            }
            catch (InvalidDataException e)
            {
                result.Errors.Add("The file can't be read as a movie: " + e.Message + ".");
                return result;
            }
            try { swf.StageSize(out result.StageWidth, out result.StageHeight); } catch { }

            //The container
            if (swf.Truncated)
                result.Errors.Add("The movie's tags are cut short (no End tag).");
            if (swf.DeclaredLength != swf.ActualLength)
                result.Errors.Add("The movie's header gives its length as " + swf.DeclaredLength + " bytes but it is " + swf.ActualLength + ".");
            if (swf.TrailingBytes > 0)
                result.Warnings.Add(swf.TrailingBytes + " bytes after the movie's End tag are ignored.");
            if (swf.Version < 9 && swf.Tags.Any(o => o.Type == SwfFile.TagDoAbc || o.Type == SwfFile.TagDoAbcOld))
                result.Errors.Add("The movie holds ActionScript 3 but says it is Flash " + swf.Version + " (ActionScript 3 needs 9 or later).");

            SwfTag attributes = swf.Tags.FirstOrDefault(o => o.Type == SwfFile.TagFileAttributes);
            bool as3Flag = attributes != null && attributes.Body.Length >= 1 && (attributes.Body[0] & 0x08) != 0;
            List<SwfTag> abcTags = swf.Tags.Where(o => o.Type == SwfFile.TagDoAbc || o.Type == SwfFile.TagDoAbcOld).ToList();
            result.ActionScript3 = as3Flag || abcTags.Count != 0;
            if (abcTags.Count != 0 && !as3Flag)
                result.Errors.Add("The movie holds ActionScript 3 code but its FileAttributes tag doesn't say so (the player would not run it).");
            if (attributes != null && swf.Tags.TakeWhile(o => o != attributes).Any(o => o.Type != SwfFile.TagExporterInfo))
                result.Warnings.Add("The FileAttributes tag is not the movie's first tag.");

            SwfTag metadata = swf.Tags.FirstOrDefault(o => o.Type == SwfFile.TagMetadata);
            if (metadata != null)
            {
                string text = SwfFile.ReadStringTag(metadata.Body);
                if (text.StartsWith(HudDesign.MetadataPrefix, StringComparison.Ordinal))
                    result.OverlayDesign = text.Substring(HudDesign.MetadataPrefix.Length);
            }

            //The classes the timeline is bound to
            List<KeyValuePair<int, string>> symbols = new List<KeyValuePair<int, string>>();
            foreach (SwfTag tag in swf.Tags.Where(o => o.Type == SwfFile.TagSymbolClass))
            {
                try { symbols.AddRange(SwfFile.ReadSymbolClass(tag.Body)); }
                catch (InvalidDataException e) { result.Errors.Add("A SymbolClass tag can't be read: " + e.Message + "."); }
            }
            result.DocumentClass = symbols.Where(o => o.Key == 0).Select(o => o.Value).LastOrDefault();

            //Every ActionScript block, read and checked
            List<AbcFile> blocks = new List<AbcFile>();
            for (int i = 0; i < abcTags.Count; i++)
            {
                string where = abcTags.Count == 1 ? "The ActionScript block" : "ActionScript block " + (i + 1);
                try
                {
                    byte[] abc;
                    if (abcTags[i].Type == SwfFile.TagDoAbc)
                        SwfFile.SplitDoAbc(abcTags[i].Body, out _, out _, out abc);
                    else
                        abc = abcTags[i].Body;
                    AbcFile block = AbcFile.Read(abc);
                    blocks.Add(block);
                    CheckBlock(block, where, result);
                }
                catch (InvalidDataException e)
                {
                    result.Errors.Add(where + " can't be read: " + e.Message + ".");
                }
            }

            //Every class the timeline names exists, the root's extends a display class the player has
            Dictionary<string, KeyValuePair<AbcFile, AbcInstance>> classes = new Dictionary<string, KeyValuePair<AbcFile, AbcInstance>>();
            foreach (AbcFile block in blocks)
                foreach (AbcInstance instance in block.Instances)
                {
                    string name = block.MnFullName(instance.Name);
                    if (!classes.ContainsKey(name))
                        classes.Add(name, new KeyValuePair<AbcFile, AbcInstance>(block, instance));
                }
            foreach (KeyValuePair<int, string> symbol in symbols)
                if (!classes.ContainsKey(symbol.Value) && !symbol.Value.StartsWith("flash.", StringComparison.Ordinal) && !symbol.Value.StartsWith("scaleform.", StringComparison.Ordinal))
                    result.Errors.Add("The movie binds " + (symbol.Key == 0 ? "its root" : "character " + symbol.Key) + " to the class " + symbol.Value + ", which its code doesn't define.");

            if (result.DocumentClass != null && classes.TryGetValue(result.DocumentClass, out KeyValuePair<AbcFile, AbcInstance> root))
            {
                //Up the chain to a class of the player's
                HashSet<string> seen = new HashSet<string>();
                KeyValuePair<AbcFile, AbcInstance> at = root;
                List<KeyValuePair<AbcFile, AbcInstance>> chain = new List<KeyValuePair<AbcFile, AbcInstance>>();
                while (true)
                {
                    chain.Add(at);
                    string super = at.Key.MnFullName(at.Value.Super);
                    if (!seen.Add(super))
                    {
                        result.Errors.Add("The root class " + result.DocumentClass + " extends itself.");
                        break;
                    }
                    if (classes.TryGetValue(super, out KeyValuePair<AbcFile, AbcInstance> next))
                    {
                        at = next;
                        continue;
                    }
                    if (!_displayRoots.Contains(super))
                        result.Errors.Add("The root class " + result.DocumentClass + " extends " + super + ", not a MovieClip or Sprite: the timeline can't be made from it.");
                    break;
                }
                if ((root.Value.Flags & AbcInstance.Interface) != 0)
                    result.Errors.Add("The root class " + result.DocumentClass + " is an interface.");

                //Its public methods, the subclass's first
                foreach (KeyValuePair<AbcFile, AbcInstance> cls in chain)
                    foreach (AbcTrait trait in cls.Value.Traits.Where(o => o.Type == AbcTrait.Method))
                    {
                        if (!IsPublic(cls.Key, trait.Name)) continue;
                        string name = cls.Key.MnName(trait.Name);
                        if (result.RootMethods.Any(o => o.Name == name) || trait.B < 0 || trait.B >= cls.Key.Methods.Count) continue;
                        AbcMethod method = cls.Key.Methods[trait.B];
                        result.RootMethods.Add(new MovieMethod()
                        {
                            Name = name,
                            Required = method.ParamTypes.Count - ((method.Flags & AbcMethod.HasOptional) != 0 ? method.Options.Count : 0),
                            Most = (method.Flags & (AbcMethod.NeedRest | AbcMethod.NeedArguments)) != 0 ? int.MaxValue : method.ParamTypes.Count,
                        });
                    }
            }
            else if (result.DocumentClass == null && result.ActionScript3)
                result.Warnings.Add("The movie's root has no class of its own (a plain MovieClip): FlashInvoke can only call the player's own MovieClip methods on it.");

            //What the game will call on it: on the ChallengeMapOverlay layer, the challenge map overlay's own calls too
            List<MovieCall> all = (calls ?? Enumerable.Empty<MovieCall>()).ToList();
            if (layerName != null && string.Equals(layerName, ChallengeMapLayer, StringComparison.Ordinal))
                foreach (KeyValuePair<string, int> engine in new[] { new KeyValuePair<string, int>("init_image_substitution", 3), new KeyValuePair<string, int>("resizeListener", 0), new KeyValuePair<string, int>("refresh_leaderboard_timer", 1) })
                    all.Add(new MovieCall() { Method = engine.Key, Arguments = engine.Value, From = "The game's challenge map layer", Engine = true });
            foreach (MovieCall call in all)
            {
                if (!result.ActionScript3)
                    break;
                MovieMethod method = result.RootMethods.FirstOrDefault(o => o.Name == call.Method);
                if (method == null)
                {
                    if (BuiltInRootMethods.TryGetValue(call.Method, out int builtIn))
                    {
                        if (builtIn != call.Arguments)
                            result.Warnings.Add(call.From + " calls the MovieClip method " + call.Method + " with " + call.Arguments + " argument(s); it takes " + builtIn + ".");
                        continue;
                    }
                    result.Warnings.Add(call.From + " calls " + call.Method + "(" + call.Arguments + " argument" + (call.Arguments == 1 ? "" : "s") + "), which the movie's root doesn't have: that call does nothing.");
                }
                else if (call.Arguments < method.Required || call.Arguments > method.Most)
                    result.Warnings.Add(call.From + " calls " + call.Method + " with " + call.Arguments + " argument(s), but it takes " + (method.Required == method.Most ? method.Required.ToString() : method.Required + " to " + (method.Most == int.MaxValue ? "any" : method.Most.ToString())) + ": that call does nothing.");
            }
            return result;
        }

        private static bool IsPublic(AbcFile a, int multiname)
        {
            if (multiname <= 0 || multiname >= a.Multinames.Count) return false;
            AbcMultiname m = a.Multinames[multiname];
            if (m.Kind != AbcMultiname.QName && m.Kind != AbcMultiname.QNameA) return false;
            if (m.A <= 0 || m.A >= a.Namespaces.Count) return false;
            AbcNamespace ns = a.Namespaces[m.A];
            return ns.Kind == AbcNamespace.Package && a.Str(ns.Name) == "" || ns.Kind == AbcNamespace.Package && ns.Name == 0;
        }

        #region The ActionScript block
        private static void CheckBlock(AbcFile a, string where, MovieCheckResult result)
        {
            List<string> errors = result.Errors;
            int before = errors.Count;
            Action<string> fault = text => { if (errors.Count - before < 40) errors.Add(where + ": " + text); };

            if (a.Major != 46)
                fault("version " + a.Major + "." + a.Minor + " (the player runs 46.x)");

            //Constant pools refer inside themselves
            for (int i = 1; i < a.Namespaces.Count; i++)
                if (a.Namespaces[i].Name >= a.Strings.Count) fault("namespace " + i + " names string " + a.Namespaces[i].Name + ", past the " + a.Strings.Count + " there are");
            for (int i = 1; i < a.NsSets.Count; i++)
                foreach (int ns in a.NsSets[i])
                    if (ns <= 0 || ns >= a.Namespaces.Count) fault("namespace set " + i + " holds namespace " + ns + ", which doesn't exist");
            for (int i = 1; i < a.Multinames.Count; i++)
            {
                AbcMultiname m = a.Multinames[i];
                switch (m.Kind)
                {
                    case AbcMultiname.QName: case AbcMultiname.QNameA:
                        if (m.A >= a.Namespaces.Count || m.B >= a.Strings.Count) fault("name " + i + " points past the namespace or string tables");
                        break;
                    case AbcMultiname.RTQName: case AbcMultiname.RTQNameA:
                        if (m.A >= a.Strings.Count) fault("name " + i + " points past the string table");
                        break;
                    case AbcMultiname.Multiname: case AbcMultiname.MultinameA:
                        if (m.A >= a.Strings.Count || m.B <= 0 || m.B >= a.NsSets.Count) fault("name " + i + " points past the string or namespace set tables");
                        break;
                    case AbcMultiname.MultinameL: case AbcMultiname.MultinameLA:
                        if (m.B <= 0 || m.B >= a.NsSets.Count) fault("name " + i + " points past the namespace set table");
                        break;
                    case AbcMultiname.TypeName:
                        if (m.A <= 0 || m.A >= a.Multinames.Count || m.Params.Any(o => o < 0 || o >= a.Multinames.Count)) fault("generic name " + i + " points past the name table");
                        break;
                }
            }

            //Methods, metadata, classes, scripts point at what exists
            for (int i = 0; i < a.Methods.Count; i++)
            {
                AbcMethod m = a.Methods[i];
                if (m.ReturnType >= a.Multinames.Count || m.ParamTypes.Any(o => o >= a.Multinames.Count) || m.Name >= a.Strings.Count)
                    fault("method " + i + "'s types or name point past the tables");
                if ((m.Flags & AbcMethod.HasOptional) != 0 && m.Options.Count > m.ParamTypes.Count)
                    fault("method " + i + " has more optional values than parameters");
                if ((m.Flags & AbcMethod.NeedRest) != 0 && (m.Flags & AbcMethod.NeedArguments) != 0)
                    fault("method " + i + " asks for both ...rest and arguments");
            }
            foreach (AbcMetadata md in a.Metadata)
                if (md.Name >= a.Strings.Count || md.Items.Any(o => o >= a.Strings.Count)) fault("a metadata entry points past the string table");

            int[] bodies = new int[a.Methods.Count];
            foreach (AbcBody body in a.Bodies)
            {
                if (body.Method < 0 || body.Method >= a.Methods.Count) { fault("a method body belongs to method " + body.Method + ", which doesn't exist"); continue; }
                if (++bodies[body.Method] == 2) fault("method " + body.Method + " has two bodies");
            }
            Action<int, string> needsBody = (method, what) =>
            {
                if (method < 0 || method >= a.Methods.Count) fault(what + " is method " + method + ", which doesn't exist");
                else if (bodies[method] == 0) fault(what + " (method " + method + ") has no code");
            };

            HashSet<string> classNames = new HashSet<string>();
            for (int i = 0; i < a.Instances.Count; i++)
            {
                AbcInstance instance = a.Instances[i];
                string name = Named(a, instance.Name);
                if (!IsQName(a, instance.Name)) fault("class " + i + "'s name is not a plain qualified name");
                else if (!classNames.Add(a.MnFullName(instance.Name))) fault("two classes are called " + a.MnFullName(instance.Name));
                if (instance.Super >= a.Multinames.Count || instance.Interfaces.Any(o => o <= 0 || o >= a.Multinames.Count))
                    fault("class " + name + " extends or implements a name past the name table");
                if ((instance.Flags & AbcInstance.ProtectedNsFlag) != 0 && (instance.ProtectedNs <= 0 || instance.ProtectedNs >= a.Namespaces.Count))
                    fault("class " + name + "'s protected namespace doesn't exist");
                //An interface's methods (and constructor) have no code
                bool isInterface = (instance.Flags & AbcInstance.Interface) != 0;
                Action<int, string> instanceNeeds = isInterface ? (method, what) => { if (method < 0 || method >= a.Methods.Count) fault(what + " is method " + method + ", which doesn't exist"); } : needsBody;
                instanceNeeds(instance.IInit, "class " + name + "'s constructor");
                CheckTraits(a, instance.Traits, "class " + name, fault, instanceNeeds);
                if (i < a.Classes.Count)
                {
                    needsBody(a.Classes[i].CInit, "class " + name + "'s static initialiser");
                    CheckTraits(a, a.Classes[i].Traits, "class " + name + " (static)", fault, needsBody);
                }
            }
            for (int i = 0; i < a.Scripts.Count; i++)
            {
                needsBody(a.Scripts[i].Init, "script " + i + "'s initialiser");
                CheckTraits(a, a.Scripts[i].Traits, "script " + i, fault, needsBody);
            }
            if (a.Scripts.Count == 0 && a.Instances.Count != 0)
                fault("classes but no script to define them");

            //Every method's code
            foreach (AbcBody body in a.Bodies)
            {
                if (body.Method < 0 || body.Method >= a.Methods.Count) continue;
                string what = "method " + body.Method + (a.Methods[body.Method].Name > 0 ? " '" + a.Str(a.Methods[body.Method].Name) + "'" : "");
                CheckTraits(a, body.Traits, what + "'s activation", fault, needsBody);
                CheckCode(a, body, what, fault, result);
                result.MethodBodies++;
            }
        }

        private static string Named(AbcFile a, int multiname) => multiname > 0 && multiname < a.Multinames.Count ? a.MnFullName(multiname) : "#" + multiname;

        private static bool IsQName(AbcFile a, int multiname) =>
            multiname > 0 && multiname < a.Multinames.Count && (a.Multinames[multiname].Kind == AbcMultiname.QName || a.Multinames[multiname].Kind == AbcMultiname.QNameA);

        /* A list of members: names that are qualified names, what they point at exists, and no name twice (a getter and a setter may share
           one) - the AVM2 refuses the whole class when two members share a name */
        private static void CheckTraits(AbcFile a, List<AbcTrait> traits, string owner, Action<string> fault, Action<int, string> needsBody)
        {
            Dictionary<string, List<AbcTrait>> byName = new Dictionary<string, List<AbcTrait>>();
            foreach (AbcTrait t in traits)
            {
                if (!IsQName(a, t.Name)) { fault(owner + " has a member whose name is not a plain qualified name"); continue; }
                AbcMultiname m = a.Multinames[t.Name];
                string key = (m.A > 0 && m.A < a.Namespaces.Count ? a.Namespaces[m.A].Kind + ":" + a.Str(a.Namespaces[m.A].Name) + (a.Namespaces[m.A].Kind == AbcNamespace.Private ? "#" + m.A : "") : "?") + "::" + a.Str(m.B);
                if (!byName.TryGetValue(key, out List<AbcTrait> same)) byName.Add(key, same = new List<AbcTrait>());
                same.Add(t);
                string name = a.MnName(t.Name);
                switch (t.Type)
                {
                    case AbcTrait.Slot: case AbcTrait.Const:
                        if (t.B >= a.Multinames.Count) fault(owner + "." + name + "'s type points past the name table");
                        break;
                    case AbcTrait.Method: case AbcTrait.Getter: case AbcTrait.Setter: case AbcTrait.Function:
                        needsBody(t.B, owner + "." + name);
                        break;
                    case AbcTrait.Class:
                        if (t.B < 0 || t.B >= a.Classes.Count) fault(owner + "." + name + " is class " + t.B + ", which doesn't exist");
                        break;
                }
                foreach (int md in t.Metadata)
                    if (md < 0 || md >= a.Metadata.Count) fault(owner + "." + name + " has metadata that doesn't exist");
            }
            foreach (KeyValuePair<string, List<AbcTrait>> same in byName.Where(o => o.Value.Count > 1))
            {
                bool getterSetter = same.Value.Count == 2 && same.Value.Count(o => o.Type == AbcTrait.Getter) == 1 && same.Value.Count(o => o.Type == AbcTrait.Setter) == 1;
                if (!getterSetter)
                    fault(owner + " has " + same.Value.Count + " members called '" + a.MnName(same.Value[0].Name) + "' (" + string.Join(" and ", same.Value.Select(o => KindName(o.Type))) + "): the ActionScript machine refuses the whole class, so the movie gets no root object");
            }
        }

        private static string KindName(int type)
        {
            switch (type)
            {
                case AbcTrait.Slot: return "a variable";
                case AbcTrait.Const: return "a constant";
                case AbcTrait.Method: return "a method";
                case AbcTrait.Getter: return "a getter";
                case AbcTrait.Setter: return "a setter";
                case AbcTrait.Class: return "a class";
                default: return "a function";
            }
        }
        #endregion

        #region Code
        private struct State { public int Stack, Scope; }

        /// <summary>
        /// For code being written: the deepest stack and local scope stack any path through a body reaches (the body's limits are
        /// set from these). Faults found on the way are returned; none means the code passes.
        /// </summary>
        public static List<string> Measure(AbcFile a, AbcBody body, out int maxStack, out int maxScope)
        {
            List<string> faults = new List<string>();
            int[] peak = new int[2];
            int keepStack = body.MaxStack, keepScope = body.MaxScope;
            body.MaxStack = int.MaxValue / 2;
            body.MaxScope = body.InitScope + 1000;
            try
            {
                CheckCode(a, body, "method " + body.Method, faults.Add, new MovieCheckResult(), peak);
            }
            finally
            {
                body.MaxStack = keepStack;
                body.MaxScope = keepScope;
            }
            maxStack = peak[0];
            maxScope = peak[1];
            return faults;
        }

        /// <summary>Decode one method's code and follow every path: each instruction known, its operands in range, every branch onto an instruction, the stack and scope depths the same wherever paths meet and within the body's limits, no path running off the end.</summary>
        private static void CheckCode(AbcFile a, AbcBody body, string what, Action<string> fault, MovieCheckResult result, int[] peak = null)
        {
            byte[] code = body.Code;
            AbcMethod method = a.Methods[body.Method];
            int scopeRoom = body.MaxScope - body.InitScope;
            if (scopeRoom < 0) { fault(what + ": its scope depth limits are the wrong way round"); return; }
            int needLocals = 1 + method.ParamTypes.Count + ((method.Flags & (AbcMethod.NeedRest | AbcMethod.NeedArguments)) != 0 ? 1 : 0);
            if (body.LocalCount < needLocals) fault(what + ": " + body.LocalCount + " locals, too few for 'this' and its parameters (" + needLocals + ")");
            if (code.Length == 0) { fault(what + ": no code"); return; }

            //Decode: where each instruction starts, what it is, where it can go next
            Dictionary<int, int> next = new Dictionary<int, int>();
            Dictionary<int, List<int>> targets = new Dictionary<int, List<int>>();
            Dictionary<int, AbcOps.Op> ops = new Dictionary<int, AbcOps.Op>();
            Dictionary<int, int[]> operands = new Dictionary<int, int[]>();
            //Faults of single instructions, reported only when a path reaches them: the machine never checks code no path reaches
            //(compilers leave a jump past the end after a throw)
            Dictionary<int, string> problems = new Dictionary<int, string>();
            AbcReader r = new AbcReader(code);
            try
            {
                while (!r.AtEnd)
                {
                    int at = r.Position;
                    byte opcode = r.U8();
                    AbcOps.Op op = AbcOps.Get(opcode);
                    if (op == null) { fault(what + ": unknown instruction 0x" + opcode.ToString("X2") + " at " + at); return; }
                    List<int> values = new List<int>();
                    List<int> to = new List<int>();
                    foreach (char kind in op.Operands)
                    {
                        switch (kind)
                        {
                            case 'b': values.Add(r.U8()); break;
                            case 'B': values.Add((sbyte)r.U8()); break;
                            case 'o': { int offset = r.S24(); values.Add(offset); to.Add(r.Position + offset); break; }
                            case 'W':
                                {
                                    to.Add(at + r.S24());
                                    int cases = r.U30();
                                    if (cases > code.Length) { fault(what + ": a switch at " + at + " with " + cases + " cases"); return; }
                                    for (int i = 0; i <= cases; i++) to.Add(at + r.S24());
                                    break;
                                }
                            case 'D': r.U8(); values.Add(r.U30()); r.U8(); r.U30(); break;
                            default: values.Add(r.U30()); break;
                        }
                    }
                    if (r.Position > code.Length) { fault(what + ": the last instruction is cut short"); return; }
                    string bad = OperandProblem(a, body, op, values);
                    if (bad != null) problems[at] = op.Name + " at " + at + " " + bad;
                    ops[at] = op;
                    operands[at] = values.ToArray();
                    targets[at] = to;
                    next[at] = r.Position;
                    result.Instructions++;
                }
            }
            catch (IndexOutOfRangeException)
            {
                fault(what + ": the last instruction is cut short");
                return;
            }

            foreach (AbcException e in body.Exceptions)
            {
                if (e.From > e.To || e.To > code.Length || !ops.ContainsKey(e.Target) || (e.From < code.Length && !ops.ContainsKey(e.From)))
                { fault(what + ": an exception handler covers code that isn't there"); return; }
                if (e.ExcType >= a.Multinames.Count || e.VarName >= a.Multinames.Count)
                { fault(what + ": an exception handler's type points past the name table"); return; }
            }

            //Follow every path
            Dictionary<int, State> seen = new Dictionary<int, State>();
            Stack<KeyValuePair<int, State>> work = new Stack<KeyValuePair<int, State>>();
            work.Push(new KeyValuePair<int, State>(0, new State()));
            foreach (AbcException e in body.Exceptions)
                work.Push(new KeyValuePair<int, State>(e.Target, new State() { Stack = 1, Scope = 0 }));
            while (work.Count != 0)
            {
                KeyValuePair<int, State> item = work.Pop();
                int at = item.Key;
                State state = item.Value;
                if (seen.TryGetValue(at, out State had))
                {
                    if (had.Stack != state.Stack) { fault(what + ": paths meet at " + at + " with " + had.Stack + " and " + state.Stack + " values on the stack"); return; }
                    if (had.Scope != state.Scope) { fault(what + ": paths meet at " + at + " with scope depths " + had.Scope + " and " + state.Scope); return; }
                    continue;
                }
                if (!ops.ContainsKey(at)) { fault(what + ": runs off the end of its code"); return; }
                seen[at] = state;
                AbcOps.Op op = ops[at];
                int[] values = operands[at];
                if (problems.TryGetValue(at, out string problem)) { fault(what + ": " + problem); return; }
                foreach (int target in targets[at])
                    if (!ops.ContainsKey(target)) { fault(what + ": " + op.Name + " at " + at + " goes to " + target + ", which is not the start of an instruction"); return; }
                Effect(a, op, values, out int pop, out int push, out int scope);
                if (op.Name == "getscopeobject" && values[0] >= state.Scope)
                { fault(what + ": getscopeobject " + values[0] + " at " + at + " with only " + state.Scope + " scopes"); return; }
                if (state.Stack < pop) { fault(what + ": " + op.Name + " at " + at + " takes " + pop + " values from a stack of " + state.Stack); return; }
                state.Stack += push - pop;
                state.Scope += scope;
                if (peak != null) { peak[0] = Math.Max(peak[0], state.Stack); peak[1] = Math.Max(peak[1], state.Scope); }
                if (state.Stack > body.MaxStack) { fault(what + ": the stack grows to " + state.Stack + " at " + at + " (its limit is " + body.MaxStack + ")"); return; }
                if (state.Scope < 0) { fault(what + ": popscope at " + at + " with no scope to pop"); return; }
                if (state.Scope > scopeRoom) { fault(what + ": the scope depth grows to " + state.Scope + " at " + at + " (room for " + scopeRoom + ")"); return; }
                foreach (int target in targets[at])
                    work.Push(new KeyValuePair<int, State>(target, state));
                if (!op.Ends)
                {
                    if (next[at] >= code.Length) { fault(what + ": runs off the end of its code after " + op.Name + " at " + at); return; }
                    work.Push(new KeyValuePair<int, State>(next[at], state));
                }
            }
        }

        private static string OperandProblem(AbcFile a, AbcBody body, AbcOps.Op op, List<int> values)
        {
            for (int i = 0; i < op.Operands.Length && i < values.Count; i++)
            {
                int v = values[i];
                switch (op.Operands[i])
                {
                    case 'm': if (v <= 0 || v >= a.Multinames.Count) return "names #" + v + ", past the name table"; break;
                    case 's': if (v <= 0 || v >= a.Strings.Count) return "uses string #" + v + ", past the string table"; break;
                    case 'i': if (v <= 0 || v >= a.Ints.Count) return "uses int #" + v + ", past the int table"; break;
                    case 'I': if (v <= 0 || v >= a.UInts.Count) return "uses uint #" + v + ", past the uint table"; break;
                    case 'd': if (v <= 0 || v >= a.Doubles.Count) return "uses number #" + v + ", past the number table"; break;
                    case 'n': if (v <= 0 || v >= a.Namespaces.Count) return "uses namespace #" + v + ", past the namespace table"; break;
                    case 'M': if (v < 0 || v >= a.Methods.Count) return "uses method #" + v + ", which doesn't exist"; break;
                    case 'C': if (v < 0 || v >= a.Classes.Count) return "uses class #" + v + ", which doesn't exist"; break;
                    case 'x': if (v < 0 || v >= body.Exceptions.Count) return "uses exception handler #" + v + ", which doesn't exist"; break;
                    case 'L': if (v < 0 || v >= body.LocalCount) return "uses local " + v + " (the method has " + body.LocalCount + ")"; break;
                }
            }
            if (op.Name.StartsWith("getlocal") && op.Name.Length == 9 || op.Name.StartsWith("setlocal") && op.Name.Length == 9)
            {
                int local = op.Name[8] - '0';
                if (local >= body.LocalCount) return "uses local " + local + " (the method has " + body.LocalCount + ")";
            }
            if (op.Name == "getlex" && values.Count == 1 && values[0] > 0 && values[0] < a.Multinames.Count && a.Multinames[values[0]].RuntimeParts != 0)
                return "with a name worked out at run time (not allowed)";
            return null;
        }

        /* What one instruction does to the stack (values taken, values left) and the scope stack */
        private static void Effect(AbcFile a, AbcOps.Op op, int[] v, out int pop, out int push, out int scope)
        {
            pop = 0; push = 0; scope = 0;
            Func<int, int> rt = mn => mn > 0 && mn < a.Multinames.Count ? a.Multinames[mn].RuntimeParts : 0;
            switch (op.Code)
            {
                case 0x03: case 0x07: case 0x11: case 0x12: case 0x1B: case 0x29: case 0x48: case 0x63: case 0x6F:
                case 0xD4: case 0xD5: case 0xD6: case 0xD7: pop = 1; break;
                case 0x1C: case 0x30: pop = 1; scope = 1; break;
                case 0x1D: scope = -1; break;
                case 0x0C: case 0x0D: case 0x0E: case 0x0F: case 0x13: case 0x14: case 0x15: case 0x16: case 0x17: case 0x18: case 0x19: case 0x1A:
                case 0x3A: case 0x3B: case 0x3C: case 0x3D: case 0x3E: case 0x6D: pop = 2; break;
                case 0x2A: pop = 1; push = 2; break;
                case 0x2B: pop = 2; push = 2; break;
                case 0x1E: case 0x1F: case 0x23: case 0x87: case 0xB3: case 0xB4: case 0xC5: case 0xC6: case 0xC7:
                case 0xA0: case 0xA1: case 0xA2: case 0xA3: case 0xA4: case 0xA5: case 0xA6: case 0xA7: case 0xA8: case 0xA9:
                case 0xAA: case 0xAB: case 0xAC: case 0xAD: case 0xAE: case 0xAF: case 0xB0: case 0xB1:
                    pop = 2; push = 1; break;
                case 0x20: case 0x21: case 0x24: case 0x25: case 0x26: case 0x27: case 0x28: case 0x2C: case 0x2D: case 0x2E: case 0x2F: case 0x31:
                case 0x32: case 0x40: case 0x57: case 0x5A: case 0x5F: case 0x60: case 0x62: case 0x64: case 0x65: case 0x67: case 0x6E:
                case 0xD0: case 0xD1: case 0xD2: case 0xD3: push = 1; break;
                case 0x35: case 0x36: case 0x37: case 0x38: case 0x39: case 0x50: case 0x51: case 0x52: case 0x58: case 0x6C:
                case 0x70: case 0x71: case 0x72: case 0x73: case 0x74: case 0x75: case 0x76: case 0x77: case 0x78:
                case 0x80: case 0x81: case 0x82: case 0x83: case 0x84: case 0x85: case 0x86: case 0x88: case 0x89:
                case 0x90: case 0x91: case 0x93: case 0x95: case 0x96: case 0x97: case 0xB2: case 0xC0: case 0xC1: case 0xC4:
                    pop = 1; push = 1; break;
                case 0x04: pop = 1 + rt(v[0]); push = 1; break;                    //getsuper
                case 0x05: pop = 2 + rt(v[0]); break;                               //setsuper
                case 0x41: pop = v[0] + 2; push = 1; break;                         //call
                case 0x42: pop = v[0] + 1; push = 1; break;                         //construct
                case 0x43: case 0x44: pop = v[1] + 1; push = 1; break;              //callmethod, callstatic
                case 0x45: case 0x46: case 0x4A: case 0x4C: pop = v[1] + 1 + rt(v[0]); push = 1; break;
                case 0x4E: case 0x4F: pop = v[1] + 1 + rt(v[0]); break;             //callsupervoid, callpropvoid
                case 0x49: pop = v[0] + 1; break;                                   //constructsuper
                case 0x53: pop = v[0] + 1; push = 1; break;                         //applytype
                case 0x55: pop = v[0] * 2; push = 1; break;                         //newobject
                case 0x56: pop = v[0]; push = 1; break;                             //newarray
                case 0x59: case 0x66: case 0x6A: pop = 1 + rt(v[0]); push = 1; break; //getdescendants, getproperty, deleteproperty
                case 0x5D: case 0x5E: pop = rt(v[0]); push = 1; break;              //findpropstrict, findproperty
                case 0x61: case 0x68: pop = 2 + rt(v[0]); break;                    //setproperty, initproperty
                default: break;                                                     //nop, label, kill, jump, returnvoid, inc/declocal, debug...
            }
        }
        #endregion
    }
}
