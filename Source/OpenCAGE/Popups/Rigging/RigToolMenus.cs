using CathodeLib;
using System;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.Rigging
{
    /// <summary>
    /// The rig tools' way into the Animation Editor: a right-click menu on its list of animation sets (one line in
    /// EditAnimations, so the window itself stays as it is).
    /// </summary>
    public static class RigToolMenus
    {
        /// <param name="selected">The set the editor has selected (null when none).</param>
        /// <param name="refresh">Rebuild the editor's lists after something changed (a new context).</param>
        public static void AttachSetMenu(Form owner, ListView setList, Func<CathodeLib.Animation.AnimationSet> selected, Action refresh)
        {
            ContextMenuStrip menu = setList.ContextMenuStrip ?? new ContextMenuStrip();
            if (menu.Items.Count != 0) menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem displayRig = new ToolStripMenuItem("Create display rig from this set's rig...");
            displayRig.ToolTipText = "A smaller, bigger or differently proportioned copy of the rig for a character to be drawn on while it plays this rig's animations.";
            displayRig.Click += (s, e) =>
            {
                string rig = null;
                CathodeLib.Animation.AnimationSet set = selected();
                if (set != null && Singleton.Animations != null)
                    rig = AnimationImport.DefaultRigFor(set);
                using (DisplayRigDialog dialog = new DisplayRigDialog(rig))
                    dialog.ShowDialog(owner);
            };
            menu.Items.Add(displayRig);

            ToolStripMenuItem context = new ToolStripMenuItem("New context in this set...");
            context.ToolTipText = "A named group of clip lines the game swaps in while a character is in that state (as WEAPON_HANDGUN), empty or copied from one of the set's contexts.";
            context.Click += (s, e) =>
            {
                CathodeLib.Animation.AnimationSet set = selected();
                if (set == null) return;
                if (NewContext(owner, set)) refresh?.Invoke();
            };
            menu.Items.Add(context);

            /* A context's lines are the slots the animation trees ask for by name (held_weapon_layer_idle, aim_centre...), so a context
             * is filled by pointing its lines at clips the set holds. Script commands only reach the set's own lines, never a context's. */
            ToolStripMenuItem pointLines = new ToolStripMenuItem("Point a context's lines at clips");
            pointLines.ToolTipText = "Choose which clip of this set one of a context's lines plays while a character is in that context.";
            pointLines.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
            menu.Items.Add(pointLines);

            ToolStripMenuItem prop = new ToolStripMenuItem("Import animated prop...");
            prop.ToolTipText = "A hatch, fan or machine arm from an FBX/glTF whose parts are animated: a rig and set of its own, and a composite in the open level that plays it.";
            prop.Click += (s, e) => { using (PropImportDialog dialog = new PropImportDialog()) dialog.ShowDialog(owner); refresh?.Invoke(); };
            menu.Items.Add(prop);

            menu.Opening += (s, e) =>
            {
                displayRig.Enabled = Singleton.AnimationsLoaded && RebindImport_HasRigs();
                context.Enabled = Singleton.AnimationsLoaded && selected()?.Database != null;
                CathodeLib.Animation.AnimationSet set = selected();
                pointLines.DropDownItems.Clear();
                foreach (CathodeLib.Animation.AnimationContext c in set == null ? Enumerable.Empty<CathodeLib.Animation.AnimationContext>() : set.Contexts.Where(c => !string.IsNullOrWhiteSpace(c.Name)).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                {
                    string contextName = c.Name;
                    pointLines.DropDownItems.Add(new ToolStripMenuItem(contextName + " (" + c.Clips.Count + ")", null, (s2, e2) => { if (PointLines(owner, set, contextName)) refresh?.Invoke(); }));
                }
                if (pointLines.DropDownItems.Count == 0) pointLines.DropDownItems.Add(new ToolStripMenuItem("(no named contexts)") { Enabled = false });
                pointLines.Enabled = Singleton.AnimationsLoaded && set?.Database != null;
            };
            setList.ContextMenuStrip = menu;
        }

        /* The new context: a name and what to copy, then written at once (ANIMATION.PAK is the game's, not the level's) */
        private static bool NewContext(Form owner, CathodeLib.Animation.AnimationSet set)
        {
            using (Form form = new Form() { Text = "New context in " + set.Name, Icon = SharedFormIcon.Icon, StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
                TextBox name = new TextBox() { Width = 260, CharacterCasing = CharacterCasing.Upper };
                ComboBox template = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
                template.Items.Add("(empty)");
                template.Items.Add("(the set's own lines)");
                foreach (var c in set.Database.Contexts.Where(c => c.Name.Length != 0)) template.Items.Add(c.Name);
                template.SelectedIndex = 0;
                Label status = new Label() { AutoSize = true, MaximumSize = new System.Drawing.Size(420, 0), ForeColor = System.Drawing.Color.Firebrick };
                Label memory = new Label() { AutoSize = true, MaximumSize = new System.Drawing.Size(420, 0) };
                try
                {
                    AnimationHeapBudget.Estimate estimate = AnimationHeapBudget.ForSets(Singleton.Animations, new[] { set.Name });
                    memory.Text = set.Name + " keeps " + AnimationHeapBudget.Megabytes(estimate.Bytes) + " of clips resident (the game's resident animation memory is " + AnimationHeapBudget.Megabytes(AnimationHeapBudget.Budget(false))
                        + ", shared by every set in use). Lines copied from another context add nothing to it; clips imported into the context are streamed.";
                }
                catch { }
                Button ok = new Button() { Text = "Create", AutoSize = true }, cancel = new Button() { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
                root.Controls.Add(new Label() { Text = "Name (e.g. WEAPON_FLARE):", AutoSize = true });
                root.Controls.Add(name);
                root.Controls.Add(new Label() { Text = "Start as a copy of:", AutoSize = true });
                root.Controls.Add(template);
                root.Controls.Add(memory);
                root.Controls.Add(status);
                FlowLayoutPanel buttons = new FlowLayoutPanel() { AutoSize = true };
                buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
                root.Controls.Add(buttons);
                form.Controls.Add(root);
                form.AcceptButton = ok; form.CancelButton = cancel;
                bool made = false;
                ok.Click += (s, e) =>
                {
                    string problem = OpenCAGE.Rigging.AnimationContexts.NameProblem(set, name.Text.Trim().ToUpperInvariant())
                        ?? AnimationPakWrite.Stale(Singleton.Animations)
                        ?? (EditorUtils.ThisInstallsGameRunning() ? "Alien: Isolation is running and holds ANIMATION.PAK open: close it first." : null);
                    if (problem != null) { status.Text = problem; return; }
                    string from = template.SelectedIndex == 0 ? null : template.SelectedIndex == 1 ? "" : (string)template.SelectedItem;
                    var result = OpenCAGE.Rigging.AnimationContexts.Create(Singleton.Animations, set, name.Text, from);
                    if (!result.Ok) { status.Text = result.Problem; return; }
                    string saved = AnimationPakWrite.SaveWhole(Singleton.Animations, form);
                    if (saved != null) { status.Text = "The context was made but ANIMATION.PAK was not written: " + saved; return; }
                    AnimationHeapBudget.Invalidate();
                    Singleton.OnAnimationsModified?.Invoke();
                    made = true;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };
                OpenCAGE.Theming.ThemeManager.ApplyToForm(form);
                form.ShowDialog(owner);
                return made;
            }
        }

        /* One line of a context pointed at a clip of the set (the line added when the context has none of that name), then written at
         * once. The lines the context already has are offered first: they are the slots its trees ask for. */
        private static bool PointLines(Form owner, CathodeLib.Animation.AnimationSet set, string contextName)
        {
            CathodeLib.Animation.AnimationContext context = set.Contexts.FirstOrDefault(c => string.Equals(c.Name, contextName, StringComparison.OrdinalIgnoreCase));
            if (context == null) return false;
            using (Form form = new Form() { Text = contextName + " in " + set.Name + ": point a line", Icon = SharedFormIcon.Icon, StartPosition = FormStartPosition.CenterParent, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false })
            {
                FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
                ComboBox line = new ComboBox() { Width = 320, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
                foreach (string n in context.Clips.Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) line.Items.Add(n);
                ComboBox clip = new ComboBox() { Width = 320, DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
                foreach (string n in set.Contexts.Where(c => string.IsNullOrWhiteSpace(c.Name)).SelectMany(c => c.Clips).Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)) clip.Items.Add(n);
                Label now = new Label() { AutoSize = true, MaximumSize = new System.Drawing.Size(420, 0), ForeColor = System.Drawing.SystemColors.GrayText };
                Label status = new Label() { AutoSize = true, MaximumSize = new System.Drawing.Size(420, 0), ForeColor = System.Drawing.Color.Firebrick };
                line.TextChanged += (s, e) =>
                {
                    CathodeLib.Animation.ClipReference had = context.Clips.FirstOrDefault(c => string.Equals(c.Name, line.Text.Trim(), StringComparison.OrdinalIgnoreCase));
                    now.Text = had == null ? "A new line: the trees only ask for slots they know, so match one of theirs." : "Plays " + had.Path + " now.";
                };
                Button ok = new Button() { Text = "Point it", AutoSize = true }, close = new Button() { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
                root.Controls.Add(new Label() { Text = "Line (the slot the animation trees ask for):", AutoSize = true });
                root.Controls.Add(line);
                root.Controls.Add(now);
                root.Controls.Add(new Label() { Text = "Clip of " + set.Name + " to play there (import new ones with Import...):", AutoSize = true });
                root.Controls.Add(clip);
                root.Controls.Add(new Label() { AutoSize = true, MaximumSize = new System.Drawing.Size(420, 0), Text = "The trees don't stream clips in: a line they play should point at a resident clip. Imported clips are streamed, which a script command (CMD_PlayAnimation) plays from the set's own lines." });
                root.Controls.Add(status);
                FlowLayoutPanel buttons = new FlowLayoutPanel() { AutoSize = true };
                buttons.Controls.Add(ok); buttons.Controls.Add(close);
                root.Controls.Add(buttons);
                form.Controls.Add(root);
                form.AcceptButton = ok; form.CancelButton = close;
                bool changed = false;
                ok.Click += (s, e) =>
                {
                    string problem = (line.Text.Trim().Length == 0 ? "Name the line." : null)
                        ?? AnimationPakWrite.Stale(Singleton.Animations)
                        ?? (EditorUtils.ThisInstallsGameRunning() ? "Alien: Isolation is running and holds ANIMATION.PAK open: close it first." : null)
                        ?? OpenCAGE.Rigging.AnimationContexts.SetLine(set, contextName, line.Text.Trim(), clip.Text.Trim());
                    if (problem != null) { status.Text = problem; return; }
                    string saved = AnimationPakWrite.SaveWhole(Singleton.Animations, form);
                    if (saved != null) { status.Text = "The line was pointed but ANIMATION.PAK was not written: " + saved; return; }
                    Singleton.OnAnimationsModified?.Invoke();
                    changed = true;
                    status.ForeColor = System.Drawing.Color.SeaGreen;
                    status.Text = line.Text.Trim() + " now plays " + clip.Text.Trim() + " in " + contextName + ".";
                };
                OpenCAGE.Theming.ThemeManager.ApplyToForm(form);
                form.ShowDialog(owner);
                return changed;
            }
        }
        private static bool RebindImport_HasRigs() => OpenCAGE.Rigging.RebindImport.CharacterRigs(Singleton.Animations).Count != 0;
    }
}
