---
title: "CraftStation — 新一代我的世界 Java 启动器"
description: "参考 PCL-CE 功能体系的现代 Minecraft Java 启动器：微软正版登录、一键安装 Forge/Fabric/NeoForge、模组体检与冲突检测、整合包导入导出，全部集于一身的工业科幻风格启动器。"
pubDate: 2026-08-08
heroImage: "/images/craftstation-hero.png"
author: "CraftStation Team"
tags: ["Minecraft", "启动器", "Java", "WPF", "C#", "开源"]
draft: false
---

<!--
  使用方式：
  - 放入 Astro 内容集合：src/content/blog/craftstation-promo.md（或 src/pages/blog/ 下）
  - 若使用经典 Layout 路由，请在 frontmatter 增加：layout: "../../layouts/BlogPost.astro"
  - heroImage 对应 public/images/craftstation-hero.png，可替换为自己的截图
-->

# CraftStation

> 参考 PCL-CE 主要功能体系打造的现代 Minecraft Java 启动器。
> 工业科幻界面 + 流畅动效 + 自研模组体检，让装模组、开服、换版本都变得简单。

![CraftStation 界面预览](/images/craftstation-hero.png)

CraftStation 是一款面向 **Minecraft Java 版**玩家的桌面启动器。它不只是一层版本列表——从正版登录、加载器安装、实例管理，到 Modrinth 下载、整合包导入导出、模组冲突诊断，一整套流程都做成了顺手的工具。

## 核心特性

### 账户系统

- **微软正版登录**：内嵌浏览器完成 OAuth 授权，Token 使用 DPAPI 加密落盘，支持静默刷新与多账户切换
- **离线账户**：一键添加，自动生成离线 UUID，局域网联机友好
- **皮肤与披风预览**：登录后直接查看正版角色信息

### 版本与加载器

- 官方版本列表：Release / Snapshot / Old，安装、更新、修复、删除
- **Forge / NeoForge / Fabric / Quilt / OptiFine** 一键安装，自动推荐与当前 MC 版本匹配的加载器版本
- 下载走 **BMCLAPI 镜像**，失败自动回退官方源，国内下载速度拉满
- Java 运行时自动安装，按版本自动选择 Java 8 / 17 / 21

### 实例管理

- 实例 = 版本 + 独立启动参数 + 图标 + 描述 + 服务器直连
- 支持**版本隔离**（mods / saves / config 独立，libraries 共享）、收藏、复制、删除、导入导出
- 启动过程实时日志、崩溃日志定位、进程停止与重启

### 资源与服务器

- 本地 mods / 资源包 / 光影包 / 存档 / 服务器统一管理
- **Modrinth 搜索与下载**：按加载器、游戏版本、分类过滤，支持更新检测
- **服务器一键直连**：Minecraft 1.7+ JSON Ping，显示 MOTD、在线人数、版本与延迟
- 整合包导入导出：支持 **Modrinth .mrpack** 与 **CurseForge .zip**，本地文件以 overrides 携带，无需 API 也能完整还原

### 自研功能：模组体检与冲突检测

这是 CraftStation 的独门能力：

- 解析 `mods.toml`、`fabric.mod.json`、`quilt.mod.json` 等元数据
- 自动发现**缺失前置**、**版本范围不匹配**、**重复 modId**、**显式冲突**、**加载器混用**与损坏 jar
- 问题按错误 / 警告分级，支持定位文件、禁用、删除
- 选中模组的**前置依赖树与反向依赖**一目了然
- 一键在 Modrinth 搜索缺失依赖，或导出完整体检报告

## 界面

界面灵感来自《明日方舟：终末地》的工业科幻视觉与 fz.wiki 的组件质感：

- 深色工业风：45° 切角卡片、细网格背景、CMYK 色带、扫描线动效
- 黄色主强调色与黑色侧栏的明暗对比，按钮、表单、弹窗全部深度定制
- 丝滑的过渡动画与实时反馈，启动、下载、登录全程有状态
- 支持 Windows 10 / 11，自适应 125% / 150% DPI

## 快速开始

1. 下载 CraftStation（见下方链接）
2. 打开后先在「账户」添加微软账户或离线账户
3. 在「版本」选择喜欢的 Minecraft 版本并安装
4. 需要模组？安装 Fabric / Forge 后到「资源中心」搜索下载
5. 点「启动」，进入游戏

## 常见问题

**Q：需要安装 Java 吗？**

不需要。CraftStation 会根据游戏版本自动下载并管理对应 Java 运行时。

**Q：支持正版登录吗？**

支持。使用内嵌浏览器完成微软 OAuth 登录，Token 加密保存，重启后自动静默刷新。

**Q：下载速度慢怎么办？**

默认使用 BMCLAPI 镜像并自动回退官方源；也可以在设置中自定义下载源与并发数。

**Q：登录提示 403？**

自建 Azure 应用需要向 Mojang 申请 Minecraft API 权限，通过 https://aka.ms/mce-reviewappid 提交申请即可（详见项目说明）。

## 下载与链接

- GitHub Releases：`https://github.com/你的仓库/releases`（替换为实际地址）
- 项目主页：`https://github.com/你的仓库`（替换为实际地址）
- 问题反馈：请在仓库 Issues 中提交

## 开源说明

CraftStation 使用 WPF / C# (.NET 10) 开发，核心逻辑与 UI 分离，欢迎贡献代码、翻译与创意。开源许可证与贡献指南见仓库 README。
