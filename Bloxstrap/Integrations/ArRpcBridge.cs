using System.Net.Sockets;
using System.Security.Cryptography;

namespace Bloxstrap.Integrations
{
    /// <summary>
    /// 网页版 Discord 的状态桥，实现的是 arRPC 的 bridge 协议（实验性）。
    ///
    /// 桌面版 Discord 走本地 IPC 管道，浏览器碰不到那条管道，所以网页版天生没有状态。
    /// arRPC 的做法是在 127.0.0.1:1337 起一个 WebSocket，把「该 dispatch 什么」原样发出去，
    /// 浏览器那边由 Vencord / Equicord 的 WebRichPresence 插件接住，塞进 Discord web 的
    /// LOCAL_ACTIVITY_UPDATE。
    ///
    /// 这里只做 1337 那半边。arRPC 还有个 6463 的端口，是给「别的程序往它这里喂状态」用的，
    /// 我们状态本来就是自己算出来的，不需要。
    ///
    /// 消息格式（照抄 arRPC 的 bridge）：
    ///   {"activity": {...} | null, "pid": 1234, "socketId": "0"}
    /// </summary>
    public class ArRpcBridge : IDisposable
    {
        private const string LOG_IDENT = "ArRpcBridge";

        public const int Port = 1337;

        // WebSocket 握手用的固定 GUID，RFC 6455 写死的
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private const int MaxHandshakeBytes = 8192;
        private const long MaxFrameBytes = 1024 * 1024;

        private readonly TcpListener _listener;
        private readonly List<Connection> _connections = new();
        private readonly CancellationTokenSource _cts = new();

        // 缓存成字段。Dispose 之后再摸 _cts.Token 会抛 ObjectDisposedException，
        // 而广播是 fire-and-forget 的，那个异常最后会冒到全局异常处理器
        private readonly CancellationToken _token;

        // 最后一条状态。浏览器可能比游戏晚连上来，连上先补一条，不然要等到下次游戏事件才有东西显示
        private string? _lastPayload;

        private bool _started;
        private DateTime _lastStartAttempt = DateTime.MinValue;

        public bool IsRunning => _started;

        public int ClientCount
        {
            get { lock (_connections) return _connections.Count; }
        }

        public ArRpcBridge()
        {
            _token = _cts.Token;
            _listener = new TcpListener(IPAddress.Loopback, Port);

            TryStart();
        }

        /// <summary>
        /// 起监听。端口被占是常态（arRPC 本体、别的启动器都可能在 1337 上挂着），
        /// 所以失败不抛异常；下次推状态时再试一次，对方退出之后就能接上
        /// </summary>
        private void TryStart()
        {
            if (_started || _cts.IsCancellationRequested)
                return;

            // 别每次推状态都试一遍，隔 30 秒再说
            if (DateTime.UtcNow - _lastStartAttempt < TimeSpan.FromSeconds(30))
                return;

            _lastStartAttempt = DateTime.UtcNow;

            try
            {
                _listener.Start();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to listen on 127.0.0.1:{Port}, will retry later");
                App.Logger.WriteException(LOG_IDENT, ex);
                return;
            }

            _started = true;
            App.Logger.WriteLine(LOG_IDENT, $"Listening on 127.0.0.1:{Port}");

            _ = Task.Run(AcceptLoopAsync);
        }

        public void Publish(DiscordRPC.RichPresence presence)
        {
            if (!_started)
                TryStart();

            if (!_started)
                return;

            string payload = Serialize(presence);
            _lastPayload = payload;
            Broadcast(payload);
        }

        public void Clear()
        {
            if (!_started)
                return;

            string payload = $"{{\"activity\":null,\"pid\":{Environment.ProcessId},\"socketId\":\"0\"}}";
            _lastPayload = payload;
            Broadcast(payload);
        }

        // ---------- 序列化 ----------

