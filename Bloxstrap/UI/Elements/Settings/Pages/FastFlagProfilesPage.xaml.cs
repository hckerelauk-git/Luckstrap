using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Win32;

// 不能用 using Wpf.Ui.Controls，那边也有个 MessageBox，会跟 System.Windows.MessageBox 撞名
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>左侧列表的一行。每次改动都重建整个列表，所以不需要 INotifyPropertyChanged。</summary>
    public class ProfileListItem
    {
        public string Name { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string FilePath { get; set; } = "";
        public FastFlagProfile Profile { get; set; } = new();
    }

    /// <summary>DataGrid 的一行。保存时从这个集合反推字典，避免两套数据互相追。</summary>
    public class FastFlagRow
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// 配置方案页：把整套 FastFlags 存成命名方案，一键切换、导入导出。
    /// 方案文件放在 Profiles 目录下，跟真正生效的 ClientAppSettings.json 分开。
    /// </summary>
    public partial class FastFlagProfilesPage
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 中文名字别转成 \uXXXX
        };

        private readonly ObservableCollection<ProfileListItem> _items = new();
        private readonly ObservableCollection<FastFlagRow> _rows = new();

        private ProfileListItem? _selected;

        public FastFlagProfilesPage()
        {
            InitializeComponent();
            ProfileList.ItemsSource = _items;
            FlagGrid.ItemsSource = _rows;
            Loaded += (_, _) => { SeedBuiltinPresets(); Reload(); };
        }

        // ── 列表 / 读取 ────────────────────────────────────────────────

        private void Reload(string? selectPath = null)
        {
            string? keep = selectPath ?? _selected?.FilePath;

            _items.Clear();

            try
            {
                Directory.CreateDirectory(Paths.Profiles);

                foreach (var file in Directory.GetFiles(Paths.Profiles, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var profile = ReadProfile(file);
                    if (profile is null)
                        continue;

                    _items.Add(new ProfileListItem
                    {
                        Name = profile.Name,
                        Subtitle = $"{profile.Flags.Count} 项 · {profile.SavedAt:MM-dd HH:mm}",
                        FilePath = file,
                        Profile = profile
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagProfiles::Reload", ex.ToString());
                Warn("读不出来", ex.Message);
            }

            ListHeader.Text = $"方案列表（{_items.Count}）";

            if (_items.Count == 0)
            {
                ClearEditor();
                return;
            }

            ProfileList.SelectedItem = _items.FirstOrDefault(i => i.FilePath == keep) ?? _items[0];
        }

        private void OnProfileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileList.SelectedItem is ProfileListItem item)
                ShowProfile(item);
        }

        private void ShowProfile(ProfileListItem item)
        {
            _selected = item;

            NameBox.Text = item.Profile.Name;
            NoteBox.Text = item.Profile.Note;

            _rows.Clear();
            foreach (var pair in item.Profile.Flags.OrderBy(p => p.Key, StringComparer.Ordinal))
                _rows.Add(new FastFlagRow { Name = pair.Key, Value = pair.Value });
        }

        private void ClearEditor()
        {
            _selected = null;
            NameBox.Text = "";
            NoteBox.Text = "";
            _rows.Clear();
        }

        // ── 增删改 ────────────────────────────────────────────────────

        private void OnCreateBlank(object sender, RoutedEventArgs e)
        {
            try
            {
                var profile = new FastFlagProfile
                {
                    Name = NextName("新方案"),
                    SavedAt = DateTime.Now
                };

                string path = MakeFilePath(profile.Name);
                WriteProfile(profile, path);
                Reload(path);

                Info("建好了", "右边填 Flag 名和值，填完点「保存修改」。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::CreateBlank", ex);
            }
        }

        private void OnSnapshotCurrent(object sender, RoutedEventArgs e)
        {
            try
            {
                var flags = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in App.FastFlags.Prop)
                    flags[pair.Key] = pair.Value?.ToString() ?? "";

                if (flags.Count == 0)
                {
                    Warn("当前没有 FastFlags", "ClientAppSettings.json 是空的，先在前面的 FastFlags 页面配点东西再来存。");
                    return;
                }

                var profile = new FastFlagProfile
                {
                    Name = NextName($"当前配置 {DateTime.Now:MM-dd HH-mm}"),
                    Note = "从当前 ClientAppSettings.json 存下来的",
                    SavedAt = DateTime.Now,
                    Flags = flags
                };

                string path = MakeFilePath(profile.Name);
                WriteProfile(profile, path);
                Reload(path);

                Info("存好了", $"把当前的 {flags.Count} 项存成了「{profile.Name}」。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Snapshot", ex);
            }
        }

        private void OnSaveProfile(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
            {
                Warn("还没选方案", "先在左边点一个方案。");
                return;
            }

            string name = NameBox.Text.Trim();

            if (name.Length == 0)
            {
                Warn("名字不能空", "给方案起个名字再保存。");
                return;
            }

            string oldPath = _selected.FilePath;

            // 重名会让列表里出现两个一模一样的条目，直接拦掉
            var clash = _items.FirstOrDefault(i =>
                !string.Equals(i.FilePath, oldPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

            if (clash is not null)
            {
                Warn("名字撞了", $"已经有一个叫「{clash.Name}」的方案，换个名字。");
                return;
            }

            var profile = _selected.Profile;

            profile.Name = name;
            profile.Note = NoteBox.Text.Trim();
            profile.SavedAt = DateTime.Now;
            profile.Flags = ReadRows();

            try
            {
                string newPath = MakeFilePath(name, oldPath);
                WriteProfile(profile, newPath);

                // 改了名字就换文件名，老文件删掉，不然会留一份重名的
                if (!string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(oldPath);
                    }
                    catch (Exception ex)
                    {
                        // 删不掉不影响新的那份，只是会多留一个旧文件
                        App.Logger.WriteLine("FastFlagProfiles::Save", $"删旧文件失败 {oldPath}: {ex.Message}");
                    }
                }

                Reload(newPath);

                Info("已保存", $"「{name}」，{profile.Flags.Count} 项。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Save", ex);
            }
        }

        private void OnDeleteProfile(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
            {
                Warn("还没选方案", "先在左边点一个方案。");
                return;
            }

            var confirm = MessageBox.Show(
                $"要删掉「{_selected.Profile.Name}」吗？\n\n只删这份存档，不影响正在用的 ClientAppSettings.json。",
                "删除方案",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                File.Delete(_selected.FilePath);
                _selected = null;
                Reload();
                Info("删掉了", "方案已经从列表里移走。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Delete", ex);
            }
        }

        private void OnAddRow(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
            {
                Warn("还没选方案", "先在左边点一个方案。");
                return;
            }

            var row = new FastFlagRow();
            _rows.Add(row);
            FlagGrid.SelectedItem = row;
            FlagGrid.ScrollIntoView(row);
        }

        private void OnDeleteRows(object sender, RoutedEventArgs e)
        {
            var selected = FlagGrid.SelectedItems.Cast<FastFlagRow>().ToList();

            if (selected.Count == 0)
            {
                Warn("没选行", "在表格里点一下要删的那行（按住 Ctrl 可以多选）。");
                return;
            }

            foreach (var row in selected)
                _rows.Remove(row);
        }

        // ── 导入导出 ──────────────────────────────────────────────────

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选一份 FastFlags 配置",
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                var profile = ParseProfile(json, Path.GetFileNameWithoutExtension(dialog.FileName));

                if (profile is null || profile.Flags.Count == 0)
                {
                    Warn("没读到内容", "这个文件里找不到 FastFlags。要么是空的，要么不是 ClientAppSettings.json 那种格式。");
                    return;
                }

                profile.Name = NextName(profile.Name);
                profile.SavedAt = DateTime.Now;

                string path = MakeFilePath(profile.Name);
                WriteProfile(profile, path);
                Reload(path);

                Info("导入成功", $"「{profile.Name}」，{profile.Flags.Count} 项。还没应用，确认没问题再点「应用这套配置」。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Import", ex);
            }
        }

        private void OnExport(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
            {
                Warn("还没选方案", "先在左边点一个方案。");
                return;
            }

            var flags = ReadRows();

            if (flags.Count == 0)
            {
                Warn("这套是空的", "空方案导出去没意义，先加点 Flag。");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出 FastFlags",
                Filter = "JSON 文件 (*.json)|*.json",
                FileName = SafeFileName(NameBox.Text.Trim()) + ".json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                // 导出成标准的 ClientAppSettings.json 格式，别人拿到可以直接丢进 Modifications 目录用
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(flags, WriteOptions));
                Info("导出成功", $"{flags.Count} 项写到 {dialog.FileName}。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Export", ex);
            }
        }

        // ── 应用 ──────────────────────────────────────────────────────

        private void OnApply(object sender, RoutedEventArgs e)
        {
            if (_selected is null)
            {
                Warn("还没选方案", "先在左边点一个方案。");
                return;
            }

            // 右边表格里改了但没点保存的，也算进来，不然用户以为改了就是改了
            var flags = ReadRows();
            string name = NameBox.Text.Trim();

            if (flags.Count == 0)
            {
                Warn("这套是空的", "空方案应用上去会把现有 FastFlags 全清掉，不给这么干。");
                return;
            }

            var confirm = MessageBox.Show(
                $"要把「{name}」应用到这台电脑吗？\n\n" +
                $"当前 {App.FastFlags.Prop.Count} 项 FastFlags 会被整套换成这套的 {flags.Count} 项。\n" +
                "覆盖之后就回不去了，想留一手就先点「把当前配置存成方案」。\n\n" +
                "改完要重启游戏才生效。",
                "应用配置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                ApplyFlags(flags);

                // 顺手把表格里改的东西落到方案文件上，不然下次点进来又变回旧的
                PersistAfterApply(flags);

                Info("应用完成", $"现在生效的是「{name}」，{flags.Count} 项。下次启动游戏就是这套。", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                Fail("FastFlagProfiles::Apply", ex);
            }
        }

        /// <summary>
        /// 应用之后把表格里的 flag 写回方案文件，不然下次点进来又变回旧的。
        /// 只写 flag 和保存时间——名字、备注归「保存修改」管，免得改了一半的名字被悄悄写进去。
        /// </summary>
        private void PersistAfterApply(Dictionary<string, string> flags)
        {
            if (_selected is null)
                return;

            try
            {
                var profile = _selected.Profile;

                profile.SavedAt = DateTime.Now;
                profile.Flags = flags;

                WriteProfile(profile, _selected.FilePath);
                Reload(_selected.FilePath);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagProfiles::PersistAfterApply", ex.Message);
            }
        }

        private static void ApplyFlags(Dictionary<string, string> flags)
        {
            var next = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var pair in flags)
                next[pair.Key] = pair.Value;

            // FastFlagManager.Load 每次都会强制这两项，整套覆盖之后得补回来，不然会被冲掉
            next[FastFlagManager.PresetFlags["Network.Log"]] = "7";
            next[FastFlagManager.PresetFlags["Rendering.ManualFullscreen"]] = "False";

            App.FastFlags.Prop = next;
            App.FastFlags.Save();
        }

        // ── 读写文件 ──────────────────────────────────────────────────

        private Dictionary<string, string> ReadRows()
        {
            var flags = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var row in _rows)
            {
                string key = (row.Name ?? "").Trim();

                if (key.Length == 0)
                    continue;

                flags[key] = (row.Value ?? "").Trim();
            }

            return flags;
        }

        private static FastFlagProfile? ReadProfile(string path)
        {
            try
            {
                return ParseProfile(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagProfiles::ReadProfile", $"{path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 两种格式都认：我们自己的 { Name / Note / Flags }，和裸的 ClientAppSettings.json。
        /// 判断依据是有没有 Flags 这个对象属性——直接 Deserialize 的话裸配置会被解析成空方案，静默失败。
        /// </summary>
        private static FastFlagProfile? ParseProfile(string json, string fallbackName)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var profile = new FastFlagProfile { Name = fallbackName };

            if (root.TryGetProperty("Flags", out var flagsElement) && flagsElement.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("Name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                    profile.Name = nameElement.GetString() ?? fallbackName;

                if (root.TryGetProperty("Note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String)
                    profile.Note = noteElement.GetString() ?? "";

                if (root.TryGetProperty("SavedAt", out var savedElement) && savedElement.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(savedElement.GetString(), out DateTime savedAt))
                    profile.SavedAt = savedAt;

                ReadFlags(flagsElement, profile.Flags);
            }
            else
            {
                ReadFlags(root, profile.Flags);
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
                profile.Name = fallbackName;

            return profile;
        }

        private static void ReadFlags(JsonElement obj, Dictionary<string, string> into)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                string? value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "True",
                    JsonValueKind.False => "False",
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };

                if (value is not null)
                    into[prop.Name] = value;
            }
        }

        private static void WriteProfile(FastFlagProfile profile, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(profile, WriteOptions));
        }

        private string MakeFilePath(string name, string? exceptPath = null)
        {
            string safe = SafeFileName(name);
            string path = Path.Combine(Paths.Profiles, safe + ".json");

            int suffix = 2;
            while (File.Exists(path) && !string.Equals(path, exceptPath, StringComparison.OrdinalIgnoreCase))
                path = Path.Combine(Paths.Profiles, $"{safe} ({suffix++}).json");

            return path;
        }

        private static string SafeFileName(string name)
        {
            string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();

            if (safe.Length == 0)
                safe = "profile";

            // 文件名太长 Windows 会炸，留点余量
            if (safe.Length > 60)
                safe = safe.Substring(0, 60).Trim();

            return safe;
        }

        private string NextName(string baseName)
        {
            var taken = new HashSet<string>(_items.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);

            if (!taken.Contains(baseName))
                return baseName;

            int index = 2;
            while (taken.Contains($"{baseName} {index}"))
                index++;

            return $"{baseName} {index}";
        }

        // ── 首次运行时塞几套预设 ──────────────────────────────────────

        private void SeedBuiltinPresets()
        {
            // 目录已经存在就说明跑过了（或者用户自己清空了），不再塞
            if (Directory.Exists(Paths.Profiles))
                return;

            try
            {
                Directory.CreateDirectory(Paths.Profiles);

                foreach (var profile in BuiltinPresets())
                    WriteProfile(profile, MakeFilePath(profile.Name));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("FastFlagProfiles::Seed", ex.ToString());
            }
        }

        private static List<FastFlagProfile> BuiltinPresets() => new()
        {
            new FastFlagProfile
            {
                Name = "低配流畅",
                Note = "核显、老机器用。关阴影关后期，贴图拉到最低，帧数优先。",
                SavedAt = DateTime.Now,
                Flags = new Dictionary<string, string>
                {
                    ["DFIntTaskSchedulerTargetFps"] = "60",
                    ["FIntRenderShadowIntensity"] = "0",
                    ["FFlagDisablePostFx"] = "True",
                    ["DFFlagTextureQualityOverrideEnabled"] = "True",
                    ["DFIntTextureQualityOverride"] = "0",
                    ["FIntTerrainArraySliceSize"] = "4",
                    ["DFIntDebugFRMQualityLevelOverride"] = "1",
                    ["FFlagDebugSkyGray"] = "True"
                }
            },

            new FastFlagProfile
            {
                Name = "均衡",
                Note = "只锁帧数，画质全交给游戏自己调。不知道选哪个就先用这个。",
                SavedAt = DateTime.Now,
                Flags = new Dictionary<string, string>
                {
                    ["DFIntTaskSchedulerTargetFps"] = "120"
                }
            },

            new FastFlagProfile
            {
                Name = "高画质",
                Note = "独显机器截图用。阴影满、贴图拉满、开 MSAA，帧数会掉。",
                SavedAt = DateTime.Now,
                Flags = new Dictionary<string, string>
                {
                    ["DFIntTaskSchedulerTargetFps"] = "240",
                    ["FIntRenderShadowIntensity"] = "1",
                    ["DFFlagTextureQualityOverrideEnabled"] = "True",
                    ["DFIntTextureQualityOverride"] = "3",
                    ["FIntTerrainArraySliceSize"] = "512",
                    ["FIntDebugForceMSAASamples"] = "4"
                }
            },

            new FastFlagProfile
            {
                Name = "竞技低延迟",
                Note = "打架用。高帧、低画质、关阴影，右上角显示帧数方便看掉没掉。",
                SavedAt = DateTime.Now,
                Flags = new Dictionary<string, string>
                {
                    ["DFIntTaskSchedulerTargetFps"] = "240",
                    ["FIntRenderShadowIntensity"] = "0",
                    ["FFlagDisablePostFx"] = "True",
                    ["DFFlagTextureQualityOverrideEnabled"] = "True",
                    ["DFIntTextureQualityOverride"] = "1",
                    ["FIntTerrainArraySliceSize"] = "16",
                    ["DFIntDebugFRMQualityLevelOverride"] = "3",
                    ["FFlagDebugDisplayFPS"] = "True"
                }
            }
        };

        // ── 提示 ──────────────────────────────────────────────────────

        private void OnRefresh(object sender, RoutedEventArgs e) => Reload();

        private void Info(string title, string message, InfoBarSeverity severity)
        {
            StatusBar.Title = title;
            StatusBar.Message = message;
            StatusBar.Severity = severity;
            StatusBar.IsOpen = true;
        }

        private void Warn(string title, string message) => Info(title, message, InfoBarSeverity.Warning);

        private void Fail(string ident, Exception ex)
        {
            App.Logger.WriteLine(ident, ex.ToString());
            Info("出错了", ex.Message, InfoBarSeverity.Error);
        }
    }
}
