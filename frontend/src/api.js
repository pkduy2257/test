// Lớp gọi API của backend LegacyVault.
// - Access token chỉ giữ trong bộ nhớ trang, mất khi tải lại trang (lấy lại bằng "Làm mới phiên").
// - Refresh token nằm trong cookie HttpOnly do backend đặt; trình duyệt tự gửi nhờ credentials: "include".
// - Đăng nhập, làm mới phiên, đăng xuất phải kèm mã chống giả mạo lấy từ GET /auth/csrf.

const csrfHeaderName = "X-CSRF-Token";

const state = {
  baseUrl: "http://localhost:5000/api/v1",
  accessToken: null,
  listeners: [],
};

export function getBaseUrl() {
  return state.baseUrl;
}

export function setBaseUrl(value) {
  let trimmed = value.trim();
  while (trimmed.endsWith("/")) {
    trimmed = trimmed.slice(0, -1);
  }
  state.baseUrl = trimmed;
}

export function getAccessToken() {
  return state.accessToken;
}

export function setAccessToken(token) {
  state.accessToken = token;
}

// Đăng ký hàm nhận nhật ký mỗi lần gọi API (để hiện lên màn hình).
export function onLog(listener) {
  state.listeners.push(listener);
}

function writeLog(entry) {
  for (const listener of state.listeners) {
    listener(entry);
  }
}

async function readBody(response) {
  const text = await response.text();
  if (text.length === 0) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

async function getCsrfToken() {
  const response = await fetch(state.baseUrl + "/auth/csrf", {
    method: "GET",
    credentials: "include",
  });
  const body = await readBody(response);
  writeLog({ method: "GET", path: "/auth/csrf", status: response.status, body: "(mã chống giả mạo, ẩn)" });
  if (!response.ok || body === null || typeof body.requestToken !== "string") {
    throw new Error("Không lấy được mã chống giả mạo (CSRF).");
  }
  return body.requestToken;
}

async function send(method, path, options) {
  const headers = {};
  let payload = undefined;

  if (options.form) {
    // Gửi tệp: để trình duyệt tự đặt Content-Type multipart kèm boundary.
    payload = options.form;
  } else if (options.body !== undefined) {
    headers["Content-Type"] = "application/json";
    payload = JSON.stringify(options.body);
  }

  if (options.csrf) {
    headers[csrfHeaderName] = await getCsrfToken();
  }

  if (state.accessToken && options.useToken !== false) {
    headers["Authorization"] = "Bearer " + state.accessToken;
  }

  const response = await fetch(state.baseUrl + path, {
    method: method,
    headers: headers,
    body: payload,
    credentials: "include",
  });
  const body = await readBody(response);
  writeLog({ method: method, path: path, status: response.status, body: body });
  return { ok: response.ok, status: response.status, data: body };
}

// Gọi API. Gặp 401 ở API thường thì thử làm mới phiên đúng một lần rồi gọi lại.
// options: { body, form, csrf, useToken, skipRefresh }
export async function api(method, path, options) {
  const settings = options || {};
  let result = await send(method, path, settings);

  if (result.status === 401 && !settings.skipRefresh && !path.startsWith("/auth/")) {
    const refreshed = await refreshSession();
    if (refreshed.ok) {
      result = await send(method, path, settings);
    }
  }
  return result;
}

export async function refreshSession() {
  const result = await send("POST", "/auth/refresh", { csrf: true, useToken: false });
  if (result.ok && result.data && result.data.accessToken) {
    state.accessToken = result.data.accessToken;
  } else {
    state.accessToken = null;
  }
  return result;
}

// Chuyển lỗi ProblemDetails của backend thành một câu dễ đọc.
export function describeError(result) {
  if (result.ok) {
    return "";
  }
  const data = result.data;
  if (data === null || typeof data !== "object") {
    return "Lỗi " + result.status;
  }
  let message = (data.title || "Lỗi") + " (" + (data.code || result.status) + ")";
  if (data.errors) {
    const parts = [];
    for (const field of Object.keys(data.errors)) {
      parts.push(field + ": " + data.errors[field].join(", "));
    }
    message = message + " — " + parts.join("; ");
  }
  return message;
}
