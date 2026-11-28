- Help links next to settings options now point at Luckstrap's own wiki
- Removed the "hide GUIs" option, it requires joining a Roblox group
- The download button in the installer now opens Luckstrap's releases
- Help links on the About page and in the uninstall info point at the wiki home

---

- 设置里选项右边的帮助链接改为指向 Luckstrap 自己的 wiki
- 去掉「隐藏界面」选项，它需要加入 Roblox 群组才生效
- 安装界面的下载按钮改为打开 Luckstrap 的 releases
- 关于页和卸载信息的帮助链接指向 wiki 首页

---

**Which file / 下载哪个**

| File | Runtime | Size | Startup |
|---|---|---|---|
| `Luckstrap-full.exe` | none / 不用 | 177MB | 1.3–1.6 s |
| `Luckstrap-lite.exe` | .NET 10 desktop runtime / 桌面运行时 | 13MB | 1.5–2.0 s |
| `Luckstrap-v*-win-x64-full-compressed.zip` | none / 不用 | 71MB | 2.3–3.0 s |
| `Luckstrap-v*-win-x64-lite-compressed.zip` | .NET 10 desktop runtime / 桌面运行时 | 5MB | 1.2–1.5 s |

Both exes are single files, double-click to run.

两个 exe 都是单文件，下载下来直接双击。

Unpacking a zip gives you `Luckstrap.exe`. The `full-compressed` build unpacks itself on launch, about a second slower. `lite-compressed` unpacks to an exe plus a pile of dlls, do not take the exe alone.

压缩包解压后是 `Luckstrap.exe`。`full-compressed` 那份运行时要先解压自己，启动慢一秒左右。`lite-compressed` 解压出来是 exe 加一堆 dll，不要单独把 exe 拿走。

`lite` builds need the .NET 10 desktop runtime. If it is missing, Windows shows a prompt with a download link.

`lite` 系列需要 .NET 10 桌面运行时，没装的话 Windows 会弹提示并给出下载地址。
