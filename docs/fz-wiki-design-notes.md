# fz.wiki 设计移植说明

来源：https://fz.wiki （终末地 Wiki，Next.js + Tailwind + CSS Modules）

## 提取的暗色设计 Token

| fz.wiki 变量 | 值 | 对应 CraftStation token |
| --- | --- | --- |
| `--color-surface` | `#181818` | `BgColor` |
| `--color-surface-muted` | `#1F1F1F` | `BgAltColor` |
| `--color-surface-raised` | `#232323` | `PanelColor` |
| `--color-border` | `#2B3136` | `BorderColor` |
| `--color-border-strong` | `#41484F` | `BorderStrongColor` |
| `--color-ink` | `#EEE` | `TextColor` |
| `--color-ink-muted` | `#A8B0B7` | `TextFaintColor` |
| `--color-accent` | `#D8BF00` | `AccentColor` |
| `--color-accent-strong` | `#B99B00` | 按压态 `#B99B00` |
| `--color-accent-soft` | `#272302` | `AccentSoftColor` |
| `--color-accent-glow` | `#ECD548` | `AccentGlowColor` |
| `--color-success` | `#00C7BD` | `SuccessColor` |
| `--color-warn` | `#D97706` | `WarningColor` |
| `--color-danger` | `#DC2626` | `DangerColor` |
| `--color-rarity-4` | `#9A7DFF` | `ComplementColor`（黄色互补紫） |
| 圆角 | `4px / 6px / 8px / pill` | 控件 `6px`、Hero `8px`、徽章 `9999` |

## 组件映射（CSS → WPF）

| fz.wiki 组件 | CSS 行为 | CraftStation 实现 |
| --- | --- | --- |
| `hud-glow` 按钮 | 悬停金色描边 + 辉光 `inset 0 0 18px -5px #ffd84a` | `HudGlowButtonStyle` / `PrimaryButtonStyle` / `SecondaryButtonStyle` 悬停 `DropShadowEffect`（金色辉光） |
| `ef-chamfer-sm` | 小切角 | 用 `CornerRadius=6` 近似 |
| 描边强调按钮 | `border-accent text-accent-strong hover:border-accent` | `OutlineButtonStyle` |
| 幽灵按钮 | `text-ink-muted hover:text-accent` | `GhostButtonStyle` |
| `industrial-card` | 悬停上浮 `-2px`、金色描边、阴影 | `CardStyle` 悬停金色描边；`QuickActionStyle` 上浮 + 投影 |
| `wiki-input` | 聚焦金色描边 + 辉光圈 | `TextBox` / `ComboBox` 聚焦 `BorderBrush=Accent` + `DropShadowEffect` |
| `timeline-tag` / pill | 药丸徽章 + 语义色 | `PillBadgeStyle` + `SuccessBadgeStyle` / `DangerBadgeStyle` / `WarningBadgeStyle` / `NeutralBadgeStyle` |
| `sidebar-link` | 3px 金色强调条 + 激活加粗 | `NavItemStyle` 选中金色胶囊 + 黑色文字（用户指定方案） |
| `top-signal-strip` | 顶部 3px 强调条 | `MainWindow` 顶部 `3px AccentBrush` 信号条 |
| `ef-eyebrow` | 小号加粗强调标题 | `EyebrowStyle` |
| `::selection` | 金色选区 | `TextBox.SelectionBrush=AccentBrush` |

## 说明

这是 WPF 原生移植：布局和交互与 Web 版一一对应，但 `clip-path`、`backdrop-blur`、CSS 变量等 Web 特性用 WPF 的 `CornerRadius`、`DropShadowEffect`、ResourceDictionary Token 等价实现。


## ????????curl?

```bash
# ??? HTML / CSS / JS chunk
curl -sSL -A "Mozilla/5.0" --compressed https://fz.wiki/ -o index.html
curl -sSL -A "Mozilla/5.0" --compressed https://fz.wiki/_next/static/chunks/1tdnbjysz5we4.css -o main.css
curl -sSL -A "Mozilla/5.0" --compressed https://fz.wiki/_next/static/chunks/3_ql57axg728o.css -o extra.css
# ? HTML ???? js/css ????????? grep ????
```

?? CSS ???????

```css
.ef-chamfer { clip-path: polygon(0 0, calc(100% - 9px) 0, 100% 9px, 100% 100%, 9px 100%, 0 calc(100% - 9px)); }
.ef-chamfer-sm { clip-path: polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px)); }
.hud-glow { transition: color .16s, border-color .16s, box-shadow .16s; }
.hud-glow:hover { box-shadow: inset 0 0 18px -5px #ffd84a99; }
.ef-entry-glow { box-shadow: inset 0 0 20px -4px #ffd84a99; }
.industrial-card { transition: transform .18s, border-color .18s, box-shadow .18s, background-color .18s; }
.industrial-card:hover { border-color: var(--color-accent); transform: translateY(-2px); box-shadow: 0 14px 34px #00000059; }
.top-signal-strip { background-image: linear-gradient(90deg, #ff00f0 0 35%, var(--color-accent) 35% 82%, var(--color-system) 82% 100%); }
.timeline-tag { letter-spacing: .04em; border: 1px solid transparent; padding: 0 .4rem; font-size: .7rem; font-weight: 700; line-height: 1.4; display: inline-flex; }
.timeline-tag--feature { background: var(--color-accent-soft); color: var(--color-accent-fg); border-color: var(--color-accent); }
.timeline-tag--fix { background: color-mix(in srgb, var(--color-success) 14%, transparent); color: var(--color-success); border-color: var(--color-success); }
.timeline-tag--breaking { background: color-mix(in srgb, var(--color-danger) 12%, transparent); color: var(--color-danger); border-color: var(--color-danger); }
.sidebar-link:before { border-top: 1.5px solid var(--color-accent); border-left: 1.5px solid var(--color-accent); }
.sidebar-link:after { border-right: 1.5px solid var(--color-accent); border-bottom: 1.5px solid var(--color-accent); }
```

