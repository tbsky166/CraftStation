/* CraftStation Web 界面：fz.wiki 风格 SPA + WPF 完整桥接 */

const NAV = [
  { key: "dashboard", label: "仪表盘", sub: "Home", icon: "◧" },
  { key: "versions", label: "版本库", sub: "Versions", icon: "▦" },
  { key: "instances", label: "实例", sub: "Instances", icon: "▤" },
  { key: "resources", label: "资源管理", sub: "Resources", icon: "▣" },
  { key: "store", label: "资源市场", sub: "Store", icon: "◫" },
  { key: "modhealth", label: "模组体检", sub: "Mod Health", icon: "◬" },
  { key: "servers", label: "服务器", sub: "Servers", icon: "◉" },
  { key: "accounts", label: "账户", sub: "Accounts", icon: "◍" },
  { key: "settings", label: "设置", sub: "Settings", icon: "⚙" },
];

const state = {
  page: "dashboard",
  accountName: "未登录",
  instanceName: "无实例",
  instanceVersion: "-",
  totalVersions: 0,
  installedVersions: 0,
  instanceCount: 0,
  gameRunning: false,
  statusText: "就绪",
  versions: [],
  instances: [],
  selVersion: null,
  selInstance: null,
  resources: { mods: [], resourcePacks: [], shaderPacks: [], saves: [] },
  resourceTab: "mods",
  projects: [],
  projectVersions: [],
  selProject: null,
  modhealth: { issues: [], mods: [] },
  depTree: [],
  servers: [],
  accounts: [],
  javas: [],
  updateInfo: null,
};

let seq = 0;
const pending = new Map();

function csCall(type, payload = {}) {
  return new Promise((resolve) => {
    const id = ++seq;
    pending.set(id, resolve);
    window.chrome.webview.postMessage({ id, type, payload });
  });
}

window.__csCallback = (id, ok, data) => {
  const resolve = pending.get(id);
  if (!resolve) return;
  pending.delete(id);
  resolve(ok ? data : { error: data });
};

window.__csEvent = (name, data) => {
  if (name === "deviceCode") showDeviceCode(data);
};

// 禁用浏览器右键菜单，防止出现 Chromium 默认菜单
document.addEventListener("contextmenu", (e) => e.preventDefault());

function toast(text, isError = false) {
  const el = document.getElementById("toast");
  el.classList.toggle("toast--error", !!isError);
  clearTimeout(el._t);
  if (isError) {
    el.innerHTML = '<span class="toast__msg"></span><button class="toast__close" aria-label="关闭" onclick="closeToast()">×</button>';
    el.querySelector(".toast__msg").textContent = text;
    el.hidden = false;
  } else {
    el.textContent = text;
    el.hidden = false;
    el._t = setTimeout(() => (el.hidden = true), 2600);
  }
}

function closeToast() {
  const el = document.getElementById("toast");
  clearTimeout(el._t);
  el.hidden = true;
}

function resultToast(r, fallback = "完成") {
  const msg = r?.error ?? r?.message;
  if (r?.error || /失败|错误|异常|无法|出错|超时|拒绝|未找到/.test(String(msg ?? ""))) {
    toast(String(msg ?? "操作失败"), true);
  } else {
    toast(msg ?? fallback);
  }
}

function busyBtn(btn, text) {
  if (!btn) return () => {};
  const old = btn.textContent;
  btn.disabled = true;
  btn.textContent = text;
  btn.classList.add("is-busy");
  return () => {
    btn.disabled = false;
    btn.textContent = old;
    btn.classList.remove("is-busy");
  };
}

function escapeHtml(s) {
  return String(s ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
  }[c]));
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(String(r.result).split(",")[1]);
    r.onerror = reject;
    r.readAsDataURL(file);
  });
}

function renderNav() {
  const nav = document.getElementById("nav");
  nav.innerHTML = NAV.map((n) => `
    <a class="sidebar-link ${state.page === n.key ? "sidebar-link--active" : ""}" data-key="${n.key}">
      <span class="sidebar-link__bar"></span>
      <span class="sidebar-link__icon">${n.icon}</span>
      <span>
        <span>${n.label}</span>
        <span class="sidebar-link__sub">${n.sub}</span>
      </span>
    </a>`).join("");
  nav.querySelectorAll(".sidebar-link").forEach((el) =>
    el.addEventListener("click", () => navigate(el.dataset.key)));
}

function navigate(key) {
  state.page = key;
  renderNav();
  document.getElementById("pageTitle").textContent =
    NAV.find((n) => n.key === key)?.label ?? "仪表盘";
  replaySignalStrip();
  flashPageTitle();
  renderPage();
}

async function renderPage() {
  const content = document.getElementById("content");
  switch (state.page) {
    case "dashboard": content.innerHTML = dashboardHtml(); bindDashboard(); break;
    case "versions": await refreshVersions(); content.innerHTML = versionsHtml(); bindVersions(); break;
    case "instances": await refreshInstances(); content.innerHTML = instancesHtml(); bindInstances(); break;
    case "resources": await refreshResources(); content.innerHTML = resourcesHtml(); bindResources(); break;
    case "store": content.innerHTML = storeHtml(); bindStore(); break;
    case "modhealth": content.innerHTML = modhealthHtml(); bindModhealth(); break;
    case "servers": await refreshServers(); content.innerHTML = serversHtml(); bindServers(); break;
    case "accounts": await refreshAccounts(); content.innerHTML = accountsHtml(); bindAccounts(); break;
    case "settings": content.innerHTML = settingsHtml(); bindSettings(); break;
  }
  staggerPage();
}

function staggerPage() {
  const content = document.getElementById("content");
  if (!content) return;
  // 终末地官网：Hero 内部元素交错入场
  const heroChildren = content.querySelectorAll(".hero-grid > div:first-child > *");
  heroChildren.forEach((el, i) => {
    el.classList.add("hero-stagger");
    el.style.setProperty("--i", i);
  });
  // fz.wiki + 终末地：滚动进场（进入视口才浮现）
  const reveal = content.querySelectorAll(
    ".industrial-card, .grid-4 > button, .list-item, .page > .form-row, .page > .grid-2"
  );
  reveal.forEach((el, i) => {
    if (i > 20) return;
    el.dataset.reveal = "";
    el.style.setProperty("--i", i);
  });
  if (revealObserver) {
    revealObserver.disconnect();
    revealObserver = null;
  }
  observeReveals();
}

let revealObserver = null;
function observeReveals() {
  const items = document.querySelectorAll("[data-reveal]:not(.revealed)");
  if (!items.length) return;
  if (!revealObserver) {
    revealObserver = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) {
          e.target.classList.add("revealed");
          revealObserver.unobserve(e.target);
        }
      });
    }, { threshold: 0.12 });
  }
  items.forEach((el) => revealObserver.observe(el));
}

function initParallax() {
  const content = document.getElementById("content");
  if (!content) return;
  content.addEventListener("scroll", () => {
    const y = Math.min(content.scrollTop / 240, 1);
    document.documentElement.style.setProperty("--parallax", y.toFixed(3));
  });
}

