using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace PocketPad {
    public class ControlModel {
        public string id {get;set;} public string type {get;set;} public string mapping {get;set;}
        public string label {get;set;} public string color {get;set;} public string shape {get;set;}
        public double x {get;set;} public double y {get;set;} public double w {get;set;} public double h {get;set;}
        public double opacity {get;set;} public double deadzone {get;set;}
        public ControlModel(){opacity=1;deadzone=.1;shape="round";color="";}
    }
    public class LayoutModel {
        public string name {get;set;} public string theme {get;set;} public string accent {get;set;} public string output {get;set;}
        public bool grid {get;set;} public double scale {get;set;} public List<ControlModel> controls {get;set;}
        public LayoutModel(){name="Custom layout";theme="xbox";output="xbox";accent="#929BFF";scale=1;controls=new List<ControlModel>();}
    }
    public class TouchState {public int id{get;set;} public double x{get;set;} public double y{get;set;}}
    public class InputState {
        public List<string> buttons{get;set;} public double lx{get;set;} public double ly{get;set;} public double rx{get;set;} public double ry{get;set;} public double lt{get;set;} public double rt{get;set;}
        public bool touchClick{get;set;} public List<TouchState> touches{get;set;}
        public InputState(){buttons=new List<string>();touches=new List<TouchState>();}
    }
    public class HostInfo {
        public string version{get;set;} public int desktopApi{get;set;} public int active{get;set;} public int port{get;set;}
        public bool driver{get;set;} public bool mock{get;set;} public bool connected{get;set;} public bool paired{get;set;}
        public string driverError{get;set;} public string controllerName{get;set;} public string output{get;set;} public string token{get;set;}
        public List<string> urls{get;set;} public List<LayoutModel> presets{get;set;} public InputState input{get;set;}
    }
    public static class Json {
        public static string Write(object value){return new JavaScriptSerializer().Serialize(value);}
        public static T Read<T>(string value){return new JavaScriptSerializer().Deserialize<T>(value);}
        public static T Copy<T>(T value){return Read<T>(Write(value));}
    }
}
