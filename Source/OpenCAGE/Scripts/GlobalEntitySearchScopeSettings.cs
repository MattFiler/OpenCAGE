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

    public static class GlobalEntitySearchScopeExtensions
    {
        /// <summary>
        /// Human-readable name for a scope, suitable for window/title bars (e.g. "Current Composite And Nested").
        /// </summary>
        public static string ToDisplayName(this GlobalEntitySearchScope scope)
        {
            switch (scope)
            {
                case GlobalEntitySearchScope.AllComposites:
                    return "All Composites";
                case GlobalEntitySearchScope.CurrentComposite:
                    return "Current Composite";
                case GlobalEntitySearchScope.CurrentCompositeAndNested:
                    return "Current Composite And Nested";
                default:
                    return scope.ToString();
            }
        }
    }

    /// <summary>
    /// Reusable controller for an entity search scope preference (All / Current / Current And Nested).
    /// Each instance is backed by its own settings key so independent features (global search,
    /// find references) can remember their own scope while sharing identical UI and semantics.
    /// </summary>
    public class EntitySearchScopeController
    {
        private readonly string _settingKey;
        private readonly GlobalEntitySearchScope _defaultScope;
        private readonly List<Action> _scopeChangedHandlers = new List<Action>();

        public EntitySearchScopeController(string settingKey, GlobalEntitySearchScope defaultScope)
        {
            _settingKey = settingKey;
            _defaultScope = defaultScope;
        }

        public GlobalEntitySearchScope Scope
        {
            get => (GlobalEntitySearchScope)SettingsManager.GetInteger(_settingKey, (int)_defaultScope);
            set
            {
                int stored = (int)value;
                if (SettingsManager.GetInteger(_settingKey, (int)_defaultScope) == stored)
                    return;

                SettingsManager.SetInteger(_settingKey, stored);
                NotifyScopeChanged();
            }
        }

        public void AddScopeChangedHandler(Action handler)
        {
            if (handler != null && !_scopeChangedHandlers.Contains(handler))
                _scopeChangedHandlers.Add(handler);
        }

        public void RemoveScopeChangedHandler(Action handler)
        {
            if (handler != null)
                _scopeChangedHandlers.Remove(handler);
        }

        public void BindSettingsButton(Button button)
        {
            if (button == null)
                return;

            GlobalEntitySearchScopeSettings.SetIconButtonImage(button, Resources.cog);

            ContextMenuStrip menu = BuildScopeMenu();
            button.Click += (sender, e) =>
            {
                UpdateMenuChecks(menu);
                menu.Show(button, new Point(0, button.Height));
            };
        }

        private ContextMenuStrip BuildScopeMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            menu.Items.Add(CreateScopeMenuItem("Search All Composites", GlobalEntitySearchScope.AllComposites, menu));
            menu.Items.Add(CreateScopeMenuItem("Search Within Current Composite", GlobalEntitySearchScope.CurrentComposite, menu));
            menu.Items.Add(CreateScopeMenuItem("Search Within Current Composite And Nested", GlobalEntitySearchScope.CurrentCompositeAndNested, menu));

            UpdateMenuChecks(menu);
            return menu;
        }

        private ToolStripMenuItem CreateScopeMenuItem(string text, GlobalEntitySearchScope scope, ContextMenuStrip menu)
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

        private void UpdateMenuChecks(ContextMenuStrip menu)
        {
            GlobalEntitySearchScope activeScope = Scope;
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Tag is GlobalEntitySearchScope scope)
                    menuItem.Checked = scope == activeScope;
            }
        }

        private void NotifyScopeChanged()
        {
            Action[] handlers = _scopeChangedHandlers.ToArray();
            foreach (Action handler in handlers)
                handler?.Invoke();
        }
    }

    public static class GlobalEntitySearchScopeSettings
    {
        private static readonly EntitySearchScopeController _controller =
            new EntitySearchScopeController(Settings.GlobalEntitySearchScope, GlobalEntitySearchScope.CurrentCompositeAndNested);

        public static GlobalEntitySearchScope Scope
        {
            get => _controller.Scope;
            set => _controller.Scope = value;
        }

        public static void AddScopeChangedHandler(Action handler) => _controller.AddScopeChangedHandler(handler);

        public static void RemoveScopeChangedHandler(Action handler) => _controller.RemoveScopeChangedHandler(handler);

        public static void BindSettingsButton(Button button) => _controller.BindSettingsButton(button);

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
