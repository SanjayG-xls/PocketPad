using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PocketPad {
    public static class Brand {
        public static readonly Color Accent=Color.FromRgb(151,156,255);
        public static Brush B(string color){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));}
        public static FormattedText Text(string text,double size,Brush brush,bool bold){return new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush,1.0);}
        public static void Logo(DrawingContext dc,Rect bounds,Brush ink){
            dc.PushTransform(new TranslateTransform(bounds.X,bounds.Y));dc.PushTransform(new ScaleTransform(bounds.Width/108,bounds.Height/108));
            var pen=new Pen(ink,3.8);pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;pen.LineJoin=PenLineJoin.Round;
            dc.DrawGeometry(null,pen,Geometry.Parse("M36,37 L72,37 Q80,37 83,48 L88,65 Q91,78 83,78 Q79,78 70,67 L38,67 Q29,78 25,78 Q17,78 20,65 L25,48 Q28,37 36,37 Z"));
            dc.DrawLine(pen,new Point(37,47),new Point(37,59));dc.DrawLine(pen,new Point(31,53),new Point(43,53));
            dc.DrawEllipse(new SolidColorBrush(Accent),null,new Point(72,49),3,3);dc.DrawEllipse(ink,null,new Point(64,57),3,3);dc.DrawEllipse(ink,null,new Point(79,57),3,3);
            dc.DrawLine(new Pen(B("#777782"),3),new Point(51,59),new Point(57,59));dc.Pop();dc.Pop();
        }
        public static void SavePng(string path,int width,int height,Action<DrawingContext> draw){var visual=new DrawingVisual();using(var dc=visual.RenderOpen())draw(dc);var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);}
        public static void Snapshot(FrameworkElement element,string path){element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);}
    }
}
