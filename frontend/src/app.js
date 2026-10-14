// Điều khiển giao diện mẫu: mỗi tab gọi đúng endpoint backend và hiển thị kết quả.
import {
  api,
  describeError,
  getAccessToken,
  getBaseUrl,
  onLog,
  refreshSession,
  setAccessToken,
  setBaseUrl,
} from "./api.js";

const baseUrlKey = "legacyvault-demo-base-url";

// Dữ liệu đang hiển thị, dùng lại cho các ô chọn ở tab Gói.
const data = {
  user: null,
  vault: null,
  assets: [],
  beneficiaries: [],
  containers: [],
  checkInEtag: null,
};

// ---------- Tiện ích hiển thị ----------

function byId(id) {
  return document.getElementById(id);
}

function on(id, eventName, handler) {
  byId(id).addEventListener(eventName, handler);
}

function readForm(form) {
  const values = {};
  for (const element of form.elements) {
    if (!element.name) {
      continue;
    }
    if (element.type === "checkbox") {
      values[element.name] = element.checked;
    } else if (element.type === "file") {
      values[element.name] = element.files.length > 0 ? element.files[0] : null;
    } else {
      values[element.name] = element.value.trim();
    }
  }
  return values;
}

// Chuỗi rỗng gửi lên thành null để backend hiểu là "không có".
function emptyToNull(value) {
  if (value === "") {
    return null;
  }
  return value;
}

let toastTimer = null;

function toast(message, isError) {
  const box = byId("toast");
  box.textContent = message;
  box.className = isError ? "toast error" : "toast ok";
  clearTimeout(toastTimer);
  toastTimer = setTimeout(function () {
    box.className = "toast hidden";
  }, 4000);
}

// Báo kết quả một lần gọi API; trả true khi thành công.
function report(result, successMessage) {
  if (result.ok) {
    toast(successMessage, false);
    return true;
  }
  toast(describeError(result), true);
  return false;
}

function formatDate(value) {
  if (!value) {
    return "—";
  }
  return new Date(value).toLocaleString("vi-VN");
}

function formatBytes(value) {
  if (value < 1024) {
    return value + " B";
  }
  if (value < 1024 * 1024) {
    return (value / 1024).toFixed(1) + " KiB";
  }
  return (value / 1024 / 1024).toFixed(2) + " MiB";
}

function shortId(id) {
  return id.substring(0, 8);
}

// Tạo bảng từ danh sách cột; dùng textContent để không chèn HTML lạ từ dữ liệu.
function renderTable(container, columns, rows) {
  container.innerHTML = "";
  container.classList.remove("muted");
  if (rows.length === 0) {
    container.textContent = "Chưa có dữ liệu.";
    container.classList.add("muted");
    return;
  }
  const table = document.createElement("table");
  const headRow = document.createElement("tr");
  for (const column of columns) {
    const th = document.createElement("th");
    th.textContent = column.title;
    headRow.appendChild(th);
  }
  table.appendChild(headRow);

  for (const row of rows) {
    const tr = document.createElement("tr");
    for (const column of columns) {
      const td = document.createElement("td");
      const value = column.value(row);
      if (value instanceof Node) {
        td.appendChild(value);
      } else {
        td.textContent = value;
      }
      tr.appendChild(td);
    }
    table.appendChild(tr);
  }
  container.appendChild(table);
}

function renderFacts(container, facts) {
  container.innerHTML = "";
  container.classList.remove("muted");
  const list = document.createElement("dl");
  list.className = "facts";
  for (const fact of facts) {
    const term = document.createElement("dt");
    term.textContent = fact[0];
    const detail = document.createElement("dd");
    detail.textContent = fact[1];
    list.appendChild(term);
    list.appendChild(detail);
  }
  container.appendChild(list);
}

function badge(text) {
  const span = document.createElement("span");
  span.className = "badge";
  span.textContent = text;
  return span;
}

function smallButton(text, handler) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "small ghost";
  button.textContent = text;
  button.addEventListener("click", handler);
  return button;
}

function fillSelect(select, items, labelOf) {
  select.innerHTML = "";
  if (items.length === 0) {
    const option = document.createElement("option");
    option.textContent = "(chưa có)";
    option.value = "";
    select.appendChild(option);
    return;
  }
  for (const item of items) {
    const option = document.createElement("option");
    option.value = item.id;
    option.textContent = labelOf(item);
    select.appendChild(option);
  }
}

