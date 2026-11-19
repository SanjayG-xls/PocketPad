using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Win32;
using PocketPad;

[assembly:AssemblyTitle("PocketPad Setup")]
[assembly:AssemblyProduct("PocketPad Desktop")]
[assembly:AssemblyCompany("Sanjay")]
[assembly:AssemblyCopyright("Copyright (c) 2026 Sanjay")]
[assembly:AssemblyVersion("2.1.0.0")]
[assembly:AssemblyFileVersion("2.1.0.0")]

namespace PocketPadSetup {
    public sealed class InstallOptions {
        public string Target;
        public bool Desktop=true,Test=false;
    }
    public sealed class Marker {public string appId{get;set;}public string version{get;set;}public bool test{get;set;}}
    public static class InstallCore {
        public const string Id="PocketPad.Desktop.Native";
        public static string DefaultTarget {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","PocketPad");}}
        private static string Key(bool test){return "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\"+(test?"PocketPadDesktopNativeTest":"PocketPadDesktop");}
        public static string Validate(string input,bool existing){
            string root=Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar);
            if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Choose a regular PocketPad folder instead of a directory link.");
            if(!String.Equals(Path.GetFileName(root),"PocketPad",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Choose a folder named PocketPad. Setup keeps program files together in that folder.");
            string marker=Path.Combine(root,"PocketPad.install.json");
            if(File.Exists(marker)){Marker m=new JavaScriptSerializer().Deserialize<Marker>(File.ReadAllText(marker));if(m==null||m.appId!=Id)throw new InvalidOperationException("This folder belongs to another application.");}
            else if(existing)throw new InvalidOperationException("A valid PocketPad installation was not found in this folder.");
            else if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new InvalidOperationException("Choose an empty PocketPad folder or an existing PocketPad installation.");
            return root;
        }
        private static void EnsureClosed(string target){foreach(Process process in Process.GetProcessesByName("PocketPad")){try{string file=process.MainModule.FileName;if(String.Equals(Path.GetDirectoryName(file),target,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Close PocketPad before updating or removing it.");}catch(InvalidOperationException){throw;}catch{}finally{process.Dispose();}}}
        private static void Shortcut(string path,string target,string icon){
            Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(type);object link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});Type t=link.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(target)});t.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{icon+",0"});t.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{"PocketPad Desktop — phone controller studio"});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
        private static void RemoveShortcut(string path,string root){
            if(!File.Exists(path))return;object shell=null,link=null;
            try{Type type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});string target=(string)link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null);if(Path.GetFullPath(target).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))File.Delete(path);}catch{}
            finally{if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);if(shell!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
        }
        private static void MoveInstalledFolder(string source,string destination){
            source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
            if(!String.Equals(Path.GetDirectoryName(source),Path.GetDirectoryName(destination),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Installation staging must stay beside the application folder.");
            // Windows can briefly lock freshly extracted files while inspecting them.
            // Retry only sharing/access errors; preserve atomic swap and rollback.
            for(int attempt=0;;attempt++){
                try{Directory.Move(source,destination);return;}
                catch(IOException e){int code=e.HResult&0xffff;if(attempt>=10||(code!=5&&code!=32&&code!=33)||!Directory.Exists(source)||Directory.Exists(destination))throw;}
                catch(UnauthorizedAccessException){if(attempt>=10||!Directory.Exists(source)||Directory.Exists(destination))throw;}
                System.Threading.Thread.Sleep(500);
            }
        }
        public static void Install(InstallOptions options,IProgress<Tuple<double,string>> progress){
            string target=Validate(options.Target,false);EnsureClosed(target);
            string staging=target+".staging-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(staging);
            string logFolder=options.Test?Path.Combine(Path.GetDirectoryName(target),"test-logs"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PocketPad","logs");Directory.CreateDirectory(logFolder);
            try{
                using(Stream embedded=Assembly.GetExecutingAssembly().GetManifestResourceStream("PocketPad.Payload")){
                    if(embedded==null)throw new InvalidOperationException("The application payload is missing from Setup.");
                    using(var archive=new ZipArchive(embedded,ZipArchiveMode.Read)){
                        long total=archive.Entries.Sum(e=>e.Length),done=0;
                        foreach(ZipArchiveEntry entry in archive.Entries){
                            string path=Path.GetFullPath(Path.Combine(staging,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
                            if(!path.StartsWith(staging+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Invalid application package path.");
                            if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(path);continue;}
                            Directory.CreateDirectory(Path.GetDirectoryName(path));using(Stream input=entry.Open())using(FileStream output=File.Create(path))input.CopyTo(output);done+=entry.Length;
                            progress.Report(Tuple.Create(total==0?100:done*94.0/total,"Installing "+entry.FullName));
                        }
                    }
                }
                File.WriteAllText(Path.Combine(staging,"PocketPad.install.json"),new JavaScriptSerializer().Serialize(new Marker{appId=Id,version="2.1",test=options.Test}));
                string backup=target+".backup-"+Guid.NewGuid().ToString("N");bool backedUp=false;
                if(Directory.Exists(target)){if(!Directory.EnumerateFileSystemEntries(target).Any())Directory.Delete(target);else{MoveInstalledFolder(target,backup);backedUp=true;}}
                try{MoveInstalledFolder(staging,target);}catch{if(backedUp&&!Directory.Exists(target))MoveInstalledFolder(backup,target);throw;}
                if(backedUp)RemoveOwnedTree(backup,backup,progress);
                string personal=options.Test?Path.Combine(Path.GetDirectoryName(target),"test-user-data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PocketPad","data");
                string seed=Path.Combine(target,"seed","presets.json");if(File.Exists(seed)&&!File.Exists(Path.Combine(personal,"presets.json"))){Directory.CreateDirectory(personal);File.Copy(seed,Path.Combine(personal,"presets.json"));}
                progress.Report(Tuple.Create(96.0,"Registering PocketPad in Windows"));
                using(RegistryKey key=Registry.CurrentUser.CreateSubKey(Key(options.Test))){key.SetValue("DisplayName",options.Test?"PocketPad Desktop Test":"PocketPad Desktop");key.SetValue("DisplayVersion","2.1");key.SetValue("Publisher","Sanjay");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",Path.Combine(target,"PocketPad.exe"));key.SetValue("UninstallString","\""+Path.Combine(target,"Uninstall.exe")+"\" --uninstall");key.SetValue("QuietUninstallString","\""+Path.Combine(target,"Uninstall.exe")+"\" --uninstall --silent");key.SetValue("InstallDate",DateTime.Now.ToString("yyyyMMdd"));key.SetValue("NoModify",1);key.SetValue("NoRepair",1);}
                string menu=options.Test?Path.Combine(Path.GetDirectoryName(target),"test-shortcuts"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"PocketPad");Directory.CreateDirectory(menu);Shortcut(Path.Combine(menu,"PocketPad.lnk"),Path.Combine(target,"PocketPad.exe"),Path.Combine(target,"PocketPad.exe"));Shortcut(Path.Combine(menu,"Uninstall PocketPad.lnk"),Path.Combine(target,"Uninstall.exe"),Path.Combine(target,"Uninstall.exe"));if(options.Desktop&&!options.Test)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"PocketPad.lnk"),Path.Combine(target,"PocketPad.exe"),Path.Combine(target,"PocketPad.exe"));
                progress.Report(Tuple.Create(100.0,"PocketPad is ready"));File.AppendAllText(Path.Combine(logFolder,"setup.log"),DateTime.Now.ToString("s")+" Installed "+target+Environment.NewLine);
            }catch(Exception e){File.AppendAllText(Path.Combine(logFolder,"setup.log"),e.ToString()+Environment.NewLine);throw;}
        }
        public static void Uninstall(string folder,bool removeData,bool test,IProgress<Tuple<double,string>> progress){
            string target=Validate(folder,true);EnsureClosed(target);
            string menu=test?Path.Combine(Path.GetDirectoryName(target),"test-shortcuts"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"PocketPad");RemoveShortcut(Path.Combine(menu,"PocketPad.lnk"),target);RemoveShortcut(Path.Combine(menu,"Uninstall PocketPad.lnk"),target);if(Directory.Exists(menu)&&!Directory.EnumerateFileSystemEntries(menu).Any())Directory.Delete(menu);if(!test)RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"PocketPad.lnk"),target);
            string resolved=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            // The marker and named absolute installation target above establish ownership.
            // Never follow directory links out of the installation while removing files.
            RemoveOwnedTree(resolved,resolved,progress);
            Registry.CurrentUser.DeleteSubKeyTree(Key(test),false);
            if(removeData&&!test){string data=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PocketPad"));if(Directory.Exists(data))RemoveOwnedTree(data,data,progress);}
            progress.Report(Tuple.Create(100.0,"PocketPad has been removed"));
        }
        private static void RemoveOwnedTree(string folder,string root,IProgress<Tuple<double,string>> progress){
            string path=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            if(path!=root&&!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Removal target is outside PocketPad's folder.");
            foreach(string file in Directory.GetFiles(path)){File.Delete(file);}
            foreach(string child in Directory.GetDirectories(path)){if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)Directory.Delete(child);else RemoveOwnedTree(child,root,progress);}
            Directory.Delete(path);
        }
    }
    public sealed class SetupWindow {
        public readonly Window Window;
        private readonly bool uninstall,test,preview;
        private string target;
        private int page;
        private bool busy,finished;
        private readonly StackPanel content=new StackPanel();
        private readonly TextBlock step=new TextBlock(),status=new TextBlock();
        private readonly Button back=new Button(),next=new Button();
        private TextBox location;
        private CheckBox desktop,driver,launch,deleteData;
        private readonly ProgressBar progress=new ProgressBar();
        public SetupWindow(bool remove,string folder,bool testing,bool previewOnly){
            uninstall=remove;target=folder;test=testing;preview=previewOnly;
            Window=new Window{Title=remove?"Uninstall PocketPad":"PocketPad Setup",Width=940,Height=650,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,WindowStyle=WindowStyle.None,Background=Brand.B("#10131C"),Foreground=Brand.B("#E9ECF8"),FontFamily=new FontFamily("Segoe UI"),FontSize=12};
            using(Stream theme=Assembly.GetExecutingAssembly().GetManifestResourceStream("PocketPad.Theme")){Window source=(Window)XamlReader.Load(theme);Window.Resources.MergedDictionaries.Add(source.Resources);}
            WindowChrome.SetWindowChrome(Window,new WindowChrome{CaptionHeight=0,CornerRadius=new CornerRadius(12),GlassFrameThickness=new Thickness(0),ResizeBorderThickness=new Thickness(0)});
            var border=new Border{CornerRadius=new CornerRadius(12),BorderBrush=Brand.B("#414A65"),BorderThickness=new Thickness(1),Background=Brand.B("#151925"),ClipToBounds=true};
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(76)});border.Child=root;Window.Content=border;
            var titlebar=new Grid{Background=Brand.B("#10141F")};titlebar.MouseLeftButtonDown+=(s,e)=>{DependencyObject node=e.OriginalSource as DependencyObject;while(node!=null){if(node is Button)return;node=VisualTreeHelper.GetParent(node);}try{Window.DragMove();}catch{}};titlebar.Children.Add(new TextBlock{Text=remove?"PocketPad · Uninstall":"PocketPad · Setup",Foreground=Brand.B("#8795B6"),Margin=new Thickness(17,0,0,0),VerticalAlignment=VerticalAlignment.Center,FontSize=10});var close=new Button{Content="×",Width=46,Padding=new Thickness(0),FontSize=20,HorizontalAlignment=HorizontalAlignment.Right,Background=Brushes.Transparent,BorderThickness=new Thickness(0)};close.Click+=(s,e)=>Window.Close();titlebar.Children.Add(close);root.Children.Add(titlebar);
            var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(288)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Grid.SetRow(body,1);root.Children.Add(body);
            var image=new Image{Stretch=Stretch.UniformToFill};using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PocketPad.SetupArt")){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();image.Source=bitmap;}body.Children.Add(image);
            var right=new Grid{Margin=new Thickness(35,27,35,20)};right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});right.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(right,1);body.Children.Add(right);step.FontSize=9;step.Foreground=Brand.B("#7689B5");right.Children.Add(step);var scroller=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroller,1);right.Children.Add(scroller);
            var bottom=new Grid{Background=Brand.B("#111620"),Margin=new Thickness(0)};Grid.SetRow(bottom,2);root.Children.Add(bottom);status.Foreground=Brand.B("#7888AA");status.FontSize=10;status.Margin=new Thickness(22,0,230,0);status.VerticalAlignment=VerticalAlignment.Center;status.TextWrapping=TextWrapping.Wrap;bottom.Children.Add(status);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,25,0)};back.Content="Back";back.MinWidth=84;back.Margin=new Thickness(0,0,10,0);back.Click+=(s,e)=>{if(page>0){page--;Render();}};next.Content="Continue";next.MinWidth=110;next.Style=Window.FindResource("Primary") as Style;next.Click+=async(s,e)=>await Advance();actions.Children.Add(back);actions.Children.Add(next);bottom.Children.Add(actions);Window.Closing+=(s,e)=>{if(busy)e.Cancel=true;};Render();
        }
        private void Title(string text,string sub){content.Children.Add(new TextBlock{Text=text,FontSize=30,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,14)});content.Children.Add(new TextBlock{Text=sub,FontSize=12,Foreground=Brand.B("#95A3C3"),TextWrapping=TextWrapping.Wrap,LineHeight=20,Margin=new Thickness(0,0,0,24)});}
        private void Line(string title,string subtitle){var panel=new StackPanel{Margin=new Thickness(0,0,0,20)};panel.Children.Add(new TextBlock{Text="✓   "+title,Foreground=Brand.B("#D3D9F0"),FontWeight=FontWeights.SemiBold,FontSize=13});panel.Children.Add(new TextBlock{Text=subtitle,Foreground=Brand.B("#7C8EAF"),FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24,7,0,0)});content.Children.Add(panel);}
        private void Render(){
            content.Children.Clear();back.Visibility=page==1&&!uninstall?Visibility.Visible:Visibility.Collapsed;status.Text="";
            if(uninstall){
                step.Text="POCKETPAD DESKTOP  /  REMOVE";Title("Uninstall PocketPad","Remove the desktop application and its Windows shortcuts. Your saved layouts can be kept for later.");content.Children.Add(new TextBlock{Text=target,Foreground=Brand.B("#A8B7D6"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});deleteData=new CheckBox{Content="Also remove my saved layouts and preferences",IsChecked=false};content.Children.Add(deleteData);next.Content="Uninstall";
            }else if(page==0){step.Text="01 / WELCOME";Title("Install PocketPad","A native desktop home for your phone controller.");Line("Native controller studio","Move, resize and map controls in a Windows application.");Line("Your phone, connected","QR pairing over Wi-Fi, hotspot or a tethered network.");Line("PS5 and Xbox layouts","Two factory layouts, plus two saved custom slots.");next.Content="Continue";status.Text="Windows 10 / 11 · 64-bit · Per-user installation";}
            else if(page==1){step.Text="02 / INSTALLATION";Title("Make it yours","Choose where PocketPad lives. Your personal layouts stay in your Windows profile.");content.Children.Add(new TextBlock{Text="INSTALLATION FOLDER",FontSize=9,Foreground=Brand.B("#7F92B8"),Margin=new Thickness(0,0,0,10)});var row=new DockPanel();var browse=new Button{Content="Browse",Margin=new Thickness(9,0,0,0)};DockPanel.SetDock(browse,Dock.Right);browse.Click+=(s,e)=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="Choose a parent folder for PocketPad";if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK){location.Text=Path.Combine(dialog.SelectedPath,"PocketPad");}}};row.Children.Add(browse);location=new TextBox{Text=target};row.Children.Add(location);content.Children.Add(row);desktop=new CheckBox{Content="Create a desktop shortcut",IsChecked=true,Margin=new Thickness(0,22,0,6)};content.Children.Add(desktop);bool installed=Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\ViGEmBus")!=null;driver=new CheckBox{Content="Install the virtual controller driver after setup",IsChecked=!installed,Visibility=installed?Visibility.Collapsed:Visibility.Visible};content.Children.Add(driver);content.Children.Add(new TextBlock{Text="A Start menu shortcut and Windows uninstall entry are included. The driver, if needed, asks for administrator approval.",TextWrapping=TextWrapping.Wrap,FontSize=10,Foreground=Brand.B("#7688AA"),Margin=new Thickness(0,17,0,0),LineHeight=17});next.Content="Install PocketPad";}
            if(!preview){content.RenderTransform=new TranslateTransform(12,0);content.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(200)));content.RenderTransform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(12,0,TimeSpan.FromMilliseconds(230)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
        }
        private async Task Advance(){
            if(finished){if(!uninstall){try{if(driver!=null&&driver.IsChecked==true){var p=Process.Start(new ProcessStartInfo(Path.Combine(target,"tools","ViGEmBus_1.22.0_x64_x86_arm64.exe")){UseShellExecute=true});await Task.Run(()=>p.WaitForExit());}if(launch!=null&&launch.IsChecked==true)Process.Start(Path.Combine(target,"PocketPad.exe"));}catch(Exception e){status.Text=e.Message;return;}}Window.Close();return;}
            if(!uninstall&&page==0){page=1;Render();return;}
            try{if(!uninstall)target=InstallCore.Validate(location.Text,false);else InstallCore.Validate(target,true);}catch(Exception e){status.Text=e.Message;return;}
            busy=true;back.Visibility=Visibility.Collapsed;next.IsEnabled=false;content.Children.Clear();step.Text=uninstall?"REMOVING APPLICATION":"03 / INSTALLING";Title(uninstall?"Removing PocketPad":"Installing PocketPad","Please keep this window open while the application files are prepared.");progress.Value=0;progress.Height=6;progress.Margin=new Thickness(0,30,0,22);content.Children.Add(progress);var file=new TextBlock{Foreground=Brand.B("#7F91B5"),TextWrapping=TextWrapping.Wrap,FontSize=10};content.Children.Add(file);var updates=new Progress<Tuple<double,string>>(p=>{progress.Value=p.Item1;file.Text=p.Item2;});
            try{if(uninstall){bool remove=deleteData.IsChecked==true;await Task.Run(()=>InstallCore.Uninstall(target,remove,test,updates));}else{var options=new InstallOptions{Target=target,Desktop=desktop.IsChecked==true,Test=test};await Task.Run(()=>InstallCore.Install(options,updates));}
                busy=false;finished=true;content.Children.Clear();step.Text=uninstall?"REMOVAL COMPLETE":"04 / READY";Title(uninstall?"PocketPad removed":"You’re ready to play",uninstall?"The application has been removed from this PC.":"Open the desktop app, connect your phone, and choose your controller layout.");if(!uninstall){launch=new CheckBox{Content="Launch PocketPad",IsChecked=true,Margin=new Thickness(0,16,0,0)};content.Children.Add(launch);Line("Your layouts are safe","Saved layouts stay in your Windows profile across updates.");}else Line("Personal data",deleteData.IsChecked==true?"Your saved layouts and preferences were removed.":"Your saved layouts and preferences have been kept.");status.Text=uninstall?"Uninstall complete":"Installed to "+target;next.Content="Finish";next.IsEnabled=true;
            }catch(Exception e){busy=false;next.IsEnabled=true;next.Content="Retry";status.Text=e.Message;content.Children.Clear();Render();status.Text=e.Message;}
        }
        public void Snapshot(string folder){Directory.CreateDirectory(folder);Window.ShowInTaskbar=false;Window.ShowActivated=false;Window.Left=-19000;Window.Top=-19000;Window.Loaded+=(s,e)=>{Window.UpdateLayout();Brand.Snapshot((FrameworkElement)Window.Content,Path.Combine(folder,uninstall?"uninstaller.png":"setup-welcome.png"));if(!uninstall){page=1;Render();Window.UpdateLayout();Brand.Snapshot((FrameworkElement)Window.Content,Path.Combine(folder,"setup-options.png"));}Window.Close();};}
    }
    public static class Entry {
        private static string Value(string[] args,string key,string fallback){int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
        [STAThread] public static int Main(string[] args){
            bool uninstall=args.Contains("--uninstall"),silent=args.Contains("--silent"),test=args.Contains("--test-install");
#if UNINSTALL
            uninstall=true;
#endif
            string root=Value(args,"--target",uninstall?AppDomain.CurrentDomain.BaseDirectory:InstallCore.DefaultTarget);
            try{
                if(uninstall&&!args.Contains("--detached")){
                    string current=Path.GetFullPath(Assembly.GetExecutingAssembly().Location),resolved=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                    if(current.StartsWith(resolved+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){
                        string temp=Path.Combine(Path.GetTempPath(),"PocketPad-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(current,temp);Process.Start(new ProcessStartInfo(temp,"--uninstall --detached --target \""+resolved+"\""+(silent?" --silent":"")+(test?" --test-install":"")){UseShellExecute=false,CreateNoWindow=true});return 0;
                    }
                }
                if(silent){var progress=new Progress<Tuple<double,string>>();if(uninstall)InstallCore.Uninstall(root,false,test,progress);else InstallCore.Install(new InstallOptions{Target=root,Desktop=false,Test=test},progress);return 0;}
                var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;var wizard=new SetupWindow(uninstall,root,test,args.Contains("--preview"));app.MainWindow=wizard.Window;if(args.Contains("--preview"))wizard.Snapshot(Value(args,"--output",Path.Combine(Environment.CurrentDirectory,"installer","qa")));app.Run(wizard.Window);return 0;
            }catch(Exception e){if(silent){File.WriteAllText(Path.Combine(Path.GetTempPath(),"PocketPad-setup-error.txt"),e.ToString());return 1;}MessageBox.Show(e.Message,"PocketPad Setup");return 1;}
        }
    }
}
