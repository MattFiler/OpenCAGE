using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenCAGE.Hud
{
    /// <summary>
    /// Makes a HUD overlay movie from a <see cref="HudDesign"/>: a Scaleform movie (as the game's own: GFX 11, a 1280x720 stage)
    /// whose root is the class OpenCageHud. Its code is fixed except for the slot-building method, written per design as calls to
    /// the fixed helpers, and every movie is checked by <see cref="HudMovieCheck"/> before it is used.
    ///
    /// What the game and the level's script call on the root:
    ///   Init(layer)            - the game, once each time the layer loads: builds the slots (the stage shown whole)
    ///   hudSet(slot, value)    - FlashInvoke INT_FLOAT: the slot's value (a text slot's line, a number, seconds, a bar's amount)
    ///   hudShow(slot, on)      - FlashInvoke INT_INT: show (on != 0) or hide a slot; slot -1 is the whole overlay
    /// The constructor does nothing but call MovieClip's, so the root object is made whatever the design.
    /// </summary>
    public static class HudMovieBuilder
    {
        public const string DocumentClass = "OpenCageHud";
        public const string InitMethod = "Init", SetMethod = "hudSet", ShowMethod = "hudShow";
        public const int StageWidth = 1280, StageHeight = 720, FrameRate = 30;

        //What ocValue does with a slot's value
        private const int KindText = 0, KindNumber = 1, KindTimer = 2, KindBar = 3, KindShape = 4;

        /// <summary>The calls the game and the HUD's script make on an overlay's root: what the movie is checked against.</summary>
        public static List<MovieCall> Calls(HudDesign design)
        {
            List<MovieCall> calls = new List<MovieCall>()
            {
                new MovieCall() { Method = InitMethod, Arguments = 1, From = "The game (as the layer loads)", Engine = true },
                new MovieCall() { Method = ShowMethod, Arguments = 2, From = "The HUD's show / hide pins" },
            };
            foreach (HudSlot slot in design.Slots)
            {
                if (slot.TakesValue)
                    calls.Add(new MovieCall() { Method = SetMethod, Arguments = 2, From = "The HUD's FlashInvoke for '" + slot.Name + "'" });
                if (slot.ShowHidePins)
                    calls.Add(new MovieCall() { Method = ShowMethod, Arguments = 2, From = "The HUD's show/hide FlashInvokes for '" + slot.Name + "'" });
            }
            return calls;
        }

        /// <summary>
        /// The movie for a design, and what checking it found (the caller writes it only when <see cref="MovieCheckResult.Ok"/>).
        /// Throws <see cref="ArgumentException"/> for a design with problems (<see cref="HudDesign.Problems"/>).
        /// </summary>
        public static byte[] Build(HudDesign design, out MovieCheckResult check)
        {
            List<string> problems = design.Problems();
            if (problems.Count != 0)
                throw new ArgumentException(string.Join(" ", problems));

            //Pictures: a lossless bitmap per image slot, bound to a BitmapData class of its own (as Flash exports a library bitmap)
            List<SwfTag> bitmaps = new List<SwfTag>();
            List<KeyValuePair<int, string>> symbols = new List<KeyValuePair<int, string>>() { new KeyValuePair<int, string>(0, DocumentClass) };
            Dictionary<HudSlot, string> imageClasses = new Dictionary<HudSlot, string>();
            foreach (HudSlot slot in design.Slots.Where(o => o.Kind == HudSlotKind.Image))
            {
                byte[] rgba = HudPng.Decode(Convert.FromBase64String(slot.ImagePng), out int width, out int height);
                int id = bitmaps.Count + 1;
                string className = "OcImg" + design.Slots.IndexOf(slot);
                bitmaps.Add(new SwfTag() { Type = SwfFile.TagDefineBitsLossless2, Body = HudPng.LosslessTag(id, width, height, rgba) });
                symbols.Add(new KeyValuePair<int, string>(id, className));
                imageClasses[slot] = className;
            }

            Writer writer = new Writer();
            writer.Fixed();
            foreach (string className in imageClasses.Values)
                writer.ImageClass(className);
            writer.Slots(design, imageClasses);
            AbcFile abc = writer.Finish();

            SwfFile swf = new SwfFile() { Signature = "GFX", Version = 11, FrameHeader = SwfFile.MakeFrameHeader(StageWidth, StageHeight, FrameRate) };
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagExporterInfo, Body = ExporterInfo(design.Name) });
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagFileAttributes, Body = new byte[] { 0x18, 0, 0, 0 } }); //ActionScript 3, has metadata
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagMetadata, Body = SwfFile.MakeStringTag(HudDesign.MetadataPrefix + design.ToJson(false)) });
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagSetBackgroundColor, Body = new byte[] { 0, 0, 0 } });
            swf.Tags.AddRange(bitmaps);
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagDoAbc, Body = SwfFile.JoinDoAbc(1, "", abc.Write()) });
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagSymbolClass, Body = SwfFile.MakeSymbolClass(symbols) });
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagShowFrame, Body = new byte[0] });
            swf.Tags.Add(new SwfTag() { Type = SwfFile.TagEnd, Body = new byte[0] });
            byte[] movie = swf.Write();

            check = CheckOverlay(movie, design);
            return movie;
        }

        /// <summary>A movie checked as a design's overlay: what the game and the HUD's script call on it must be there.</summary>
        public static MovieCheckResult CheckOverlay(byte[] movie, HudDesign design)
        {
            MovieCheckResult check = HudMovieCheck.Check(movie, Calls(design), design.LayerName);
            if (check.ActionScript3 || check.Ok)
                foreach (string method in new[] { InitMethod, SetMethod, ShowMethod })
                    if (!check.RootMethods.Any(o => o.Name == method))
                        check.Errors.Add("The overlay's root has no " + method + " method: it isn't an overlay the HUD's script can drive.");
            return check;
        }

        /// <summary>The design an OpenCAGE overlay movie keeps, or null (another movie, or unreadable).</summary>
        public static HudDesign ReadDesign(byte[] movie)
        {
            try
            {
                SwfFile swf = SwfFile.Read(movie);
                SwfTag tag = swf.Tags.FirstOrDefault(o => o.Type == SwfFile.TagMetadata);
                if (tag == null) return null;
                string text = SwfFile.ReadStringTag(tag.Body);
                return text.StartsWith(HudDesign.MetadataPrefix, StringComparison.Ordinal) ? HudDesign.FromJson(text.Substring(HudDesign.MetadataPrefix.Length)) : null;
            }
            catch
            {
                return null;
            }
        }

        /* As gfxexport writes it: version 0x401, no flags, DDS images, no prefix, the movie's name */
        private static byte[] ExporterInfo(string name)
        {
            List<byte> body = new List<byte>() { 0x01, 0x04, 0, 0, 0, 0, 0x0E, 0x00, 0 };
            byte[] text = Encoding.ASCII.GetBytes(name.Length > 200 ? name.Substring(0, 200) : name);
            body.Add((byte)text.Length);
            body.AddRange(text);
            body.Add(0);
            body.Add(0);
            return body.ToArray();
        }

        /// <summary>Writes the ActionScript block.</summary>
        private sealed class Writer
        {
            private readonly AbcFile _a = new AbcFile();
            private readonly AbcInstance _class;
            private readonly int _public, _as3, _publicSet, _as3Set, _index;

            //Scope depths as Flash writes a top-level class extending MovieClip: the script runs with 1 scope and pushes 8
            //(global, Object ... MovieClip) to make the class; the class's static code starts at 9, its methods at 10
            private const int ScriptScope = 1, StaticScope = 9, InstanceScope = 10;

            public Writer()
            {
                _public = _a.Namespace(AbcNamespace.Package, "");
                _as3 = _a.Namespace(AbcNamespace.Namespace, "http://adobe.com/AS3/2006/builtin");
                _publicSet = _a.NsSet(_public);
                _as3Set = _a.NsSet(_public, _as3);
                _index = _a.MultinameL(_publicSet);
                _class = new AbcInstance()
                {
                    Name = _a.QName(_public, DocumentClass),
                    Super = _a.Pkg("flash.display", "MovieClip"),
                    Flags = AbcInstance.ProtectedNsFlag,
                    ProtectedNs = _a.Namespace(AbcNamespace.Protected, DocumentClass),
                };
                foreach (string slot in new[] { "ocS", "ocK", "ocP", "ocV", "ocBuilt" })
                    _class.Traits.Add(new AbcTrait() { Name = _a.Pub(slot), Kind = AbcTrait.Slot });
            }

            private int Cls(string package, string name) => _a.Pkg(package, name);

            /* A method: a body that starts with 'this' as its scope, its stack and scope limits measured from its code */
            private int Method(string name, int parameters, int locals, Action<AbcCode> write, bool trait = true, int initScope = InstanceScope, bool pushThis = true, Action<AbcMethod> info = null)
            {
                AbcMethod method = new AbcMethod() { Name = _a.String(name), ReturnType = 0 };
                for (int i = 0; i < parameters; i++) method.ParamTypes.Add(0);
                info?.Invoke(method);
                _a.Methods.Add(method);
                int index = _a.Methods.Count - 1;
                AbcCode c = new AbcCode(_a);
                if (pushThis)
                    c.GetLocal(0).PushScope();
                write(c);
                AbcBody body = new AbcBody() { Method = index, LocalCount = Math.Max(locals, parameters + 1), InitScope = initScope, MaxScope = initScope, Code = c.ToArray() };
                body.Exceptions.AddRange(c.Handlers());
                List<string> faults = HudMovieCheck.Measure(_a, body, out int maxStack, out int maxScope);
                if (faults.Count != 0)
                    throw new InvalidOperationException("the overlay's code for " + name + " is wrong: " + string.Join("; ", faults));
                body.MaxStack = Math.Max(1, maxStack);
                body.MaxScope = initScope + Math.Max(1, maxScope);
                _a.Bodies.Add(body);
                if (trait)
                    _class.Traits.Add(new AbcTrait() { Name = _a.Pub(name), Kind = AbcTrait.Method, B = index });
                return index;
            }

            /// <summary>The fixed part: constructor, Init, hudSet, hudShow, the value code and the drawing helpers.</summary>
            public void Fixed()
            {
                int at = _index;

                //The constructor: only super() - nothing that can fail while the game makes the root
                _class.IInit = Method(DocumentClass, 0, 1, c => c.GetLocal(0).ConstructSuper(0).Ret(), trait: false);

                //Init(layer): the game calls it each time the layer loads
                Method(InitMethod, 1, 2, c => c.GetLocal(0).CallVoid("ocBuild", 0).GetLocal(0).CallVoid("ocLayout", 0).Ret());

                //ocLayout(): the 1280x720 design scaled to fit the screen and centred, whatever the layer's view mode (other layers
                //start stretched; ChallengeMapOverlay leaves the stage unscaled): the stage set unscaled and top-left, the root scaled
                Method("ocLayout", 0, 6, c => c
                    .Target("try")
                    .GetLocal(0).Get("stage").SetLocal(1)
                    .GetLocal(1).IfFalse("done")
                    .GetLocal(1).Str("noScale").Set("scaleMode")
                    .GetLocal(1).Str("TL").Set("align")
                    .GetLocal(1).Get("stageWidth").ToNumber().SetLocal(2)
                    .GetLocal(1).Get("stageHeight").ToNumber().SetLocal(3)
                    .GetLocal(2).Int(0).IfNGt("done")
                    .GetLocal(3).Int(0).IfNGt("done")
                    .GetLocal(2).Num(StageWidth).Div().SetLocal(4)
                    .GetLocal(3).Num(StageHeight).Div().SetLocal(5)
                    .GetLocal(5).GetLocal(4).IfNLt("keep")
                    .GetLocal(5).SetLocal(4)
                    .Target("keep")
                    .GetLocal(0).GetLocal(4).Set("scaleX")
                    .GetLocal(0).GetLocal(4).Set("scaleY")
                    .GetLocal(0).GetLocal(2).Num(StageWidth).GetLocal(4).Mul().Sub().Int(2).Div().Set("x")
                    .GetLocal(0).GetLocal(3).Num(StageHeight).GetLocal(4).Mul().Sub().Int(2).Div().Set("y")
                    .Target("done")
                    .Ret()
                    .Target("caught").Pop().Ret()
                    .Try("try", "done", "caught"));

                //What the game calls on the ChallengeMapOverlay layer's movie: resizeListener re-lays out, the rest are no-ops
                Method("resizeListener", 0, 1, c => c.GetLocal(0).CallVoid("ocLayout", 0).Ret());
                Method("init_image_substitution", 3, 4, c => c.Ret());
                Method("refresh_leaderboard_timer", 1, 2, c => c.Ret());
                foreach (string pressed in new[] { "pressed_changed_filter", "pressed_view_player_info", "pressed_continue_use", "pressed_continue" })
                    Method(pressed, 0, 1, c => c.Ret());

                //hudSet(slot, value)
                Method(SetMethod, 2, 3, c => c
                    .GetLocal(0).Get("ocBuilt").IfTrue("built")
                    .GetLocal(0).CallVoid("ocBuild", 0)
                    .GetLocal(0).CallVoid("ocLayout", 0)
                    .Target("built")
                    .GetLocal(1).ToInt().SetLocal(1)
                    .GetLocal(1).Int(0).IfLt("out")
                    .GetLocal(1).GetLocal(0).Get("ocK").Get("length").IfGe("out")
                    .Target("try")
                    .GetLocal(0).GetLocal(1).GetLocal(2).ToNumber().CallVoid("ocValue", 2)
                    .Target("tried")
                    .Target("out").Ret()
                    .Target("caught").Pop().Ret()
                    .Try("try", "tried", "caught"));

                //hudShow(slot, on): slot -1 is the whole overlay
                Method(ShowMethod, 2, 3, c => c
                    .GetLocal(0).Get("ocBuilt").IfTrue("built")
                    .GetLocal(0).CallVoid("ocBuild", 0)
                    .GetLocal(0).CallVoid("ocLayout", 0)
                    .Target("built")
                    .GetLocal(1).ToInt().SetLocal(1)
                    .GetLocal(2).ToInt().Int(0).EqualsOp().Not().SetLocal(2)
                    .GetLocal(1).Int(0).IfGe("slot")
                    .GetLocal(0).GetLocal(2).Set("visible").Ret()
                    .Target("slot")
                    .GetLocal(1).GetLocal(0).Get("ocK").Get("length").IfGe("out")
                    .GetLocal(0).Get("ocS").GetLocal(1).GetMn(at).GetLocal(2).Set("visible")
                    .Target("out").Ret());

                //ocAdd(index, kind, object, params, alpha, visible, initial): keep a slot, then show its first value
                Method("ocAdd", 7, 8, c => c
                    .GetLocal(3).GetLocal(5).Set("alpha")
                    .GetLocal(3).GetLocal(6).Set("visible")
                    .GetLocal(0).Get("ocS").GetLocal(1).GetLocal(3).SetMn(at)
                    .GetLocal(0).Get("ocK").GetLocal(1).GetLocal(2).SetMn(at)
                    .GetLocal(0).Get("ocP").GetLocal(1).GetLocal(4).SetMn(at)
                    .GetLocal(0).Get("ocV").GetLocal(1).Int(0).SetMn(at)
                    .GetLocal(0).GetLocal(1).GetLocal(7).ToNumber().CallVoid("ocValue", 2)
                    .Ret());

                //ocValue(index, value): locals 3 kind, 4 object, 5 params, 6-9 working values
                int toFixed = _a.MultinameIn("toFixed", _as3Set);
                Method("ocValue", 2, 10, c =>
                {
                    c.GetLocal(0).Get("ocK").GetLocal(1).GetMn(at).ToInt().SetLocal(3)
                     .GetLocal(0).Get("ocS").GetLocal(1).GetMn(at).SetLocal(4)
                     .GetLocal(0).Get("ocP").GetLocal(1).GetMn(at).SetLocal(5)
                     .GetLocal(3).Switch("end", "text", "number", "timer", "bar");

                    //text: the line the value picks, kept within the lines there are
                    c.Target("text")
                     .GetLocal(2).ToInt().SetLocal(6)
                     .GetLocal(6).GetLocal(5).Get("length").IfLt("t1")
                     .GetLocal(5).Get("length").Int(1).Sub().ToInt().SetLocal(6)
                     .Target("t1")
                     .GetLocal(6).Int(0).IfGe("t2")
                     .Int(0).SetLocal(6)
                     .Target("t2")
                     .GetLocal(4).GetLocal(5).GetLocal(6).GetMn(at).ToStringOp().Set("text")
                     .Ret();

                    //number: prefix + the value to its decimals + suffix
                    c.Target("number")
                     .GetLocal(4)
                     .GetLocal(5).Int(0).GetMn(at).ToStringOp()
                     .GetLocal(2).ToNumber().GetLocal(5).Int(2).GetMn(at).ToInt().CallMn(toFixed, 1).ToStringOp().Add()
                     .GetLocal(5).Int(1).GetMn(at).ToStringOp().Add()
                     .Set("text")
                     .Ret();

                    //timer: prefix + m:ss[.t] + suffix (nothing below 0)
                    c.Target("timer")
                     .GetLocal(2).ToNumber().SetLocal(6)
                     .GetLocal(6).Int(0).IfGt("tpos")
                     .Int(0).ToNumber().SetLocal(6)
                     .Target("tpos")
                     .GetLocal(6).Int(60).Div().ToInt().SetLocal(7)
                     .GetLocal(6).GetLocal(7).Int(60).Mul().Sub().ToInt().SetLocal(8)
                     .GetLocal(4)
                     .GetLocal(5).Int(0).GetMn(at).ToStringOp()
                     .GetLocal(7).ToStringOp().Add()
                     .Str(":").Add()
                     .GetLocal(8).Int(10).IfGe("nopad")
                     .Str("0").Add()
                     .Target("nopad")
                     .GetLocal(8).ToStringOp().Add()
                     .GetLocal(5).Int(2).GetMn(at).IfFalse("notenths")
                     .Str(".").Add()
                     .GetLocal(6).Int(10).Mul().ToInt().Int(10).Mod().ToInt().ToStringOp().Add()
                     .Target("notenths")
                     .GetLocal(5).Int(1).GetMn(at).ToStringOp().Add()
                     .Set("text")
                     .Ret();

                    //bar: value / max (max 0: the largest value seen so far), the low fill at or below the mark
                    c.Target("bar")
                     .GetLocal(5).Int(0).GetMn(at).ToNumber().SetLocal(6)
                     .GetLocal(6).Int(0).IfGt("havemax")
                     .GetLocal(2).GetLocal(0).Get("ocV").GetLocal(1).GetMn(at).IfNGt("nomax")
                     .GetLocal(0).Get("ocV").GetLocal(1).GetLocal(2).SetMn(at)
                     .Target("nomax")
                     .GetLocal(0).Get("ocV").GetLocal(1).GetMn(at).ToNumber().SetLocal(6)
                     .Target("havemax")
                     .Int(0).ToNumber().SetLocal(7)
                     .GetLocal(6).Int(0).IfNGt("fraction")
                     .GetLocal(2).GetLocal(6).Div().SetLocal(7)
                     .Target("fraction")
                     .GetLocal(7).Int(1).IfNGt("under")
                     .Int(1).ToNumber().SetLocal(7)
                     .Target("under")
                     .GetLocal(7).Num(0.001).IfGe("over")
                     .Num(0.001).SetLocal(7)
                     .Target("over")
                     .GetLocal(4).Int(1).Call("getChildAt", 1).SetLocal(8)
                     .GetLocal(4).Int(2).Call("getChildAt", 1).SetLocal(9)
                     .GetLocal(5).Int(2).GetMn(at).IfTrue("vertical")
                     .GetLocal(8).GetLocal(7).Set("scaleX")
                     .GetLocal(9).GetLocal(7).Set("scaleX")
                     .Jump("low")
                     .Target("vertical")
                     .GetLocal(8).GetLocal(7).Set("scaleY")
                     .GetLocal(9).GetLocal(7).Set("scaleY")
                     .Target("low")
                     .GetLocal(7).GetLocal(5).Int(1).GetMn(at).ToNumber().LessEquals().SetLocal(6)
                     .GetLocal(9).GetLocal(6).Set("visible")
                     .GetLocal(8).GetLocal(6).Not().Set("visible")
                     .Ret();

                    c.Target("end").Ret();
                });

                //Drawing helpers
                int sprite = Cls("flash.display", "Sprite"), shape = Cls("flash.display", "Shape");
                int textField = Cls("flash.text", "TextField"), textFormat = Cls("flash.text", "TextFormat"), glow = Cls("flash.filters", "GlowFilter");

                //ocSprite(parent, x, y), ocShape(parent, x, y): a new child at x, y
                Method("ocSprite", 3, 5, c => c
                    .FindPropStrict(sprite).ConstructProp(sprite, 0).SetLocal(4)
                    .GetLocal(4).GetLocal(2).Set("x")
                    .GetLocal(4).GetLocal(3).Set("y")
                    .GetLocal(4).False().Set("mouseEnabled")
                    .GetLocal(4).False().Set("mouseChildren")
                    .GetLocal(1).GetLocal(4).CallVoid("addChild", 1)
                    .GetLocal(4).RetValue());
                Method("ocShape", 3, 5, c => c
                    .FindPropStrict(shape).ConstructProp(shape, 0).SetLocal(4)
                    .GetLocal(4).GetLocal(2).Set("x")
                    .GetLocal(4).GetLocal(3).Set("y")
                    .GetLocal(1).GetLocal(4).CallVoid("addChild", 1)
                    .GetLocal(4).RetValue());

                //ocFill(target, colour, alpha, x, y, w, h)
                Method("ocFill", 7, 9, c => c
                    .GetLocal(1).Get("graphics").SetLocal(8)
                    .GetLocal(8).GetLocal(2).GetLocal(3).CallVoid("beginFill", 2)
                    .GetLocal(8).GetLocal(4).GetLocal(5).GetLocal(6).GetLocal(7).CallVoid("drawRect", 4)
                    .GetLocal(8).CallVoid("endFill", 0)
                    .Ret());
                //ocStroke(target, thickness, colour, alpha)
                Method("ocStroke", 4, 5, c => c
                    .GetLocal(1).Get("graphics").GetLocal(2).GetLocal(3).GetLocal(4).CallVoid("lineStyle", 3)
                    .Ret());
                //ocSeg(target, x1, y1, x2, y2)
                Method("ocSeg", 5, 7, c => c
                    .GetLocal(1).Get("graphics").SetLocal(6)
                    .GetLocal(6).GetLocal(2).GetLocal(3).CallVoid("moveTo", 2)
                    .GetLocal(6).GetLocal(4).GetLocal(5).CallVoid("lineTo", 2)
                    .Ret());
                //ocCircle(target, cx, cy, r): an outline with the stroke set
                Method("ocCircle", 4, 5, c => c
                    .GetLocal(1).Get("graphics").GetLocal(2).GetLocal(3).GetLocal(4).CallVoid("drawCircle", 3)
                    .Ret());
                //ocDot(target, colour, alpha, cx, cy, r): a filled circle
                Method("ocDot", 6, 8, c => c
                    .GetLocal(1).Get("graphics").SetLocal(7)
                    .GetLocal(7).GetLocal(2).GetLocal(3).CallVoid("beginFill", 2)
                    .GetLocal(7).GetLocal(4).GetLocal(5).GetLocal(6).CallVoid("drawCircle", 3)
                    .GetLocal(7).CallVoid("endFill", 0)
                    .Ret());
                //ocOutline(target, x, y, w, h): a rectangle outline with the stroke set
                Method("ocOutline", 5, 6, c => c
                    .GetLocal(1).Get("graphics").GetLocal(2).GetLocal(3).GetLocal(4).GetLocal(5).CallVoid("drawRect", 4)
                    .Ret());

                //ocPlace(bitmap, x, y, w, h): a picture on the root, stretched to its box, smoothed
                Method("ocPlace", 5, 6, c => c
                    .GetLocal(1).GetLocal(2).Set("x")
                    .GetLocal(1).GetLocal(3).Set("y")
                    .GetLocal(1).GetLocal(4).Set("width")
                    .GetLocal(1).GetLocal(5).Set("height")
                    .GetLocal(1).True().Set("smoothing")
                    .GetLocal(0).GetLocal(1).CallVoid("addChild", 1)
                    .GetLocal(1).RetValue());

                //ocText(x, y, w, h, font, size, colour, align, shadow): a text field on the root
                Method("ocText", 9, 12, c => c
                    .FindPropStrict(textField).ConstructProp(textField, 0).SetLocal(10)
                    .GetLocal(10).GetLocal(1).Set("x")
                    .GetLocal(10).GetLocal(2).Set("y")
                    .GetLocal(10).GetLocal(3).Set("width")
                    .GetLocal(10).GetLocal(4).Set("height")
                    .GetLocal(10).False().Set("selectable")
                    .GetLocal(10).False().Set("mouseEnabled")
                    .GetLocal(10).True().Set("embedFonts")
                    .FindPropStrict(textFormat).GetLocal(5).GetLocal(6).GetLocal(7).ConstructProp(textFormat, 3).SetLocal(11)
                    .GetLocal(11).GetLocal(8).Set("align")
                    .GetLocal(10).GetLocal(11).Set("defaultTextFormat")
                    .GetLocal(9).IfFalse("plain")
                    .GetLocal(10).FindPropStrict(glow).Int(0).Num(0.85).Int(3).Int(3).Int(4).ConstructProp(glow, 5).NewArray(1).Set("filters")
                    .Target("plain")
                    .GetLocal(10).Str("").Set("text")
                    .GetLocal(0).GetLocal(10).CallVoid("addChild", 1)
                    .GetLocal(10).RetValue());
            }

            /// <summary>ocBuild(): the root set up and every slot made, in the design's order (slot i is index i).</summary>
            public void Slots(HudDesign design, Dictionary<HudSlot, string> imageClasses)
            {
                _imageClasses = imageClasses;
                Method("ocBuild", 0, 3, c =>
                {
                    c.GetLocal(0).Get("ocBuilt").IfFalse("go").Ret()
                     .Target("go")
                     .GetLocal(0).True().Set("ocBuilt");
                    foreach (string list in new[] { "ocS", "ocK", "ocP", "ocV" })
                        c.GetLocal(0).NewArray(0).Set(list);
                    c.GetLocal(0).False().Set("mouseEnabled")
                     .GetLocal(0).False().Set("mouseChildren")
                     .GetLocal(0).Bool(design.ShowAtStart).Set("visible");
                    //Each slot on its own: one that fails (a font, a filter the player lacks) leaves the others
                    for (int i = 0; i < design.Slots.Count; i++)
                    {
                        c.Target("s" + i);
                        Slot(c, i, design.Slots[i]);
                        c.Target("e" + i).Jump("n" + i)
                         .Target("h" + i).Pop().GetLocal(0).PushScope()
                         .Target("n" + i)
                         .Try("s" + i, "e" + i, "h" + i);
                    }
                    c.Ret();
                });
            }

            private Dictionary<HudSlot, string> _imageClasses = new Dictionary<HudSlot, string>();

            private void Slot(AbcCode c, int index, HudSlot slot)
            {
                int colour = HudDesign.Rgb(slot.Colour);
                double w = slot.Width, h = slot.Height;
                switch (slot.Kind)
                {
                    case HudSlotKind.Image:
                        {
                            //new Bitmap(new OcImg<n>(0, 0)) placed in the slot's box
                            int bitmap = Cls("flash.display", "Bitmap"), picture = _a.QName(_public, _imageClasses[slot]);
                            c.GetLocal(0)
                             .FindPropStrict(bitmap).FindPropStrict(picture).Int(0).Int(0).ConstructProp(picture, 2).ConstructProp(bitmap, 1)
                             .Num(slot.X).Num(slot.Y).Num(w).Num(h).Call("ocPlace", 5).SetLocal(1);
                            Add(c, index, KindShape, slot, () => 0);
                            break;
                        }
                    case HudSlotKind.Text:
                    case HudSlotKind.Number:
                    case HudSlotKind.Timer:
                        {
                            c.GetLocal(0).Num(slot.X).Num(slot.Y).Num(w).Num(h).Str(slot.Font).Num(slot.Size).Int(colour).Str(slot.Align).Bool(slot.Shadow)
                             .Call("ocText", 9).SetLocal(1);
                            if (slot.Kind == HudSlotKind.Text)
                                Add(c, index, KindText, slot, () => { foreach (string line in slot.Lines) c.Str(line); return slot.Lines.Count; });
                            else if (slot.Kind == HudSlotKind.Number)
                                Add(c, index, KindNumber, slot, () => { c.Str(slot.Prefix ?? "").Str(slot.Suffix ?? "").Int(slot.Decimals); return 3; });
                            else
                                Add(c, index, KindTimer, slot, () => { c.Str(slot.Prefix ?? "").Str(slot.Suffix ?? "").Bool(slot.Tenths); return 3; });
                            break;
                        }
                    case HudSlotKind.Bar:
                        {
                            //The fill shrinks towards the side it grows from: drawn from that edge, placed at it
                            double ox = 0, oy = 0, rx = 0, ry = 0;
                            bool vertical = slot.Direction == "up" || slot.Direction == "down";
                            if (slot.Direction == "left") { ox = w; rx = -w; }
                            if (slot.Direction == "up") { oy = h; ry = -h; }
                            int low = string.IsNullOrEmpty(slot.LowColour) ? colour : HudDesign.Rgb(slot.LowColour);
                            c.GetLocal(0).GetLocal(0).Num(slot.X).Num(slot.Y).Call("ocSprite", 3).SetLocal(1);
                            NewShape(c, 0, 0).GetLocal(0).GetLocal(2).Int(HudDesign.Rgb(slot.BackColour)).Num(slot.BackAlpha).Num(0).Num(0).Num(w).Num(h).CallVoid("ocFill", 7);
                            NewShape(c, ox, oy).GetLocal(0).GetLocal(2).Int(colour).Num(1).Num(rx).Num(ry).Num(w).Num(h).CallVoid("ocFill", 7);
                            NewShape(c, ox, oy).GetLocal(0).GetLocal(2).Int(low).Num(1).Num(rx).Num(ry).Num(w).Num(h).CallVoid("ocFill", 7);
                            if (!string.IsNullOrEmpty(slot.BorderColour))
                            {
                                NewShape(c, 0, 0).GetLocal(0).GetLocal(2).Num(slot.BorderWidth).Int(HudDesign.Rgb(slot.BorderColour)).Num(1).CallVoid("ocStroke", 4);
                                c.GetLocal(0).GetLocal(2).Num(0).Num(0).Num(w).Num(h).CallVoid("ocOutline", 5);
                            }
                            Add(c, index, KindBar, slot, () => { c.Num(slot.Max).Num(slot.LowAt).Bool(vertical); return 3; });
                            break;
                        }
                    case HudSlotKind.Reticle:
                        {
                            c.GetLocal(0).GetLocal(0).Num(slot.X).Num(slot.Y).Call("ocShape", 3).SetLocal(1);
                            double cx = w / 2, cy = h / 2, r = Math.Min(w, h) / 2, gap = Math.Max(0, slot.Gap);
                            Action stroke = () => c.GetLocal(0).GetLocal(1).Num(slot.Thickness).Int(colour).Num(1).CallVoid("ocStroke", 4);
                            Action<double, double, double, double> seg = (x1, y1, x2, y2) => c.GetLocal(0).GetLocal(1).Num(x1).Num(y1).Num(x2).Num(y2).CallVoid("ocSeg", 5);
                            switch (slot.Style)
                            {
                                case "circle":
                                    stroke();
                                    c.GetLocal(0).GetLocal(1).Num(cx).Num(cy).Num(Math.Max(1, r - slot.Thickness / 2)).CallVoid("ocCircle", 4);
                                    break;
                                case "dot":
                                    c.GetLocal(0).GetLocal(1).Int(colour).Num(1).Num(cx).Num(cy).Num(Math.Max(1, r)).CallVoid("ocDot", 6);
                                    break;
                                case "brackets":
                                    {
                                        double arm = Math.Min(w, h) / 4;
                                        stroke();
                                        seg(0, arm, 0, 0); seg(0, 0, arm, 0);
                                        seg(w - arm, 0, w, 0); seg(w, 0, w, arm);
                                        seg(w, h - arm, w, h); seg(w, h, w - arm, h);
                                        seg(arm, h, 0, h); seg(0, h, 0, h - arm);
                                        break;
                                    }
                                default:
                                    stroke();
                                    seg(0, cy, Math.Max(0, cx - gap), cy); seg(Math.Min(w, cx + gap), cy, w, cy);
                                    seg(cx, 0, cx, Math.Max(0, cy - gap)); seg(cx, Math.Min(h, cy + gap), cx, h);
                                    break;
                            }
                            Add(c, index, KindShape, slot, () => 0);
                            break;
                        }
                    default: //box
                        {
                            c.GetLocal(0).GetLocal(0).Num(slot.X).Num(slot.Y).Call("ocShape", 3).SetLocal(1);
                            c.GetLocal(0).GetLocal(1).Int(colour).Num(1).Num(0).Num(0).Num(w).Num(h).CallVoid("ocFill", 7);
                            if (!string.IsNullOrEmpty(slot.BorderColour))
                            {
                                c.GetLocal(0).GetLocal(1).Num(slot.BorderWidth).Int(HudDesign.Rgb(slot.BorderColour)).Num(1).CallVoid("ocStroke", 4);
                                c.GetLocal(0).GetLocal(1).Num(0).Num(0).Num(w).Num(h).CallVoid("ocOutline", 5);
                            }
                            Add(c, index, KindShape, slot, () => 0);
                            break;
                        }
                }
            }

            /* A child shape of the slot's sprite (local 1) into local 2 */
            private static AbcCode NewShape(AbcCode c, double x, double y) => c.GetLocal(0).GetLocal(1).Num(x).Num(y).Call("ocShape", 3).SetLocal(2);

            /* ocAdd(index, kind, local 1, [params], alpha, visible, initial) */
            private static void Add(AbcCode c, int index, int kind, HudSlot slot, Func<int> pushParams)
            {
                c.GetLocal(0).Int(index).Int(kind).GetLocal(1);
                int count = pushParams();
                c.NewArray(count).Num(slot.Alpha).Bool(slot.Visible).Num(slot.Initial).CallVoid("ocAdd", 7);
            }

            /// <summary>The class's static code and the script that defines the class, then the block.</summary>
            public AbcFile Finish()
            {
                AbcClass statics = new AbcClass();
                int classIndex = _a.Instances.Count;
                _a.Instances.Add(_class);
                _a.Classes.Add(statics);
                statics.CInit = Method("", 0, 1, c => c.Ret(), trait: false, initScope: StaticScope);
                int movieClip = Cls("flash.display", "MovieClip");
                int script = Method("", 0, 1, c =>
                {
                    c.GetScopeObject(0);
                    foreach (int chain in new[] { _a.Pub("Object"), Cls("flash.events", "EventDispatcher"), Cls("flash.display", "DisplayObject"),
                        Cls("flash.display", "InteractiveObject"), Cls("flash.display", "DisplayObjectContainer"), Cls("flash.display", "Sprite"), movieClip })
                        c.GetLex(chain).PushScope();
                    c.GetLex(movieClip).NewClass(classIndex);
                    for (int i = 0; i < 7; i++) c.PopScope();
                    c.InitProperty(_class.Name).Ret();
                }, trait: false, initScope: ScriptScope);
                _a.Scripts.Add(new AbcScript() { Init = script, Traits = new List<AbcTrait>() { new AbcTrait() { Name = _class.Name, Kind = AbcTrait.Class, A = 1, B = classIndex } } });
                return _a;
            }

            /// <summary>
            /// A class for one picture, bound to its bitmap by the SymbolClass tag as Flash exports a library bitmap: extends BitmapData,
            /// its constructor (width:int = 0, height:int = 0) only calls super; its own script, scopes 1 / 4 / 5.
            /// </summary>
            public void ImageClass(string name)
            {
                int bitmapData = Cls("flash.display", "BitmapData");
                AbcInstance instance = new AbcInstance()
                {
                    Name = _a.QName(_public, name),
                    Super = bitmapData,
                    Flags = AbcInstance.ProtectedNsFlag,
                    ProtectedNs = _a.Namespace(AbcNamespace.Protected, name),
                };
                AbcClass statics = new AbcClass();
                int classIndex = _a.Instances.Count;
                _a.Instances.Add(instance);
                _a.Classes.Add(statics);
                statics.CInit = Method("", 0, 1, c => c.Ret(), trait: false, initScope: 4);
                int intType = _a.Pub("int"), zero = _a.Int(0);
                instance.IInit = Method(name, 2, 3, c => c.GetLocal(0).GetLocal(1).GetLocal(2).ConstructSuper(2).Ret(), trait: false, initScope: 5,
                    info: m =>
                    {
                        m.ParamTypes[0] = intType; m.ParamTypes[1] = intType;
                        m.Flags |= AbcMethod.HasOptional;
                        m.Options.Add(new AbcOption() { Value = zero, Kind = 0x03 });
                        m.Options.Add(new AbcOption() { Value = zero, Kind = 0x03 });
                    });
                int script = Method("", 0, 1, c =>
                {
                    c.GetScopeObject(0).GetLex(_a.Pub("Object")).PushScope().GetLex(bitmapData).PushScope();
                    c.GetLex(bitmapData).NewClass(classIndex).PopScope().PopScope();
                    c.InitProperty(instance.Name).Ret();
                }, trait: false, initScope: ScriptScope);
                _a.Scripts.Add(new AbcScript() { Init = script, Traits = new List<AbcTrait>() { new AbcTrait() { Name = instance.Name, Kind = AbcTrait.Class, A = 1, B = classIndex } } });
            }
        }
    }
}
