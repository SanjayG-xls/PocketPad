using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PocketPad {
    public sealed class ControllerView : FrameworkElement {
        public LayoutModel Layout;
        public InputState Input=new InputState();
        public Brush Surface=Brand.B("#121419"),Tone=Brand.B("#A7ADFF");
        public bool Editable=true,Snap=true;
        public ControlModel Selected;
        public event Action<ControlModel> Selection;
        public event Action EditStarted;
        public event Action Edited;
        private bool dragging,resizing,moved;
        private Point start;
        private double oldX,oldY,oldW,oldH;
        public ControllerView(){Width=1000;Height=480;Focusable=true;ClipToBounds=true;}
        public Rect Bounds(ControlModel c){double scale=Layout==null||Layout.scale<=0?1:Layout.scale;double w=Math.Min(990,c.w*scale),h=Math.Min(470,c.h*scale);return new Rect(Clamp(c.x-w/2,5,995-w),Clamp(c.y-h/2,5,475-h),w,h);}
        public static double Clamp(double x,double a,double b){return Math.Max(a,Math.Min(b,x));}
        protected override void OnRender(DrawingContext dc){
            dc.DrawRectangle(Surface,null,new Rect(0,0,1000,480));
            if(Layout==null)return;
            if(Layout.grid)for(int y=10;y<480;y+=20)for(int x=10;x<1000;x+=20)dc.DrawEllipse(Brand.B("#292B34"),null,new Point(x,y),.8,.8);
            foreach(ControlModel c in Layout.controls)DrawControl(dc,c,Bounds(c),Layout.theme,Input,Tone);
            if(Editable&&Selected!=null){Rect box=Bounds(Selected);box.Inflate(6,6);var pen=new Pen(Brand.B("#B2B5FF"),1.5);pen.DashStyle=DashStyles.Dash;dc.DrawRoundedRectangle(null,pen,box,8,8);dc.DrawRoundedRectangle(Brand.B("#C7C9FF"),new Pen(Brand.B("#121419"),2),new Rect(box.Right-6,box.Bottom-6,12,12),2,2);}
        }
        public static void DrawControl(DrawingContext dc,ControlModel c,Rect r,string theme,InputState input,Brush tone=null){
            bool ps=theme=="ps5";
            bool pressed=input.buttons!=null&&input.buttons.Contains(c.mapping??"");
            if(c.mapping=="TOUCH_CLICK")pressed=input.touchClick;
            Brush ink=Brand.B("#D9DAE6");if(!String.IsNullOrEmpty(c.color)){try{ink=Brand.B(c.color);}catch{}}
            dc.PushOpacity(c.opacity<=0?1:c.opacity);
            if(c.type=="label"){CenterText(dc,c.label,r,Math.Min(18,r.Height*.6),Brand.B("#757887"),false);dc.Pop();return;}
            if(c.type=="stick"){
                dc.DrawEllipse(Brand.B("#08090D"),null,new Point(r.X+r.Width/2,r.Y+r.Height/2+4),r.Width/2,r.Height/2);
                dc.DrawEllipse(new RadialGradientBrush((Color)ColorConverter.ConvertFromString("#0C0D12"),(Color)ColorConverter.ConvertFromString("#292C36")),new Pen(Brand.B("#3B3F4C"),2.5),new Point(r.X+r.Width/2,r.Y+r.Height/2),r.Width/2-2,r.Height/2-2);
                double x=c.mapping=="left"?input.lx:input.rx,y=c.mapping=="left"?input.ly:input.ry;
                Point middle=new Point(r.X+r.Width*(.5+x*.15),r.Y+r.Height*(.5+y*.15));
                dc.DrawEllipse(Brand.B("#101117"),new Pen(Brand.B("#4A4E5D"),2),middle,r.Width*.33,r.Height*.33);
                dc.DrawEllipse(new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#2D303B"),(Color)ColorConverter.ConvertFromString("#20222A"),90),new Pen(Brand.B("#383C49"),1.4),middle,r.Width*.285,r.Height*.285);
                var dots=new Pen(Brand.B("#525564"),1);dots.DashStyle=DashStyles.Dot;dc.DrawEllipse(null,dots,middle,r.Width*.25,r.Height*.25);
                CenterText(dc,c.label,new Rect(middle.X-18,middle.Y-12,36,24),12,Brand.B("#858A9E"),false);dc.Pop();return;
            }
            if(c.type=="dpad"){
                dc.PushTransform(new TranslateTransform(r.X,r.Y));dc.PushTransform(new ScaleTransform(r.Width/100,r.Height/100));
                Brush baseColor=pressed?Brand.B("#565B75"):Brand.B("#2C2F39");var pen=new Pen(Brand.B("#4B505F"),1.2);
                if(ps){string[] shapes={"M36,8 Q50,2 64,8 L60,32 50,43 40,32 Z","M92,36 Q98,50 92,64 L68,60 57,50 68,40 Z","M64,92 Q50,98 36,92 L40,68 50,57 60,68 Z","M8,64 Q2,50 8,36 L32,40 43,50 32,60 Z"};foreach(string s in shapes)dc.DrawGeometry(baseColor,pen,Geometry.Parse(s));dc.DrawGeometry(null,new Pen(Brand.B("#969BAC"),1.2),Geometry.Parse("M45,23 L50,18 55,23 M77,45 L82,50 77,55 M55,77 L50,82 45,77 M23,55 L18,50 23,45"));}
                else{dc.DrawEllipse(Brand.B("#111218"),new Pen(Brand.B("#424653"),2),new Point(50,50),48,48);dc.DrawGeometry(baseColor,pen,Geometry.Parse("M18,18 L37,7 63,7 82,18 93,37 93,63 82,82 63,93 37,93 18,82 7,63 7,37 Z"));dc.DrawGeometry(null,new Pen(Brand.B("#101117"),1.5),Geometry.Parse("M37,7 L37,37 7,37 M63,7 L63,37 93,37 M7,63 L37,63 37,93 M93,63 L63,63 63,93 M37,37 L63,37 63,63 37,63 Z M18,18 L37,37 M82,18 L63,37 M18,82 L37,63 M82,82 L63,63"));}
                dc.Pop();dc.Pop();dc.Pop();return;
            }
            if(c.type=="touchpad"){
                dc.DrawRoundedRectangle(new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#282B36"),(Color)ColorConverter.ConvertFromString("#1B1D26"),90),new Pen(tone??Brand.B(input.touchClick?"#B3B7FF":"#7177D6"),2),r,22,22);
                for(double yy=r.Top+10;yy<r.Bottom-10;yy+=7)for(double xx=r.Left+10;xx<r.Right-10;xx+=7)dc.DrawEllipse(Brand.B("#353847"),null,new Point(xx,yy),.5,.5);
                if(input.touches!=null)foreach(TouchState t in input.touches)dc.DrawEllipse(Brand.B("#555A9277"),new Pen(Brand.B("#C3C6FF"),1.5),new Point(r.X+t.x*r.Width,r.Y+t.y*r.Height),10,10);
                CenterText(dc,"TOUCHPAD",new Rect(r.X,r.Bottom-24,r.Width,18),9,Brand.B("#777D98"),false);dc.Pop();return;
            }
            bool round=c.type=="button"&&c.shape!="rect";
            double radius=round?Math.Min(r.Width,r.Height)/2:Math.Min(16,r.Height*.28);
            Rect shadow=new Rect(r.X,r.Y+4,r.Width,r.Height);
            dc.DrawRoundedRectangle(Brand.B("#07080C"),null,shadow,radius,radius);
            Brush fill=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(pressed?"#535872":"#31343F"),(Color)ColorConverter.ConvertFromString("#191B23"),90);
            if(c.mapping=="GUIDE"&&!ps){fill=Brand.B("#D7D9E6");ink=Brand.B("#191B24");}
            dc.DrawRoundedRectangle(fill,new Pen(Brand.B(pressed?"#BDC0FF":"#454A59"),1.4),r,radius,radius);
            if(c.type=="trigger"||c.type=="slider"){
                double value=c.mapping=="lt"?input.lt:c.mapping=="rt"?input.rt:c.mapping=="lx"?(input.lx+1)/2:(input.rx+1)/2;
                dc.PushClip(new RectangleGeometry(r,radius,radius));dc.DrawRectangle(Brand.B("#666E81AA"),null,c.type=="trigger"?new Rect(r.X,r.Bottom-r.Height*value,r.Width,r.Height*value):new Rect(r.X,r.Y,r.Width*value,r.Height));dc.Pop();
            }
            string label=c.label??"";
            bool defaultFace=new[]{"A","B","X","Y","×","○","□","△"}.Contains(label)&&new[]{"A","B","X","Y"}.Contains(c.mapping);
            if(defaultFace&&ps){
                var p=new Pen(ink,Math.Max(1.5,r.Width*.025));Rect symbol=new Rect(r.X+r.Width*.28,r.Y+r.Height*.28,r.Width*.44,r.Height*.44);
                if(c.mapping=="A"){dc.DrawLine(p,symbol.TopLeft,symbol.BottomRight);dc.DrawLine(p,symbol.TopRight,symbol.BottomLeft);}
                else if(c.mapping=="B")dc.DrawEllipse(null,p,new Point(r.X+r.Width/2,r.Y+r.Height/2),symbol.Width/2,symbol.Height/2);
                else if(c.mapping=="X")dc.DrawRectangle(null,p,symbol);
                else{var g=new StreamGeometry();using(var w=g.Open()){w.BeginFigure(new Point(r.X+r.Width/2,r.Y+r.Height*.24),false,true);w.LineTo(new Point(r.X+r.Width*.77,r.Y+r.Height*.74),true,false);w.LineTo(new Point(r.X+r.Width*.23,r.Y+r.Height*.74),true,false);}dc.DrawGeometry(null,p,g);}
            }else if(c.mapping=="GUIDE"&&new[]{"GUIDE","Xbox","PS"}.Contains(label)){
                if(ps)CenterText(dc,"PS",r,r.Width*.38,ink,true);
                else{double m=r.Width*.23;dc.DrawEllipse(ink,null,new Point(r.X+r.Width/2,r.Y+r.Height/2),r.Width*.3,r.Height*.3);var p=new Pen(Brand.B("#D7D9E6"),r.Width*.065);dc.DrawLine(p,new Point(r.X+m,r.Y+m),new Point(r.Right-m,r.Bottom-m));dc.DrawLine(p,new Point(r.Right-m,r.Y+m),new Point(r.X+m,r.Bottom-m));}
            }else if((c.mapping=="BACK"||c.mapping=="START"||c.mapping=="TOUCH_CLICK")&&new[]{"BACK","START","TOUCH_CLICK","View","Share","Menu","Create","Options","Touch click"}.Contains(label)){
                double size=Math.Min(r.Width,r.Height)*.45;Rect box=new Rect(r.X+(r.Width-size)/2,r.Y+(r.Height-size)/2,size,size);var p=new Pen(ink,1.6);
                if(c.mapping=="START"){for(int i=0;i<3;i++)dc.DrawLine(p,new Point(box.X,box.Y+box.Height*(.15+i*.35)),new Point(box.Right,box.Y+box.Height*(.15+i*.35)));}
                else if(c.mapping=="TOUCH_CLICK"){dc.DrawRoundedRectangle(null,p,box,3,3);dc.DrawLine(p,new Point(box.X+size*.3,box.Y+size*.65),new Point(box.X+size*.7,box.Y+size*.65));}
                else if(ps){dc.DrawLine(p,new Point(box.X+size*.5,box.Y),new Point(box.X+size*.5,box.Bottom));dc.DrawLine(p,new Point(box.X+size*.2,box.Y+size*.2),new Point(box.X+size*.2,box.Bottom-size*.2));dc.DrawLine(p,new Point(box.X+size*.8,box.Y+size*.2),new Point(box.X+size*.8,box.Bottom-size*.2));}
                else{dc.DrawRectangle(null,p,new Rect(box.X,box.Y,box.Width*.7,box.Height*.7));dc.DrawRectangle(null,p,new Rect(box.X+size*.3,box.Y+size*.3,box.Width*.7,box.Height*.7));}
            }else{
                if(defaultFace&&!ps&&String.IsNullOrEmpty(c.color)){ink=Brand.B(c.mapping=="A"?"#7CCA5D":c.mapping=="B"?"#EF757A":c.mapping=="X"?"#61B3EB":"#E9CA69");}
                double size=Math.Min(r.Height*.48,Math.Min(25,r.Width/Math.Max(1,label.Length)*1.5));CenterText(dc,label,r,size,ink,c.type=="button");
            }
            dc.Pop();
        }
        public static void CenterText(DrawingContext dc,string text,Rect r,double size,Brush brush,bool bold){var formatted=Brand.Text(text??"",Math.Max(5,size),brush,bold);dc.DrawText(formatted,new Point(r.X+(r.Width-formatted.Width)/2,r.Y+(r.Height-formatted.Height)/2));}
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){
            base.OnMouseLeftButtonDown(e);if(!Editable||Layout==null)return;Focus();Point p=e.GetPosition(this);
            resizing=Selected!=null&&new Rect(Bounds(Selected).Right-8,Bounds(Selected).Bottom-8,25,25).Contains(p);
            if(!resizing){Selected=Layout.controls.LastOrDefault(c=>Bounds(c).Contains(p));if(Selection!=null)Selection(Selected);}
            if(Selected!=null){start=p;oldX=Selected.x;oldY=Selected.y;oldW=Selected.w;oldH=Selected.h;dragging=true;moved=false;CaptureMouse();}
            InvalidateVisual();e.Handled=true;
        }
        protected override void OnMouseMove(MouseEventArgs e){
            base.OnMouseMove(e);Point p=e.GetPosition(this);
            if(!dragging){Cursor=Selected!=null&&new Rect(Bounds(Selected).Right-8,Bounds(Selected).Bottom-8,25,25).Contains(p)?Cursors.SizeNWSE:Cursors.Arrow;return;}
            if(Selected==null||Layout==null)return;
            if(!moved){if((p-start).Length<1)return;if(EditStarted!=null)EditStarted();moved=true;}
            if(resizing){Selected.w=Clamp(Math.Round(oldW+(p.X-start.X)/Layout.scale),24,600);Selected.h=Clamp(Math.Round(oldH+(p.Y-start.Y)/Layout.scale),24,460);Selected.x=Clamp(oldX+(Selected.w-oldW)*Layout.scale/2,Selected.w/2,1000-Selected.w/2);Selected.y=Clamp(oldY+(Selected.h-oldH)*Layout.scale/2,Selected.h/2,480-Selected.h/2);}
            else{double x=oldX+p.X-start.X,y=oldY+p.Y-start.Y;if(Snap){x=Math.Round(x/10)*10;y=Math.Round(y/10)*10;}Selected.x=Clamp(x,Selected.w/2,1000-Selected.w/2);Selected.y=Clamp(y,Selected.h/2,480-Selected.h/2);}
            InvalidateVisual();e.Handled=true;
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(dragging){dragging=false;ReleaseMouseCapture();if(moved&&Edited!=null)Edited();e.Handled=true;}}
    }
    public sealed class LibraryGlyph : FrameworkElement {
        private readonly ControlModel model;private readonly string theme;
        public LibraryGlyph(ControlModel control,string style){model=Json.Copy(control);model.opacity=1;theme=style;Width=64;Height=44;}
        protected override void OnRender(DrawingContext dc){double ratio=model.w/model.h;double h=34,w=h*ratio;if(w>60){w=60;h=w/ratio;}ControllerView.DrawControl(dc,model,new Rect((64-w)/2,(44-h)/2,w,h),theme,new InputState());}
    }
}