        /// <summary>
        /// 拼成 Discord 那边的 activity 结构。字段名和层级是照 arRPC 的 server.js 抄的，
        /// 它把 RPC 客户端发来的 SET_ACTIVITY 原样翻成 dispatch 用的对象。
        /// </summary>
        private static string Serialize(DiscordRPC.RichPresence presence)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                writer.WritePropertyName("activity");
                writer.WriteStartObject();

                // 名字显式给一份。浏览器插件里那句是 activity.name ||= app.name，
                // 给了就不会再去拿 app.name，少一处可能抛异常的地方
                writer.WriteString("name", App.ProjectName);
                writer.WriteString("application_id", DiscordRichPresence.DiscordApplicationId);
                writer.WriteNumber("type", 0);
                writer.WriteBoolean("instance", false);
                writer.WriteNumber("flags", 0);

                if (!String.IsNullOrEmpty(presence.Details))
                    writer.WriteString("details", presence.Details);

                if (!String.IsNullOrEmpty(presence.State))
                    writer.WriteString("state", presence.State);

                if (presence.Timestamps?.Start is DateTime start)
                {
                    writer.WritePropertyName("timestamps");
                    writer.WriteStartObject();
                    writer.WriteNumber("start", new DateTimeOffset(start.ToUniversalTime()).ToUnixTimeMilliseconds());
                    writer.WriteEndObject();
                }

                // 游戏封面和头像都是 Roblox 的图片直链。Discord 客户端认 http(s) 开头的
                // 图片地址，fetchAssetIds 里有专门的分支走 external-assets 换成
                // mp:external/... 的代理路径，所以这里直接发原地址就行。
                // 不是 URL 的（比如应用后台上传的资源键）跳过，查不到还会拖慢整条消息
                WriteAssets(writer, presence.Assets);
                WriteButtons(writer, presence.Buttons);

                writer.WriteEndObject();

                writer.WriteNumber("pid", Environment.ProcessId);
                writer.WriteString("socketId", "0");

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteAssets(Utf8JsonWriter writer, DiscordRPC.Assets? assets)
        {
            if (assets is null)
                return;

            bool hasLarge = IsHttpUrl(assets.LargeImageKey);
            bool hasSmall = IsHttpUrl(assets.SmallImageKey);

            if (!hasLarge && !hasSmall)
                return;

            writer.WritePropertyName("assets");
            writer.WriteStartObject();

            if (hasLarge)
            {
                writer.WriteString("large_image", assets.LargeImageKey);

                if (!String.IsNullOrEmpty(assets.LargeImageText))
                    writer.WriteString("large_text", assets.LargeImageText);
            }

            if (hasSmall)
            {
                writer.WriteString("small_image", assets.SmallImageKey);

                if (!String.IsNullOrEmpty(assets.SmallImageText))
                    writer.WriteString("small_text", assets.SmallImageText);
            }

            writer.WriteEndObject();
        }

