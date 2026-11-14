using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using PadLauncher;
class EmulatorDetectionTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static string Write(string root,string relative) {string file=Path.Combine(root,relative); Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,""); return file;}
    [STAThread] public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorDetection-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            var records=new List<EmulatorInstallRecord>(); var executables=new Dictionary<string,string>();
            foreach(var preset in EmulatorLibrary.Presets) {
                string file=Write(root,Path.Combine("Installed apps",preset.Id,EmulatorDetection.Executables[preset.Id][0])); executables[preset.Id]=file;
                records.Add(new EmulatorInstallRecord {Name=preset.Name+" 2.0 (64-bit)",Location=Path.GetDirectoryName(file)});
            }
            var detected=EmulatorDetection.FromSources(null,records,null,CancellationToken.None);
            Check(detected.Matches.Select(item => item.Id).Distinct().Count()==EmulatorLibrary.Presets.Length,"Installer fixtures detect every supported emulator without executing it");
            foreach(var preset in EmulatorLibrary.Presets) Check(detected.Matches.Any(item => item.Id==preset.Id && item.Path==executables[preset.Id]),preset.Name+" installer path is detected");
            string pcsx2=executables["pcsx2"];
            Check(EmulatorDetection.ExecutableFromCommand("\""+pcsx2+"\" --fullscreen")==pcsx2 && EmulatorDetection.ExecutableFromCommand("\""+pcsx2+"\",0")==pcsx2,"Quoted commands and DisplayIcon suffixes preserve paths with spaces");
            Check(EmulatorDetection.ExecutableFromCommand(pcsx2+" --fullscreen")==pcsx2,"Unquoted executable paths with spaces are recognized");
            Check(EmulatorDetection.ExecutableFromCommand("pcsx2-qt.exe")==null && EmulatorDetection.ExecutableFromCommand(@"\\server\share\pcsx2-qt.exe")==null,"Relative and network targets are not probed");
            var hints=new[]{new EmulatorExecutableHint {Path="\""+pcsx2+"\"",Source="App Paths fixture"},new EmulatorExecutableHint {Path=pcsx2,Source="Shortcut fixture"},new EmulatorExecutableHint {Path=pcsx2.ToUpperInvariant(),Source="App Paths fixture"}};
            var duplicate=EmulatorDetection.FromSources(hints,new[]{records.Single(item => item.Name.StartsWith("PCSX2"))},null,CancellationToken.None);
            Check(duplicate.Matches.Count==1 && duplicate.Matches[0].Sources.Count==3,"Registry views and shortcut duplicates merge evidence for the same executable");
            string alternate=Write(root,Path.Combine("Alternate PCSX2","pcsx2.exe"));
            Check(EmulatorDetection.FromSources(hints.Concat(new[]{new EmulatorExecutableHint {Path=alternate,Source="App Paths fixture"}}),null,null,CancellationToken.None).Matches.Count==2,"Different installations remain separate choices");
            string invalid=Write(root,Path.Combine("Wrong","updater.exe"));
            Check(EmulatorDetection.FromSources(new[]{new EmulatorExecutableHint {Path=invalid,Source="Shortcut"},new EmulatorExecutableHint {Path=Path.Combine(root,"gone","rpcs3.exe"),Source="Stale registration"}},null,null,CancellationToken.None).Matches.Count==0,"Updater executables and stale registrations are rejected");
            var psp=records.Single(item => item.Name.StartsWith("PPSSPP")); psp.Location=null; psp.Icon=Path.Combine(Path.GetDirectoryName(executables["ppsspp"]),"PPSSPPWindows.exe")+",0";
            Check(EmulatorDetection.FromSources(null,new[]{psp},null,CancellationToken.None).Matches.Single().Path==executables["ppsspp"],"A missing PPSSPP 32-bit icon still locates its installed 64-bit executable");
            string installedRoot=Path.Combine(root,"Standard apps"); string retro=Write(installedRoot,Path.Combine("RetroArch","retroarch.exe"));
            Write(installedRoot,Path.Combine("Unrelated","rpcs3.exe"));
            Check(EmulatorDetection.FromSources(null,null,new[]{installedRoot},CancellationToken.None).Matches.Single().Path==retro,"Standard locations inspect only relevant emulator folders");
            Check(EmulatorDetection.FromSources(null,new[]{new EmulatorInstallRecord {Name="Dolphin file manager",Location=Path.GetDirectoryName(executables["dolphin"]),Icon=executables["dolphin"]}},null,CancellationToken.None).Matches.Count==0,"Unrelated Dolphin applications are not treated as emulator installations");
            var settings=new Settings {Emulators=new List<EmulatorConfig>{new EmulatorConfig {Id="pcsx2",Path=alternate,ImportPath="old library",RomFolder="keep ROMs",Arguments="--custom {game}",Games=new List<EmulatorGame>{new EmulatorGame {Id="keep-game",Name="Existing game",Path="keep.iso"}}}}};
            var applied=EmulatorDetection.Apply(settings,new Dictionary<string,string>{{"pcsx2",pcsx2}});
            Check(EmulatorLibrary.Config(applied,"pcsx2").Path==pcsx2 && EmulatorLibrary.Config(settings,"pcsx2").Path==alternate,"Applying reviewed matches changes a copied configuration only");
            Check(EmulatorLibrary.Config(applied,"pcsx2").RomFolder=="keep ROMs" && EmulatorLibrary.Config(applied,"pcsx2").Arguments=="--custom {game}" && EmulatorLibrary.Config(applied,"pcsx2").Games.Single().Id=="keep-game","Applying an executable preserves ROM folders, arguments and games");
            Check(EmulatorLibrary.Config(applied,"pcsx2").ImportPath!="old library","Applying a changed executable re-detects the library location");
            Check(EmulatorLibrary.Config(EmulatorDetection.Apply(settings,new Dictionary<string,string>()),"pcsx2").Path==alternate,"Unchecked matches do not replace existing paths");
            bool rejected=false; try {EmulatorDetection.Apply(settings,new Dictionary<string,string>{{"pcsx2",executables["rpcs3"]}});} catch(ArgumentException) {rejected=true;}
            Check(rejected && EmulatorLibrary.Config(settings,"pcsx2").Path==alternate,"An executable for the wrong emulator cannot be applied");
            var cancellation=new CancellationTokenSource(); cancellation.Cancel(); bool canceled=false;
            try {EmulatorDetection.FromSources(null,null,null,cancellation.Token);} catch(OperationCanceledException) {canceled=true;} Check(canceled,"Canceled scans stop even with no sources");
            object shell=null,link=null;
            try {
                var type=Type.GetTypeFromProgID("WScript.Shell"); shell=Activator.CreateInstance(type);
                string shortcut=Path.Combine(root,"PCSX2.lnk"); link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcut});
                link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{pcsx2});
                link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
                Check(EmulatorDetection.ShortcutTarget(shortcut)==pcsx2,"A real temporary Windows shortcut is read without launching its target");
            } finally {if(link!=null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); if(shell!=null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);}
            Console.WriteLine(checks+" emulator detection checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