function requireVault() {
  if (data.vault === null) {
    toast("Hãy tạo hoặc tải kho ở tab Kho trước.", true);
    return false;
  }
  return true;
}

function vaultPath(suffix) {
  return "/vaults/" + data.vault.id + suffix;
}

// ---------- Tab và phiên ----------

function showTab(name) {
  const buttons = byId("tabs").querySelectorAll("button");
  for (const button of buttons) {
    button.classList.toggle("active", button.dataset.tab === name);
  }
  const tabs = document.querySelectorAll(".tab");
  for (const tab of tabs) {
    tab.classList.toggle("hidden", tab.id !== "tab-" + name);
  }
}

function renderSession() {
  const box = byId("session-box");
  if (data.user === null) {
    box.textContent = getAccessToken() ? "Đã có phiên (chưa tải thông tin)" : "Chưa đăng nhập";
    return;
  }
  let text = data.user.fullName + " · " + data.user.email + " · " + data.user.role;
  if (!data.user.emailVerified) {
    text = text + " · chưa xác minh email";
  }
  box.textContent = text;
}

function applySession(result) {
  if (result.ok && result.data) {
    setAccessToken(result.data.accessToken);
    data.user = result.data.user;
  }
  renderSession();
}

// ---------- Tài khoản ----------

async function register(event) {
  event.preventDefault();
  const values = readForm(event.target);
  if (values.password !== values.confirmPassword) {
    toast("Hai mật khẩu không khớp.", true);
    return;
  }
  const result = await api("POST", "/auth/register", { body: values });
  report(result, "Đã gửi yêu cầu đăng ký. Mở link xác minh trong log backend.");
}

async function verifyEmail(token) {
  const result = await api("POST", "/auth/verify-email", { body: { token: token } });
  if (report(result, "Đã xác minh email.")) {
    if (data.user !== null) {
      data.user = result.data;
      renderSession();
    }
  }
}

async function submitVerify(event) {
  event.preventDefault();
  await verifyEmail(readForm(event.target).token);
}

async function resend() {
  const email = byId("form-verify").elements.email.value.trim();
  const result = await api("POST", "/auth/resend-verification", { body: { email: email } });
  report(result, "Nếu email hợp lệ, link xác minh mới đã được gửi.");
}

async function login(event) {
  event.preventDefault();
  const values = readForm(event.target);
  const result = await api("POST", "/auth/login", {
    body: { email: values.email, password: values.password, rememberMe: values.rememberMe },
    csrf: true,
    useToken: false,
  });
  applySession(result);
  if (report(result, "Đăng nhập thành công.")) {
    await loadVault(true);
  }
}

async function loadSession() {
  const result = await api("GET", "/auth/session");
  if (result.ok) {
    data.user = result.data;
    renderSession();
  }
  byId("out-session").textContent = JSON.stringify(result.data, null, 2);
  if (!result.ok) {
    toast(describeError(result), true);
  }
}

async function refresh() {
  const result = await refreshSession();
  applySession(result);
  report(result, "Đã làm mới phiên.");
}

async function logout() {
  const result = await api("POST", "/auth/logout", {
    body: { revokeAllDevices: byId("logout-all").checked },
    csrf: true,
    useToken: false,
  });
  setAccessToken(null);
  data.user = null;
  data.vault = null;
  renderSession();
  report(result, "Đã đăng xuất.");
}

// ---------- Kho ----------

function renderVault() {
  const vault = data.vault;
  renderFacts(byId("vault-view"), [
    ["Tên", vault.name],
    ["Mô tả", vault.description || "—"],
    ["Trạng thái kho", vault.status],
    ["Kế hoạch", vault.planStatus + " (cấu hình v" + vault.planConfigVersion + ")"],
    ["Hạng dịch vụ", vault.serviceTier + " · " + vault.serviceStatus],
    ["Dung lượng", formatBytes(vault.storageUsedBytes) + " / " + formatBytes(vault.storageQuotaBytes)],
    ["Suất người nhận", vault.usedBeneficiarySlots + " / " + vault.beneficiarySlots],
    ["Số gói đang dùng", String(vault.activeContainerCount)],
    ["Tạo lúc", formatDate(vault.createdAt)],
    ["Mã kho", vault.id],
  ]);
}

