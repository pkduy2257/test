// Máy chủ tĩnh cho giao diện mẫu, không cần cài thư viện.
// Chạy: node frontend/server.mjs  → mở http://localhost:5173
// Cổng 5173 trùng với origin mà backend đã cho phép trong Cors:AllowedOrigins.
//
// Có thêm một đường dẫn CHỈ DÙNG KHI DEMO: POST /demo/upgrade-tier đổi hạng dịch vụ của kho
// bằng cách sửa thẳng database qua sqlcmd, vì backend chưa có chức năng thanh toán.
// Không đưa cách làm này vào code chính.
//
// Cấu hình database cho phần demo (biến môi trường, đều không bắt buộc):
//   DEMO_SQL_SERVER    mặc định (localdb)\MSSQLLocalDB; dùng Docker thì đặt 127.0.0.1,14331
//   DEMO_SQL_DATABASE  mặc định LegacyVault
//   DEMO_SQL_USER, DEMO_SQL_PASSWORD  có thì đăng nhập SQL (ví dụ sa của Docker), không có thì dùng tài khoản Windows
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { execFile } from "node:child_process";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL(".", import.meta.url));
const port = 5173;

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
};

// Giá trị của từng hạng, khớp với cách backend tạo kho và các con số trong tài liệu yêu cầu.
const tiers = {
  Xs: { quotaBytes: 200 * 1024 * 1024, slots: 2, paid: true },
  XsMax: { quotaBytes: 500 * 1024 * 1024, slots: 5, paid: true },
  Free: { quotaBytes: 20 * 1024 * 1024, slots: 0, paid: false },
};

const emailPattern = /^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$/;

function sendJson(response, status, body) {
  response.writeHead(status, { "Content-Type": "application/json; charset=utf-8" });
  response.end(JSON.stringify(body));
}

function readJsonBody(request) {
  return new Promise(function (resolve, reject) {
    let text = "";
    request.on("data", function (chunk) {
      text = text + chunk;
      if (text.length > 2048) {
        reject(new Error("Dữ liệu gửi lên quá lớn."));
        request.destroy();
      }
    });
    request.on("end", function () {
      try {
        resolve(JSON.parse(text));
      } catch {
        reject(new Error("Dữ liệu gửi lên không phải JSON."));
      }
    });
  });
}

function buildUpgradeSql(tierName, tier) {
  // email đã được kiểm tra bằng emailPattern nên không chứa dấu nháy; tierName chỉ lấy từ danh sách cố định.
  let status = "Free";
  let expires = "NULL";
  if (tier.paid) {
    status = "PaidActive";
    expires = "DATEADD(day, 365, SYSDATETIMEOFFSET())";
  }
  return "SET NOCOUNT ON; "
    + "UPDATE e SET Tier = N'" + tierName + "', Status = N'" + status + "', "
    + "StorageQuotaBytes = " + tier.quotaBytes + ", IncludedBeneficiarySlots = " + tier.slots + ", "
    + "ServiceExpiresAt = " + expires + ", UpdatedAt = SYSDATETIMEOFFSET() "
    + "FROM VaultEntitlements e JOIN Vaults v ON v.Id = e.VaultId JOIN Users u ON u.Id = v.OwnerId "
    + "WHERE u.Email = N'$(email)'; "
    + "SELECT @@ROWCOUNT;";
}

function runSqlcmd(sql, email) {
  const args = [
    "-S", process.env.DEMO_SQL_SERVER || "(localdb)\\MSSQLLocalDB",
    "-d", process.env.DEMO_SQL_DATABASE || "LegacyVault",
    "-b", "-h", "-1", "-W", "-C",
    "-v", "email=" + email,
    "-Q", sql,
  ];
  if (process.env.DEMO_SQL_USER) {
    args.push("-U", process.env.DEMO_SQL_USER, "-P", process.env.DEMO_SQL_PASSWORD || "");
  } else {
    args.push("-E");
  }
  return new Promise(function (resolve, reject) {
    execFile("sqlcmd", args, { timeout: 30000 }, function (error, stdout, stderr) {
      if (error) {
        reject(new Error((stderr || stdout || error.message).trim()));
        return;
      }
      resolve(stdout.trim());
    });
  });
}

async function handleUpgrade(request, response) {
  // Bắt buộc header riêng và JSON: trang web khác không gửi được kiểu request này nếu không qua CORS,
  // mà máy chủ này không trả header CORS nào.
  if (request.headers["x-demo"] !== "1" || !String(request.headers["content-type"]).startsWith("application/json")) {
    sendJson(response, 403, { message: "Chỉ gọi được từ giao diện mẫu." });
    return;
  }

  let body = null;
  try {
    body = await readJsonBody(request);
  } catch (error) {
    sendJson(response, 400, { message: error.message });
    return;
  }

  const email = String(body.email || "").trim().toLowerCase();
  const tierName = String(body.tier || "");
  const tier = tiers[tierName];
  if (!emailPattern.test(email) || tier === undefined) {
    sendJson(response, 400, { message: "Email hoặc hạng dịch vụ không hợp lệ." });
    return;
  }

  try {
    const output = await runSqlcmd(buildUpgradeSql(tierName, tier), email);
    const updated = Number(output.split(/\s+/).pop());
    if (updated === 0) {
      sendJson(response, 404, { message: "Tài khoản này chưa có kho. Hãy tạo kho trước." });
      return;
    }
    sendJson(response, 200, { message: "Đã đổi hạng dịch vụ sang " + tierName + ".", updated: updated });
  } catch (error) {
    sendJson(response, 500, { message: "Không chạy được sqlcmd: " + error.message });
  }
}

async function handleStatic(request, response) {
  const url = new URL(request.url, "http://localhost");
  let path = normalize(decodeURIComponent(url.pathname));

  // Mọi đường dẫn không phải tệp (ví dụ /verify-email?token=...) đều trả về trang chính.
  if (path === "/" || path === "\\" || extname(path) === "") {
    path = "/index.html";
  }

  const filePath = join(root, path);
  if (!filePath.startsWith(root)) {
    response.writeHead(403);
    response.end("Không được phép");
    return;
  }

  try {
    const body = await readFile(filePath);
    const type = contentTypes[extname(filePath)] || "application/octet-stream";
    response.writeHead(200, { "Content-Type": type, "Cache-Control": "no-store" });
    response.end(body);
  } catch {
    response.writeHead(404);
    response.end("Không tìm thấy");
  }
}

async function handle(request, response) {
  const url = new URL(request.url, "http://localhost");
  if (url.pathname === "/demo/upgrade-tier") {
    if (request.method !== "POST") {
      sendJson(response, 405, { message: "Chỉ hỗ trợ POST." });
      return;
    }
    await handleUpgrade(request, response);
    return;
  }
  await handleStatic(request, response);
}

// Chỉ nghe trên máy này (127.0.0.1) để máy khác trong mạng không gọi được phần sửa database.
createServer(handle).listen(port, "127.0.0.1", function () {
  console.log("Giao diện mẫu đang chạy tại http://localhost:" + port);
});