WPF ?????

- `ef-chamfer` ? `CraftStation.Controls.ChamferBorder`???????`Chamfer=6/8` ??????????? Primary/Hero/Secondary/Outline/Ghost/HudGlow/QuickAction ???
- `top-signal-strip` ? `FzSignalStripBrush`???????????????????? 3px ????


## ??????????? fz.wiki ?????

| ?? | fz.wiki ?? | CraftStation ?? |
| --- | --- | --- |
| Button??/??/??/??/??/??? | `hud-glow` / `ef-chamfer(-sm)` / Tailwind border-accent | `PrimaryButtonStyle` / `SecondaryButtonStyle` / `OutlineButtonStyle` / `GhostButtonStyle` / `HudGlowButtonStyle` / `WindowButtonStyle`??? `ChamferBorder` + ???? |
| TextBox | `wiki-input`????????+???? | ??????`Chamfer=0`??? `BorderBrush=Accent` + `DropShadowEffect` |
| ComboBox | ?? `ef-chamfer-sm border-border-strong shadow-[0_12px_24px_-12px_rgba(0,0,0,.35)]` | ?????????????? `ChamferBorder` + ??? + ?? |
| ComboBoxItem | ?? `accent-soft` + `text-accent` | ???????? `AccentSoftBrush` + ???? |
| CheckBox | `accent-color: var(--color-accent)` | ??? 16px ?????????+??+??? |
| ListBox | ???? + ???? | ?? `ScrollViewer + ItemsPresenter` ?? |
| ScrollBar | `--sidebar-scroll-thumb-color` / active accent | ?/????????? |
| TabControl / TabItem | `wiki-tabs`????? + tablist ???? | `ChamferBorder` ?? + `TabPanel`???????? + `accent-soft` ? |
| ProgressBar | ? | ??? Track/Indicator ?? |
| ToolTip | `--color-tooltip:#18181b` / `#fafafa` | ????????? |
| ContextMenu / MenuItem | ?? `accent-soft` + `text-accent` | ?????????? |
| ???? | `focus-visible:ring-accent` | `FzFocusVisualStyle`??? 1px ?????????????/???? |


## ?????????FzWikiControls.xaml?

????????????? WPF ??????? fz ???????

- ???ContentControl / HeaderedContentControl / ItemsControl / Frame / ScrollViewer / Label
- ???PasswordBox / RichTextBox
- ???RadioButton / Slider / ?? ToggleButton / RepeatButton / ?? Thumb
- ???ListBoxItem?????ListView?GridViewColumnHeader
- ???DataGrid?DataGridColumnHeader?DataGridRow?DataGridCell?DataGridRowHeader
- ???TreeView?TreeViewItem?Expander?GroupBox
- ????Menu?ToolBarTray?ToolBar?StatusBar?MenuItem/ContextMenu Separator
- ???Calendar?CalendarDayButton?CalendarButton?DatePicker

????? Endfield.xaml ?????App.xaml????????????????????? WPF ???


## ?? HTML ???WebView2?

WPF ?? 1:1 ?? `clip-path`?`backdrop-filter`?`color-mix` ? CSS????? WebView2 ?? HTML ???????????

- ?????? ???? Web ????? HTML??
- ?????`Assets/Web/index.html` + `app.css` + `app.js`?fz.wiki ???????
- ???????`app.local` ? `Assets/Web`
- ???JS `postMessage` ? C# `HtmlBridge`?getState / getVersions / getInstances / launch / installVersion / createInstance?
- ????????????????????+?????????+???
- ????????????????????

?????`WebPreviewWindow.xaml(.cs)`?`Services/HtmlBridge.cs`?`Assets/Web/*`?


## ?? HTML ?????????

WebView2 ??????? 9 ??????? HtmlBridge ? WPF ???????

| ?? | ???? |
| --- | --- |
| ??? | getState / launchInstance / stopGame / refresh |
| ??? | getVersions / installVersion / repairVersion / deleteVersion / getLoaderVersions / installLoader |
| ?? | getInstances / createInstance / selectInstance / saveInstance / deleteInstance / launchInstance / openGameFolder / importPack / exportPack |
| ???? | getResources / importResource / toggleResource / deleteResource / openResourceFolder / openSaveFolder |
| ???? | searchProjects / getProjectVersions / downloadProjectVersion |
| ???? | scanMods / getDependencyTree / disableMod / deleteMod / exportModReport |
| ??? | getServers / addServer / deleteServer / pingServer / launchServer |
| ?? | getAccounts / addOfflineAccount / selectAccount / removeAccount / loginMicrosoft / loginDeviceCode???????/ refreshAccount / downloadSkin |
| ?? | getSettings / saveSettings / openDataFolder / openLogsFolder / checkUpdate / scanJava |
| ??/?? | stopGame / getGameLog |

?????JS `window.chrome.webview.postMessage({id,type,payload})` ? C# `HtmlBridge.HandleAsync` ? `window.__csCallback(id, ok, data)`?C# ? JS ??? `window.__csEvent`????????????