async function loadVault(silent) {
  const result = await api("GET", "/vaults/me");
  if (result.ok) {
    data.vault = result.data;
    renderVault();
  } else if (!silent) {
    toast(describeError(result), true);
  }
}

async function createVault(event) {
  event.preventDefault();
  const values = readForm(event.target);
  const result = await api("POST", "/vaults", {
    body: { name: emptyToNull(values.name), description: emptyToNull(values.description) },
  });
  if (report(result, "Đã tạo hoặc mở kho.")) {
    data.vault = result.data;
    renderVault();
  }
}

async function updateVault(event) {
  event.preventDefault();
  if (!requireVault()) {
    return;
  }
  const values = readForm(event.target);
  const result = await api("PUT", vaultPath(""), {
    body: { etag: data.vault.etag, name: emptyToNull(values.name), description: emptyToNull(values.description) },
  });
  if (report(result, "Đã lưu kho.")) {
    data.vault = result.data;
    renderVault();
  }
}

// Chỉ dùng khi demo: nhờ máy chủ giao diện mẫu sửa hạng dịch vụ trong database (backend chưa có thanh toán).
async function upgradeTier() {
  if (data.user === null) {
    toast("Hãy đăng nhập trước.", true);
    return;
  }
  if (!requireVault()) {
    return;
  }
  const tier = byId("sel-tier").value;
  const response = await fetch("/demo/upgrade-tier", {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Demo": "1" },
    body: JSON.stringify({ email: data.user.email, tier: tier }),
  });
  let body = null;
  try {
    body = await response.json();
  } catch {
    body = { message: "Máy chủ giao diện mẫu không trả kết quả." };
  }
  toast(body.message, !response.ok);
  if (response.ok) {
    await loadVault(false);
  }
}

// ---------- Tài sản ----------

async function loadAssets() {
  if (!requireVault()) {
    return;
  }
  const result = await api("GET", vaultPath("/assets?pageSize=100"));
  if (!result.ok) {
    toast(describeError(result), true);
    return;
  }
  data.assets = result.data.items;
  renderTable(byId("assets-view"), [
    { title: "Tên", value: function (a) { return a.name; } },
    { title: "Loại", value: function (a) { return badge(a.kind); } },
    { title: "Phiên bản", value: function (a) { return "v" + a.currentVersion.versionNumber; } },
    { title: "Kích thước", value: function (a) { return formatBytes(a.currentVersion.sizeBytes); } },
    { title: "Tạo lúc", value: function (a) { return formatDate(a.createdAt); } },
    { title: "Mã", value: function (a) { return shortId(a.id); } },
  ], data.assets);
  fillAssetSelect();
}

async function uploadFile(event) {
  event.preventDefault();
  if (!requireVault()) {
    return;
  }
  const values = readForm(event.target);
  const form = new FormData();
  form.append("file", values.file);
  if (values.name !== "") {
    form.append("name", values.name);
  }
  if (values.description !== "") {
    form.append("description", values.description);
  }
  const result = await api("POST", vaultPath("/assets"), { form: form });
  if (report(result, "Đã tải tệp lên.")) {
    event.target.reset();
    await loadAssets();
  }
}

async function createTextAsset(event) {
  event.preventDefault();
  if (!requireVault()) {
    return;
  }
  const values = readForm(event.target);
  const result = await api("POST", vaultPath("/assets/text"), {
    body: { name: values.name, description: emptyToNull(values.description), content: values.content },
  });
  if (report(result, "Đã lưu ghi chú.")) {
    event.target.reset();
    await loadAssets();
  }
}

// ---------- Người nhận ----------

async function loadBeneficiaries() {
  if (!requireVault()) {
    return;
  }
  const result = await api("GET", vaultPath("/beneficiaries?pageSize=100"));
  if (!result.ok) {
    toast(describeError(result), true);
    return;
  }
  data.beneficiaries = result.data.items;
  renderTable(byId("beneficiaries-view"), [
    { title: "Họ tên", value: function (b) { return b.fullName; } },
    { title: "CCCD", value: function (b) { return "•••• " + b.citizenIdLast4; } },
    { title: "Ngày sinh", value: function (b) { return b.dateOfBirth; } },
    { title: "Liên hệ", value: function (b) { return b.email || b.phone || "—"; } },
    { title: "Số gói", value: function (b) { return String(b.containerIds.length); } },
    { title: "", value: function (b) { return smallButton("Xóa", function () { deleteBeneficiary(b); }); } },
  ], data.beneficiaries);
  fillBeneficiarySelect();
}

