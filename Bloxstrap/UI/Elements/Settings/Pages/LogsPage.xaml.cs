using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// 日志查看页：列出 Logs 目录下的文件，支持搜索和另存。
    /// </summary>
    public partial class LogsPage
    {
        private readonly List<FileInfo> _files = new();
        private string _currentContent = "";

        public LogsPage()
        {
            InitializeComponent();
            Loaded += (_, _) => RefreshFiles();
        }

        private void RefreshFiles()
        {
            FileList.Items.Clear();
            _files.Clear();

            try
            {
                if (!Directory.Exists(Paths.Logs))
                {
                    ContentBox.Text = Strings.Dlg_099;
                    return;
                }

                var dir = new DirectoryInfo(Paths.Logs);
                _files.AddRange(dir.GetFiles("*.log").OrderByDescending(f => f.LastWriteTime).Take(100));

                foreach (var f in _files)
                {
                    long kb = Math.Max(1, f.Length / 1024);
                    FileList.Items.Add($"{f.Name}   ({kb} KB)");
                }

                if (_files.Count == 0)
                    ContentBox.Text = Strings.Dlg_100;
            }
            catch (Exception ex)
            {
                ContentBox.Text = Strings.Dlg_101 + ex.Message;
            }
        }

        private void OnRefresh(object sender, RoutedEventArgs e) => RefreshFiles();

        private void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            int idx = FileList.SelectedIndex;
            if (idx < 0 || idx >= _files.Count)
                return;

            try
            {
                var f = _files[idx];

                // 太大的日志只读最后 4MB，避免界面卡死
                const long maxBytes = 4 * 1024 * 1024;
                if (f.Length > maxBytes)
                {
                    using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(-maxBytes, SeekOrigin.End);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    _currentContent = Strings.Log_FileTooLarge + "\r\n\r\n" + sr.ReadToEnd();
                }
                else
                {
                    using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    _currentContent = sr.ReadToEnd();
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                ContentBox.Text = Strings.Dlg_102 + ex.Message;
            }
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(_currentContent))
            {
                ContentBox.Text = "";
                return;
            }

            string keyword = (SearchBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                ContentBox.Text = _currentContent;
                return;
            }

            var hits = _currentContent
                .Split('\n')
                .Where(l => l.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            ContentBox.Text = hits.Length == 0
                ? String.Format(Strings.DlgF_NoSearchHit, keyword)
                : string.Join("\r\n", hits);
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(Paths.Logs))
                    Process.Start(new ProcessStartInfo(Paths.Logs) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Strings.Dlg_103);
            }
        }

        private void OnSaveAs(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentContent))
            {
                MessageBox.Show(Strings.Dlg_104, Strings.Dlg_105);
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Strings.Dlg_106,
                FileName = "luckstrap-log.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, ContentBox.Text, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, Strings.Dlg_107);
                }
            }
        }
    }
}
