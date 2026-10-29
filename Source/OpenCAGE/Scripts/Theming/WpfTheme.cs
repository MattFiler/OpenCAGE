using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// Dark mode for WPF views hosted in an ElementHost.
    ///
    /// ThemeEngine only walks WinForms controls, so a WPF view kept WPF's own light styles - white tab
    /// pages, white group frames and near-black text on the dark window around it. This puts a set of dark
    /// implicit styles on the view's root while dark mode is on, so everything under it picks them up,
    /// whether it was declared in XAML or built in code later. The colours are ThemeColours'.
    /// </summary>
    public static class WpfTheme
    {
        private const string Marker = "OpenCAGE.WpfTheme.Dark";
        private static string _xaml;

        /// <summary>Theme the view now, and again whenever the theme changes for as long as it is loaded.</summary>
        public static void Follow(FrameworkElement root)
        {
            if (root == null)
                return;

            Apply(root);
            Action changed = () => root.Dispatcher.BeginInvoke(new Action(() => Apply(root)));
            ThemeManager.ThemeChanged += changed;
            root.Unloaded += (s, e) => ThemeManager.ThemeChanged -= changed;
            root.Loaded += (s, e) =>
            {
                ThemeManager.ThemeChanged -= changed;
                ThemeManager.ThemeChanged += changed;
                Apply(root);
            };
        }

        public static void Apply(FrameworkElement root)
        {
            if (root == null)
                return;

            for (int i = root.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
                if (root.Resources.MergedDictionaries[i].Contains(Marker))
                    root.Resources.MergedDictionaries.RemoveAt(i);
            if (ThemeManager.IsDark)
            {
                root.Resources.MergedDictionaries.Add(DarkStyles());
                //Text inherits its colour, so the root's covers every TextBlock without restyling them
                root.SetValue(TextElement.ForegroundProperty, Brush(ThemeColours.Text));
                if (root is Control control)
                    control.Background = Brush(ThemeColours.Surface);
                else if (root is Panel panel)
                    panel.Background = Brush(ThemeColours.Surface);
            }
            else
            {
                root.ClearValue(TextElement.ForegroundProperty);
                if (root is Control control)
                    control.ClearValue(Control.BackgroundProperty);
                else if (root is Panel panel)
                    panel.ClearValue(Panel.BackgroundProperty);
            }
        }

        private static SolidColorBrush Brush(System.Drawing.Color colour)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
            brush.Freeze();
            return brush;
        }

        private static string Hex(System.Drawing.Color colour)
        {
            return "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2");
        }

        private static ResourceDictionary DarkStyles()
        {
            //A dictionary of its own for each view: one instance merged into several can't be taken back out cleanly
            if (_xaml != null)
                return (ResourceDictionary)XamlReader.Parse(_xaml);

            string surface = Hex(ThemeColours.Surface), raised = Hex(ThemeColours.Raised), input = Hex(ThemeColours.Input);
            string border = Hex(ThemeColours.Border), borderStrong = Hex(ThemeColours.BorderStrong), hover = Hex(ThemeColours.Hover);
            string text = Hex(ThemeColours.Text), textDisabled = Hex(ThemeColours.TextDisabled), accent = Hex(ThemeColours.Accent);
            string disabledFill = Hex(ThemeColours.InputDisabled), thumb = borderStrong, thumbHover = "#6E6E74", thumbDrag = Hex(ThemeColours.TextDim);

            string xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='OpenCAGE.WpfTheme.Dark' Color='Black'/>
  <Style TargetType='GroupBox'>
    <Setter Property='BorderBrush' Value='{border}'/>
    <Setter Property='Foreground' Value='{text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <!-- WPF's own draws a white inner line inside the frame whatever the brushes say -->
        <ControlTemplate TargetType='GroupBox'>
          <Grid SnapsToDevicePixels='True'>
            <Grid.RowDefinitions>
              <RowDefinition Height='Auto'/>
              <RowDefinition Height='*'/>
            </Grid.RowDefinitions>
            <ContentPresenter Grid.Row='0' ContentSource='Header' Margin='6,0,6,4' RecognizesAccessKey='True'/>
            <Border Grid.Row='1' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'>
              <ContentPresenter/>
            </Border>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='Label'>
    <Setter Property='Foreground' Value='{text}'/>
  </Style>
  <Style TargetType='TabControl'>
    <Setter Property='Background' Value='{surface}'/>
    <Setter Property='BorderBrush' Value='{border}'/>
    <Setter Property='Foreground' Value='{text}'/>
  </Style>
  <Style TargetType='TabItem'>
    <Setter Property='Foreground' Value='{text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TabItem'>
          <Border x:Name='Bd' Background='{raised}' BorderBrush='{border}' BorderThickness='1,1,1,0' Margin='0,0,-1,0' Padding='8,2' SnapsToDevicePixels='True'>
            <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{hover}'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{accent}'/>
              <Setter TargetName='Bd' Property='BorderBrush' Value='{accent}'/>
              <Setter Property='Panel.ZIndex' Value='1'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Foreground' Value='{textDisabled}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='Button'>
    <Setter Property='Foreground' Value='{text}'/>
    <Setter Property='Background' Value='{input}'/>
    <Setter Property='BorderBrush' Value='{borderStrong}'/>
    <Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='6,2'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='Bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{hover}'/>
              <Setter TargetName='Bd' Property='BorderBrush' Value='{accent}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='Bd' Property='Background' Value='{accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Foreground' Value='{textDisabled}'/>
              <Setter TargetName='Bd' Property='Background' Value='{disabledFill}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='{text}'/>
    <Setter Property='Background' Value='{input}'/>
    <Setter Property='BorderBrush' Value='{border}'/>
    <Setter Property='CaretBrush' Value='{text}'/>
    <Setter Property='SelectionBrush' Value='{accent}'/>
  </Style>
  <Style TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <StackPanel Orientation='Horizontal' Background='Transparent'>
            <Border x:Name='Box' Width='14' Height='14' Background='{input}' BorderBrush='{borderStrong}' BorderThickness='1' VerticalAlignment='Center' SnapsToDevicePixels='True'>
              <Path x:Name='Mark' Data='M1.5,6 L4.5,9 L10.5,2.5' Stroke='{text}' StrokeThickness='2' Visibility='Collapsed'/>
            </Border>
            <ContentPresenter Margin='5,0,0,0' VerticalAlignment='Center' RecognizesAccessKey='True'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='Mark' Property='Visibility' Value='Visible'/>
              <Setter TargetName='Box' Property='Background' Value='{accent}'/>
              <Setter TargetName='Box' Property='BorderBrush' Value='{accent}'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='Box' Property='BorderBrush' Value='{accent}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Foreground' Value='{textDisabled}'/>
              <Setter TargetName='Box' Property='Opacity' Value='0.5'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='DarkScrollThumb' TargetType='Thumb'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Thumb'>
          <Border x:Name='T' Background='{thumb}' Margin='2'/>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='T' Property='Background' Value='{thumbHover}'/>
            </Trigger>
            <Trigger Property='IsDragging' Value='True'>
              <Setter TargetName='T' Property='Background' Value='{thumbDrag}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key='DarkScrollPage' TargetType='RepeatButton'>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RepeatButton'>
          <Border Background='Transparent'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='ScrollBar'>
    <Setter Property='Background' Value='{surface}'/>
    <Setter Property='Width' Value='12'/>
    <Setter Property='MinWidth' Value='12'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Border Background='{TemplateBinding Background}'>
            <Track x:Name='PART_Track' IsDirectionReversed='True'>
              <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource DarkScrollPage}' Command='ScrollBar.PageUpCommand'/></Track.DecreaseRepeatButton>
              <Track.Thumb><Thumb Style='{StaticResource DarkScrollThumb}'/></Track.Thumb>
              <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource DarkScrollPage}' Command='ScrollBar.PageDownCommand'/></Track.IncreaseRepeatButton>
            </Track>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property='Orientation' Value='Horizontal'>
        <Setter Property='Width' Value='Auto'/>
        <Setter Property='MinWidth' Value='0'/>
        <Setter Property='Height' Value='12'/>
        <Setter Property='MinHeight' Value='12'/>
        <Setter Property='Template'>
          <Setter.Value>
            <ControlTemplate TargetType='ScrollBar'>
              <Border Background='{TemplateBinding Background}'>
                <Track x:Name='PART_Track' IsDirectionReversed='False'>
                  <Track.DecreaseRepeatButton><RepeatButton Style='{StaticResource DarkScrollPage}' Command='ScrollBar.PageLeftCommand'/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb Style='{StaticResource DarkScrollThumb}'/></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Style='{StaticResource DarkScrollPage}' Command='ScrollBar.PageRightCommand'/></Track.IncreaseRepeatButton>
                </Track>
              </Border>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Trigger>
    </Style.Triggers>
  </Style>
</ResourceDictionary>";
            xaml = xaml.Replace("{surface}", surface).Replace("{raised}", raised).Replace("{input}", input).Replace("{borderStrong}", borderStrong)
                .Replace("{border}", border).Replace("{hover}", hover).Replace("{textDisabled}", textDisabled).Replace("{text}", text)
                .Replace("{accent}", accent).Replace("{disabledFill}", disabledFill).Replace("{thumbHover}", thumbHover)
                .Replace("{thumbDrag}", thumbDrag).Replace("{thumb}", thumb);
            _xaml = xaml;
            return (ResourceDictionary)XamlReader.Parse(_xaml);
        }
    }
}
