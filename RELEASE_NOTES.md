- Web Discord status now shows the game thumbnail and the account avatar

**Which file**

| File | Runtime | Size | Startup |
|---|---|---|---|
| `Luckstrap-full.exe` | none | 177MB | 1.3–1.6 s |
| `Luckstrap-lite.exe` | .NET 10 desktop runtime | 13MB | 1.5–2.0 s |
| `Luckstrap-v*-win-x64-full-compressed.zip` | none | 71MB | 2.3–3.0 s |
| `Luckstrap-v*-win-x64-lite-compressed.zip` | .NET 10 desktop runtime | 5MB | 1.2–1.5 s |

Both exes are single files, double-click to run.

Unpacking a zip gives you `Luckstrap.exe`. The `full-compressed` build unpacks itself on launch, about a second slower. `lite-compressed` unpacks to an exe plus a pile of dlls, do not take the exe alone.

`lite` builds need the .NET 10 desktop runtime. If it is missing, Windows shows a prompt with a download link.

---

- 网页版 Discord 状态显示游戏封面和账号头像

**下载哪个**

| 文件 | 运行时 | 大小 | 启动 |
|---|---|---|---|
| `Luckstrap-full.exe` | 不用 | 177MB | 1.3–1.6 秒 |
| `Luckstrap-lite.exe` | .NET 10 桌面运行时 | 13MB | 1.5–2.0 秒 |
| `Luckstrap-v*-win-x64-full-compressed.zip` | 不用 | 71MB | 2.3–3.0 秒 |
| `Luckstrap-v*-win-x64-lite-compressed.zip` | .NET 10 桌面运行时 | 5MB | 1.2–1.5 秒 |

两个 exe 都是单文件，下载下来直接双击。

压缩包解压后是 `Luckstrap.exe`。`full-compressed` 那份运行时要先解压自己，启动慢一秒左右。`lite-compressed` 解压出来是 exe 加一堆 dll，不要单独把 exe 拿走。

`lite` 系列需要 .NET 10 桌面运行时，没装的话 Windows 会弹提示并给出下载地址。
