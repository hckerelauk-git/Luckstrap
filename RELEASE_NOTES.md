- 工作室页加了「本地文件」，可以选 .rbxl / .rbxlx 用工作室打开，或者建桌面快捷方式
- 自更新按发行形态下载对应产物，lite 不会再被换成自包含版
- lite-compressed 安装时连同依赖一起复制到安装目录
- 启动菜单加了 Roblox Studio 入口，没装过时显示「安装 Roblox Studio」，装到 Luckstrap 的 Versions 目录
- 新增工作室页，可以直接启动 Roblox Studio
- 新增「启动前检查更新」开关，关闭后直接启动已安装的工作室

---

**下载哪个**

| 文件 | 装运行时 | 大小 | 启动 |
|---|---|---|---|
| `Luckstrap-full.exe` | 不用 | 177MB | 1.3–1.6 秒 |
| `Luckstrap-lite.exe` | .NET 10 桌面运行时 | 13MB | 1.5–2.0 秒 |
| `Luckstrap-v*-win-x64-full-compressed.zip` | 不用 | 71MB | 2.3–3.0 秒 |
| `Luckstrap-v*-win-x64-lite-compressed.zip` | .NET 10 桌面运行时 | 5MB | 1.2–1.5 秒 |

两个 exe 都是单文件，下载下来直接双击。

压缩包解压后是 `Luckstrap.exe`。`full-compressed` 那份运行时要先解压自己，启动慢一秒左右。`lite-compressed` 解压出来是 exe 加一堆 dll，不要单独把 exe 拿走。

`lite` 系列需要 .NET 10 桌面运行时，没装的话 Windows 会弹提示并给出下载地址。