function flashPageTitle() {
  const title = document.getElementById("pageTitle");
  title.classList.remove("ef-flashing");
  void title.offsetWidth;
  title.classList.add("ef-flashing");
}

function replaySignalStrip() {
  const strip = document.querySelector(".top-signal-strip");
  if (!strip) return;
  strip.style.animation = "none";
  void strip.offsetWidth;
  strip.style.animation = "";
}

/* ---------------- 仪表盘 ---------------- */

function dashboardHtml() {
  return `
  <div class="page">
    <section class="hero ef-chamfer">
      <div class="hero-grid">
        <div>
          <div class="eyebrow">CORE CHAPTER / 核心章节</div>
          <h1 class="display-title" style="margin-top:10px">CRAFTSTATION</h1>
          <p style="color:var(--ink-muted);margin-top:8px;font-size:17px">我的世界 Java 启动器</p>
          <div class="hero-meta">
            <span>当前实例 <b id="heroInstance" style="color:var(--ink)">${escapeHtml(state.instanceName)}</b></span>
            <span>版本 <b id="heroVersion" style="color:var(--ink)">${escapeHtml(state.instanceVersion)}</b></span>
          </div>
          <div class="hero-actions" id="heroActions">
            ${state.gameRunning
              ? `<button class="btn btn-danger ef-chamfer-sm" id="stopBtn">停 止 游 戏</button>`
              : `<button class="btn btn-primary ef-chamfer-sm" id="launchBtn">启 动 游 戏</button>`}
          </div>
          <p id="heroStatus" style="color:var(--ink-muted);margin-top:16px">${escapeHtml(state.statusText)}</p>
        </div>
        <div class="hero-status ef-chamfer-sm">
          <div style="margin-bottom:10px"><span class="status-dot"></span><b style="color:var(--ink)">SYSTEM STATUS</b></div>
          <div class="status-row"><span>JAVA</span><b>AUTO</b></div>
          <div class="status-row"><span>MIRROR</span><b>BMCLAPI + OFFLINE</b></div>
          <div class="status-row"><span>ACCOUNT</span><b>${escapeHtml(state.accountName)}</b></div>
          <div class="status-row"><span>VERSION</span><b>${escapeHtml(state.instanceVersion)}</b></div>
          <div class="status-row"><span>PROCESS</span><b id="procState">${state.gameRunning ? "运行中" : "空闲"}</b></div>
        </div>
      </div>
    </section>

    <div class="grid-2">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">账户</div>
        <div class="stat-value" id="statAccount" style="margin-top:10px">${escapeHtml(state.accountName)}</div>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">版本统计</div>
        <div style="display:flex;gap:28px;margin-top:10px">
          <div><div class="stat-value" id="statTotal">${state.totalVersions}</div><div class="stat-label">可用版本</div></div>
          <div><div class="stat-value" id="statInstalled">${state.installedVersions}</div><div class="stat-label">已安装</div></div>
          <div><div class="stat-value" id="statInstances">${state.instanceCount}</div><div class="stat-label">实例数量</div></div>
        </div>
      </section>
    </div>

    <div class="eyebrow" style="margin:26px 0 12px">快速入口 / QUICK ACCESS</div>
    <div class="grid-4">
      ${quickCard("versions", "▦", "版本库", "安装 / 修复游戏版本", "var(--accent)")}
      ${quickCard("instances", "▤", "实例管理", "隔离 / 启动参数 / 导入导出", "var(--system)")}
      ${quickCard("store", "◫", "资源市场", "Modrinth 搜索与下载", "var(--warn)")}
      ${quickCard("modhealth", "◬", "模组体检", "依赖 / 冲突 / 损坏检测", "var(--danger)")}
    </div>
  </div>`;
}

function quickCard(key, icon, title, sub, color) {
  return `
    <button class="industrial-card ef-chamfer quick-card" data-nav="${key}">
      <span class="quick-icon" style="color:${color}">${icon}</span>
      <span style="font-weight:600">${title}</span>
      <span class="stat-label">${sub}</span>
    </button>`;
}

function bindDashboard() {
  const launch = document.getElementById("launchBtn");
  if (launch) launch.addEventListener("click", async () => {
    const restore = busyBtn(launch, "启动中…");
    toast("正在启动游戏…");
    const r = await csCall("launchInstance", {});
    restore();
    resultToast(r);
    await loadState();
  });
  const stop = document.getElementById("stopBtn");
  if (stop) stop.addEventListener("click", async () => {
    const restore = busyBtn(stop, "停止中…");
    toast("正在停止游戏…");
    const r = await csCall("stopGame");
    restore();
    resultToast(r);
    await loadState();
  });
  document.querySelectorAll("[data-nav]").forEach((el) =>
    el.addEventListener("click", () => navigate(el.dataset.nav)));
}

function updateDashboardLive(r) {
  const set = (id, text) => {
    const el = document.getElementById(id);
    if (el) el.textContent = text;
  };
  set("statAccount", r.accountName);
  set("statTotal", r.totalVersions);
  set("statInstalled", r.installedVersions);
  set("statInstances", r.instanceCount);
  set("heroInstance", r.instanceName);
  set("heroVersion", r.instanceVersion);
  set("heroStatus", r.statusText);
  set("procState", r.gameRunning ? "运行中" : "空闲");

  const actions = document.getElementById("heroActions");
  if (!actions) return;
  const wantsStop = !!r.gameRunning;
  const hasStop = !!document.getElementById("stopBtn");
  if (wantsStop === hasStop) return;
  actions.innerHTML = wantsStop
    ? `<button class="btn btn-danger ef-chamfer-sm" id="stopBtn">停 止 游 戏</button>`
    : `<button class="btn btn-primary ef-chamfer-sm" id="launchBtn">启 动 游 戏</button>`;
  const btn = document.getElementById(wantsStop ? "stopBtn" : "launchBtn");
  btn.addEventListener("click", async () => {
    const restore = busyBtn(btn, wantsStop ? "停止中…" : "启动中…");
    toast(wantsStop ? "正在停止游戏…" : "正在启动游戏…");
    const res = await csCall(wantsStop ? "stopGame" : "launchInstance", {});
    restore();
    resultToast(res);
    await loadState();
  });
}

/* ---------------- 版本库 ---------------- */

