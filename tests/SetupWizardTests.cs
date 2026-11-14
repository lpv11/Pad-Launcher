using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PadLauncher;
class SetupWizardTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static IEnumerable<DependencyObject> Descendants(DependencyObject node) {
        yield return node;
        foreach(var item in LogicalTreeHelper.GetChildren(node)) {var child=item as DependencyObject; if(child!=null) foreach(var next in Descendants(child)) yield return next;}
    }
    static T Find<T>(Window wizard,string name) where T:FrameworkElement {return Descendants(wizard).OfType<T>().First(item => item.Name==name);}
    static void Click(Window wizard,string name) {Find<Button>(wizard,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    static Window Wizard(Launcher launcher) {return (Window)typeof(Launcher).GetField("setupWindow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(launcher);}
    static void SkipToEnd(Window wizard) {for(int i=0;i<4;i++) Click(wizard,"SetupSkip");}
    static void CheckStartupProfiles(string root) {
        string previous=Storage.Root;
        try {
            foreach(string profile in new[]{"fresh","existing","cached"}) {
                Storage.Root=Path.Combine(root,profile); Directory.CreateDirectory(Storage.Root);
                if(profile=="existing") Storage.Write("settings.json",new Settings {LeftStickSpeed=2});
                if(profile=="cached") Storage.Write("library.json",new Library());
                var instance=new Launcher(false,false);
                try {
                    Check(instance.NeedsInitialSetup==(profile=="fresh"),"First-run setup is only automatic for a fresh profile: "+profile);
                    instance.StartInitialSetup();
                    Check((Wizard(instance)!=null)==(profile=="fresh"),"Startup does not force setup onto existing data: "+profile);
                    if(profile=="existing") Check(Storage.Read<Settings>("settings.json").LeftStickSpeed==2 && !Storage.Read<Settings>("settings.json").SetupWizardShown,"Existing settings are not rewritten by a setup prompt");
                } finally {
                    // Prevent the real launcher scan when closing this fresh-profile fixture.
                    typeof(Launcher).GetField("demo",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(instance,true);
                    if(Wizard(instance)!=null) Wizard(instance).Close(); instance.ExitApp();
                }
            }
        } finally {Storage.Root=previous;}
    }
    [STAThread] public static void Main(string[] args) {
        string root=Path.Combine(Path.GetTempPath(),"SetupWizard-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        try {
            CheckStartupProfiles(root);
            string oldExe=Path.Combine(root,"old","pcsx2.exe"),newExe=Path.Combine(root,"new","pcsx2-qt.exe");
            foreach(string file in new[]{oldExe,newExe}) {Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,"");}
            var initial=new Settings {GogEnabled=false,LeftStickSpeed=2,HiddenGameIds=new List<string>{"hidden-sentinel"},CustomLists=new List<CustomList>{new CustomList {Id="list:sentinel",Name="Keep",Games=new List<string>{"keep-game"}}},Emulators=new List<EmulatorConfig>{new EmulatorConfig {Id="pcsx2",Path=oldExe,RomFolder="keep-rom-folder",Arguments="--existing {game}",Games=new List<EmulatorGame>()}}};
            Storage.Write("settings.json",initial); ArtworkKey.Save("existing-key"); launcher=new Launcher(true,true);
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); var wizard=Wizard(launcher);
            if(args.Length==2 && (args[0]=="--render" || args[0]=="--render-emulators" || args[0]=="--render-layouts")) {
                if(args[0]=="--render-emulators") Click(wizard,"SetupSkip");
                if(args[0]=="--render-layouts") for(int step=0;step<3;step++) Click(wizard,"SetupSkip");
                wizard.UpdateLayout();
                var content=(FrameworkElement)wizard.Content;
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);
                var visual=new System.Windows.Media.DrawingVisual();
                using(var drawing=visual.RenderOpen()) drawing.DrawRectangle(new System.Windows.Media.VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));
                image.Render(visual);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using(var output=File.Create(args[1])) png.Save(output);
                return;
            }
            Check(wizard!=null && Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && !Find<Button>(wizard,"SetupBack").IsEnabled,"Setup starts on its first optional page");
            Find<TextBox>(wizard,"SetupSteamPath").Text="invalid Steam path"; Click(wizard,"SetupNext");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && Find<TextBlock>(wizard,"SetupStatus").Text.Contains("Could not save"),"Invalid optional input is reported without changing the saved page");
            Click(wizard,"SetupSkip"); Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 2 of 5","Skip bypasses invalid optional input");
            Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text="invalid emulator path"; Click(wizard,"SetupSkip");
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="skip-this-key"; Click(wizard,"SetupSkip");
            Find<ComboBox>(wizard,"SetupButtonStyle").SelectedValue="Nintendo"; Find<ComboBox>(wizard,"SetupLayout").SelectedValue="grid"; Click(wizard,"SetupSkip");
            Check(Find<Button>(wizard,"SetupSkip").Visibility==Visibility.Collapsed && Find<Button>(wizard,"SetupNext").Visibility==Visibility.Collapsed && Find<Button>(wizard,"SetupRestart").Visibility==Visibility.Visible && Find<Button>(wizard,"SetupFinish").Visibility==Visibility.Visible,"Last page replaces Skip and Next with Restart setup and Finish");
            var skipped=Storage.Read<Settings>("settings.json");
            Check(skipped.GogEnabled==false && skipped.ButtonPrompts=="PlayStation" && skipped.CarouselLayout=="angled" && EmulatorLibrary.Config(skipped,"pcsx2").Path==oldExe && ArtworkKey.Load()=="existing-key","Skipped pages preserve launchers, emulator paths, artwork keys and appearance");
            Click(wizard,"SetupFinish");
            Check(Wizard(launcher)==null && Storage.Read<Settings>("settings.json").SetupWizardCompleted,"All pages can be skipped and setup still finishes");
            Check(Storage.Read<Settings>("settings.json").LeftStickSpeed==2 && Storage.Read<Settings>("settings.json").HiddenGameIds.Single()=="hidden-sentinel" && Storage.Read<Settings>("settings.json").CustomLists.Single().Id=="list:sentinel","Setup preserves unrelated preferences and existing lists");
            launcher.StartInitialSetup();
            Check(Wizard(launcher)==null,"Later launches do not force the wizard again");
            launcher.OpenSettingsTab(5);
            Check(Descendants(launcher.HelpWindow).OfType<Button>().Any(button => button.Name=="RestartSetupWizard"),"System provides a Restart setup action in the regular app");
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); wizard=Wizard(launcher);
            Check(!Find<CheckBox>(wizard,"SetupLauncher_gog").IsChecked.GetValueOrDefault(),"Rerunning setup reflects saved launcher choices");
            Find<TextBox>(wizard,"SetupSteamPath").Clear(); Find<CheckBox>(wizard,"SetupLauncher_gog").IsChecked=true; Click(wizard,"SetupNext");
            Check(Storage.Read<Settings>("settings.json").GogEnabled && Storage.Read<Settings>("settings.json").SetupWizardPage==1,"Next saves launcher choices and progress without requiring Steam");
            Check(Find<Button>(wizard,"SetupDetectEmulators")!=null,"Emulator detection is accessible directly from the wizard");
            Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text=newExe;
            Click(wizard,"SetupBack"); Click(wizard,"SetupNext");
            Check(Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text==newExe,"Back and Next preserve an emulator-page draft");
            Click(wizard,"SetupNext"); var applied=Storage.Read<Settings>("settings.json");
            Check(EmulatorLibrary.Config(applied,"pcsx2").Path==newExe && EmulatorLibrary.Config(applied,"pcsx2").RomFolder=="keep-rom-folder" && EmulatorLibrary.Config(applied,"pcsx2").Arguments=="--existing {game}","Wizard saves emulator paths without clearing ROM folders or arguments");
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="new-key"; Click(wizard,"SetupNext");
            Check(ArtworkKey.Load()=="new-key" && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"steamgriddb.key"))).Contains("new-key"),"Next saves the optional artwork key encrypted");
            Check(Descendants(wizard).OfType<Image>().Count(image => image.Source is System.Windows.Media.Imaging.BitmapSource)==3,"All three layout options have template pictures");
            Click(wizard,"SetupLayoutPreview_grid");
            Check((string)Find<ComboBox>(wizard,"SetupLayout").SelectedValue=="grid","Clicking a layout picture selects that layout");
            Click(wizard,"SetupLayoutPreview_rectangular");
            Check((string)Find<ComboBox>(wizard,"SetupLayout").SelectedValue=="rectangular","The rectangular preview selects the same option as the dropdown");
            Find<ComboBox>(wizard,"SetupButtonStyle").SelectedValue="Nintendo"; Find<ComboBox>(wizard,"SetupLayout").SelectedValue="rectangular"; Find<CheckBox>(wizard,"SetupShowTitle").IsChecked=false; Click(wizard,"SetupNext");
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="Nintendo" && Storage.Read<Settings>("settings.json").CarouselLayout=="rectangular" && !Storage.Read<Settings>("settings.json").ShowGameTitle,"Next saves optional controller labels and overlay choices");
            Click(wizard,"SetupRestart");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && Storage.Read<Settings>("settings.json").GogEnabled && EmulatorLibrary.Config(Storage.Read<Settings>("settings.json"),"pcsx2").Path==newExe && ArtworkKey.Load()=="new-key","Restart returns to the first page without erasing saved configuration");
            SkipToEnd(wizard); Click(wizard,"SetupFinish");
            Check(Storage.Read<Settings>("settings.json").SetupWizardCompleted,"Setup can finish after restarting and skipping every page");
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); wizard=Wizard(launcher); Click(wizard,"SetupSkip"); wizard.Close();
            Check(Wizard(launcher)==null && Storage.Read<Settings>("settings.json").SetupWizardShown && Storage.Read<Settings>("settings.json").SetupWizardPage==1,"Closing early leaves saved progress and keeps setup optional");
            Console.WriteLine(checks+" setup wizard checks passed");
        } finally {if(launcher!=null) {var wizard=Wizard(launcher); if(wizard!=null) wizard.Close(); launcher.ExitApp();} if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
