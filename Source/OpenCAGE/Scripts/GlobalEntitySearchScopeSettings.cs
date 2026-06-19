using OpenCAGE;
using OpenCAGE.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCAGE.Scripts
{
    public enum GlobalEntitySearchScope
    {
        AllComposites = 0,
        CurrentComposite = 1,
        CurrentCompositeAndNested = 2,
    }

    public static class GlobalEntitySearchScopeSettings
    {
        private static readonly GlobalEntitySearchScope DefaultScope = GlobalEntitySearchScope.CurrentCompositeAndNested;
        private static readonly List<Action> _scopeChangedHandlers = new List<Action>();

        public static GlobalEntitySearchScope Scope
        {
            get => (GlobalEntitySearchScope)SettingsManager.GetInteger(
                Settings.GlobalEntitySearchScope,
                (int)DefaultScope);
            set
            {
                int stored = (int)value;
                if (SettingsManager.GetInteger(Settings.GlobalEntitySearchScope, (int)DefaultScope) == stored)
                    return;

                SettingsManager.SetInteger(Settings.GlobalEntitySearchScope, stored);
                NotifyScopeChanged();
            }
        }

        public static void AddScopeChangedHandler(Action handler)
        {
            if (handler != null && !_scopeChangedHandlers.Contains(handler))
                _scopeChangedHandlers.Add(handler);
        }

        public static void RemoveScopeChangedHandler(Action handler)
        {
            if (handler != null)
                _scopeChangedHandlers.Remove(handler);
        }

        public static void BindSettingsButton(Button button)
        {
            if (button == null)
                return;

            SetIconButtonImage(button, Resources.cog);

            ContextMenuStrip menu = BuildScopeMenu();
            button.Click += (sender, e) =>
            {
                UpdateMenuChecks(menu);
                menu.Show(button, new Point(0, button.Height));
            };
        }

        private static ContextMenuStrip BuildScopeMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            menu.Items.Add(CreateScopeMenuItem("Search All Composites", GlobalEntitySearchScope.AllComposites, menu));
            menu.Items.Add(CreateScopeMenuItem("Search Within Current Composite", GlobalEntitySearchScope.CurrentComposite, menu));
            menu.Items.Add(CreateScopeMenuItem("Search Within Current Composite And Nested", GlobalEntitySearchScope.CurrentCompositeAndNested, menu));

            UpdateMenuChecks(menu);
            return menu;
        }

        private static ToolStripMenuItem CreateScopeMenuItem(string text, GlobalEntitySearchScope scope, ContextMenuStrip menu)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text)
            {
                CheckOnClick = true,
                Tag = scope,
            };

            item.Click += (sender, e) =>
            {
                Scope = scope;
                UpdateMenuChecks(menu);
            };

            return item;
        }

        private static void UpdateMenuChecks(ContextMenuStrip menu)
        {
            GlobalEntitySearchScope activeScope = Scope;
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is GlobalEntitySearchScope scope)
                    menuItem.Checked = scope == activeScope;
            }
        }

        private static void NotifyScopeChanged()
        {
            Action[] handlers = _scopeChangedHandlers.ToArray();
            foreach (Action handler in handlers)
                handler?.Invoke();
        }

        public static void SetIconButtonImage(Button button, Icon icon)
        {
            if (button == null || icon == null)
                return;

            int availableWidth = button.ClientSize.Width > 0 ? button.ClientSize.Width : button.Width;
            int availableHeight = button.ClientSize.Height > 0 ? button.ClientSize.Height : button.Height;
            int targetSize = Math.Max(12, Math.Min(availableWidth, availableHeight) - 4);

            Image previousImage = button.Image;
            Bitmap scaledImage = new Bitmap(targetSize, targetSize);
            using (Graphics graphics = Graphics.FromImage(scaledImage))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawIcon(icon, new Rectangle(0, 0, targetSize, targetSize));
            }

            button.Image = scaledImage;
            previousImage?.Dispose();
            button.ImageAlign = ContentAlignment.MiddleCenter;
            button.Text = string.Empty;
        }
    }
}