async function createBeneficiary(event) {
  event.preventDefault();
  if (!requireVault()) {
    return;
  }
  const values = readForm(event.target);
  const result = await api("POST", vaultPath("/beneficiaries"), {
    body: {
      citizenId: values.citizenId,
      fullName: values.fullName,
      dateOfBirth: values.dateOfBirth,
      email: emptyToNull(values.email),
      phone: emptyToNull(values.phone),
      confirmMinor: values.confirmMinor,
    },
  });
  if (report(result, "Đã thêm người nhận.")) {
    event.target.reset();
    await loadBeneficiaries();
  }
}

async function deleteBeneficiary(beneficiary) {
  if (!confirm("Xóa người nhận " + beneficiary.fullName + "?")) {
    return;
  }
  const result = await api("DELETE", vaultPath("/beneficiaries/" + beneficiary.id));
  if (report(result, "Đã xóa người nhận.")) {
    await loadBeneficiaries();
  }
}

// ---------- Gói bàn giao ----------

function fillAssetSelect() {
  fillSelect(byId("sel-asset"), data.assets, function (a) { return a.name; });
}

function fillBeneficiarySelect() {
  fillSelect(byId("sel-beneficiary"), data.beneficiaries, function (b) { return b.fullName + " (•••• " + b.citizenIdLast4 + ")"; });
}

function nameOfAsset(id) {
  for (const asset of data.assets) {
    if (asset.id === id) {
      return asset.name;
    }
  }
  return shortId(id);
}

function nameOfBeneficiary(id) {
  for (const beneficiary of data.beneficiaries) {
    if (beneficiary.id === id) {
      return beneficiary.fullName;
    }
  }
  return shortId(id);
}

function confirmQuery() {
  return "?confirmPlanDeactivation=" + byId("confirm-plan").checked;
}

function containerMembers(container) {
  const box = document.createElement("div");
  for (const assetId of container.assetIds) {
    box.appendChild(memberLine("Tài sản: " + nameOfAsset(assetId), function () {
      removeFromContainer(container, "/assets/" + assetId);
    }));
  }
  for (const beneficiaryId of container.beneficiaryIds) {
    box.appendChild(memberLine("Người nhận: " + nameOfBeneficiary(beneficiaryId), function () {
      removeFromContainer(container, "/beneficiaries/" + beneficiaryId);
    }));
  }
  if (container.missingRequirements.length > 0) {
    const missing = document.createElement("div");
    missing.className = "hint";
    missing.textContent = "Còn thiếu: " + container.missingRequirements.join(", ");
    box.appendChild(missing);
  }
  return box;
}

function memberLine(text, removeHandler) {
  const line = document.createElement("div");
  line.className = "row";
  line.appendChild(document.createTextNode(text));
  line.appendChild(smallButton("Gỡ", removeHandler));
  return line;
}

async function loadContainers() {
  if (!requireVault()) {
    return;
  }
  const archived = byId("include-archived").checked;
  const result = await api("GET", vaultPath("/containers?pageSize=100&includeArchived=" + archived));
  if (!result.ok) {
    toast(describeError(result), true);
    return;
  }
  data.containers = result.data.items;
  renderTable(byId("containers-view"), [
    { title: "Tên gói", value: function (c) { return c.name; } },
    { title: "Trạng thái", value: function (c) { return badge(c.status); } },
    { title: "Thành phần", value: containerMembers },
    { title: "", value: function (c) {
      if (c.status === "ARCHIVED") {
        return "";
      }
      return smallButton("Lưu trữ", function () { archiveContainer(c); });
    } },
  ], data.containers);

  const active = [];
  for (const container of data.containers) {
    if (container.status !== "ARCHIVED") {
      active.push(container);
    }
  }
  fillSelect(byId("sel-container"), active, function (c) { return c.name + " (" + c.status + ")"; });
}

async function createContainer(event) {
  event.preventDefault();
  if (!requireVault()) {
    return;
  }
  const values = readForm(event.target);
  const result = await api("POST", vaultPath("/containers"), {
    body: { name: values.name, description: emptyToNull(values.description) },
  });
  if (report(result, "Đã tạo gói.")) {
    event.target.reset();
    await loadContainers();
  }
}