function versionsHtml() {
  const q = (state.selVersion ?? "").toLowerCase();
  const list = state.versions
    .filter((v) => !q || v.name.toLowerCase().includes(q))
    .slice(0, 80)
    .map((v) => `
    <div class="list-item" data-name="${escapeHtml(v.name)}">
      <div>
        <div class="item-title">${escapeHtml(v.name)}</div>
        <div class="item-sub">${escapeHtml(v.typeLabel)} · ${escapeHtml(v.releaseTimeUtc || "-")}</div>
      </div>
      <div style="display:flex;align-items:center;gap:8px">
        ${v.isInstalled ? '<span class="tag tag--success">已安装</span>' : ""}
        <button class="btn btn-outline ef-chamfer-sm" data-install="${escapeHtml(v.name)}">安装</button>
        ${v.isInstalled ? `<button class="btn ef-chamfer-sm" data-repair="${escapeHtml(v.name)}">修复</button>
                           <button class="btn btn-danger ef-chamfer-sm" data-delete="${escapeHtml(v.name)}">删除</button>` : ""}
      </div>
    </div>`).join("");
  return `
  <div class="page">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:16px">
      <h1 class="page-title">版本库</h1>
      <input class="wiki-input" id="versionSearch" placeholder="筛选版本…" value="${escapeHtml(state.selVersion || "")}" style="max-width:280px"/>
      <button class="btn ef-chamfer-sm" id="versionsRefresh">刷新</button>
    </div>
    <div class="list" style="margin-top:16px">${list || '<div class="placeholder ef-chamfer">暂无版本</div>'}</div>
    <section class="industrial-card ef-chamfer" style="margin-top:18px">
      <div class="eyebrow">加载器安装</div>
      <div class="form-row" style="margin-top:10px">
        <select class="wiki-input" id="loaderKind">
          ${["Fabric", "Forge", "Quilt", "NeoForge", "OptiFine", "LiteLoader"].map((l) => `<option>${l}</option>`).join("")}
        </select>
        <select class="wiki-input" id="loaderVersion"><option value="">获取可用版本…</option></select>
        <button class="btn ef-chamfer-sm" id="loadLoaderVersions">获取版本</button>
        <button class="btn btn-primary ef-chamfer-sm" id="installLoader">安装加载器</button>
      </div>
      <p class="stat-label" style="margin-top:8px">目标版本：${escapeHtml(state.selVersion || "未选择（请在列表中点击版本）")}</p>
    </section>
  </div>`;
}

function bindVersions() {
  document.getElementById("versionSearch").addEventListener("input", (e) => {
    state.selVersion = e.target.value;
    renderPage();
  });
  document.getElementById("versionsRefresh").addEventListener("click", async () => {
    await refreshVersions();
    renderPage();
  });
  document.querySelectorAll("[data-install]").forEach((el) =>
    el.addEventListener("click", async () => {
      const restore = busyBtn(el, "安装中…");
      toast("正在下载安装版本…");
      const r = await csCall("installVersion", { name: el.dataset.install });
      restore();
      resultToast(r);
      await refreshVersions();
      renderPage();
    }));
  document.querySelectorAll("[data-repair]").forEach((el) =>
    el.addEventListener("click", async () => {
      const restore = busyBtn(el, "修复中…");
      toast("正在修复版本…");
      const r = await csCall("repairVersion", { name: el.dataset.repair });
      restore();
      resultToast(r);
    }));
  document.querySelectorAll("[data-delete]").forEach((el) =>
    el.addEventListener("click", async () => {
      const restore = busyBtn(el, "删除中…");
      const r = await csCall("deleteVersion", { name: el.dataset.delete });
      restore();
      resultToast(r);
      await refreshVersions();
      renderPage();
    }));
  document.querySelectorAll(".list-item[data-name]").forEach((el) =>
    el.addEventListener("click", () => {
      state.selVersion = el.dataset.name;
      renderPage();
    }));
  document.getElementById("loadLoaderVersions").addEventListener("click", async () => {
    if (!state.selVersion) return toast("请先在列表中点击选择版本", true);
    const loader = document.getElementById("loaderKind").value;
    const r = await csCall("getLoaderVersions", { version: state.selVersion, loader });
    const sel = document.getElementById("loaderVersion");
    sel.innerHTML = (r?.versions ?? []).map((v) => `<option value="${escapeHtml(v)}">${escapeHtml(v)}</option>`).join("");
  });
  document.getElementById("installLoader").addEventListener("click", async () => {
    if (!state.selVersion) return toast("请先选择版本", true);
    const loader = document.getElementById("loaderKind").value;
    const loaderVersion = document.getElementById("loaderVersion").value || null;
    const btn = document.getElementById("installLoader");
    const restore = busyBtn(btn, "安装中…");
    toast("正在安装加载器…");
    const r = await csCall("installLoader", { version: state.selVersion, loader, loaderVersion });
    restore();
    resultToast(r);
  });
}

/* ---------------- 实例 ---------------- */

function instancesHtml() {
  const list = state.instances.map((i) => `
    <div class="list-item ${i.isCurrent ? "is-current" : ""}" data-instance="${i.id}">
      <div>
        <div class="item-title">${escapeHtml(i.name)}</div>
        <div class="item-sub">${escapeHtml(i.resolvedVersionName)}</div>
      </div>
      <span class="tag ${i.isCurrent ? "tag--feature" : "tag--neutral"}">${i.isCurrent ? "当前" : "切换"}</span>
    </div>`).join("");
  const i = state.instances.find((x) => x.id === state.selInstance) || state.instances.find((x) => x.isCurrent);
  return `
  <div class="page">
    <div class="grid-2">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">实例列表</div>
        <div class="form-row" style="margin-top:10px">
          <input class="wiki-input" id="newName" placeholder="名称"/>
          <input class="wiki-input" id="newVersion" placeholder="版本，如 1.21.1"/>
          <button class="btn btn-primary ef-chamfer-sm" id="createInstance">新建</button>
        </div>
        <div class="list" style="margin-top:12px">${list || '<div class="placeholder ef-chamfer">暂无实例</div>'}</div>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">实例设置</div>
        ${i ? instanceForm(i) : '<div class="placeholder ef-chamfer">选择实例后编辑</div>'}
      </section>
    </div>
    <div class="form-row" style="margin-top:14px">
      <input type="file" id="packFile" accept=".zip,.mrpack" style="display:none"/>
      <button class="btn ef-chamfer-sm" id="importPackBtn">导入整合包</button>
      <button class="btn ef-chamfer-sm" id="exportMrpack">导出 .mrpack</button>
      <button class="btn ef-chamfer-sm" id="exportZip">导出 .zip</button>
    </div>
  </div>`;
}

