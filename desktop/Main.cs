using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly:AssemblyTitle("PocketPad Desktop")]
[assembly:AssemblyDescription("Native phone controller studio for Windows")]
[assembly:AssemblyCompany("Sanjay")]
[assembly:AssemblyCopyright("Copyright (c) 2026 Sanjay")]
[assembly:AssemblyProduct("PocketPad")]
[assembly:AssemblyVersion("2.2.0.0")]
[assembly:AssemblyFileVersion("2.2.0.0")]

namespace PocketPad {
    public static class Program {
        public static string Arg(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        [STAThread] public static int Main(string[] args){
            bool smoke=args.Contains("--smoke");Mutex mutex=null;
            if(!smoke){bool created;mutex=new Mutex(true,"Local\\PocketPadDesktop",out created);if(!created){MessageBox.Show("PocketPad is already open. Use its taskbar window.","PocketPad");mutex.Dispose();return 0;}}
            try{
                string root=Path.GetFullPath(Arg(args,"--root",AppDomain.CurrentDomain.BaseDirectory));
                string data=Path.GetFullPath(Arg(args,"--data-dir",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"PocketPad","data")));
                var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;
                var shell=new Shell(root,data,smoke,int.Parse(Arg(args,"--port","8765")),args.Contains("--mock"),Arg(args,"--output",Path.Combine(root,"desktop","build","qa")));
                app.Run(shell.Window);return shell.ExitCode;
            }catch(Exception e){if(smoke){string dir=Arg(args,"--output",Path.Combine(Environment.CurrentDirectory,"desktop","build","qa"));Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"error.txt"),e.ToString());}else MessageBox.Show(e.ToString(),"PocketPad could not start");return 1;}
            finally{if(mutex!=null){mutex.ReleaseMutex();mutex.Dispose();}}
        }
    }
    public sealed partial class Shell {
        public readonly Window Window;
        public int ExitCode=0;
        private readonly Engine engine;
        private readonly string root,output,prefsFile,draftFile;
        private readonly bool smoke,mock;
        private readonly int preferredPort;
        private readonly ControllerView canvas=new ControllerView(),monitor=new ControllerView {Editable=false};
        private readonly DispatcherTimer poll=new DispatcherTimer(),toastTimer=new DispatcherTimer();
        private HostInfo info;
        private LayoutModel draft;
        private int loadedSlot=-1;
        private bool dirty,busy,closing,closed,buildingInspector,smokeMotion;
        private string page="studio",remoteLayout="";
        private readonly List<string> undo=new List<string>(),redo=new List<string>();
        private readonly Dictionary<string,Border> buttonLights=new Dictionary<string,Border>();
        private readonly Dictionary<string,Tuple<ProgressBar,TextBlock>> axes=new Dictionary<string,Tuple<ProgressBar,TextBlock>>();
        private readonly List<ControlModel> library=new List<ControlModel>();
        private readonly Dictionary<string,string> libraryNames=new Dictionary<string,string>();
        private Point libraryDown;
        private bool libraryWasDragged;
        public Shell(string basePath,string dataPath,bool isSmoke,int port,bool testMode,string qaOutput){
            root=basePath;smoke=isSmoke;mock=testMode;preferredPort=port;output=qaOutput;engine=new Engine(root,dataPath);
            prefsFile=Path.Combine(Path.GetDirectoryName(dataPath),"desktop.json");draftFile=Path.Combine(dataPath,"desktop-draft.json");
            using(var stream=File.OpenRead(Path.Combine(root,"desktop","Shell.xaml")))Window=(Window)XamlReader.Load(stream);
            WindowChrome.SetWindowChrome(Window,new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(6),CornerRadius=new CornerRadius(12),GlassFrameThickness=new Thickness(0)});
            try{Window.Icon=new BitmapImage(new Uri(Path.Combine(root,"desktop","assets","PocketPad.ico")));}catch{}
            var logo=new BitmapImage(new Uri(Path.Combine(root,"desktop","assets","logo.png")));
            N<Image>("BrandLogo").Source=logo;N<Image>("LaunchLogo").Source=logo;
            N<Viewbox>("CanvasViewbox").Child=canvas;N<Viewbox>("MonitorCanvas").Child=monitor;
            N<Grid>("TitleBar").MouseLeftButtonDown+=(s,e)=>{DependencyObject node=e.OriginalSource as DependencyObject;while(node!=null){if(node is Button)return;node=VisualTreeHelper.GetParent(node);}try{if(e.ClickCount==2)ToggleMaximize();else Window.DragMove();}catch{}};
            Click("Minimize",()=>Window.WindowState=WindowState.Minimized);Click("Maximize",ToggleMaximize);Click("Close",()=>Window.Close());Click("LaunchClose",()=>Window.Close());
            Click("NavStudio",()=>Navigate("studio"));Click("NavConnect",()=>Navigate("connect"));Click("NavMonitor",()=>Navigate("monitor"));Click("NavSettings",()=>Navigate("settings"));
            Click("AndroidShortcut",()=>Navigate("mobile"));
            Click("Undo",()=>History(true));Click("Redo",()=>History(false));Click("Reset",Reset);Click("Save",SaveDialog);
            Click("Export",Export);Click("Import",Import);Click("Deselect",()=>{canvas.Selected=null;canvas.InvalidateVisual();Inspector();});
            Click("CopyLink",()=>{Clipboard.SetText(N<TextBox>("PairUrl").Text);Toast("Link copied.");});
            Click("Disconnect",async()=>{try{await engine.Action(new {type="disconnect"});await Refresh();}catch(Exception e){Toast(e.Message);}});
            Click("InstallDriver",()=>{try{Process.Start(new ProcessStartInfo(Path.Combine(root,"tools","ViGEmBus_1.22.0_x64_x86_arm64.exe")){UseShellExecute=true});Toast("Finish the driver installation, then restart PocketPad.");}catch(Exception e){Toast(e.Message);}});
            Click("Firewall",Firewall);Click("GameControllers",()=>Process.Start("control.exe","joy.cpl"));
            Click("OpenData",()=>Process.Start("explorer.exe",engine.DataPath));
            Click("OpenLogs",()=>{string path=Path.Combine(Path.GetDirectoryName(engine.DataPath),"logs");Directory.CreateDirectory(path);Process.Start("explorer.exe",path);});
            Click("OpenAndroid",()=>Process.Start("explorer.exe","/select,\""+Path.Combine(root,"PocketPad-Android.apk")+"\""));
            N<TextBlock>("DataLocation").Text=engine.DataPath;
            N<TextBox>("Search").TextChanged+=(s,e)=>RenderLibrary();
            N<ComboBox>("Network").SelectionChanged+=async(s,e)=>await PairQr();
            N<ComboBox>("PairPurpose").SelectionChanged+=async(s,e)=>await PairQr();
            N<CheckBox>("Snap").Checked+=(s,e)=>canvas.Snap=true;N<CheckBox>("Snap").Unchecked+=(s,e)=>canvas.Snap=false;
            N<CheckBox>("Animations").Checked+=(s,e)=>Preferences();N<CheckBox>("Animations").Unchecked+=(s,e)=>Preferences();
            N<CheckBox>("LivePreview").Checked+=(s,e)=>Preferences();N<CheckBox>("LivePreview").Unchecked+=(s,e)=>Preferences();
            N<Slider>("LayoutScale").PreviewMouseLeftButtonDown+=(s,e)=>Checkpoint();
            N<Slider>("LayoutScale").ValueChanged+=(s,e)=>{if(buildingInspector||draft==null)return;draft.scale=e.NewValue/100;Dirty();canvas.InvalidateVisual();};
            canvas.Selection+=c=>Inspector();canvas.EditStarted+=Checkpoint;canvas.Edited+=()=>{Dirty();Inspector();};
            canvas.AllowDrop=true;canvas.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent("PocketPad.Control")?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
            canvas.Drop+=(s,e)=>{string id=e.Data.GetData("PocketPad.Control") as string;ControlModel item=library.FirstOrDefault(c=>c.id==id);if(item!=null){Point p=e.GetPosition(canvas);AddControl(item,p.X,p.Y);e.Handled=true;}};
            Window.PreviewKeyDown+=Keyboard;
            poll.Interval=TimeSpan.FromMilliseconds(300);poll.Tick+=async(s,e)=>{if(busy||closing)return;await Refresh();};
            toastTimer.Interval=TimeSpan.FromSeconds(3.5);toastTimer.Tick+=(s,e)=>{toastTimer.Stop();N<Border>("Toast").Visibility=Visibility.Collapsed;};
            Window.Loaded+=async(s,e)=>await Start();Window.Closing+=OnClosing;
            LoadPreferences();InitializeExperience();BuildLibrary();BuildMonitor();Navigate("studio");
            if(smoke){Window.ShowInTaskbar=false;Window.ShowActivated=false;Window.Left=-19000;Window.Top=-19000;}
        }
        private T N<T>(string name) where T:class{return Window.FindName(name) as T;}
        private void Click(string name,Action action){N<Button>(name).Click+=(s,e)=>action();}
        private bool Animate {get{return N<CheckBox>("Animations").IsChecked==true&&(!smoke||smokeMotion);}}
        private void ToggleMaximize(){Window.WindowState=Window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;}
        private async Task Start(){
            if(Animate){var scale=N<Image>("LaunchLogo").RenderTransform as ScaleTransform;scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.8,1,TimeSpan.FromMilliseconds(700)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.8,1,TimeSpan.FromMilliseconds(700)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});N<ProgressBar>("LaunchProgress").BeginAnimation(ProgressBar.ValueProperty,new DoubleAnimation(0,88,TimeSpan.FromMilliseconds(1000)));}
            try{
                info=await engine.Start(preferredPort,mock);Adopt(info,true);
                if(!smoke){await Task.Delay(Animate?350:0);var overlay=N<Border>("LaunchOverlay");if(Animate){overlay.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(480)));await Task.Delay(490);}overlay.Visibility=Visibility.Collapsed;Reveal(N<Grid>("StudioPage"));poll.Start();if(!experience.tourComplete)StartGuide();}
                else{N<Border>("LaunchOverlay").Visibility=Visibility.Collapsed;await Smoke();}
            }catch(Exception e){N<TextBlock>("LaunchStatus").Text=e.Message;N<TextBlock>("LaunchStatus").TextWrapping=TextWrapping.Wrap;N<TextBlock>("LaunchStatus").MaxWidth=450;ExitCode=1;if(smoke){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());}}
            if(smoke&&!closed&&ExitCode!=0)await Exit();
        }
        private async Task Refresh(){
            busy=true;
            try{HostInfo latest=await engine.Info();Adopt(latest,false);}catch(Exception e){N<TextBlock>("EngineStatus").Text="Reconnecting…";N<TextBlock>("FooterStatus").Text=e.Message;SetConnection("reconnecting","The local engine is unavailable. Keep PocketPad open while it recovers.");}
            finally{busy=false;}
        }
        private void Adopt(HostInfo latest,bool first){
            info=latest;string config=Json.Write(new {latest.active,latest.presets});
            if(first||(!dirty&&config!=remoteLayout)){loadedSlot=info.active;draft=Json.Copy(info.presets[loadedSlot]);canvas.Selected=null;undo.Clear();redo.Clear();
                if(first&&File.Exists(draftFile)){try{var saved=Json.Read<DraftRecord>(File.ReadAllText(draftFile));if(saved.slot==loadedSlot){draft=saved.layout;dirty=true;}}catch{}}
                DrawEditor();RenderLibrary();Inspector();}
            remoteLayout=config;canvas.Layout=draft;
            if(N<CheckBox>("LivePreview").IsChecked==true)canvas.Input=info.input??new InputState();else canvas.Input=new InputState();canvas.InvalidateVisual();
            monitor.Layout=info.presets[info.active];monitor.Input=info.input??new InputState();monitor.InvalidateVisual();
            N<TextBlock>("EngineStatus").Text="Local engine online";N<System.Windows.Shapes.Ellipse>("EngineDot").Fill=Brand.B("#9FBAB0");
            N<TextBlock>("SidebarPhone").Text=info.connected?info.controllerName:info.paired?"Phone connected · paused":"Phone not connected";
            N<TextBlock>("FooterStatus").Text="Local engine · port "+engine.Port+"    /    "+(info.output=="ds4"?"PlayStation / DS4":"Xbox / XInput")+"    /    "+(info.driver?"Virtual controller ready":info.mock?"Test mode":"Driver setup needed");
            ConnectionStatus();
            N<TextBlock>("PhoneStatus").Text=info.connected?info.controllerName:info.paired?"Connected · ready to start":"Waiting for your phone";
            N<TextBlock>("DriverStatus").Text=(info.output=="ds4"?"PlayStation / DS4":"Xbox / XInput")+" · "+(info.driver?"Ready":info.mock?"Test mode":"Driver setup needed");
            N<Border>("DriverNotice").Visibility=String.IsNullOrEmpty(info.driverError)?Visibility.Collapsed:Visibility.Visible;N<TextBlock>("DriverError").Text=info.driverError??"";N<Button>("InstallDriver").Visibility=info.driver?Visibility.Collapsed:Visibility.Visible;N<Button>("Disconnect").IsEnabled=info.connected;
            if(first){N<ComboBox>("Network").ItemsSource=info.urls.Select(u=>new Uri(u).Host).ToList();N<ComboBox>("Network").SelectedIndex=0;N<ComboBox>("MobileNetwork").ItemsSource=info.urls.Select(u=>new Uri(u).Host).ToList();N<ComboBox>("MobileNetwork").SelectedIndex=0;}
            Monitor();
        }
        private void DrawEditor(){
            if(draft==null)return;canvas.Layout=draft;canvas.InvalidateVisual();N<TextBlock>("LayoutTitle").Text=draft.name;N<StackPanel>("Presets").Children.Clear();
            for(int i=0;i<4;i++){int slot=i;LayoutModel item=info.presets[i];var stack=new StackPanel();stack.Children.Add(new TextBlock{Text=item==null?"Custom "+(i-1):item.name,FontSize=11,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=107});stack.Children.Add(new TextBlock{Text=i<2?"FACTORY":item==null?"EMPTY SLOT":"SAVED",FontSize=8,Foreground=Brand.B("#6F7E9F"),Margin=new Thickness(0,5,0,0)});
                var b=new Button{Content=stack,Width=124,Padding=new Thickness(13,9,13,9),Margin=new Thickness(0,0,8,10),Background=TBrush(loadedSlot==i?"SelectedBg":"ButtonBg"),BorderBrush=TBrush(loadedSlot==i?"AccentBrush":"LineBrush")};b.Click+=async(s,e)=>{if(item==null){SaveDialog(slot);return;}if(dirty&&!Confirm("Discard unsaved edits?","Your saved custom layouts will remain available."))return;try{dirty=false;DeleteDraft();await engine.Action(new {type="activate",slot=slot});await Refresh();}catch(Exception ex){Toast(ex.Message);}};N<StackPanel>("Presets").Children.Add(b);}
            buildingInspector=true;N<Slider>("LayoutScale").Value=(draft.scale<=0?1:draft.scale)*100;buildingInspector=false;DraftUi();
        }
        private void Dirty(){dirty=true;try{File.WriteAllText(draftFile,Json.Write(new DraftRecord{slot=loadedSlot,layout=draft}));}catch{}DraftUi();}
        private void DraftUi(){N<TextBlock>("DraftBadge").Visibility=dirty?Visibility.Visible:Visibility.Collapsed;N<TextBlock>("LayoutTitle").Text=draft==null?"Layout":draft.name;N<TextBlock>("ScaleValue").Text=Math.Round((draft==null?1:draft.scale)*100)+"%";N<Button>("Undo").IsEnabled=undo.Count>0;N<Button>("Redo").IsEnabled=redo.Count>0;}
        private void DeleteDraft(){if(File.Exists(draftFile))File.Delete(draftFile);}
        private void Checkpoint(){if(draft==null)return;undo.Add(Json.Write(draft));if(undo.Count>50)undo.RemoveAt(0);redo.Clear();DraftUi();}
        private void History(bool back){var from=back?undo:redo;var to=back?redo:undo;if(from.Count==0)return;to.Add(Json.Write(draft));draft=Json.Read<LayoutModel>(from[from.Count-1]);from.RemoveAt(from.Count-1);canvas.Selected=null;Dirty();DrawEditor();Inspector();}
        private void Reset(){if(draft==null)return;if(dirty&&!Confirm("Reset this draft?","The last saved version of this layout will be restored."))return;Checkpoint();draft=Json.Copy(info.presets[loadedSlot]);canvas.Selected=null;Dirty();DrawEditor();Inspector();}
        private void Navigate(string target){
            page=target;string[] ids={"studio","connect","monitor","mobile","settings"};string[] pages={"StudioPage","ConnectPage","MonitorPage","MobilePage","SettingsPage"};string[] nav={"NavStudio","NavConnect","NavMonitor","NavMobile","NavSettings"};string[] titles={"Controller studio","Connect your phone","Live input","Mobile app","Preferences"};string[] subtitles={"Arrange your controls. Make room for the way you play.","One local connection, from your phone to your PC.","See the buttons, sticks and touch contacts arriving from your phone.","Download the Android companion and get connected.","Appearance, motion and your desktop application."};
            for(int i=0;i<ids.Length;i++){bool active=ids[i]==target;var element=N<FrameworkElement>(pages[i]);element.Visibility=active?Visibility.Visible:Visibility.Collapsed;N<Button>(nav[i]).Background=active?TBrush("SelectedBg"):Brushes.Transparent;N<Button>(nav[i]).Foreground=TBrush(active?"AccentLight":"MutedBrush");if(active){N<TextBlock>("PageTitle").Text=titles[i];N<TextBlock>("PageSubtitle").Text=subtitles[i];Reveal(element);}}
            if(target=="mobile")Window.Dispatcher.BeginInvoke(new Action(async()=>await MobileQr()));
        }
        private void Toast(string text){N<TextBlock>("ToastText").Text=text;var toast=N<Border>("Toast");toast.Visibility=Visibility.Visible;if(Animate)toast.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(150)));toastTimer.Stop();toastTimer.Start();}
        private async Task PairQr(){
            if(info==null||N<ComboBox>("Network").SelectedItem==null)return;
            try{string host=N<ComboBox>("Network").SelectedItem.ToString();bool app=N<ComboBox>("PairPurpose").SelectedIndex==1;string url="http://"+host+":"+engine.Port+(app?"/android":"/play?token="+info.token);N<TextBox>("PairUrl").Text=url;N<TextBlock>("PairHint").Text=app?"Scan with your phone camera to download and install the Android app.":"Open the Android app and scan this code. You can also use your phone camera.";
                byte[] bytes=await engine.Image("/api/qr.png?host="+Uri.EscapeDataString(host)+(app?"&target=android":""));var image=new BitmapImage();using(var memory=new MemoryStream(bytes)){image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=memory;image.EndInit();image.Freeze();}N<Image>("QrImage").Source=image;
            }catch(Exception e){Toast("QR code: "+e.Message);}
        }
        private void BuildLibrary(){
            string[] buttons={"A","B","X","Y","LB","RB","BACK","START","L3","R3","GUIDE","TOUCH_CLICK","UP","DOWN","LEFT","RIGHT"};string[] names={"A / Cross","B / Circle","X / Square","Y / Triangle","Left bumper","Right bumper","View / Create","Menu / Options","Left stick click","Right stick click","Home","Touchpad click","D-pad up","D-pad down","D-pad left","D-pad right"};
            for(int i=0;i<buttons.Length;i++){bool shoulder=i==4||i==5;AddLibrary(names[i],new ControlModel{id="lib-"+buttons[i],type="button",mapping=buttons[i],label=buttons[i],w=shoulder?178:84,h=shoulder?48:84,shape=shoulder?"rect":"round"});}
            AddLibrary("Left joystick",new ControlModel{id="lib-left",type="stick",mapping="left",label="L",w=192,h=192});AddLibrary("Right joystick",new ControlModel{id="lib-right",type="stick",mapping="right",label="R",w=192,h=192});AddLibrary("Directional pad",new ControlModel{id="lib-dpad",type="dpad",mapping="dpad",label="D-pad",w=190,h=190});
            AddLibrary("Left trigger",new ControlModel{id="lib-lt",type="trigger",mapping="lt",label="LT",w=178,h=48});AddLibrary("Right trigger",new ControlModel{id="lib-rt",type="trigger",mapping="rt",label="RT",w=178,h=48});AddLibrary("PS touchpad",new ControlModel{id="lib-touchpad",type="touchpad",mapping="touchpad",label="Touchpad",w=274,h=144});
            AddLibrary("Steering slider",new ControlModel{id="lib-steer",type="slider",mapping="lx",label="STEER",w=220,h=50});AddLibrary("Throttle slider",new ControlModel{id="lib-gas",type="slider",mapping="rt",label="GAS",w=200,h=50});AddLibrary("Brake slider",new ControlModel{id="lib-brake",type="slider",mapping="lt",label="BRAKE",w=200,h=50});AddLibrary("HUD text",new ControlModel{id="lib-text",type="label",mapping="",label="PLAYER ONE",w=190,h=38});RenderLibrary();
        }
        private void AddLibrary(string name,ControlModel model){library.Add(model);libraryNames[model.id]=name;}
        private void RenderLibrary(){
            var panel=N<WrapPanel>("Library");panel.Children.Clear();string query=N<TextBox>("Search").Text.ToLowerInvariant();
            foreach(ControlModel model in library){string name=libraryNames[model.id];if(!name.ToLowerInvariant().Contains(query)&&!model.mapping.ToLowerInvariant().Contains(query))continue;
                var stack=new StackPanel();stack.Children.Add(new LibraryGlyph(model,draft==null?"xbox":draft.theme));stack.Children.Add(new TextBlock{Text=name,FontSize=8,Foreground=Brand.B("#8F9DBA"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,0)});var b=new Button{Content=stack,Width=65,Padding=new Thickness(0,5,0,8),Margin=new Thickness(0,0,3,6),ToolTip=name,Background=Brand.B("#1B202C"),BorderBrush=Brand.B("#2D3548")};
                b.PreviewMouseLeftButtonDown+=(s,e)=>{libraryDown=e.GetPosition(Window);libraryWasDragged=false;};b.PreviewMouseMove+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(Window)-libraryDown).Length>6&&!libraryWasDragged){libraryWasDragged=true;DragDrop.DoDragDrop(b,new DataObject("PocketPad.Control",model.id),DragDropEffects.Copy);}};b.Click+=(s,e)=>{if(!libraryWasDragged)AddControl(model,500,240);libraryWasDragged=false;};panel.Children.Add(b);
            }
        }
        private void AddControl(ControlModel model,double x,double y){if(draft==null)return;if(draft.controls.Count>=100){Toast("Use up to 100 controls in a layout.");return;}Checkpoint();ControlModel c=Json.Copy(model);c.id="control-"+Guid.NewGuid().ToString("N");c.x=ControllerView.Clamp(x,c.w/2,1000-c.w/2);c.y=ControllerView.Clamp(y,c.h/2,480-c.h/2);draft.controls.Add(c);canvas.Selected=c;Dirty();canvas.InvalidateVisual();Inspector();}
        private StackPanel InspectorPanel {get{return N<StackPanel>("Inspector");}}
        private void Label(string text){InspectorPanel.Children.Add(new TextBlock{Text=text,Foreground=Brand.B("#7F8CAA"),FontSize=10,Margin=new Thickness(0,16,0,7)});}
        private TextBox Field(string label,string value,Action<string> change){Label(label);var field=new TextBox{Text=value};string before=value;Action commit=()=>{if(buildingInspector||field.Text==before)return;string next=field.Text;try{Checkpoint();change(next);before=next;Dirty();canvas.InvalidateVisual();}catch{field.Text=before;Toast("Enter a valid value for "+label.ToLowerInvariant()+".");}};field.LostKeyboardFocus+=(s,e)=>commit();field.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){commit();e.Handled=true;}};InspectorPanel.Children.Add(field);return field;}
        private ComboBox Choice(string label,string[] options,string selected,Action<string> change){Label(label);var combo=new ComboBox{ItemsSource=options,SelectedItem=selected};combo.SelectionChanged+=(s,e)=>{if(buildingInspector||combo.SelectedItem==null)return;Checkpoint();change(combo.SelectedItem.ToString());Dirty();canvas.InvalidateVisual();};InspectorPanel.Children.Add(combo);return combo;}
        private Button InspectorButton(string label,Action click){var button=new Button{Content=label,Margin=new Thickness(0,10,0,0)};button.Click+=(s,e)=>click();InspectorPanel.Children.Add(button);return button;}
        private double Number(string value,double low,double high){double n=Double.Parse(value,CultureInfo.InvariantCulture);if(Double.IsNaN(n)||Double.IsInfinity(n))throw new Exception();return ControllerView.Clamp(n,low,high);}
        private void Inspector(){
            if(draft==null)return;buildingInspector=true;InspectorPanel.Children.Clear();ControlModel c=canvas.Selected;N<TextBlock>("InspectorTitle").Text=c==null?"Layout settings":"Control settings";
            if(c==null){Field("Layout name",draft.name,v=>draft.name=v.Substring(0,Math.Min(40,v.Length)));Choice("Button style",new[]{"xbox","ps5","neon"},draft.theme,v=>{draft.theme=v;RenderLibrary();});Choice("PC controller output",new[]{"xbox","ds4"},draft.output??"xbox",v=>draft.output=v);
                InspectorPanel.Children.Add(new TextBlock{Text="DS4 enables native touchpad input. Xbox output uses the touchpad as a mouse.",TextWrapping=TextWrapping.Wrap,FontSize=10,Foreground=Brand.B("#7785A4"),Margin=new Thickness(0,10,0,0)});
                Field("Accent color",draft.accent,v=>{Brand.B(v);draft.accent=v;});var grid=new CheckBox{Content="Background grid",IsChecked=draft.grid,Margin=new Thickness(0,17,0,3)};grid.Click+=(s,e)=>{Checkpoint();draft.grid=grid.IsChecked==true;Dirty();canvas.InvalidateVisual();};InspectorPanel.Children.Add(grid);
                InspectorButton("Clear canvas",()=>{if(Confirm("Clear this canvas?","You can undo this change.")){Checkpoint();draft.controls.Clear();Dirty();canvas.InvalidateVisual();}});
                if(loadedSlot>1)InspectorButton("Delete custom slot",async()=>{if(!Confirm("Delete saved layout?","This custom slot will become empty."))return;try{dirty=false;DeleteDraft();await engine.Action(new {type="delete",slot=loadedSlot});await Refresh();}catch(Exception e){Toast(e.Message);}});
            }else{
                Field("Label",c.label,v=>c.label=v.Substring(0,Math.Min(30,v.Length)));
                string[] mappings=c.type=="button"?new[]{"A","B","X","Y","LB","RB","BACK","START","L3","R3","GUIDE","TOUCH_CLICK","UP","DOWN","LEFT","RIGHT"}:c.type=="stick"?new[]{"left","right"}:c.type=="trigger"?new[]{"lt","rt"}:c.type=="slider"?new[]{"lx","ly","rx","ry","lt","rt"}:c.type=="dpad"?new[]{"dpad"}:c.type=="touchpad"?new[]{"touchpad"}:new[]{""};
                Choice("Gamepad mapping",mappings,c.mapping,v=>c.mapping=v);
                Label("Size · "+Math.Round(c.w)+" × "+Math.Round(c.h));var size=new Slider{Minimum=50,Maximum=175,Value=100};double bw=c.w,bh=c.h;size.PreviewMouseLeftButtonDown+=(s,e)=>Checkpoint();size.ValueChanged+=(s,e)=>{if(buildingInspector)return;c.w=ControllerView.Clamp(Math.Round(bw*size.Value/100),24,600);c.h=ControllerView.Clamp(Math.Round(bh*size.Value/100),24,460);Dirty();canvas.InvalidateVisual();};size.PreviewMouseLeftButtonUp+=(s,e)=>Inspector();InspectorPanel.Children.Add(size);
                Field("Width",c.w.ToString(CultureInfo.InvariantCulture),v=>c.w=Number(v,24,600));Field("Height",c.h.ToString(CultureInfo.InvariantCulture),v=>c.h=Number(v,24,460));Field("Position X",c.x.ToString("0",CultureInfo.InvariantCulture),v=>c.x=Number(v,c.w/2,1000-c.w/2));Field("Position Y",c.y.ToString("0",CultureInfo.InvariantCulture),v=>c.y=Number(v,c.h/2,480-c.h/2));
                if(c.type=="button")Choice("Shape",new[]{"round","rect"},c.shape??"round",v=>c.shape=v);
                Field("Color (hex or name)",String.IsNullOrEmpty(c.color)?"#D9DAE6":c.color,v=>{Brand.B(v);c.color=v;});Field("Opacity · 0.15 to 1",c.opacity.ToString(CultureInfo.InvariantCulture),v=>c.opacity=Number(v,.15,1));if(c.type=="stick")Field("Deadzone · 0 to 0.5",c.deadzone.ToString(CultureInfo.InvariantCulture),v=>c.deadzone=Number(v,0,.5));
                InspectorButton("Duplicate control",()=>AddControl(c,c.x+30,c.y+30));InspectorButton("Delete control",DeleteSelected);InspectorButton("Done",()=>{canvas.Selected=null;canvas.InvalidateVisual();Inspector();});
            }
            buildingInspector=false;
        }
        private void DeleteSelected(){if(canvas.Selected==null)return;Checkpoint();draft.controls.Remove(canvas.Selected);canvas.Selected=null;Dirty();canvas.InvalidateVisual();Inspector();}
        private void Keyboard(object sender,KeyEventArgs e){
            if(System.Windows.Input.Keyboard.FocusedElement is TextBox||System.Windows.Input.Keyboard.FocusedElement is ComboBox||System.Windows.Input.Keyboard.FocusedElement is Slider)return;
            bool control=(System.Windows.Input.Keyboard.Modifiers&ModifierKeys.Control)!=0;
            if(control&&e.Key==Key.Z){History(true);e.Handled=true;return;}
            if(control&&e.Key==Key.Y){History(false);e.Handled=true;return;}
            if(e.Key==Key.Delete){DeleteSelected();e.Handled=true;return;}
            if(e.Key==Key.Escape){canvas.Selected=null;canvas.InvalidateVisual();Inspector();return;}
            if(canvas.Selected!=null&&new[]{Key.Left,Key.Right,Key.Up,Key.Down}.Contains(e.Key)){
                Checkpoint();double step=(System.Windows.Input.Keyboard.Modifiers&ModifierKeys.Shift)!=0?10:1;
                if(e.Key==Key.Left)canvas.Selected.x-=step;if(e.Key==Key.Right)canvas.Selected.x+=step;if(e.Key==Key.Up)canvas.Selected.y-=step;if(e.Key==Key.Down)canvas.Selected.y+=step;
                canvas.Selected.x=ControllerView.Clamp(canvas.Selected.x,canvas.Selected.w/2,1000-canvas.Selected.w/2);canvas.Selected.y=ControllerView.Clamp(canvas.Selected.y,canvas.Selected.h/2,480-canvas.Selected.h/2);Dirty();canvas.InvalidateVisual();Inspector();e.Handled=true;
            }
        }
        private void Export(){if(draft==null)return;var dialog=new SaveFileDialog{Filter="PocketPad layout (*.json)|*.json",FileName="PocketPad-"+draft.name+".json"};if(dialog.ShowDialog(Window)==true){File.WriteAllText(dialog.FileName,Json.Write(draft));Toast("Layout exported.");}}
        private void Import(){var dialog=new OpenFileDialog{Filter="PocketPad layout (*.json)|*.json"};if(dialog.ShowDialog(Window)!=true)return;try{var layout=Json.Read<LayoutModel>(File.ReadAllText(dialog.FileName));if(layout==null||layout.controls==null||layout.controls.Count>100)throw new Exception();foreach(ControlModel c in layout.controls)if(c.w<24||c.w>600||c.h<24||c.h>460||!new[]{"button","stick","dpad","trigger","slider","touchpad","label"}.Contains(c.type))throw new Exception();Checkpoint();draft=layout;if(draft.scale<=0)draft.scale=1;canvas.Selected=null;Dirty();DrawEditor();Inspector();Toast("Layout imported. Save it to a custom slot.");}catch{Toast("That file is not a valid PocketPad layout.");}}
        private Window Dialog(string title,int width,int height){var dialog=new Window{Title=title,Owner=Window,Width=width,Height=height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Brand.B("#191E2B"),Foreground=Brand.B("#E4E8F4"),FontFamily=new FontFamily("Segoe UI"),ShowInTaskbar=false};dialog.Resources.MergedDictionaries.Add(Window.Resources);return dialog;}
        private bool Confirm(string title,string message){var dialog=Dialog(title,410,210);var panel=new StackPanel{Margin=new Thickness(24)};panel.Children.Add(new TextBlock{Text=title,FontSize=19,FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap,Foreground=Brand.B("#98A5C1"),Margin=new Thickness(0,13,0,22)});var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button{Content="Cancel",Margin=new Thickness(0,0,8,0)};cancel.Click+=(s,e)=>dialog.DialogResult=false;var ok=new Button{Content="Continue",Style=Window.FindResource("Primary") as Style};ok.Click+=(s,e)=>dialog.DialogResult=true;actions.Children.Add(cancel);actions.Children.Add(ok);panel.Children.Add(actions);dialog.Content=panel;return dialog.ShowDialog()==true;}
        private void SaveDialog(){SaveDialog(loadedSlot>1?loadedSlot:(info!=null&&info.presets[2]!=null?3:2));}
        private void SaveDialog(int slot){if(draft==null)return;var dialog=Dialog("Save layout",440,360);var panel=new StackPanel{Margin=new Thickness(26)};panel.Children.Add(new TextBlock{Text="Save layout",FontSize=21,FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text="Save your edits in one of the two custom slots.",Foreground=Brand.B("#8C9AB8"),Margin=new Thickness(0,10,0,20)});var name=new TextBox{Text=loadedSlot<2?draft.name+" custom":draft.name,MaxLength=40};panel.Children.Add(name);var select=new ComboBox{ItemsSource=new[]{"Custom 1","Custom 2"},SelectedIndex=slot-2,Margin=new Thickness(0,15,0,0)};panel.Children.Add(select);var warning=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=Brand.B("#8D9AB8"),FontSize=11,Margin=new Thickness(0,12,0,17)};Action update=()=>warning.Text=info.presets[select.SelectedIndex+2]==null?"This slot is empty.":"This replaces “"+info.presets[select.SelectedIndex+2].name+"”.";select.SelectionChanged+=(s,e)=>update();update();panel.Children.Add(warning);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=new Button{Content="Cancel",Margin=new Thickness(0,0,8,0)};cancel.Click+=(s,e)=>dialog.Close();var save=new Button{Content="Save layout",Style=Window.FindResource("Primary") as Style};save.Click+=async(s,e)=>{try{save.IsEnabled=false;draft.name=String.IsNullOrWhiteSpace(name.Text)?"Custom layout":name.Text.Trim();await SaveTo(select.SelectedIndex+2);dialog.Close();}catch(Exception ex){save.IsEnabled=true;warning.Text=ex.Message;}};actions.Children.Add(cancel);actions.Children.Add(save);panel.Children.Add(actions);dialog.Content=panel;dialog.ShowDialog();}
        private async Task SaveTo(int slot){await engine.Action(new {type="save",slot=slot,layout=draft});dirty=false;DeleteDraft();remoteLayout="";await Refresh();Toast("Layout saved. Your phone is up to date.");}
        private void BuildMonitor(){string[] buttons={"A","B","X","Y","LB","RB","BACK","START","L3","R3","GUIDE","UP","DOWN","LEFT","RIGHT","TOUCH_CLICK"};foreach(string key in buttons){var border=new Border{Background=Brand.B("#202737"),CornerRadius=new CornerRadius(6),BorderBrush=Brand.B("#343F58"),BorderThickness=new Thickness(1),Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,0,7,7),Child=new TextBlock{Text=key,FontSize=10,Foreground=Brand.B("#A6B4D1")}};buttonLights[key]=border;N<WrapPanel>("InputButtons").Children.Add(border);}foreach(string key in new[]{"lx","ly","rx","ry","lt","rt"}){var panel=new StackPanel{Margin=new Thickness(0,0,24,18)};var dock=new DockPanel();dock.Children.Add(new TextBlock{Text=key.ToUpperInvariant(),Foreground=Brand.B("#94A5C7"),FontSize=10});var number=new TextBlock{Text="0.00",Foreground=Brand.B("#7D8CAC"),HorizontalAlignment=HorizontalAlignment.Right,FontSize=10};dock.Children.Add(number);panel.Children.Add(dock);var progress=new ProgressBar{Minimum=key.EndsWith("t")?0:-1,Maximum=1,Value=0,Height=5,Margin=new Thickness(0,9,0,0)};panel.Children.Add(progress);axes[key]=Tuple.Create(progress,number);N<UniformGrid>("InputAxes").Children.Add(panel);}}
        private void Monitor(){var state=info.input??new InputState();N<TextBlock>("MonitorStatus").Text=info.connected?"Receiving phone input":"Waiting for phone";foreach(var item in buttonLights){bool on=item.Key=="TOUCH_CLICK"?state.touchClick:state.buttons.Contains(item.Key);item.Value.Background=Brand.B(on?"#565F9E":"#202737");}double[] values={state.lx,state.ly,state.rx,state.ry,state.lt,state.rt};string[] keys={"lx","ly","rx","ry","lt","rt"};for(int i=0;i<keys.Length;i++){axes[keys[i]].Item1.Value=values[i];axes[keys[i]].Item2.Text=values[i].ToString("0.00",CultureInfo.InvariantCulture);}N<TextBlock>("TouchStatus").Text="Touchpad · "+state.touches.Count+" / 2 contacts · Click "+(state.touchClick?"pressed":"released")+String.Join("",state.touches.Select(t=>"    #"+t.id+" ("+t.x.ToString("0.00")+", "+t.y.ToString("0.00")+")"));}
        private void Firewall(){try{string exe=Path.Combine(root,"runtime","python.exe");Process.Start(new ProcessStartInfo("netsh.exe","advfirewall firewall add rule name=\"PocketPad Local Controller\" dir=in action=allow program=\""+exe+"\" protocol=TCP localport="+engine.Port+" profile=private"){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});Toast("Windows will request administrator approval for the network rule.");}catch(Exception e){Toast(e.Message);}}
        private void LoadPreferences(){try{experience=Json.Read<DesktopPreferences>(File.ReadAllText(prefsFile))??new DesktopPreferences();}catch{}N<CheckBox>("Animations").IsChecked=experience.animations;N<CheckBox>("LivePreview").IsChecked=experience.live;preferencesLoaded=true;}
        private void Preferences(){if(!preferencesLoaded)return;try{experience.animations=N<CheckBox>("Animations").IsChecked==true;experience.live=N<CheckBox>("LivePreview").IsChecked==true;Window.Resources["MenuMotion"]=experience.animations?PopupAnimation.Fade:PopupAnimation.None;if(!experience.animations)StopMotion(N<Grid>("RootGrid"));ConnectionMotion();File.WriteAllText(prefsFile,Json.Write(experience));}catch{}}
        private async void OnClosing(object sender,CancelEventArgs e){if(closed)return;e.Cancel=true;if(closing)return;if(!smoke&&dirty&&!Confirm("Close with unsaved edits?","Your draft is kept and can be restored next time."))return;await Exit();}
        private async Task Exit(){closing=true;poll.Stop();await engine.Stop();engine.Dispose();closed=true;Window.Close();}
        private async Task Smoke(){
            try{Directory.CreateDirectory(output);Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),Path.Combine(output,"desktop-studio.png"));
                string original=Json.Write(draft);int originalSlot=loadedSlot;Checkpoint();AddControl(library.First(c=>c.type=="touchpad"),500,200);canvas.Selected.w=320;canvas.Selected.h=160;Inspector();draft.name="Native smoke layout";await SaveTo(2);HostInfo stored=await engine.Info();if(stored.presets[2].controls.Last().w!=320||stored.presets[2].name!="Native smoke layout")throw new Exception("Native layout saving failed.");
                await engine.Action(new{type="activate",slot=1});await Refresh();Navigate("studio");Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),Path.Combine(output,"desktop-ps5.png"));smokeMotion=true;Navigate("connect");await PairQr();await Task.Delay(300);Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),Path.Combine(output,"desktop-connect.png"));if(N<Image>("QrImage").Source==null)throw new Exception("Native QR rendering failed.");smokeMotion=false;Navigate("monitor");Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),Path.Combine(output,"desktop-input.png"));Navigate("settings");Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),Path.Combine(output,"desktop-settings.png"));File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: native WPF rendering, controller editing, custom slot save, PS5 switching, animated transition, pairing QR, input monitor and preferences.");
            }catch(Exception e){ExitCode=1;File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());}
            if(ExitCode==0){try{await CheckExperience();File.AppendAllText(Path.Combine(output,"result.txt"),"\nPASS: three themes, OLED black, visible APK download page/QR, interactive walkthrough and complete motion disable.");}catch(Exception ex){ExitCode=1;File.WriteAllText(Path.Combine(output,"error.txt"),ex.ToString());}}
            await Exit();
        }
        public sealed class DraftRecord {public int slot{get;set;} public LayoutModel layout{get;set;}}
    }
}
