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
            foreach (var drive in DriveInfo.GetDrives())
            {
                // https://github.com/bloxstraplabs/bloxstrap/issues/1648#issuecomment-2192571030
                if (path.ToUpperInvariant().StartsWith(drive.Name))
                    return drive.AvailableFreeSpace;
            }

            return -1;
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

            string[] patterns = { $"{App.ProjectName}*.exe", "*.dll", "*.json", "*.txt" };

            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(sourceDir, pattern))
                {
                    string target = Path.Combine(targetDir, Path.GetFileName(file));

                    if (String.Equals(file, target, StringComparison.OrdinalIgnoreCase))
                        continue;

                    FileInfo targetInfo = new(target);

                    if (targetInfo.Exists && targetInfo.IsReadOnly)
                        targetInfo.IsReadOnly = false;

                    File.Copy(file, target, true);
                }
            }
        }
    }
}