function instanceForm(i) {
  return `
  <div class="form-grid" data-id="${i.id}">
    <label>名称<input class="wiki-input" data-f="name" value="${escapeHtml(i.name)}"/></label>
    <label>描述<input class="wiki-input" data-f="description" value="${escapeHtml(i.description || "")}"/></label>
    <label>游戏版本<input class="wiki-input" data-f="versionId" value="${escapeHtml(i.versionId)}"/></label>
    <label>加载器
      <select class="wiki-input" data-f="loader">
        ${["Vanilla", "Fabric", "Forge", "Quilt", "NeoForge", "OptiFine", "LiteLoader"]
          .map((l) => `<option ${i.loader === l ? "selected" : ""}>${l}</option>`).join("")}
      </select>
    </label>
    <label>加载器版本<input class="wiki-input" data-f="loaderVersion" value="${escapeHtml(i.loaderVersion || "")}"/></label>
    <label>Java 路径<input class="wiki-input" data-f="javaPath" value="${escapeHtml(i.javaPath || "")}"/></label>
    <label>最小内存<input class="wiki-input" data-f="minMemoryMb" type="number" value="${i.minMemoryMb}"/></label>
    <label>最大内存<input class="wiki-input" data-f="maxMemoryMb" type="number" value="${i.maxMemoryMb}"/></label>
    <label>JVM 参数<input class="wiki-input" data-f="jvmArgs" value="${escapeHtml(i.jvmArgs)}"/></label>
    <label>游戏参数<input class="wiki-input" data-f="gameArgs" value="${escapeHtml(i.gameArgs)}"/></label>
    <label>窗口宽度<input class="wiki-input" data-f="windowWidth" type="number" value="${i.windowWidth}"/></label>
    <label>窗口高度<input class="wiki-input" data-f="windowHeight" type="number" value="${i.windowHeight}"/></label>
    <label class="check-line"><input type="checkbox" data-f="versionIsolation" ${i.versionIsolation ? "checked" : ""}/> 版本隔离</label>
    <label class="check-line"><input type="checkbox" data-f="fullscreen" ${i.fullscreen ? "checked" : ""}/> 全屏</label>
    <label class="check-line"><input type="checkbox" data-f="closeLauncherAfterLaunch" ${i.closeLauncherAfterLaunch ? "checked" : ""}/> 启动后关闭启动器</label>
  </div>
  <div class="form-row" style="margin-top:12px">
    <button class="btn btn-primary ef-chamfer-sm" id="saveInstance">保存</button>
    <button class="btn ef-chamfer-sm" id="launchInstance">启动</button>
    <button class="btn ef-chamfer-sm" id="openGameFolder">打开目录</button>
    <button class="btn btn-danger ef-chamfer-sm" id="deleteInstance">删除</button>
  </div>`;
}

function bindInstances() {
  const createBtn = document.getElementById("createInstance");
  createBtn.addEventListener("click", async () => {
    const restore = busyBtn(createBtn, "创建中…");
    toast("正在创建实例…");
    const r = await csCall("createInstance", {
      name: document.getElementById("newName").value,
      version: document.getElementById("newVersion").value,
    });
    restore();
    resultToast(r);
    await refreshInstances();
    renderPage();
  });
  document.querySelectorAll("[data-instance]").forEach((el) =>
    el.addEventListener("click", async () => {
      const id = el.dataset.instance;
      await csCall("selectInstance", { id });
      state.selInstance = id;
      await refreshInstances();
      renderPage();
    }));
  const save = document.getElementById("saveInstance");
  if (save) save.addEventListener("click", async () => {
    const root = save.closest(".form-grid");
    const id = root.dataset.id;
    const payload = { id };
    root.querySelectorAll("[data-f]").forEach((el) => {
      const key = el.dataset.f;
      payload[key] = el.type === "checkbox" ? el.checked : el.value;
    });
    const restore = busyBtn(save, "保存中…");
    toast("正在保存实例设置…");
    const r = await csCall("saveInstance", payload);
    restore();
    resultToast(r);
    await refreshInstances();
    renderPage();
  });
  const launch = document.getElementById("launchInstance");
  if (launch) launch.addEventListener("click", async () => {
    const restore = busyBtn(launch, "启动中…");
    toast("正在启动游戏…");
    const r = await csCall("launchInstance", { id: state.selInstance });
    restore();
    resultToast(r);
  });
  const folder = document.getElementById("openGameFolder");
  if (folder) folder.addEventListener("click", async () => {
    const r = await csCall("openGameFolder", { id: state.selInstance });
    resultToast(r);
  });
  const del = document.getElementById("deleteInstance");
  if (del) del.addEventListener("click", async () => {
    const restore = busyBtn(del, "删除中…");
    toast("正在删除实例…");
    const r = await csCall("deleteInstance", { id: state.selInstance });
    restore();
    resultToast(r);
    state.selInstance = null;
    await refreshInstances();
    renderPage();
  });
  document.getElementById("packFile").addEventListener("change", async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    toast("正在导入整合包…");
    const data = await readFileAsBase64(file);
    const r = await csCall("importPack", { fileName: file.name, data });
    resultToast(r);
    await refreshInstances();
    renderPage();
  });
  document.getElementById("importPackBtn").addEventListener("click", () =>
    document.getElementById("packFile").click());
  document.getElementById("exportMrpack").addEventListener("click", async () => {
    const btn = document.getElementById("exportMrpack");
    const restore = busyBtn(btn, "导出中…");
    toast("正在导出整合包…");
    const r = await csCall("exportPack", { id: state.selInstance, format: "mrpack" });
    restore();
    resultToast(r);
  });
  document.getElementById("exportZip").addEventListener("click", async () => {
    const btn = document.getElementById("exportZip");
    const restore = busyBtn(btn, "导出中…");
    toast("正在导出整合包…");
    const r = await csCall("exportPack", { id: state.selInstance, format: "zip" });
    restore();
    resultToast(r);
  });
}

/* ---------------- 资源管理 ---------------- */

function resourcesHtml() {
  const tabs = [
    ["mods", "模组", state.resources.mods],
    ["resourcePacks", "资源包", state.resources.resourcePacks],
    ["shaderPacks", "光影包", state.resources.shaderPacks],
    ["saves", "存档", state.resources.saves],
  ];
  const kindKey = state.resourceTab;
  const tab = tabs.find((t) => t[0] === kindKey);
  const items = (tab ? tab[2] : []).map((r) => `
    <div class="list-item">
      <div>
        <div class="item-title">${escapeHtml(r.displayName || r.fileName)}</div>
        <div class="item-sub">${escapeHtml(r.fileName)} · ${escapeHtml(r.sizeLabel || "")} ${r.version ? "· " + escapeHtml(r.version) : ""}</div>
      </div>
      <div style="display:flex;gap:6px;align-items:center">
        ${r.isDisabled ? '<span class="tag tag--warn">已禁用</span>' : ""}
        ${kindKey === "saves"
          ? `<button class="btn ef-chamfer-sm" data-save-folder="${escapeHtml(r.folderPath)}">打开目录</button>`
          : `<button class="btn ef-chamfer-sm" data-toggle="${escapeHtml(r.filePath)}">${r.isDisabled ? "启用" : "禁用"}</button>
             <button class="btn btn-danger ef-chamfer-sm" data-delete-res="${escapeHtml(r.filePath)}">删除</button>`}
      </div>
    </div>`).join("");
  return `
  <div class="page">
    <h1 class="page-title">资源管理</h1>
    <div class="tabs" style="margin-top:12px">
      ${tabs.map((t) => `<button class="tab ${state.resourceTab === t[0] ? "tab--active" : ""}" data-tab="${t[0]}">${t[1]} (${t[2].length})</button>`).join("")}
    </div>
    <div class="form-row" style="margin-top:12px">
      <input type="file" id="resourceFile" style="display:none" ${kindKey === "mods" ? 'accept=".jar"' : 'accept=".zip"'}/>
      ${kindKey !== "saves" ? `<button class="btn btn-primary ef-chamfer-sm" id="importResourceBtn">导入${tab ? tab[1] : ""}</button>` : ""}
      ${kindKey !== "saves" ? `<button class="btn ef-chamfer-sm" id="openResourceFolder">打开目录</button>` : ""}
      <button class="btn ef-chamfer-sm" id="refreshResources">刷新</button>
    </div>
    <div class="list" style="margin-top:14px">${items || '<div class="placeholder ef-chamfer">暂无内容</div>'}</div>
  </div>`;
}

