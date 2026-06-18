/* ============================================================
 *  GlowHub Admin Panel — admin.js
 *  Requires: api.js (apiFetch, Auth, adminGuard, renderAdminTopbar)
 *  Backends: APIService :5001  |  AuthService :5002
 *  Notes (per Admin Guide):
 *    • Per-shop financials come from SubOrder, NOT Order.ShopId
 *    • Pagination uses CountAsync/SumAsync — never load entire tables
 *    • Audit log JSON parsing wrapped in try/catch
 * ============================================================ */
/* eslint-disable */
// @ts-nocheck

(function () {
  "use strict";

  // ───────────────────────────────────────────────────────────
  //  CONFIG
  // ───────────────────────────────────────────────────────────
  // Đọc từ window.GH_CONFIG nếu có (cho phép override khi deploy production)
  // Để override: thêm <script>window.GH_CONFIG = { API_URL: '...', AUTH_URL: '...' }</script>
  // vào admin.html trước khi load admin.js.
  const API =
    (window.GH_CONFIG && window.GH_CONFIG.API_URL) || "http://localhost:5001"; // APIService
  const AUTH =
    (window.GH_CONFIG && window.GH_CONFIG.AUTH_URL) || "http://localhost:5002"; // AuthService
  const PAGE_SIZE = 20;

  const CLOUDINARY_CLOUD = "dcucbyzdo";
  const CLOUDINARY_PRESET = "GlowHub_Upload";

  const STATUS_VN = {
    PENDING: { label: "Chờ Xử Lý", cls: "adm-badge-warning" },
    CONFIRMED: { label: "Đã Xác Nhận", cls: "adm-badge-info" },
    SHIPPING: { label: "Đang Giao", cls: "adm-badge-orange" },
    DELIVERED: { label: "Đã Giao", cls: "adm-badge-success" },
    COMPLETED: { label: "Hoàn Thành", cls: "adm-badge-success-dark" },
    CANCELLED: { label: "Đã Hủy", cls: "adm-badge-danger" },
  };

  const SHOP_STATUS = {
    0: { label: "Chờ duyệt", cls: "adm-badge-warning" },
    1: { label: "Hoạt động", cls: "adm-badge-success" },
    2: { label: "Đã khóa", cls: "adm-badge-danger" },
  };

  const USER_TYPE = {
    0: { label: "Customer", cls: "adm-badge-info" },
    1: { label: "Admin", cls: "adm-badge-purple" },
    2: { label: "Seller", cls: "adm-badge-orange" },
  };

  // ───────────────────────────────────────────────────────────
  //  UTILITIES
  // ───────────────────────────────────────────────────────────
  function $(sel, root) {
    return (root || document).querySelector(sel);
  }
  function $$(sel, root) {
    return Array.from((root || document).querySelectorAll(sel));
  }
  function escHtml(s) {
    if (s == null) return "";
    return String(s)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }
  function fmtMoney(n) {
    if (n == null || isNaN(+n)) return "0 ₫";
    return new Intl.NumberFormat("vi-VN").format(Math.round(+n)) + " ₫";
  }
  function fmtNum(n) {
    if (n == null || isNaN(+n)) return "0";
    return new Intl.NumberFormat("vi-VN").format(+n);
  }
  function fmtDate(d) {
    if (!d) return "";
    const date = new Date(d);
    if (isNaN(date.getTime())) return String(d);
    return date.toLocaleString("vi-VN", {
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  }
  function fmtDateShort(d) {
    if (!d) return "";
    const date = new Date(d);
    if (isNaN(date.getTime())) return String(d);
    return date.toLocaleDateString("vi-VN");
  }
  function statusBadge(status) {
    const s = STATUS_VN[(status || "").toUpperCase()] || {
      label: status || "—",
      cls: "adm-badge-gray",
    };
    return (
      '<span class="adm-badge ' + s.cls + '">' + escHtml(s.label) + "</span>"
    );
  }
  function safeParseJson(s) {
    if (!s) return null;
    try {
      return JSON.parse(s);
    } catch (_) {
      return s;
    }
  }
  function debounce(fn, ms) {
    let t;
    return function () {
      const self = this,
        args = arguments;
      clearTimeout(t);
      t = setTimeout(function () {
        fn.apply(self, args);
      }, ms || 300);
    };
  }
  // Wire auto-reload on filter changes: text inputs use debounced `input`,
  // selects/dates use immediate `change`. The reload button still works manually.
  function autoReload(btnSel, textSelectors, changeSelectors) {
    const fire = function () {
      const btn = $(btnSel);
      if (btn) btn.click();
    };
    const debounced = debounce(fire, 300);
    (textSelectors || []).forEach(function (s) {
      const el = $(s);
      if (el) el.addEventListener("input", debounced);
    });
    (changeSelectors || []).forEach(function (s) {
      const el = $(s);
      if (el) el.addEventListener("change", fire);
    });
  }

  // ───────────────────────────────────────────────────────────
  //  HTTP wrappers
  //  Backend giữ PascalCase (Program.cs cố tình set PropertyNamingPolicy=null
  //  để giữ tương thích với các trang frontend khác). Admin panel đọc camelCase
  //  → normalize ngay tại lớp HTTP: chỉ đổi ký tự đầu thành thường, không đụng tới giá trị.
  // ───────────────────────────────────────────────────────────
  function toCamelDeep(o) {
    if (Array.isArray(o)) return o.map(toCamelDeep);
    if (o && typeof o === "object" && o.constructor === Object) {
      const out = {};
      Object.keys(o).forEach(function (k) {
        // Hằng số ALL_CAPS (PENDING, COD, UNPAID, ...) thường là dictionary key — giữ nguyên.
        const isConst = /^[A-Z][A-Z0-9_]*$/.test(k);
        const nk = isConst ? k : k.charAt(0).toLowerCase() + k.slice(1);
        // Nếu key đã có cả 2 dạng (vd có sẵn camelCase từ anonymous object) → ưu tiên camelCase đã có
        if (nk !== k && Object.prototype.hasOwnProperty.call(o, nk)) {
          out[nk] = toCamelDeep(o[nk]);
        } else {
          out[nk] = toCamelDeep(o[k]);
        }
      });
      return out;
    }
    return o;
  }
  async function api(path, method, body) {
    const r = await apiFetch(API, path, method || "GET", body || null);
    return toCamelDeep(r);
  }
  async function auth(path, method, body) {
    const r = await apiFetch(AUTH, path, method || "GET", body || null);
    return toCamelDeep(r);
  }

  function qs(params) {
    if (!params) return "";
    const parts = [];
    Object.keys(params).forEach(function (k) {
      const v = params[k];
      if (v == null || v === "") return;
      parts.push(encodeURIComponent(k) + "=" + encodeURIComponent(v));
    });
    return parts.length ? "?" + parts.join("&") : "";
  }

  // ───────────────────────────────────────────────────────────
  //  TOAST
  // ───────────────────────────────────────────────────────────
  function toast(msg, kind) {
    const box = $("#admToastBox");
    if (!box) {
      alert(msg);
      return;
    }
    const el = document.createElement("div");
    el.className = "adm-toast" + (kind ? " adm-toast-" + kind : "");
    el.textContent = msg;
    box.appendChild(el);
    setTimeout(function () {
      el.style.opacity = "0";
      setTimeout(function () {
        el.remove();
      }, 250);
    }, 3500);
  }

  // ───────────────────────────────────────────────────────────
  //  MODAL helpers
  // ───────────────────────────────────────────────────────────
  function openModal(opts) {
    const mask = $("#admModal");
    const titleEl = $("#admModalTitle");
    const bodyEl = $("#admModalBody");
    const confirmBtn = $("#admModalConfirm");
    const cancelBtn = $("#admModalCancel");
    const closeBtn = $("#admModalClose");
    const modal = mask.querySelector(".adm-modal");

    titleEl.textContent = opts.title || "";
    if (opts.html != null) bodyEl.innerHTML = opts.html;
    else bodyEl.textContent = opts.body || "";

    modal.classList.remove("adm-modal-lg", "adm-modal-xl");
    if (opts.size === "lg") modal.classList.add("adm-modal-lg");
    if (opts.size === "xl") modal.classList.add("adm-modal-xl");

    confirmBtn.textContent = opts.confirmText || "Xác nhận";
    cancelBtn.textContent = opts.cancelText || "Hủy";

    confirmBtn.style.display = opts.hideConfirm ? "none" : "inline-block";
    cancelBtn.style.display = opts.hideCancel ? "none" : "inline-block";

    confirmBtn.classList.remove("adm-btn-danger");
    confirmBtn.classList.add("adm-btn-primary");
    if (opts.kind === "danger") {
      confirmBtn.classList.add("adm-btn-danger");
      confirmBtn.classList.remove("adm-btn-primary");
    }

    mask.hidden = false;

    function close() {
      mask.hidden = true;
      confirmBtn.onclick = null;
      cancelBtn.onclick = null;
      closeBtn.onclick = null;
      mask.onclick = null;
    }
    confirmBtn.onclick = async function () {
      if (typeof opts.onConfirm === "function") {
        // Disable button while awaiting để tránh double-click trigger duplicate transactions
        const origText = confirmBtn.textContent;
        confirmBtn.disabled = true;
        confirmBtn.textContent = "Đang xử lý...";
        try {
          const r = await opts.onConfirm(bodyEl, close);
          if (r !== false) close();
        } catch (e) {
          toast(e.message || "Lỗi", "danger");
        } finally {
          confirmBtn.disabled = false;
          confirmBtn.textContent = origText;
        }
      } else close();
    };
    cancelBtn.onclick = close;
    closeBtn.onclick = close;
    mask.onclick = function (e) {
      if (e.target === mask) close();
    };
  }
  function closeModal() {
    $("#admModal").hidden = true;
  }

  function confirmAction(message, onYes, yesText, kind) {
    openModal({
      title: "Xác nhận",
      html: '<p style="margin:0;font-size:15px;">' + escHtml(message) + "</p>",
      confirmText: yesText || "Đồng ý",
      kind: kind,
      onConfirm: async function () {
        await onYes();
        return true;
      },
    });
  }

  // ───────────────────────────────────────────────────────────
  //  CLOUDINARY UPLOAD
  // ───────────────────────────────────────────────────────────
  async function uploadToCloudinary(file) {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("upload_preset", CLOUDINARY_PRESET);
    const res = await fetch(
      "https://api.cloudinary.com/v1_1/" + CLOUDINARY_CLOUD + "/image/upload",
      {
        method: "POST",
        body: fd,
      },
    );
    if (!res.ok) throw new Error("Upload thất bại");
    const data = await res.json();
    return data.secure_url || data.url;
  }

  // ───────────────────────────────────────────────────────────
  //  PAGER
  // ───────────────────────────────────────────────────────────
  function renderPager(containerSel, page, totalPages, onChange) {
    const box = $(containerSel);
    if (!box) return;
    if (!totalPages || totalPages <= 1) {
      box.innerHTML = "";
      return;
    }

    let html = "";
    html +=
      '<button data-p="prev"' + (page <= 1 ? " disabled" : "") + ">«</button>";

    const range = [];
    const start = Math.max(1, page - 2);
    const end = Math.min(totalPages, page + 2);
    if (start > 1) range.push(1);
    if (start > 2) range.push("...");
    for (let i = start; i <= end; i++) range.push(i);
    if (end < totalPages - 1) range.push("...");
    if (end < totalPages) range.push(totalPages);

    range.forEach(function (p) {
      if (p === "...") html += '<span class="adm-pager-info">...</span>';
      else
        html +=
          '<button data-p="' +
          p +
          '"' +
          (p === page ? ' class="active"' : "") +
          ">" +
          p +
          "</button>";
    });

    html +=
      '<button data-p="next"' +
      (page >= totalPages ? " disabled" : "") +
      ">»</button>";
    html +=
      '<span class="adm-pager-info">Trang ' +
      page +
      " / " +
      totalPages +
      "</span>";
    box.innerHTML = html;

    box.querySelectorAll("button[data-p]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        const v = btn.getAttribute("data-p");
        if (v === "prev") onChange(Math.max(1, page - 1));
        else if (v === "next") onChange(Math.min(totalPages, page + 1));
        else onChange(parseInt(v, 10));
      });
    });
  }

  // ───────────────────────────────────────────────────────────
  //  TAB SWITCHER
  // ───────────────────────────────────────────────────────────
  const TAB_TITLES = {
    dashboard: "Tổng Quan",
    users: "Quản Lý Người Dùng",
    shops: "Quản Lý Cửa Hàng",
    products: "Quản Lý Sản Phẩm",
    orders: "Quản Lý Đơn Hàng",
    bank: "Xác Nhận Chuyển Khoản",
    categories: "Quản Lý Danh Mục",
    banners: "Quản Lý Banner",
    flashsale: "Flash Sale",
    vouchers: "Voucher Hệ Thống",
    commission: "Doanh Thu & Hoa Hồng",
    wallet: "Ví & Giải Ngân",
    qna: "Hỏi & Đáp",
    disputes: "Tranh Chấp",
    audit: "Nhật Ký",
    settings: "Cài Đặt Hệ Thống",
    stats: "Thống Kê",
  };

  const TAB_LOADERS = {};

  function switchTab(name) {
    if (!TAB_TITLES[name]) name = "dashboard";
    $$(".adm-nav-link").forEach(function (a) {
      a.classList.toggle("active", a.getAttribute("data-tab") === name);
    });
    $$(".adm-tab").forEach(function (s) {
      s.classList.toggle("active", s.id === "tab-" + name);
    });
    const titleEl = $("#admPageTitle");
    if (titleEl) titleEl.textContent = TAB_TITLES[name];
    if (typeof TAB_LOADERS[name] === "function") {
      try {
        TAB_LOADERS[name]();
      } catch (e) {
        console.error(e);
      }
    }
    $("#admSidebar").classList.remove("is-open");
    if (location.hash !== "#" + name) location.hash = name;
  }

  function bindNav() {
    $$(".adm-nav-link[data-tab]").forEach(function (a) {
      a.addEventListener("click", function (e) {
        e.preventDefault();
        switchTab(a.getAttribute("data-tab"));
      });
    });
    $("#admBurgerBtn").addEventListener("click", function () {
      $("#admSidebar").classList.toggle("is-open");
    });
    $("#admLogoutBtn").addEventListener("click", function (e) {
      e.preventDefault();
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      window.location.href = "login.html";
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · DASHBOARD
  //  ⚠ KHÔNG kéo list /api/admin/orders để vẽ chart/tính KPI —
  //  aggregate đã được gom trong /api/admin/stats/overview (Sprint 2).
  // ═══════════════════════════════════════════════════════════
  let chartRev7;
  TAB_LOADERS.dashboard = async function () {
    try {
      const today = new Date().toISOString().slice(0, 10);
      const weekAgo = new Date(Date.now() - 6 * 86400000)
        .toISOString()
        .slice(0, 10);

      const [usersRes, shopsRes, stats7d, commission, recentOrders] =
        await Promise.all([
          api("/api/admin/users?page=1&limit=1"),
          api("/api/shops/admin/all?page=1&pageSize=1"),
          api(
            "/api/admin/stats/overview?from=" +
              weekAgo +
              "&to=" +
              today +
              "&topShopLimit=5",
          ).catch(function () {
            return null;
          }),
          api("/api/admin/commission/summary").catch(function () {
            return null;
          }),
          api("/api/admin/orders?page=1&limit=10"),
        ]);

      const days = (stats7d && stats7d.revenueByDay) || [];
      const todayBucket = days.length
        ? days[days.length - 1]
        : { revenue: 0, orderCount: 0 };

      $("#kpiUsers").textContent = fmtNum(usersRes.total || 0);
      $("#kpiShops").textContent = fmtNum(shopsRes.total || 0);
      $("#kpiOrdersToday").textContent = fmtNum(todayBucket.orderCount || 0);
      $("#kpiRevenueToday").textContent = fmtMoney(todayBucket.revenue || 0);
      $("#kpiCommission").textContent = fmtMoney(
        commission ? commission.totalCommission : 0,
      );
      $("#kpiPendingPayout").textContent = fmtMoney(
        commission ? commission.pendingRelease : 0,
      );

      // Top shops: ưu tiên dữ liệu range 7 ngày (gần thời điểm hiện tại); fallback commission summary
      const topShops =
        stats7d && stats7d.topShops && stats7d.topShops.length
          ? stats7d.topShops.map(function (s) {
              return { shopName: s.shopName, totalRevenue: s.revenue };
            })
          : commission && commission.byShop
            ? commission.byShop.slice(0, 5)
            : [];
      $("#topShopsList").innerHTML = topShops.length
        ? topShops
            .map(function (s) {
              return (
                '<div class="adm-mini-row"><span class="name">' +
                escHtml(s.shopName) +
                '</span><span class="val">' +
                fmtMoney(s.totalRevenue) +
                "</span></div>"
              );
            })
            .join("")
        : '<div class="adm-empty">Chưa có dữ liệu</div>';

      const items = recentOrders.items || [];
      $("#recentOrdersBody").innerHTML = items.length
        ? items
            .map(function (o) {
              return (
                "<tr><td><strong>" +
                escHtml(o.orderCode) +
                "</strong></td>" +
                "<td>" +
                escHtml((o.customer && o.customer.name) || "—") +
                "</td>" +
                "<td>" +
                fmtMoney(o.finalAmount) +
                "</td>" +
                "<td>" +
                statusBadge(o.status) +
                "</td>" +
                "<td>" +
                fmtDate(o.createdAt) +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="5" class="adm-empty">Chưa có đơn hàng</td></tr>';

      drawRevenueChart7(days);
    } catch (e) {
      toast("Lỗi tải Tổng Quan: " + e.message, "danger");
    }
  };

  function drawRevenueChart7(days) {
    const ctx = $("#chartRevenue7");
    if (!ctx || !window.Chart) return;

    const labels = (days || []).map(function (d) {
      const dt = new Date(d.date);
      return dt.toLocaleDateString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
      });
    });
    const data = (days || []).map(function (d) {
      return +d.revenue || 0;
    });

    if (chartRev7) chartRev7.destroy();
    chartRev7 = new Chart(ctx.getContext("2d"), {
      type: "line",
      data: {
        labels: labels,
        datasets: [
          {
            label: "Doanh thu (₫)",
            data: data,
            fill: true,
            backgroundColor: "rgba(247, 89, 171, 0.15)",
            borderColor: "#f759ab",
            tension: 0.3,
          },
        ],
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: function (v) {
                return fmtNum(v);
              },
            },
          },
        },
      },
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · USERS
  // ═══════════════════════════════════════════════════════════
  const usersState = { page: 1, search: "", role: "", isActive: "" };

  TAB_LOADERS.users = function () {
    loadUsers();
  };

  async function loadUsers() {
    const tbody = $("#usersTableBody");
    tbody.innerHTML =
      '<tr><td colspan="6" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: usersState.page,
        limit: PAGE_SIZE,
        search: usersState.search || null,
        role: usersState.role !== "" ? usersState.role : null,
        isActive: usersState.isActive !== "" ? usersState.isActive : null,
      });
      const r = await api("/api/admin/users" + q);
      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (u) {
              const role = USER_TYPE[u.userType] || {
                label: "?",
                cls: "adm-badge-gray",
              };
              const stat = u.isActive
                ? '<span class="adm-badge adm-badge-success">Hoạt động</span>'
                : '<span class="adm-badge adm-badge-danger">Đã khóa</span>';
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(u.name || u.userName) +
                "</strong></td>" +
                "<td>" +
                escHtml(u.email || "") +
                '<br><small class="adm-muted">' +
                escHtml(u.phone || "") +
                "</small></td>" +
                '<td><span class="adm-badge ' +
                role.cls +
                '">' +
                role.label +
                "</span></td>" +
                "<td>" +
                stat +
                "</td>" +
                "<td>" +
                fmtDateShort(u.createdAt) +
                "</td>" +
                "<td>" +
                (u.isActive
                  ? '<button class="adm-btn-sm adm-btn-danger" data-act="ban" data-id="' +
                    escHtml(u.id) +
                    '">Khóa</button>'
                  : '<button class="adm-btn-sm adm-btn-success" data-act="unban" data-id="' +
                    escHtml(u.id) +
                    '">Mở khóa</button>') +
                '<button class="adm-btn-sm" data-act="role" data-id="' +
                escHtml(u.id) +
                '" data-role="' +
                u.userType +
                '">Đổi vai trò</button>' +
                '<button class="adm-btn-sm adm-btn-danger" data-act="delete" data-id="' +
                escHtml(u.id) +
                '">Xóa</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="6" class="adm-empty">Không có user</td></tr>';

      renderPager("#usersPager", r.page || 1, r.totalPages || 1, function (p) {
        usersState.page = p;
        loadUsers();
      });
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="6" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindUsersTab() {
    $("#usersReloadBtn").addEventListener("click", function () {
      usersState.search = $("#usersSearch").value.trim();
      usersState.role = $("#usersRoleFilter").value;
      usersState.isActive = $("#usersActiveFilter").value;
      usersState.page = 1;
      loadUsers();
    });
    $("#usersSearch").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#usersReloadBtn").click();
    });
    autoReload(
      "#usersReloadBtn",
      ["#usersSearch"],
      ["#usersRoleFilter", "#usersActiveFilter"],
    );
    $("#usersTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "ban") {
        confirmAction(
          "Khóa tài khoản này?",
          async function () {
            await api(
              "/api/admin/users/" + encodeURIComponent(id) + "/ban",
              "PUT",
            );
            toast("Đã khóa", "success");
            loadUsers();
          },
          "Khóa",
          "danger",
        );
      } else if (act === "unban") {
        try {
          await api(
            "/api/admin/users/" + encodeURIComponent(id) + "/unban",
            "PUT",
          );
          toast("Đã mở khóa", "success");
          loadUsers();
        } catch (err) {
          toast(err.message, "danger");
        }
      } else if (act === "role") {
        const current = parseInt(btn.getAttribute("data-role"), 10) || 0;
        openModal({
          title: "Đổi vai trò người dùng",
          html:
            '<div class="adm-form-row"><label>Vai trò</label>' +
            '<select class="adm-input" id="modalRoleSel">' +
            '<option value="0"' +
            (current === 0 ? " selected" : "") +
            ">Customer</option>" +
            '<option value="1"' +
            (current === 1 ? " selected" : "") +
            ">Admin</option>" +
            '<option value="2"' +
            (current === 2 ? " selected" : "") +
            ">Seller</option>" +
            "</select></div>",
          confirmText: "Lưu",
          onConfirm: async function () {
            const role = parseInt($("#modalRoleSel").value, 10);
            await api(
              "/api/admin/users/" + encodeURIComponent(id) + "/role",
              "PUT",
              { role: role },
            );
            toast("Đã cập nhật vai trò", "success");
            loadUsers();
          },
        });
      } else if (act === "delete") {
        confirmAction(
          "Xóa tài khoản này? Không thể hoàn tác.",
          async function () {
            await api("/api/admin/users/" + encodeURIComponent(id), "DELETE");
            toast("Đã xóa", "success");
            loadUsers();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · SHOPS
  // ═══════════════════════════════════════════════════════════
  const shopsState = { page: 1, status: -1 };

  TAB_LOADERS.shops = function () {
    loadShops();
  };

  async function loadShops() {
    const tbody = $("#shopsTableBody");
    tbody.innerHTML =
      '<tr><td colspan="7" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: shopsState.page,
        limit: PAGE_SIZE,
        status: shopsState.status !== -1 ? shopsState.status : null,
      });
      const r = await api("/api/shops/admin/stats" + q);
      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (s) {
              const st = SHOP_STATUS[s.status] || {
                label: s.status,
                cls: "adm-badge-gray",
              };
              const actions = [];
              if (s.status === 0)
                actions.push(
                  '<button class="adm-btn-sm adm-btn-success" data-act="approve" data-id="' +
                    escHtml(s.id) +
                    '">Duyệt</button>',
                );
              if (s.status === 1)
                actions.push(
                  '<button class="adm-btn-sm adm-btn-danger" data-act="ban"     data-id="' +
                    escHtml(s.id) +
                    '">Khóa</button>',
                );
              actions.push(
                '<button class="adm-btn-sm" data-act="commission" data-id="' +
                  escHtml(s.id) +
                  '" data-rate="' +
                  s.commissionRate +
                  '">Sửa HH%</button>',
              );

              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(s.shopName) +
                "</strong></td>" +
                "<td>" +
                fmtNum(s.productCount) +
                "</td>" +
                "<td>" +
                fmtNum(s.orderCount) +
                "</td>" +
                "<td>" +
                fmtMoney(s.revenue) +
                "</td>" +
                "<td>" +
                (+s.commissionRate).toFixed(1) +
                "%</td>" +
                '<td><span class="adm-badge ' +
                st.cls +
                '">' +
                st.label +
                "</span></td>" +
                "<td>" +
                actions.join("") +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="7" class="adm-empty">Không có shop</td></tr>';

      renderPager("#shopsPager", r.page || 1, r.totalPages || 1, function (p) {
        shopsState.page = p;
        loadShops();
      });
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="7" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindShopsTab() {
    $("#shopsReloadBtn").addEventListener("click", function () {
      const v = parseInt($("#shopsStatusFilter").value, 10);
      shopsState.status = isNaN(v) ? -1 : v;
      shopsState.page = 1;
      loadShops();
    });
    autoReload("#shopsReloadBtn", [], ["#shopsStatusFilter"]);
    $("#shopsTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      try {
        if (act === "approve") {
          await api(
            "/api/shops/admin/" + encodeURIComponent(id) + "/approve",
            "PUT",
          );
          toast("Đã duyệt shop", "success");
          loadShops();
        } else if (act === "ban") {
          confirmAction(
            "Khóa shop này?",
            async function () {
              await api(
                "/api/shops/admin/" + encodeURIComponent(id) + "/ban",
                "PUT",
              );
              toast("Đã khóa", "success");
              loadShops();
            },
            "Khóa",
            "danger",
          );
        } else if (act === "commission") {
          const rate = parseFloat(btn.getAttribute("data-rate")) || 10;
          openModal({
            title: "Cập nhật tỷ lệ hoa hồng",
            html:
              '<div class="adm-form-row"><label>Tỷ lệ hoa hồng (%)</label>' +
              '<input type="number" min="0" max="50" step="0.5" class="adm-input" id="modalRate" value="' +
              rate +
              '"></div>' +
              '<p class="adm-muted">Áp dụng cho các đơn hàng <b>mới</b> tạo sau khi cập nhật.</p>',
            confirmText: "Lưu",
            onConfirm: async function () {
              const v = parseFloat($("#modalRate").value);
              if (isNaN(v) || v < 0) throw new Error("Tỷ lệ không hợp lệ");
              await api(
                "/api/shops/admin/" + encodeURIComponent(id) + "/commission",
                "PUT",
                { commissionRate: v },
              );
              toast("Đã cập nhật", "success");
              loadShops();
            },
          });
        }
      } catch (err) {
        toast(err.message, "danger");
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · PRODUCTS
  // ═══════════════════════════════════════════════════════════
  const productsState = {
    page: 1,
    search: "",
    categoryId: "",
    minPrice: "",
    maxPrice: "",
  };
  let _categoryCache = null;

  TAB_LOADERS.products = async function () {
    if (!_categoryCache) {
      try {
        _categoryCache = await api("/api/categories");
      } catch (_) {
        _categoryCache = [];
      }
      const sel = $("#productsCategoryFilter");
      _categoryCache.forEach(function (c) {
        const opt = document.createElement("option");
        opt.value = c.id;
        opt.textContent = c.name;
        sel.appendChild(opt);
      });
    }
    loadProducts();
  };

  async function loadProducts() {
    const tbody = $("#productsTableBody");
    tbody.innerHTML =
      '<tr><td colspan="7" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: productsState.page,
        pageSize: PAGE_SIZE,
        keyword: productsState.search || null,
        categoryId: productsState.categoryId || null,
        minPrice: productsState.minPrice !== "" ? productsState.minPrice : null,
        maxPrice: productsState.maxPrice !== "" ? productsState.maxPrice : null,
        isActive: false,
      });
      const r = await api("/api/products" + q);
      const items =
        r.items || r.data || r.products || (Array.isArray(r) ? r : []);
      const total = r.total || r.totalCount || items.length;
      const totalPages = r.totalPages || Math.ceil(total / PAGE_SIZE);

      tbody.innerHTML = items.length
        ? items
            .map(function (p) {
              const status = p.isActive
                ? '<span class="adm-badge adm-badge-success">Hiển thị</span>'
                : '<span class="adm-badge adm-badge-gray">Đã ẩn</span>';
              const toggle = p.isActive
                ? '<button class="adm-btn-sm" data-act="hide" data-id="' +
                  p.id +
                  '">Ẩn</button>'
                : '<button class="adm-btn-sm adm-btn-success" data-act="show" data-id="' +
                  p.id +
                  '">Hiện</button>';
              return (
                "<tr>" +
                "<td>" +
                (p.imageUrl
                  ? '<img class="adm-thumb" src="' +
                    escHtml(p.imageUrl) +
                    '" alt="">'
                  : "") +
                "</td>" +
                "<td><strong>" +
                escHtml(p.name) +
                "</strong></td>" +
                "<td>" +
                fmtMoney(p.price) +
                (p.discountPrice
                  ? '<br><small class="adm-muted">Giảm: ' +
                    fmtMoney(p.discountPrice) +
                    "</small>"
                  : "") +
                "</td>" +
                "<td>" +
                fmtNum(p.stock) +
                "</td>" +
                "<td>" +
                fmtNum(p.soldCount || 0) +
                "</td>" +
                "<td>" +
                status +
                "</td>" +
                "<td>" +
                toggle +
                '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                p.id +
                '">Xóa</button>' +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="7" class="adm-empty">Không có sản phẩm</td></tr>';

      renderPager(
        "#productsPager",
        productsState.page,
        totalPages,
        function (p) {
          productsState.page = p;
          loadProducts();
        },
      );
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="7" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function productForm(p) {
    p = p || {};
    const cats = _categoryCache || [];
    const images = Array.isArray(p.images) ? p.images.slice(0, 5) : [];
    // Loại bỏ ảnh chính khỏi mảng phụ để không lưu trùng
    const extraImages = images.filter(function (u) {
      return u && u !== p.imageUrl;
    });
    return (
      '<div class="adm-form-row"><label>Tên sản phẩm</label>' +
      '<input class="adm-input" id="prdName" value="' +
      escHtml(p.name || "") +
      '"></div>' +
      '<div class="adm-form-grid">' +
      '<div class="adm-form-row"><label>Giá gốc (₫)</label>' +
      '<input type="number" min="0" class="adm-input" id="prdPrice" value="' +
      (p.price || 0) +
      '"></div>' +
      '<div class="adm-form-row"><label>Giá khuyến mãi (₫)</label>' +
      '<input type="number" min="0" class="adm-input" id="prdDiscount" value="' +
      (p.discountPrice || "") +
      '" placeholder="Để trống nếu không có"></div>' +
      '<div class="adm-form-row"><label>Tồn kho</label>' +
      '<input type="number" min="0" class="adm-input" id="prdStock" value="' +
      (p.stock != null ? p.stock : 0) +
      '"></div>' +
      '<div class="adm-form-row"><label>Danh mục</label>' +
      '<select class="adm-input" id="prdCategory">' +
      cats
        .map(function (c) {
          return (
            '<option value="' +
            c.id +
            '"' +
            (c.id === p.categoryId ? " selected" : "") +
            ">" +
            escHtml(c.name) +
            "</option>"
          );
        })
        .join("") +
      "</select></div>" +
      '<div class="adm-form-row"><label>Trạng thái</label>' +
      '<select class="adm-input" id="prdActive">' +
      '<option value="true"' +
      (p.isActive !== false ? " selected" : "") +
      ">Hiển thị</option>" +
      '<option value="false"' +
      (p.isActive === false ? " selected" : "") +
      ">Đã ẩn</option>" +
      "</select></div>" +
      "</div>" +
      '<div class="adm-form-row"><label>Mô tả</label>' +
      '<textarea class="adm-textarea" id="prdDesc" rows="3">' +
      escHtml(p.description || "") +
      "</textarea></div>" +
      '<div class="adm-form-row"><label>Ảnh chính</label>' +
      '<input type="file" id="prdImageFile" accept="image/*">' +
      '<input class="adm-input" id="prdImageUrl" placeholder="URL ảnh chính" value="' +
      escHtml(p.imageUrl || "") +
      '" style="margin-top:6px;">' +
      '<div id="prdImagePreview" style="margin-top:8px;">' +
      (p.imageUrl
        ? '<img src="' +
          escHtml(p.imageUrl) +
          '" style="max-height:120px;border-radius:8px;">'
        : "") +
      "</div>" +
      "</div>" +
      '<div class="adm-form-row"><label>Ảnh phụ (tối đa 4)</label>' +
      '<input type="file" id="prdExtraFile" accept="image/*" multiple>' +
      '<div id="prdExtraList" style="margin-top:8px;display:flex;flex-wrap:wrap;gap:8px;">' +
      extraImages
        .map(function (u) {
          return (
            '<div class="adm-prd-extra" data-url="' +
            escHtml(u) +
            '" style="position:relative;">' +
            '<img src="' +
            escHtml(u) +
            '" style="height:72px;border-radius:6px;">' +
            '<button type="button" class="adm-btn-sm adm-btn-danger" data-rm-extra style="position:absolute;top:2px;right:2px;padding:0 6px;">×</button>' +
            "</div>"
          );
        })
        .join("") +
      "</div>" +
      "</div>"
    );
  }

  function readProductBody() {
    // Ảnh chính → cột imageUrl. Ảnh PHỤ (≤4) → cột images JSON. KHÔNG trùng nhau —
    // GetById sẽ tự ghép [imageUrl] + images, gửi cả 2 vào images sẽ duplicate.
    const main = $("#prdImageUrl").value.trim();
    const extras = Array.from(
      document.querySelectorAll("#prdExtraList .adm-prd-extra"),
    )
      .map(function (el) {
        return el.getAttribute("data-url");
      })
      .filter(function (u) {
        return u && u !== main;
      });
    const body = {
      name: $("#prdName").value.trim(),
      price: parseFloat($("#prdPrice").value) || 0,
      stock: parseInt($("#prdStock").value, 10) || 0,
      categoryId: parseInt($("#prdCategory").value, 10),
      description: $("#prdDesc").value.trim(),
      imageUrl: main,
      isActive: $("#prdActive").value === "true",
      images: extras.slice(0, 4),
    };
    const d = parseFloat($("#prdDiscount").value);
    body.discountPrice = isNaN(d) || d <= 0 ? null : d;
    return body;
  }

  function bindProductImageUploads() {
    const main = $("#prdImageFile");
    const extra = $("#prdExtraFile");
    const list = $("#prdExtraList");
    const confirmBtn = $("#admModalConfirm");

    function lock(busy) {
      if (confirmBtn) {
        confirmBtn.disabled = busy;
        confirmBtn.textContent = busy ? "Đang upload ảnh..." : "Lưu";
      }
    }
    if (main)
      main.addEventListener("change", async function () {
        if (!main.files || !main.files[0]) return;
        lock(true);
        main.disabled = true;
        try {
          const url = await uploadToCloudinary(main.files[0]);
          $("#prdImageUrl").value = url;
          $("#prdImagePreview").innerHTML =
            '<img src="' +
            url +
            '" style="max-height:120px;border-radius:8px;">';
          toast("Đã upload ảnh chính", "success");
        } catch (e) {
          toast(e.message, "danger");
        } finally {
          lock(false);
          main.disabled = false;
        }
      });
    if (extra)
      extra.addEventListener("change", async function () {
        if (!extra.files || !extra.files.length) return;
        lock(true);
        extra.disabled = true;
        try {
          for (let i = 0; i < extra.files.length; i++) {
            if (list.querySelectorAll(".adm-prd-extra").length >= 4) {
              toast("Tối đa 4 ảnh phụ", "warning");
              break;
            }
            const url = await uploadToCloudinary(extra.files[i]);
            const div = document.createElement("div");
            div.className = "adm-prd-extra";
            div.setAttribute("data-url", url);
            div.style.position = "relative";
            div.innerHTML =
              '<img src="' +
              url +
              '" style="height:72px;border-radius:6px;">' +
              '<button type="button" class="adm-btn-sm adm-btn-danger" data-rm-extra style="position:absolute;top:2px;right:2px;padding:0 6px;">×</button>';
            list.appendChild(div);
          }
          extra.value = "";
          toast("Đã upload ảnh phụ", "success");
        } catch (e) {
          toast(e.message, "danger");
        } finally {
          lock(false);
          extra.disabled = false;
        }
      });
    if (list)
      list.addEventListener("click", function (e) {
        const btn = e.target.closest("[data-rm-extra]");
        if (!btn) return;
        btn.closest(".adm-prd-extra").remove();
      });
  }

  async function openProductEditModal(id) {
    if (!_categoryCache) {
      try {
        _categoryCache = await api("/api/categories");
      } catch (_) {
        _categoryCache = [];
      }
    }
    let p;
    try {
      p = await api("/api/products/" + id);
    } catch (e) {
      toast("Không tải được sản phẩm: " + e.message, "danger");
      return;
    }

    openModal({
      title: "Sửa sản phẩm — " + (p.name || "#" + id),
      size: "lg",
      html: productForm(p),
      confirmText: "Lưu",
      onConfirm: async function () {
        const body = readProductBody();
        if (!body.name) throw new Error("Nhập tên sản phẩm");
        if (body.price <= 0) throw new Error("Giá phải lớn hơn 0");
        if (body.stock < 0) throw new Error("Tồn kho không được âm");
        if (!body.categoryId) throw new Error("Chọn danh mục");
        if (body.discountPrice != null && body.discountPrice >= body.price)
          throw new Error("Giá khuyến mãi phải nhỏ hơn giá gốc");
        await api("/api/products/" + id, "PUT", body);
        toast("Đã lưu sản phẩm", "success");
        loadProducts();
      },
    });
    bindProductImageUploads();
  }

  function bindProductsTab() {
    $("#productsReloadBtn").addEventListener("click", function () {
      productsState.search = $("#productsSearch").value.trim();
      productsState.categoryId = $("#productsCategoryFilter").value;
      const minVal = $("#productsMinPrice").value.trim();
      const maxVal = $("#productsMaxPrice").value.trim();
      // Validate khoảng giá — chặn min > max ngay UI để khỏi tốn 1 round-trip API
      if (minVal !== "" && maxVal !== "" && +minVal > +maxVal) {
        toast("Giá từ không được lớn hơn giá đến", "warning");
        return;
      }
      productsState.minPrice = minVal !== "" && +minVal >= 0 ? +minVal : "";
      productsState.maxPrice = maxVal !== "" && +maxVal >= 0 ? +maxVal : "";
      productsState.page = 1;
      loadProducts();
    });
    $("#productsSearch").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#productsReloadBtn").click();
    });
    $("#productsMinPrice").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#productsReloadBtn").click();
    });
    $("#productsMaxPrice").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#productsReloadBtn").click();
    });
    autoReload(
      "#productsReloadBtn",
      ["#productsSearch", "#productsMinPrice", "#productsMaxPrice"],
      ["#productsCategoryFilter"],
    );
    $("#productsTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      try {
        if (act === "edit") {
          openProductEditModal(id);
        } else if (act === "hide") {
          await api("/api/products/" + id + "/hide", "PUT");
          toast("Đã ẩn sản phẩm", "success");
          loadProducts();
        } else if (act === "show") {
          await api("/api/products/" + id + "/show", "PUT");
          toast("Đã hiển thị lại", "success");
          loadProducts();
        } else if (act === "del") {
          confirmAction(
            "Xóa vĩnh viễn sản phẩm này?",
            async function () {
              await api("/api/products/" + id, "DELETE");
              toast("Đã xóa", "success");
              loadProducts();
            },
            "Xóa",
            "danger",
          );
        }
      } catch (err) {
        toast(err.message, "danger");
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · ORDERS
  // ═══════════════════════════════════════════════════════════
  const ordersState = { page: 1, search: "", status: "", payment: "" };

  TAB_LOADERS.orders = function () {
    loadOrders();
  };

  async function loadOrders() {
    const tbody = $("#ordersTableBody");
    tbody.innerHTML =
      '<tr><td colspan="7" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: ordersState.page,
        limit: PAGE_SIZE,
        search: ordersState.search || null,
        status: ordersState.status || null,
        payment: ordersState.payment || null,
      });
      const r = await api("/api/admin/orders" + q);
      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (o) {
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(o.orderCode) +
                "</strong></td>" +
                "<td>" +
                escHtml((o.customer && o.customer.name) || "—") +
                '<br><small class="adm-muted">' +
                escHtml(o.receiverPhone || "") +
                "</small></td>" +
                "<td>" +
                fmtMoney(o.finalAmount) +
                "</td>" +
                "<td>" +
                escHtml(o.paymentMethod || "—") +
                '<br><small class="adm-muted">' +
                escHtml(o.paymentStatus || "") +
                "</small></td>" +
                "<td>" +
                statusBadge(o.status) +
                "</td>" +
                "<td>" +
                fmtDate(o.createdAt) +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm" data-act="view" data-id="' +
                o.orderId +
                '">Chi tiết</button>' +
                (o.status !== "CANCELLED" && o.status !== "COMPLETED"
                  ? '<button class="adm-btn-sm adm-btn-danger" data-act="cancel" data-id="' +
                    o.orderId +
                    '">Hủy</button>'
                  : "") +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="7" class="adm-empty">Không có đơn hàng</td></tr>';

      renderPager("#ordersPager", r.page || 1, r.totalPages || 1, function (p) {
        ordersState.page = p;
        loadOrders();
      });
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="7" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  async function showOrderDetail(orderId) {
    try {
      const o = await api("/api/admin/orders/" + orderId);
      const itemsHtml = (o.subOrders || [])
        .map(function (so) {
          const productRows = (so.items || [])
            .map(function (it) {
              return (
                "<tr><td>" +
                escHtml(it.productName) +
                "</td><td>" +
                it.qty +
                "</td><td>" +
                fmtMoney(it.unitPrice) +
                "</td><td>" +
                fmtMoney(it.subtotal) +
                "</td></tr>"
              );
            })
            .join("");
          return (
            '<div class="adm-card" style="margin-bottom:12px;">' +
            '<div class="adm-card-head" style="display:flex;justify-content:space-between;">' +
            "<span>🏪 " +
            escHtml(so.shopName) +
            " · " +
            statusBadge(so.status) +
            "</span>" +
            "<span>" +
            fmtMoney(so.finalAmount) +
            "</span>" +
            "</div>" +
            '<div class="adm-card-body">' +
            '<table class="adm-table"><thead><tr><th>SP</th><th>SL</th><th>Đơn giá</th><th>TT</th></tr></thead>' +
            "<tbody>" +
            productRows +
            "</tbody></table>" +
            '<div class="adm-muted" style="margin-top:8px;">' +
            "Hàng: " +
            fmtMoney(so.totalAmount) +
            " · Ship: " +
            fmtMoney(so.shippingFee) +
            (so.shopVoucherDiscount > 0
              ? " · Voucher shop: -" + fmtMoney(so.shopVoucherDiscount)
              : "") +
            "</div>" +
            "</div>" +
            "</div>"
          );
        })
        .join("");

      const timelineHtml = (o.statusHistory || [])
        .map(function (h) {
          return (
            '<div style="display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid #f3f0eb;">' +
            "<span>" +
            statusBadge(h.status) +
            " " +
            escHtml(h.note || "") +
            "</span>" +
            '<small class="adm-muted">' +
            fmtDate(h.changedAt) +
            "</small>" +
            "</div>"
          );
        })
        .join("");

      openModal({
        title: "Chi tiết đơn hàng " + o.orderCode,
        size: "lg",
        html:
          '<div class="adm-form-grid" style="margin-bottom:14px;">' +
          "<div><b>Khách hàng:</b> " +
          escHtml(o.customer.name) +
          "<br>" +
          escHtml(o.customer.email) +
          "<br>" +
          escHtml(o.customer.phone || "") +
          "</div>" +
          "<div><b>Trạng thái:</b> " +
          statusBadge(o.status) +
          "<br><b>Thanh toán:</b> " +
          escHtml(o.paymentMethod || "—") +
          " / " +
          escHtml(o.paymentStatus || "") +
          "<br><b>Ngày đặt:</b> " +
          fmtDate(o.createdAt) +
          "</div>" +
          '<div style="grid-column:1/-1;"><b>Địa chỉ:</b> ' +
          escHtml(o.shippingAddress) +
          "<br><b>Người nhận:</b> " +
          escHtml(o.receiverName) +
          " — " +
          escHtml(o.receiverPhone) +
          (o.trackingCode
            ? "<br><b>Mã VC:</b> " + escHtml(o.trackingCode)
            : "") +
          (o.cancelReason
            ? "<br><b>Lý do hủy:</b> " + escHtml(o.cancelReason)
            : "") +
          "</div>" +
          "</div>" +
          '<h4 style="margin:14px 0 8px;">Sub-orders theo shop</h4>' +
          itemsHtml +
          '<h4 style="margin:14px 0 8px;">Tổng kết</h4>' +
          '<div class="adm-form-grid">' +
          "<div>Tạm tính: " +
          fmtMoney(o.totalAmount) +
          "</div>" +
          "<div>Phí ship: " +
          fmtMoney(o.shippingFee) +
          "</div>" +
          "<div>Voucher hệ thống: -" +
          fmtMoney(o.systemVoucherDiscount || 0) +
          "</div>" +
          "<div>Giảm ship: -" +
          fmtMoney(o.freeshipDiscount || 0) +
          "</div>" +
          '<div style="grid-column:1/-1;"><b>Khách trả: ' +
          fmtMoney(o.finalAmount) +
          "</b></div>" +
          "</div>" +
          '<h4 style="margin:14px 0 8px;">Lịch sử trạng thái</h4>' +
          (timelineHtml || '<div class="adm-muted">Chưa có</div>'),
        hideConfirm: true,
        cancelText: "Đóng",
      });
    } catch (e) {
      toast(e.message, "danger");
    }
  }

  function bindOrdersTab() {
    $("#ordersReloadBtn").addEventListener("click", function () {
      ordersState.search = $("#ordersSearch").value.trim();
      ordersState.status = $("#ordersStatusFilter").value;
      ordersState.payment = $("#ordersPaymentFilter").value;
      ordersState.page = 1;
      loadOrders();
    });
    $("#ordersSearch").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#ordersReloadBtn").click();
    });
    autoReload(
      "#ordersReloadBtn",
      ["#ordersSearch"],
      ["#ordersStatusFilter", "#ordersPaymentFilter"],
    );
    $("#ordersTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "view") showOrderDetail(id);
      else if (act === "cancel") {
        openModal({
          title: "Hủy đơn hàng",
          html:
            '<div class="adm-form-row"><label>Lý do hủy</label>' +
            '<textarea class="adm-textarea" id="modalCancelReason" placeholder="Nhập lý do..."></textarea></div>',
          confirmText: "Xác nhận hủy",
          kind: "danger",
          onConfirm: async function () {
            const note = $("#modalCancelReason").value.trim();
            if (!note) throw new Error("Vui lòng nhập lý do");
            await api("/api/admin/orders/" + id + "/status", "PUT", {
              status: "CANCELLED",
              note: note,
            });
            toast("Đã hủy đơn", "success");
            loadOrders();
          },
        });
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · BANK CONFIRM
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.bank = function () {
    loadBankPending();
  };

  async function loadBankPending() {
    const tbody = $("#bankTableBody");
    tbody.innerHTML =
      '<tr><td colspan="6" class="adm-empty">Đang tải...</td></tr>';
    try {
      const list = await api("/api/admin/bank-confirmation/pending");
      $("#bankCountInfo").textContent = list.length + " đơn đang chờ xác nhận";
      tbody.innerHTML = list.length
        ? list
            .map(function (o) {
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(o.orderCode) +
                "</strong></td>" +
                "<td>" +
                escHtml(o.customerName || "—") +
                "</td>" +
                "<td>" +
                escHtml(o.customerEmail || "") +
                "</td>" +
                "<td>" +
                fmtMoney(o.finalAmount) +
                "</td>" +
                "<td>" +
                fmtDate(o.confirmedAt) +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm adm-btn-success" data-act="confirm" data-id="' +
                o.orderId +
                '">Xác nhận</button>' +
                '<button class="adm-btn-sm adm-btn-danger"  data-act="reject"  data-id="' +
                o.orderId +
                '">Từ chối</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="6" class="adm-empty">Không có đơn nào</td></tr>';
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="6" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindBankTab() {
    $("#bankReloadBtn").addEventListener("click", loadBankPending);
    $("#bankTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      try {
        if (act === "confirm") {
          confirmAction(
            "Xác nhận đã nhận tiền cho đơn này?",
            async function () {
              await api(
                "/api/admin/bank-confirmation/" + id + "/confirm",
                "POST",
              );
              toast("Đã xác nhận", "success");
              loadBankPending();
            },
          );
        } else if (act === "reject") {
          openModal({
            title: "Từ chối xác nhận chuyển khoản",
            html:
              '<p class="adm-muted">Đơn sẽ bị hủy và hoàn kho.</p>' +
              '<div class="adm-form-row"><label>Lý do (tuỳ chọn)</label>' +
              '<textarea class="adm-textarea" id="bankRejectNote" placeholder="VD: Không tìm thấy biên lai..."></textarea></div>',
            confirmText: "Từ chối",
            kind: "danger",
            onConfirm: async function () {
              const note = $("#bankRejectNote").value.trim();
              await api(
                "/api/admin/bank-confirmation/" + id + "/reject",
                "POST",
                { note: note },
              );
              toast("Đã từ chối", "warning");
              loadBankPending();
            },
          });
        }
      } catch (err) {
        toast(err.message, "danger");
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · CATEGORIES
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.categories = function () {
    loadCategories();
  };

  async function loadCategories() {
    const tbody = $("#categoriesTableBody");
    tbody.innerHTML =
      '<tr><td colspan="4" class="adm-empty">Đang tải...</td></tr>';
    try {
      const list = await api("/api/categories");
      _categoryCache = list;
      tbody.innerHTML = list.length
        ? list
            .map(function (c) {
              return (
                "<tr>" +
                "<td>" +
                c.id +
                "</td>" +
                "<td><strong>" +
                escHtml(c.name) +
                "</strong></td>" +
                "<td>" +
                escHtml(c.description || "") +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm" data-act="edit" data-id="' +
                c.id +
                '" data-name="' +
                escHtml(c.name) +
                '" data-desc="' +
                escHtml(c.description || "") +
                '">Sửa</button>' +
                '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                c.id +
                '">Xóa</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="4" class="adm-empty">Chưa có danh mục</td></tr>';
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="4" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function categoryForm(c) {
    return (
      '<div class="adm-form-row"><label>Tên danh mục</label>' +
      '<input class="adm-input" id="catName" value="' +
      escHtml((c && c.name) || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Mô tả</label>' +
      '<textarea class="adm-textarea" id="catDesc">' +
      escHtml((c && c.description) || "") +
      "</textarea></div>"
    );
  }

  function bindCategoriesTab() {
    $("#catNewBtn").addEventListener("click", function () {
      openModal({
        title: "Thêm danh mục",
        html: categoryForm(null),
        confirmText: "Tạo",
        onConfirm: async function () {
          const name = $("#catName").value.trim();
          if (!name) throw new Error("Nhập tên danh mục");
          await api("/api/categories", "POST", {
            name: name,
            description: $("#catDesc").value.trim(),
          });
          toast("Đã tạo", "success");
          loadCategories();
        },
      });
    });
    $("#categoriesTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "edit") {
        const c = {
          name: btn.getAttribute("data-name"),
          description: btn.getAttribute("data-desc"),
        };
        openModal({
          title: "Sửa danh mục",
          html: categoryForm(c),
          confirmText: "Lưu",
          onConfirm: async function () {
            const name = $("#catName").value.trim();
            if (!name) throw new Error("Nhập tên danh mục");
            await api("/api/categories/" + id, "PUT", {
              name: name,
              description: $("#catDesc").value.trim(),
            });
            toast("Đã lưu", "success");
            loadCategories();
          },
        });
      } else if (act === "del") {
        confirmAction(
          "Xóa danh mục này?",
          async function () {
            await api("/api/categories/" + id, "DELETE");
            toast("Đã xóa", "success");
            loadCategories();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · BANNERS
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.banners = function () {
    loadBanners();
  };

  async function loadBanners() {
    const tbody = $("#bannersTableBody");
    tbody.innerHTML =
      '<tr><td colspan="7" class="adm-empty">Đang tải...</td></tr>';
    try {
      const list = await api("/api/Banners/all");
      tbody.innerHTML = list.length
        ? list
            .map(function (b) {
              const status = b.isActive
                ? '<span class="adm-badge adm-badge-success">Hiện</span>'
                : '<span class="adm-badge adm-badge-gray">Ẩn</span>';
              return (
                "<tr>" +
                "<td>" +
                (b.imageUrl
                  ? '<img class="adm-thumb" src="' + escHtml(b.imageUrl) + '">'
                  : "") +
                "</td>" +
                "<td><strong>" +
                escHtml(b.title) +
                "</strong></td>" +
                "<td>" +
                escHtml(b.subtitle || "") +
                "</td>" +
                "<td>" +
                escHtml(b.linkUrl || "") +
                "</td>" +
                "<td>" +
                b.sortOrder +
                "</td>" +
                "<td>" +
                status +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm" data-act="edit" data-id="' +
                b.id +
                '">Sửa</button>' +
                '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                b.id +
                '">Xóa</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="7" class="adm-empty">Chưa có banner</td></tr>';
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="7" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bannerForm(b) {
    b = b || {};
    return (
      '<div class="adm-form-row"><label>Tiêu đề</label><input class="adm-input" id="bnTitle" value="' +
      escHtml(b.title || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Mô tả</label><input class="adm-input" id="bnSubtitle" value="' +
      escHtml(b.subtitle || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Liên kết</label><input class="adm-input" id="bnLinkUrl" value="' +
      escHtml(b.linkUrl || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Nút CTA</label><input class="adm-input" id="bnButtonText" value="' +
      escHtml(b.buttonText || "") +
      '"></div>' +
      '<div class="adm-form-grid">' +
      '<div class="adm-form-row"><label>Thứ tự</label><input type="number" class="adm-input" id="bnSortOrder" value="' +
      (b.sortOrder || 0) +
      '"></div>' +
      '<div class="adm-form-row"><label>Trạng thái</label><select class="adm-input" id="bnIsActive">' +
      '<option value="true"' +
      (b.isActive !== false ? " selected" : "") +
      ">Hiện</option>" +
      '<option value="false"' +
      (b.isActive === false ? " selected" : "") +
      ">Ẩn</option>" +
      "</select></div>" +
      "</div>" +
      '<div class="adm-form-row"><label>Ảnh banner</label>' +
      '<input type="file" id="bnImageFile" accept="image/*">' +
      '<input class="adm-input" id="bnImageUrl" placeholder="URL ảnh" value="' +
      escHtml(b.imageUrl || "") +
      '" style="margin-top:6px;">' +
      '<div id="bnImagePreview" style="margin-top:8px;">' +
      (b.imageUrl
        ? '<img src="' +
          escHtml(b.imageUrl) +
          '" style="max-height:120px;border-radius:8px;">'
        : "") +
      "</div>" +
      "</div>"
    );
  }

  function bindBannerImageUpload() {
    const f = $("#bnImageFile");
    if (!f) return;
    f.addEventListener("change", async function () {
      if (!f.files || !f.files[0]) return;
      // Disable confirm button của modal trong khi upload, tránh submit với imageUrl rỗng
      const confirmBtn = $("#admModalConfirm");
      const origConfirmText = confirmBtn ? confirmBtn.textContent : "";
      if (confirmBtn) {
        confirmBtn.disabled = true;
        confirmBtn.textContent = "Đang upload ảnh...";
      }
      f.disabled = true;
      try {
        toast("Đang upload...", "info");
        const url = await uploadToCloudinary(f.files[0]);
        $("#bnImageUrl").value = url;
        $("#bnImagePreview").innerHTML =
          '<img src="' + url + '" style="max-height:120px;border-radius:8px;">';
        toast("Upload thành công", "success");
      } catch (e) {
        toast(e.message, "danger");
      } finally {
        f.disabled = false;
        if (confirmBtn) {
          confirmBtn.disabled = false;
          confirmBtn.textContent = origConfirmText;
        }
      }
    });
  }

  function readBannerFormBody() {
    return {
      title: $("#bnTitle").value.trim(),
      subtitle: $("#bnSubtitle").value.trim(),
      imageUrl: $("#bnImageUrl").value.trim(),
      linkUrl: $("#bnLinkUrl").value.trim(),
      buttonText: $("#bnButtonText").value.trim(),
      sortOrder: parseInt($("#bnSortOrder").value, 10) || 0,
      isActive: $("#bnIsActive").value === "true",
    };
  }

  function bindBannersTab() {
    $("#bannerNewBtn").addEventListener("click", function () {
      openModal({
        title: "Thêm banner",
        size: "lg",
        html: bannerForm(null),
        confirmText: "Tạo",
        onConfirm: async function () {
          const body = readBannerFormBody();
          if (!body.title) throw new Error("Nhập tiêu đề");
          await api("/api/Banners", "POST", body);
          toast("Đã tạo", "success");
          loadBanners();
        },
      });
      bindBannerImageUpload();
    });
    $("#bannersTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "edit") {
        try {
          const b = await api("/api/Banners/" + id);
          openModal({
            title: "Sửa banner",
            size: "lg",
            html: bannerForm(b),
            confirmText: "Lưu",
            onConfirm: async function () {
              const body = readBannerFormBody();
              await api("/api/Banners/" + id, "PUT", body);
              toast("Đã lưu", "success");
              loadBanners();
            },
          });
          bindBannerImageUpload();
        } catch (err) {
          toast("Không tải được banner: " + err.message, "danger");
        }
      } else if (act === "del") {
        confirmAction(
          "Xóa banner này?",
          async function () {
            await api("/api/Banners/" + id, "DELETE");
            toast("Đã xóa", "success");
            loadBanners();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · FLASH SALE
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.flashsale = function () {
    loadFlash();
  };

  async function loadFlash() {
    const tbody = $("#flashTableBody");
    tbody.innerHTML =
      '<tr><td colspan="7" class="adm-empty">Đang tải...</td></tr>';
    try {
      const r = await api(
        "/api/flashsale/admin/list?page=1&limit=" + PAGE_SIZE,
      );
      const list = r.items || (Array.isArray(r) ? r : []);
      tbody.innerHTML = list.length
        ? list
            .map(function (f) {
              const status = f.isActive
                ? '<span class="adm-badge adm-badge-success">Bật</span>'
                : '<span class="adm-badge adm-badge-gray">Tắt</span>';
              return (
                "<tr>" +
                "<td>" +
                f.id +
                "</td>" +
                "<td><strong>" +
                escHtml(f.name) +
                "</strong></td>" +
                "<td>" +
                fmtDate(f.startTime) +
                "</td>" +
                "<td>" +
                fmtDate(f.endTime) +
                "</td>" +
                "<td>" +
                (f.productCount != null ? f.productCount : "—") +
                "</td>" +
                "<td>" +
                status +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm" data-act="manage" data-id="' +
                f.id +
                '">Quản lý SP</button>' +
                '<button class="adm-btn-sm" data-act="edit" data-id="' +
                f.id +
                '" data-name="' +
                escHtml(f.name) +
                '" data-start="' +
                escHtml(f.startTime) +
                '" data-end="' +
                escHtml(f.endTime) +
                '">Sửa</button>' +
                '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                f.id +
                '">Xóa</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="7" class="adm-empty">Chưa có flash sale</td></tr>';
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="7" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function flashForm(f) {
    f = f || {};
    const fmtForInput = function (s) {
      if (!s) return "";
      const d = new Date(s);
      if (isNaN(d.getTime())) return "";
      d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
      return d.toISOString().slice(0, 16);
    };
    return (
      '<div class="adm-form-row"><label>Tên</label><input class="adm-input" id="fsName" value="' +
      escHtml(f.name || "") +
      '"></div>' +
      '<div class="adm-form-grid">' +
      '<div class="adm-form-row"><label>Bắt đầu</label><input type="datetime-local" class="adm-input" id="fsStart" value="' +
      fmtForInput(f.startTime) +
      '"></div>' +
      '<div class="adm-form-row"><label>Kết thúc</label><input type="datetime-local" class="adm-input" id="fsEnd" value="' +
      fmtForInput(f.endTime) +
      '"></div>' +
      "</div>"
    );
  }

  async function showFlashProducts(saleId) {
    try {
      const fs = await api("/api/flashsale/" + saleId);
      const productsHtml = (fs.products || [])
        .map(function (p) {
          return (
            "<tr>" +
            "<td>" +
            escHtml(p.productName || "SP #" + p.productId) +
            "</td>" +
            "<td>" +
            fmtMoney(p.originalPrice) +
            "</td>" +
            "<td>" +
            fmtMoney(p.salePrice) +
            "</td>" +
            "<td>" +
            p.quantity +
            "</td>" +
            "<td>" +
            p.soldCount +
            "</td>" +
            '<td><button class="adm-btn-sm adm-btn-danger" data-rm="' +
            p.productId +
            '">Xóa</button></td>' +
            "</tr>"
          );
        })
        .join("");

      openModal({
        title: "Sản phẩm trong " + fs.name,
        size: "lg",
        html:
          '<div class="adm-form-row"><label>Thêm sản phẩm (ID)</label>' +
          '<div style="display:flex;gap:8px;">' +
          '<input class="adm-input" id="fsAddProdId" placeholder="Product ID" style="flex:1;">' +
          '<input class="adm-input" id="fsAddPercent" type="number" min="1" max="90" placeholder="% giảm" style="width:110px;">' +
          '<button class="adm-btn-primary" id="fsAddBtn">+ Thêm</button>' +
          "</div></div>" +
          '<table class="adm-table"><thead><tr><th>SP</th><th>Giá gốc</th><th>Sale</th><th>SL</th><th>Đã bán</th><th></th></tr></thead>' +
          '<tbody id="fsProdTbody">' +
          (productsHtml ||
            '<tr><td colspan="6" class="adm-empty">Chưa có sản phẩm</td></tr>') +
          "</tbody></table>",
        hideConfirm: true,
        cancelText: "Đóng",
      });

      $("#fsAddBtn").addEventListener("click", async function () {
        const pid = parseInt($("#fsAddProdId").value, 10);
        const percent = parseInt($("#fsAddPercent").value, 10);
        if (!pid || !percent) {
          toast("Nhập đầy đủ", "warning");
          return;
        }
        try {
          await api("/api/flashsale/" + saleId + "/products", "POST", {
            productId: pid,
            discountPercent: percent,
          });
          toast("Đã thêm", "success");
          closeModal();
          showFlashProducts(saleId);
        } catch (e) {
          toast(e.message, "danger");
        }
      });
      $("#fsProdTbody").addEventListener("click", async function (e) {
        const b = e.target.closest("button[data-rm]");
        if (!b) return;
        const pid = b.getAttribute("data-rm");
        try {
          await api("/api/flashsale/" + saleId + "/products/" + pid, "DELETE");
          toast("Đã xóa", "success");
          closeModal();
          showFlashProducts(saleId);
        } catch (err) {
          toast(err.message, "danger");
        }
      });
    } catch (e) {
      toast(e.message, "danger");
    }
  }

  function bindFlashTab() {
    $("#flashNewBtn").addEventListener("click", function () {
      openModal({
        title: "Tạo flash sale",
        html: flashForm(null),
        confirmText: "Tạo",
        onConfirm: async function () {
          const body = {
            name: $("#fsName").value.trim(),
            startTime: $("#fsStart").value,
            endTime: $("#fsEnd").value,
          };
          if (!body.name || !body.startTime || !body.endTime)
            throw new Error("Điền đủ thông tin");
          await api("/api/flashsale", "POST", body);
          toast("Đã tạo", "success");
          loadFlash();
        },
      });
    });
    $("#flashTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "manage") {
        showFlashProducts(id);
      } else if (act === "edit") {
        const f = {
          name: btn.getAttribute("data-name"),
          startTime: btn.getAttribute("data-start"),
          endTime: btn.getAttribute("data-end"),
        };
        openModal({
          title: "Sửa flash sale",
          html: flashForm(f),
          confirmText: "Lưu",
          onConfirm: async function () {
            const body = {
              name: $("#fsName").value.trim(),
              startTime: $("#fsStart").value,
              endTime: $("#fsEnd").value,
            };
            await api("/api/flashsale/" + id, "PUT", body);
            toast("Đã lưu", "success");
            loadFlash();
          },
        });
      } else if (act === "del") {
        confirmAction(
          "Xóa flash sale này?",
          async function () {
            await api("/api/flashsale/" + id, "DELETE");
            toast("Đã xóa", "success");
            loadFlash();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · VOUCHERS
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.vouchers = function () {
    loadVouchers();
  };

  async function loadVouchers() {
    const tbody = $("#vouchersTableBody");
    tbody.innerHTML =
      '<tr><td colspan="9" class="adm-empty">Đang tải...</td></tr>';
    try {
      const list = await api("/api/Vouchers?page=1&limit=200");
      const arr = Array.isArray(list) ? list : list.items || [];
      const filter = $("#vouchersFilter").value;
      const filtered = arr.filter(function (v) {
        if (filter === "system") return !v.shopId;
        if (filter === "shop") return !!v.shopId;
        return true;
      });
      tbody.innerHTML = filtered.length
        ? filtered
            .map(function (v) {
              const value =
                v.discountType === "percent"
                  ? v.discountValue + "%"
                  : fmtMoney(v.discountValue);
              const kind = v.shopId
                ? '<span class="adm-badge adm-badge-purple">Shop</span>'
                : '<span class="adm-badge adm-badge-info">Hệ thống</span>';
              const active = v.isActive
                ? '<span class="adm-badge adm-badge-success">Hoạt động</span>'
                : '<span class="adm-badge adm-badge-gray">Tắt</span>';
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(v.code) +
                "</strong></td>" +
                "<td>" +
                escHtml(v.description || "") +
                "</td>" +
                "<td>" +
                (v.discountType === "percent" ? "%" : "₫") +
                "</td>" +
                "<td>" +
                value +
                "</td>" +
                "<td>" +
                fmtMoney(v.minOrderAmount || 0) +
                "</td>" +
                "<td>" +
                (v.usedCount || 0) +
                " / " +
                (v.usageLimit || "∞") +
                "</td>" +
                "<td>" +
                kind +
                "</td>" +
                "<td>" +
                active +
                "</td>" +
                "<td>" +
                (v.shopId
                  ? ""
                  : '<button class="adm-btn-sm" data-act="edit" data-id="' +
                    v.id +
                    '">Sửa</button>' +
                    '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                    v.id +
                    '">Xóa</button>') +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="9" class="adm-empty">Không có voucher</td></tr>';
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="9" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function voucherForm(v) {
    v = v || {};
    const dt = v.discountType || "percent";
    return (
      '<div class="adm-form-row"><label>Mã (CODE)</label>' +
      '<input class="adm-input" id="vcCode" value="' +
      escHtml(v.code || "") +
      '" ' +
      (v.id ? "readonly" : "") +
      "></div>" +
      '<div class="adm-form-row"><label>Mô tả</label><input class="adm-input" id="vcDesc" value="' +
      escHtml(v.description || "") +
      '"></div>' +
      '<div class="adm-form-grid">' +
      '<div class="adm-form-row"><label>Loại</label><select class="adm-input" id="vcType">' +
      '<option value="percent"' +
      (dt === "percent" ? " selected" : "") +
      ">% giảm</option>" +
      '<option value="fixed"' +
      (dt === "fixed" ? " selected" : "") +
      ">Giảm cố định</option>" +
      "</select></div>" +
      '<div class="adm-form-row"><label>Giá trị</label><input type="number" class="adm-input" id="vcValue" value="' +
      (v.discountValue || 0) +
      '"></div>' +
      '<div class="adm-form-row"><label>Đơn tối thiểu (₫)</label><input type="number" class="adm-input" id="vcMin" value="' +
      (v.minOrderAmount || 0) +
      '"></div>' +
      '<div class="adm-form-row"><label>Giảm tối đa (₫)</label><input type="number" class="adm-input" id="vcMax" value="' +
      (v.maxDiscount || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Giới hạn lượt</label><input type="number" class="adm-input" id="vcLimit" value="' +
      (v.usageLimit || "") +
      '"></div>' +
      '<div class="adm-form-row"><label>Trạng thái</label><select class="adm-input" id="vcActive">' +
      '<option value="true"' +
      (v.isActive !== false ? " selected" : "") +
      ">Hoạt động</option>" +
      '<option value="false"' +
      (v.isActive === false ? " selected" : "") +
      ">Tắt</option>" +
      "</select></div>" +
      "</div>"
    );
  }

  function readVoucherBody() {
    return {
      code: $("#vcCode").value.trim().toUpperCase(),
      description: $("#vcDesc").value.trim(),
      discountType: $("#vcType").value,
      discountValue: parseFloat($("#vcValue").value) || 0,
      minOrderAmount: parseFloat($("#vcMin").value) || 0,
      maxDiscount: $("#vcMax").value ? parseFloat($("#vcMax").value) : null,
      usageLimit: $("#vcLimit").value
        ? parseInt($("#vcLimit").value, 10)
        : null,
      isActive: $("#vcActive").value === "true",
    };
  }

  function bindVouchersTab() {
    $("#vouchersFilter").addEventListener("change", loadVouchers);
    $("#voucherNewBtn").addEventListener("click", function () {
      openModal({
        title: "Tạo voucher hệ thống",
        html: voucherForm(null),
        confirmText: "Tạo",
        onConfirm: async function () {
          const body = readVoucherBody();
          if (!body.code) throw new Error("Nhập CODE");
          await api("/api/Vouchers", "POST", body);
          toast("Đã tạo", "success");
          loadVouchers();
        },
      });
    });
    $("#vouchersTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "edit") {
        const list = await api("/api/Vouchers?page=1&limit=200");
        const arr = Array.isArray(list) ? list : list.items || [];
        const v = arr.find(function (x) {
          return String(x.id) === String(id);
        });
        openModal({
          title: "Sửa voucher",
          html: voucherForm(v),
          confirmText: "Lưu",
          onConfirm: async function () {
            const body = readVoucherBody();
            await api("/api/Vouchers/" + id, "PUT", body);
            toast("Đã lưu", "success");
            loadVouchers();
          },
        });
      } else if (act === "del") {
        confirmAction(
          "Xóa voucher này?",
          async function () {
            await api("/api/Vouchers/" + id, "DELETE");
            toast("Đã xóa", "success");
            loadVouchers();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · COMMISSION
  // ═══════════════════════════════════════════════════════════
  const commissionState = { page: 1 };

  TAB_LOADERS.commission = function () {
    loadCommission();
  };

  async function loadCommission() {
    try {
      const [summary, shops] = await Promise.all([
        api("/api/admin/commission/summary"),
        api(
          "/api/admin/commission/shops?page=" +
            commissionState.page +
            "&limit=" +
            PAGE_SIZE,
        ),
      ]);

      $("#comTotalRevenue").textContent = fmtMoney(summary.totalRevenue);
      $("#comTotalCommission").textContent = fmtMoney(summary.totalCommission);
      $("#comTotalPaid").textContent = fmtMoney(summary.totalPaidOut);
      $("#comPendingRelease").textContent = fmtMoney(summary.pendingRelease);

      const tbody = $("#commissionShopsBody");
      const items = shops.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (s) {
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(s.shopName) +
                "</strong></td>" +
                "<td>" +
                (+s.commissionRate).toFixed(1) +
                "%</td>" +
                "<td>" +
                fmtMoney(s.revenue) +
                "</td>" +
                "<td>" +
                fmtMoney(s.commissionAmount) +
                "</td>" +
                "<td>" +
                fmtMoney(s.netRevenue) +
                "</td>" +
                "<td>" +
                fmtMoney(s.walletBalance) +
                "</td>" +
                "<td>" +
                fmtMoney(s.pendingPayout) +
                "</td>" +
                "<td>" +
                (s.walletBalance > 0
                  ? '<button class="adm-btn-sm adm-btn-success" data-act="payout" data-id="' +
                    escHtml(s.shopId) +
                    '" data-name="' +
                    escHtml(s.shopName) +
                    '" data-bal="' +
                    s.walletBalance +
                    '">Ghi nhận payout</button>'
                  : "") +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="8" class="adm-empty">Chưa có dữ liệu</td></tr>';

      renderPager(
        "#commissionPager",
        shops.page || 1,
        shops.totalPages || 1,
        function (p) {
          commissionState.page = p;
          loadCommission();
        },
      );
    } catch (e) {
      toast("Lỗi tải Hoa Hồng: " + e.message, "danger");
    }
  }

  function bindCommissionTab() {
    $("#commissionShopsBody").addEventListener("click", function (e) {
      const btn = e.target.closest("button[data-act='payout']");
      if (!btn) return;
      const shopId = btn.getAttribute("data-id");
      const name = btn.getAttribute("data-name");
      const bal = parseFloat(btn.getAttribute("data-bal")) || 0;
      openModal({
        title: "Payout cho " + name,
        html:
          '<div class="adm-form-row"><label>Số tiền (₫) — Số dư hiện tại: ' +
          fmtMoney(bal) +
          "</label>" +
          '<input type="number" class="adm-input" id="poAmount" value="' +
          bal +
          '" min="1"></div>' +
          '<div class="adm-form-row"><label>Ghi chú</label><textarea class="adm-textarea" id="poNote" placeholder="VD: Chuyển khoản BIDV..."></textarea></div>',
        confirmText: "Ghi nhận",
        onConfirm: async function () {
          const amount = parseFloat($("#poAmount").value);
          if (!amount || amount <= 0) throw new Error("Số tiền không hợp lệ");
          await api(
            "/api/admin/commission/payout/" + encodeURIComponent(shopId),
            "POST",
            {
              amount: amount,
              note: $("#poNote").value.trim(),
              payoutDate: new Date().toISOString(),
            },
          );
          toast("Đã ghi nhận payout", "success");
          loadCommission();
        },
      });
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · WALLET (PAYOUT)
  // ═══════════════════════════════════════════════════════════
  TAB_LOADERS.wallet = function () {
    loadWallet();
  };

  async function loadWallet() {
    try {
      const [overview, tx] = await Promise.all([
        api("/api/admin/wallet/overview"),
        api("/api/admin/wallet/transactions?page=1&limit=20"),
      ]);

      $("#walTotalBalance").textContent = fmtMoney(overview.totalBalance);
      $("#walPendingCount").textContent = fmtNum(overview.pendingReleaseCount);
      $("#walPendingAmount").textContent = fmtMoney(
        overview.pendingReleaseAmount,
      );
      $("#walTotalWithdrawn").textContent = fmtMoney(overview.totalWithdrawn);

      const wallets = overview.wallets || [];
      $("#walletShopsBody").innerHTML = wallets.length
        ? wallets
            .map(function (w) {
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(w.shopName || "—") +
                "</strong></td>" +
                "<td>" +
                fmtMoney(w.balance) +
                "</td>" +
                "<td>" +
                fmtMoney(w.totalEarned) +
                "</td>" +
                "<td>" +
                fmtMoney(w.totalWithdrawn) +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm" data-act="adjust" ' +
                'data-id="' +
                escHtml(w.shopId) +
                '" ' +
                'data-name="' +
                escHtml(w.shopName || "") +
                '" ' +
                'data-bal="' +
                w.balance +
                '">Điều chỉnh</button>' +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="5" class="adm-empty">Chưa có ví</td></tr>';

      const items = tx.items || [];
      $("#walletTxBody").innerHTML = items.length
        ? items
            .map(function (t) {
              const cls =
                t.type === "EARNING"
                  ? "adm-badge-success"
                  : t.type === "REFUND"
                    ? "adm-badge-danger"
                    : "adm-badge-info";
              return (
                "<tr>" +
                '<td><span class="adm-badge ' +
                cls +
                '">' +
                escHtml(t.type) +
                "</span></td>" +
                "<td>" +
                escHtml(t.shopName || "—") +
                "</td>" +
                "<td>" +
                fmtMoney(t.amount) +
                "</td>" +
                "<td>" +
                fmtDate(t.createdAt) +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="4" class="adm-empty">Chưa có giao dịch</td></tr>';
    } catch (e) {
      toast("Lỗi tải Ví: " + e.message, "danger");
    }
  }

  function bindWalletTab() {
    $("#walReloadBtn").addEventListener("click", loadWallet);
    $("#walReleaseBtn").addEventListener("click", function () {
      const pending = $("#walPendingAmount").textContent;
      confirmAction(
        "Giải ngân tất cả đơn ở trạng thái WAITING_RELEASE (" + pending + ")?",
        async function () {
          try {
            // Per Admin Guide §2.2: endpoint loops SubOrders internally.
            const r = await api("/api/admin/wallet/release-payouts", "POST");
            toast(
              "Đã giải ngân " +
                r.released +
                " sub-đơn (" +
                fmtMoney(r.amount) +
                ")",
              "success",
            );
            loadWallet();
          } catch (e) {
            toast(e.message, "danger");
          }
        },
      );
    });
    $("#walletShopsBody").addEventListener("click", function (e) {
      const btn = e.target.closest("button[data-act='adjust']");
      if (!btn) return;
      const shopId = btn.getAttribute("data-id");
      const name = btn.getAttribute("data-name");
      const bal = parseFloat(btn.getAttribute("data-bal")) || 0;
      openModal({
        title: "Điều chỉnh ví — " + name,
        html:
          '<div class="adm-muted" style="margin-bottom:10px;">Số dư hiện tại: <b>' +
          fmtMoney(bal) +
          "</b></div>" +
          '<div class="adm-form-grid">' +
          '<div class="adm-form-row"><label>Hướng</label>' +
          '<select class="adm-input" id="adjDir">' +
          '<option value="add">Cộng vào ví (+)</option>' +
          '<option value="sub">Trừ khỏi ví (−)</option>' +
          "</select></div>" +
          '<div class="adm-form-row"><label>Số tiền (₫)</label>' +
          '<input type="number" min="1" class="adm-input" id="adjAmount" value="0"></div>' +
          "</div>" +
          '<div class="adm-form-row"><label>Lý do (bắt buộc)</label>' +
          '<textarea class="adm-textarea" id="adjNote" placeholder="VD: Hoàn tiền sự cố giao hàng / phạt vi phạm..."></textarea></div>',
        confirmText: "Xác nhận",
        onConfirm: async function () {
          const dir = $("#adjDir").value;
          const amount = Math.abs(parseFloat($("#adjAmount").value) || 0);
          const note = $("#adjNote").value.trim();
          if (amount <= 0) throw new Error("Số tiền không hợp lệ");
          if (!note) throw new Error("Vui lòng nhập lý do");
          const r = await api("/api/admin/wallet/adjust", "POST", {
            shopId: shopId,
            amount: dir === "sub" ? -amount : amount,
            note: note,
          });
          toast(r.message || "Đã điều chỉnh ví", "success");
          loadWallet();
        },
      });
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · DISPUTES
  // ═══════════════════════════════════════════════════════════
  const disputesState = { page: 1, status: "" };

  TAB_LOADERS.disputes = function () {
    loadDisputes();
  };

  async function loadDisputes() {
    const tbody = $("#disputesTableBody");
    tbody.innerHTML =
      '<tr><td colspan="8" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: disputesState.page,
        limit: PAGE_SIZE,
        status: disputesState.status || null,
      });
      const r = await api("/api/disputes" + q);
      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (d) {
              const st =
                d.status === "OPEN"
                  ? '<span class="adm-badge adm-badge-warning">Mới mở</span>'
                  : d.status === "PROCESSING"
                    ? '<span class="adm-badge adm-badge-info">Đang xử lý</span>'
                    : '<span class="adm-badge adm-badge-success">Đã giải quyết</span>';
              return (
                "<tr>" +
                "<td>#" +
                d.id +
                "</td>" +
                "<td>" +
                escHtml(d.orderCode || "ORD-" + d.orderId) +
                "</td>" +
                "<td>" +
                escHtml(d.customerName || "—") +
                "</td>" +
                "<td>" +
                escHtml(d.reason) +
                "</td>" +
                "<td>" +
                st +
                "</td>" +
                "<td>" +
                fmtMoney(d.refundAmount || 0) +
                "</td>" +
                "<td>" +
                fmtDate(d.createdAt) +
                "</td>" +
                "<td>" +
                (d.status === "OPEN"
                  ? '<button class="adm-btn-sm" data-act="process" data-id="' +
                    d.id +
                    '">Bắt đầu xử lý</button>'
                  : "") +
                (d.status !== "RESOLVED"
                  ? '<button class="adm-btn-sm adm-btn-success" data-act="resolve" data-id="' +
                    d.id +
                    '">Giải quyết</button>'
                  : "") +
                '<button class="adm-btn-sm" data-act="view" data-id="' +
                d.id +
                '">Chi tiết</button>' +
                "</td></tr>"
              );
            })
            .join("")
        : '<tr><td colspan="8" class="adm-empty">Không có khiếu nại</td></tr>';

      renderPager(
        "#disputesPager",
        r.page || 1,
        r.totalPages || 1,
        function (p) {
          disputesState.page = p;
          loadDisputes();
        },
      );
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="8" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindDisputesTab() {
    $("#disputesReloadBtn").addEventListener("click", function () {
      disputesState.status = $("#disputesStatusFilter").value;
      disputesState.page = 1;
      loadDisputes();
    });
    autoReload("#disputesReloadBtn", [], ["#disputesStatusFilter"]);
    $("#disputesTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      try {
        if (act === "process") {
          await api("/api/disputes/" + id + "/process", "PUT");
          toast("Đã chuyển sang đang xử lý", "success");
          loadDisputes();
        } else if (act === "resolve") {
          openModal({
            title: "Giải quyết khiếu nại",
            html:
              '<div class="adm-form-row"><label>Số tiền hoàn (₫)</label><input type="number" class="adm-input" id="dRefund" value="0" min="0"></div>' +
              '<div class="adm-form-row"><label>Bên thắng</label><select class="adm-input" id="dFavor"><option value="true">Customer</option><option value="false">Seller</option></select></div>' +
              '<div class="adm-form-row"><label>Kết luận</label><textarea class="adm-textarea" id="dResolution"></textarea></div>',
            confirmText: "Đóng vụ",
            onConfirm: async function () {
              const body = {
                refundAmount: parseFloat($("#dRefund").value) || 0,
                favorCustomer: $("#dFavor").value === "true",
                resolution: $("#dResolution").value.trim(),
              };
              await api("/api/disputes/" + id + "/resolve", "PUT", body);
              toast("Đã giải quyết", "success");
              loadDisputes();
            },
          });
        } else if (act === "view") {
          const d = await api("/api/disputes/" + id);
          openModal({
            title: "Chi tiết khiếu nại #" + d.id,
            size: "lg",
            html:
              "<p><b>Đơn:</b> " +
              escHtml(d.orderCode || "#" + d.orderId) +
              " · <b>Trạng thái đơn:</b> " +
              statusBadge(d.orderStatus) +
              "</p>" +
              "<p><b>Khách:</b> " +
              escHtml(d.customerName) +
              " (" +
              escHtml(d.customerEmail || "") +
              ")</p>" +
              "<p><b>Lý do:</b> " +
              escHtml(d.reason) +
              "</p>" +
              "<p><b>Mô tả:</b><br>" +
              escHtml(d.description || "") +
              "</p>" +
              (d.evidence
                ? "<p><b>Bằng chứng:</b> " + escHtml(d.evidence) + "</p>"
                : "") +
              (d.resolution
                ? "<p><b>Kết luận:</b> " +
                  escHtml(d.resolution) +
                  "<br>Hoàn " +
                  fmtMoney(d.refundAmount) +
                  " · Bên thắng: " +
                  (d.favorCustomer ? "Customer" : "Seller") +
                  "</p>"
                : ""),
            hideConfirm: true,
            cancelText: "Đóng",
          });
        }
      } catch (err) {
        toast(err.message, "danger");
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · Q&A
  // ═══════════════════════════════════════════════════════════
  const qnaState = { page: 1, search: "", answered: "" };

  TAB_LOADERS.qna = function () {
    loadQnA();
  };

  async function loadQnA() {
    const tbody = $("#qnaTableBody");
    tbody.innerHTML =
      '<tr><td colspan="5" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: qnaState.page,
        limit: PAGE_SIZE,
        search: qnaState.search || null,
        answered: qnaState.answered !== "" ? qnaState.answered : null,
      });
      const [r, st] = await Promise.all([
        api("/api/admin/qna" + q),
        api("/api/admin/qna/stats").catch(function () {
          return null;
        }),
      ]);

      if (st) {
        $("#qnaStatsInfo").textContent =
          "Tổng " +
          fmtNum(st.total) +
          " · Đã trả lời " +
          fmtNum(st.answered) +
          " · Chờ " +
          fmtNum(st.pending);
      }

      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (it) {
              const ansBlock = it.answer
                ? "<div>" +
                  escHtml(it.answer) +
                  "</div>" +
                  '<small class="adm-muted">' +
                  fmtDate(it.answeredAt) +
                  "</small>"
                : '<span class="adm-badge adm-badge-warning">Chưa trả lời</span>';
              return (
                "<tr>" +
                "<td>" +
                (it.productImage
                  ? '<img class="adm-thumb" src="' +
                    escHtml(it.productImage) +
                    '">'
                  : "") +
                "<div><strong>" +
                escHtml(it.productName) +
                "</strong></div>" +
                "</td>" +
                "<td>" +
                escHtml(it.customerName || "—") +
                (it.customerEmail
                  ? '<br><small class="adm-muted">' +
                    escHtml(it.customerEmail) +
                    "</small>"
                  : "") +
                "</td>" +
                "<td>" +
                escHtml(it.question) +
                '<br><small class="adm-muted">' +
                fmtDate(it.askedAt) +
                "</small></td>" +
                "<td>" +
                ansBlock +
                "</td>" +
                "<td>" +
                '<button class="adm-btn-sm adm-btn-primary" data-act="answer" data-id="' +
                it.id +
                '" data-q="' +
                escHtml(it.question) +
                '" data-a="' +
                escHtml(it.answer || "") +
                '" data-prd="' +
                escHtml(it.productName) +
                '">' +
                (it.answer ? "Sửa trả lời" : "Trả lời") +
                "</button>" +
                '<button class="adm-btn-sm adm-btn-danger" data-act="del" data-id="' +
                it.id +
                '">Xóa</button>' +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="5" class="adm-empty">Không có câu hỏi</td></tr>';

      renderPager("#qnaPager", r.page || 1, r.totalPages || 1, function (p) {
        qnaState.page = p;
        loadQnA();
      });
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="5" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindQnATab() {
    $("#qnaReloadBtn").addEventListener("click", function () {
      qnaState.search = $("#qnaSearch").value.trim();
      qnaState.answered = $("#qnaAnsweredFilter").value;
      qnaState.page = 1;
      loadQnA();
    });
    $("#qnaSearch").addEventListener("keydown", function (e) {
      if (e.key === "Enter") $("#qnaReloadBtn").click();
    });
    autoReload("#qnaReloadBtn", ["#qnaSearch"], ["#qnaAnsweredFilter"]);
    $("#qnaTableBody").addEventListener("click", async function (e) {
      const btn = e.target.closest("button[data-act]");
      if (!btn) return;
      const id = btn.getAttribute("data-id");
      const act = btn.getAttribute("data-act");
      if (act === "answer") {
        const q = btn.getAttribute("data-q");
        const a = btn.getAttribute("data-a") || "";
        const prd = btn.getAttribute("data-prd");
        openModal({
          title: "Trả lời câu hỏi",
          html:
            '<div class="adm-muted" style="margin-bottom:6px;">Sản phẩm: <b>' +
            escHtml(prd) +
            "</b></div>" +
            '<div class="adm-form-row"><label>Câu hỏi</label>' +
            '<div style="padding:10px;border:1px solid #eee;border-radius:6px;background:#fafafa;">' +
            escHtml(q) +
            "</div></div>" +
            '<div class="adm-form-row"><label>Câu trả lời</label>' +
            '<textarea class="adm-textarea" id="qnaAnswerInput" rows="4">' +
            escHtml(a) +
            "</textarea></div>",
          confirmText: "Gửi trả lời",
          onConfirm: async function () {
            const ans = $("#qnaAnswerInput").value.trim();
            if (!ans) throw new Error("Câu trả lời không được rỗng");
            await api("/api/admin/qna/" + id + "/answer", "POST", {
              answer: ans,
            });
            toast("Đã gửi trả lời", "success");
            loadQnA();
          },
        });
      } else if (act === "del") {
        confirmAction(
          "Xóa câu hỏi này khỏi trang sản phẩm?",
          async function () {
            await api("/api/admin/qna/" + id, "DELETE");
            toast("Đã xóa", "success");
            loadQnA();
          },
          "Xóa",
          "danger",
        );
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · AUDIT LOG
  // ═══════════════════════════════════════════════════════════
  const auditState = { page: 1, action: "", from: "", to: "" };

  TAB_LOADERS.audit = function () {
    loadAudit();
  };

  async function loadAudit() {
    const tbody = $("#auditTableBody");
    tbody.innerHTML =
      '<tr><td colspan="6" class="adm-empty">Đang tải...</td></tr>';
    try {
      const q = qs({
        page: auditState.page,
        limit: 50,
        action: auditState.action || null,
        from: auditState.from || null,
        to: auditState.to || null,
      });
      const r = await api("/api/admin/audit-logs" + q);
      const items = r.items || [];
      tbody.innerHTML = items.length
        ? items
            .map(function (a) {
              // Safe parse — per guide §3.13
              let detail = "";
              try {
                const old = safeParseJson(a.oldValue);
                const nu = safeParseJson(a.newValue);
                if (old || nu) {
                  detail =
                    '<details><summary class="adm-muted" style="cursor:pointer;">Xem JSON</summary>' +
                    '<pre class="adm-json-pre">old: ' +
                    escHtml(JSON.stringify(old, null, 2)) +
                    "\nnew: " +
                    escHtml(JSON.stringify(nu, null, 2)) +
                    "</pre></details>";
                }
              } catch (_) {
                detail = "(lỗi format)";
              }
              return (
                "<tr>" +
                "<td>" +
                fmtDate(a.createdAt) +
                "</td>" +
                "<td>" +
                escHtml(a.userName || a.userId || "—") +
                "</td>" +
                "<td><code>" +
                escHtml(a.action) +
                "</code></td>" +
                "<td>" +
                escHtml(a.entityType || "") +
                (a.entityId ? " #" + escHtml(a.entityId) : "") +
                "</td>" +
                "<td>" +
                detail +
                "</td>" +
                "<td>" +
                escHtml(a.ipAddress || "") +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="6" class="adm-empty">Không có log</td></tr>';

      renderPager("#auditPager", r.page || 1, r.totalPages || 1, function (p) {
        auditState.page = p;
        loadAudit();
      });
    } catch (e) {
      tbody.innerHTML =
        '<tr><td colspan="6" class="adm-empty">Lỗi: ' +
        escHtml(e.message) +
        "</td></tr>";
    }
  }

  function bindAuditTab() {
    $("#auditReloadBtn").addEventListener("click", function () {
      auditState.action = $("#auditAction").value.trim();
      auditState.from = $("#auditFrom").value;
      auditState.to = $("#auditTo").value;
      auditState.page = 1;
      loadAudit();
    });
    autoReload("#auditReloadBtn", ["#auditAction"], ["#auditFrom", "#auditTo"]);
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · SETTINGS
  // ═══════════════════════════════════════════════════════════
  let _allSettings = [];

  TAB_LOADERS.settings = function () {
    loadSettings();
  };

  async function loadSettings() {
    try {
      _allSettings = await api("/api/admin/config");
      const groups = Array.from(
        new Set(
          _allSettings.map(function (s) {
            return s.group || "general";
          }),
        ),
      );
      const sel = $("#settingsGroupFilter");
      sel.innerHTML =
        '<option value="">Tất cả nhóm</option>' +
        groups
          .map(function (g) {
            return (
              '<option value="' + escHtml(g) + '">' + escHtml(g) + "</option>"
            );
          })
          .join("");
      renderSettings();
    } catch (e) {
      $("#settingsContainer").innerHTML =
        '<div class="adm-empty">Lỗi: ' + escHtml(e.message) + "</div>";
    }
  }

  function renderSettings() {
    const filter = $("#settingsGroupFilter").value;
    const list = _allSettings.filter(function (s) {
      return !filter || s.group === filter;
    });
    if (!list.length) {
      $("#settingsContainer").innerHTML = '<div class="adm-empty">Trống</div>';
      return;
    }
    $("#settingsContainer").innerHTML = list
      .map(function (s) {
        const v = s.value == null ? "" : s.value;
        let input;
        if (s.type === "bool") {
          input =
            '<select data-key="' +
            escHtml(s.key) +
            '">' +
            '<option value="1"' +
            (v === "1" || v === "true" ? " selected" : "") +
            ">Bật</option>" +
            '<option value="0"' +
            (v === "0" || v === "false" ? " selected" : "") +
            ">Tắt</option>" +
            "</select>";
        } else if (s.type === "color") {
          input =
            '<input type="color" data-key="' +
            escHtml(s.key) +
            '" value="' +
            escHtml(v || "#000000") +
            '">';
        } else if (
          s.type === "html" ||
          s.type === "json" ||
          (v && v.length > 80)
        ) {
          input =
            '<textarea data-key="' +
            escHtml(s.key) +
            '">' +
            escHtml(v) +
            "</textarea>";
        } else {
          input =
            '<input type="text" data-key="' +
            escHtml(s.key) +
            '" value="' +
            escHtml(v) +
            '">';
        }
        return (
          '<div class="adm-setting-item">' +
          '<span class="adm-setting-label">' +
          escHtml(s.label || s.key) +
          "</span>" +
          '<span class="adm-setting-key">' +
          escHtml(s.key) +
          " · " +
          escHtml(s.group || "general") +
          "</span>" +
          input +
          "</div>"
        );
      })
      .join("");
  }

  function bindSettingsTab() {
    $("#settingsReloadBtn").addEventListener("click", loadSettings);
    $("#settingsGroupFilter").addEventListener("change", renderSettings);
    $("#settingsSaveBtn").addEventListener("click", async function () {
      const inputs = $$("#settingsContainer [data-key]");
      const changes = inputs.map(function (el) {
        return { key: el.getAttribute("data-key"), value: el.value };
      });
      if (!changes.length) {
        toast("Không có thay đổi", "warning");
        return;
      }
      try {
        await api("/api/admin/config", "PUT", changes);
        toast("Đã lưu " + changes.length + " cấu hình", "success");
        loadSettings();
      } catch (e) {
        toast(e.message, "danger");
      }
    });
  }

  // ═══════════════════════════════════════════════════════════
  //                     TAB · STATS
  // ═══════════════════════════════════════════════════════════
  let chartRevRange, chartOrderStatus;

  TAB_LOADERS.stats = function () {
    loadStats();
  };

  async function loadStats() {
    const today = new Date().toISOString().slice(0, 10);
    const monthAgo = new Date(Date.now() - 29 * 86400000)
      .toISOString()
      .slice(0, 10);
    const from = $("#statsFrom").value || monthAgo;
    const to = $("#statsTo").value || today;

    try {
      // Dedicated endpoint — server đã gom doanh thu/ngày, status count, top shops, tổng
      const r = await api(
        "/api/admin/stats/overview?from=" +
          from +
          "&to=" +
          to +
          "&topShopLimit=10",
      );

      const days = r.revenueByDay || [];
      const labels = days.map(function (d) {
        const dt = new Date(d.date);
        return dt.toLocaleDateString("vi-VN", {
          day: "2-digit",
          month: "2-digit",
        });
      });
      const revData = days.map(function (d) {
        return +d.revenue || 0;
      });

      if (chartRevRange) chartRevRange.destroy();
      chartRevRange = new Chart($("#chartRevenueRange").getContext("2d"), {
        type: "bar",
        data: {
          labels: labels,
          datasets: [
            {
              label: "Doanh thu (₫)",
              data: revData,
              backgroundColor: "#f759ab",
            },
          ],
        },
        options: {
          responsive: true,
          plugins: { legend: { display: false } },
          scales: {
            y: {
              beginAtZero: true,
              ticks: {
                callback: function (v) {
                  return fmtNum(v);
                },
              },
            },
          },
        },
      });

      const statusCounts = r.orderStatusCounts || {};
      const statusKeys = [
        "PENDING",
        "CONFIRMED",
        "SHIPPING",
        "DELIVERED",
        "COMPLETED",
        "CANCELLED",
      ];
      if (chartOrderStatus) chartOrderStatus.destroy();
      chartOrderStatus = new Chart($("#chartOrderStatus").getContext("2d"), {
        type: "doughnut",
        data: {
          labels: statusKeys.map(function (s) {
            return STATUS_VN[s].label;
          }),
          datasets: [
            {
              data: statusKeys.map(function (s) {
                return statusCounts[s] || 0;
              }),
              backgroundColor: [
                "#FFC107",
                "#2196F3",
                "#FF9800",
                "#4CAF50",
                "#1B5E20",
                "#F44336",
              ],
            },
          ],
        },
        options: { responsive: true },
      });

      const tops = r.topShops || [];
      $("#statsTopShopsBody").innerHTML = tops.length
        ? tops
            .map(function (s) {
              return (
                "<tr>" +
                "<td><strong>" +
                escHtml(s.shopName) +
                "</strong></td>" +
                "<td>" +
                fmtNum(s.orderCount || 0) +
                "</td>" +
                "<td>" +
                fmtMoney(s.revenue) +
                "</td>" +
                "<td>" +
                fmtMoney(s.commissionAmount) +
                "</td>" +
                "<td>" +
                fmtMoney(s.netRevenue) +
                "</td>" +
                "</tr>"
              );
            })
            .join("")
        : '<tr><td colspan="5" class="adm-empty">Chưa có dữ liệu</td></tr>';
    } catch (e) {
      toast("Lỗi tải Thống Kê: " + e.message, "danger");
    }
  }

  function bindStatsTab() {
    $("#statsReloadBtn").addEventListener("click", loadStats);
    autoReload("#statsReloadBtn", [], ["#statsFrom", "#statsTo"]);
  }

  // ═══════════════════════════════════════════════════════════
  //                     INIT
  // ═══════════════════════════════════════════════════════════
  document.addEventListener("DOMContentLoaded", function () {
    // Admin panel chỉ cho phép Admin (userType=1) — Seller phải dùng seller-dashboard.html
    if (typeof Auth !== "undefined") {
      if (!Auth.isLoggedIn()) {
        alert("Vui lòng đăng nhập!");
        window.location.href = "login.html";
        return;
      }
      if (!Auth.isAdmin()) {
        alert("Bạn không có quyền truy cập khu vực Admin!");
        window.location.href = "index.html";
        return;
      }
    } else if (typeof adminGuard === "function" && !adminGuard()) {
      return;
    }
    if (typeof renderAdminTopbar === "function") renderAdminTopbar();

    bindNav();
    bindUsersTab();
    bindShopsTab();
    bindProductsTab();
    bindOrdersTab();
    bindBankTab();
    bindCategoriesTab();
    bindBannersTab();
    bindFlashTab();
    bindVouchersTab();
    bindCommissionTab();
    bindWalletTab();
    bindQnATab();
    bindDisputesTab();
    bindAuditTab();
    bindSettingsTab();
    bindStatsTab();

    const init = location.hash ? location.hash.replace(/^#/, "") : "dashboard";
    switchTab(TAB_TITLES[init] ? init : "dashboard");
    window.addEventListener("hashchange", function () {
      const n = location.hash.replace(/^#/, "");
      if (TAB_TITLES[n]) switchTab(n);
    });
  });
})();
