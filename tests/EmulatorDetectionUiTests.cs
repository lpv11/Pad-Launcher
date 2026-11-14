using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadLauncher;
class EmulatorDetectionUiTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static IEnumerable<DependencyObject> Children(DependencyObject root) {
        yield return root;
        foreach(var item in LogicalTreeHelper.GetChildren(root)) {var child=item as DependencyObject; if(child!=null) foreach(var descendant in Children(child)) yield return descendant;}
    }
    [STAThread] public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorDetectionUi-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        try {
            Check(!TestEnvironment.EmulatorDetection,"Detection UI tests cover the regular app");
            string oldExe=Path.Combine(root,"old","pcsx2.exe"),newExe=Path.Combine(root,"new","pcsx2-qt.exe"),psp=Path.Combine(root,"PSP","PPSSPPWindows64.exe");
            foreach(string file in new[]{oldExe,newExe,psp}) {Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,"");}
            Storage.Write("settings.json",new Settings {Emulators=new List<EmulatorConfig>{new EmulatorConfig {Id="pcsx2",Path=oldExe,RomFolder="retain folder",Games=new List<EmulatorGame>()}}});
            launcher=new Launcher(false,true); var owner=launcher.CreateHelpWindow(); owner.Show();
            try {
                Check(Children(owner).OfType<Button>().Any(button => button.Name=="DetectInstalledEmulators"),"Test Settings includes the installed-emulator detection action");
                Check(Children(owner).OfType<CheckBox>().Single(box => box.Name=="StartWithWindowsSetting").IsEnabled,"The normal app retains its Windows startup setting");
                var result=new EmulatorDetectionResult {Matches=new List<DetectedEmulator>{new DetectedEmulator {Id="pcsx2",Name="PCSX2",Path=newExe,Sources=new List<string>{"Installer fixture"}},new DetectedEmulator {Id="ppsspp",Name="PPSSPP",Path=psp,Sources=new List<string>{"App Paths fixture"}}}};
                bool rebuilt=false; Func<CancellationToken,EmulatorDetectionResult> scan=token => result;
                typeof(Launcher).GetMethod("ShowEmulatorDetection",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(launcher,new object[]{owner,(Action)(() => rebuilt=true),scan});
                var dialog=owner.OwnedWindows.Cast<Window>().Single(window => window.Title=="Detect installed emulators");
                try {
                    var frame=new DispatcherFrame(); var timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)}; bool timedOut=false;
                    timeout.Tick+=(s,e) => {timedOut=true; frame.Continue=false;}; timeout.Start();
                    var poll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
                    poll.Tick+=(s,e) => {if(Children(dialog).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators").IsEnabled) frame.Continue=false;}; poll.Start();
                    Dispatcher.PushFrame(frame); poll.Stop(); timeout.Stop(); Check(!timedOut,"Detected candidates render without blocking the UI thread");
                    var boxes=Children(dialog).OfType<CheckBox>().ToList();
                    Check(boxes.Single(box => (string)box.Content=="PCSX2").IsChecked==false && boxes.Single(box => (string)box.Content=="PPSSPP").IsChecked==true,"Configured emulators start unchecked while newly found ones start checked");
                    Check(EmulatorLibrary.Config(Storage.Read<Settings>("settings.json"),"pcsx2").Path==oldExe,"Detection alone does not change any saved paths");
                    Children(dialog).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var applied=Storage.Read<Settings>("settings.json");
                    Check(rebuilt && EmulatorLibrary.Config(applied,"pcsx2").Path==oldExe && EmulatorLibrary.Config(applied,"pcsx2").RomFolder=="retain folder" && EmulatorLibrary.Config(applied,"ppsspp").Path==psp,"Applying reviewed choices preserves unchecked emulator paths and existing ROM settings");
                    Check(!dialog.IsVisible && !owner.OwnedWindows.Cast<Window>().Contains(dialog),"Successfully applying selected emulator paths closes the review window");
                } finally {dialog.Close();}
            } finally {owner.Close();}
            Console.WriteLine(checks+" emulator detection UI checks passed");
        } finally {if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