        private static bool IsHttpUrl(string? value)
        {
            if (String.IsNullOrEmpty(value))
                return false;

            return value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteButtons(Utf8JsonWriter writer, DiscordRPC.Button[]? buttons)
        {
            if (buttons is null || buttons.Length == 0)
                return;

            // Discord 那边按钮分两处放：显示文字在 activity.buttons，链接在 metadata.button_urls
            writer.WritePropertyName("buttons");
            writer.WriteStartArray();

            foreach (DiscordRPC.Button button in buttons)
                writer.WriteStringValue(button.Label);

            writer.WriteEndArray();

            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WritePropertyName("button_urls");
            writer.WriteStartArray();

            foreach (DiscordRPC.Button button in buttons)
                writer.WriteStringValue(ToBrowserUrl(button.Url));

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        /// 桌面版 Discord 认 roblox:// 这种自定义协议，点一下直接把游戏拉起来。
        /// 网页版不行 —— 按钮是浏览器里 window.open 出去的，浏览器不认这个协议，
        /// 有的客户端干脆把非 http(s) 的链接整个丢掉，表现就是「Join server 不见了」。
        /// 换成官网的启动地址，浏览器点开就是 Roblox 自己的「打开应用」跳转，效果一样
        /// </summary>
        private static string ToBrowserUrl(string? url)
        {
            if (String.IsNullOrEmpty(url))
                return String.Empty;

            if (url.StartsWith("roblox://experiences/start?", StringComparison.OrdinalIgnoreCase))
                return "https://www.roblox.com/games/start?" + url["roblox://experiences/start?".Length..];

            // 老格式 roblox://placeId=xxx&gameInstanceId=yyy
            if (url.StartsWith("roblox://placeId=", StringComparison.OrdinalIgnoreCase))
                return "https://www.roblox.com/games/start?" + url["roblox://".Length..];

            return url;
        }

        // ---------- 连接管理 ----------

        private async Task AcceptLoopAsync()
        {
            while (!_token.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(_token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_token.IsCancellationRequested)
                        break;

                    App.Logger.WriteLine(LOG_IDENT, "Accept failed");
                    App.Logger.WriteException(LOG_IDENT, ex);
                    continue;
                }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            CancellationToken token = _token;
            Connection? connection = null;

            try
            {
                client.NoDelay = true;
                NetworkStream stream = client.GetStream();

                string? key = await ReadHandshakeAsync(stream, token);

                if (key is null)
                {
                    client.Dispose();
                    return;
                }

                string accept = Convert.ToBase64String(
                    SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));

                string response =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {accept}\r\n" +
                    "\r\n";

                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token);

                connection = new Connection(client, stream);

                int total;

                lock (_connections)
                {
                    _connections.Add(connection);
                    total = _connections.Count;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Browser connected ({total} total)");

                if (_lastPayload is not null)
                    await connection.SendAsync(_lastPayload, token);

                while (!token.IsCancellationRequested)
                {
                    (int Opcode, byte[] Payload)? frame = await ReadFrameAsync(stream, token);

                    if (frame is null || frame.Value.Opcode == 0x8)
                        break;

                    // 浏览器偶尔会发 ping，回一个 pong 就行，内容不用管
                    if (frame.Value.Opcode == 0x9)
                        await connection.SendFrameAsync(0xA, frame.Value.Payload, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Connection dropped");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
            finally
            {
                if (connection is not null)
                {
                    lock (_connections)
                        _connections.Remove(connection);

                    connection.Dispose();
                }
                else
                {
                    client.Dispose();
                }
            }
        }

        private void Broadcast(string payload)
        {
            Connection[] snapshot;

            lock (_connections)
                snapshot = _connections.ToArray();

            foreach (Connection connection in snapshot)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await connection.SendAsync(payload, _token);
                    }
                    catch (Exception)
                    {
                        // 写失败说明浏览器那边已经断了，回收掉，下次连上来重新补状态
                        lock (_connections)
                            _connections.Remove(connection);

                        connection.Dispose();
                    }
                });
            }
        }

        // ---------- 握手 ----------

        /// <summary>
        /// 读到 HTTP 头结束，返回 Sec-WebSocket-Key。逐字节读，握手包很小，不值得开缓冲。
        /// </summary>
        private static async Task<string?> ReadHandshakeAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new List<byte>(512);
            byte[] one = new byte[1];

            while (buffer.Count < MaxHandshakeBytes)
            {
                if (await stream.ReadAsync(one, token) == 0)
                    return null;

                buffer.Add(one[0]);

                int n = buffer.Count;

                if (n >= 4
                    && buffer[n - 4] == (byte)'\r'
                    && buffer[n - 3] == (byte)'\n'
                    && buffer[n - 2] == (byte)'\r'
                    && buffer[n - 1] == (byte)'\n')
                    break;
            }

            string text = Encoding.ASCII.GetString(buffer.ToArray());

            foreach (string line in text.Split("\r\n"))
            {
                int colon = line.IndexOf(':');

                if (colon <= 0)
                    continue;

                if (!line[..colon].Trim().Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                    continue;

                return line[(colon + 1)..].Trim();
            }

            return null;
        }

        // ---------- 帧 ----------

        private static async Task<(int Opcode, byte[] Payload)?> ReadFrameAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] header = new byte[2];

