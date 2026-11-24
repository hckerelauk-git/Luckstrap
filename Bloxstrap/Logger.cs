namespace Bloxstrap
{
    // https://stackoverflow.com/a/53873141/11812173

    public class Logger
    {
        private readonly object _lock = new();
        private StreamWriter? _writer;

        public readonly List<string> History = new();
        public bool Initialized = false;
        public bool NoWriteMode = false;
        public string? FileLocation;

        public string AsDocument => String.Join('\n', History);

        /// <summary>
        /// 打开日志文件。会依次尝试安装目录、临时目录、LocalAppData，
        /// 全部失败才退化成「只记在内存里」，并把失败原因写进 History。
        ///
        /// 以前只有一个目录，而且 <c>Directory.CreateDirectory</c> 抛异常时没人接，
        /// 表现就是启动直接闪退或者弹「无法写入日志」然后整个会话没有日志。
        /// </summary>
        public void Initialize(bool useTempDir = false)
        {
            const string LOG_IDENT = "Logger::Initialize";

            if (Initialized)
            {
                WriteLine(LOG_IDENT, "Failed to initialize because logger is already initialized");
                return;
            }

            foreach (string directory in GetCandidateDirectories(useTempDir))
            {
                if (TryInitializeAt(directory))
                {
                    WriteLine(LOG_IDENT, "Finished initializing!");
                    CleanupOldLogs();
                    return;
                }
            }

            // 一个都写不了。别再弹框打断启动，把原因留在内存日志里，
            // 用户从「关于 - 日志」还能看到这次启动发生了什么
            NoWriteMode = true;

            WriteLine(LOG_IDENT, "Failed to initialize at every candidate directory, running without a log file");
            WriteLine(LOG_IDENT, $"Last error: {_lastError ?? "unknown"}");

            if (!useTempDir)
            {
                Frontend.ShowMessageBox(
                    String.Format(Strings.Logger_NoWriteMode, String.Join("\n", GetCandidateDirectories(false))),
                    System.Windows.MessageBoxImage.Warning,
                    System.Windows.MessageBoxButton.OK
                );
            }
        }

        private string? _lastError;

        private IEnumerable<string> GetCandidateDirectories(bool useTempDir)
        {
            if (useTempDir)
                yield return Paths.TempLogs;

            if (Paths.Initialized)
                yield return Path.Combine(Paths.Base, "Logs");

            yield return Paths.TempLogs;

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!String.IsNullOrEmpty(local))
                yield return Path.Combine(local, App.ProjectName, "Logs");
        }

        private bool TryInitializeAt(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
                string location = Path.Combine(directory, $"{App.ProjectName}_{timestamp}.log");

                // 同一秒内启动两次时加序号，不要因为重名就放弃写日志
                int suffix = 2;
                while (File.Exists(location))
                {
                    location = Path.Combine(directory, $"{App.ProjectName}_{timestamp}_{suffix++}.log");

                    if (suffix > 20)
                        throw new IOException("Too many log files for the same timestamp");
                }

                var stream = new FileStream(location, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                Initialized = true;
                FileLocation = location;

                if (History.Count > 0)
                    WriteToLog(String.Join("\r\n", History));

                return true;
            }
            catch (Exception ex)
            {
                _lastError = $"{directory}: {ex.GetType().Name}: {ex.Message}";
                Debug.WriteLine($"[Logger] {_lastError}");
                return false;
            }
        }

        private void CleanupOldLogs()
        {
            if (String.IsNullOrEmpty(FileLocation))
                return;

            string? directory = Path.GetDirectoryName(FileLocation);

            if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            foreach (FileInfo log in new DirectoryInfo(directory).GetFiles($"{App.ProjectName}_*.log"))
            {
                if (log.LastWriteTimeUtc.AddDays(7) > DateTime.UtcNow)
                    continue;

                // 正在写的这份别删
                if (String.Equals(log.FullName, FileLocation, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    log.Delete();
                }
                catch (Exception)
                {
                    // 被别的实例占用就下次再说，不值得为它写日志（会递归）
                }
            }
        }

        private void WriteLine(string message)
        {
            string timestamp = DateTime.UtcNow.ToString("s") + "Z";
            string outcon = $"{timestamp} {message}";
            string outlog = outcon.Replace(Paths.UserProfile, "%UserProfile%", StringComparison.InvariantCultureIgnoreCase);

            Debug.WriteLine(outcon);

            lock (_lock)
            {
                History.Add(outlog);
                WriteToLog(outlog);
            }
        }

        public void WriteLine(string identifier, string message) => WriteLine($"[{identifier}] {message}");

        public void WriteException(string identifier, Exception ex)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            string hresult = "0x" + ex.HResult.ToString("X8");

            WriteLine($"[{identifier}] ({hresult}) {ex}");

            Thread.CurrentThread.CurrentUICulture = Locale.CurrentCulture;
        }

        /// <summary>
        /// 同步写。一行日志几十字节，异步反而会丢顺序、丢末尾几行，
        /// 退出时没 flush 完的那部分就是「日志缺最后一段」的来源
        /// </summary>
        private void WriteToLog(string message)
        {
            if (!Initialized || _writer is null)
                return;

            try
            {
                _writer.WriteLine(message);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// 退出前调一次，确保缓冲区落盘
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                catch (Exception)
                {
                }

                _writer = null;
                Initialized = false;
            }
        }
    }
}
