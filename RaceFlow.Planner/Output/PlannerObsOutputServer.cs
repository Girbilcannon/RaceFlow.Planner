using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RaceFlow.Planner.ThemeBuilder;

namespace RaceFlow.Planner.Output
{
    internal sealed class PlannerObsOutputServer : IDisposable
    {
        private readonly ThemeBuilderCanvas _canvas;
        private readonly HttpListener _listener = new();
        private readonly string _baseUrl;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private bool _started;

        private readonly SemaphoreSlim _renderGate = new(1, 1);
        private readonly object _cacheLock = new();
        private byte[]? _cachedRenderPng;
        private DateTime _cachedRenderUtc = DateTime.MinValue;
        private const int RenderWidth = 1920;
        private const int RenderHeight = 1080;
        private const int RenderCacheMilliseconds = 500;

        public PlannerObsOutputServer(ThemeBuilderCanvas canvas, int port = 5057)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _baseUrl = $"http://localhost:{port}/";
            _listener.Prefixes.Add(_baseUrl);
        }

        public string BaseUrl => _baseUrl;
        public string ObsUrl => _baseUrl + "obs";

        public void Start()
        {
            if (_started)
                return;

            _cts = new CancellationTokenSource();
            _listener.Start();
            _started = true;
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        public void OpenInBrowser()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _baseUrl,
                UseShellExecute = true
            });
        }

        public void Stop()
        {
            if (!_started)
                return;

            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
            }
            finally
            {
                _started = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    if (!_started || token.IsCancellationRequested)
                        break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (token.IsCancellationRequested)
                        break;
                }

                if (context != null)
                    _ = Task.Run(() => HandleAsync(context), token);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url?.AbsolutePath.Trim('/').ToLowerInvariant() ?? string.Empty;

                if (path == "render.png")
                {
                    await WriteRenderPngAsync(context.Response).ConfigureAwait(false);
                    return;
                }

                if (path == "obs")
                {
                    await WriteTextAsync(context.Response, BuildObsHtml(), "text/html; charset=utf-8").ConfigureAwait(false);
                    return;
                }

                await WriteTextAsync(context.Response, BuildHelperHtml(), "text/html; charset=utf-8").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                    await WriteTextAsync(context.Response, ex.Message, "text/plain; charset=utf-8").ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private async Task WriteRenderPngAsync(HttpListenerResponse response)
        {
            byte[] bytes = await GetCachedRenderPngAsync().ConfigureAwait(false);

            response.ContentType = "image/png";
            response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            response.Headers["Pragma"] = "no-cache";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.OutputStream.Close();
        }

        private async Task<byte[]> GetCachedRenderPngAsync()
        {
            byte[]? cached = TryGetFreshCachedRender();
            if (cached != null)
                return cached;

            // If OBS/browser sends overlapping requests, do not queue multiple UI-thread
            // render operations. Return the last snapshot while one refresh is already running.
            if (!await _renderGate.WaitAsync(0).ConfigureAwait(false))
            {
                byte[]? stale = TryGetAnyCachedRender();
                if (stale != null)
                    return stale;

                await _renderGate.WaitAsync().ConfigureAwait(false);
                _renderGate.Release();
                return TryGetAnyCachedRender() ?? Array.Empty<byte>();
            }

            try
            {
                cached = TryGetFreshCachedRender();
                if (cached != null)
                    return cached;

                using Bitmap bitmap = RenderBitmapOnUiThread(RenderWidth, RenderHeight);
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                byte[] bytes = ms.ToArray();

                lock (_cacheLock)
                {
                    _cachedRenderPng = bytes;
                    _cachedRenderUtc = DateTime.UtcNow;
                }

                return bytes;
            }
            finally
            {
                _renderGate.Release();
            }
        }

        private byte[]? TryGetFreshCachedRender()
        {
            lock (_cacheLock)
            {
                if (_cachedRenderPng == null)
                    return null;

                double ageMs = (DateTime.UtcNow - _cachedRenderUtc).TotalMilliseconds;
                return ageMs <= RenderCacheMilliseconds ? _cachedRenderPng : null;
            }
        }

        private byte[]? TryGetAnyCachedRender()
        {
            lock (_cacheLock)
                return _cachedRenderPng;
        }

        private Bitmap RenderBitmapOnUiThread(int width, int height)
        {
            if (_canvas.IsDisposed)
                return new Bitmap(width, height, PixelFormat.Format32bppArgb);

            if (_canvas.InvokeRequired)
            {
                return (Bitmap)_canvas.Invoke(new Func<Bitmap>(() => _canvas.RenderOutputBitmap(width, height)));
            }

            return _canvas.RenderOutputBitmap(width, height);
        }

        private string BuildHelperHtml()
        {
            string obsUrl = WebUtility.HtmlEncode(ObsUrl);
            return $@"<!doctype html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>RaceFlow Planner OBS Output</title>
<style>
html,body{{margin:0;background:#111821;color:#e7edf4;font-family:Segoe UI,Arial,sans-serif;}}
.header{{padding:12px 16px;background:#18212c;border-bottom:1px solid #334252;}}
.label{{font-size:13px;color:#9fb0c2;margin-bottom:4px;}}
.url{{font-size:16px;font-weight:700;color:#a9f0c5;user-select:all;}}
.note{{font-size:12px;color:#9fb0c2;margin-top:6px;}}
.preview{{width:100vw;height:calc(100vh - 82px);border:0;display:block;background:transparent;}}
</style>
</head>
<body>
<div class=""header"">
  <div class=""label"">OBS Browser Source URL</div>
  <div class=""url"">{obsUrl}</div>
  <div class=""note"">Use this URL in OBS. The embedded preview below is the same transparent output without Theme Builder containers/headings.</div>
</div>
<iframe class=""preview"" src=""/obs""></iframe>
</body>
</html>";
        }

        private static string BuildObsHtml()
        {
            return @"<!doctype html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>RaceFlow Planner OBS</title>
<style>
html,body{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}
#render{position:absolute;inset:0;width:100%;height:100%;object-fit:contain;background:transparent;}
</style>
</head>
<body>
<img id=""render"" alt=""RaceFlow Planner OBS Output"">
<script>
const img = document.getElementById('render');
function refresh(){ img.src = '/render.png?t=' + Date.now(); }
refresh();
setInterval(refresh, 500);
</script>
</body>
</html>";
        }

        private static async Task WriteTextAsync(HttpListenerResponse response, string text, string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.OutputStream.Close();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
