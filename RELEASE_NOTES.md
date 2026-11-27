- 运行库从 .NET 6 升到 .NET 10
- 依赖包和构建工具升到当前版本

---

**下载哪个**

| 文件 | 要不要装运行时 | 体积 | 启动 |
|---|---|---|---|
| `Luckstrap-v*-win-x64-full-compressed.zip` | 不用 | 约 77MB | 2.3–3.0 秒 |
| `Luckstrap-v*-win-x64-full.zip` | 不用 | 约 177MB | 1.3–1.6 秒 |
| `Luckstrap-v*-win-x64-lite.zip` | 要 .NET 10 桌面运行时 | 约 13MB | 1.5–2.0 秒 |
| `Luckstrap-v*-win-x64-lite-folder.zip` | 要 .NET 10 桌面运行时 | 约 13MB | 1.2–1.5 秒 |

不知道选哪个就用 `full-compressed`。解压出来的 `Luckstrap.exe` 直接双击运行。

`lite` 和 `lite-folder` 都需要 .NET 10 桌面运行时，没装的话 Windows 会弹提示并给出下载地址。两个体积差不多，区别是 `lite` 只有一个 exe，`lite-folder` 解压后是 exe 加一堆 dll，启动略快，但不要单独把 exe 拿走。
