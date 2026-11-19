using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PocketPad {
    public sealed class Engine : IDisposable {
        private Process process;
        private readonly HttpClient client;
        private readonly object logLock=new object();
        public int Port {get;private set;}
        public string DataPath {get;private set;}
        public string BaseUrl {get{return "http://127.0.0.1:"+Port;}}
        public string Root {get;private set;}
        public Engine(string root,string dataPath){Root=root;DataPath=dataPath;Directory.CreateDirectory(DataPath);client=new HttpClient(new HttpClientHandler {UseProxy=false});client.Timeout=TimeSpan.FromSeconds(3);}
        private void Log(string line){if(line==null)return;try{lock(logLock){string folder=Path.Combine(Path.GetDirectoryName(DataPath),"logs");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"host.log"),DateTime.Now.ToString("s")+" "+line+Environment.NewLine);}}catch{}}
        public async Task<HostInfo> Start(int preferred,bool mock){
            string legacy=Path.Combine(Root,"data","presets.json"),destination=Path.Combine(DataPath,"presets.json");
            if(!File.Exists(destination)&&File.Exists(legacy))File.Copy(legacy,destination);
            for(int port=preferred;port<preferred+12;port++){
                Port=port;
                try{HostInfo existing=await Info();if(existing.desktopApi>=1)return existing;continue;}catch{}
                try{
                    var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any,port);listener.Start();listener.Stop();
                }catch{continue;}
                string python=Path.Combine(Root,"runtime","python.exe");
                var start=new ProcessStartInfo(python,"\""+Path.Combine(Root,"host.py")+"\" --no-browser --port "+port+" --data-dir \""+DataPath+"\""+(mock?" --mock":""));
                start.WorkingDirectory=Root;start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
                process=new Process();process.StartInfo=start;process.OutputDataReceived+=(s,e)=>Log(e.Data);process.ErrorDataReceived+=(s,e)=>Log(e.Data);process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
                for(int attempt=0;attempt<40;attempt++){
                    await Task.Delay(180);
                    if(process.HasExited)break;
                    try{return await Info();}catch{}
                }
                if(process.HasExited)throw new InvalidOperationException("The controller engine stopped. Open the log folder in Preferences for details.");
                throw new InvalidOperationException("The controller engine did not respond. Restart PocketPad and check its log folder.");
            }
            throw new InvalidOperationException("No available local connection port was found.");
        }
        public async Task<HostInfo> Info(){return Json.Read<HostInfo>(await client.GetStringAsync(BaseUrl+"/api/info"));}
        public async Task Action(object body){
            using(var content=new StringContent(Json.Write(body),Encoding.UTF8,"application/json"))
            using(var response=await client.PostAsync(BaseUrl+"/api/desktop/action",content)){
                string text=await response.Content.ReadAsStringAsync();
                if(!response.IsSuccessStatusCode){string error="The change could not be saved.";try{error=Json.Read<System.Collections.Generic.Dictionary<string,object>>(text)["error"].ToString();}catch{}throw new InvalidOperationException(error);}
            }
        }
        public async Task<byte[]> Image(string path){return await client.GetByteArrayAsync(BaseUrl+path);}
        public async Task Stop(){
            if(process==null)return;
            try{if(!process.HasExited){try{await client.PostAsync(BaseUrl+"/api/desktop/stop",new StringContent("{}"));}catch{}await Task.Run(()=>process.WaitForExit(2200));if(!process.HasExited)process.Kill();}}catch{}
            process.Dispose();process=null;
        }
        public void Dispose(){client.Dispose();}
    }
}