function bindResources() {
  document.querySelectorAll("[data-tab]").forEach((el) =>
    el.addEventListener("click", () => {
      state.resourceTab = el.dataset.tab;
      renderPage();
    }));
  document.getElementById("resourceFile").addEventListener("change", async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    toast("正在导入…");
    const data = await readFileAsBase64(file);
    const kind = state.resourceTab === "resourcePacks" ? "resourcepack" : state.resourceTab === "shaderPacks" ? "shader" : "mod";
    const r = await csCall("importResource", { kind, fileName: file.name, data });
    resultToast(r);
    await refreshResources();
    renderPage();
  });
  const imp = document.getElementById("importResourceBtn");
  if (imp) imp.addEventListener("click", () => document.getElementById("resourceFile").click());
  const folder = document.getElementById("openResourceFolder");
  if (folder) folder.addEventListener("click", async () => {
    const kind = state.resourceTab === "resourcePacks" ? "resourcepack" : state.resourceTab === "shaderPacks" ? "shader" : "mod";
    const r = await csCall("openResourceFolder", { kind });
    resultToast(r);
  });
  document.getElementById("refreshResources").addEventListener("click", async () => {
    await refreshResources();
    renderPage();
  });
  document.querySelectorAll("[data-toggle]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("toggleResource", { filePath: el.dataset.toggle });
      resultToast(r);
      await refreshResources();
      renderPage();
    }));
  document.querySelectorAll("[data-delete-res]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("deleteResource", { filePath: el.dataset.deleteRes });
      resultToast(r);
      await refreshResources();
      renderPage();
    }));
  document.querySelectorAll("[data-save-folder]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("openSaveFolder", { folder: el.dataset.saveFolder });
      resultToast(r);
    }));
}

/* ---------------- 资源市场 ---------------- */

function storeHtml() {
  const projects = state.projects.map((p) => `
    <div class="list-item" data-project="${p.id}">
      <div>
        <div class="item-title">${escapeHtml(p.title)} <span class="tag tag--neutral">${escapeHtml(p.typeLabel)}</span></div>
        <div class="item-sub">${escapeHtml(p.description || "")}</div>
        <div class="item-sub">下载 ${p.downloads} · 收藏 ${p.followers}</div>
      </div>
      <button class="btn ef-chamfer-sm">版本</button>
    </div>`).join("");
  const versions = state.projectVersions.map((v) => `
    <div class="list-item">
      <div>
        <div class="item-title">${escapeHtml(v.name)}</div>
        <div class="item-sub">${escapeHtml(v.versionNumber)} · ${escapeHtml(v.datePublished)} · ${escapeHtml((v.gameVersions || []).slice(0, 3).join(", "))}</div>
      </div>
      <button class="btn btn-primary ef-chamfer-sm" data-download="${v.id}">下载</button>
    </div>`).join("");
  return `
  <div class="page">
    <h1 class="page-title">资源市场</h1>
    <div class="form-row" style="margin-top:12px">
      <input class="wiki-input" id="storeQuery" placeholder="搜索关键词"/>
      <select class="wiki-input" id="storeType">
        ${["mod", "resourcepack", "shader", "modpack"].map((t) => `<option value="${t}">${t}</option>`).join("")}
      </select>
      <input class="wiki-input" id="storeVersion" placeholder="游戏版本，如 1.21.1"/>
      <input class="wiki-input" id="storeLoader" placeholder="加载器，如 fabric"/>
      <button class="btn btn-primary ef-chamfer-sm" id="storeSearch">搜索</button>
    </div>
    <div class="grid-2" style="margin-top:14px">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">项目</div>
        <div class="list" style="margin-top:10px">${projects || '<div class="placeholder ef-chamfer">搜索后显示</div>'}</div>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">版本</div>
        <div class="list" style="margin-top:10px">${versions || '<div class="placeholder ef-chamfer">点击项目查看版本</div>'}</div>
      </section>
    </div>
  </div>`;
}

function bindStore() {
  document.getElementById("storeSearch").addEventListener("click", async () => {
    const r = await csCall("searchProjects", {
      query: document.getElementById("storeQuery").value,
      projectType: document.getElementById("storeType").value,
      gameVersion: document.getElementById("storeVersion").value,
      loader: document.getElementById("storeLoader").value,
    });
    state.projects = r?.projects ?? [];
    state.projectVersions = [];
    state.selProject = null;
    renderPage();
  });
  document.querySelectorAll("[data-project]").forEach((el) =>
    el.addEventListener("click", async () => {
      const id = el.dataset.project;
      state.selProject = id;
      const r = await csCall("getProjectVersions", {
        projectId: id,
        gameVersion: document.getElementById("storeVersion").value,
        loader: document.getElementById("storeLoader").value,
      });
      state.projectVersions = r?.versions ?? [];
      renderPage();
    }));
  document.querySelectorAll("[data-download]").forEach((el) =>
    el.addEventListener("click", async () => {
      const restore = busyBtn(el, "下载中…");
      toast("正在下载…");
      const r = await csCall("downloadProjectVersion", { projectId: state.selProject, versionId: el.dataset.download });
      restore();
      resultToast(r);
    }));
}

/* ---------------- 模组体检 ---------------- */

