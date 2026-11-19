using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace PocketPad {
    public sealed class DesktopPreferences {
        public bool animations{get;set;} public bool live{get;set;} public string appearance{get;set;} public bool tourComplete{get;set;}
        public DesktopPreferences(){animations=true;live=true;appearance="matte";}
    }
    public sealed partial class Shell {
        private DesktopPreferences experience=new DesktopPreferences();
        private bool preferencesLoaded,everPaired;
        private string connectionVisual="",appearance="matte";
        private DateTime connectionLost=DateTime.MinValue;
        private int guideStep;
        private Button hovered;
        private Brush TBrush(string key){return Window.FindResource(key) as Brush;}
        private void InitializeExperience(){
            Window.Resources["MenuMotion"]=experience.animations?System.Windows.Controls.Primitives.PopupAnimation.Fade:System.Windows.Controls.Primitives.PopupAnimation.None;
            N<Image>("MobileLogo").Source=N<Image>("BrandLogo").Source;
            var choice=N<ComboBox>("AppearanceChoice");choice.ItemsSource=new[]{"Matte black","OLED black","Translucent"};choice.SelectedIndex=experience.appearance=="oled"?1:experience.appearance=="glass"?2:0;
            choice.SelectionChanged+=(s,e)=>{ApplyAppearance(choice.SelectedIndex==1?"oled":choice.SelectedIndex==2?"glass":"matte");Preferences();};
            ApplyAppearance(experience.appearance);
            Click("NavMobile",()=>Navigate("mobile"));Click("ReplayGuide",StartGuide);Click("GuideSkip",FinishGuide);Click("GuideNext",()=>{if(guideStep==3)FinishGuide();else{guideStep++;RenderGuide();}});Click("GuideBack",()=>{guideStep=Math.Max(0,guideStep-1);RenderGuide();});
            Click("MobileGoPair",()=>{Navigate("connect");N<ComboBox>("PairPurpose").SelectedIndex=0;});
            Click("CopyMobileLink",()=>{Clipboard.SetText(N<TextBox>("MobileDownloadUrl").Text);Toast("Android download link copied.");});
            Click("SaveMobileApk",()=>{var dialog=new SaveFileDialog{Filter="Android application (*.apk)|*.apk",FileName="PocketPad-Android.apk"};if(dialog.ShowDialog(Window)==true){File.Copy(System.IO.Path.Combine(root,"PocketPad-Android.apk"),dialog.FileName,true);Toast("Android APK saved.");}});
            Click("ShowMobileApk",()=>System.Diagnostics.Process.Start("explorer.exe","/select,\""+System.IO.Path.Combine(root,"PocketPad-Android.apk")+"\""));
            N<ComboBox>("MobileNetwork").SelectionChanged+=async(s,e)=>await MobileQr();
            Window.PreviewMouseMove+=(s,e)=>{
                DependencyObject node=e.OriginalSource as DependencyObject;Button button=null;
                while(node!=null){button=node as Button;if(button!=null)break;try{node=VisualTreeHelper.GetParent(node);}catch{break;}}
                if(hovered==button)return;
                if(hovered!=null){hovered.Effect=null;if(Animate){var scale=hovered.RenderTransform as ScaleTransform;if(scale!=null){scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(180)));scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(180)));}}else hovered.RenderTransform=Transform.Identity;}
                hovered=button;if(button==null||!Animate)return;
                button.RenderTransformOrigin=new Point(.5,.5);button.RenderTransform=new ScaleTransform(1,1);
                var transform=(ScaleTransform)button.RenderTransform;transform.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1.035,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});transform.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1.035,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
                var tone=(TBrush("AccentBrush") as SolidColorBrush).Color;button.Effect=new DropShadowEffect{Color=tone,ShadowDepth=0,BlurRadius=18,Opacity=.17};
            };
        }
        private void ApplyAppearance(string value){
            appearance=new[]{"matte","oled","glass"}.Contains(value)?value:"matte";experience.appearance=appearance;
            string[] keys={"WindowBg","MainBg","SidebarBg","TitleBg","FooterBg","SplashBg","CardBg","CanvasBg","StatusBg","ButtonBg","FieldBg","PopupBg","ToastBg","AccentBrush","AccentLight","SelectedBg","LineBrush","MutedBrush","TextBrush"};
            string[] matte={"#111216","#111216","#16191F","#13151B","#141820","#111216","#191C25","#121419","#212735","#202634","#141820","#252C3B","#30394E","#A7ADFF","#DADEFF","#2A3049","#343C50","#8F9CB8","#EDEFF7"};
            string[] oled={"#000000","#000000","#040807","#000000","#030807","#000000","#080E0C","#000000","#0C201B","#0A1713","#020805","#0C211C","#16352B","#7FE3CF","#D3FFF2","#102C24","#244B40","#8BAE9F","#E5FFF3"};
            string[] glass={"#101523","#101523","#CC192135","#B31D263D","#B318233A","#101523","#802B3853","#55131B2D","#88344161","#A333405C","#88131F35","#E6273551","#DD3B496A","#C3B3FF","#EBE3FF","#88483F73","#667C82B0","#A7B4D2","#F1EEFF"};
            string[] colors=appearance=="oled"?oled:appearance=="glass"?glass:matte;
            for(int i=0;i<keys.Length;i++)Window.Resources[keys[i]]=Brand.B(colors[i]);
            if(appearance=="glass"){var gradient=new RadialGradientBrush((Color)ColorConverter.ConvertFromString("#2F3553"),(Color)ColorConverter.ConvertFromString("#0F1727"));gradient.Center=new Point(.8,.05);gradient.RadiusX=1;gradient.RadiusY=1.2;Window.Resources["MainBg"]=gradient;}
            canvas.Surface=TBrush("CanvasBg");monitor.Surface=TBrush("CanvasBg");canvas.Tone=monitor.Tone=TBrush("AccentBrush");canvas.InvalidateVisual();monitor.InvalidateVisual();
            N<TextBlock>("AppearanceHint").Text=appearance=="oled"?"True black surfaces with mint accents. Ideal for an OLED screen.":appearance=="glass"?"Layered translucent surfaces and soft lavender accents.":"Matte charcoal surfaces with a cool violet accent.";
            if(draft!=null){DrawEditor();RenderLibrary();}if(preferencesLoaded)Navigate(page);
        }
        private void Reveal(FrameworkElement element){
            if(!Animate){element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;element.RenderTransform=Transform.Identity;return;}
            element.RenderTransformOrigin=new Point(.5,.5);var group=new TransformGroup();var scale=new ScaleTransform(.96,.96);var translation=new TranslateTransform(28,20);group.Children.Add(scale);group.Children.Add(translation);element.RenderTransform=group;
            element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(440)));
            translation.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(28,0,TimeSpan.FromMilliseconds(620)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            translation.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(20,0,TimeSpan.FromMilliseconds(620)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.96,1,TimeSpan.FromMilliseconds(650)){EasingFunction=new BackEase{Amplitude=.25,EasingMode=EasingMode.EaseOut}});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.96,1,TimeSpan.FromMilliseconds(650)){EasingFunction=new BackEase{Amplitude=.25,EasingMode=EasingMode.EaseOut}});
        }
        private void StopMotion(DependencyObject rootNode){
            var ui=rootNode as UIElement;if(ui!=null){ui.BeginAnimation(UIElement.OpacityProperty,null);ui.Opacity=1;ui.RenderTransform=Transform.Identity;if(ui is Button)ui.Effect=null;}
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(rootNode);i++)StopMotion(VisualTreeHelper.GetChild(rootNode,i));
        }
        private void ConnectionMotion(){
            var halo=N<Ellipse>("ConnectionHalo");halo.BeginAnimation(UIElement.OpacityProperty,null);halo.RenderTransform=new ScaleTransform(1,1);halo.Opacity=.6;
            if(!Animate)return;
            bool ready=connectionVisual=="ready";var pulse=new DoubleAnimation(ready ? .75 : .25,ready?1.3:1.15,TimeSpan.FromMilliseconds(ready?1250:850)){RepeatBehavior=RepeatBehavior.Forever,AutoReverse=!ready};
            var scale=(ScaleTransform)halo.RenderTransform;scale.BeginAnimation(ScaleTransform.ScaleXProperty,pulse);scale.BeginAnimation(ScaleTransform.ScaleYProperty,pulse);
            halo.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(ready ? .9 : .3,ready?0:.95,TimeSpan.FromMilliseconds(ready?1250:850)){RepeatBehavior=RepeatBehavior.Forever,AutoReverse=!ready});
        }
        private void SetConnection(string state,string detail){
            bool changed=connectionVisual!=state;connectionVisual=state;
            N<TextBlock>("ConnectionHeading").Text=state=="ready"?"Ready":state=="reconnecting"?"Reconnecting":"Connecting";
            N<TextBlock>("ConnectionGlyph").Text=state=="ready"?"✓":state=="reconnecting"?"↻":"⌁";N<TextBlock>("ConnectionDetail").Text=detail;
            N<TextBlock>("SessionBadge").Text=state=="ready"?"PHONE READY":state=="reconnecting"?"RECONNECTING":"WAITING FOR PHONE";
            if(changed){ConnectionMotion();if(Animate)Reveal(N<TextBlock>("ConnectionHeading"));}
        }
        private void ConnectionStatus(){
            if(info==null)return;
            if(info.connected||info.paired){everPaired=true;SetConnection("ready",!info.driver&&!info.mock?"Phone connected. Complete driver setup to send gamepad input.":!info.connected?"Phone connected. Tap Start controller on your phone to play.":"Phone connected. The virtual controller is ready.");}
            else{if(everPaired&&connectionVisual=="ready")connectionLost=DateTime.UtcNow;bool recovering=everPaired&&(DateTime.UtcNow-connectionLost).TotalSeconds<30;SetConnection(recovering?"reconnecting":"connecting",recovering?"Phone connection interrupted. Keep the companion app open.":"Open PocketPad on your phone and scan the pairing QR.");}
        }
        private async Task MobileQr(){
            if(info==null||N<ComboBox>("MobileNetwork").SelectedItem==null)return;
            try{string host=N<ComboBox>("MobileNetwork").SelectedItem.ToString();N<TextBox>("MobileDownloadUrl").Text="http://"+host+":"+engine.Port+"/android";byte[] bytes=await engine.Image("/api/qr.png?host="+Uri.EscapeDataString(host)+"&target=android");var bitmap=new System.Windows.Media.Imaging.BitmapImage();using(var stream=new MemoryStream(bytes)){bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}N<Image>("MobileQr").Source=bitmap;long size=new FileInfo(System.IO.Path.Combine(root,"PocketPad-Android.apk")).Length;N<TextBlock>("MobileVersion").Text="ANDROID 1.2  ·  "+(size/1024.0/1024).ToString("0.0")+" MB APK";}
            catch(Exception e){Toast("Mobile download: "+e.Message);}
        }
        private void StartGuide(){guideStep=0;N<Border>("WalkthroughCard").Visibility=Visibility.Visible;RenderGuide();}
        private void FinishGuide(){N<Border>("WalkthroughCard").Visibility=Visibility.Collapsed;experience.tourComplete=true;Preferences();}
        private void RenderGuide(){
            string[] titles={"Pair your phone","Make controls fit","Touch, swipe, click","Tilt and recenter"};
            string[] descriptions={"The Mobile app page is always in the sidebar. Scan its download QR with your phone camera, install the APK, then scan Connect phone’s pairing QR from the app.","Drag controls in the studio. Select one to resize it from its corner or its Size slider. Practice below without changing a saved layout.","The PS5 touchpad tracks two fingers and a separate click. On your phone, try a two-finger tap. Move over this practice pad; Shift adds a second contact.","In the Android app, choose Gyro, then steering or aiming. Hold your phone comfortably and tap Recenter. This practice slider never controls a game."};
            Navigate(guideStep==0?"mobile":guideStep==2?"monitor":"studio");
            N<TextBlock>("GuideStep").Text="GETTING STARTED · "+(guideStep+1)+" / 4";N<TextBlock>("GuideTitle").Text=titles[guideStep];N<TextBlock>("GuideText").Text=descriptions[guideStep];N<Button>("GuideBack").IsEnabled=guideStep>0;N<Button>("GuideNext").Content=guideStep==3?"Finish":"Next";N<TextBlock>("GuideFeedback").Text="Practice only · no game input";
            var practice=N<StackPanel>("GuidePractice");practice.Children.Clear();
            if(guideStep==0){var button=new Button{Content="Show controller pairing QR"};button.Click+=(s,e)=>Navigate("connect");practice.Children.Add(button);}
            else if(guideStep==1){var sample=new Border{Width=55,Height=55,CornerRadius=new CornerRadius(50),Background=TBrush("SelectedBg"),BorderBrush=TBrush("AccentBrush"),BorderThickness=new Thickness(2),Child=new TextBlock{Text="×",FontSize=27,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};practice.Children.Add(sample);var slider=new Slider{Minimum=40,Maximum=95,Value=55,Margin=new Thickness(0,10,0,0)};slider.ValueChanged+=(s,e)=>{sample.Width=sample.Height=e.NewValue;N<TextBlock>("GuideFeedback").Text=Math.Round(e.NewValue)+" × "+Math.Round(e.NewValue)+" · practice only";};practice.Children.Add(slider);}
            else if(guideStep==2){var pad=new Canvas{Width=310,Height=90,Background=TBrush("SelectedBg"),ClipToBounds=true};var first=new Ellipse{Width=16,Height=16,Fill=TBrush("AccentBrush")};var second=new Ellipse{Width=16,Height=16,Fill=TBrush("AccentLight"),Visibility=Visibility.Collapsed};pad.Children.Add(first);pad.Children.Add(second);pad.MouseMove+=(s,e)=>{Point p=e.GetPosition(pad);Canvas.SetLeft(first,p.X-8);Canvas.SetTop(first,p.Y-8);bool two=(System.Windows.Input.Keyboard.Modifiers&ModifierKeys.Shift)!=0;second.Visibility=two?Visibility.Visible:Visibility.Collapsed;Canvas.SetLeft(second,310-p.X-8);Canvas.SetTop(second,90-p.Y-8);N<TextBlock>("GuideFeedback").Text=(two?"2 / 2 fingers":"1 / 2 fingers")+" · practice only";};practice.Children.Add(pad);}
            else{var track=new Canvas{Width=310,Height=45,Background=TBrush("FieldBg")};var dot=new Ellipse{Width=25,Height=25,Fill=TBrush("AccentBrush")};Canvas.SetLeft(dot,142);Canvas.SetTop(dot,10);track.Children.Add(dot);practice.Children.Add(track);var slider=new Slider{Minimum=-30,Maximum=30,Value=0};slider.ValueChanged+=(s,e)=>{Canvas.SetLeft(dot,142+e.NewValue*3);N<TextBlock>("GuideFeedback").Text="Tilt "+Math.Round(e.NewValue)+"° · practice only";};practice.Children.Add(slider);var center=new Button{Content="Recenter"};center.Click+=(s,e)=>slider.Value=0;practice.Children.Add(center);}
            Reveal(N<Border>("WalkthroughCard"));
        }
        private async Task CheckExperience(){
            ApplyAppearance("oled");if(((SolidColorBrush)TBrush("CanvasBg")).Color!=Colors.Black)throw new Exception("OLED theme does not use true black.");Navigate("studio");Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),System.IO.Path.Combine(output,"desktop-oled.png"));
            ApplyAppearance("glass");Navigate("mobile");await MobileQr();Window.UpdateLayout();Brand.Snapshot(N<Border>("RootBorder"),System.IO.Path.Combine(output,"desktop-mobile-app.png"));if(N<Image>("MobileQr").Source==null)throw new Exception("Dedicated mobile download QR is missing.");
            ApplyAppearance("matte");StartGuide();guideStep=1;RenderGuide();((Slider)N<StackPanel>("GuidePractice").Children[1]).Value=85;if(!N<TextBlock>("GuideFeedback").Text.Contains("85"))throw new Exception("Interactive walkthrough practice failed.");FinishGuide();
            N<CheckBox>("Animations").IsChecked=false;if(N<Ellipse>("ConnectionHalo").HasAnimatedProperties)throw new Exception("Connection motion did not stop.");N<CheckBox>("Animations").IsChecked=true;
        }
    }
}
