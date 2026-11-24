<p align="center">
    <img src="Images/Luckstrap.png" width="128">
</p>

<h1 align="center">Luckstrap</h1>

<p align="center">新一代 Roblox 启动器</p>

----

## 这是干嘛的

官方启动器负责拿版本号、下载客户端、拉起游戏。Luckstrap 换掉的就是这一层，不碰游戏进程，也不注入。

从 [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) 改的，启动流程、FastFlags、模组那套底子是他们写的，功能是在上面加出来的。

## 功能

**换号**：几个账号的登录态存下来，点一下切过去，不用重新输密码。当前账号显示在标题栏右上角，点一下进入换号页。

**收藏和快速启动**：常玩的游戏存起来，粘 Place ID、游戏链接或者私服访问码都能进。不用翻网页，直接从「最近玩过」里挑也行。

**区服选择**：填个游戏，选个地区，直接进那个地区的服。边查边找，找到一台就停，不用等全部查完。想自己挑就列全部服务器，看人数和地区选一台。

**最近玩过**：进过的服务器都记着，最多 50 条，重启也在。装之前玩的那几局也能从 Roblox 自己的日志里补回来。掉线了能回同一台，前提是它还没关。

**网络诊断**：进不去游戏的时候跑一遍，能看到是哪个域名断了、卡在哪一步。

**Discord 状态**：显示在玩什么、在哪个服。游戏里的脚本可以用 BloxstrapRPC 改显示内容，接口名没换，老脚本照常跑。网页版 Discord 也能显示，但要装浏览器插件，所以标了实验性。

**FastFlags**：编辑器带预设，也能存成方案给不同游戏用。旧版客户端可以装回去。

**浏览器启动链接**：网页上点 Play 走 Luckstrap，FastFlags 和模组才生效。不想让它接管就在设置里关掉，交回官方启动器。

**模组**：死亡音效、鼠标指针、字体、表情。

**界面**：几种启动器风格可选，跟随系统语言，支持 14 种语言。

## 安装

去 [Releases](https://github.com/hckerelauk-git/Luckstrap/releases) 下 `Luckstrap.exe`，双击，选个位置，装。

单文件，不用装 .NET 运行时，目录里也不会丢一堆 dll。

Windows 会提示"未知发布者"，因为没买代码签名证书。点"更多信息"再点"仍要运行"。

## 从 Bloxstrap 换过来

直接覆盖安装。配置目录是 `%LOCALAPPDATA%\Luckstrap`，跟 Bloxstrap 那个分开放的，两边不打架。想彻底换过来，装完把 Bloxstrap 卸了。

## 常见问题

**会被封吗**

不会。不碰游戏进程，不读写它的内存。官方启动器该做的事它做，官方没做的它不做。

**装完打不开，提示缺 dll**

下错包了。Releases 里那个 `Luckstrap.exe` 是自包含的单文件。

**进游戏弹 SSL 错误**

先去设置里跑「网络诊断」。如果「客户端部署」那一项红了，就是本机到 Roblox 的链路有问题，挂代理再试。

**杀毒报毒**

没签名的小程序经常被误报。源码都在这儿，不放心可以自己编。

**自动更新**

启动时检查，有新版会自己下。下载走 GitHub 镜像，慢的时候要等几分钟，别当成卡死了。实在等不了就去 Releases 手动下，覆盖安装目录里的 exe 就行。

## 编译

要 .NET 6 SDK。

```
dotnet publish Bloxstrap/Bloxstrap.csproj -c Release -r win-x64 --self-contained true -o publish
```

出来就一个 `Luckstrap.exe`。

## 许可

MIT，跟上游一样。