function modhealthHtml() {
  const issues = state.modhealth.issues.map((x) => `
    <div class="list-item">
      <div>
        <div class="item-title"><span class="tag tag--${x.severity === "Error" ? "danger" : x.severity === "Warning" ? "warn" : "neutral"}">${escapeHtml(x.severityLabel)}</span> ${escapeHtml(x.title)}</div>
        <div class="item-sub">${escapeHtml(x.detail)}</div>
        ${x.suggestion ? `<div class="item-sub">建议：${escapeHtml(x.suggestion)}</div>` : ""}
      </div>
    </div>`).join("");
  const mods = state.modhealth.mods.map((m) => `
    <div class="list-item">
      <div>
        <div class="item-title">${escapeHtml(m.display)}</div>
        <div class="item-sub">${escapeHtml(m.fileName)} · ${escapeHtml(m.version || "-")} ${m.isDisabled ? "· 已禁用" : ""}</div>
      </div>
      <div style="display:flex;gap:6px">
        <button class="btn ef-chamfer-sm" data-dep="${escapeHtml(m.modId || m.fileName)}">依赖树</button>
        <button class="btn ef-chamfer-sm" data-disable-mod="${escapeHtml(m.filePath)}">${m.isDisabled ? "启用" : "禁用"}</button>
        <button class="btn btn-danger ef-chamfer-sm" data-delete-mod="${escapeHtml(m.filePath)}">删除</button>
      </div>
    </div>`).join("");
  const tree = state.depTree.map((m) => `<div class="item-sub">↳ ${escapeHtml(m.display)} ${escapeHtml(m.version || "")}</div>`).join("");
  return `
  <div class="page">
    <div style="display:flex;align-items:center;justify-content:space-between">
      <h1 class="page-title">模组体检</h1>
      <div style="display:flex;gap:8px">
        <button class="btn btn-primary ef-chamfer-sm" id="scanMods">开始扫描</button>
        <button class="btn ef-chamfer-sm" id="exportModReport">导出报告</button>
      </div>
    </div>
    <div class="grid-2" style="margin-top:14px">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">问题（${state.modhealth.issues.length}）</div>
        <div class="list" style="margin-top:10px">${issues || '<div class="placeholder ef-chamfer">扫描后显示</div>'}</div>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">模组（${state.modhealth.mods.length}）</div>
        <div class="list" style="margin-top:10px">${mods || '<div class="placeholder ef-chamfer">扫描后显示</div>'}</div>
        <div style="margin-top:12px;border-top:1px solid var(--border);padding-top:8px">${tree ? `<div class="eyebrow">依赖树</div>${tree}` : ""}</div>
      </section>
    </div>
  </div>`;
}

function bindModhealth() {
  document.getElementById("scanMods").addEventListener("click", async () => {
    const btn = document.getElementById("scanMods");
    const restore = busyBtn(btn, "扫描中…");
    toast("正在扫描模组…");
    const r = await csCall("scanMods");
    restore();
    state.modhealth = { issues: r?.issues ?? [], mods: r?.mods ?? [] };
    state.depTree = [];
    renderPage();
  });
  document.getElementById("exportModReport").addEventListener("click", async () => {
    const btn = document.getElementById("exportModReport");
    const restore = busyBtn(btn, "导出中…");
    toast("正在导出报告…");
    const r = await csCall("exportModReport");
    restore();
    resultToast(r);
    if (r?.content) downloadText("modhealth-report.md", r.content);
  });
  document.querySelectorAll("[data-dep]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("getDependencyTree", { modId: el.dataset.dep });
      state.depTree = r?.mods ?? [];
      renderPage();
    }));
  document.querySelectorAll("[data-disable-mod]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("disableMod", { filePath: el.dataset.disableMod });
      resultToast(r);
      const s = await csCall("scanMods");
      state.modhealth = { issues: s?.issues ?? [], mods: s?.mods ?? [] };
      renderPage();
    }));
  document.querySelectorAll("[data-delete-mod]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("deleteMod", { filePath: el.dataset.deleteMod });
      resultToast(r);
      const s = await csCall("scanMods");
      state.modhealth = { issues: s?.issues ?? [], mods: s?.mods ?? [] };
      renderPage();
    }));
}

function downloadText(name, content) {
  const blob = new Blob([content], { type: "text/markdown" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = name; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 4000);
}

/* ---------------- 服务器 ---------------- */

function serversHtml() {
  const list = state.servers.map((s) => `
    <div class="list-item" data-server="${s.id}">
      <div>
        <div class="item-title">${escapeHtml(s.name)}</div>
        <div class="item-sub">${escapeHtml(s.address)}:${s.port} ${s.lastPingUtc ? "· 上次 " + escapeHtml(s.lastPingUtc) : ""}</div>
      </div>
      <div style="display:flex;gap:6px">
        <button class="btn ef-chamfer-sm" data-ping="${s.id}">Ping</button>
        <button class="btn btn-primary ef-chamfer-sm" data-launch-server="${s.id}">直连启动</button>
        <button class="btn btn-danger ef-chamfer-sm" data-delete-server="${s.id}">删除</button>
      </div>
    </div>`).join("");
  return `
  <div class="page">
    <h1 class="page-title">服务器</h1>
    <section class="industrial-card ef-chamfer" style="margin-top:12px">
      <div class="eyebrow">添加服务器</div>
      <div class="form-row" style="margin-top:10px">
        <input class="wiki-input" id="serverName" placeholder="名称"/>
        <input class="wiki-input" id="serverAddress" placeholder="地址，如 play.example.com"/>
        <input class="wiki-input" id="serverPort" placeholder="端口" value="25565" style="max-width:100px"/>
        <button class="btn btn-primary ef-chamfer-sm" id="addServer">添加</button>
      </div>
      <p class="stat-label" id="pingResult" style="margin-top:8px"></p>
    </section>
    <div class="list" style="margin-top:14px">${list || '<div class="placeholder ef-chamfer">暂无服务器</div>'}</div>
  </div>`;
}

function bindServers() {
  document.getElementById("addServer").addEventListener("click", async () => {
    const btn = document.getElementById("addServer");
    const restore = busyBtn(btn, "添加中…");
    toast("正在添加服务器…");
    const r = await csCall("addServer", {
      name: document.getElementById("serverName").value,
      address: document.getElementById("serverAddress").value,
      port: parseInt(document.getElementById("serverPort").value, 10) || 25565,
    });
    restore();
    resultToast(r);
    await refreshServers();
    renderPage();
  });
  document.querySelectorAll("[data-ping]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("pingServer", { id: el.dataset.ping });
      const box = document.getElementById("pingResult");
      box.textContent = r?.online
        ? `在线 · ${r.playersOnline}/${r.playersMax} 人 · ${r.latencyMs} ms · ${r.version || ""}`
        : `离线：${r?.error || "未知错误"}`;
    }));
  document.querySelectorAll("[data-launch-server]").forEach((el) =>
    el.addEventListener("click", async () => {
      const restore = busyBtn(el, "启动中…");
      toast("正在直连启动…");
      const r = await csCall("launchServer", { id: el.dataset.launchServer });
      restore();
      resultToast(r);
    }));
  document.querySelectorAll("[data-delete-server]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("deleteServer", { id: el.dataset.deleteServer });
      resultToast(r);
      await refreshServers();
      renderPage();
    }));
}

/* ---------------- 账户 ---------------- */

