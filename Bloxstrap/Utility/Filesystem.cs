using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Bloxstrap.Utility
{
    internal static class Filesystem
    {
        internal static long GetFreeDiskSpace(string path)
        {
            if (String.IsNullOrEmpty(path))
                return -1;

            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                fullPath = path;
            }

            long freeSpace = -1;
            int matchedLength = 0;

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        // 取最长匹配，避免把 C:\ 当成 C:\Users\... 的归属盘
                        if (!fullPath.StartsWith(drive.Name, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (drive.Name.Length <= matchedLength)
                            continue;

                        // 空的读卡器、断开的网络盘在这里会抛 IOException。
                        // 以前没有兜底，机器上插个读卡器就能让启动直接失败
                        freeSpace = drive.AvailableFreeSpace;
                        matchedLength = drive.Name.Length;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        App.Logger.WriteLine("Filesystem::GetFreeDiskSpace", $"Could not read free space of {drive.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("Filesystem::GetFreeDiskSpace", "Could not enumerate drives");
                App.Logger.WriteException("Filesystem::GetFreeDiskSpace", ex);

                return -1;
            }

            return freeSpace;
        }

        internal static void AssertReadOnly(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists || !fileInfo.IsReadOnly)
                return;

            fileInfo.IsReadOnly = false;
            App.Logger.WriteLine("Filesystem::AssertReadOnly", $"The following file was set as read-only: {filePath}");
        }

        internal static void AssertReadOnlyDirectory(string directoryPath)
        {
            var directory = new DirectoryInfo(directoryPath) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
                info.Attributes = FileAttributes.Normal;

            App.Logger.WriteLine("Filesystem::AssertReadOnlyDirectory", $"The following directory was set as read-only: {directoryPath}");
        }

        /// <summary>
        /// 把多文件发布（框架依赖 + 压缩）的整份输出搬进目标目录。
        /// 光复制 exe 是装不好的 —— 旁边那堆 dll 才是它的依赖
        /// </summary>
        internal static void CopyPayloadFolder(string sourceDir, string targetDir)
        {
            if (String.IsNullOrEmpty(sourceDir) || String.IsNullOrEmpty(targetDir))
                return;

            if (String.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                return;

            Directory.CreateDirectory(targetDir);

            string[] files;

            try
            {
                // 递归取全部文件。以前只扫顶层，产物里只要出现子目录
                // （runtimes、satellite 程序集之类）就会漏拷，装完起不来
                files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Logger.WriteLine("Filesystem::CopyPayloadFolder", $"Failed to enumerate {sourceDir}");
                App.Logger.WriteException("Filesystem::CopyPayloadFolder", ex);

                return;
            }

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                bool isPayload =
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    || (fileName.StartsWith(App.ProjectName, StringComparison.OrdinalIgnoreCase)
                        && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (!isPayload)
                    continue;

                // 保留相对路径，子目录里的依赖才能落到同一个位置
                string target = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));

                if (String.Equals(file, target, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    FileInfo targetInfo = new(target);

                    if (targetInfo.Exists && targetInfo.IsReadOnly)
                        targetInfo.IsReadOnly = false;

                    File.Copy(file, target, true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    App.Logger.WriteLine("Filesystem::CopyPayloadFolder", $"Failed to copy {file} to {target}");
                    App.Logger.WriteException("Filesystem::CopyPayloadFolder", ex);
                }
            }
        }
    }
}
