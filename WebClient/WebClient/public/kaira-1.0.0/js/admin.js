      // ============================================================
      //  CONFIG
      // ============================================================
      // Tự động detect môi trường: dev → localhost với port riêng, production → cùng origin
      (function () {
        const host = window.location.hostname;
        const isLocal =
          host === "localhost" || host === "127.0.0.1" || host === "::1";
        window.__API = isLocal
          ? { auth: "http://localhost:5002", product: "http://localhost:5001" }
          : { auth: window.location.origin, product: window.location.origin };
      })();
      const AUTH_API = window.__API.auth;
      const PRODUCT_API = window.__API.product;
      const ORDER_API = window.__API.product;

      // ============================================================
      //  STATE
      // ============================================================
      let allProducts = [];
      let allOrders = [];
      let allCustomers = [];
      let allCategories = [];
      let allOrderDetails = [];
      let filteredProducts = [];
      let filteredOrders = [];
      let filteredCustomers = [];
      let filteredOrderDetails = [];
      let productPage = 1;
      let currentOrderPage = 1;
      let customerPage = 1;
      let orderDetailPage = 1;
      let editingProductId = null;
      let currentOrderId = null;
      let pendingDeleteFn = null;

      // Banners & Featured Products state
      let allBanners = [];
      let allFeatured = [];
      let currentFeaturedSection = "new_arrivals";

      const PRODUCT_PAGE_SIZE = 10;
      const ORDER_PAGE_SIZE = 10;
      const CUSTOMER_PAGE_SIZE = 10;
      const ORDER_DETAIL_PAGE_SIZE = 10;

      // ============================================================
      //  HTTP HELPER
      // ============================================================
      async function apiFetch(base, path, method = "GET", body = null) {
        const token = localStorage.getItem("token");
        const opts = {
          method,
          headers: {
            "Content-Type": "application/json",
            ...(token ? { Authorization: "Bearer " + token } : {}),
          },
        };
        if (body) opts.body = JSON.stringify(body);
        const res = await fetch(base + path, opts);
        if (res.status === 401) {
          doLogout();
          throw new Error("Phiên hết hạn");
        }
        if (!res.ok) {
          let msg = "Lỗi " + res.status;
          try {
            const d = await res.json();
            msg = d.message || d.Message || msg;
          } catch (_) {}
          throw new Error(msg);
        }
        const ct = res.headers.get("content-type") || "";
        return ct.includes("application/json") ? res.json() : res.text();
      }

      // ============================================================
      //  SIDEBAR MOBILE
      // ============================================================
      function toggleSidebar() {
        const sb = document.getElementById("sidebar");
        const ov = document.getElementById("sidebarOverlay");
        if (!sb) return;
        const opened = sb.classList.toggle("open");
        if (ov) ov.classList.toggle("show", opened);
      }
      function closeSidebar() {
        const sb = document.getElementById("sidebar");
        const ov = document.getElementById("sidebarOverlay");
        if (sb) sb.classList.remove("open");
        if (ov) ov.classList.remove("show");
      }

      // ============================================================
      //  AUTH GUARD
      // ============================================================
      function doLogout() {
        localStorage.removeItem("token");
        localStorage.removeItem("user");
        window.location.href = "login.html";
      }

      function initAdminUser() {
        try {
          const user = JSON.parse(localStorage.getItem("user") || "null");
          if (!user) {
            doLogout();
            return;
          }
          const name =
            user.Name || user.name || user.UserName || user.Username || "Admin";
          document.getElementById("adminName").textContent = name;
          document.getElementById("adminAvatar").textContent = name
            .charAt(0)
            .toUpperCase();
          const role = user.Role || user.role || user.UserType || user.userType;
          if (role !== "Admin" && role !== "admin" && role !== 1) {
            alert("Bạn không có quyền truy cập trang Admin!");
            window.location.href = "index.html";
          }
        } catch (e) {
          doLogout();
        }
      }

      // ============================================================
      //  API STATUS
      // ============================================================
      async function checkApiStatus() {
        try {
          await apiFetch(PRODUCT_API, "/api/products?page=1&pageSize=1");
          setStatus("apiStatus", "ok", "API");
        } catch (e) {
          setStatus(
            "apiStatus",
            e.message.includes("401") || e.message.includes("403")
              ? "ok"
              : "err",
            "API",
          );
        }
        try {
          await apiFetch(AUTH_API, "/api/users");
          setStatus("authStatus", "ok", "Auth");
        } catch (e) {
          setStatus(
            "authStatus",
            e.message.includes("401") ? "ok" : "err",
            "Auth",
          );
        }
      }

      function setStatus(elId, status, label) {
        const el = document.getElementById(elId);
        el.className = "api-status " + status;
        el.innerHTML = `<span class="api-dot"></span>${label}`;
      }

      // ============================================================
      //  NAVIGATION
      // ============================================================
      const pageTitles = {
        dashboard: "Bảng Điều Khiển",
        products: "Quản Lý Sản Phẩm",
        orders: "Quản Lý Đơn Hàng",
        orderdetails: "Chi Tiết Đơn Hàng",
        customers: "Quản Lý Tài Khoản",
        stats: "Thống Kê",
        categories: "Danh mục",
        reviews: "Đánh giá",
        carts: "Giỏ hàng",
        orderlogs: "Lịch sử trạng thái đơn hàng",
        vouchers: "Mã giảm giá / Voucher",
        banners: "Quản lý Banners",
        featured: "Sản phẩm nổi bật",
        roles: "Phân quyền hệ thống",
        settings: "Cấu hình hệ thống",
      };

      function switchPage(page) {
        document
          .querySelectorAll(".page-section")
          .forEach((s) => s.classList.remove("active"));
        document
          .querySelectorAll(".nav-item-link")
          .forEach((a) => a.classList.remove("active"));
        document.getElementById("page-" + page)?.classList.add("active");
        document
          .querySelector(`[data-page="${page}"]`)
          ?.classList.add("active");
        document.getElementById("pageTitle").textContent =
          pageTitles[page] || page;
        if (page === "products") loadProducts();
        if (page === "orders") loadOrders();
        if (page === "customers") loadCustomers();
        if (page === "stats") loadStats();
        if (page === "dashboard") loadDashboard();
        if (page === "orderdetails") loadOrderDetails();
        if (page === "categories") loadCategories();
        if (page === "reviews") loadReviews();
        if (page === "carts") loadCarts();
        if (page === "orderlogs") loadOrderLogs();
        if (page === "vouchers") loadVouchers();
        if (page === "settings") loadSettings();
        if (page === "banners") loadBanners();
        if (page === "featured") loadFeaturedProducts();
        if (page === "roles") loadRoles();
      }

      // ============================================================
      //  TOAST
      // ============================================================
      function showToast(message, type = "success") {
        const c = document.getElementById("toastContainer");
        const t = document.createElement("div");
        t.className = `toast-msg ${type}`;
        t.innerHTML = `<span>${type === "success" ? "✓" : type === "error" ? "✕" : "ℹ"}</span> ${message}`;
        c.appendChild(t);
        setTimeout(() => t.remove(), 3500);
      }

      // ============================================================
      //  MODALS
      // ============================================================
      function openModal(id) {
        document.getElementById(id).classList.add("show");
      }
      function closeModal(id) {
        document.getElementById(id).classList.remove("show");
      }

      document.querySelectorAll(".modal-overlay").forEach((overlay) => {
        overlay.addEventListener("click", function (e) {
          if (e.target === this) this.classList.remove("show");
        });
      });

      // ============================================================
      //  FORMAT HELPERS
      // ============================================================
      function formatMoney(n) {
        if (n == null) return "--";
        return new Intl.NumberFormat("vi-VN").format(n) + " ₫";
      }
      function formatDate(d) {
        if (!d) return "--";
        return new Date(d).toLocaleDateString("vi-VN");
      }
      function escapeHtml(str) {
        if (!str) return "";
        return str.replace(
          /[&<>]/g,
          (m) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" })[m],
        );
      }
      function emptyRow(cols, msg) {
        return `<tr><td colspan="${cols}" class="text-center py-4" style="color:#9ca3af">${msg}</td></tr>`;
      }

      // ============================================================
      //  SORT HELPER — chuẩn hóa thứ tự hiển thị trong mọi bảng admin
      //  Mặc định: sắp theo ID tăng dần (STT 1, 2, 3...).
      //  Cho phép truyền field khác (vd: "code", "name") khi cần A→Z.
      // ============================================================
      function sortAsc(arr, field) {
        if (!Array.isArray(arr)) return arr;
        const fields = field ? [field] : ["id", "Id", "ID"];
        const pick = (obj) => {
          for (const f of fields) {
            if (obj && obj[f] !== undefined && obj[f] !== null) return obj[f];
          }
          return undefined;
        };
        return [...arr].sort((a, b) => {
          const va = pick(a);
          const vb = pick(b);
          if (va == null && vb == null) return 0;
          if (va == null) return 1;
          if (vb == null) return -1;
          const na = Number(va);
          const nb = Number(vb);
          if (!Number.isNaN(na) && !Number.isNaN(nb)) return na - nb;
          return String(va).localeCompare(String(vb), "vi", {
            sensitivity: "base",
          });
        });
      }

      // Sắp xếp theo ID giảm dần — mới nhất lên đầu
      function sortDesc(arr, field) {
        return sortAsc(arr, field).reverse();
      }

      // ============================================================
      //  XUẤT BÁO CÁO — EXCEL (SheetJS) + PDF (jsPDF + AutoTable)
      // ============================================================

      /**
       * Xuất danh sách records ra file .xlsx
       * @param {string} filename     vd. "san-pham"
       * @param {string} sheetName    tên sheet trong file
       * @param {Array}  columns      [{ header, key, width? }]
       * @param {Array}  rows         mảng object dữ liệu
       * @param {string} title        tiêu đề ở dòng đầu (optional)
       */
      function exportExcel(filename, sheetName, columns, rows, title) {
        if (typeof XLSX === "undefined") {
          showToast("Thư viện Excel chưa được tải", "error");
          return;
        }
        if (!rows || rows.length === 0) {
          showToast("Không có dữ liệu để xuất", "info");
          return;
        }
        const aoa = [];
        if (title) {
          aoa.push([title]);
          aoa.push(["Xuất lúc: " + new Date().toLocaleString("vi-VN")]);
          aoa.push([]); // dòng trống
        }
        aoa.push(columns.map((c) => c.header));
        rows.forEach((r) => {
          aoa.push(
            columns.map((c) => {
              const v = typeof c.key === "function" ? c.key(r) : r[c.key];
              return v == null ? "" : v;
            }),
          );
        });
        const ws = XLSX.utils.aoa_to_sheet(aoa);
        ws["!cols"] = columns.map((c) => ({ wch: c.width || 18 }));
        if (title) {
          // Merge title cell across all columns
          ws["!merges"] = ws["!merges"] || [];
          ws["!merges"].push({
            s: { r: 0, c: 0 },
            e: { r: 0, c: columns.length - 1 },
          });
        }
        const wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, sheetName.slice(0, 31));
        const ts = new Date().toISOString().slice(0, 10);
        XLSX.writeFile(wb, `${filename}-${ts}.xlsx`);
        showToast("✓ Đã xuất " + filename + ".xlsx", "success");
      }

      /**
       * Xuất bảng ra PDF (A4 landscape) với pdfmake.
       * Font Roboto bundled trong vfs_fonts hỗ trợ ĐẦY ĐỦ tiếng Việt có dấu.
       */
      function exportPdf(filename, title, columns, rows) {
        if (typeof pdfMake === "undefined") {
          showToast("Thư viện PDF chưa được tải", "error");
          return;
        }
        if (!rows || rows.length === 0) {
          showToast("Không có dữ liệu để xuất", "info");
          return;
        }

        // Header row của bảng
        const headerRow = columns.map((c) => ({
          text: c.header,
          style: "tableHeader",
          alignment: "center",
        }));

        // Body rows — auto-detect số để căn phải
        const bodyRows = rows.map((r) =>
          columns.map((c) => {
            const v = typeof c.key === "function" ? c.key(r) : r[c.key];
            const text = v == null ? "" : String(v);
            const isNum =
              typeof v === "number" ||
              (/^-?[0-9.,\s]+(\s*₫)?$/.test(text) && text.trim().length > 0);
            return {
              text,
              style: "tableCell",
              alignment: isNum ? "right" : "left",
            };
          }),
        );

        // Phân bổ chiều rộng cột theo column.width
        const totalW = columns.reduce((s, c) => s + (c.width || 18), 0);
        const widths = columns.map(
          (c) => ((c.width || 18) / totalW) * 100 + "%",
        );

        const docDef = {
          pageOrientation: "landscape",
          pageMargins: [30, 50, 30, 40],
          defaultStyle: { font: "Roboto", fontSize: 9 },
          content: [
            { text: title, style: "title" },
            {
              text: "Xuất lúc: " + new Date().toLocaleString("vi-VN"),
              style: "sub",
            },
            {
              table: {
                headerRows: 1,
                widths,
                body: [headerRow, ...bodyRows],
              },
              layout: {
                fillColor: (rowIdx) => {
                  if (rowIdx === 0) return "#f759ab"; // header pink
                  return rowIdx % 2 === 0 ? "#fdf2f8" : null; // pink nhạt xen kẽ
                },
                hLineWidth: () => 0.5,
                vLineWidth: () => 0.5,
                hLineColor: () => "#fbcfe8",
                vLineColor: () => "#fbcfe8",
                paddingLeft: () => 6,
                paddingRight: () => 6,
                paddingTop: () => 5,
                paddingBottom: () => 5,
              },
            },
          ],
          styles: {
            title: {
              fontSize: 16,
              bold: true,
              color: "#c41d7f",
              alignment: "center",
              margin: [0, 0, 0, 4],
            },
            sub: {
              fontSize: 9,
              color: "#6b7280",
              alignment: "center",
              margin: [0, 0, 0, 12],
            },
            tableHeader: {
              bold: true,
              color: "#ffffff",
              fontSize: 10,
            },
            tableCell: { fontSize: 9, color: "#1f2937" },
          },
          footer: function (currentPage, pageCount) {
            return {
              text: `Trang ${currentPage} / ${pageCount}`,
              alignment: "center",
              fontSize: 9,
              color: "#9ca3af",
              margin: [0, 12, 0, 0],
            };
          },
        };

        const ts = new Date().toISOString().slice(0, 10);
        try {
          pdfMake.createPdf(docDef).download(`${filename}-${ts}.pdf`);
          showToast("✓ Đã xuất " + filename + ".pdf", "success");
        } catch (e) {
          console.error("exportPdf error:", e);
          showToast("Lỗi xuất PDF: " + e.message, "error");
        }
      }

      // ----- Wrappers: dữ liệu cụ thể cho từng tab -----

      const PRODUCT_EXPORT_COLS = [
        { header: "ID", key: (r) => r.id || r.Id, width: 8 },
        { header: "Tên sản phẩm", key: (r) => r.name || r.Name, width: 35 },
        {
          header: "Danh mục",
          key: (r) => {
            const cid = r.categoryId ?? r.CategoryId;
            const c = allCategories.find((x) => x.id == cid);
            return c?.name || "--";
          },
          width: 20,
        },
        { header: "Giá", key: (r) => r.price ?? r.Price ?? 0, width: 14 },
        { header: "Tồn kho", key: (r) => r.stock ?? r.Stock ?? 0, width: 10 },
        {
          header: "Trạng thái",
          key: (r) => ((r.isActive ?? r.IsActive) ? "Hoạt động" : "Ngừng bán"),
          width: 14,
        },
        {
          header: "Mô tả",
          key: (r) => (r.description || r.Description || "").slice(0, 200),
          width: 50,
        },
      ];
      function exportProductsExcel() {
        const data = filteredProducts.length ? filteredProducts : allProducts;
        exportExcel(
          "san-pham",
          "Sản phẩm",
          PRODUCT_EXPORT_COLS,
          data,
          "DANH SÁCH SẢN PHẨM — GLOWHUB",
        );
      }
      function exportProductsPdf() {
        const data = filteredProducts.length ? filteredProducts : allProducts;
        // PDF — bỏ cột Mô tả cho gọn
        const cols = PRODUCT_EXPORT_COLS.filter((c) => c.header !== "Mô tả");
        exportPdf("san-pham", "DANH SÁCH SẢN PHẨM — GLOWHUB", cols, data);
      }

      const ORDER_STATUS_TEXT = {
        0: "Chờ xử lý",
        1: "Đang xử lý",
        2: "Đang giao",
        3: "Đã giao",
        4: "Đã hủy",
        PENDING: "Chờ xử lý",
        CONFIRMED: "Đang xử lý",
        SHIPPING: "Đang giao",
        COMPLETED: "Đã giao",
        CANCELLED: "Đã hủy",
      };
      const ORDER_EXPORT_COLS = [
        { header: "Mã đơn", key: (r) => "#" + (r.id || r.Id), width: 10 },
        {
          header: "Khách hàng",
          key: (r) =>
            r.userName || r.UserName || r.customerName || r.user?.name || "--",
          width: 22,
        },
        {
          header: "Ngày đặt",
          key: (r) => formatDate(r.orderDate || r.OrderDate),
          width: 14,
        },
        {
          header: "Tổng tiền",
          key: (r) => r.totalAmount ?? r.TotalAmount ?? 0,
          width: 16,
        },
        {
          header: "Trạng thái",
          key: (r) =>
            ORDER_STATUS_TEXT[r.status ?? r.Status] ??
            String(r.status ?? r.Status ?? ""),
          width: 14,
        },
        {
          header: "Địa chỉ",
          key: (r) => r.shippingAddress || r.ShippingAddress || "",
          width: 36,
        },
        {
          header: "Ghi chú",
          key: (r) => r.note || r.Note || "",
          width: 24,
        },
      ];
      function exportOrdersExcel() {
        const data = filteredOrders.length ? filteredOrders : allOrders;
        exportExcel(
          "don-hang",
          "Đơn hàng",
          ORDER_EXPORT_COLS,
          data,
          "DANH SÁCH ĐƠN HÀNG — GLOWHUB",
        );
      }
      function exportOrdersPdf() {
        const data = filteredOrders.length ? filteredOrders : allOrders;
        const cols = ORDER_EXPORT_COLS.filter(
          (c) => !["Địa chỉ", "Ghi chú"].includes(c.header),
        );
        exportPdf("don-hang", "DANH SÁCH ĐƠN HÀNG — GLOWHUB", cols, data);
      }

      const CUSTOMER_EXPORT_COLS = [
        { header: "ID", key: (r) => r.id || r.Id, width: 30 },
        {
          header: "Họ tên",
          key: (r) => r.name || r.Name || "--",
          width: 22,
        },
        {
          header: "Tên đăng nhập",
          key: (r) => r.userName || r.UserName || "",
          width: 18,
        },
        {
          header: "Email",
          key: (r) => r.email || r.Email || "",
          width: 24,
        },
        {
          header: "SĐT",
          key: (r) => r.phone || r.Phone || "",
          width: 14,
        },
        {
          header: "Loại tài khoản",
          key: (r) => {
            const t = r.userType ?? r.UserType;
            return t === 1 ? "Admin" : t === 2 ? "Manager" : "Khách hàng";
          },
          width: 16,
        },
        {
          header: "Trạng thái",
          key: (r) => ((r.isActive ?? r.IsActive) ? "Hoạt động" : "Bị khóa"),
          width: 14,
        },
        {
          header: "Ngày tạo",
          key: (r) => formatDate(r.created || r.Created || r.createdAt),
          width: 14,
        },
      ];
      function exportCustomersExcel() {
        const data = filteredCustomers.length
          ? filteredCustomers
          : allCustomers;
        exportExcel(
          "khach-hang",
          "Khách hàng",
          CUSTOMER_EXPORT_COLS,
          data,
          "DANH SÁCH KHÁCH HÀNG — GLOWHUB",
        );
      }
      function exportCustomersPdf() {
        const data = filteredCustomers.length
          ? filteredCustomers
          : allCustomers;
        const cols = CUSTOMER_EXPORT_COLS.filter((c) => c.header !== "ID");
        exportPdf("khach-hang", "DANH SÁCH KHÁCH HÀNG — GLOWHUB", cols, data);
      }

      /** Xuất báo cáo doanh thu theo tháng (12 tháng gần nhất) */
      function exportRevenueReport(fmt) {
        if (!window._statsCache) {
          showToast("Vui lòng vào tab Thống Kê trước", "info");
          return;
        }
        const { labels, values } = aggregateRevenue(
          window._statsCache.delivered,
          currentRevenueRange,
        );
        const rangeText =
          currentRevenueRange === "week"
            ? "7 ngày qua"
            : currentRevenueRange === "month"
              ? "30 ngày qua"
              : "12 tháng qua";
        const total = values.reduce((s, v) => s + v, 0);
        const rows = labels.map((lb, i) => ({
          period: lb,
          revenue: values[i],
        }));
        rows.push({
          period: "TỔNG CỘNG",
          revenue: total,
        });
        const cols = [
          { header: "Kỳ", key: "period", width: 18 },
          { header: "Doanh thu (₫)", key: "revenue", width: 22 },
        ];
        const title = `BÁO CÁO DOANH THU — ${rangeText.toUpperCase()}`;
        if (fmt === "pdf") {
          exportPdf(
            "doanh-thu-" + currentRevenueRange,
            title,
            cols,
            rows.map((r) => ({
              period: r.period,
              revenue: formatMoney(r.revenue),
            })),
          );
        } else {
          exportExcel(
            "doanh-thu-" + currentRevenueRange,
            "Doanh thu",
            cols,
            rows,
            title,
          );
        }
      }
      function getOrderStatusBadge(status) {
        let s = status;
        if (typeof s === "string") {
          const map = {
            PENDING: 0,
            CONFIRMED: 1,
            SHIPPING: 2,
            COMPLETED: 3,
            CANCELLED: 4,
          };
          s = map[s] !== undefined ? map[s] : 0;
        } else {
          s = s ?? 0;
        }
        const badgeMap = {
          0: ["pending", "Chờ Xử Lý"],
          1: ["processing", "Đang Xử Lý"],
          2: ["shipped", "Đang Giao"],
          3: ["delivered", "Đã Giao"],
          4: ["cancelled", "Đã Hủy"],
        };
        const [cls, label] = badgeMap[s] || ["pending", "Không Rõ"];
        return `<span class="badge-status badge-${cls}">${label}</span>`;
      }

      // ============================================================
      //  ORDER STATUS — constants & state machine
      // ============================================================
      const ORDER_STATUS_LABEL = {
        0: "Chờ Xử Lý",
        1: "Đang Xử Lý",
        2: "Đang Giao",
        3: "Đã Giao",
        4: "Đã Hủy",
      };
      const ORDER_STATUS_NAME = [
        "PENDING",
        "CONFIRMED",
        "SHIPPING",
        "COMPLETED",
        "CANCELLED",
      ];

      function normalizeOrderStatus(raw) {
        if (typeof raw === "string") {
          const map = {
            PENDING: 0,
            CONFIRMED: 1,
            SHIPPING: 2,
            COMPLETED: 3,
            CANCELLED: 4,
          };
          return map[raw] ?? 0;
        }
        return raw ?? 0;
      }

      /**
       * Workflow chuyển trạng thái đơn hàng:
       *   PENDING(0) → CONFIRMED(1) → SHIPPING(2) → COMPLETED(3)
       *   Bất kỳ (trừ COMPLETED) → CANCELLED(4)
       *   COMPLETED & CANCELLED là terminal (không đổi nữa)
       */
      function canTransitionOrderStatus(from, to) {
        from = normalizeOrderStatus(from);
        to = normalizeOrderStatus(to);
        if (from === to) return true; // giữ nguyên = OK (để hiển thị option hiện tại)
        if (from === 3 || from === 4) return false; // terminal
        if (to === 4) return true; // hủy bất kỳ lúc nào (trừ đã giao)
        return to === from + 1; // chỉ cho tiến đúng 1 bước
      }

      // ============================================================
      //  DASHBOARD
      // ============================================================
      async function loadDashboard() {
        try {
          [
            "stat-orders",
            "stat-products",
            "stat-customers",
            "stat-revenue",
          ].forEach((id) => {
            document.getElementById(id).textContent = "...";
          });
          let orders = [];
          try {
            const res = await apiFetch(ORDER_API, "/api/orders/all");
            orders = Array.isArray(res) ? res : res?.items || [];
          } catch (e) {
            // Fallback: gom hết qua phân trang để KPI không bị cắt ngọn
            let page = 1,
              pageSize = 200,
              hasMore = true;
            while (hasMore) {
              const res = await apiFetch(
                ORDER_API,
                `/api/orders?page=${page}&pageSize=${pageSize}`,
              );
              const items = Array.isArray(res) ? res : res?.items || [];
              orders = orders.concat(items);
              const totalPages =
                res?.totalPages ||
                Math.ceil((res?.totalCount || items.length) / pageSize);
              if (items.length < pageSize || page >= totalPages) hasMore = false;
              else page++;
              if (page > 50) break; // an toàn chống lặp vô tận
            }
          }
          const productRes = await apiFetch(
            PRODUCT_API,
            "/api/products?page=1&pageSize=100",
          );
          const products = Array.isArray(productRes)
            ? productRes
            : productRes?.items || [];
          let customers = [];
          try {
            const r = await apiFetch(AUTH_API, "/api/users");
            customers = Array.isArray(r) ? r : r?.items || r?.data || [];
          } catch (err) {
            customers = [];
          }
          allOrders = sortAsc(orders);
          allProducts = sortAsc(products);
          allCustomers = sortAsc(customers, "name");
          const deliveredOrders = orders.filter((o) => {
            let s = o.status ?? o.Status;
            if (typeof s === "string") {
              const m = {
                PENDING: 0,
                CONFIRMED: 1,
                SHIPPING: 2,
                COMPLETED: 3,
                CANCELLED: 4,
              };
              s = m[s] ?? 0;
            }
            return s === 3;
          });
          const revenue = deliveredOrders.reduce(
            (sum, o) => sum + (o.totalAmount ?? o.TotalAmount ?? 0),
            0,
          );
          document.getElementById("stat-orders").textContent = orders.length;
          document.getElementById("stat-products").textContent =
            products.length;
          document.getElementById("stat-customers").textContent =
            customers.length;
          document.getElementById("stat-revenue").textContent =
            formatMoney(revenue);
          const recentOrders = [...orders]
            .sort(
              (a, b) =>
                new Date(b.orderDate || b.OrderDate || b.createdAt || 0) -
                new Date(a.orderDate || a.OrderDate || a.createdAt || 0),
            )
            .slice(0, 8)
            .map((o) => {
              let status = o.status ?? o.Status;
              if (typeof status === "string") {
                const m = {
                  PENDING: 0,
                  CONFIRMED: 1,
                  SHIPPING: 2,
                  COMPLETED: 3,
                  CANCELLED: 4,
                };
                status = m[status] ?? 0;
              }
              return `<tr>
                <td><span style="font-weight:600;color:var(--pink)">#${o.id || o.Id}</span></td>
                <td>${o.userName || o.UserName || "Khách hàng"}</td>
                <td style="font-weight:600">${formatMoney(o.totalAmount ?? o.TotalAmount)}</td>
                <td>${getOrderStatusBadge(status)}</td>
                <td>${formatDate(o.orderDate || o.OrderDate || o.createdAt)}</td>
              </tr>`;
            })
            .join("");
          document.getElementById("dashboardOrderTable").innerHTML =
            recentOrders || emptyRow(5, "Chưa có đơn hàng nào");
        } catch (err) {
          console.error("Dashboard error:", err);
          showToast("Lỗi tải dashboard: " + err.message, "error");
        }
      }

      async function refreshDashboardIfActive() {
        const el = document.getElementById("page-dashboard");
        if (el && el.classList.contains("active")) await loadDashboard();
      }

      // ============================================================
      //  PAGINATION HELPER
      // ============================================================
      function renderPagination(
        containerId,
        totalItems,
        pageSize,
        currentPage,
        onPageChange,
      ) {
        const totalPages = Math.ceil(totalItems / pageSize);
        const el = document.getElementById(containerId);
        if (totalPages <= 1) {
          el.innerHTML = "";
          return;
        }
        let html = `<button class="page-btn" ${currentPage === 1 ? 'disabled style="opacity:0.4;cursor:not-allowed"' : ""} onclick="${onPageChange}(${currentPage - 1})">‹</button>`;
        for (let i = 1; i <= totalPages; i++) {
          if (
            totalPages > 7 &&
            i > 2 &&
            i < totalPages - 1 &&
            Math.abs(i - currentPage) > 1
          ) {
            if (i === 3 || i === totalPages - 2)
              html += `<span style="padding:0 4px;color:#9ca3af">…</span>`;
            continue;
          }
          html += `<button class="page-btn ${i === currentPage ? "active" : ""}" onclick="${onPageChange}(${i})">${i}</button>`;
        }
        html += `<button class="page-btn" ${currentPage === totalPages ? 'disabled style="opacity:0.4;cursor:not-allowed"' : ""} onclick="${onPageChange}(${currentPage + 1})">›</button>`;
        el.innerHTML = html;
      }

      // ============================================================
      //  PRODUCTS
      // ============================================================
      /**
       * Lấy TẤT CẢ danh mục (yêu cầu pageSize lớn) + đổ vào:
       *   - filter dropdown #productCategoryFilter (toolbar trang Sản phẩm)
       *   - select #productCategory (trong modal Thêm/Sửa sản phẩm)
       * Lưu vào biến global allCategories.
       */
      async function loadCategoriesForProduct() {
        try {
          // Một số backend phân trang mặc định 10–20 → request size lớn để chắc lấy hết
          let res;
          try {
            res = await apiFetch(
              PRODUCT_API,
              "/api/categories?page=1&pageSize=1000",
            );
          } catch (_) {
            res = await apiFetch(PRODUCT_API, "/api/categories");
          }
          allCategories = Array.isArray(res)
            ? res
            : res?.items || res?.data || [];

          // Đổ dropdown filter của trang Sản phẩm
          const sel = document.getElementById("productCategoryFilter");
          if (sel) {
            const current = sel.value;
            sel.innerHTML = '<option value="">📂 Tất cả danh mục</option>';
            allCategories.forEach((cat) => {
              const id = cat.id || cat.Id;
              const name = cat.name || cat.Name || "(không tên)";
              const opt = document.createElement("option");
              opt.value = id;
              opt.textContent = name;
              sel.appendChild(opt);
            });
            // Giữ lại lựa chọn cũ nếu vẫn tồn tại
            if (current && [...sel.options].some((o) => o.value === current)) {
              sel.value = current;
            }
          }

          // Đổ select trong modal Sản phẩm
          const modalSel = document.getElementById("productCategory");
          if (modalSel) {
            const current = modalSel.value;
            modalSel.innerHTML =
              '<option value="">-- Chọn danh mục --</option>';
            allCategories.forEach((cat) => {
              const id = cat.id || cat.Id;
              const name = cat.name || cat.Name || "(không tên)";
              const opt = document.createElement("option");
              opt.value = id;
              opt.textContent = name;
              modalSel.appendChild(opt);
            });
            if (
              current &&
              [...modalSel.options].some((o) => o.value === current)
            ) {
              modalSel.value = current;
            }
          }
        } catch (err) {
          console.warn("Lỗi load categories cho dropdown SP:", err);
        }
      }

      async function loadProducts() {
        const tbody = document.getElementById("productTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="6" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(
            PRODUCT_API,
            "/api/products?page=1&pageSize=100",
          );
          let products = [];
          if (Array.isArray(res)) products = res;
          else if (res?.items) products = res.items;
          else if (res?.data) products = res.data;
          else if (res?.products) products = res.products;
          allProducts = sortDesc(products);
          filteredProducts = [...allProducts];
          productPage = 1;
          // LUÔN reload danh mục cho dropdown (phòng trường hợp user vừa thêm/xóa danh mục)
          await loadCategoriesForProduct();
          renderProductPage();
        } catch (err) {
          console.error("Lỗi loadProducts:", err);
          if (tbody) tbody.innerHTML = emptyRow(6, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải sản phẩm: " + err.message, "error");
        }
      }

      function renderProductPage() {
        const start = (productPage - 1) * PRODUCT_PAGE_SIZE;
        const pageItems = filteredProducts.slice(
          start,
          start + PRODUCT_PAGE_SIZE,
        );
        const total = filteredProducts.length;
        const countSpan = document.getElementById("productCount");
        if (countSpan)
          countSpan.textContent = `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + PRODUCT_PAGE_SIZE, total)} / ${total} sản phẩm`;
        const tbody = document.getElementById("productTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(6, "Không có sản phẩm nào");
          return;
        }
        const get = (obj, field) => {
          if (!obj) return undefined;
          if (obj[field] !== undefined) return obj[field];
          const lf = field.toLowerCase();
          for (let k in obj) if (k.toLowerCase() === lf) return obj[k];
          return undefined;
        };
        let html = "";
        for (let p of pageItems) {
          const id = get(p, "id"),
            name = get(p, "name") || "Không tên",
            price = get(p, "price") || 0,
            stock = get(p, "stock") || 0,
            isActive = get(p, "isActive") === true,
            imageUrl = get(p, "imageUrl") || get(p, "image") || "",
            categoryId = get(p, "categoryId");
          let categoryName = "--";
          if (categoryId && allCategories.length) {
            const found = allCategories.find((c) => c.id == categoryId);
            if (found) categoryName = found.name;
          }
          // ImageUrl có thể là JSON array → lấy ảnh đầu làm thumbnail
          const firstImg = parseProductImages(imageUrl)[0] || "";
          const imgSrc = firstImg
            ? resolveImageUrl(firstImg)
            : "https://placehold.co/42x42/f5f0ea/999?text=NO-IMG";
          html += `<tr>
            <td><div class="d-flex align-items-center gap-3">
              <img src="${imgSrc}" class="product-thumb" onerror="this.src='https://placehold.co/42x42/f5f0ea/aaa?text=ERR'">
              <div><div style="font-weight:500;font-size:13px">${escapeHtml(name)}</div><div style="font-size:11px;color:#9ca3af">#${id}</div></div>
            </div></td>
            <td><span style="background:#f3f4f6;padding:3px 10px;border-radius:20px;font-size:12px">${escapeHtml(categoryName)}</span></td>
            <td style="font-weight:600;color:var(--pink)">${formatMoney(price)}</td>
            <td>${stock}</td>
            <td><span class="badge-status badge-${isActive ? "active" : "inactive"}">${isActive ? "Đang Bán" : "Ẩn"}</span></td>
            <td><div class="d-flex gap-1">
              <button type="button" class="btn-icon edit" title="Sửa" onclick="editProductById('${id}')">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              </button>
              <button type="button" class="btn-icon delete" title="Xóa" onclick="deleteProduct('${id}','${escapeHtml(name)}')">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
              </button>
            </div></td>
          </tr>`;
        }
        tbody.innerHTML = html;
        renderPagination(
          "productPagination",
          total,
          PRODUCT_PAGE_SIZE,
          productPage,
          "goProductPage",
        );
      }

      function goProductPage(p) {
        productPage = p;
        renderProductPage();
      }

      function filterProducts() {
        const search = document
          .getElementById("productSearch")
          .value.toLowerCase();
        const catId = document.getElementById("productCategoryFilter").value;
        const statusFilter = document.getElementById(
          "productStatusFilter",
        ).value;
        filteredProducts = allProducts.filter((p) => {
          const name = (p.Name || p.name || "").toLowerCase();
          const pCatId = p.categoryId ?? p.CategoryId;
          const isActive = (p.IsActive ?? p.isActive) === true;
          return (
            name.includes(search) &&
            (!catId || pCatId == catId) &&
            (!statusFilter || String(isActive) === statusFilter)
          );
        });
        productPage = 1;
        renderProductPage();
      }

      async function openProductModal() {
        editingProductId = null;
        document.getElementById("productModalTitle").innerText =
          "Thêm Sản Phẩm Mới";
        [
          "productId",
          "productName",
          "productPrice",
          "productStock",
          "productDescription",
          "productImage",
        ].forEach((id) => {
          document.getElementById(id).value = "";
        });
        document.getElementById("productStatus").value = "true";
        setProductImages([]);
        if (allCategories.length === 0) await loadCategoriesForProduct();
        const catSelect = document.getElementById("productCategory");
        if (catSelect) {
          catSelect.innerHTML = '<option value="">-- Chọn danh mục --</option>';
          allCategories.forEach((cat) => {
            const opt = document.createElement("option");
            opt.value = cat.id;
            opt.textContent = cat.name;
            catSelect.appendChild(opt);
          });
        }
        openModal("productModal");
      }

      function editProductById(id) {
        const p = allProducts.find(
          (x) => String(x.id || x.Id) === String(id),
        );
        if (p) editProduct(p);
      }

      async function editProduct(p) {
        if (typeof p === "string") p = JSON.parse(p);
        editingProductId = p.id || p.Id;
        document.getElementById("productModalTitle").innerText =
          "Chỉnh Sửa Sản Phẩm";
        document.getElementById("productId").value = editingProductId;
        document.getElementById("productName").value = p.name || p.Name || "";
        document.getElementById("productPrice").value =
          p.price ?? p.Price ?? "";
        document.getElementById("productStock").value =
          p.stock ?? p.Stock ?? "";
        document.getElementById("productStatus").value =
          (p.isActive ?? p.IsActive) ? "true" : "false";
        document.getElementById("productDescription").value =
          p.description || p.Description || "";
        // Parse danh sách ảnh (JSON array hoặc single URL) → absolute URL
        const rawImg = p.imageUrl || p.ImageUrl || p.image || "";
        const imgs = parseProductImages(rawImg).map(resolveImageUrl);
        setProductImages(imgs);
        if (allCategories.length === 0) await loadCategoriesForProduct();
        const catSelect = document.getElementById("productCategory");
        if (catSelect) {
          catSelect.innerHTML = '<option value="">-- Chọn danh mục --</option>';
          allCategories.forEach((cat) => {
            const opt = document.createElement("option");
            opt.value = cat.id;
            opt.textContent = cat.name;
            catSelect.appendChild(opt);
          });
          const catId = p.categoryId ?? p.CategoryId;
          if (catId) catSelect.value = catId;
        }
        openModal("productModal");
      }

      async function saveProduct() {
        const name = document.getElementById("productName").value.trim();
        const categoryId = parseInt(
          document.getElementById("productCategory").value,
        );
        const price = parseFloat(document.getElementById("productPrice").value);
        const stock =
          parseInt(document.getElementById("productStock").value) || 0;
        const isActive =
          document.getElementById("productStatus").value === "true";
        const description = document.getElementById("productDescription").value;
        // Lấy danh sách ảnh từ state gallery (đã được hidden field sync)
        const images = productImageList.slice();
        if (images.some((u) => u && u.startsWith("data:image"))) {
          showToast(
            "Không thể lưu ảnh dạng base64. Vui lòng upload lại hoặc nhập URL.",
            "error",
          );
          return;
        }
        // Backend chỉ có 1 cột ImageUrl → serialize:
        //  - 0 ảnh: ""
        //  - 1 ảnh: giữ nguyên URL (tương thích ngược)
        //  - >=2 ảnh: JSON array string
        let imageField = "";
        if (images.length === 1) imageField = images[0];
        else if (images.length > 1) imageField = JSON.stringify(images);
        const btn = document.getElementById("saveProductBtn");
        btn.disabled = true;
        btn.innerText = "Đang lưu...";
        const data = {
          Name: name,
          CategoryId: categoryId,
          Price: price,
          Stock: stock,
          IsActive: isActive,
          Description: description,
          ImageUrl: imageField,
        };
        if (editingProductId && window._rowVersion)
          data.RowVersion = window._rowVersion;
        try {
          if (editingProductId) {
            await apiFetch(
              PRODUCT_API,
              `/api/products/${editingProductId}`,
              "PUT",
              data,
            );
            showToast("Cập nhật thành công!", "success");
          } else {
            await apiFetch(PRODUCT_API, "/api/products", "POST", data);
            showToast("Thêm sản phẩm thành công!", "success");
          }
          closeModal("productModal");
          loadProducts();
          await refreshDashboardIfActive();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        } finally {
          btn.disabled = false;
          btn.innerText = "Lưu Sản Phẩm";
          delete window._rowVersion;
        }
      }

      function deleteProduct(id, name) {
        document.getElementById("confirmMessage").textContent =
          `Bạn có chắc muốn xóa sản phẩm "${name}"?`;
        pendingDeleteFn = async () => {
          await apiFetch(PRODUCT_API, `/api/products/${id}`, "DELETE");
          showToast("Đã xóa sản phẩm!", "success");
          loadProducts();
          await refreshDashboardIfActive();
        };
        openModal("confirmModal");
      }

      // ============================================================
      //  ORDER DETAILS
      // ============================================================
      async function loadOrderDetails() {
        const tbody = document.getElementById("orderDetailTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          let allOrdersData = [],
            page = 1,
            pageSize = 100,
            hasMore = true;
          const isAdmin = isAdminUser();
          while (hasMore) {
            const endpoint = isAdmin
              ? `/api/orders/all?page=${page}&pageSize=${pageSize}`
              : `/api/orders?page=${page}&pageSize=${pageSize}`;
            let data;
            try {
              data = await apiFetch(ORDER_API, endpoint);
            } catch (e) {
              if (isAdmin)
                data = await apiFetch(
                  ORDER_API,
                  `/api/orders?page=${page}&pageSize=${pageSize}`,
                );
              else throw e;
            }
            const items = Array.isArray(data) ? data : data?.items || [];
            allOrdersData = allOrdersData.concat(items);
            const totalPages =
              data?.totalPages ||
              Math.ceil((data?.totalCount || items.length) / pageSize);
            if (page >= totalPages) hasMore = false;
            else page++;
          }
          allOrders = sortAsc(allOrdersData);
          const [productsData, categoriesData] = await Promise.all([
            apiFetch(PRODUCT_API, "/api/products?page=1&pageSize=1000"),
            apiFetch(PRODUCT_API, "/api/categories"),
          ]);
          allProducts = sortAsc(
            Array.isArray(productsData)
              ? productsData
              : productsData?.items || [],
          );
          allCategories = sortAsc(
            Array.isArray(categoriesData)
              ? categoriesData
              : categoriesData?.items || [],
          );
          const details = [];
          for (const order of allOrders) {
            const orderId = order.id || order.Id;
            const items = order.items || order.orderDetails || [];
            for (const item of items) {
              const productId = item.productId ?? item.ProductId;
              const product = allProducts.find(
                (p) => (p.id || p.Id) == productId,
              );
              const productName = product
                ? product.name || product.Name
                : "Sản phẩm không xác định";
              let categoryId = null,
                categoryName = "--";
              if (product) {
                categoryId = product.categoryId ?? product.CategoryId;
                if (categoryId && allCategories.length) {
                  const cat = allCategories.find((c) => c.id == categoryId);
                  if (cat) categoryName = cat.name;
                }
              }
              details.push({
                id: item.id || item.Id,
                orderId,
                productName,
                categoryId,
                categoryName,
                quantity: item.quantity ?? item.Quantity ?? 0,
                unitPrice: item.unitPrice ?? item.UnitPrice ?? 0,
                total:
                  (item.quantity ?? item.Quantity ?? 0) *
                  (item.unitPrice ?? item.UnitPrice ?? 0),
                userName: order.userName || order.UserName || "N/A",
              });
            }
          }
          allOrderDetails = sortAsc(details);
          filteredOrderDetails = [...allOrderDetails];
          orderDetailPage = 1;
          const catFilter = document.getElementById(
            "orderDetailCategoryFilter",
          );
          if (catFilter && allCategories.length) {
            catFilter.innerHTML = '<option value="">Tất cả danh mục</option>';
            allCategories.forEach((cat) => {
              const opt = document.createElement("option");
              opt.value = cat.id;
              opt.textContent = cat.name;
              catFilter.appendChild(opt);
            });
          }
          renderOrderDetailPage();
        } catch (err) {
          console.error(err);
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải chi tiết đơn hàng: " + err.message, "error");
        }
      }

      function renderOrderDetailPage() {
        const start = (orderDetailPage - 1) * ORDER_DETAIL_PAGE_SIZE;
        const pageItems = filteredOrderDetails.slice(
          start,
          start + ORDER_DETAIL_PAGE_SIZE,
        );
        const total = filteredOrderDetails.length;
        document.getElementById("orderDetailCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + ORDER_DETAIL_PAGE_SIZE, total)} / ${total} chi tiết`;
        const tbody = document.getElementById("orderDetailTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Không có chi tiết đơn hàng nào");
          return;
        }
        let html = "";
        for (let d of pageItems) {
          html += `<tr>
            <td class="text-center">${d.id || "--"}</td>
            <td class="text-center"><span class="fw-bold text-pink">#${d.orderId}</span><br><small class="text-muted">${escapeHtml(d.userName)}</small></td>
            <td class="fw-semibold">${escapeHtml(d.productName)}</td>
            <td>${escapeHtml(d.categoryName)}</td>
            <td class="text-center">${d.quantity}</td>
            <td class="text-end">${formatMoney(d.unitPrice)}</td>
            <td class="text-end fw-bold">${formatMoney(d.total)}</td>
          </tr>`;
        }
        tbody.innerHTML = html;
        renderPagination(
          "orderDetailPagination",
          total,
          ORDER_DETAIL_PAGE_SIZE,
          orderDetailPage,
          "goOrderDetailPage",
        );
      }

      function goOrderDetailPage(page) {
        orderDetailPage = page;
        renderOrderDetailPage();
      }

      function filterOrderDetails() {
        const search =
          document.getElementById("orderDetailSearch")?.value?.toLowerCase() ||
          "";
        const orderIdFilter = document.getElementById(
          "orderDetailOrderIdFilter",
        )?.value;
        const categoryFilter = document.getElementById(
          "orderDetailCategoryFilter",
        )?.value;
        filteredOrderDetails = allOrderDetails.filter(
          (d) =>
            d.productName.toLowerCase().includes(search) &&
            (!orderIdFilter || d.orderId == orderIdFilter) &&
            (!categoryFilter || d.categoryId == categoryFilter),
        );
        orderDetailPage = 1;
        renderOrderDetailPage();
      }

      // ============================================================
      //  ORDERS
      // ============================================================
      function isAdminUser() {
        try {
          const user = JSON.parse(localStorage.getItem("user") || "{}");
          const role =
            user.Role ?? user.role ?? user.UserType ?? user.userType;
          return (
            role === 1 ||
            role === "Admin" ||
            role === "admin" ||
            user.isAdmin === true
          );
        } catch {
          return false;
        }
      }

      async function loadOrders() {
        const tbody = document.getElementById("orderTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const endpoint = isAdminUser() ? "/api/orders/all" : "/api/orders";
          const res = await apiFetch(ORDER_API, endpoint);
          allOrders = sortDesc(
            Array.isArray(res) ? res : res?.items || res?.data || [],
          );
          filteredOrders = [...allOrders];
          currentOrderPage = 1;
          renderOrderPage();
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải đơn hàng: " + err.message, "error");
        }
      }

      function renderOrderPage() {
        const start = (currentOrderPage - 1) * ORDER_PAGE_SIZE;
        const pageItems = filteredOrders.slice(start, start + ORDER_PAGE_SIZE);
        const total = filteredOrders.length;
        document.getElementById("orderCount").innerText =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + ORDER_PAGE_SIZE, total)} / ${total} đơn hàng`;
        const tbody = document.getElementById("orderTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Chưa có đơn hàng nào");
          return;
        }
        let html = "";
        for (let o of pageItems) {
          const id = o.id || o.Id;
          const userName = o.userName || o.UserName || o.customerName || "N/A";
          const shippingAddress =
            o.shippingAddress || o.ShippingAddress || "--";
          const totalAmount = o.totalAmount ?? o.TotalAmount ?? 0;
          let status = o.status ?? o.Status ?? 0;
          if (typeof status === "string") {
            const m = {
              PENDING: 0,
              CONFIRMED: 1,
              SHIPPING: 2,
              COMPLETED: 3,
              CANCELLED: 4,
            };
            status = m[status] ?? 0;
          }
          const orderDate = o.orderDate || o.OrderDate || o.createdAt;
          html += `<tr>
            <td><span style="font-weight:600;color:var(--pink)">#${id}</span></td>
            <td>${escapeHtml(userName)}</td>
            <td style="max-width:180px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${escapeHtml(shippingAddress)}</td>
            <td style="font-weight:600">${formatMoney(totalAmount)}</td>
            <td>${getOrderStatusBadge(status)}</td>
            <td>${formatDate(orderDate)}</td>
            <td><button type="button" class="btn-icon edit" onclick="viewOrder('${id}')">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
            </button></td>
          </tr>`;
        }
        tbody.innerHTML = html;
        renderOrderPagination(total);
      }

      function renderOrderPagination(total) {
        const totalPages = Math.ceil(total / ORDER_PAGE_SIZE);
        const container = document.getElementById("orderPagination");
        if (!container) return;
        if (totalPages <= 1) {
          container.innerHTML = "";
          return;
        }
        let html = `<button class="page-btn" ${currentOrderPage === 1 ? "disabled" : ""} data-page="${currentOrderPage - 1}">‹</button>`;
        for (let i = 1; i <= totalPages; i++) {
          if (
            totalPages > 7 &&
            i > 2 &&
            i < totalPages - 1 &&
            Math.abs(i - currentOrderPage) > 1
          ) {
            if (i === 3 || i === totalPages - 2)
              html += `<span style="padding:0 4px;color:#9ca3af">…</span>`;
            continue;
          }
          html += `<button class="page-btn ${i === currentOrderPage ? "active" : ""}" data-page="${i}">${i}</button>`;
        }
        html += `<button class="page-btn" ${currentOrderPage === totalPages ? "disabled" : ""} data-page="${currentOrderPage + 1}">›</button>`;
        container.innerHTML = html;
        container
          .querySelectorAll(".page-btn:not([disabled])")
          .forEach((btn) => {
            btn.addEventListener("click", () => {
              const p = parseInt(btn.getAttribute("data-page"));
              if (!isNaN(p) && p >= 1 && p <= totalPages) {
                currentOrderPage = p;
                renderOrderPage();
              }
            });
          });
      }

      function filterOrders() {
        const keyword =
          document.getElementById("orderSearch")?.value?.toLowerCase().trim() ||
          "";
        const statusFilter =
          document.getElementById("orderStatusFilter")?.value || "";
        const priceFrom =
          parseFloat(document.getElementById("priceFrom")?.value) || null;
        const priceTo =
          parseFloat(document.getElementById("priceTo")?.value) || null;
        filteredOrders = allOrders.filter((o) => {
          const id = String(o.id || o.Id || "");
          const userName = (
            o.userName ||
            o.UserName ||
            o.customerName ||
            ""
          ).toLowerCase();
          const address = (
            o.shippingAddress ||
            o.ShippingAddress ||
            ""
          ).toLowerCase();
          const totalAmount = parseFloat(o.totalAmount ?? o.TotalAmount ?? 0);
          let rawStatus = o.status ?? o.Status;
          let statusNum = 0;
          if (typeof rawStatus === "string") {
            const m = {
              PENDING: 0,
              CONFIRMED: 1,
              SHIPPING: 2,
              COMPLETED: 3,
              CANCELLED: 4,
            };
            statusNum = m[rawStatus] ?? 0;
          } else {
            statusNum = rawStatus ?? 0;
          }
          const matchKeyword =
            !keyword ||
            id.includes(keyword) ||
            userName.includes(keyword) ||
            address.includes(keyword);
          const matchStatus = !statusFilter || statusNum == statusFilter;
          const matchPrice =
            (!priceFrom || totalAmount >= priceFrom) &&
            (!priceTo || totalAmount <= priceTo);
          return matchKeyword && matchStatus && matchPrice;
        });
        currentOrderPage = 1;
        renderOrderPage();
      }

      async function viewOrder(orderId) {
        try {
          let order = allOrders.find((o) => (o.id || o.Id) == orderId);
          if (!order)
            order = await apiFetch(ORDER_API, `/api/orders/${orderId}`);
          currentOrderId = orderId;
          document.getElementById("orderModalId").innerText = `#${orderId}`;
          let statusNum = order.status ?? order.Status ?? 0;
          if (typeof statusNum === "string") {
            const m = {
              PENDING: 0,
              CONFIRMED: 1,
              SHIPPING: 2,
              COMPLETED: 3,
              CANCELLED: 4,
            };
            statusNum = m[statusNum] ?? 0;
          }
          // Chỉ liệt kê các trạng thái hợp lệ theo workflow + trạng thái hiện tại
          const statusOptions = [0, 1, 2, 3, 4]
            .filter((v) => canTransitionOrderStatus(statusNum, v))
            .map(
              (v) =>
                `<option value="${v}" ${statusNum == v ? "selected" : ""}>${ORDER_STATUS_LABEL[v]}</option>`,
            )
            .join("");
          const isTerminal = statusNum === 3 || statusNum === 4;
          const items =
            order.items || order.orderDetails || order.OrderDetails || [];
          let itemsHtml = items.length
            ? items
                .map(
                  (item) =>
                    `<div class="d-flex justify-content-between py-2 border-bottom"><span>${escapeHtml(item.productName || item.ProductName || "Sản phẩm")} × ${item.quantity || item.Quantity || 1}</span><span>${formatMoney((item.price || item.Price || 0) * (item.quantity || item.Quantity || 1))}</span></div>`,
                )
                .join("")
            : '<div class="text-muted">Không có chi tiết sản phẩm</div>';
          document.getElementById("orderModalBody").innerHTML = `
            <div class="row g-3 mb-4">
              <div class="col-6"><div class="small text-uppercase text-muted">Khách hàng</div><div>${escapeHtml(order.userName || order.UserName || "N/A")}</div></div>
              <div class="col-6"><div class="small text-uppercase text-muted">Ngày đặt</div><div>${formatDate(order.orderDate || order.OrderDate || order.createdAt)}</div></div>
              <div class="col-12"><div class="small text-uppercase text-muted">Địa chỉ giao hàng</div><div>${escapeHtml(order.shippingAddress || order.ShippingAddress || "--")}</div></div>
            </div>
            <div class="bg-light p-3 rounded mb-3"><div class="fw-semibold mb-2">Sản phẩm</div>${itemsHtml}
              <div class="d-flex justify-content-between mt-2 pt-2 border-top"><strong>Tổng cộng</strong><strong style="color:var(--pink)">${formatMoney(order.totalAmount || order.TotalAmount)}</strong></div>
            </div>
            <div class="mb-3"><label class="form-label-admin">Cập nhật trạng thái</label><select class="form-control-admin" id="orderStatusSelect" ${isTerminal ? "disabled" : ""}>${statusOptions}</select>${isTerminal ? '<div class="small text-muted mt-1">⚠️ Đơn ở trạng thái cuối, không thể đổi nữa.</div>' : ""}</div>
            <div class="mb-3"><label class="form-label-admin">Ghi chú (tuỳ chọn)</label><textarea class="form-control-admin" id="orderStatusNote" rows="2" placeholder="Nhập ghi chú cho lần cập nhật này (sẽ hiển thị ở Lịch sử trạng thái)..." style="resize:vertical;min-height:60px" ${isTerminal ? "disabled" : ""}></textarea></div>`;
          openModal("orderModal");
        } catch (err) {
          showToast("Lỗi tải chi tiết đơn: " + err.message, "error");
        }
      }

      async function saveOrderStatus() {
        const newStatus = parseInt(
          document.getElementById("orderStatusSelect").value,
        );
        const note =
          document.getElementById("orderStatusNote")?.value?.trim() || null;

        // Validate workflow lần cuối — phòng trường hợp DOM bị chỉnh thủ công
        const order = allOrders.find(
          (o) => (o.id || o.Id) == currentOrderId,
        );
        const currentStatus = normalizeOrderStatus(
          order?.status ?? order?.Status,
        );
        if (!canTransitionOrderStatus(currentStatus, newStatus)) {
          showToast(
            `Không thể chuyển từ "${ORDER_STATUS_LABEL[currentStatus]}" → "${ORDER_STATUS_LABEL[newStatus]}"`,
            "error",
          );
          return;
        }
        if (currentStatus === newStatus) {
          showToast("Trạng thái không thay đổi", "info");
          return;
        }

        const btn = document.getElementById("saveOrderBtn");
        btn.disabled = true;
        btn.textContent = "Đang lưu...";
        try {
          await apiFetch(
            ORDER_API,
            `/api/orders/${currentOrderId}/status`,
            "PUT",
            {
              status: ORDER_STATUS_NAME[newStatus],
              note,
              clientCreatedAt: new Date().toISOString(),
            },
          );
          const idx = allOrders.findIndex(
            (o) => (o.id || o.Id) == currentOrderId,
          );
          if (idx !== -1) {
            allOrders[idx].status = newStatus;
            allOrders[idx].Status = ORDER_STATUS_NAME[newStatus];
          }
          filterOrders();
          closeModal("orderModal");
          showToast("Đã cập nhật trạng thái đơn hàng!", "success");
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        } finally {
          btn.disabled = false;
          btn.textContent = "Cập Nhật Trạng Thái";
        }
      }

      // ======================= CUSTOMERS =======================
      //let allCustomers = [];
      //let filteredCustomers = [];
      //let customerPage = 1;
      //const CUSTOMER_PAGE_SIZE = 3;

      // Tải danh sách khách hàng
      async function loadCustomers() {
        const tbody = document.getElementById("customerTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(AUTH_API, "/api/users");
          if (Array.isArray(res)) allCustomers = res;
          else if (res?.items) allCustomers = res.items;
          else if (res?.data) allCustomers = res.data;
          else allCustomers = [];
          // Khách hàng dùng ID dạng GUID → sắp theo tên (A→Z) cho dễ tra cứu
          allCustomers = sortAsc(allCustomers, "name");
          filteredCustomers = [...allCustomers];
          customerPage = 1;
          renderCustomerPage();
          //showToast(`Đã tải ${allCustomers.length} khách hàng`, "success");
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải khách hàng: " + err.message, "error");
        }
      }

      // Hiển thị bảng khách hàng
      function renderCustomerPage() {
        const start = (customerPage - 1) * CUSTOMER_PAGE_SIZE;
        const pageItems = filteredCustomers.slice(
          start,
          start + CUSTOMER_PAGE_SIZE,
        );
        const total = filteredCustomers.length;
        document.getElementById("customerCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + CUSTOMER_PAGE_SIZE, total)} / ${total} khách hàng`;
        const tbody = document.getElementById("customerTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Không có khách hàng nào");
          return;
        }
        let html = "";
        for (let c of pageItems) {
          const name = c.name || c.Name || "--";
          const userName = c.userName || c.UserName || c.username || "--";
          const email = c.email || c.Email || "--";
          const phone = c.phone || c.Phone || "--";
          const userType =
            (c.userType ?? c.UserType) === 1 ? "Admin" : "Khách hàng";
          const isActive = (c.isActive ?? c.IsActive) === true;
          html += `
      <tr>
        <td class="fw-semibold">${escapeHtml(name)}</td>
        <td>${escapeHtml(userName)}</td>
        <td>${escapeHtml(email)}</td>
        <td>${escapeHtml(phone)}</td>
        <td>${userType === "Admin" ? '<span class="badge-status badge-processing">Admin</span>' : '<span class="badge-status">Khách hàng</span>'}</td>
        <td><span class="badge-status badge-${isActive ? "active" : "inactive"}">${isActive ? "Hoạt động" : "Khóa"}</span></td>
        <td>
          <div class="d-flex gap-1">
            <button type="button" class="btn-icon edit" onclick="openEditUserModal('${c.id}')">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
            </button>
            <button type="button" class="btn-icon delete" onclick="deleteUserAccount('${c.id}','${escapeHtml(name)}')">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
            </button>
          </div>
        </td>
      </tr>
    `;
        }
        tbody.innerHTML = html;
        renderPagination(
          "customerPagination",
          total,
          CUSTOMER_PAGE_SIZE,
          customerPage,
          "goCustomerPage",
        );
      }

      // Phân trang
      function goCustomerPage(page) {
        if (
          page < 1 ||
          page > Math.ceil(filteredCustomers.length / CUSTOMER_PAGE_SIZE)
        )
          return;
        customerPage = page;
        renderCustomerPage();
      }

      // Lọc khách hàng
      function filterCustomers() {
        const keyword =
          document.getElementById("customerSearch")?.value?.toLowerCase() || "";
        const role = document.getElementById("customerRoleFilter")?.value || "";
        const activeStatus =
          document.getElementById("customerActiveFilter")?.value || "";

        filteredCustomers = allCustomers.filter((c) => {
          const name = (c.name || c.Name || "").toLowerCase();
          const userName = (c.userName || c.UserName || "").toLowerCase();
          const email = (c.email || c.Email || "").toLowerCase();
          const matchSearch =
            name.includes(keyword) ||
            userName.includes(keyword) ||
            email.includes(keyword);
          const userType = c.userType ?? c.UserType ?? 0;
          const matchRole = !role || userType == role;
          const isActive = (c.isActive ?? c.IsActive) === true;
          const matchActive =
            !activeStatus || String(isActive) === activeStatus;
          return matchSearch && matchRole && matchActive;
        });
        customerPage = 1;
        renderCustomerPage();
      }

      // Mở modal sửa thông tin
      async function openEditUserModal(userId) {
        const user = allCustomers.find((u) => u.id == userId);
        if (!user) return;
        document.getElementById("editUserId").value = user.id;
        document.getElementById("editName").value =
          user.name || user.Name || "";
        document.getElementById("editUserName").value =
          user.userName || user.UserName || user.username || "";
        document.getElementById("editEmail").value =
          user.email || user.Email || "";
        document.getElementById("editPhone").value =
          user.phone || user.Phone || "";
        document.getElementById("editUserType").value =
          (user.userType ?? user.UserType) === 1 ? "1" : "0";
        document.getElementById("editIsActive").value =
          (user.isActive ?? user.IsActive) === true ? "true" : "false";
        document.getElementById("editPassword").value = "";
        openModal("userModal");
      }

      // Xóa tài khoản
      async function deleteUserAccount(userId, userName) {
        // Chặn admin tự xóa chính mình
        const me = JSON.parse(localStorage.getItem("user") || "{}");
        const myId = me.id || me.Id;
        if (myId && String(myId) === String(userId)) {
          showToast("Không thể xóa chính tài khoản của bạn!", "error");
          return;
        }
        // Chặn xóa admin cuối cùng
        const target = allCustomers.find(
          (u) => String(u.id || u.Id) === String(userId),
        );
        const isTargetAdmin = (target?.userType ?? target?.UserType) === 1;
        if (isTargetAdmin) {
          const adminCount = allCustomers.filter(
            (u) => (u.userType ?? u.UserType) === 1,
          ).length;
          if (adminCount <= 1) {
            showToast(
              "Không thể xóa admin cuối cùng của hệ thống!",
              "error",
            );
            return;
          }
        }
        if (
          confirm(
            `Bạn có chắc muốn xóa tài khoản "${userName}"? Hành động này không thể khôi phục.`,
          )
        ) {
          try {
            await apiFetch(AUTH_API, `/api/users/${userId}`, "DELETE");
            showToast(`Đã xóa tài khoản "${userName}"`, "success");
            await loadCustomers();
            refreshDashboardIfActive();
          } catch (err) {
            showToast("Lỗi xóa tài khoản: " + err.message, "error");
          }
        }
      }

      // Lưu thông tin (có đổi mật khẩu)
      async function saveUser() {
        const userId = document.getElementById("editUserId").value;
        const name = document.getElementById("editName").value.trim();
        const email = document.getElementById("editEmail").value.trim();
        const phone = document.getElementById("editPhone").value.trim();
        const userType = parseInt(
          document.getElementById("editUserType").value,
        );
        const isActive =
          document.getElementById("editIsActive").value === "true";
        const newPassword = document
          .getElementById("editPassword")
          .value.trim();

        if (!name || !email) {
          showToast("Họ tên và email không được để trống", "error");
          return;
        }
        // Validate định dạng email
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
          showToast("Email không đúng định dạng", "error");
          return;
        }
        // Validate SĐT (nếu nhập): chỉ chấp nhận 9–11 chữ số, có thể có dấu + ở đầu
        if (phone) {
          const phoneClean = phone.replace(/[\s.-]/g, "");
          if (!/^\+?\d{9,11}$/.test(phoneClean)) {
            showToast(
              "Số điện thoại phải có 9–11 chữ số",
              "error",
            );
            return;
          }
        }

        // Chặn admin tự khóa hoặc tự hạ quyền chính mình
        const me = JSON.parse(localStorage.getItem("user") || "{}");
        const myId = me.id || me.Id;
        const isSelf = myId && String(myId) === String(userId);
        if (isSelf) {
          if (!isActive) {
            showToast("Không thể tự khóa tài khoản của bạn!", "error");
            return;
          }
          if (userType !== 1) {
            showToast(
              "Không thể tự hạ quyền admin của bạn!",
              "error",
            );
            return;
          }
        } else {
          // Đổi user khác → chặn nếu đang hạ quyền/khóa admin cuối cùng còn hoạt động
          const target = allCustomers.find(
            (u) => String(u.id || u.Id) === String(userId),
          );
          const wasAdmin = (target?.userType ?? target?.UserType) === 1;
          if (wasAdmin && (userType !== 1 || !isActive)) {
            const activeAdminCount = allCustomers.filter(
              (u) =>
                (u.userType ?? u.UserType) === 1 &&
                (u.isActive ?? u.IsActive) === true,
            ).length;
            if (activeAdminCount <= 1) {
              showToast(
                "Không thể hạ quyền/khóa admin cuối cùng đang hoạt động!",
                "error",
              );
              return;
            }
          }
        }

        const data = {
          Name: name,
          Email: email,
          Phone: phone,
          UserType: userType,
          IsActive: isActive,
        };
        if (newPassword) {
          data.Password = newPassword; // cần backend hỗ trợ
        }

        const btn = document.querySelector("#userModal .btn-primary-admin");
        btn.disabled = true;
        btn.innerText = "Đang lưu...";

        try {
          await apiFetch(AUTH_API, `/api/users/${userId}`, "PUT", data);
          showToast("Cập nhật thành công", "success");
          closeModal("userModal");
          await loadCustomers();
          refreshDashboardIfActive();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
          console.error(err);
        } finally {
          btn.disabled = false;
          btn.innerText = "Lưu thay đổi";
        }
      }

      // Hiển thị/ẩn mật khẩu
      function togglePasswordVisibility(fieldId) {
        const field = document.getElementById(fieldId);
        if (field.type === "password") field.type = "text";
        else field.type = "password";
      }

      // ============================================================
      //  STATS
      // ============================================================
      async function loadStats() {
        [
          "stats-total-revenue",
          "stats-total-orders",
          "stats-avg-order",
          "stats-completion-rate",
        ].forEach((id) => {
          const el = document.getElementById(id);
          if (el) el.innerText = "--";
        });
        document.getElementById("statsOrderStatus").innerHTML =
          '<div class="text-center py-3"><div class="spinner mx-auto"></div></div>';
        document.getElementById("statsTopProducts").innerHTML =
          '<tr><td colspan="6" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>';
        document.getElementById("statsCustomerTypes").innerHTML =
          '<div class="text-center py-3"><div class="spinner mx-auto"></div></div>';
        document.getElementById("statsRecentOrders").innerHTML =
          '<tr><td colspan="4" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>';
        // Helper: gom hết đơn qua phân trang, dùng khi /orders/all fail
        const fetchAllOrdersPaginated = async () => {
          let all = [],
            page = 1,
            pageSize = 200,
            hasMore = true;
          while (hasMore) {
            const r = await apiFetch(
              ORDER_API,
              `/api/orders?page=${page}&pageSize=${pageSize}`,
            );
            const items = Array.isArray(r) ? r : r?.items || [];
            all = all.concat(items);
            const totalPages =
              r?.totalPages ||
              Math.ceil((r?.totalCount || items.length) / pageSize);
            if (items.length < pageSize || page >= totalPages) hasMore = false;
            else page++;
            if (page > 50) break;
          }
          return all;
        };

        try {
          const [ordersRes, productsRes, usersRes, categoriesRes] =
            await Promise.allSettled([
              apiFetch(ORDER_API, "/api/orders/all").catch(
                fetchAllOrdersPaginated,
              ),
              apiFetch(PRODUCT_API, "/api/products?page=1&pageSize=1000"),
              apiFetch(AUTH_API, "/api/users"),
              apiFetch(PRODUCT_API, "/api/categories"),
            ]);
          const extractArr = (res) => {
            if (res.status !== "fulfilled") return [];
            const d = res.value;
            return Array.isArray(d) ? d : d?.items || d?.data || d?.users || [];
          };
          let orders = extractArr(ordersRes),
            products = extractArr(productsRes),
            users = extractArr(usersRes),
            categories = extractArr(categoriesRes);

          const normalizedOrders = orders.map((o) => {
            let s = o.status ?? o.Status ?? 0;
            if (typeof s === "string") {
              const m = {
                PENDING: 0,
                CONFIRMED: 1,
                SHIPPING: 2,
                COMPLETED: 3,
                CANCELLED: 4,
              };
              s = m[s] ?? 0;
            }
            return {
              id: o.id || o.Id,
              totalAmount: o.totalAmount ?? o.TotalAmount ?? 0,
              status: s,
              orderDate: o.orderDate || o.OrderDate || o.createdAt,
              userName: o.userName || o.UserName || o.customerName || "Khách",
              items: o.items || o.orderDetails || [],
            };
          });

          const delivered = normalizedOrders.filter((o) => o.status === 3);
          const totalRev = delivered.reduce((sum, o) => sum + o.totalAmount, 0);
          const avgOrder = delivered.length
            ? Math.round(totalRev / delivered.length)
            : 0;
          const completion = normalizedOrders.length
            ? Math.round((delivered.length / normalizedOrders.length) * 100)
            : 0;

          document.getElementById("stats-total-revenue").innerText =
            formatMoney(totalRev);
          document.getElementById("stats-total-orders").innerText =
            normalizedOrders.length;
          document.getElementById("stats-avg-order").innerText =
            formatMoney(avgOrder);
          document.getElementById("stats-completion-rate").innerText =
            completion + "%";

          // Order status bars
          const sc = { 0: 0, 1: 0, 2: 0, 3: 0, 4: 0 };
          normalizedOrders.forEach((o) => sc[o.status]++);
          const slabels = {
            0: "Chờ xử lý",
            1: "Đang xử lý",
            2: "Đang giao",
            3: "Đã giao",
            4: "Đã hủy",
          };
          const scolors = {
            0: "#f59e0b",
            1: "#3b82f6",
            2: "#8b5cf6",
            3: "#10b981",
            4: "#ef4444",
          };
          let statusHtml = "";
          for (let i = 0; i <= 4; i++) {
            const pct = normalizedOrders.length
              ? Math.round((sc[i] / normalizedOrders.length) * 100)
              : 0;
            statusHtml += `<div class="mb-3"><div class="d-flex justify-content-between mb-1"><span>${slabels[i]}</span><span class="fw-bold" style="color:${scolors[i]}">${sc[i]} đơn (${pct}%)</span></div><div class="progress" style="height:8px;"><div class="progress-bar" style="width:${pct}%;background:${scolors[i]};"></div></div></div>`;
          }
          document.getElementById("statsOrderStatus").innerHTML = statusHtml;

          // Top products
          const productSales = new Map();
          for (const order of delivered) {
            for (const item of order.items || []) {
              const productId = item.productId ?? item.ProductId;
              const qty = item.quantity ?? item.Quantity ?? 0;
              if (!productId) continue;
              const product = products.find((p) => (p.id || p.Id) == productId);
              if (!product) continue;
              if (productSales.has(productId))
                productSales.get(productId).totalSold += qty;
              else productSales.set(productId, { product, totalSold: qty });
            }
          }
          const topProducts = Array.from(productSales.values())
            .sort((a, b) => b.totalSold - a.totalSold)
            .slice(0, 5);
          let topHtml =
            topProducts.length === 0
              ? '<tr><td colspan="5" class="text-center">Chưa có dữ liệu bán hàng</td></tr>'
              : topProducts
                  .map(({ product, totalSold }, i) => {
                    const catId = product.categoryId ?? product.CategoryId;
                    const cat = catId
                      ? categories.find((c) => c.id == catId)
                      : null;
                    return `<tr><td class="fw-semibold text-muted">${i + 1}</td><td class="fw-semibold">${escapeHtml(product.name || product.Name || "--")}</td><td>${escapeHtml(cat?.name || "--")}</td><td style="color:var(--pink);font-weight:600">${formatMoney(product.price ?? product.Price ?? 0)}</td><td class="fw-bold text-center">${totalSold}</td></tr>`;
                  })
                  .join("");
          document.getElementById("statsTopProducts").innerHTML = topHtml;

          // Customer types
          const adminCount = users.filter(
            (u) => (u.userType ?? u.UserType) === 1,
          ).length;
          const activeCount = users.filter(
            (u) => (u.isActive ?? u.IsActive) === true,
          ).length;
          document.getElementById("statsCustomerTypes").innerHTML = `
            <div class="row g-3 text-center">
              <div class="col-6"><div class="p-3 bg-light rounded-3"><div class="fs-1 fw-bold text-primary">${users.length - adminCount}</div><div class="small text-muted">Khách hàng</div></div></div>
              <div class="col-6"><div class="p-3 bg-light rounded-3"><div class="fs-1 fw-bold text-info">${adminCount}</div><div class="small text-muted">Quản trị viên</div></div></div>
              <div class="col-6"><div class="p-3 bg-light rounded-3"><div class="fs-1 fw-bold text-success">${activeCount}</div><div class="small text-muted">Đang hoạt động</div></div></div>
              <div class="col-6"><div class="p-3 bg-light rounded-3"><div class="fs-1 fw-bold text-danger">${users.length - activeCount}</div><div class="small text-muted">Bị khóa</div></div></div>
            </div>
            <div class="text-center mt-3 text-muted small">Tổng số: <strong>${users.length}</strong> tài khoản</div>`;

          // Recent orders
          const recentHtml = [...normalizedOrders]
            .sort((a, b) => new Date(b.orderDate) - new Date(a.orderDate))
            .slice(0, 5)
            .map(
              (o) =>
                `<tr><td class="fw-semibold" style="color:var(--pink)">#${o.id}</td><td>${escapeHtml(o.userName)}</td><td class="fw-bold">${formatMoney(o.totalAmount)}</td><td>${getOrderStatusBadge(o.status)}</td></tr>`,
            )
            .join("");
          document.getElementById("statsRecentOrders").innerHTML =
            recentHtml ||
            '<tr><td colspan="4" class="text-center">Chưa có đơn hàng</td></tr>';

          // Lưu cache để các nút toggle Tuần/Tháng/Năm dùng lại không cần fetch
          window._statsCache = {
            normalizedOrders,
            delivered,
            statusCount: sc,
            statusLabels: slabels,
            statusColors: scolors,
            topProducts,
          };

          // Vẽ 3 biểu đồ
          renderRevenueChart(currentRevenueRange);
          renderStatusDoughnut();
          renderTopProductsChart();
        } catch (error) {
          console.error("loadStats error:", error);
          showToast("Lỗi khi tải thống kê: " + error.message, "error");
        }
      }

      // ============================================================
      //  BIỂU ĐỒ THỐNG KÊ NÂNG CAO
      // ============================================================
      let currentRevenueRange = "week";
      const _charts = { revenue: null, status: null, top: null };

      function setRevenueRange(range) {
        currentRevenueRange = range;
        document.querySelectorAll(".btn-range").forEach((b) => {
          b.classList.toggle("active", b.dataset.range === range);
        });
        if (window._statsCache) renderRevenueChart(range);
      }

      /**
       * Gom doanh thu các đơn ĐÃ GIAO theo bucket thời gian.
       * range = "week" → 7 ngày gần nhất (theo ngày)
       *       = "month" → 30 ngày gần nhất (theo ngày)
       *       = "year" → 12 tháng gần nhất (theo tháng)
       */
      function aggregateRevenue(delivered, range) {
        const now = new Date();
        const buckets = [];
        const map = new Map();

        if (range === "year") {
          // 12 tháng gần nhất
          for (let i = 11; i >= 0; i--) {
            const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
            const key =
              d.getFullYear() + "-" + String(d.getMonth() + 1).padStart(2, "0");
            const label =
              "T" + (d.getMonth() + 1) + "/" + String(d.getFullYear()).slice(2);
            buckets.push({ key, label });
            map.set(key, 0);
          }
          delivered.forEach((o) => {
            const od = new Date(o.orderDate);
            if (isNaN(od)) return;
            const key =
              od.getFullYear() +
              "-" +
              String(od.getMonth() + 1).padStart(2, "0");
            if (map.has(key)) map.set(key, map.get(key) + (o.totalAmount || 0));
          });
        } else {
          // week (7 ngày) hoặc month (30 ngày)
          const days = range === "week" ? 7 : 30;
          for (let i = days - 1; i >= 0; i--) {
            const d = new Date(
              now.getFullYear(),
              now.getMonth(),
              now.getDate() - i,
            );
            const key =
              d.getFullYear() +
              "-" +
              String(d.getMonth() + 1).padStart(2, "0") +
              "-" +
              String(d.getDate()).padStart(2, "0");
            const label =
              range === "week"
                ? ["CN", "T2", "T3", "T4", "T5", "T6", "T7"][d.getDay()] +
                  " " +
                  d.getDate() +
                  "/" +
                  (d.getMonth() + 1)
                : d.getDate() + "/" + (d.getMonth() + 1);
            buckets.push({ key, label });
            map.set(key, 0);
          }
          delivered.forEach((o) => {
            const od = new Date(o.orderDate);
            if (isNaN(od)) return;
            const key =
              od.getFullYear() +
              "-" +
              String(od.getMonth() + 1).padStart(2, "0") +
              "-" +
              String(od.getDate()).padStart(2, "0");
            if (map.has(key)) map.set(key, map.get(key) + (o.totalAmount || 0));
          });
        }
        return {
          labels: buckets.map((b) => b.label),
          values: buckets.map((b) => map.get(b.key) || 0),
        };
      }

      function renderRevenueChart(range) {
        if (typeof Chart === "undefined" || !window._statsCache) return;
        const ctx = document.getElementById("revenueChart");
        if (!ctx) return;
        const { labels, values } = aggregateRevenue(
          window._statsCache.delivered,
          range,
        );
        const total = values.reduce((s, v) => s + v, 0);
        const rangeText =
          range === "week"
            ? "7 ngày qua"
            : range === "month"
              ? "30 ngày qua"
              : "12 tháng qua";
        const sumEl = document.getElementById("revenueSummary");
        if (sumEl)
          sumEl.textContent = `${rangeText} · Tổng ${formatMoney(total)}`;

        if (_charts.revenue) _charts.revenue.destroy();

        // Gradient hồng cho fill
        const c = ctx.getContext("2d");
        const grad = c.createLinearGradient(0, 0, 0, 320);
        grad.addColorStop(0, "rgba(247,89,171,0.35)");
        grad.addColorStop(1, "rgba(247,89,171,0.02)");

        _charts.revenue = new Chart(ctx, {
          type: "line",
          data: {
            labels,
            datasets: [
              {
                label: "Doanh thu",
                data: values,
                borderColor: "#eb2f96",
                backgroundColor: grad,
                borderWidth: 2.5,
                tension: 0.35,
                fill: true,
                pointRadius: 3,
                pointHoverRadius: 6,
                pointBackgroundColor: "#fff",
                pointBorderColor: "#eb2f96",
                pointBorderWidth: 2,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
              legend: { display: false },
              tooltip: {
                backgroundColor: "#111",
                padding: 12,
                titleFont: { size: 12, weight: "600" },
                bodyFont: { size: 13, weight: "700" },
                callbacks: {
                  label: (ctx) => "  " + formatMoney(ctx.parsed.y),
                },
              },
            },
            scales: {
              x: { grid: { display: false }, ticks: { font: { size: 11 } } },
              y: {
                beginAtZero: true,
                grid: { color: "rgba(0,0,0,0.04)" },
                ticks: {
                  font: { size: 11 },
                  callback: (v) =>
                    v >= 1_000_000
                      ? (v / 1_000_000).toFixed(1) + "tr"
                      : v >= 1000
                        ? v / 1000 + "k"
                        : v,
                },
              },
            },
          },
        });
      }

      function renderStatusDoughnut() {
        if (typeof Chart === "undefined" || !window._statsCache) return;
        const ctx = document.getElementById("statusDoughnut");
        if (!ctx) return;
        const { statusCount, statusLabels, statusColors, normalizedOrders } =
          window._statsCache;
        const total = normalizedOrders.length;
        const labels = [],
          data = [],
          colors = [];
        for (let i = 0; i <= 4; i++) {
          labels.push(statusLabels[i]);
          data.push(statusCount[i] || 0);
          colors.push(statusColors[i]);
        }
        if (_charts.status) _charts.status.destroy();
        _charts.status = new Chart(ctx, {
          type: "doughnut",
          data: {
            labels,
            datasets: [
              {
                data,
                backgroundColor: colors,
                borderWidth: 3,
                borderColor: "#fff",
                hoverOffset: 8,
              },
            ],
          },
          options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: "62%",
            plugins: {
              legend: { display: false },
              tooltip: {
                callbacks: {
                  label: (ctx) => {
                    const pct = total
                      ? Math.round((ctx.parsed / total) * 100)
                      : 0;
                    return `  ${ctx.label}: ${ctx.parsed} đơn (${pct}%)`;
                  },
                },
              },
            },
          },
        });

        // Custom legend với phần trăm
        const legend = document.getElementById("statusLegend");
        if (legend) {
          legend.innerHTML = labels
            .map((lb, i) => {
              const pct = total ? Math.round((data[i] / total) * 100) : 0;
              return `<div class="d-flex justify-content-between align-items-center mb-2">
                <span class="d-inline-flex align-items-center gap-2">
                  <span style="width:10px;height:10px;border-radius:50%;background:${colors[i]};display:inline-block"></span>
                  <span style="color:#374151">${lb}</span>
                </span>
                <span class="fw-bold" style="color:${colors[i]}">${data[i]} <span style="color:#9ca3af;font-weight:500">(${pct}%)</span></span>
              </div>`;
            })
            .join("");
        }
      }

      function renderTopProductsChart() {
        if (typeof Chart === "undefined" || !window._statsCache) return;
        const ctx = document.getElementById("topProductsChart");
        if (!ctx) return;
        const top = window._statsCache.topProducts || [];
        const labels = top.map(({ product }) => {
          const n = product.name || product.Name || "--";
          return n.length > 28 ? n.substring(0, 28) + "…" : n;
        });
        const data = top.map(({ totalSold }) => totalSold);
        const colors = [
          "#eb2f96",
          "#f759ab",
          "#ff85bf",
          "#ffadd2",
          "#ffd6e7",
        ].slice(0, top.length);

        if (_charts.top) _charts.top.destroy();
        _charts.top = new Chart(ctx, {
          type: "bar",
          data: {
            labels,
            datasets: [
              {
                label: "Số lượng bán",
                data,
                backgroundColor: colors,
                borderRadius: 8,
                barThickness: 24,
              },
            ],
          },
          options: {
            indexAxis: "y",
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
              legend: { display: false },
              tooltip: {
                callbacks: {
                  label: (ctx) => "  " + ctx.parsed.x + " sản phẩm đã bán",
                },
              },
            },
            scales: {
              x: {
                beginAtZero: true,
                grid: { color: "rgba(0,0,0,0.04)" },
                ticks: { stepSize: 1, font: { size: 11 } },
              },
              y: {
                grid: { display: false },
                ticks: { font: { size: 12, weight: "600" } },
              },
            },
          },
        });
      }

      // ======================= CATEGORIES =======================
      //let allCategories = [];
      let filteredCategories = [];
      let categoryPage = 1;
      const CATEGORY_PAGE_SIZE = 10;

      async function loadCategories() {
        const tbody = document.getElementById("categoryTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="4" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          // pageSize lớn để chắc chắn lấy hết danh mục (tránh bị backend phân trang)
          let res;
          try {
            res = await apiFetch(
              PRODUCT_API,
              "/api/categories?page=1&pageSize=1000",
            );
          } catch (_) {
            res = await apiFetch(PRODUCT_API, "/api/categories");
          }
          allCategories = Array.isArray(res)
            ? res
            : res?.items || res?.data || [];
          allCategories = sortAsc(allCategories);
          filteredCategories = [...allCategories];
          categoryPage = 1;
          renderCategoryPage();
          // Cập nhật luôn dropdown filter của trang Sản phẩm để đồng bộ
          if (typeof loadCategoriesForProduct === "function") {
            // dùng cache đã fetch — không cần fetch lại
            const sel = document.getElementById("productCategoryFilter");
            if (sel) {
              const current = sel.value;
              sel.innerHTML = '<option value="">📂 Tất cả danh mục</option>';
              allCategories.forEach((cat) => {
                const id = cat.id || cat.Id;
                const name = cat.name || cat.Name || "(không tên)";
                const opt = document.createElement("option");
                opt.value = id;
                opt.textContent = name;
                sel.appendChild(opt);
              });
              if (current && [...sel.options].some((o) => o.value === current))
                sel.value = current;
            }
          }
          //showToast(`Đã tải ${allCategories.length} danh mục`, "success");
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(4, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải danh mục: " + err.message, "error");
        }
      }

      function renderCategoryPage() {
        const start = (categoryPage - 1) * CATEGORY_PAGE_SIZE;
        const pageItems = filteredCategories.slice(
          start,
          start + CATEGORY_PAGE_SIZE,
        );
        const total = filteredCategories.length;
        document.getElementById("categoryCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + CATEGORY_PAGE_SIZE, total)} / ${total} danh mục`;
        const tbody = document.getElementById("categoryTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(4, "Không có danh mục nào");
          return;
        }
        let html = "";
        for (let c of pageItems) {
          html += `
      <tr>
        <td class="text-center">${c.id}</td>
        <td class="fw-semibold">${escapeHtml(c.name)}</td>
        <td>${escapeHtml(c.description || "--")}</td>
        <td>
          <div class="d-flex gap-1">
            <button class="btn-icon edit" onclick="openEditCategoryModal(${c.id})">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
            </button>
            <button class="btn-icon delete" onclick="deleteCategory(${c.id}, '${escapeHtml(c.name)}')">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
            </button>
          </div>
        </td>
      </tr>
    `;
        }
        tbody.innerHTML = html;
        renderPagination(
          "categoryPagination",
          total,
          CATEGORY_PAGE_SIZE,
          categoryPage,
          "goCategoryPage",
        );
      }

      function goCategoryPage(page) {
        categoryPage = page;
        renderCategoryPage();
      }

      function filterCategories() {
        const keyword =
          document
            .getElementById("categorySearch")
            ?.value.toLowerCase()
            .trim() || "";
        filteredCategories = allCategories.filter(
          (c) =>
            c.name.toLowerCase().includes(keyword) ||
            (c.description || "").toLowerCase().includes(keyword),
        );
        categoryPage = 1;
        renderCategoryPage();
      }

      function openCategoryModal() {
        document.getElementById("categoryModalTitle").innerText =
          "Thêm danh mục";
        document.getElementById("categoryId").value = "";
        document.getElementById("categoryName").value = "";
        document.getElementById("categoryDescription").value = "";
        openModal("categoryModal");
      }

      function openEditCategoryModal(id) {
        const cat = allCategories.find((c) => c.id === id);
        if (!cat) return;
        document.getElementById("categoryModalTitle").innerText =
          "Sửa danh mục";
        document.getElementById("categoryId").value = cat.id;
        document.getElementById("categoryName").value = cat.name;
        document.getElementById("categoryDescription").value =
          cat.description || "";
        openModal("categoryModal");
      }

      async function saveCategory() {
        const id = document.getElementById("categoryId").value;
        const name = document.getElementById("categoryName").value.trim();
        const description = document
          .getElementById("categoryDescription")
          .value.trim();

        if (!name) {
          showToast("Tên danh mục không được để trống", "error");
          return;
        }

        const data = { name, description };
        const btn = document.querySelector("#categoryModal .btn-primary-admin");
        btn.disabled = true;
        btn.innerText = "Đang lưu...";

        try {
          if (id) {
            await apiFetch(PRODUCT_API, `/api/categories/${id}`, "PUT", data);
            showToast("Cập nhật thành công", "success");
          } else {
            await apiFetch(PRODUCT_API, "/api/categories", "POST", data);
            showToast("Thêm danh mục thành công", "success");
          }
          closeModal("categoryModal");
          await loadCategories(); // reload danh sách
          // Cập nhật lại dropdown danh mục trong trang sản phẩm (nếu có)
          if (typeof loadCategoriesForProduct === "function")
            await loadCategoriesForProduct();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        } finally {
          btn.disabled = false;
          btn.innerText = "Lưu";
        }
      }

      async function deleteCategory(id, name) {
        // Đếm số sản phẩm thuộc danh mục này — cảnh báo trước khi xóa
        let productCount = 0;
        try {
          let list = allProducts;
          if (!list || !list.length) {
            const res = await apiFetch(
              PRODUCT_API,
              "/api/products?page=1&pageSize=1000",
            );
            list = Array.isArray(res) ? res : res?.items || [];
          }
          productCount = list.filter(
            (p) => (p.categoryId ?? p.CategoryId) == id,
          ).length;
        } catch (_) {
          // Nếu không đếm được, vẫn cho confirm cơ bản
        }

        const msg =
          productCount > 0
            ? `Bạn có chắc muốn xóa danh mục "${name}"?\n\n⚠️ CẢNH BÁO: Có ${productCount} sản phẩm đang thuộc danh mục này. Sau khi xóa, các sản phẩm đó sẽ bị mất danh mục (orphan).\n\nVẫn tiếp tục xóa?`
            : `Bạn có chắc muốn xóa danh mục "${name}"?`;

        if (!confirm(msg)) return;
        try {
          await apiFetch(PRODUCT_API, `/api/categories/${id}`, "DELETE");
          showToast(`Đã xóa danh mục "${name}"`, "success");
          await loadCategories();
          if (typeof loadCategoriesForProduct === "function")
            await loadCategoriesForProduct();
        } catch (err) {
          showToast("Lỗi xóa: " + err.message, "error");
        }
      }
      // ======================= ORDER STATUS LOGS =======================
      let allOrderLogs = [];
      let filteredOrderLogs = [];
      let orderLogPage = 1;
      const ORDER_LOG_PAGE_SIZE = 15;

      async function loadOrderLogs() {
        const tbody = document.getElementById("orderLogTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(
            ORDER_API,
            "/api/orderstatuslogs?page=1&pageSize=500",
          );
          allOrderLogs = Array.isArray(res) ? res : res?.items || [];
          allOrderLogs = sortAsc(allOrderLogs);
          filteredOrderLogs = [...allOrderLogs];
          orderLogPage = 1;
          renderOrderLogs();
          //showToast(`Đã tải ${allOrderLogs.length} log`, "success");
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải log: " + err.message, "error");
        }
      }

      function filterOrderLogs() {
        const orderIdFilter = parseInt(
          document.getElementById("orderLogFilter")?.value,
        );
        if (!orderIdFilter || isNaN(orderIdFilter)) {
          filteredOrderLogs = [...allOrderLogs];
        } else {
          filteredOrderLogs = allOrderLogs.filter(
            (l) => l.orderId === orderIdFilter,
          );
        }
        orderLogPage = 1;
        renderOrderLogs();
      }

      function renderOrderLogs() {
        const start = (orderLogPage - 1) * ORDER_LOG_PAGE_SIZE;
        const pageItems = filteredOrderLogs.slice(
          start,
          start + ORDER_LOG_PAGE_SIZE,
        );
        const total = filteredOrderLogs.length;
        document.getElementById("orderLogCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + ORDER_LOG_PAGE_SIZE, total)} / ${total} log`;
        const tbody = document.getElementById("orderLogTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Chưa có log nào");
          return;
        }
        const fmtDt = (d) => {
          if (!d) return "--";
          const dt = new Date(d);
          return dt.toLocaleString("vi-VN");
        };
        let html = "";
        for (let l of pageItems) {
          html += `<tr>
            <td class="text-center">${l.id}</td>
            <td class="text-center"><span class="fw-bold" style="color:var(--pink)">#${l.orderId}</span></td>
            <td>${l.oldStatus ? getOrderStatusBadge(l.oldStatus) : '<span style="color:#9ca3af">—</span>'}</td>
            <td>${getOrderStatusBadge(l.newStatus)}</td>
            <td>${escapeHtml(l.changedByName || "(Admin)")}</td>
            <td style="max-width: 280px; word-break: break-word">${escapeHtml(l.note || "--")}</td>
            <td class="text-center" style="font-size: 12px; color: #6b7280">${fmtDt(l.createdAt)}</td>
          </tr>`;
        }
        tbody.innerHTML = html;
        renderPagination(
          "orderLogPagination",
          total,
          ORDER_LOG_PAGE_SIZE,
          orderLogPage,
          "goOrderLogPage",
        );
      }

      function goOrderLogPage(p) {
        orderLogPage = p;
        renderOrderLogs();
      }

      /** Xem lịch sử của 1 đơn cụ thể (gọi từ chi tiết đơn) */
      async function viewOrderLogs(orderId) {
        document.getElementById("orderLogsModalOrderId").textContent =
          "#" + orderId;
        const body = document.getElementById("orderLogsModalBody");
        body.innerHTML = `<div class="text-center py-3"><div class="spinner mx-auto"></div></div>`;
        openModal("orderLogsModal");
        try {
          const logs = await apiFetch(
            ORDER_API,
            `/api/orderstatuslogs/by-order/${orderId}`,
          );
          const arr = Array.isArray(logs) ? logs : [];
          if (arr.length === 0) {
            body.innerHTML = `<div class="text-center py-4 text-muted">Chưa có lịch sử thay đổi cho đơn này.</div>`;
            return;
          }
          const fmtDt = (d) => new Date(d).toLocaleString("vi-VN");
          body.innerHTML = `
            <div style="position: relative; padding-left: 28px">
              <div style="position: absolute; left: 8px; top: 8px; bottom: 8px; width: 2px; background: #fbcfe8"></div>
              ${arr
                .map(
                  (l) => `
                <div style="position: relative; margin-bottom: 18px">
                  <div style="position: absolute; left: -24px; top: 4px; width: 14px; height: 14px; border-radius: 50%; background: var(--pink); border: 3px solid #fff; box-shadow: 0 0 0 2px var(--pink)"></div>
                  <div style="font-size: 12px; color: #6b7280; margin-bottom: 4px">${fmtDt(l.createdAt)} · ${escapeHtml(l.changedByName || "(Admin)")}</div>
                  <div style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap">
                    ${l.oldStatus ? getOrderStatusBadge(l.oldStatus) + '<span style="color:#9ca3af">→</span>' : ""}
                    ${getOrderStatusBadge(l.newStatus)}
                  </div>
                  ${l.note ? `<div style="margin-top: 6px; font-size: 13px; color: #374151; padding: 8px 12px; background: #fdf2f8; border-radius: 6px">💬 ${escapeHtml(l.note)}</div>` : ""}
                </div>
              `,
                )
                .join("")}
            </div>`;
        } catch (err) {
          body.innerHTML = `<div class="text-center py-4" style="color:#dc2626">❌ ${err.message}</div>`;
        }
      }

      // ======================= VOUCHERS =======================
      let allVouchers = [];
      let filteredVouchers = [];
      let voucherPage = 1;
      const VOUCHER_PAGE_SIZE = 10;

      async function loadVouchers() {
        const tbody = document.getElementById("voucherTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="10" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(PRODUCT_API, "/api/vouchers");
          allVouchers = Array.isArray(res) ? res : res?.items || [];
          allVouchers = sortAsc(allVouchers);
          filteredVouchers = [...allVouchers];
          voucherPage = 1;
          renderVouchers();
          //showToast(`Đã tải ${allVouchers.length} voucher`, "success");
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(10, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải voucher: " + err.message, "error");
        }
      }

      function filterVouchers() {
        const keyword =
          document
            .getElementById("voucherSearch")
            ?.value.toLowerCase()
            .trim() || "";
        const status =
          document.getElementById("voucherStatusFilter")?.value || "";
        const now = new Date();
        filteredVouchers = allVouchers.filter((v) => {
          if (keyword) {
            const code = (v.code || "").toLowerCase();
            const desc = (v.description || "").toLowerCase();
            if (!code.includes(keyword) && !desc.includes(keyword))
              return false;
          }
          // Thứ tự ưu tiên trạng thái (khớp với badge trong renderVouchers):
          //   !isActive  → "Đã tắt"      (chiếm trước, bất kể hết hạn hay chưa)
          //   expired    → "Hết hạn"     (chỉ áp dụng nếu vẫn đang bật)
          //   notStarted → "Chưa bắt đầu"
          //   else       → "Còn hiệu lực"
          if (status === "active") {
            if (!v.isActive) return false;
            if (v.expiryDate && new Date(v.expiryDate) < now) return false;
            if (v.startDate && new Date(v.startDate) > now) return false;
          } else if (status === "expired") {
            // Voucher bị tắt thì thuộc nhóm "Đã tắt", không phải "Hết hạn"
            if (!v.isActive) return false;
            if (!v.expiryDate || new Date(v.expiryDate) >= now) return false;
          } else if (status === "notstarted") {
            // Voucher đặt lịch tương lai — phải đang bật + chưa hết hạn + chưa tới ngày bắt đầu
            if (!v.isActive) return false;
            if (v.expiryDate && new Date(v.expiryDate) < now) return false;
            if (!v.startDate || new Date(v.startDate) <= now) return false;
          } else if (status === "inactive") {
            if (v.isActive) return false;
          }
          return true;
        });
        voucherPage = 1;
        renderVouchers();
      }

      function renderVouchers() {
        const start = (voucherPage - 1) * VOUCHER_PAGE_SIZE;
        const pageItems = filteredVouchers.slice(
          start,
          start + VOUCHER_PAGE_SIZE,
        );
        const total = filteredVouchers.length;
        document.getElementById("voucherCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + VOUCHER_PAGE_SIZE, total)} / ${total} voucher`;
        const tbody = document.getElementById("voucherTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(10, "Chưa có voucher nào");
          return;
        }
        const fmtDate = (d) =>
          d ? new Date(d).toLocaleDateString("vi-VN") : "—";
        const now = new Date();
        let html = "";
        for (let v of pageItems) {
          const expired = v.expiryDate && new Date(v.expiryDate) < now;
          const notStarted = v.startDate && new Date(v.startDate) > now;
          const statusBadge = !v.isActive
            ? '<span class="badge-status badge-cancelled">Đã tắt</span>'
            : expired
              ? '<span class="badge-status badge-cancelled">Hết hạn</span>'
              : notStarted
                ? '<span class="badge-status badge-pending">Chưa bắt đầu</span>'
                : '<span class="badge-status badge-delivered">Còn hiệu lực</span>';
          const discountText =
            v.discountType === "percent"
              ? v.discountValue + "%"
              : formatMoney(v.discountValue);
          html += `<tr>
            <td class="text-center">${v.id}</td>
            <td><span style="font-family: monospace; font-weight: 700; color: var(--pink); background: #fdf2f8; padding: 3px 10px; border-radius: 4px">${escapeHtml(v.code)}</span></td>
            <td style="max-width: 240px">${escapeHtml(v.description || "—")}</td>
            <td class="text-center">${v.discountType === "percent" ? "%" : "₫"}</td>
            <td class="text-end fw-bold" style="color: var(--pink)">${discountText}${v.maxDiscount ? `<br><small style="font-weight:400;color:#9ca3af">max ${formatMoney(v.maxDiscount)}</small>` : ""}</td>
            <td class="text-end">${formatMoney(v.minOrderAmount || 0)}</td>
            <td class="text-center">${v.usedCount || 0}${v.usageLimit ? ` / ${v.usageLimit}` : " / ∞"}</td>
            <td class="text-center" style="font-size: 12px">${fmtDate(v.startDate)}<br>→ ${fmtDate(v.expiryDate)}</td>
            <td class="text-center">${statusBadge}</td>
            <td class="text-center">
              <button type="button" class="btn-icon edit" onclick="editVoucher(${v.id})" title="Sửa">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              </button>
              <button type="button" class="btn-icon delete" onclick="deleteVoucher(${v.id}, '${escapeHtml(v.code)}')" title="Xóa">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
              </button>
            </td>
          </tr>`;
        }
        tbody.innerHTML = html;
        renderPagination(
          "voucherPagination",
          total,
          VOUCHER_PAGE_SIZE,
          voucherPage,
          "goVoucherPage",
        );
      }

      function goVoucherPage(p) {
        voucherPage = p;
        renderVouchers();
      }

      function onDiscountTypeChange() {
        const t = document.getElementById("voucherDiscountType").value;
        document.getElementById("voucherDiscountLabel").textContent =
          t === "percent" ? "Giá trị (%)" : "Giá trị (₫)";
        // maxDiscount chỉ có nghĩa với % — disable + clear khi chọn fixed
        const maxField = document.getElementById("voucherMaxDiscount");
        if (maxField) {
          if (t === "fixed") {
            maxField.value = "";
            maxField.disabled = true;
            maxField.placeholder = "Không áp dụng cho loại ₫";
          } else {
            maxField.disabled = false;
            maxField.placeholder = "Để trống nếu không giới hạn";
          }
        }
      }

      function openVoucherModal() {
        document.getElementById("voucherModalTitle").textContent =
          "Thêm voucher";
        document.getElementById("voucherId").value = "";
        document.getElementById("voucherCode").value = "";
        document.getElementById("voucherDiscountType").value = "percent";
        document.getElementById("voucherDiscountValue").value = "";
        document.getElementById("voucherMaxDiscount").value = "";
        document.getElementById("voucherMinOrder").value = "0";
        document.getElementById("voucherUsageLimit").value = "";
        document.getElementById("voucherStartDate").value = "";
        document.getElementById("voucherExpiryDate").value = "";
        document.getElementById("voucherDescription").value = "";
        document.getElementById("voucherIsActive").checked = true;
        onDiscountTypeChange();
        openModal("voucherModal");
      }

      function editVoucher(id) {
        const v = allVouchers.find((x) => x.id === id);
        if (!v) return;
        document.getElementById("voucherModalTitle").textContent =
          "Sửa voucher #" + id;
        document.getElementById("voucherId").value = v.id;
        document.getElementById("voucherCode").value = v.code || "";
        document.getElementById("voucherDiscountType").value =
          v.discountType || "percent";
        document.getElementById("voucherDiscountValue").value =
          v.discountValue ?? "";
        document.getElementById("voucherMaxDiscount").value =
          v.maxDiscount ?? "";
        document.getElementById("voucherMinOrder").value =
          v.minOrderAmount ?? 0;
        document.getElementById("voucherUsageLimit").value = v.usageLimit ?? "";
        document.getElementById("voucherStartDate").value = v.startDate
          ? v.startDate.split("T")[0]
          : "";
        document.getElementById("voucherExpiryDate").value = v.expiryDate
          ? v.expiryDate.split("T")[0]
          : "";
        document.getElementById("voucherDescription").value =
          v.description || "";
        document.getElementById("voucherIsActive").checked =
          v.isActive !== false;
        onDiscountTypeChange();
        openModal("voucherModal");
      }

      async function saveVoucher() {
        const id = document.getElementById("voucherId").value;
        const code = document
          .getElementById("voucherCode")
          .value.trim()
          .toUpperCase();
        const discountType = document.getElementById(
          "voucherDiscountType",
        ).value;
        const discountValue = parseFloat(
          document.getElementById("voucherDiscountValue").value,
        );
        const maxDiscountRaw =
          document.getElementById("voucherMaxDiscount").value;
        const minOrder =
          parseFloat(document.getElementById("voucherMinOrder").value) || 0;
        const usageLimitRaw =
          document.getElementById("voucherUsageLimit").value;
        const startDate = document.getElementById("voucherStartDate").value;
        const expiryDate = document.getElementById("voucherExpiryDate").value;
        const description = document
          .getElementById("voucherDescription")
          .value.trim();
        const isActive = document.getElementById("voucherIsActive").checked;

        if (!code) return showToast("Vui lòng nhập mã code", "error");
        if (isNaN(discountValue) || discountValue <= 0)
          return showToast("Giá trị giảm phải > 0", "error");
        if (discountType === "percent" && discountValue > 100)
          return showToast("% không thể > 100", "error");
        if (startDate && expiryDate) {
          const sd = new Date(startDate);
          const ed = new Date(expiryDate);
          if (!isNaN(sd) && !isNaN(ed) && sd > ed) {
            return showToast(
              "Ngày bắt đầu phải trước ngày hết hạn",
              "error",
            );
          }
        }
        // Khi sửa voucher: usageLimit không được nhỏ hơn số lượt đã dùng
        if (id && usageLimitRaw) {
          const limit = parseInt(usageLimitRaw);
          const existing = allVouchers.find((x) => x.id == id);
          const used = existing?.usedCount || 0;
          if (limit < used) {
            return showToast(
              `Giới hạn lượt dùng (${limit}) không thể nhỏ hơn số lượt đã dùng (${used})`,
              "error",
            );
          }
        }

        const payload = {
          code,
          description,
          discountType,
          discountValue,
          // maxDiscount chỉ áp dụng cho loại % — bỏ qua khi fixed
          maxDiscount:
            discountType === "percent" && maxDiscountRaw
              ? parseFloat(maxDiscountRaw)
              : null,
          minOrderAmount: minOrder,
          usageLimit: usageLimitRaw ? parseInt(usageLimitRaw) : null,
          startDate: startDate ? new Date(startDate).toISOString() : null,
          expiryDate: expiryDate ? new Date(expiryDate).toISOString() : null,
          isActive,
        };

        const btn = document.getElementById("saveVoucherBtn");
        btn.disabled = true;
        btn.textContent = "Đang lưu...";
        try {
          if (id) {
            await apiFetch(PRODUCT_API, `/api/vouchers/${id}`, "PUT", payload);
            showToast("Đã cập nhật voucher", "success");
          } else {
            await apiFetch(PRODUCT_API, "/api/vouchers", "POST", payload);
            showToast("Đã thêm voucher mới", "success");
          }
          closeModal("voucherModal");
          await loadVouchers();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        } finally {
          btn.disabled = false;
          btn.textContent = "Lưu voucher";
        }
      }

      async function deleteVoucher(id, code) {
        if (!confirm(`Xóa voucher "${code}"? Hành động không thể hoàn tác.`))
          return;
        try {
          await apiFetch(PRODUCT_API, `/api/vouchers/${id}`, "DELETE");
          showToast("Đã xóa voucher", "success");
          await loadVouchers();
        } catch (err) {
          showToast("Lỗi xóa: " + err.message, "error");
        }
      }

      // ======================= SITE SETTINGS =======================
      let allSettings = [];

      // Cấu hình mặc định nếu DB chưa có key nào — dùng làm template
      const DEFAULT_SETTINGS = [
        {
          group: "general",
          key: "site_name",
          label: "Tên cửa hàng",
          type: "text",
          defaultValue: "GlowHub",
        },
        {
          group: "general",
          key: "site_email",
          label: "Email liên hệ",
          type: "text",
          defaultValue: "contact@glowhub.vn",
        },
        {
          group: "general",
          key: "site_phone",
          label: "Số điện thoại",
          type: "text",
          defaultValue: "0901234567",
        },
        {
          group: "general",
          key: "site_address",
          label: "Địa chỉ cửa hàng",
          type: "text",
          defaultValue: "123 Lê Lợi, Quận 1, TP.HCM",
        },
        {
          group: "general",
          key: "site_logo",
          label: "URL Logo",
          type: "image",
          defaultValue: "",
        },
        {
          group: "general",
          key: "site_facebook",
          label: "Facebook URL",
          type: "text",
          defaultValue: "",
        },
        {
          group: "general",
          key: "site_instagram",
          label: "Instagram URL",
          type: "text",
          defaultValue: "",
        },
        {
          group: "homepage",
          key: "topbar_text",
          label: "Thông báo trên cùng",
          type: "text",
          defaultValue: "Miễn phí vận chuyển đơn từ 500.000₫",
        },
        {
          group: "homepage",
          key: "hero_title",
          label: "Tiêu đề hero",
          type: "text",
          defaultValue: "Bộ Sưu Tập Mới",
        },
        {
          group: "homepage",
          key: "hero_subtitle",
          label: "Mô tả hero",
          type: "text",
          defaultValue: "Khám phá xu hướng làm đẹp mới nhất",
        },
        {
          group: "homepage",
          key: "primary_color",
          label: "Màu chủ đạo",
          type: "color",
          defaultValue: "#f759ab",
        },
        {
          group: "homepage",
          key: "footer_text",
          label: "Nội dung Footer",
          type: "text",
          defaultValue: "© 2026 GlowHub. Mỹ phẩm chính hãng.",
        },
      ];

      async function loadSettings() {
        const container = document.getElementById("settingsContent");
        container.innerHTML = `<div class="text-center py-3"><div class="spinner mx-auto"></div></div>`;
        try {
          // Lấy tất cả setting hiện có trong DB
          const res = await apiFetch(
            PRODUCT_API,
            "/api/sitesettings/detail",
          ).catch(() => null);
          const fromDb = Array.isArray(res) ? res : res?.items || [];
          // Merge với template — key trùng dùng giá trị DB, key thiếu dùng default
          const merged = DEFAULT_SETTINGS.map((tpl) => {
            const found = fromDb.find((s) => (s.key || s.Key) === tpl.key);
            return {
              ...tpl,
              value: found
                ? (found.value ?? found.Value ?? tpl.defaultValue)
                : tpl.defaultValue,
              fromDb: !!found,
            };
          });
          // Thêm các key không có trong template nhưng có trong DB
          fromDb.forEach((s) => {
            const key = s.key || s.Key;
            if (!DEFAULT_SETTINGS.find((t) => t.key === key)) {
              merged.push({
                group: s.group || s.Group || "other",
                key,
                label: s.label || s.Label || key,
                type: s.type || s.Type || "text",
                value: s.value ?? s.Value ?? "",
                fromDb: true,
              });
            }
          });
          allSettings = merged;
          renderSettings();
        } catch (err) {
          container.innerHTML = `<div class="text-center py-4" style="color:#dc2626">❌ ${err.message}</div>`;
          showToast("Lỗi tải cấu hình: " + err.message, "error");
        }
      }

      function renderSettings() {
        const container = document.getElementById("settingsContent");
        const grouped = {};
        allSettings.forEach((s) => {
          const g = s.group || "general";
          if (!grouped[g]) grouped[g] = [];
          grouped[g].push(s);
        });
        const groupLabels = {
          general: "🏪 Thông tin cửa hàng",
          homepage: "🎨 Giao diện trang chủ",
          seo: "🔍 SEO",
          social: "📱 Mạng xã hội",
          other: "⚙️ Khác",
        };
        let html = "";
        for (const g of Object.keys(grouped)) {
          html += `<div style="margin-bottom: 28px">
            <h5 style="font-size: 14px; font-weight: 700; color: var(--pink); margin-bottom: 14px; padding-bottom: 8px; border-bottom: 2px solid #fce7f3">${groupLabels[g] || g}</h5>
            <div class="row g-3">`;
          for (const s of grouped[g]) {
            html += renderSettingField(s);
          }
          html += `</div></div>`;
        }
        container.innerHTML = html;
      }

      function renderSettingField(s) {
        const id = "setting-" + s.key;
        let input;
        if (s.type === "color") {
          input = `<div style="display:flex;gap:8px;align-items:center">
            <input type="color" id="${id}" value="${s.value || "#f759ab"}" style="width:60px;height:40px;border:1px solid #e5e7eb;border-radius:6px;cursor:pointer" />
            <input type="text" class="form-control-admin" value="${escapeHtml(s.value || "")}" oninput="document.getElementById('${id}').value=this.value" style="flex:1" />
          </div>`;
        } else if (s.type === "bool") {
          input = `<label style="display:flex;align-items:center;gap:8px;cursor:pointer">
            <input type="checkbox" id="${id}" ${s.value === "true" || s.value === true ? "checked" : ""} />
            <span style="font-size:13px">Bật / Tắt</span>
          </label>`;
        } else if (s.type === "html" || s.type === "json") {
          input = `<textarea id="${id}" class="form-control-admin" rows="4">${escapeHtml(s.value || "")}</textarea>`;
        } else if (s.type === "image") {
          input = `<div>
            <input type="text" id="${id}" class="form-control-admin" value="${escapeHtml(s.value || "")}" placeholder="https://..." />
            ${s.value ? `<img src="${resolveImageUrl(s.value)}" style="margin-top:8px;max-height:60px;border-radius:6px" onerror="this.style.display='none'" />` : ""}
          </div>`;
        } else {
          input = `<input type="text" id="${id}" class="form-control-admin" value="${escapeHtml(s.value || "")}" />`;
        }
        const colSize =
          s.type === "html" || s.type === "json" ? "col-12" : "col-md-6";
        return `<div class="${colSize}">
          <div class="form-group-admin">
            <label class="form-label-admin" title="${s.key}">
              ${escapeHtml(s.label)} <small style="color:#9ca3af;font-weight:400">(${s.key})</small>
            </label>
            ${input}
          </div>
        </div>`;
      }

      async function saveAllSettings() {
        const updates = [];
        for (const s of allSettings) {
          const el = document.getElementById("setting-" + s.key);
          if (!el) continue;
          let value;
          if (s.type === "bool") value = el.checked ? "true" : "false";
          else value = el.value;
          updates.push({
            key: s.key,
            value,
            type: s.type,
            group: s.group,
            label: s.label,
          });
        }
        try {
          await apiFetch(PRODUCT_API, "/api/sitesettings/bulk", "PUT", updates);
          showToast("✓ Đã lưu " + updates.length + " cấu hình", "success");
        } catch (err) {
          showToast("Lỗi lưu: " + err.message, "error");
        }
      }

      // ======================= REVIEWS =======================
      let allReviews = [];
      let filteredReviews = [];
      let reviewPage = 1;
      const REVIEW_PAGE_SIZE = 10;

      async function loadReviews() {
        const tbody = document.getElementById("reviewTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          // Lấy danh sách reviews (giả sử backend có endpoint /api/reviews)
          const res = await apiFetch(PRODUCT_API, "/api/reviews");
          allReviews = Array.isArray(res) ? res : res?.items || [];

          // Map thêm tên sản phẩm và người dùng (nếu API chưa trả về đầy đủ, cần fetch thêm products và users)
          // Nếu API trả về productId và userId, bạn có thể join với allProducts và allCustomers
          // Dưới đây giả sử API trả sẵn productName và userName
          allReviews = sortAsc(allReviews);
          filteredReviews = [...allReviews];
          reviewPage = 1;
          renderReviewPage();
          //showToast(`Đã tải ${allReviews.length} đánh giá`, "success");
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải đánh giá: " + err.message, "error");
        }
      }

      function renderReviewPage() {
        const start = (reviewPage - 1) * REVIEW_PAGE_SIZE;
        const pageItems = filteredReviews.slice(
          start,
          start + REVIEW_PAGE_SIZE,
        );
        const total = filteredReviews.length;
        document.getElementById("reviewCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + REVIEW_PAGE_SIZE, total)} / ${total} đánh giá`;
        const tbody = document.getElementById("reviewTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Không có đánh giá nào");
          return;
        }
        let html = "";
        for (let r of pageItems) {
          const id = r.id;
          const productName =
            r.productName || r.product?.name || `SP#${r.productId}`;
          const userName =
            r.userName ||
            r.user?.name ||
            r.user?.userName ||
            `User#${r.userId}`;
          const rating = r.rating;
          const comment = r.comment || "";
          const createdAt = formatDate(r.createdAt);
          // Tạo 5 sao hiển thị dạng icon hoặc chữ
          const stars = "★".repeat(rating) + "☆".repeat(5 - rating);
          html += `
      <tr>
        <td class="text-center">${id}</td>
        <td class="fw-semibold">${escapeHtml(productName)}</td>
        <td>${escapeHtml(userName)}</td>
        <td class="text-center"><span style="color:#f5b042;">${stars}</span> (${rating})</td>
        <td style="max-width: 300px; white-space: normal; word-break: break-word;">${escapeHtml(comment)}</td>
        <td class="text-center">${createdAt}</td>
        <td class="text-center">
          <button class="btn-icon delete" onclick="deleteReview(${id})" title="Xóa đánh giá">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
          </button>
        </td>
      </tr>
    `;
        }
        tbody.innerHTML = html;
        renderPagination(
          "reviewPagination",
          total,
          REVIEW_PAGE_SIZE,
          reviewPage,
          "goReviewPage",
        );
      }

      function goReviewPage(page) {
        reviewPage = page;
        renderReviewPage();
      }

      function filterReviews() {
        const keyword =
          document.getElementById("reviewSearch")?.value.toLowerCase().trim() ||
          "";
        const rating = document.getElementById("ratingFilter")?.value;
        filteredReviews = allReviews.filter((r) => {
          const matchKeyword =
            (r.comment || "").toLowerCase().includes(keyword) ||
            (r.productName || "").toLowerCase().includes(keyword) ||
            (r.userName || "").toLowerCase().includes(keyword);
          const matchRating = !rating || r.rating == rating;
          return matchKeyword && matchRating;
        });
        reviewPage = 1;
        renderReviewPage();
      }

      async function deleteReview(id) {
        if (
          confirm(
            "Bạn có chắc muốn xóa đánh giá này? Hành động không thể hoàn tác.",
          )
        ) {
          try {
            await apiFetch(PRODUCT_API, `/api/reviews/${id}`, "DELETE");
            showToast("Đã xóa đánh giá", "success");
            await loadReviews(); // reload danh sách
          } catch (err) {
            showToast("Lỗi xóa: " + err.message, "error");
          }
        }
      }
      // ======================= CARTS =======================
      let allCarts = [];
      let filteredCarts = [];
      let cartPage = 1;
      const CART_PAGE_SIZE = 5;

      async function loadCarts() {
        const tbody = document.getElementById("cartTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          // Lấy songsong: cart + products + categories (để enrich danh mục)
          const [cartRes, productsRes, categoriesRes] = await Promise.all([
            apiFetch(PRODUCT_API, "/api/cart/all"),
            apiFetch(PRODUCT_API, "/api/products?page=1&pageSize=1000").catch(
              () => null,
            ),
            apiFetch(PRODUCT_API, "/api/categories?page=1&pageSize=1000").catch(
              () => apiFetch(PRODUCT_API, "/api/categories").catch(() => null),
            ),
          ]);

          allCarts = Array.isArray(cartRes) ? cartRes : [];
          // Cart không có ID số → sắp theo tên người dùng (A→Z) cho dễ tra cứu
          allCarts = sortAsc(allCarts, "userName");

          // Cache products + categories (đè lên global nếu rỗng, không đè nếu đã có lớn hơn)
          const prodList = productsRes
            ? Array.isArray(productsRes)
              ? productsRes
              : productsRes?.items || productsRes?.data || []
            : [];
          const catList = categoriesRes
            ? Array.isArray(categoriesRes)
              ? categoriesRes
              : categoriesRes?.items || categoriesRes?.data || []
            : [];
          if (prodList.length) allProducts = sortAsc(prodList);
          if (catList.length) allCategories = sortAsc(catList);

          // Build map productId → { categoryId, categoryName }
          const catMap = new Map(
            allCategories.map((c) => [c.id || c.Id, c.name || c.Name]),
          );
          const prodCatMap = new Map();
          allProducts.forEach((p) => {
            const pid = p.id || p.Id;
            const cid = p.categoryId ?? p.CategoryId;
            prodCatMap.set(pid, {
              categoryId: cid,
              categoryName: catMap.get(cid) || "—",
            });
          });

          // Enrich từng item trong cart với categoryId + categoryName
          allCarts.forEach((cart) => {
            (cart.items || []).forEach((it) => {
              const pid = it.productId ?? it.ProductId;
              const info = prodCatMap.get(pid);
              it._categoryId = info?.categoryId ?? null;
              it._categoryName = info?.categoryName ?? "—";
            });
          });
          filteredCarts = [...allCarts];

          // Đổ dropdown lọc theo danh mục
          const catFilter = document.getElementById("cartCategoryFilter");
          if (catFilter) {
            const current = catFilter.value;
            catFilter.innerHTML =
              '<option value="">📂 Tất cả danh mục</option>';
            allCategories.forEach((c) => {
              const id = c.id || c.Id;
              const name = c.name || c.Name || "(không tên)";
              const opt = document.createElement("option");
              opt.value = id;
              opt.textContent = name;
              catFilter.appendChild(opt);
            });
            if (
              current &&
              [...catFilter.options].some((o) => o.value === current)
            )
              catFilter.value = current;
          }

          cartPage = 1;
          renderCarts();
          //showToast(`Đã tải ${allCarts.length} giỏ hàng`, "success");
        } catch (err) {
          console.error("loadCarts error:", err);
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải giỏ hàng: " + err.message, "error");
        }
      }

      function renderCarts() {
        const start = (cartPage - 1) * CART_PAGE_SIZE;
        const pageItems = filteredCarts.slice(start, start + CART_PAGE_SIZE);
        const total = filteredCarts.length;
        document.getElementById("cartCount").innerHTML =
          `Hiển thị ${Math.min(start + 1, total)}–${Math.min(start + CART_PAGE_SIZE, total)} / ${total} giỏ hàng`;
        const tbody = document.getElementById("cartTable");
        if (!tbody) return;
        if (pageItems.length === 0) {
          tbody.innerHTML = emptyRow(7, "Không có giỏ hàng nào");
          return;
        }
        let html = "";
        // Lấy filter danh mục hiện tại để cũng tô đậm/highlight các item khớp (tùy chọn)
        const activeCatId =
          document.getElementById("cartCategoryFilter")?.value || "";

        for (let cart of pageItems) {
          // _visibleItems được gán trong filterCarts() (chỉ những item khớp filter danh mục)
          // Nếu không có _visibleItems (chưa filter), hiển thị tất cả
          const items = cart._visibleItems || cart.items || [];
          // Tính lại tổng tiền của các item được hiện thị
          const subTotal = items.reduce(
            (s, it) => s + (it.subTotal ?? it.unitPrice * it.quantity ?? 0),
            0,
          );

          for (let item of items) {
            const catName = item._categoryName || "—";
            const isMatch =
              activeCatId && String(item._categoryId) === String(activeCatId);
            html += `
        <tr>
          <td class="fw-semibold">${escapeHtml(cart.userName)}</td>
          <td class="fw-semibold">${escapeHtml(item.productName)}</td>
          <td>
            <span style="background:${isMatch ? "var(--pink)" : "#f3f4f6"};color:${isMatch ? "#fff" : "#374151"};padding:3px 10px;border-radius:20px;font-size:12px;font-weight:${isMatch ? "600" : "500"}">
              ${escapeHtml(catName)}
            </span>
          </td>
          <td class="text-end">${formatMoney(item.unitPrice)}</td>
          <td class="text-center" style="width: 150px;">
            <div class="d-flex gap-1 justify-content-center align-items-center">
              <button type="button" class="btn-icon" onclick="updateCartQty(${item.id}, ${item.quantity - 1})">-</button>
              <input type="number" id="qty-${item.id}" value="${item.quantity}" min="0" style="width: 65px; text-align: center;" class="form-control-admin">
              <button type="button" class="btn-icon" onclick="updateCartQty(${item.id}, ${item.quantity + 1})">+</button>
            </div>
          </td>
          <td class="text-end fw-bold">${formatMoney(item.subTotal ?? item.unitPrice * item.quantity)}</td>
          <td class="text-center">
            <button type="button" class="btn-icon delete" onclick="removeCartItem(${item.id})" title="Xóa sản phẩm">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
            </button>
            <button type="button" class="btn-icon" onclick="clearUserCart('${cart.userId}')" title="Xóa toàn bộ giỏ">🗑️</button>
          </td>
        </tr>
      `;
          }
          if (items.length > 0) {
            const totalLabel = activeCatId
              ? `Tổng giỏ (đã lọc danh mục) của ${escapeHtml(cart.userName)}:`
              : `Tổng giỏ hàng của ${escapeHtml(cart.userName)}:`;
            html += `<tr style="background:#fdf2f8">
        <td colspan="5" class="text-end fw-bold">${totalLabel}</td>
        <td class="text-end fw-bold" style="color:var(--pink)">${formatMoney(subTotal)}</td>
        <td></td>
      </tr>`;
          }
        }
        tbody.innerHTML = html;
        renderPagination(
          "cartPagination",
          total,
          CART_PAGE_SIZE,
          cartPage,
          "goCartPage",
        );
      }

      function goCartPage(page) {
        if (page < 1 || page > Math.ceil(filteredCarts.length / CART_PAGE_SIZE))
          return;
        cartPage = page;
        renderCarts();
      }

      function filterCarts() {
        const keyword =
          document.getElementById("cartSearch")?.value.trim().toLowerCase() ||
          "";
        const catId =
          document.getElementById("cartCategoryFilter")?.value || "";
        const priceFromRaw = document.getElementById("cartPriceFrom")?.value;
        const priceToRaw = document.getElementById("cartPriceTo")?.value;

        let priceFrom = null,
          priceTo = null;
        if (priceFromRaw && priceFromRaw !== "")
          priceFrom = parseFloat(priceFromRaw);
        if (priceToRaw && priceToRaw !== "") priceTo = parseFloat(priceToRaw);
        if (isNaN(priceFrom)) priceFrom = null;
        if (isNaN(priceTo)) priceTo = null;

        // Khi lọc theo danh mục: chỉ giữ các item khớp danh mục trong từng cart
        // và ẩn các cart không còn item nào sau lọc.
        filteredCarts = allCarts
          .map((cart) => {
            const visible = catId
              ? (cart.items || []).filter(
                  (it) => String(it._categoryId) === String(catId),
                )
              : cart.items || [];
            return { ...cart, _visibleItems: visible };
          })
          .filter((cart) => {
            // 1) Lọc theo tên KH
            if (
              keyword &&
              !(cart.userName || "").toLowerCase().includes(keyword)
            )
              return false;
            // 2) Lọc theo danh mục — phải có ít nhất 1 item khớp
            if (
              catId &&
              (!cart._visibleItems || cart._visibleItems.length === 0)
            )
              return false;
            // 3) Lọc theo khoảng tổng tiền — tính lại trên _visibleItems để chính xác khi đã lọc danh mục
            const subTotal = (cart._visibleItems || []).reduce(
              (s, it) => s + (it.subTotal ?? it.unitPrice * it.quantity ?? 0),
              0,
            );
            if (priceFrom !== null && subTotal < priceFrom) return false;
            if (priceTo !== null && subTotal > priceTo) return false;
            return true;
          });
        cartPage = 1;
        renderCarts();
      }

      async function updateCartQty(cartItemId, newQty) {
        if (newQty < 0) newQty = 0;
        const input = document.getElementById(`qty-${cartItemId}`);
        if (input) input.value = newQty;
        if (newQty === 0) {
          if (confirm("Số lượng = 0, sản phẩm sẽ bị xóa khỏi giỏ. Xác nhận?")) {
            await removeCartItem(cartItemId);
          } else {
            await loadCarts(); // reload để hoàn nguyên
          }
        } else {
          await saveCartQty(cartItemId);
        }
      }

      async function saveCartQty(cartItemId) {
        const input = document.getElementById(`qty-${cartItemId}`);
        let newQuantity = parseInt(input.value);
        if (isNaN(newQuantity) || newQuantity < 1) newQuantity = 1;
        input.value = newQuantity;
        try {
          await apiFetch(PRODUCT_API, `/api/cart/update`, "PUT", {
            cartItemId,
            quantity: newQuantity,
          });
          showToast("Đã cập nhật số lượng", "success");
          await loadCarts(); // reload lại toàn bộ để đồng bộ
        } catch (err) {
          console.error("saveCartQty error:", err);
          showToast("Lỗi: " + err.message, "error");
          await loadCarts(); // reload để hoàn nguyên
        }
      }

      async function removeCartItem(cartItemId) {
        if (confirm("Xóa sản phẩm này khỏi giỏ?")) {
          try {
            await apiFetch(PRODUCT_API, `/api/cart/${cartItemId}`, "DELETE");
            showToast("Đã xóa", "success");
            await loadCarts();
          } catch (err) {
            console.error("removeCartItem error:", err);
            showToast("Lỗi: " + err.message, "error");
          }
        }
      }

      async function clearUserCart(userId) {
        if (
          confirm(
            "Xóa toàn bộ giỏ hàng của khách hàng này? Hành động không thể hoàn tác.",
          )
        ) {
          try {
            await apiFetch(PRODUCT_API, `/api/cart/clear/${userId}`, "DELETE");
            showToast("Đã xóa toàn bộ giỏ", "success");
            await loadCarts();
          } catch (err) {
            console.error("clearUserCart error:", err);
            showToast("Lỗi: " + err.message, "error");
          }
        }
      }
      // ============================================================
      //  IMAGE PICKER
      // ============================================================
      function switchImgTab(tab) {
        ["url", "upload"].forEach((t) => {
          var k = t.charAt(0).toUpperCase() + t.slice(1);
          var panel = document.getElementById("imgPanel" + k);
          var btn = document.getElementById("imgTab" + k);
          if (panel) panel.style.display = t === tab ? "block" : "none";
          if (btn) {
            btn.style.background = t === tab ? "#111" : "#f5f5f5";
            btn.style.color = t === tab ? "#fff" : "#555";
          }
        });
      }

      /**
       * Chuẩn hóa URL ảnh:
       *  - "http..."     → giữ nguyên
       *  - "/uploads/x"  → ghép với PRODUCT_API (http://localhost:5001/uploads/x)
       *  - "uploads/x"   → ghép với PRODUCT_API/uploads/x
       *  - ""            → ""
       * Trả URL absolute để <img> luôn load đúng dù trang đang ở origin khác.
       */
      function resolveImageUrl(url) {
        if (!url) return "";
        var s = String(url).trim();
        if (/^https?:\/\//i.test(s) || s.startsWith("data:")) return s;
        if (s.startsWith("/")) return PRODUCT_API + s;
        return PRODUCT_API + "/" + s;
      }

      function previewImageFromUrl(url) {
        // Giữ lại stub để các listener cũ (nếu có) không vỡ — không dùng nữa.
      }

      // ============================================================
      //  MULTI-IMAGE GALLERY STATE
      //  productImageList: array các absolute URL (ảnh đầu = ảnh chính)
      // ============================================================
      var productImageList = [];

      // ImageUrl trong DB có thể là JSON array string hoặc 1 URL đơn → cùng parse về array
      function parseProductImages(raw) {
        if (!raw) return [];
        if (Array.isArray(raw)) return raw.filter(Boolean);
        const s = String(raw).trim();
        if (!s) return [];
        if (s.startsWith("[")) {
          try {
            const arr = JSON.parse(s);
            if (Array.isArray(arr)) return arr.filter(Boolean);
          } catch (_) {}
        }
        return [s];
      }

      function setProductImages(list) {
        productImageList = Array.isArray(list)
          ? list.filter((u) => u && String(u).trim()).map((u) => String(u).trim())
          : [];
        renderProductImages();
      }

      function addProductImage(url) {
        if (!url) return;
        const u = String(url).trim();
        if (!u) return;
        if (productImageList.includes(u)) {
          showToast("Ảnh này đã có trong danh sách", "error");
          return;
        }
        productImageList.push(u);
        renderProductImages();
      }

      function removeProductImage(index) {
        if (index < 0 || index >= productImageList.length) return;
        productImageList.splice(index, 1);
        renderProductImages();
      }

      function setAsCoverImage(index) {
        if (index <= 0 || index >= productImageList.length) return;
        const [item] = productImageList.splice(index, 1);
        productImageList.unshift(item);
        renderProductImages();
      }

      function renderProductImages() {
        const wrap = document.getElementById("imgGalleryWrap");
        const list = document.getElementById("imgGalleryList");
        const countEl = document.getElementById("imgGalleryCount");
        const hidden = document.getElementById("productImage");
        if (!wrap || !list) return;
        if (productImageList.length === 0) {
          wrap.style.display = "none";
          list.innerHTML = "";
          if (countEl) countEl.textContent = "0";
          if (hidden) hidden.value = "";
          return;
        }
        wrap.style.display = "block";
        if (countEl) countEl.textContent = String(productImageList.length);
        if (hidden)
          hidden.value =
            productImageList.length === 1
              ? productImageList[0]
              : JSON.stringify(productImageList);
        list.innerHTML = productImageList
          .map(
            (url, i) => `
            <div style="position:relative;width:96px">
              <img src="${url}"
                   alt="ảnh ${i + 1}"
                   onclick="setAsCoverImage(${i})"
                   title="${i === 0 ? "Ảnh chính" : "Bấm để đặt làm ảnh chính"}"
                   style="width:96px;height:96px;object-fit:cover;
                          border:2px solid ${i === 0 ? "#f759ab" : "#e0e0e0"};
                          cursor:${i === 0 ? "default" : "pointer"};display:block"
                   onerror="this.style.background='#fce4ec';this.alt='⚠'" />
              ${
                i === 0
                  ? '<div style="position:absolute;left:0;bottom:0;background:#f759ab;color:#fff;font-size:9px;padding:2px 6px;letter-spacing:1px">CHÍNH</div>'
                  : ""
              }
              <button type="button"
                onclick="removeProductImage(${i})"
                title="Xóa ảnh"
                style="position:absolute;top:-6px;right:-6px;width:20px;height:20px;
                       border-radius:50%;background:#ef4444;color:#fff;border:none;
                       cursor:pointer;font-size:12px;line-height:1;
                       display:flex;align-items:center;justify-content:center">×</button>
            </div>
          `,
          )
          .join("");
      }

      function addImageFromUrlInput() {
        const inp = document.getElementById("productImageUrlInput");
        if (!inp) return;
        const url = inp.value.trim();
        if (!url) return;
        addProductImage(resolveImageUrl(url));
        inp.value = "";
      }

      function uploadImageFile(file) {
        if (!file) return;
        if (file.size > 5 * 1024 * 1024) {
          showToast("Ảnh quá lớn! Tối đa 5MB", "error");
          return;
        }
        if (!file.type.startsWith("image/")) {
          showToast("Chỉ chấp nhận file ảnh!", "error");
          return;
        }

        const progress = document.getElementById("uploadProgress");
        const progressBar = document.getElementById("uploadProgressBar");
        const statusDiv = document.getElementById("uploadStatus");
        if (progress) progress.style.display = "block";
        if (progressBar) progressBar.style.width = "0%";
        if (statusDiv) statusDiv.textContent = "Đang tải lên...";

        const formData = new FormData();
        formData.append("file", file);
        const token = localStorage.getItem("token") || "";

        fetch(PRODUCT_API + "/api/products/upload-image", {
          method: "POST",
          headers: { Authorization: "Bearer " + token },
          body: formData,
        })
          .then((res) => {
            if (!res.ok) throw new Error("Upload failed: " + res.status);
            return res.json();
          })
          .then((data) => {
            // Backend có thể trả về { url, imageUrl, path, fileName, ... } — fallback nhiều dạng
            var rawUrl =
              data.url ||
              data.Url ||
              data.imageUrl ||
              data.ImageUrl ||
              data.path ||
              data.Path ||
              data.fileName ||
              data.FileName ||
              "";
            if (!rawUrl) throw new Error("Backend không trả về URL ảnh");

            // Chuyển sang URL absolute để cả preview và DB đều load đúng
            var absUrl = resolveImageUrl(rawUrl);

            if (progressBar) progressBar.style.width = "100%";
            if (statusDiv) {
              statusDiv.textContent = "✓ Tải lên thành công!";
              statusDiv.style.color = "#22c55e";
            }
            // Thêm ảnh mới vào gallery (giữ các ảnh hiện có)
            addProductImage(absUrl);
            setTimeout(() => {
              if (progress) progress.style.display = "none";
            }, 2000);
            showToast("✓ Ảnh đã tải lên thành công!", "success");
          })
          .catch((err) => {
            console.error("Upload error:", err);
            showToast("❌ Lỗi tải lên: " + err.message, "error");
            if (progress) progress.style.display = "none";
          });
      }

      // ============================================================
      //  CONFIRM DELETE
      // ============================================================
      async function confirmDeleteAction() {
        if (pendingDeleteFn) {
          try {
            await pendingDeleteFn();
          } catch (e) {
            showToast("Lỗi xóa: " + e.message, "error");
          }
          pendingDeleteFn = null;
        }
        closeModal("confirmModal");
      }

      // ============================================================
      //  BANNERS
      // ============================================================
      async function loadBanners() {
        const tbody = document.getElementById("bannerTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(PRODUCT_API, "/api/Banners/all");
          allBanners = Array.isArray(res) ? res : res?.items || [];
          allBanners = sortAsc(allBanners, "sortOrder");
          renderBanners();
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải banners: " + err.message, "error");
        }
      }

      function renderBanners() {
        const total = allBanners.length;
        document.getElementById("bannerCount").innerText =
          `Hiển thị ${total} banner`;
        const tbody = document.getElementById("bannerTable");
        if (!tbody) return;
        if (total === 0) {
          tbody.innerHTML = emptyRow(7, "Chưa có banner nào");
          return;
        }
        let html = "";
        for (let b of allBanners) {
          const id = b.id || b.Id;
          const imgUrl = resolveImageUrl(b.imageUrl || b.ImageUrl || "");
          html += `<tr>
            <td class="text-center">${id}</td>
            <td>${imgUrl ? `<img src="${imgUrl}" style="width:80px;height:40px;object-fit:cover;border-radius:4px" onerror="this.style.display='none'" />` : '<span style="color:#9ca3af">—</span>'}</td>
            <td><div class="fw-semibold">${escapeHtml(b.title || b.Title || "—")}</div>${b.subtitle || b.Subtitle ? `<div style="font-size:11px;color:#6b7280">${escapeHtml(b.subtitle || b.Subtitle)}</div>` : ""}</td>
            <td style="max-width:160px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${b.linkUrl || b.LinkUrl ? escapeHtml(b.linkUrl || b.LinkUrl) : '<span style="color:#9ca3af">—</span>'}</td>
            <td class="text-center"><input type="number" value="${b.sortOrder ?? b.SortOrder ?? 0}" min="0" style="width:60px;text-align:center" class="form-control-admin banner-sort" data-id="${id}" /></td>
            <td class="text-center"><span class="badge-status badge-${(b.isActive ?? b.IsActive) ? "delivered" : "cancelled"}">${(b.isActive ?? b.IsActive) ? "Hiển thị" : "Ẩn"}</span></td>
            <td class="text-center"><div class="d-flex gap-1 justify-content-center">
              <button type="button" class="btn-icon edit" onclick="editBanner(${id})" title="Sửa">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
              </button>
              <button type="button" class="btn-icon delete" onclick="deleteBanner(${id},'${escapeHtml(b.title || b.Title || "")}')" title="Xóa">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
              </button>
            </div></td>
          </tr>`;
        }
        tbody.innerHTML = html;
      }

      function openBannerModal() {
        document.getElementById("bannerModalTitle").innerText = "Thêm Banner";
        document.getElementById("bannerId").value = "";
        document.getElementById("bannerTitle").value = "";
        document.getElementById("bannerSubtitle").value = "";
        document.getElementById("bannerImageUrl").value = "";
        document.getElementById("bannerBgColor").value = "#f759ab";
        document.getElementById("bannerLinkUrl").value = "";
        document.getElementById("bannerButtonText").value = "";
        document.getElementById("bannerSortOrder").value = "0";
        document.getElementById("bannerIsActive").checked = true;
        openModal("bannerModal");
      }

      function editBanner(id) {
        const b = allBanners.find((x) => (x.id || x.Id) == id);
        if (!b) return;
        document.getElementById("bannerModalTitle").innerText = "Sửa Banner #" + id;
        document.getElementById("bannerId").value = id;
        document.getElementById("bannerTitle").value = b.title || b.Title || "";
        document.getElementById("bannerSubtitle").value = b.subtitle || b.Subtitle || "";
        document.getElementById("bannerImageUrl").value = b.imageUrl || b.ImageUrl || "";
        document.getElementById("bannerBgColor").value = b.bgColor || b.BgColor || "#f759ab";
        document.getElementById("bannerLinkUrl").value = b.linkUrl || b.LinkUrl || "";
        document.getElementById("bannerButtonText").value = b.buttonText || b.ButtonText || "";
        document.getElementById("bannerSortOrder").value = b.sortOrder ?? b.SortOrder ?? 0;
        document.getElementById("bannerIsActive").checked = (b.isActive ?? b.IsActive) !== false;
        openModal("bannerModal");
      }

      async function saveBanner() {
        const id = document.getElementById("bannerId").value;
        const title = document.getElementById("bannerTitle").value.trim();
        const imageUrl = document.getElementById("bannerImageUrl").value.trim();
        if (!title) return showToast("Vui lòng nhập tiêu đề", "error");
        if (!imageUrl) return showToast("Vui lòng nhập URL ảnh", "error");
        const data = {
          Title: title,
          Subtitle: document.getElementById("bannerSubtitle").value.trim() || null,
          ImageUrl: imageUrl,
          LinkUrl: document.getElementById("bannerLinkUrl").value.trim() || null,
          ButtonText: document.getElementById("bannerButtonText").value.trim() || null,
          BgColor: document.getElementById("bannerBgColor").value,
          SortOrder: parseInt(document.getElementById("bannerSortOrder").value) || 0,
          IsActive: document.getElementById("bannerIsActive").checked,
        };
        const btn = document.getElementById("saveBannerBtn");
        btn.disabled = true;
        btn.textContent = "Đang lưu...";
        try {
          if (id) {
            await apiFetch(PRODUCT_API, `/api/Banners/${id}`, "PUT", data);
            showToast("Đã cập nhật banner", "success");
          } else {
            await apiFetch(PRODUCT_API, "/api/Banners", "POST", data);
            showToast("Đã thêm banner mới", "success");
          }
          closeModal("bannerModal");
          await loadBanners();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        } finally {
          btn.disabled = false;
          btn.textContent = "Lưu Banner";
        }
      }

      async function deleteBanner(id, title) {
        if (!confirm(`Xóa banner "${title}"? Hành động không thể hoàn tác.`)) return;
        try {
          await apiFetch(PRODUCT_API, `/api/Banners/${id}`, "DELETE");
          showToast("Đã xóa banner", "success");
          await loadBanners();
        } catch (err) {
          showToast("Lỗi xóa: " + err.message, "error");
        }
      }

      async function saveBannerOrder() {
        const inputs = document.querySelectorAll(".banner-sort");
        const orders = [];
        inputs.forEach((inp) => {
          orders.push({
            Id: parseInt(inp.dataset.id),
            SortOrder: parseInt(inp.value) || 0,
          });
        });
        try {
          await apiFetch(PRODUCT_API, "/api/Banners/reorder", "PUT", orders);
          showToast("Đã lưu thứ tự " + orders.length + " banner", "success");
          await loadBanners();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        }
      }

      // ============================================================
      //  FEATURED PRODUCTS
      // ============================================================
      async function loadFeaturedProducts() {
        const section = document.getElementById("featuredSectionFilter")?.value || "";
        currentFeaturedSection = section;
        const tbody = document.getElementById("featuredTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="7" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const url = section
            ? `/api/FeaturedProducts?section=${encodeURIComponent(section)}`
            : "/api/FeaturedProducts";
          const res = await apiFetch(PRODUCT_API, url);
          allFeatured = Array.isArray(res) ? res : res?.items || [];
          renderFeatured();
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(7, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải: " + err.message, "error");
        }
      }

      function renderFeatured() {
        const sectionLabels = {
          new_arrivals: "Hàng mới về",
          best_sellers: "Bán chạy",
          hero_slider: "Hero Slider",
        };
        const total = allFeatured.length;
        document.getElementById("featuredCount").innerText = `Hiển thị ${total} sản phẩm`;
        const tbody = document.getElementById("featuredTable");
        if (!tbody) return;
        if (total === 0) {
          tbody.innerHTML = emptyRow(7, "Chưa có sản phẩm nổi bật nào");
          return;
        }
        let html = "";
        for (let f of allFeatured) {
          const id = f.id || f.Id;
          const section = f.section || f.Section || "—";
          const product = f.product || {};
          const productName = product.name || product.Name || `SP#${product.id || product.Id}`;
          const category = product.category || product.Category || "—";
          const price = product.price ?? product.Price ?? 0;
          const imageUrl = resolveImageUrl(product.imageUrl || product.ImageUrl || "");
          html += `<tr>
            <td class="text-center">${id}</td>
            <td><div class="d-flex align-items-center gap-2">${imageUrl ? `<img src="${imageUrl}" style="width:36px;height:36px;object-fit:cover;border-radius:4px" onerror="this.style.display='none'" />` : ""}<span class="fw-semibold">${escapeHtml(productName)}</span></div></td>
            <td>${escapeHtml(category)}</td>
            <td style="font-weight:600;color:var(--pink)">${formatMoney(price)}</td>
            <td class="text-center"><span class="badge-status badge-processing">${sectionLabels[section] || section}</span></td>
            <td class="text-center"><input type="number" value="${f.sortOrder ?? f.SortOrder ?? 0}" min="0" style="width:60px;text-align:center" class="form-control-admin featured-sort" data-id="${id}" /></td>
            <td class="text-center">
              <button type="button" class="btn-icon delete" onclick="deleteFeaturedProduct(${id})" title="Xóa khỏi danh sách">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a1 1 0 011-1h4a1 1 0 011 1v2"/></svg>
              </button>
            </td>
          </tr>`;
        }
        tbody.innerHTML = html;
      }

      async function openFeaturedModal() {
        const sel = document.getElementById("featuredProductId");
        sel.innerHTML = '<option value="">-- Đang tải sản phẩm --</option>';
        // Luôn fetch mới toàn bộ sản phẩm, không dùng cache (cache có thể thiếu)
        try {
          const res = await apiFetch(PRODUCT_API, "/api/products?page=1&pageSize=1000");
          allProducts = sortAsc(Array.isArray(res) ? res : res?.items || []);
        } catch (_) {}
        sel.innerHTML = '<option value="">-- Chọn sản phẩm --</option>';
        allProducts.forEach((p) => {
          const opt = document.createElement("option");
          opt.value = p.id || p.Id;
          opt.textContent = (p.name || p.Name || "SP") + " — " + formatMoney(p.price ?? p.Price ?? 0);
          sel.appendChild(opt);
        });
        document.getElementById("featuredSortOrder").value = "0";
        document.getElementById("featuredSection").value = currentFeaturedSection;
        openModal("featuredModal");
      }

      async function saveFeaturedProduct() {
        const productId = parseInt(document.getElementById("featuredProductId").value);
        const section = document.getElementById("featuredSection").value;
        const sortOrder = parseInt(document.getElementById("featuredSortOrder").value) || 0;
        if (!productId) return showToast("Vui lòng chọn sản phẩm", "error");
        try {
          await apiFetch(PRODUCT_API, "/api/FeaturedProducts", "POST", {
            ProductId: productId,
            Section: section,
            SortOrder: sortOrder,
          });
          showToast("Đã thêm sản phẩm vào danh sách nổi bật", "success");
          closeModal("featuredModal");
          await loadFeaturedProducts();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        }
      }

      async function deleteFeaturedProduct(id) {
        if (!confirm("Xóa sản phẩm này khỏi danh sách nổi bật?")) return;
        try {
          await apiFetch(PRODUCT_API, `/api/FeaturedProducts/${id}`, "DELETE");
          showToast("Đã xóa", "success");
          await loadFeaturedProducts();
        } catch (err) {
          showToast("Lỗi xóa: " + err.message, "error");
        }
      }

      async function saveFeaturedOrder() {
        const inputs = document.querySelectorAll(".featured-sort");
        const orders = [];
        inputs.forEach((inp) => {
          orders.push({
            Id: parseInt(inp.dataset.id),
            SortOrder: parseInt(inp.value) || 0,
          });
        });
        try {
          await apiFetch(PRODUCT_API, "/api/FeaturedProducts/reorder", "PUT", orders);
          showToast("Đã lưu thứ tự " + orders.length + " sản phẩm", "success");
          await loadFeaturedProducts();
        } catch (err) {
          showToast("Lỗi: " + err.message, "error");
        }
      }

      // ============================================================
      //  ROLES & PERMISSIONS
      // ============================================================
      let allRoles = [];

      async function loadRoles() {
        const tbody = document.getElementById("roleTable");
        if (tbody)
          tbody.innerHTML = `<tr><td colspan="5" class="text-center py-4"><div class="spinner mx-auto"></div></td></tr>`;
        try {
          const res = await apiFetch(AUTH_API, "/api/Roles");
          allRoles = Array.isArray(res) ? res : res?.items || [];
          renderRoles();
        } catch (err) {
          if (tbody) tbody.innerHTML = emptyRow(5, "❌ Lỗi: " + err.message);
          showToast("Lỗi tải roles: " + err.message, "error");
        }
      }

      function renderRoles() {
        const tbody = document.getElementById("roleTable");
        if (!tbody) return;
        if (allRoles.length === 0) {
          tbody.innerHTML = emptyRow(5, "Không có role nào");
          return;
        }
        const typeLabels = { 0: "Khách hàng", 1: "Admin", 2: "Manager" };
        let html = "";
        for (let r of allRoles) {
          const id = r.id || r.Id;
          const name = r.name || r.Name || "—";
          const desc = r.description || r.Description || "—";
          const userType = r.userType ?? r.UserType ?? 0;
          html += `<tr>
            <td class="text-center">${id}</td>
            <td class="fw-semibold">${escapeHtml(name)}</td>
            <td>${escapeHtml(desc)}</td>
            <td class="text-center"><span class="badge-status badge-${userType === 1 ? "processing" : userType === 2 ? "shipped" : "pending"}">${typeLabels[userType] || userType}</span></td>
            <td class="text-center">
              <button type="button" class="btn-admin btn-outline-admin" style="font-size:12px;padding:4px 10px" onclick="viewPermissions(${id},'${escapeHtml(name)}')">🔑 Xem quyền</button>
            </td>
          </tr>`;
        }
        tbody.innerHTML = html;
      }

      async function viewPermissions(roleId, roleName) {
        const card = document.getElementById("permissionCard");
        const detail = document.getElementById("permissionDetail");
        document.getElementById("permissionRoleName").innerText = roleName;
        card.style.display = "block";
        detail.innerHTML = `<div class="text-center py-3"><div class="spinner mx-auto"></div></div>`;
        try {
          const res = await apiFetch(AUTH_API, `/api/Roles/${roleId}/permissions`);
          const perms = res?.permissions || [];
          if (perms.length === 0) {
            detail.innerHTML = `<div class="text-center py-3 text-muted">Role này không có quyền nào</div>`;
            return;
          }
          // Nhóm quyền theo domain (vd: users.read, users.write → nhóm "users")
          const grouped = {};
          perms.forEach((p) => {
            const parts = p.split(".");
            const domain = parts[0] || "khác";
            const action = parts[1] || p;
            if (!grouped[domain]) grouped[domain] = [];
            grouped[domain].push({ raw: p, action });
          });
          const actionLabels = {
            read: "👁 Xem",
            write: "✏️ Ghi",
            create: "➕ Tạo",
            edit: "📝 Sửa",
            delete: "🗑 Xóa",
            manage: "⚙️ Quản lý",
          };
          let html = '<div class="row g-3">';
          for (const [domain, actions] of Object.entries(grouped)) {
            html += `<div class="col-md-6 col-lg-4">
              <div style="background:#fdf2f8;border-radius:8px;padding:14px;height:100%">
                <div style="font-weight:700;color:var(--pink);margin-bottom:8px;text-transform:uppercase;font-size:12px;letter-spacing:1px">📦 ${domain}</div>`;
            actions.forEach((a) => {
              html += `<div style="font-size:13px;padding:3px 0;color:#374151">${actionLabels[a.action] || "🔹 " + a.action} <code style="font-size:10px;color:#9ca3af;background:#fff;padding:1px 5px;border-radius:3px">${a.raw}</code></div>`;
            });
            html += `</div></div>`;
          }
          html += "</div>";
          html += `<div class="mt-3 text-muted small">Tổng: <strong>${perms.length}</strong> quyền</div>`;
          detail.innerHTML = html;
          card.scrollIntoView({ behavior: "smooth" });
        } catch (err) {
          detail.innerHTML = `<div class="text-center py-3" style="color:#dc2626">❌ ${err.message}</div>`;
        }
      }

      // ============================================================
      //  DOM READY — chỉ 1 block duy nhất
      // ============================================================
      document.addEventListener("DOMContentLoaded", function () {
        // 1. Init user
        initAdminUser();
        checkApiStatus();
        loadDashboard();

        // 2. Sidebar navigation — dùng event delegation, chặn mọi href
        document
          .querySelector(".sidebar-nav")
          .addEventListener("click", function (e) {
            // Tìm link gần nhất được click
            const link = e.target.closest(".nav-item-link");
            if (!link) return;
            e.preventDefault();
            e.stopPropagation();

            const page = link.dataset.page;
            const href = link.dataset.href;

            if (page) {
              // Chuyển tab nội bộ — KHÔNG reload
              switchPage(page);
              // Tự đóng sidebar trên mobile sau khi chọn
              if (window.innerWidth < 768) closeSidebar();
            } else if (href) {
              // Link thật (Xem Website) — mở tab mới thay vì reload trang
              window.open(href, "_blank");
            }
          });

        // 3. Upload button — FIX CHÍNH cho lỗi reload
        var uploadBtn = document.getElementById("uploadImageBtn");
        var fileInput = document.getElementById("imgFileInput");
        if (uploadBtn && fileInput) {
          uploadBtn.addEventListener("click", function (e) {
            e.preventDefault();
            e.stopPropagation();
            fileInput.click();
          });
          fileInput.addEventListener("change", function (e) {
            if (e.target.files && e.target.files.length) {
              Array.from(e.target.files).forEach((f) => uploadImageFile(f));
              e.target.value = "";
            }
          });
        }

        // 4. Chặn submit ở mọi nơi trong productModal (capture + bubble)
        var modal = document.getElementById("productModal");
        if (modal) {
          modal.addEventListener(
            "submit",
            function (e) {
              e.preventDefault();
              e.stopPropagation();
              return false;
            },
            true,
          );
          modal.addEventListener("submit", function (e) {
            e.preventDefault();
            e.stopPropagation();
          });
        }

        // 5. Đóng modal khi click overlay
        document.querySelectorAll(".modal-overlay").forEach((overlay) => {
          overlay.addEventListener("click", function (e) {
            if (e.target === this) this.classList.remove("show");
          });
        });
      });
