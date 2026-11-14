using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        void ApplySettingsTheme(Window dialog) {
            using(var theme=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SettingsTheme.xaml")) {
                var resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(theme);
                dialog.Resources.MergedDictionaries.Add(resources);
                dialog.Resources.Add(typeof(SettingsComboBox),resources[typeof(ComboBox)]);
            }
            string inputStyle=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type INPUT}'>
                <Setter Property='Background' Value='#1C303D'/><Setter Property='Foreground' Value='#ECF3F2'/>
                <Setter Property='BorderBrush' Value='#3A5260'/><Setter Property='BorderThickness' Value='1'/>
                <Setter Property='CaretBrush' Value='#A3F0CD'/><Setter Property='SelectionBrush' Value='#497D75'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type INPUT}'>
                    <Border x:Name='InputBorder' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                        <ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#64848E'/></Trigger>
                        <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#A3F0CD'/></Trigger>
                        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='InputBorder' Property='Opacity' Value='0.5'/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>";
            var textStyle=(Style)System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","TextBox"));
            textStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,dialog.FindResource("PadTextMenu")));
            dialog.Resources.Add(typeof(TextBox),textStyle);
            var passwordStyle=(Style)System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","PasswordBox"));
            passwordStyle.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,dialog.FindResource("PadPasswordMenu")));
            dialog.Resources.Add(typeof(PasswordBox),passwordStyle);
            dialog.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
                <Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='6'/><Setter Property='Background' Value='Transparent'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'>
                        <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
                        <Track.Thumb><Thumb x:Name='MintThumb' Background='#A3F0CD' MinHeight='24'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{TemplateBinding Background}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                        <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
                    </Track></Grid>
                </ControlTemplate></Setter.Value></Setter>
            </Style>"));
        }
    }
}
