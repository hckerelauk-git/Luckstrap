<p align="center">
    <img src="Images/Luckstrap.png" width="128">
</p>

<h1 align="center">Luckstrap</h1>

<p align="center">Roblox 启动器的第三方替代品，在官方启动器的基础上补了一些日常用得上的功能。</p>

----

## 这是什么

Roblox 官方启动器只负责把游戏拉起来，别的什么都不管。Luckstrap 换掉了那一层，游戏本体还是官方客户端，一行没动。

不注入、不读内存、不改游戏逻辑，跟官方客户端的交互方式和你在网页上点 Play 完全一样。

## 加了些啥

**日常用得上的**

- **账号切换** —— 存多份登录态，点一下换号，不用每次重新登录
- **收藏游戏** —— 常玩的游戏存起来，点一下直接进，不用翻网页
- **快速启动** —— 粘 Place ID、游戏链接或私服代码，生成链接或者直接进游戏
- **服务器历史** —— 进过的服务器都记着，掉线了能直接回同一个
- **网络诊断** —— 进不去游戏的时候跑一遍，看是哪个域名断了

**Discord 状态**

- 自动显示当前在玩什么游戏、服务器在哪个地区
- 支持游戏内脚本通过 `BloxstrapRPC` 自定义显示内容（接口没改名，老脚本还能用）

**画面和性能**

- FastFlags 编辑器，带预设（画质、光照、抗锯齿、MSAA 之类）
- 配置方案，不同游戏用不同的一套设置
- 旧版 Roblox 客户端管理，可以回退版本
- 模组：死亡音效、鼠标指针、字体、表情

**其他**

- 启动器外观：多种风格和图标可选
- 中文界面（跟随系统语言）
- 配置导出，换机器的时候直接带走

## 安装

1. 去 [Releases](https://github.com/hckerelauk-git/Luckstrap/releases) 下载 `Luckstrap.exe`
2. 双击运行，选个安装位置，点安装
3. 装完之后开始菜单和桌面会有快捷方式

就一个 exe，不用装 .NET 运行时，不用解压一堆 dll。

**Windows 提示"未知发布者"是正常的** —— 这个程序没买代码签名证书。点"更多信息" → "仍要运行"。

## 从 Bloxstrap 迁移

可以直接覆盖安装。Luckstrap 用自己独立的配置目录（`%LOCALAPPDATA%\Luckstrap`），不会动原来 Bloxstrap 的设置，两边可以共存。

想彻底换过来的话，装完把 Bloxstrap 卸了就行。

## 常见问题

**会被封号吗**

不会。这个程序不碰游戏进程，不注入，不读写游戏内存。它做的事情就是下载官方客户端、改几个官方自己暴露的启动参数、然后启动它。

**装完打不开 / 报缺 dll**

说明你下的不是单文件版。去 Releases 下最新的 `Luckstrap.exe`，那个是自包含的。

**进游戏的时候弹 SSL 错误**

先跑一遍设置里的「网络诊断」。如果「客户端部署」那项挂了，说明本机到 Roblox 的链路有问题，挂代理再试。

**杀毒软件报毒**

没有签名的小众程序经常被误报。源码在这个仓库里，可以自己编译。

## 编译

需要 .NET 6 SDK。

```
dotnet publish Bloxstrap/Bloxstrap.csproj -c Release -r win-x64 --self-contained true -o publish
```

产物是单个 `Luckstrap.exe`，直接能跑。

## 致谢

界面库用的是 [WPF UI](https://github.com/lepoco/wpfui)。

这个项目是从 [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) 改出来的，启动流程、FastFlags 管理、模组系统这些底子都是他们写的，按 MIT 协议继续开源。

## 许可

MIT，见 [LICENSE](LICENSE)。上游 Bloxstrap 也是 MIT，改的时候没有换协议。