async function addToContainer(kind) {
  if (!requireVault()) {
    return;
  }
  const containerId = byId("sel-container").value;
  if (containerId === "") {
    toast("Chưa có gói để chọn.", true);
    return;
  }
  let body = null;
  if (kind === "assets") {
    body = { assetId: byId("sel-asset").value };
  } else {
    body = { beneficiaryId: byId("sel-beneficiary").value };
  }
  const result = await api("POST", vaultPath("/containers/" + containerId + "/" + kind), { body: body });
  if (report(result, "Đã thêm vào gói.")) {
    await loadContainers();
  }
}

async function removeFromContainer(container, suffix) {
  const result = await api("DELETE", vaultPath("/containers/" + container.id + suffix + confirmQuery()));
  if (report(result, "Đã gỡ khỏi gói.")) {
    await loadContainers();
  }
}

async function archiveContainer(container) {
  if (!confirm("Lưu trữ gói " + container.name + "? Gói sẽ bị gỡ hết tài sản và người nhận.")) {
    return;
  }
  const result = await api("DELETE", vaultPath("/containers/" + container.id + confirmQuery()));
  if (report(result, "Đã lưu trữ gói.")) {
    await loadContainers();
  }
}

// ---------- Điểm danh ----------

function renderCheckIn(status) {
  data.checkInEtag = status.settings.etag;
  renderFacts(byId("checkin-view"), [
    ["Trạng thái", status.status],
    ["Kế hoạch", status.planStatus],
    ["Lần điểm danh gần nhất", formatDate(status.lastCheckInAt)],
    ["Hạn điểm danh kế tiếp", formatDate(status.nextCheckInDueAt)],
    ["Hết thời gian chờ", formatDate(status.graceEndsAt)],
    ["Chu kỳ / thời gian chờ", status.settings.cycleDays + " ngày / " + status.settings.graceDays + " ngày"],
    ["Đổi lịch đang chờ", status.settings.pendingCycleDays ? status.settings.pendingCycleDays + " / " + status.settings.pendingGraceDays + " ngày" : "—"],
  ]);
}

async function loadCheckIn() {
  const result = await api("GET", "/check-ins/me");
  if (result.ok) {
    renderCheckIn(result.data);
  } else {
    toast(describeError(result), true);
  }
}

async function checkIn() {
  const result = await api("POST", "/check-ins");
  if (report(result, "Đã điểm danh.")) {
    renderCheckIn(result.data);
  }
}

async function updateCheckInSettings(event) {
  event.preventDefault();
  if (data.checkInEtag === null) {
    toast("Bấm \"Xem trạng thái\" trước để lấy cấu hình hiện tại.", true);
    return;
  }
  const values = readForm(event.target);
  const body = { etag: data.checkInEtag };
  if (values.cycleDays !== "") {
    body.cycleDays = Number(values.cycleDays);
  }
  if (values.graceDays !== "") {
    body.graceDays = Number(values.graceDays);
  }
  const result = await api("PATCH", "/vaults/me/check-in-settings", { body: body });
  if (report(result, "Đã lưu lịch, áp dụng từ kỳ sau.")) {
    renderCheckIn(result.data);
  }
}

// ---------- eKYC ----------

async function loadEkyc() {
  const result = await api("GET", "/ekyc/me");
  byId("out-ekyc").textContent = JSON.stringify(result.data, null, 2);
  if (!result.ok) {
    toast(describeError(result), true);
  }
}

async function sendEkycImage(event, path, successMessage) {
  event.preventDefault();
  const values = readForm(event.target);
  const form = new FormData();
  form.append("file", values.file);
  const result = await api("POST", path, { form: form });
  byId("out-ekyc").textContent = JSON.stringify(result.data, null, 2);
  report(result, successMessage);
}

// ---------- Cấu hình và nhật ký ----------

function saveSettings(event) {
  event.preventDefault();
  const value = readForm(event.target).baseUrl;
  setBaseUrl(value);
  try {
    localStorage.setItem(baseUrlKey, getBaseUrl());
  } catch {
    // Trình duyệt chặn lưu cục bộ thì vẫn dùng giá trị trong phiên này.
  }
  toast("Đã lưu địa chỉ backend.", false);
}

