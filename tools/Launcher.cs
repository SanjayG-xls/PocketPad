using System;
using System.Diagnostics;
using System.IO;

class PocketPad {
    static int Main() {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        try {
            Console.Title = "PocketPad - Local Controller Host";
            var info = new ProcessStartInfo {
                FileName = Path.Combine(root, "runtime", "python.exe"),
                Arguments = "\"" + Path.Combine(root, "host.py") + "\"",
                WorkingDirectory = root,
                UseShellExecute = false
            };
            using (var host = Process.Start(info)) {
                host.WaitForExit();
                if (host.ExitCode != 0) {
                    Console.WriteLine("\nPocketPad could not start. Close any other PocketPad host, then try again. Press Enter to close.");
                    Console.ReadLine();
                }
                return host.ExitCode;
            }
        } catch (Exception e) {
            Console.WriteLine("PocketPad could not start: " + e.Message + "\nKeep PocketPad.exe with the runtime and web folders. Press Enter to close.");
            Console.ReadLine();
            return 1;
        }
    }
}
