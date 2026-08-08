# CraftStation

> 新一代我的世界 Java 启动器 · 终末地风格

CraftStation 是一款面向 **Minecraft Java 版**玩家的桌面启动器，把版本管理、加载器安装、资源下载、整合包、服务器直连和模组诊断整合成一条顺畅的流水线。

## 功能

- **账户系统**：微软正版登录（内嵌浏览器授权，Token 加密落盘、静默刷新）、离线账户、皮肤与披风预览
- **版本与加载器**：官方 Release / Snapshot / Old 版本库；Forge、NeoForge、Fabric、Quilt、OptiFine、LiteLoader 一键安装
- **下载加速**：默认 BMCLAPI 镜像，失败自动回退官方源，可自定义下载源与并发数
- **实例管理**：实例 = 版本 + 加载器 + 独立启动参数；支持版本隔离、收藏、复制、删除、导入导出
- **资源中心**：Modrinth 搜索与下载，按加载器 / 游戏版本 / 分类过滤
- **本地资源**：mods、资源包、光影包、存档统一管理，存档可查看 level.dat 信息
- **服务器**：Minecraft 1.7+ JSON Ping（MOTD、在线人数、延迟），一键直连启动
- **整合包**：导入 / 导出 Modrinth `.mrpack` 与 CurseForge `.zip`，本地文件以 overrides 方式完整携带
- **自研模组体检**：解析 `mods.toml` / `fabric.mod.json` / `quilt.mod.json`，检测缺失前置、版本冲突、重复 modId、加载器混用、损坏 jar；支持依赖树与反向依赖、定位 / 禁用 / 删除、Modrinth 搜索缺失依赖、导出报告
- **Java 自动管理**：按版本自动选择 Java 8 / 17 / 21，支持系统扫描与自定义路径

## 界面

深色工业科幻视觉：45° 切角卡片、细网格背景、CMYK 信号色带、丝滑过渡动画，所有控件深度定制，无浏览器默认样式。支持 Windows 10 / 11，125% / 150% DPI 自适应。

## 下载

- Windows：请到 [GitHub Releases](https://github.com/tbsky166/CraftStation/releases) 下载最新版（暂未发布）

## 快速开始

1. 下载并打开 CraftStation
2. 在「账户」页添加微软账户或离线账户
3. 在「版本库」安装你喜欢的 Minecraft 版本
4. 需要模组时，先安装 Fabric / Forge，再到「资源市场」搜索下载
5. 点「启动」进入游戏

## 从源码构建

环境要求：Windows 10 / 11、.NET 10 SDK、WebView2 Runtime。

```powershell
git clone --recurse-submodules https://github.com/tbsky166/CraftStation.git
cd CraftStation

# 复制配置模板并填入你自己的 Azure Client ID
Copy-Item CraftStation.Core\Config.cs.example CraftStation.Core\Config.cs

dotnet build
dotnet test
```

## 微软登录说明

- 登录使用 Azure 公开客户端 ID，配置集中在 `CraftStation.Core/Config.cs`（真实值仅本地保留，仓库只提交 `Config.cs.example`）。
- 自建 Azure 应用后，需要向 Mojang 申请 Minecraft API 权限：https://aka.ms/mce-reviewappid ，未获批前登录会返回 403。

## 仓库结构

本项目拆分为三个仓库，通过 Git Submodule 关联：

| 仓库 | 说明 |
| --- | --- |
| [CraftStation](https://github.com/tbsky166/CraftStation) | WPF 前端 + HTML 界面 |
| [CraftStation.Core](https://github.com/tbsky166/CraftStation.Core) | 核心业务库（无 UI 依赖） |
| [CraftStation.Tests](https://github.com/tbsky166/CraftStation.Tests) | 单元测试 |

## 许可证

本项目采用 [MIT 许可证](LICENSE)。
