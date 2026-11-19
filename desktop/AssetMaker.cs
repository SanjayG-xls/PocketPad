using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PocketPad {
    public static class AssetMaker {
        [STAThread] public static void Main(string[] args){
            string folder=args[0];Directory.CreateDirectory(folder);
            Brand.SavePng(Path.Combine(folder,"logo.png"),216,216,dc=>Brand.Logo(dc,new Rect(0,0,216,216),Brand.B("#F1F1F5")));
            Brand.SavePng(Path.Combine(folder,"setup-art.png"),720,1080,dc=>{
                dc.DrawRectangle(new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#202231"),(Color)ColorConverter.ConvertFromString("#111217"),90),null,new Rect(0,0,720,1080));
                for(int i=0;i<8;i++)dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb((byte)(26-i*2),151,156,255)),1.2),new Point(360,530),170+i*48,170+i*48);
                Brand.Logo(dc,new Rect(35,180,650,650),Brand.B("#E8E9F3"));
                dc.DrawText(Brand.Text("PocketPad",62,Brand.B("#F2F2F6"),true),new Point(62,82));
                dc.DrawText(Brand.Text("WINDOWS DESKTOP",16,Brand.B("#9295AC"),false),new Point(66,168));
                dc.DrawLine(new Pen(Brand.B("#44465A"),2),new Point(65,895),new Point(655,895));
                dc.DrawText(Brand.Text("PS5  /  XBOX",23,Brand.B("#E5E5EF"),true),new Point(65,932));
                dc.DrawText(Brand.Text("A controller built around you.",20,Brand.B("#85889D"),false),new Point(65,982));
            });
            var pngs=new List<byte[]>();int[] sizes={16,24,32,48,64,128,256};
            foreach(int size in sizes){string path=Path.Combine(folder,"icon-"+size+".png");Brand.SavePng(path,size,size,dc=>{dc.DrawRoundedRectangle(Brand.B("#14151B"),null,new Rect(0,0,size,size),size*.22,size*.22);Brand.Logo(dc,new Rect(-size*.02,-size*.08,size*1.04,size*1.04),Brand.B("#EDEDF4"));});pngs.Add(File.ReadAllBytes(path));}
            using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,"PocketPad.ico")))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(pngs[i].Length);writer.Write(offset);offset+=pngs[i].Length;}foreach(byte[] image in pngs)writer.Write(image);}
        }
    }
}