function accountsHtml() {
  const list = state.accounts.map((a) => `
    <div class="list-item">
      <div style="display:flex;align-items:center;gap:10px">
        ${a.skinUrl ? `<img src="${escapeHtml(a.skinUrl)}" style="width:32px;height:32px;border:1px solid var(--border)"/>` : `<span class="tag tag--neutral">无皮肤</span>`}
        <div>
          <div class="item-title">${escapeHtml(a.displayName)}</div>
          <div class="item-sub">${escapeHtml(a.kindLabel)}</div>
        </div>
      </div>
      <div style="display:flex;gap:6px">
        ${a.isCurrent ? '<span class="tag tag--feature">当前</span>' : `<button class="btn ef-chamfer-sm" data-select-account="${a.id}">设为当前</button>`}
        ${a.kindLabel.includes("微软") ? `<button class="btn ef-chamfer-sm" data-refresh-account="${a.id}">刷新</button>
                                           <button class="btn ef-chamfer-sm" data-skin="${a.id}">下载皮肤</button>` : ""}
        <button class="btn btn-danger ef-chamfer-sm" data-remove-account="${a.id}">移除</button>
      </div>
    </div>`).join("");
  return `
  <div class="page">
    <h1 class="page-title">账户</h1>
    <div class="grid-2" style="margin-top:12px">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">离线账户</div>
        <div class="form-row" style="margin-top:10px">
          <input class="wiki-input" id="offlineName" placeholder="用户名"/>
          <button class="btn btn-primary ef-chamfer-sm" id="addOffline">添加</button>
        </div>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">微软账户</div>
        <div class="form-row" style="margin-top:10px">
          <button class="btn ef-chamfer-sm" id="loginMs">内置浏览器登录</button>
          <button class="btn ef-chamfer-sm" id="loginDevice">设备码登录</button>
        </div>
      </section>
    </div>
    <div class="list" style="margin-top:14px">${list || '<div class="placeholder ef-chamfer">暂无账户</div>'}</div>
  </div>`;
}

function bindAccounts() {
  const addBtn = document.getElementById("addOffline");
  addBtn.addEventListener("click", async () => {
    const restore = busyBtn(addBtn, "添加中…");
    toast("正在添加离线账户…");
    const r = await csCall("addOfflineAccount", { name: document.getElementById("offlineName").value });
    restore();
    resultToast(r);
    await refreshAccounts();
    renderPage();
  });
  const loginMsBtn = document.getElementById("loginMs");
  loginMsBtn.addEventListener("click", async () => {
    const restore = busyBtn(loginMsBtn, "登录中…");
    toast("正在打开微软登录…");
    const r = await csCall("loginMicrosoft");
    restore();
    resultToast(r);
    await refreshAccounts();
    await loadState();
    renderPage();
  });
  const loginDeviceBtn = document.getElementById("loginDevice");
  loginDeviceBtn.addEventListener("click", () => {
    const restore = busyBtn(loginDeviceBtn, "登录中…");
    toast("正在获取设备码…");
    showDeviceCode({ userCode: "等待中…", verificationUri: "" }, true);
    csCall("loginDeviceCode").then(async (r) => {
      hideDeviceCode();
      restore();
      resultToast(r);
      await refreshAccounts();
      await loadState();
      renderPage();
    });
  });
  document.querySelectorAll("[data-select-account]").forEach((el) =>
    el.addEventListener("click", async () => {
      await csCall("selectAccount", { id: el.dataset.selectAccount });
      await refreshAccounts();
      await loadState();
      renderPage();
    }));
  document.querySelectorAll("[data-refresh-account]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("refreshAccount", { id: el.dataset.refreshAccount });
      resultToast(r);
      await refreshAccounts();
      renderPage();
    }));
  document.querySelectorAll("[data-skin]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("downloadSkin", { id: el.dataset.skin });
      resultToast(r);
    }));
  document.querySelectorAll("[data-remove-account]").forEach((el) =>
    el.addEventListener("click", async () => {
      const r = await csCall("removeAccount", { id: el.dataset.removeAccount });
      resultToast(r);
      await refreshAccounts();
      await loadState();
      renderPage();
    }));
}

function showDeviceCode(data, waiting = false) {
  let modal = document.getElementById("deviceModal");
  if (!modal) {
    modal = document.createElement("div");
    modal.id = "deviceModal";
    modal.className = "modal";
    modal.innerHTML = `
      <div class="modal-card ef-chamfer">
        <div class="eyebrow">设备码登录</div>
        <h2 id="dcCode" style="font-family:var(--font-mono);letter-spacing:.2em;margin:12px 0">${escapeHtml(data.userCode || "")}</h2>
        <p class="stat-label">在浏览器打开：<a id="dcUri" href="#" target="_blank">${escapeHtml(data.verificationUri || "")}</a></p>
        <p class="stat-label" id="dcExpires"></p>
        <button class="btn ef-chamfer-sm" id="dcClose" style="margin-top:12px">关闭</button>
      </div>`;
    document.body.appendChild(modal);
    modal.querySelector("#dcClose").addEventListener("click", hideDeviceCode);
  }
  modal.hidden = false;
  document.getElementById("dcCode").textContent = data.userCode || "";
  const uri = document.getElementById("dcUri");
  const verificationUrl = data.verificationUrl || data.verificationUri || "";
  uri.textContent = verificationUrl;
  uri.href = verificationUrl || "#";
  document.getElementById("dcExpires").textContent = data.expiresOn ? `有效期至 ${data.expiresOn}` : (waiting ? "等待微软返回验证码…" : "");
}

function hideDeviceCode() {
  const modal = document.getElementById("deviceModal");
  if (modal) modal.hidden = true;
}

/* ---------------- 设置 ---------------- */

function settingsHtml() {
  return `
  <div class="page">
    <h1 class="page-title">设置</h1>
    <div class="grid-2" style="margin-top:12px">
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">常规</div>
        <div class="form-grid" style="margin-top:10px">
          <label>游戏目录<input class="wiki-input" id="setGameDir"/></label>
          <label>下载源
            <select class="wiki-input" id="setSource">
              ${["Bmclapi", "Mojang", "Custom"].map((s) => `<option>${s}</option>`).join("")}
            </select>
          </label>
          <label>自定义下载源<input class="wiki-input" id="setCustomSource"/></label>
          <label class="check-line"><input type="checkbox" id="setFallback"/> 镜像失败时回退官方源</label>
          <label>代理<input class="wiki-input" id="setProxy" placeholder="http://127.0.0.1:7890"/></label>
          <label>下载并发数<input class="wiki-input" id="setConcurrency" type="number"/></label>
          <label>更新源<input class="wiki-input" id="setUpdateEndpoint" placeholder="GitHub 仓库地址"/></label>
          <label>CurseForge API Key<input class="wiki-input" id="setCurseKey"/></label>
        </div>
        <div class="form-row" style="margin-top:12px">
          <button class="btn btn-primary ef-chamfer-sm" id="saveSettings">保存设置</button>
          <button class="btn ef-chamfer-sm" id="openDataFolder">打开数据目录</button>
          <button class="btn ef-chamfer-sm" id="openLogsFolder">打开日志目录</button>
          <button class="btn ef-chamfer-sm" id="checkUpdate">检查更新</button>
          <button class="btn ef-chamfer-sm" id="scanJava">扫描 Java</button>
        </div>
        <p class="stat-label" id="settingsStatus" style="margin-top:8px"></p>
      </section>
      <section class="industrial-card ef-chamfer">
        <div class="eyebrow">Java 环境</div>
        <div class="list" style="margin-top:10px" id="javaList">
          ${state.javas.length ? state.javas.map((j) => `<div class="item-title">${escapeHtml(j.version)} · ${escapeHtml(j.vendor)}</div><div class="item-sub">${escapeHtml(j.path)}</div>`).join("") : '<div class="placeholder ef-chamfer">点击扫描 Java</div>'}
        </div>
        <div class="eyebrow" style="margin-top:16px">更新信息</div>
        <p class="stat-label" id="updateInfo" style="margin-top:6px">${state.updateInfo ? escapeHtml(state.updateInfo.message) : "未检查"}</p>
      </section>
    </div>
  </div>`;
}

