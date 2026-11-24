<p align="center">
    <img src="Images/Luckstrap.png" width="128">
</p>

<h1 align="center">Luckstrap</h1>

<p align="center">新一代 Roblox 启动器</p>

----

基于 [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) 修改

## 功能

- **多账号**：保存多个账号的登录态，一键切换
- **收藏**：保存常用游戏，支持 Place ID、游戏链接、私服访问码
- **快速启动**：从收藏或最近玩过启动游戏
- **区服选择**：按地区筛选服务器，也可查看全部服务器
- **最近玩过**：保留最近 50 个服务器记录
- **网络诊断**：检测启动链路上各域名的连通性
- **Discord 状态**：显示当前游戏与服务器，支持 BloxstrapRPC
- **FastFlags**：编辑器、预设、方案管理、旧版客户端
- **模组**：死亡音效、鼠标指针、字体、表情
- **界面**：多种启动器风格，跟随系统语言

## 安装

从 [Releases](https://github.com/hckerelauk-git/Luckstrap/releases) 下载 `Luckstrap.exe` 运行。

单文件，无需安装 .NET 运行时。

## 从 Bloxstrap 迁移

覆盖安装即可。配置目录为 `%LOCALAPPDATA%\Luckstrap`，与 Bloxstrap 分开。

## 常见问题

**会被封号吗**

不会。不修改游戏进程，不读写游戏内存。

**提示缺少 DLL**

下载的文件不对。Releases 里的 `Luckstrap.exe` 是自包含单文件。

**SSL 错误**

在设置中运行「网络诊断」。

**杀毒软件报毒**

未签名的程序可能被误报。源码在本仓库，可自行编译。

**自动更新**

启动时检查更新，通过 GitHub 镜像下载。

## 编译

需要 .NET 6 SDK。

```
dotnet publish Bloxstrap/Bloxstrap.csproj -c Release -r win-x64 --self-contained true -o publish
```

## 许可

MIT
