// Chiya Pasal for Windows: a tiny launcher that carries the game inside the .exe.
// It serves the embedded game on http://127.0.0.1:47831/ and opens it in its own Microsoft Edge app window (no address bar),
// with its own saved-data folder, so progress is kept between runs. It closes itself shortly after the window is closed.
// Built by tools/build_exe.ps1 (no downloads needed: it uses the C# compiler that ships with Windows).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    const int Port = 47831;
    static DateTime lastPing = DateTime.MinValue;

    static readonly Dictionary<string, string> Mime = new Dictionary<string, string> {
        { ".html", "text/html; charset=utf-8" }, { ".js", "text/javascript; charset=utf-8" },
        { ".webmanifest", "application/manifest+json" }, { ".png", "image/png" }, { ".json", "application/json" }
    };

    [STAThread]
    static int Main()
    {
        string url = "http://127.0.0.1:" + Port + "/";
        HttpListener listener = null;
        try { listener = new HttpListener(); listener.Prefixes.Add(url); listener.Start(); }
        catch { listener = null; }   // already running: just open another window on the running copy

        if (listener != null)
        {
            var t = new Thread(() => Serve(listener)) { IsBackground = true };
            t.Start();
        }
        if (!OpenWindow(url)) return 1;
        if (listener == null) return 0;

        // stay alive while the page is open (it pings every few seconds); leave once it has gone quiet
        var started = DateTime.UtcNow;
        while (true)
        {
            Thread.Sleep(2000);
            var now = DateTime.UtcNow;
            if (lastPing == DateTime.MinValue ? (now - started).TotalSeconds > 90 : (now - lastPing).TotalSeconds > 150) break;
        }
        return 0;
    }

    static void Serve(HttpListener l)
    {
        while (l.IsListening)
        {
            HttpListenerContext c;
            try { c = l.GetContext(); } catch { return; }
            try { Handle(c); } catch { try { c.Response.Abort(); } catch { } }
        }
    }

    static void Handle(HttpListenerContext c)
    {
        string path = c.Request.Url.AbsolutePath.TrimStart('/');
        if (path == "") path = "index.html";
        if (path == "__ping") { lastPing = DateTime.UtcNow; Send(c, 204, "text/plain", new byte[0]); return; }
        byte[] data = ReadResource(path);
        if (data == null) { Send(c, 404, "text/plain", Encoding.UTF8.GetBytes("Not found")); return; }
        if (path == "index.html")
        {
            string html = Encoding.UTF8.GetString(data);
            string ping = "<script>setInterval(function(){fetch('/__ping').catch(function(){})},4000);</script></body>";
            int i = html.LastIndexOf("</body>", StringComparison.Ordinal);
            if (i >= 0) html = html.Substring(0, i) + ping + html.Substring(i + 7);
            data = Encoding.UTF8.GetBytes(html);
        }
        string mime;
        if (!Mime.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out mime)) mime = "application/octet-stream";
        c.Response.AddHeader("Cache-Control", "no-cache");
        Send(c, 200, mime, data);
    }

    static byte[] ReadResource(string name)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        {
            if (s == null) return null;
            var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
        }
    }

    static void Send(HttpListenerContext c, int code, string mime, byte[] body)
    {
        c.Response.StatusCode = code; c.Response.ContentType = mime; c.Response.ContentLength64 = body.Length;
        if (body.Length > 0) c.Response.OutputStream.Write(body, 0, body.Length);
        c.Response.Close();
    }

    static bool OpenWindow(string url)
    {
        string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChiyaPasal", "Profile");
        string[] candidates = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe")
        };
        foreach (string exe in candidates)
        {
            if (!File.Exists(exe)) continue;
            string args = "--app=" + url + " --user-data-dir=\"" + profile + "\" --window-size=1000,820 --no-first-run --no-default-browser-check"
                        + " --disable-background-timer-throttling --disable-renderer-backgrounding";
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false });
            return true;
        }
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); return true; }
        catch { MessageBox.Show("Chiya Pasal needs Microsoft Edge or Google Chrome to open.", "Chiya Pasal"); return false; }
    }
}