function bindSettings() {
  const load = async () => {
    const r = await csCall("getSettings");
    if (!r) return;
    document.getElementById("setGameDir").value = r.gameDirectory || "";
    document.getElementById("setSource").value = r.downloadSource || "Bmclapi";
    document.getElementById("setCustomSource").value = r.customDownloadSource || "";
    document.getElementById("setFallback").checked = !!r.fallbackToOfficial;
    document.getElementById("setProxy").value = r.proxy || "";
    document.getElementById("setConcurrency").value = r.maxConcurrency || 8;
    document.getElementById("setUpdateEndpoint").value = r.updateEndpoint || "";
    document.getElementById("setCurseKey").value = r.curseForgeApiKey || "";
  };
  load();
  const saveBtn = document.getElementById("saveSettings");
  saveBtn.addEventListener("click", async () => {
    const restore = busyBtn(saveBtn, "保存中…");
    toast("正在保存设置…");
    const r = await csCall("saveSettings", {
      gameDirectory: document.getElementById("setGameDir").value,
      downloadSource: document.getElementById("setSource").value,
      customDownloadSource: document.getElementById("setCustomSource").value,
      fallbackToOfficial: document.getElementById("setFallback").checked,
      proxy: document.getElementById("setProxy").value,
      maxConcurrency: parseInt(document.getElementById("setConcurrency").value, 10) || 8,
      updateEndpoint: document.getElementById("setUpdateEndpoint").value,
      curseForgeApiKey: document.getElementById("setCurseKey").value,
    });
    restore();
    document.getElementById("settingsStatus").textContent = r?.error ?? r?.message ?? "完成";
    resultToast(r);
  });
  document.getElementById("openDataFolder").addEventListener("click", async () => {
    const r = await csCall("openDataFolder");
    resultToast(r);
  });
  document.getElementById("openLogsFolder").addEventListener("click", async () => {
    const r = await csCall("openLogsFolder");
    resultToast(r);
  });
  document.getElementById("checkUpdate").addEventListener("click", async () => {
    const btn = document.getElementById("checkUpdate");
    const restore = busyBtn(btn, "检查中…");
    toast("正在检查更新…");
    const r = await csCall("checkUpdate");
    restore();
    state.updateInfo = r;
    document.getElementById("updateInfo").textContent = r?.error ?? r?.message ?? "检查完成";
    resultToast(r);
  });
  document.getElementById("scanJava").addEventListener("click", async () => {
    const btn = document.getElementById("scanJava");
    const restore = busyBtn(btn, "扫描中…");
    toast("正在扫描 Java…");
    const r = await csCall("scanJava");
    restore();
    state.javas = r?.javas ?? [];
    renderPage();
  });
}

/* ---------------- 数据刷新 ---------------- */

async function refreshVersions() {
  const r = await csCall("getVersions");
  state.versions = r?.versions ?? [];
}

async function refreshInstances() {
  const r = await csCall("getInstances");
  state.instances = r?.instances ?? [];
  if (state.selInstance && !state.instances.find((i) => i.id === state.selInstance)) state.selInstance = null;
}

async function refreshResources() {
  const r = await csCall("getResources");
  state.resources = {
    mods: r?.mods ?? [],
    resourcePacks: r?.resourcePacks ?? [],
    shaderPacks: r?.shaderPacks ?? [],
    saves: r?.saves ?? [],
  };
}

async function refreshServers() {
  const r = await csCall("getServers");
  state.servers = r?.servers ?? [];
}

async function refreshAccounts() {
  const r = await csCall("getAccounts");
  state.accounts = r?.accounts ?? [];
}

async function loadState() {
  const r = await csCall("getState");
  if (!r) return;
  state.accountName = r.accountName;
  state.instanceName = r.instanceName;
  state.instanceVersion = r.instanceVersion;
  state.totalVersions = r.totalVersions;
  state.installedVersions = r.installedVersions;
  state.instanceCount = r.instanceCount;
  state.gameRunning = !!r.gameRunning;
  state.statusText = r.statusText;
  document.getElementById("instanceChip").textContent = state.instanceName;
  document.getElementById("accountChip").textContent = state.accountName;
  if (state.page === "dashboard") renderPage();
}

document.getElementById("searchInput").addEventListener("keydown", (e) => {
  if (e.key === "Enter" && e.target.value.trim()) {
    state.selVersion = e.target.value.trim();
    navigate("versions");
  }
});

/* ---------------- 无边框窗口：HTML 顶栏拖动 + 窗口控制 ---------------- */

const titlebar = document.querySelector(".header");
const dragIgnored = (t) => t.closest("input, button, select, textarea, a, .chip, .search-kbd, .window-controls");
let dragState = null;

titlebar.addEventListener("pointerdown", (e) => {
  if (e.button !== 0 || dragIgnored(e.target)) return;
  dragState = { x: e.screenX, y: e.screenY, moved: false };
  try { titlebar.setPointerCapture(e.pointerId); } catch { /* 忽略捕获失败 */ }
});

titlebar.addEventListener("pointermove", (e) => {
  if (!dragState || dragState.moved) return;
  if (Math.hypot(e.screenX - dragState.x, e.screenY - dragState.y) > 4) {
    dragState.moved = true;
    csCall("windowDrag");
  }
});

const endDrag = () => { dragState = null; };
titlebar.addEventListener("pointerup", endDrag);
titlebar.addEventListener("pointercancel", endDrag);

titlebar.addEventListener("dblclick", (e) => {
  if (dragIgnored(e.target)) return;
  csCall("windowToggleMaximize");
});

document.getElementById("winMin").addEventListener("click", () => csCall("windowMinimize"));
document.getElementById("winMax").addEventListener("click", () => csCall("windowToggleMaximize"));
document.getElementById("winClose").addEventListener("click", () => csCall("windowClose"));

renderNav();
initParallax();
loadState();

// 自动监测：每 3 秒拉取一次状态，仪表盘原地更新，不整页重渲染
setInterval(async () => {
  const r = await csCall("getState");
  if (!r) return;
  state.accountName = r.accountName;
  state.instanceName = r.instanceName;
  state.instanceVersion = r.instanceVersion;
  state.totalVersions = r.totalVersions;
  state.installedVersions = r.installedVersions;
  state.instanceCount = r.instanceCount;
  state.gameRunning = !!r.gameRunning;
  state.statusText = r.statusText;
  document.getElementById("instanceChip").textContent = state.instanceName;
  document.getElementById("accountChip").textContent = state.accountName;
  if (state.page === "dashboard") updateDashboardLive(r);
}, 3000);