            if (!await ReadExactAsync(stream, header, token))
                return null;

            int opcode = header[0] & 0x0F;
            bool masked = (header[1] & 0x80) != 0;
            long length = header[1] & 0x7F;

            if (length == 126)
            {
                byte[] extended = new byte[2];

                if (!await ReadExactAsync(stream, extended, token))
                    return null;

                length = (extended[0] << 8) | extended[1];
            }
            else if (length == 127)
            {
                byte[] extended = new byte[8];

                if (!await ReadExactAsync(stream, extended, token))
                    return null;

                length = 0;

                for (int i = 0; i < 8; i++)
                    length = (length << 8) | extended[i];
            }

            // 浏览器不会往这边发大包，超过 1MB 就当是异常流量断开
            if (length < 0 || length > MaxFrameBytes)
                return null;

            byte[]? mask = null;

            if (masked)
            {
                mask = new byte[4];

                if (!await ReadExactAsync(stream, mask, token))
                    return null;
            }

            byte[] payload = new byte[length];

            if (length > 0 && !await ReadExactAsync(stream, payload, token))
                return null;

            if (mask is not null)
            {
                for (int i = 0; i < payload.Length; i++)
                    payload[i] ^= mask[i % 4];
            }

            return (opcode, payload);
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
        {
            int offset = 0;

            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset), token);

                if (read == 0)
                    return false;

                offset += read;
            }

            return true;
        }

        private static byte[] BuildFrame(int opcode, byte[] payload)
        {
            using var stream = new MemoryStream(payload.Length + 10);

            stream.WriteByte((byte)(0x80 | opcode));

            if (payload.Length < 126)
            {
                stream.WriteByte((byte)payload.Length);
            }
            else if (payload.Length <= 0xFFFF)
            {
                stream.WriteByte(126);
                stream.WriteByte((byte)(payload.Length >> 8));
                stream.WriteByte((byte)(payload.Length & 0xFF));
            }
            else
            {
                stream.WriteByte(127);

                for (int i = 7; i >= 0; i--)
                    stream.WriteByte((byte)((long)payload.Length >> (8 * i)));
            }

            stream.Write(payload, 0, payload.Length);

            return stream.ToArray();
        }

        public void Dispose()
        {
            if (!_started)
                return;

            _started = false;
            App.Logger.WriteLine(LOG_IDENT, "Shutting down");

            try
            {
                _cts.Cancel();
                _listener.Stop();
            }
            catch (Exception)
            {
            }

            Connection[] snapshot;

            lock (_connections)
            {
                snapshot = _connections.ToArray();
                _connections.Clear();
            }

            foreach (Connection connection in snapshot)
                connection.Dispose();

            _cts.Dispose();

            GC.SuppressFinalize(this);
        }

        private sealed class Connection : IDisposable
        {
            private readonly TcpClient _client;
            private readonly NetworkStream _stream;
            private readonly SemaphoreSlim _writeLock = new(1, 1);
            private bool _disposed;

            public Connection(TcpClient client, NetworkStream stream)
            {
                _client = client;
                _stream = stream;
            }

            public Task SendAsync(string text, CancellationToken token)
                => SendFrameAsync(0x1, Encoding.UTF8.GetBytes(text), token);

            public async Task SendFrameAsync(int opcode, byte[] payload, CancellationToken token)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(Connection));

                byte[] frame = BuildFrame(opcode, payload);

                await _writeLock.WaitAsync(token);

                try
                {
                    if (_disposed)
                        return;

                    await _stream.WriteAsync(frame, token);
                }
                finally
                {
                    // _writeLock 不跟着 Dispose 一起释放。
                    // 以前这里 Dispose 会把信号量也 dispose 掉，另一个线程正卡在
                    // WaitAsync/Release 上就直接抛 ObjectDisposedException，
                    // 表现就是 arRPC 偶发一条异常日志
                    _writeLock.Release();
                }
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                try { _stream.Dispose(); } catch { }
                try { _client.Dispose(); } catch { }
            }
        }
    }
}