async function checkHealth() {
  const origin = new URL(getBaseUrl()).origin;
  try {
    const response = await fetch(origin + "/health/live");
    if (response.ok) {
      toast("Backend đang chạy tại " + origin, false);
    } else {
      toast("Backend trả lỗi " + response.status, true);
    }
  } catch {
    toast("Không kết nối được " + origin + ". Backend đã chạy chưa?", true);
  }
}

function appendLog(entry) {
  const line = document.createElement("div");
  line.className = "log-line";
  const status = document.createElement("span");
  status.className = entry.status < 400 ? "ok" : "error";
  status.textContent = entry.status + " ";
  line.appendChild(status);
  let text = entry.method + " " + entry.path;
  if (entry.body !== null && entry.body !== undefined) {
    let body = typeof entry.body === "string" ? entry.body : JSON.stringify(entry.body);
    // Không hiện access token trong nhật ký.
    body = body.replace(/"accessToken":"[^"]*"/g, "\"accessToken\":\"(ẩn)\"");
    if (body.length > 300) {
      body = body.substring(0, 300) + "…";
    }
    text = text + "  " + body;
  }
  line.appendChild(document.createTextNode(text));
  const log = byId("log");
  log.prepend(line);
}

// ---------- Khởi động ----------

function bindEvents() {
  const tabButtons = byId("tabs").querySelectorAll("button");
  for (const button of tabButtons) {
    button.addEventListener("click", function () { showTab(button.dataset.tab); });
  }

  on("form-register", "submit", register);
  on("form-verify", "submit", submitVerify);
  on("btn-resend", "click", resend);
  on("form-login", "submit", login);
  on("btn-session", "click", loadSession);
  on("btn-refresh", "click", refresh);
  on("btn-logout", "click", logout);

  on("form-vault-create", "submit", createVault);
  on("form-vault-update", "submit", updateVault);
  on("btn-vault-load", "click", function () { loadVault(false); });
  on("btn-upgrade", "click", upgradeTier);

  on("form-asset-file", "submit", uploadFile);
  on("form-asset-text", "submit", createTextAsset);
  on("btn-assets-load", "click", loadAssets);

  on("form-beneficiary", "submit", createBeneficiary);
  on("btn-beneficiaries-load", "click", loadBeneficiaries);

  on("form-container", "submit", createContainer);
  on("btn-containers-load", "click", loadContainers);
  on("include-archived", "change", loadContainers);
  on("btn-add-asset", "click", function () { addToContainer("assets"); });
  on("btn-add-beneficiary", "click", function () { addToContainer("beneficiaries"); });

  on("btn-checkin-load", "click", loadCheckIn);
  on("btn-checkin", "click", checkIn);
  on("form-checkin-settings", "submit", updateCheckInSettings);

  on("btn-ekyc-load", "click", loadEkyc);
  on("form-ekyc-document", "submit", function (event) { sendEkycImage(event, "/ekyc/document", "Đã đọc ảnh CCCD."); });
  on("form-ekyc-selfie", "submit", function (event) { sendEkycImage(event, "/ekyc/face-match", "Đã so khớp khuôn mặt."); });

  on("form-settings", "submit", saveSettings);
  on("btn-health", "click", checkHealth);
  on("btn-clear-log", "click", function () { byId("log").innerHTML = ""; });
}

async function start() {
  try {
    const saved = localStorage.getItem(baseUrlKey);
    if (saved) {
      setBaseUrl(saved);
    }
  } catch {
    // Không đọc được lưu cục bộ thì dùng địa chỉ mặc định.
  }
  byId("form-settings").elements.baseUrl.value = getBaseUrl();

  onLog(appendLog);
  bindEvents();
  renderSession();

  // Mở từ link xác minh email (/verify-email?token=...) thì xác minh ngay.
  const params = new URLSearchParams(window.location.search);
  const token = params.get("token");
  if (window.location.pathname.startsWith("/verify-email") && token) {
    byId("form-verify").elements.token.value = token;
    await verifyEmail(token);
    history.replaceState(null, "", "/");
  }

  // Cookie phiên còn hạn (ví dụ sau khi tải lại trang) thì khôi phục phiên.
  const restored = await refreshSession();
  if (restored.ok) {
    applySession(restored);
    await loadVault(true);
  }
}

start();
