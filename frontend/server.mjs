// Máy chủ tĩnh cho giao diện mẫu, không cần cài thư viện.
// Chạy: node frontend/server.mjs  → mở http://localhost:5173
// Cổng 5173 trùng với origin mà backend đã cho phép trong Cors:AllowedOrigins.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
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

async function handle(request, response) {
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

createServer(handle).listen(port, function () {
  console.log("Giao diện mẫu đang chạy tại http://localhost:" + port);
});
