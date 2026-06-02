// @ts-nocheck
// @ts-ignore
/* eslint-disable */
/**
 * ============================================================
 *  GlowHub — api.js  (v2.0 — full rewrite)
 *  Kết nối: login.html ↔ register.html ↔ index.html ↔ admin.html ↔ checkout.html
 * ============================================================
 *
 *  FIX CHÍNH:
 *  1. BASE_URL đúng port 5002 (AuthService)
 *  2. Giỏ hàng (Cart) hoạt động thực sự — lưu localStorage, cập nhật UI ngay
 *  3. Sản phẩm từ DB hiển thị lên index.html (new-products-grid + swiper)
 *  4. Admin CRUD sản phẩm/đơn hàng → gọi API thật
 *  5. Navbar tự động hiện tên user / nút đăng xuất
 *  6. Guard trang admin — chặn user thường
 * ============================================================
 */

// ============================================================
//  CONFIG
// ============================================================
const API_BASE = "http://localhost:5002"; // ← AuthService port (từ ảnh debug)
const PRODUCT_API = "http://localhost:5001"; // APIService
const ORDER_API = "http://localhost:5001"; // APIService

// ============================================================
//  HTTP HELPER
// ============================================================
async function apiFetch(base, path, method = "GET", body = null) {
  const opts = {
    method,
    headers: { "Content-Type": "application/json" },
  };
  const token = localStorage.getItem("token");
  if (token) opts.headers["Authorization"] = "Bearer " + token;
  if (body) opts.body = JSON.stringify(body);

  let res;
  try {
    res = await fetch(base + path, opts);
  } catch (netErr) {
    console.error(
      "[GlowHub API] Không kết nối được:",
      base + path,
      netErr.message,
    );
    throw netErr;
  }

  if (res.status === 401) {
    // Token hết hạn → logout
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    if (!window.location.pathname.endsWith("login.html")) {
      window.location.href = "login.html";
    }
    throw new Error("Phiên đăng nhập hết hạn");
  }

  if (!res.ok) {
    let msg = "Lỗi " + res.status;
    try {
      const d = await res.json();
      msg = d.message || d.Message || msg;
    } catch (_) {}
    console.error("[GlowHub API]", method, base + path, "→", res.status, msg);
    throw new Error(msg);
  }

  const ct = res.headers.get("content-type") || "";
  if (ct.includes("application/json")) return res.json();
  return res.text();
}

// ============================================================
//  AUTH MODULE
// ============================================================
const Auth = {
  /** Đăng nhập — trả về { token, user } */
  async login(credentials) {
    // credentials: { Username, Password }
    const data = await apiFetch(
      API_BASE,
      "/api/Auth/login",
      "POST",
      credentials,
    );
    // Backend có thể trả token ở nhiều dạng khác nhau
    const token =
      data.token || data.Token || data.accessToken || data.AccessToken;

    // Nếu backend trả user object lồng → dùng trực tiếp
    // Nếu trả flat (token + user fields cùng cấp) → tách ra, loại bỏ các field token
    let user = data.user || data.User;
    if (!user) {
      // Flat response: clone data rồi xóa các field token
      user = Object.assign({}, data);
      delete user.token;
      delete user.Token;
      delete user.accessToken;
      delete user.AccessToken;
      delete user.refreshToken;
      delete user.RefreshToken;
    }

    // Normalize: đảm bảo cả camelCase và PascalCase đều có
    // DB fields: Id, Name, UserName, Email, Phone, Contact, Position, Image, AvatarUrl, IsActive, UserType, Created, Address
    if (user) {
      user.id = user.id || user.Id || "";
      user.name =
        user.name || user.Name || user.fullName || user.FullName || "";
      user.userName = user.userName || user.UserName || user.username || "";
      user.email = user.email || user.Email || "";
      user.phone = user.phone || user.Phone || "";
      user.contact = user.contact || user.Contact || "";
      user.position = user.position || user.Position || "";
      user.image = user.image || user.Image || "";
      user.avatarUrl = user.avatarUrl || user.AvatarUrl || user.image || "";
      user.isActive =
        user.isActive !== undefined
          ? user.isActive
          : user.IsActive !== undefined
            ? user.IsActive
            : true;
      user.userType =
        user.userType !== undefined
          ? user.userType
          : user.UserType !== undefined
            ? user.UserType
            : 0;
      user.created = user.created || user.Created || "";
      user.address = user.address || user.Address || "";
    }

    if (token) {
      localStorage.setItem("token", token);
      localStorage.setItem("user", JSON.stringify(user));
    }
    return { token, user };
  },

  /** Đăng ký */
  async register(userData) {
    return apiFetch(API_BASE, "/api/Auth/register", "POST", userData);
  },

  /** Lấy danh sách user (admin) */
  async getUsers() {
    return apiFetch(API_BASE, "/api/users", "GET");
  },

  /** Cập nhật profile */
  async updateProfile(data) {
    return apiFetch(API_BASE, "/api/users/profile", "PUT", data);
  },

  /** Đổi mật khẩu */
  async changePassword(data) {
    return apiFetch(API_BASE, "/api/users/change-password", "POST", data);
  },

  /** Lấy thông tin user hiện tại từ localStorage */
  getCurrentUser() {
    try {
      return JSON.parse(localStorage.getItem("user") || "null");
    } catch {
      return null;
    }
  },

  /** Kiểm tra đã đăng nhập chưa */
  isLoggedIn() {
    return !!localStorage.getItem("token") && !!localStorage.getItem("user");
  },

  /** Kiểm tra admin */
  isAdmin() {
    const u = this.getCurrentUser();
    if (!u) return false;
    const t = u.userType ?? u.UserType ?? 0;
    const r = u.role ?? u.Role ?? "";
    return t === 1 || r === "Admin" || r === "admin";
  },

  /** Kiểm tra seller */
  isSeller() {
    const u = this.getCurrentUser();
    if (!u) return false;
    const t = u.userType ?? u.UserType ?? 0;
    const r = u.role ?? u.Role ?? "";
    return t === 2 || r === "Seller" || r === "seller";
  },

  /** Kiểm tra có quyền quản lý (admin hoặc seller) */
  canManage() {
    return this.isAdmin() || this.isSeller();
  },

  /** Đăng xuất */
  logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    window.location.href = "login.html";
  },
};

// ============================================================
//  PRODUCT MODULE
// ============================================================
const Product = {
  async getAll(params = {}) {
    // Tất cả params mà backend hỗ trợ
    var allowed = [
      "isActive",
      "isNew",
      "onlyNew",
      "onlySale",
      "categoryId",
      "cat",
      "limit",
      "page",
      "pageSize",
      "search",
      "keyword",
      "section",
      "sort",
      "minPrice",
      "maxPrice",
      "minRating",
      "brand",
    ];
    var clean = {};
    Object.keys(params).forEach(function (k) {
      if (
        allowed.indexOf(k) > -1 &&
        params[k] !== undefined &&
        params[k] !== null &&
        params[k] !== ""
      )
        clean[k] = params[k];
    });
    const qs = new URLSearchParams(clean).toString();
    const path = "/api/products" + (qs ? "?" + qs : "");
    return apiFetch(PRODUCT_API, path, "GET");
  },

  async getById(id) {
    return apiFetch(PRODUCT_API, "/api/products/" + id, "GET");
  },

  async create(data) {
    return apiFetch(PRODUCT_API, "/api/products", "POST", data);
  },

  async update(id, data) {
    return apiFetch(PRODUCT_API, "/api/products/" + id, "PUT", data);
  },

  async delete(id) {
    return apiFetch(PRODUCT_API, "/api/products/" + id, "DELETE");
  },
};

// ============================================================
//  ORDER MODULE  (Sprint 6 upgrade)
// ============================================================
const Order = {
  // ── Admin ──
  getAll()        { return apiFetch(ORDER_API, "/api/orders/all", "GET"); },
  getById(id)     { return apiFetch(ORDER_API, "/api/orders/" + id, "GET"); },
  updateStatus(id, status) { return apiFetch(ORDER_API, "/api/orders/" + id + "/status", "PUT", { status }); },

  // ── Customer — new /my routes ──
  getMy(status, page = 1, limit = 10) {
    const qs = new URLSearchParams({ page, limit });
    if (status) qs.set("status", status);
    return apiFetch(ORDER_API, "/api/orders/my?" + qs.toString(), "GET");
  },

  getMyOrders() { return this.getMy(); }, // backwards-compat alias

  getDetail(id)  { return apiFetch(ORDER_API, "/api/orders/my/" + id, "GET"); },

  getTracking(id) { return apiFetch(ORDER_API, "/api/orders/my/" + id + "/track", "GET"); },

  cancel(id, reason) {
    return apiFetch(ORDER_API, "/api/orders/my/" + id + "/cancel", "POST", { Reason: reason || "" });
  },

  confirmReceived(id) { return apiFetch(ORDER_API, "/api/orders/my/" + id + "/received", "POST"); },

  // Checkout v2 — gửi đủ ReceiverName, ReceiverPhone, PaymentMethod, VoucherCode, ShippingMethod
  checkout(data) { return apiFetch(ORDER_API, "/api/orders/checkout", "POST", data); },
  create(data)   { return this.checkout(data); }, // backwards-compat alias
};

// ============================================================
//  VOUCHER MODULE  (Customer-facing)
// ============================================================
const Voucher = {
  validate(code, orderAmount, shopId) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/validate", "POST", {
      Code: code,
      OrderAmount: orderAmount,
      ShopId: shopId || null,
    });
  },
  getAvailable(shopId, orderAmount) {
    const qs = new URLSearchParams({ orderAmount: orderAmount || 0 });
    if (shopId) qs.set("shopId", shopId);
    return apiFetch(PRODUCT_API, "/api/Vouchers/available?" + qs.toString(), "GET");
  },
};

// ============================================================
//  ADDRESS MODULE  (Customer saved addresses)
// ============================================================
const Address = {
  getAll()           { return apiFetch(PRODUCT_API, "/api/addresses", "GET"); },
  create(data)       { return apiFetch(PRODUCT_API, "/api/addresses", "POST", data); },
  update(id, data)   { return apiFetch(PRODUCT_API, "/api/addresses/" + id, "PUT", data); },
  delete(id)         { return apiFetch(PRODUCT_API, "/api/addresses/" + id, "DELETE"); },
  setDefault(id)     { return apiFetch(PRODUCT_API, "/api/addresses/" + id + "/set-default", "PUT"); },
};

// ============================================================
//  SHOP MODULE
// ============================================================
const Shop = {
  async getMy() {
    return apiFetch(PRODUCT_API, "/api/shops/my", "GET");
  },

  async getById(id) {
    return apiFetch(PRODUCT_API, "/api/shops/" + id, "GET");
  },

  async register(data) {
    // data: { ShopName, Description, Logo, Address, Phone }
    return apiFetch(PRODUCT_API, "/api/shops/register", "POST", data);
  },

  async update(id, data) {
    return apiFetch(PRODUCT_API, "/api/shops/" + id, "PUT", data);
  },

  // Admin endpoints
  async adminGetAll(page = 1, pageSize = 20) {
    return apiFetch(PRODUCT_API, `/api/shops/admin/all?page=${page}&pageSize=${pageSize}`, "GET");
  },

  async adminApprove(id) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/approve", "PUT");
  },

  async adminBan(id) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/ban", "PUT");
  },
};

// ============================================================
//  SELLER SHOP MODULE
// ============================================================
const SellerShop = {
  getDashboard() {
    return apiFetch(PRODUCT_API, "/api/shops/my/dashboard", "GET");
  },
  getStats(from, to) {
    const qs = new URLSearchParams();
    if (from) qs.set("from", from);
    if (to)   qs.set("to", to);
    return apiFetch(PRODUCT_API, "/api/shops/my/stats?" + qs.toString(), "GET");
  },
  getInfo() {
    return apiFetch(PRODUCT_API, "/api/shops/my", "GET");
  },
  update(shopId, data) {
    return apiFetch(PRODUCT_API, "/api/shops/" + shopId, "PUT", data);
  },
  register(data) {
    return apiFetch(PRODUCT_API, "/api/shops/register", "POST", data);
  },
};

// ============================================================
//  SELLER PRODUCT MODULE
// ============================================================
const SellerProduct = {
  getAll(page = 1, search = "", status = "all", limit = 10) {
    const qs = new URLSearchParams({ page, limit });
    if (search) qs.set("search", search);
    if (status) qs.set("status", status);
    return apiFetch(PRODUCT_API, "/api/products/my?" + qs.toString(), "GET");
  },
  create(data)      { return apiFetch(PRODUCT_API, "/api/products", "POST", data); },
  update(id, data)  { return apiFetch(PRODUCT_API, "/api/products/" + id, "PUT", data); },
  delete(id)        { return apiFetch(PRODUCT_API, "/api/products/" + id, "DELETE"); },
  toggle(id)        { return apiFetch(PRODUCT_API, "/api/products/" + id + "/toggle", "PATCH"); },
  getStats(id)      { return apiFetch(PRODUCT_API, "/api/products/" + id + "/stats", "GET"); },
};

// ============================================================
//  SELLER ORDER MODULE
// ============================================================
const SellerOrder = {
  getAll(status = "", page = 1, limit = 10) {
    const qs = new URLSearchParams({ page, limit });
    if (status) qs.set("status", status);
    return apiFetch(PRODUCT_API, "/api/orders/shop?" + qs.toString(), "GET");
  },
  getDetail(id)  { return apiFetch(PRODUCT_API, "/api/orders/shop/" + id, "GET"); },
  confirm(id)    { return apiFetch(PRODUCT_API, "/api/orders/shop/" + id + "/confirm", "PUT"); },
  ship(id, trackingCode) {
    return apiFetch(PRODUCT_API, "/api/orders/shop/" + id + "/ship", "PUT", { TrackingCode: trackingCode || null });
  },
  cancel(id, reason) {
    return apiFetch(PRODUCT_API, "/api/orders/shop/" + id + "/cancel", "PUT", { Reason: reason });
  },
};

// ============================================================
//  INVENTORY MODULE
// ============================================================
const Inventory = {
  getAll()              { return apiFetch(PRODUCT_API, "/api/inventory/my", "GET"); },
  getLowStock()         { return apiFetch(PRODUCT_API, "/api/inventory/low-stock", "GET"); },
  update(productId, stock) {
    return apiFetch(PRODUCT_API, "/api/inventory/" + productId, "PUT", { Stock: stock });
  },
};

// ============================================================
//  SELLER VOUCHER MODULE
// ============================================================
const SellerVoucher = {
  getAll()         { return apiFetch(PRODUCT_API, "/api/seller/vouchers", "GET"); },
  create(data)     { return apiFetch(PRODUCT_API, "/api/seller/vouchers", "POST", data); },
  update(id, data) { return apiFetch(PRODUCT_API, "/api/seller/vouchers/" + id, "PUT", data); },
  delete(id)       { return apiFetch(PRODUCT_API, "/api/seller/vouchers/" + id, "DELETE"); },
};

// ============================================================
//  SELLER REVIEW MODULE
// ============================================================
const SellerReview = {
  getAll(page = 1, rating = null, replied = null) {
    const qs = new URLSearchParams({ page, limit: 10 });
    if (rating  != null) qs.set("rating",  rating);
    if (replied != null) qs.set("replied", replied);
    return apiFetch(PRODUCT_API, "/api/reviews/shop?" + qs.toString(), "GET");
  },
  reply(reviewId, replyText) {
    return apiFetch(PRODUCT_API, "/api/reviews/" + reviewId + "/reply", "POST", { Reply: replyText });
  },
  getStats() {
    return apiFetch(PRODUCT_API, "/api/reviews/shop/stats", "GET");
  },
};

// ============================================================
//  Q&A MODULE
// ============================================================
const QnA = {
  // Seller
  getShopQuestions(page = 1, answered = null) {
    const qs = new URLSearchParams({ page, limit: 10 });
    if (answered != null) qs.set("answered", answered);
    return apiFetch(PRODUCT_API, "/api/qna/shop?" + qs.toString(), "GET");
  },
  answer(questionId, answerText) {
    return apiFetch(PRODUCT_API, "/api/qna/" + questionId + "/answer", "POST", { Answer: answerText });
  },
  getStats() {
    return apiFetch(PRODUCT_API, "/api/qna/shop/stats", "GET");
  },
  // Customer
  getProductQnA(productId) {
    return apiFetch(PRODUCT_API, "/api/qna/customer/" + productId, "GET");
  },
  ask(productId, question) {
    return apiFetch(PRODUCT_API, "/api/qna/customer/ask", "POST", { ProductId: productId, Question: question });
  },
};

// ============================================================
//  NOTIFICATION MODULE
// ============================================================
const Notification = {
  getAll(page = 1, isRead = null) {
    const qs = new URLSearchParams({ page, limit: 20 });
    if (isRead != null) qs.set("isRead", isRead);
    return apiFetch(PRODUCT_API, "/api/notifications/my?" + qs.toString(), "GET");
  },
  getUnreadCount() {
    return apiFetch(PRODUCT_API, "/api/notifications/unread-count", "GET");
  },
  markRead(id) {
    return apiFetch(PRODUCT_API, "/api/notifications/" + id + "/read", "PUT");
  },
  markAllRead() {
    return apiFetch(PRODUCT_API, "/api/notifications/read-all", "PUT");
  },
  delete(id) {
    return apiFetch(PRODUCT_API, "/api/notifications/" + id, "DELETE");
  },
};

// ============================================================
//  SELLER REPORT MODULE
// ============================================================
const SellerReport = {
  getRevenue(from, to) {
    const qs = new URLSearchParams();
    if (from) qs.set("from", from);
    if (to)   qs.set("to",   to);
    return apiFetch(PRODUCT_API, "/api/reports/seller/revenue?" + qs.toString(), "GET");
  },
  getTopProducts(limit = 10, from = null, to = null) {
    const qs = new URLSearchParams({ limit });
    if (from) qs.set("from", from);
    if (to)   qs.set("to",   to);
    return apiFetch(PRODUCT_API, "/api/reports/seller/products?" + qs.toString(), "GET");
  },
  getSummary() {
    return apiFetch(PRODUCT_API, "/api/reports/seller/summary", "GET");
  },
  exportCSV(from, to) {
    const rows = [...document.querySelectorAll('#statsTable tr')];
    if (!rows.length) { showGlobalToast('Không có dữ liệu để xuất', 'error'); return; }
    const headers = ['Ngày', 'Số Đơn', 'Doanh Thu', 'Hoa Hồng', 'Thực Nhận'];
    const csv = [
      headers.join(','),
      ...rows.map(row => [...row.cells].map(c => '"' + c.textContent.trim().replace(/"/g, '""') + '"').join(','))
    ].join('\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `bao_cao_${from || 'all'}_${to || 'all'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  },
};

// ============================================================
//  SELLER WALLET MODULE
// ============================================================
const SellerWallet = {
  getWallet() {
    return apiFetch(PRODUCT_API, "/api/seller/wallet", "GET");
  },
  getTransactions(type = "", page = 1, limit = 15) {
    const qs = new URLSearchParams({ page, limit });
    if (type) qs.set("type", type);
    return apiFetch(PRODUCT_API, "/api/seller/wallet/transactions?" + qs.toString(), "GET");
  },
};

// ============================================================
//  ADMIN WALLET MODULE
// ============================================================
const AdminWallet = {
  getOverview() {
    return apiFetch(PRODUCT_API, "/api/admin/wallet/overview", "GET");
  },
  releasePayouts() {
    return apiFetch(PRODUCT_API, "/api/admin/wallet/release-payouts", "POST");
  },
  getTransactions(shopId = "", type = "", page = 1, limit = 20) {
    const qs = new URLSearchParams({ page, limit });
    if (shopId) qs.set("shopId", shopId);
    if (type)   qs.set("type", type);
    return apiFetch(PRODUCT_API, "/api/admin/wallet/transactions?" + qs.toString(), "GET");
  },
};

// ============================================================
//  CART MODULE  ← Fix hoàn toàn
// ============================================================
const Cart = {
  STORAGE_KEY: "glowhub_cart",

  /** Đọc giỏ hàng từ localStorage */
  getItems() {
    try {
      return JSON.parse(localStorage.getItem(this.STORAGE_KEY) || "[]");
    } catch {
      return [];
    }
  },

  /** Lưu giỏ hàng vào localStorage */
  _save(items) {
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(items));
    this._updateUI();
  },

  /** Thêm sản phẩm vào giỏ */
  add(product, qty = 1) {
    const items = this.getItems();
    const id = product.Id || product.id || product._id;
    const idx = items.findIndex((i) => (i.id || i.Id) == id);
    if (idx > -1) {
      items[idx].qty = (items[idx].qty || 1) + qty;
    } else {
      items.push({
        id: id,
        name: product.Name || product.name,
        price: product.Price || product.price,
        // DB Products dùng ImageUrl, không phải Image
        image:
          product.ImageUrl ||
          product.imageUrl ||
          product.Image ||
          product.image ||
          "",
        category:
          (product.Category &&
            (product.Category.Name || product.Category.name)) ||
          product.CategoryName ||
          product.Category ||
          product.category ||
          "",
        qty: qty,
      });
    }
    this._save(items);
    this._flashBadge();
    return items;
  },

  /** Xóa một sản phẩm */
  remove(id) {
    const items = this.getItems().filter((i) => (i.id || i.Id) != id);
    this._save(items);
  },

  /** Thay đổi số lượng */
  setQty(id, qty) {
    const items = this.getItems();
    const idx = items.findIndex((i) => (i.id || i.Id) == id);
    if (idx > -1) {
      if (qty <= 0) items.splice(idx, 1);
      else items[idx].qty = qty;
    }
    this._save(items);
  },

  /** Xóa toàn bộ */
  clear() {
    localStorage.removeItem(this.STORAGE_KEY);
    this._updateUI();
  },

  /** Tổng số lượng */
  totalCount() {
    return this.getItems().reduce((s, i) => s + (i.qty || 1), 0);
  },

  /** Tổng tiền */
  totalPrice() {
    return this.getItems().reduce(
      (s, i) => s + (i.price || 0) * (i.qty || 1),
      0,
    );
  },

  /** Cập nhật toàn bộ UI giỏ hàng */
  _updateUI() {
    const items = this.getItems();
    const count = this.totalCount();
    const total = this.totalPrice();

    // Badges
    ["cart-count", "cart-count-mobile"].forEach((id) => {
      const el = document.getElementById(id);
      if (el) el.textContent = count;
    });

    // Cart list trong offcanvas
    const listEl = document.getElementById("cart-list");
    if (listEl) {
      if (items.length === 0) {
        listEl.innerHTML =
          '<li class="list-group-item text-center text-muted py-5">Giỏ hàng trống</li>';
      } else {
        var ph = "https://placehold.co/52x52/f5f0ea/888?text=IMG";
        listEl.innerHTML = items
          .map(function (item) {
            var qty = item.qty || 1;
            var img = item.image || ph;
            var decQty = qty - 1;
            var incQty = qty + 1;
            return `<li class="list-group-item px-0 py-2">
            <div class="d-flex gap-3 align-items-start">
              <img src="${img}" style="width:52px;height:52px;object-fit:cover;border-radius:4px"
                   onerror="this.src='${ph}'"/>
              <div class="flex-grow-1 min-width-0">
                <div style="font-size:13px;font-weight:500;line-height:1.3">${item.name || ""}</div>
                <div style="font-size:12px;color:#f759ab;margin-top:2px">${_fmtMoney(item.price)}</div>
                <div class="d-flex align-items-center gap-2 mt-1">
                  <button onclick="Cart.setQty(${item.id},${decQty})"
                    style="width:22px;height:22px;border:1px solid #ddd;background:none;cursor:pointer;border-radius:3px">−</button>
                  <span style="font-size:13px;min-width:20px;text-align:center">${qty}</span>
                  <button onclick="Cart.setQty(${item.id},${incQty})"
                    style="width:22px;height:22px;border:1px solid #ddd;background:none;cursor:pointer;border-radius:3px">+</button>
                  <button onclick="Cart.remove(${item.id})"
                    style="margin-left:auto;background:none;border:none;color:#999;cursor:pointer;font-size:16px">×</button>
                </div>
              </div>
            </div>
          </li>`;
          })
          .join("");
      }
    }

    // Tổng tiền
    const totalEl = document.getElementById("cart-total");
    if (totalEl) totalEl.textContent = _fmtMoney(total);
  },

  /** Hiệu ứng nhấp nháy badge */
  _flashBadge() {
    ["cart-count", "cart-count-mobile"].forEach((id) => {
      const el = document.getElementById(id);
      if (!el) return;
      el.style.transform = "scale(1.6)";
      el.style.transition = "transform 0.15s";
      setTimeout(() => {
        el.style.transform = "scale(1)";
      }, 200);
    });
    // Mở offcanvas cart tự động
    const offcanvasEl = document.getElementById("offcanvasCart");
    if (offcanvasEl && typeof bootstrap !== "undefined") {
      const oc = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
      oc.show();
    }
  },
};

// ============================================================
//  FORMAT HELPERS  (toàn cục)
// ============================================================
function _fmtMoney(n) {
  if (!n && n !== 0) return "--";
  return new Intl.NumberFormat("vi-VN").format(n) + " ₫";
}

// ============================================================
//  TOAST HELPER  (toàn cục, hoạt động trên mọi trang)
// ============================================================
function showGlobalToast(msg, type) {
  if (type === undefined) type = "success";

  // Try to use the base.css #gh-toast element first
  var ghToast = document.getElementById("gh-toast");
  if (ghToast) {
    ghToast.textContent = msg;
    ghToast.className = "show";
    if (type === "error" || type === "err") ghToast.classList.add("toast-err");
    else if (type === "warn" || type === "warning") ghToast.classList.add("toast-warn");
    else if (type === "ok" || type === "success") ghToast.classList.add("toast-ok");
    clearTimeout(ghToast._tid);
    ghToast._tid = setTimeout(function() { ghToast.className = ""; }, 3200);
    return;
  }

  // Fallback: create floating toast
  var isErr = (type === "error" || type === "err");
  var isWarn = (type === "warn" || type === "warning");
  var borderColor = isErr ? "#dc2626" : isWarn ? "#f59e0b" : "#f759ab";
  var icon = isErr ? "✕" : isWarn ? "⚠" : "✓";

  var container = document.getElementById("_globalToastContainer");
  if (!container) {
    container = document.createElement("div");
    container.id = "_globalToastContainer";
    container.style.cssText =
      "position:fixed;bottom:24px;right:24px;z-index:11000;display:flex;flex-direction:column;gap:8px;pointer-events:none";
    document.body.appendChild(container);
  }
  var t = document.createElement("div");
  t.style.cssText =
    "padding:12px 20px;background:#111;color:#fff;font-size:12px;letter-spacing:.5px;" +
    "box-shadow:0 4px 16px rgba(0,0,0,.2);border-left:3px solid " + borderColor +
    ";transform:translateX(120%);transition:transform 0.3s ease;display:flex;align-items:center;gap:8px;max-width:320px";
  t.innerHTML = '<span style="font-size:14px">' + icon + '</span><span>' + msg + '</span>';
  container.appendChild(t);
  requestAnimationFrame(function() { t.style.transform = "translateX(0)"; });
  setTimeout(function() {
    t.style.transform = "translateX(120%)";
    setTimeout(function() { t.remove(); }, 350);
  }, 3000);
}

// ============================================================
//  NAVBAR — render user area (index.html)
// ============================================================

// ============================================================
//  CART DB — Sync localStorage → DB (khớp CartController.cs)
// ============================================================
var CartDB = {
  getAll: function () {
    return apiFetch(PRODUCT_API, "/api/Cart", "GET");
  },
  addItem: function (pid, qty) {
    return apiFetch(PRODUCT_API, "/api/Cart/add", "POST", {
      ProductId: pid,
      Quantity: qty,
    });
  },
  removeItem: function (cid) {
    return apiFetch(PRODUCT_API, "/api/Cart/" + cid, "DELETE");
  },
  clear: function () {
    return apiFetch(PRODUCT_API, "/api/Cart/clear", "DELETE");
  },
  syncToDB: async function () {
    if (typeof Auth === "undefined" || !Auth.isLoggedIn()) return;
    var items = Cart.getItems();
    if (!items || !items.length) return;
    try {
      await CartDB.clear();
    } catch (e) {}
    for (var i = 0; i < items.length; i++) {
      var it = items[i];
      var pid = parseInt(it.id || it.Id) || 0;
      var qty = it.qty || it.Qty || 1;
      if (pid > 0)
        try {
          await CartDB.addItem(pid, qty);
        } catch (e) {
          console.warn("[CartDB]", pid, e.message);
        }
    }
    console.log("[GlowHub] Cart synced:", items.length, "items");
  },
};

function renderNavUser() {
  var user = Auth.getCurrentUser();
  var areas = ["nav-user-area", "nav-user-mobile"];

  areas.forEach(function (areaId) {
    var el = document.getElementById(areaId);
    if (!el) return;

    if (user) {
      var name =
        user.name || user.Name || user.userName || user.UserName || "Tài khoản";
      // DB: AvatarUrl hoặc Image
      var avatarUrl =
        user.avatarUrl || user.AvatarUrl || user.image || user.Image || "";
      var initial = name.trim().split(" ").pop().charAt(0).toUpperCase();
      var avatarHtml = avatarUrl
        ? '<img src="' +
          avatarUrl +
          '" style="width:100%;height:100%;object-fit:cover;border-radius:50%" onerror="this.style.display=\'none\';this.parentElement.textContent=\'' +
          initial +
          "'\">"
        : initial;
      var adminLi = Auth.isAdmin()
        ? '<a href="admin.html">⚙️ Quản Trị</a>'
        : Auth.isSeller()
          ? '<a href="admin.html">🏪 Quản Lý Shop</a>'
          : "";

      el.innerHTML = `
        <button class="gh-user-btn" id="userBtn_${areaId}" onclick="toggleUserMenu(this)" type="button">
          <span class="gh-user-avatar">${avatarHtml}</span>
          <span class="gh-user-name">${name}</span>
          <div class="gh-dropdown">
            <a href="profile.html">👤 Hồ Sơ</a>
            <a href="checkout.html?tab=orders">📦 Đơn Hàng</a>
            ${adminLi}
            <hr/>
            <a href="#" class="logout" onclick="event.preventDefault();Auth.logout()">🚪 Đăng Xuất</a>
          </div>
        </button>`;
    } else {
      el.innerHTML = `
        <div style="display:flex;align-items:center;gap:16px">
          <a href="login.html" class="gh-nav-link">Đăng Nhập</a>
          <a href="register.html"
             style="padding:7px 16px;background:var(--dark);color:#fff;
                    font-size:10px;letter-spacing:2px;text-transform:uppercase;
                    text-decoration:none;font-family:var(--font-body);
                    transition:background .2s"
             onmouseover="this.style.background='#f759ab'"
             onmouseout="this.style.background='#111'">Đăng Ký</a>
        </div>`;
    }
  });

  // Đóng dropdown khi click ra ngoài
  document.addEventListener("click", function (e) {
    if (!e.target.closest(".gh-user-btn")) {
      document.querySelectorAll(".gh-user-btn.open").forEach(function (b) {
        b.classList.remove("open");
      });
    }
  });
}

function toggleUserMenu(btn) {
  var isOpen = btn.classList.contains("open");
  // Đóng tất cả dropdowns khác
  document.querySelectorAll(".gh-user-btn.open").forEach(function (b) {
    b.classList.remove("open");
  });
  if (!isOpen) btn.classList.add("open");
}

// ============================================================
//  INDEX PAGE — render sản phẩm từ API vào grid
// ============================================================
async function renderProductsOnIndex() {
  var grid = document.getElementById("new-products-grid");
  if (!grid) return;

  var products = [];
  try {
    var result = await Product.getAll({});
    products =
      result?.items || result?.data || result?.products || result || [];
    if (!Array.isArray(products)) products = [];
  } catch (err) {
    console.warn("[GlowHub] renderProductsOnIndex lỗi:", err && err.message);
    _attachStaticCartButtons();
    return;
  }

  if (!products || !products.length) {
    console.warn(
      "[GlowHub] API trả về mảng rỗng — kiểm tra backend /api/products",
    );
    _attachStaticCartButtons();
    return;
  }

  var PH =
    "https://images.unsplash.com/photo-1598033129183-c4f50c736f10?w=400&q=80";

  grid.innerHTML = products
    .map(function (p) {
      var id = p.Id || p.id;
      var name = p.Name || p.name || "Sản phẩm";
      var price = p.Price || p.price || 0;
      var img = p.ImageUrl || p.imageUrl || p.Image || p.image || PH;
      var isNew = p.IsNew || p.isNew;
      var sale = p.SalePercent || p.salePercent || p.discount || 0;
      var sn = name.replace(/'/g, "&#39;");

      var badgeHtml = isNew
        ? '<span class="badge-new">Mới</span>'
        : sale
          ? `<span class="badge-sale">-${sale}%</span>`
          : "";

      return `<div class="col-6 col-md-3">
      <div class="product-card bg-white" data-product-id="${id}"
           onclick="location.href='product.html?id=${id}'">
        ${badgeHtml}
        <a href="product.html?id=${id}">
          <img src="${img}" alt="${sn}" loading="lazy"
               onerror="this.src='${PH}'"/>
        </a>
        <div class="p-3">
          <a href="product.html?id=${id}" class="product-title d-block mb-1">${name}</a>
          <div class="stars" style="font-size:12px">★★★★★</div>
          <span class="product-price">${_fmtMoney(price)}</span>
        </div>
        <button class="btn-add-cart"
          data-id="${id}" data-name="${sn}" data-price="${price}" data-img="${img}"
          onclick="event.stopPropagation();addToCartFromCard(this)">
          Thêm Vào Giỏ
        </button>
      </div>
    </div>`;
    })
    .join("");
}

/** Gắn sự kiện cho các nút tĩnh trong HTML (khi API chưa sẵn sàng) */
function _attachStaticCartButtons() {
  document.querySelectorAll(".btn-add-cart").forEach((btn) => {
    if (btn.dataset.cartBound) return;
    btn.dataset.cartBound = "1";
    btn.addEventListener("click", function () {
      // Lấy thông tin từ DOM gần nhất
      const card =
        this.closest(".product-card") || this.closest(".swiper-slide");
      const name =
        (card && card.querySelector(".product-title")
          ? card.querySelector(".product-title").textContent.trim()
          : "") || "Sản phẩm";
      const priceText =
        card && card.querySelector(".product-price")
          ? card.querySelector(".product-price").textContent
          : "0";
      const price = parseInt(priceText.replace(/[^0-9]/g, "")) || 0;
      const img =
        card && card.querySelector("img") ? card.querySelector("img").src : "";
      const id =
        (card && card.dataset && card.dataset.productId
          ? card.dataset.productId
          : null) || Date.now();

      Cart.add({ id, name, price, image: img });
      showGlobalToast('✓ Đã thêm "' + name + '" vào giỏ hàng');
    });
  });
}

// ============================================================
//  ADMIN GUARD
// ============================================================
function adminGuard() {
  if (!Auth.isLoggedIn()) {
    alert("Vui lòng đăng nhập!");
    window.location.href = "login.html";
    return false;
  }
  if (!Auth.canManage()) {
    alert("Bạn không có quyền truy cập khu vực này!");
    window.location.href = "index.html";
    return false;
  }
  return true;
}

// ============================================================
//  ADMIN TOPBAR — hiện tên admin
// ============================================================
function renderAdminTopbar() {
  const user = Auth.getCurrentUser();
  const nameEl = document.getElementById("adminName");
  const avatarEl = document.getElementById("adminAvatar");
  if (!user) return;
  const name =
    user.name || user.Name || user.userName || user.UserName || "Admin";
  if (nameEl) nameEl.textContent = name;
  if (avatarEl) avatarEl.textContent = name.charAt(0).toUpperCase();
}

// ============================================================
//  AUTO-INIT  —  chạy khi DOM sẵn sàng
// ============================================================
document.addEventListener("DOMContentLoaded", function () {
  const page = window.location.pathname.split("/").pop() || "index.html";

  // ---- LOGIN PAGE ----
  if (page === "login.html") {
    const form = document.getElementById("login-form");
    if (form) {
      form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const username = document.getElementById("username").value.trim();
        const password = document.getElementById("password").value;
        const errorDiv = document.getElementById("login-error");
        const errorSpan = document.getElementById("error-message");
        const btn = form.querySelector('button[type="submit"]');

        errorDiv.style.display = "none";
        btn.disabled = true;
        btn.textContent = "Đang đăng nhập...";

        try {
          const res = await Auth.login({
            Username: username,
            Password: password,
          });
          if (res && res.user) {
            showGlobalToast("Đăng nhập thành công!");
            setTimeout(() => {
              if (Auth.canManage()) {
                window.location.href = "admin.html";
              } else {
                window.location.href = "index.html";
              }
            }, 400);
          } else {
            throw new Error("Tài khoản hoặc mật khẩu không đúng!");
          }
        } catch (err) {
          errorSpan.textContent =
            err.message || "Tài khoản hoặc mật khẩu không đúng!";
          errorDiv.style.display = "block";
          errorDiv.classList.remove("shake-animation");
          void errorDiv.offsetWidth; // reflow để reset animation
          errorDiv.classList.add("shake-animation");
          btn.disabled = false;
          btn.textContent = "Đăng nhập ngay";
        }
      });
    }
    return; // Không chạy init khác trên trang login
  }

  // ---- REGISTER PAGE ----
  if (page === "register.html") {
    const form = document.getElementById("register-form");
    if (form) {
      form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const fd = new FormData(e.target);
        const userData = Object.fromEntries(fd.entries());
        userData.UserType = 0;
        userData.IsActive = true;
        userData.Created = new Date().toISOString();

        const btn = form.querySelector('button[type="submit"]');
        btn.disabled = true;
        btn.textContent = "Đang đăng ký...";

        try {
          await Auth.register(userData);
          showGlobalToast("Đăng ký thành công! Đang chuyển hướng...");
          setTimeout(() => (window.location.href = "login.html"), 1200);
        } catch (err) {
          showGlobalToast("Lỗi: " + err.message, "error");
          btn.disabled = false;
          btn.textContent = "Hoàn tất đăng ký";
        }
      });
    }
    return;
  }

  // ---- ADMIN PAGE ----
  if (page === "admin.html") {
    if (!adminGuard()) return;
    Cart._updateUI(); // Init cart count
    renderAdminTopbar();

    // Nút đăng xuất admin
    const logoutBtn = document.getElementById("adminLogoutBtn");
    if (logoutBtn) logoutBtn.addEventListener("click", Auth.logout.bind(Auth));
    return;
  }

  // ---- INDEX + TẤT CẢ TRANG KHÁC ----
  // Khởi động giỏ hàng
  Cart._updateUI();

  // Render nav user
  renderNavUser();

  // Load sản phẩm từ API vào index
  if (document.getElementById("new-products-grid")) {
    renderProductsOnIndex().then(() => {
      // Sau khi render động, gắn lại nút tĩnh phòng trường hợp fallback
      _attachStaticCartButtons();
    });
  } else {
    // Trang khác (checkout, etc.) — chỉ gắn nút tĩnh nếu có
    _attachStaticCartButtons();
  }
});

// ============================================================
//  BANNER MODULE
// ============================================================
var Banner = {
  getAll: function () {
    return apiFetch(PRODUCT_API, "/api/Banners", "GET");
  },
  create: function (d) {
    return apiFetch(PRODUCT_API, "/api/Banners", "POST", d);
  },
  update: function (id, d) {
    return apiFetch(PRODUCT_API, "/api/Banners/" + id, "PUT", d);
  },
  delete: function (id) {
    return apiFetch(PRODUCT_API, "/api/Banners/" + id, "DELETE");
  },
};

// ============================================================
//  SITE SETTINGS MODULE
// ============================================================
var SiteSettings = {
  getAll: function (group) {
    var qs = group ? "?group=" + group : "";
    return apiFetch(PRODUCT_API, "/api/SiteSettings" + qs, "GET");
  },
  update: function (key, value) {
    return apiFetch(PRODUCT_API, "/api/SiteSettings/" + key, "PUT", {
      value: value,
    });
  },
  bulkUpdate: function (updates) {
    return apiFetch(PRODUCT_API, "/api/SiteSettings/bulk", "PUT", updates);
  },
};

// ============================================================
//  FEATURED PRODUCTS MODULE
// ============================================================
var Featured = {
  getBySection: function (section) {
    return apiFetch(
      PRODUCT_API,
      "/api/FeaturedProducts?section=" + section,
      "GET",
    );
  },
  add: function (productId, section) {
    return apiFetch(PRODUCT_API, "/api/FeaturedProducts", "POST", {
      productId: productId,
      section: section,
    });
  },
  remove: function (id) {
    return apiFetch(PRODUCT_API, "/api/FeaturedProducts/" + id, "DELETE");
  },
};

// ============================================================
//  USER ADDRESS MODULE — kết nối /api/UserAddresses (alias cũ)
// ============================================================
var UserAddress = {
  // Lấy tất cả địa chỉ của user
  getAll: function () {
    return apiFetch(PRODUCT_API, "/api/UserAddresses", "GET");
  },
  // Thêm địa chỉ mới
  create: function (data) {
    return apiFetch(PRODUCT_API, "/api/UserAddresses", "POST", data);
  },
  // Cập nhật địa chỉ
  update: function (id, data) {
    return apiFetch(PRODUCT_API, "/api/UserAddresses/" + id, "PUT", data);
  },
  // Đặt làm mặc định
  setDefault: function (id) {
    return apiFetch(
      PRODUCT_API,
      "/api/UserAddresses/" + id + "/set-default",
      "PUT",
    );
  },
  // Xóa địa chỉ
  delete: function (id) {
    return apiFetch(PRODUCT_API, "/api/UserAddresses/" + id, "DELETE");
  },
};

// ============================================================
//  VOUCHER LEGACY (alias cũ — dùng VoucherLegacy để tránh trùng const Voucher)
// ============================================================
var VoucherLegacy = {
  validate: function (code, orderAmount) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/validate", "POST", {
      code: code,
      orderAmount: orderAmount,
    });
  },
  getAll: function () {
    return apiFetch(PRODUCT_API, "/api/Vouchers", "GET");
  },
};

// ============================================================
//  CATEGORY MODULE — SQL Server (Id, Name, Description)
// ============================================================
const Category = {
  async getAll() {
    return apiFetch(PRODUCT_API, "/api/categories", "GET");
  },
  async getById(id) {
    return apiFetch(PRODUCT_API, "/api/categories/" + id, "GET");
  },
};

// ============================================================
//  SHOP PUBLIC MODULE — Customer-facing shop profile
// ============================================================
const ShopPublic = {
  getProfile(shopId) {
    return apiFetch(PRODUCT_API, "/api/shops/" + shopId + "/profile", "GET");
  },
  getProducts(shopId, params) {
    params = params || {};
    const qs = new URLSearchParams();
    qs.set("page",  params.page  || 1);
    qs.set("limit", params.limit || 12);
    if (params.categoryId != null && params.categoryId !== "") qs.set("categoryId", params.categoryId);
    if (params.minPrice   != null) qs.set("minPrice",   params.minPrice);
    if (params.maxPrice   != null) qs.set("maxPrice",   params.maxPrice);
    if (params.sort)               qs.set("sort",        params.sort);
    return apiFetch(PRODUCT_API, "/api/shops/" + shopId + "/products?" + qs.toString(), "GET");
  },
  getReviews(shopId, page, rating, limit) {
    const qs = new URLSearchParams({ page: page || 1, limit: limit || 10 });
    if (rating) qs.set("rating", rating);
    return apiFetch(PRODUCT_API, "/api/shops/" + shopId + "/reviews?" + qs.toString(), "GET");
  },
  getStats(shopId) {
    return apiFetch(PRODUCT_API, "/api/shops/" + shopId + "/stats", "GET");
  },
};

// ============================================================
//  WISHLIST MODULE
// ============================================================
const Wishlist = {
  getAll() {
    return apiFetch(PRODUCT_API, "/api/wishlist", "GET");
  },
  add(productId) {
    return apiFetch(PRODUCT_API, "/api/wishlist/" + productId, "POST");
  },
  remove(productId) {
    return apiFetch(PRODUCT_API, "/api/wishlist/" + productId, "DELETE");
  },
  check(productId) {
    return apiFetch(PRODUCT_API, "/api/wishlist/check/" + productId, "GET");
  },
};

// ============================================================
//  ADMIN MODULE — Dành riêng cho Admin Dashboard (Sprint 8)
// ============================================================
const Admin = {
  // ── Dashboard ──
  getDashboard() {
    return apiFetch(PRODUCT_API, "/api/admin/dashboard", "GET");
  },
  getRevenueStats(qs) {
    return apiFetch(PRODUCT_API, "/api/admin/stats/revenue" + (qs ? "?" + qs : ""), "GET");
  },
  getSummary() {
    return apiFetch(PRODUCT_API, "/api/admin/stats/summary", "GET");
  },

  // ── Users ──
  getUsers(qs) {
    return apiFetch(PRODUCT_API, "/api/admin/users" + (qs ? "?" + qs : ""), "GET");
  },
  getUserById(id) {
    return apiFetch(PRODUCT_API, "/api/admin/users/" + id, "GET");
  },
  banUser(id) {
    return apiFetch(PRODUCT_API, "/api/admin/users/" + id + "/ban", "PUT");
  },
  unbanUser(id) {
    return apiFetch(PRODUCT_API, "/api/admin/users/" + id + "/unban", "PUT");
  },
  changeRole(id, role) {
    return apiFetch(PRODUCT_API, "/api/admin/users/" + id + "/role", "PUT", { Role: role });
  },
  deleteUser(id) {
    return apiFetch(PRODUCT_API, "/api/admin/users/" + id, "DELETE");
  },

  // ── Orders ──
  getOrders(qs) {
    return apiFetch(PRODUCT_API, "/api/orders/admin/orders" + (qs ? "?" + qs : ""), "GET");
  },
  getOrderDetail(id) {
    return apiFetch(PRODUCT_API, "/api/orders/admin/orders/" + id, "GET");
  },
  updateOrderStatus(id, status, note) {
    return apiFetch(PRODUCT_API, "/api/orders/admin/orders/" + id + "/status", "PUT", { Status: status, Note: note || null });
  },

  // ── Shops ──
  getShopStats(qs) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/stats" + (qs ? "?" + qs : ""), "GET");
  },
  approveShop(id) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/approve", "PUT");
  },
  banShop(id) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/ban", "PUT");
  },
  updateCommission(id, rate) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/commission", "PUT", { CommissionRate: rate });
  },
  getShopStatsById(id, qs) {
    return apiFetch(PRODUCT_API, "/api/shops/admin/" + id + "/stats" + (qs ? "?" + qs : ""), "GET");
  },

  // ── Categories ──
  getCategories() {
    return apiFetch(PRODUCT_API, "/api/categories", "GET");
  },
  createCategory(data) {
    return apiFetch(PRODUCT_API, "/api/categories", "POST", { Name: data.name, Description: data.description });
  },
  updateCategory(id, data) {
    return apiFetch(PRODUCT_API, "/api/categories/" + id, "PUT", { Name: data.name, Description: data.description });
  },
  deleteCategory(id) {
    return apiFetch(PRODUCT_API, "/api/categories/" + id, "DELETE");
  },

  // ── Banners ──
  getBanners() {
    return apiFetch(PRODUCT_API, "/api/banners/all", "GET");
  },
  getBanner(id) {
    return apiFetch(PRODUCT_API, "/api/banners/" + id, "GET");
  },
  createBanner(data) {
    return apiFetch(PRODUCT_API, "/api/banners", "POST", data);
  },
  updateBanner(id, data) {
    return apiFetch(PRODUCT_API, "/api/banners/" + id, "PUT", data);
  },
  deleteBanner(id) {
    return apiFetch(PRODUCT_API, "/api/banners/" + id, "DELETE");
  },

  // ── Flash Sale ──
  getFlashSales(qs) {
    return apiFetch(PRODUCT_API, "/api/flashsale/admin/list" + (qs ? "?" + qs : ""), "GET");
  },
  createFlashSale(data) {
    return apiFetch(PRODUCT_API, "/api/flashsale", "POST", data);
  },
  updateFlashSale(id, data) {
    return apiFetch(PRODUCT_API, "/api/flashsale/" + id, "PUT", data);
  },
  deleteFlashSale(id) {
    return apiFetch(PRODUCT_API, "/api/flashsale/" + id, "DELETE");
  },
  toggleFlashSale(id) {
    return apiFetch(PRODUCT_API, "/api/flashsale/" + id + "/toggle", "PUT");
  },

  // ── Vouchers ──
  getVouchers() {
    return apiFetch(PRODUCT_API, "/api/Vouchers", "GET");
  },
  createVoucher(data) {
    return apiFetch(PRODUCT_API, "/api/Vouchers", "POST", data);
  },
  updateVoucher(id, data) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/" + id, "PUT", data);
  },
  deleteVoucher(id) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/" + id, "DELETE");
  },
  getVoucherUsage(id, qs) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/" + id + "/usage" + (qs ? "?" + qs : ""), "GET");
  },

  // ── System Config ──
  getConfig() {
    return apiFetch(PRODUCT_API, "/api/admin/config", "GET");
  },
  updateConfig(items) {
    return apiFetch(PRODUCT_API, "/api/admin/config", "PUT", items);
  },
  updateConfigKey(key, value) {
    return apiFetch(PRODUCT_API, "/api/admin/config/" + key, "PUT", { Key: key, Value: value });
  },

  // ── Commission (Hoa Hồng) ──
  getCommissionSummary() {
    return apiFetch(PRODUCT_API, "/api/admin/commission/summary", "GET");
  },
  getCommissionShops(qs) {
    return apiFetch(PRODUCT_API, "/api/admin/commission/shops" + (qs ? "?" + qs : ""), "GET");
  },
  createPayout(shopId, data) {
    return apiFetch(PRODUCT_API, "/api/admin/commission/payout/" + shopId, "POST", data);
  },
  getPayoutHistory(qs) {
    return apiFetch(PRODUCT_API, "/api/admin/commission/history" + (qs ? "?" + qs : ""), "GET");
  },

  // ── Disputes (Khiếu Nại) ──
  getDisputes(qs) {
    return apiFetch(PRODUCT_API, "/api/disputes" + (qs ? "?" + qs : ""), "GET");
  },
  getDisputeDetail(id) {
    return apiFetch(PRODUCT_API, "/api/disputes/" + id, "GET");
  },
  processDispute(id) {
    return apiFetch(PRODUCT_API, "/api/disputes/" + id + "/process", "PUT");
  },
  resolveDispute(id, data) {
    return apiFetch(PRODUCT_API, "/api/disputes/" + id + "/resolve", "PUT", data);
  },

  // ── Audit Log ──
  getAuditLogs(qs) {
    return apiFetch(PRODUCT_API, "/api/admin/audit-logs" + (qs ? "?" + qs : ""), "GET");
  },
};

// ============================================================
//  REVIEW MODULE (thêm vào Product)
// ============================================================
// Gắn thêm vào Product object
if (typeof Product !== "undefined") {
  Product.getReviews = function (productId) {
    return apiFetch(
      PRODUCT_API,
      "/api/products/" + productId + "/reviews",
      "GET",
    );
  };
  Product.addReview = function (productId, data) {
    // DTO chỉ nhận Rating và Comment (UserId lấy từ JWT token phía backend)
    var payload = {
      Rating: data.Rating || data.rating || 5,
      Comment: data.Comment || data.comment || "",
    };
    return apiFetch(
      PRODUCT_API,
      "/api/products/" + productId + "/reviews",
      "POST",
      payload,
    );
  };
}

// ============================================================
//  FLASH SALE
// ============================================================
const FlashSale = {
  getActive: function () {
    return apiFetch(PRODUCT_API, "/api/flashsale/active", "GET");
  },
  getUpcoming: function () {
    return apiFetch(PRODUCT_API, "/api/flashsale/upcoming", "GET");
  },
  getById: function (id) {
    return apiFetch(PRODUCT_API, "/api/flashsale/" + id, "GET");
  },
};

// ============================================================
//  VOUCHER PUBLIC (customer-facing, no auth required for public)
// ============================================================
const VoucherPublic = {
  getAll: function () {
    return apiFetch(PRODUCT_API, "/api/vouchers/public", "GET");
  },
  getMy: function () {
    return apiFetch(PRODUCT_API, "/api/vouchers/my", "GET");
  },
  save: function (code) {
    return apiFetch(PRODUCT_API, "/api/vouchers/save/" + code, "POST");
  },
  validate: function (code, orderAmount, shopId) {
    return apiFetch(PRODUCT_API, "/api/vouchers/validate", "POST", {
      Code: code,
      OrderAmount: orderAmount,
      ShopId: shopId || null,
    });
  },
};

// ============================================================
//  COMPARE (in-memory via localStorage; server endpoint optional)
// ============================================================
const Compare = {
  _key: "gh_compare",
  _get: function () {
    try { return JSON.parse(localStorage.getItem(this._key) || "[]"); } catch (_) { return []; }
  },
  _save: function (list) {
    try { localStorage.setItem(this._key, JSON.stringify(list)); } catch (_) {}
  },
  getAll: function () { return Promise.resolve(this._get()); },
  add: function (product) {
    var list = this._get();
    if (list.find(function (p) { return p.id === product.id; })) return Promise.resolve(list);
    if (list.length >= 3) return Promise.reject(new Error("Max 3 products"));
    list.push(product);
    this._save(list);
    return Promise.resolve(list);
  },
  remove: function (productId) {
    var list = this._get().filter(function (p) { return p.id !== productId; });
    this._save(list);
    return Promise.resolve(list);
  },
  clear: function () {
    this._save([]);
    return Promise.resolve([]);
  },
};

// ============================================================
//  RECENTLY VIEWED
// ============================================================
const RecentlyViewed = {
  get: function () {
    // Try API, fallback to localStorage
    var token = Auth && Auth.getToken ? Auth.getToken() : null;
    if (token) {
      return apiFetch(PRODUCT_API, "/api/recently-viewed", "GET").catch(function () {
        try { return JSON.parse(localStorage.getItem("gh_recently_viewed") || "[]"); } catch (_) { return []; }
      });
    }
    try { return Promise.resolve(JSON.parse(localStorage.getItem("gh_recently_viewed") || "[]")); }
    catch (_) { return Promise.resolve([]); }
  },
  add: function (productId) {
    return apiFetch(PRODUCT_API, "/api/recently-viewed/" + productId, "POST").catch(function () {});
  },
};

// ============================================================
//  DISPUTE MODULE — Customer-facing (tạo khiếu nại)
// ============================================================
const DisputeApi = {
  create(data) {
    return apiFetch(PRODUCT_API, "/api/disputes", "POST", data);
  },
  getMy(page, limit) {
    const qs = new URLSearchParams({ page: page || 1, limit: limit || 10 });
    return apiFetch(PRODUCT_API, "/api/disputes/my?" + qs.toString(), "GET");
  },
  getDetail(id) {
    return apiFetch(PRODUCT_API, "/api/disputes/" + id, "GET");
  },
};

// ============================================================
//  SHIPPING MODULE
// ============================================================
const Shipping = {
  calculate: function (fromRegion, toRegion, weightGram) {
    var qs = new URLSearchParams({ fromRegion: fromRegion, toRegion: toRegion, weightGram: weightGram });
    return apiFetch(PRODUCT_API, "/api/shipping/calculate?" + qs.toString(), "GET");
  },
  calculateCart: function (fromShopId, toProvince, items) {
    return apiFetch(PRODUCT_API, "/api/shipping/calculate-cart", "POST", {
      fromShopId: fromShopId,
      toProvince: toProvince,
      items: items
    });
  },
  getRegions: function () {
    return apiFetch(PRODUCT_API, "/api/shipping/regions", "GET");
  },
};

// ============================================================
//  CART — getGrouped (grouped by shop with shipping estimate)
// ============================================================
Cart.getGrouped = function (toProvince) {
  var qs = toProvince ? "?toProvince=" + encodeURIComponent(toProvince) : "";
  return apiFetch(PRODUCT_API, "/api/cart/grouped" + qs, "GET");
};

// ============================================================
//  SELLER SUB-ORDER MODULE
// ============================================================
const SellerSubOrder = {
  getAll: function (params) {
    var qs = new URLSearchParams(params || {});
    return apiFetch(ORDER_API, "/api/orders/shop/suborders?" + qs.toString(), "GET");
  },
  confirm: function (id) {
    return apiFetch(ORDER_API, "/api/orders/shop/suborders/" + id + "/confirm", "PUT");
  },
  ship: function (id, trackingCode) {
    return apiFetch(ORDER_API, "/api/orders/shop/suborders/" + id + "/ship", "PUT", { trackingCode: trackingCode });
  },
  cancel: function (id, reason) {
    return apiFetch(ORDER_API, "/api/orders/shop/suborders/" + id + "/cancel", "PUT", { reason: reason });
  },
};

// ============================================================
//  CUSTOMER WALLET MODULE
// ============================================================
const CustomerWallet = {
  get: function () {
    return apiFetch(PRODUCT_API, "/api/customer/wallet", "GET");
  },
  getTransactions: function (params) {
    var qs = new URLSearchParams(params || {});
    return apiFetch(PRODUCT_API, "/api/customer/wallet/transactions?" + qs.toString(), "GET");
  },
  topUp: function (data) {
    return apiFetch(PRODUCT_API, "/api/customer/wallet/topup", "POST", data);
  },
};

// ============================================================
//  FLASH SALE — buy (race-condition safe)
// ============================================================
FlashSale.buy = function (flashSaleProductId, quantity, shippingAddress) {
  return apiFetch(PRODUCT_API, "/api/flashsale/buy", "POST", {
    flashSaleProductId: flashSaleProductId,
    quantity: quantity || 1,
    shippingAddress: shippingAddress || ""
  });
};

// ============================================================
//  CART — add with shopId/shopName support
// ============================================================
(function () {
  var _origAdd = Cart.add.bind(Cart);
  Cart.add = function (product, qty) {
    var items = this.getItems();
    var id = product.Id || product.id || product._id;
    var idx = items.findIndex(function (i) { return (i.id || i.Id) == id; });
    if (idx > -1) {
      items[idx].qty = (items[idx].qty || 1) + (qty || 1);
    } else {
      items.push({
        id: id,
        name: product.Name || product.name,
        price: product.Price || product.price,
        image: product.ImageUrl || product.imageUrl || product.Image || product.image || "",
        category: (product.Category && (product.Category.Name || product.Category.name)) || product.CategoryName || product.Category || product.category || "",
        shopId: product.ShopId || product.shopId || null,
        shopName: product.ShopName || product.shopName || null,
        weightGram: product.WeightGram || product.weightGram || 500,
        qty: qty || 1,
      });
    }
    this._save(items);
    this._flashBadge();
    return items;
  };
})();

// ============================================================
//  VN PROVINCES — 3-level cascade (province → district → ward)
//  Source: https://provinces.open-api.vn/api/
//  Cache: localStorage 24h
// ============================================================
var VNProvinces = (function () {
  var BASE = 'https://provinces.open-api.vn/api';
  var TTL  = 86400000; // 24h

  function _write(key, data) {
    try { localStorage.setItem(key, JSON.stringify({ d: data, t: Date.now() })); } catch (_) {}
  }
  function _read(key) {
    try {
      var c = JSON.parse(localStorage.getItem(key));
      return (c && (Date.now() - c.t) < TTL) ? c.d : null;
    } catch (_) { return null; }
  }
  function _fetch(url, cacheKey) {
    var hit = _read(cacheKey);
    if (hit) return Promise.resolve(hit);
    return fetch(url)
      .then(function (r) { return r.json(); })
      .then(function (d) { _write(cacheKey, d); return d; });
  }

  function getProvinces() {
    return _fetch(BASE + '/p/?depth=1', 'gh_vnp');
  }
  function getDistricts(pCode) {
    // Correct endpoint: /p/{code}?depth=2 returns { districts: [...] }
    // /d/?p= is NOT a supported filter — returns ALL ~700 districts (wrong)
    return _fetch(BASE + '/p/' + pCode + '?depth=2', 'gh_vnd2_' + pCode)
      .then(function (d) { return d.districts || []; });
  }
  function getWards(dCode) {
    // Correct endpoint: /d/{code}?depth=2 returns { wards: [...] }
    // /w/?d= is NOT a supported filter — returns ALL wards (wrong)
    return _fetch(BASE + '/d/' + dCode + '?depth=2', 'gh_vnw2_' + dCode)
      .then(function (d) { return d.wards || []; });
  }

  // Fill one <select> element; returns Promise
  function fillSel(sel, items, placeholder) {
    sel.innerHTML = '<option value="">Đang tải...</option>';
    sel.disabled = true;
    return (items instanceof Promise ? items : Promise.resolve(items))
      .then(function (list) {
        sel.innerHTML = '<option value="">' + (placeholder || '-- Chọn --') + '</option>';
        (list || []).forEach(function (item) {
          var o = document.createElement('option');
          o.value = item.code;
          o.textContent = item.name;
          o.dataset.name = item.name;
          sel.appendChild(o);
        });
        sel.disabled = false;
      })
      .catch(function () {
        sel.innerHTML = '<option value="">Lỗi tải dữ liệu. Nhập thủ công.</option>';
        sel.disabled = false;
      });
  }

  // Reset one select to empty/disabled state
  function resetSel(sel, placeholder) {
    if (!sel) return;
    sel.innerHTML = '<option value="">' + (placeholder || '-- Chọn --') + '</option>';
    sel.disabled = true;
  }

  /**
   * Setup 3-level cascade on given element IDs.
   * opts: { prov, dist, ward } — element IDs
   * onChange: function({ provCode, provName, distCode, distName, wardCode, wardName })
   *           called on every change (even partial)
   */
  function setup(opts, onChange) {
    var provSel = document.getElementById(opts.prov);
    var distSel = opts.dist ? document.getElementById(opts.dist) : null;
    var wardSel = opts.ward ? document.getElementById(opts.ward) : null;
    if (!provSel) return;

    if (distSel) resetSel(distSel, '-- Chọn quận/huyện --');
    if (wardSel) resetSel(wardSel, '-- Chọn phường/xã --');

    fillSel(provSel, getProvinces(), '-- Chọn tỉnh/thành --').then(function () {
      provSel.disabled = false;
    });

    provSel.addEventListener('change', function () {
      var pCode = provSel.value;
      var pName = pCode ? (provSel.options[provSel.selectedIndex].dataset.name || provSel.options[provSel.selectedIndex].textContent) : '';
      if (distSel) resetSel(distSel, '-- Chọn quận/huyện --');
      if (wardSel) resetSel(wardSel, '-- Chọn phường/xã --');
      if (onChange) onChange({ provCode: pCode, provName: pName, distCode: '', distName: '', wardCode: '', wardName: '' });
      if (!pCode || !distSel) return;
      fillSel(distSel, getDistricts(pCode), '-- Chọn quận/huyện --').then(function () {
        distSel.disabled = false;
      });
    });

    if (distSel) {
      distSel.addEventListener('change', function () {
        var pCode = provSel.value;
        var pName = pCode ? (provSel.options[provSel.selectedIndex].dataset.name || provSel.options[provSel.selectedIndex].textContent) : '';
        var dCode = distSel.value;
        var dName = dCode ? (distSel.options[distSel.selectedIndex].dataset.name || distSel.options[distSel.selectedIndex].textContent) : '';
        if (wardSel) resetSel(wardSel, '-- Chọn phường/xã --');
        if (onChange) onChange({ provCode: pCode, provName: pName, distCode: dCode, distName: dName, wardCode: '', wardName: '' });
        if (!dCode || !wardSel) return;
        fillSel(wardSel, getWards(dCode), '-- Chọn phường/xã --').then(function () {
          wardSel.disabled = false;
        });
      });
    }

    if (wardSel) {
      wardSel.addEventListener('change', function () {
        var pCode = provSel.value;
        var pName = pCode ? (provSel.options[provSel.selectedIndex].dataset.name || provSel.options[provSel.selectedIndex].textContent) : '';
        var dCode = distSel ? distSel.value : '';
        var dName = (dCode && distSel) ? (distSel.options[distSel.selectedIndex].dataset.name || distSel.options[distSel.selectedIndex].textContent) : '';
        var wCode = wardSel.value;
        var wName = wCode ? (wardSel.options[wardSel.selectedIndex].dataset.name || wardSel.options[wardSel.selectedIndex].textContent) : '';
        if (onChange) onChange({ provCode: pCode, provName: pName, distCode: dCode, distName: dName, wardCode: wCode, wardName: wName });
      });
    }
  }

  // Map province name → shipping region code (mirrors ShippingRegion.Normalize in C#)
  function getRegion(provinceName) {
    if (!provinceName) return 'OTHER';
    var n = provinceName.toLowerCase();
    var SOUTH = ['hồ chí minh','ho chi minh','hcm','tphcm','tp.hcm','sài gòn','saigon','sai gon',
      'bình dương','binh duong','đồng nai','dong nai','bà rịa','ba ria','vũng tàu','vung tau',
      'long an','tiền giang','tien giang','bến tre','ben tre','vĩnh long','vinh long',
      'trà vinh','tra vinh','đồng tháp','dong thap','an giang','kiên giang','kien giang',
      'cần thơ','can tho','hậu giang','hau giang','sóc trăng','soc trang',
      'bạc liêu','bac lieu','cà mau','ca mau','tây ninh','tay ninh','bình phước','binh phuoc'];
    var NORTH = ['hà nội','ha noi','hanoi','hải phòng','hai phong','quảng ninh','quang ninh',
      'hải dương','hai duong','hưng yên','hung yen','thái bình','thai binh','nam định','nam dinh',
      'hà nam','ha nam','ninh bình','ninh binh','vĩnh phúc','vinh phuc','bắc ninh','bac ninh',
      'bắc giang','bac giang','thái nguyên','thai nguyen','lạng sơn','lang son','cao bằng','cao bang',
      'bắc kạn','bac kan','tuyên quang','tuyen quang','hà giang','ha giang','lào cai','lao cai',
      'yên bái','yen bai','phú thọ','phu tho','sơn la','son la','điện biên','dien bien',
      'lai châu','lai chau','hòa bình','hoa binh'];
    var ISLAND = ['phú quốc','phu quoc','côn đảo','con dao','hoàng sa','hoang sa','trường sa','truong sa','lý sơn','ly son'];
    if (ISLAND.some(function (k) { return n.includes(k); })) return 'ISLAND';
    if (SOUTH.some(function (k) { return n.includes(k); })) return 'SOUTH';
    if (NORTH.some(function (k) { return n.includes(k); })) return 'NORTH';
    // Miền Trung: Thanh Hóa → Lâm Đồng — fallback
    return 'CENTRAL';
  }

  // setupCascade — same as setup() but accepts {province, district, ward} key names
  function setupCascade(ids, onChange) {
    return setup(
      { prov: ids.province || ids.prov, dist: ids.district || ids.dist, ward: ids.ward },
      onChange
    );
  }

  // setValues — fill + pre-select all 3 levels (for edit-address forms)
  function setValues(ids, values) {
    var provId   = ids.province || ids.prov;
    var distId   = ids.district || ids.dist;
    var wardId   = ids.ward;
    var provSel  = provId ? document.getElementById(provId) : null;
    var distSel  = distId ? document.getElementById(distId) : null;
    var wardSel  = wardId ? document.getElementById(wardId) : null;
    var provCode = String(values.provinceCode || '');
    var distCode = String(values.districtCode || '');
    var wardCode = String(values.wardCode     || '');

    if (!provSel || !provCode) return Promise.resolve();

    return fillSel(provSel, getProvinces(), '-- Chọn tỉnh/thành --').then(function () {
      for (var i = 0; i < provSel.options.length; i++) {
        if (String(provSel.options[i].value) === provCode) { provSel.selectedIndex = i; break; }
      }
      if (!distSel || !distCode) return;
      return fillSel(distSel, getDistricts(provCode), '-- Chọn quận/huyện --').then(function () {
        distSel.disabled = false;
        for (var i = 0; i < distSel.options.length; i++) {
          if (String(distSel.options[i].value) === distCode) { distSel.selectedIndex = i; break; }
        }
        if (!wardSel || !wardCode) return;
        return fillSel(wardSel, getWards(distCode), '-- Chọn phường/xã --').then(function () {
          wardSel.disabled = false;
          for (var i = 0; i < wardSel.options.length; i++) {
            if (String(wardSel.options[i].value) === wardCode) { wardSel.selectedIndex = i; break; }
          }
        });
      });
    });
  }

  return {
    getProvinces: getProvinces, getDistricts: getDistricts, getWards: getWards,
    setup: setup, setupCascade: setupCascade, setValues: setValues, getRegion: getRegion
  };
})();

// ============================================================
//  PAYMENT — VNPay + Bank Transfer + Auto-expire
// ============================================================
var Payment = (function () {

  // Tạo URL thanh toán VNPay cho đơn hàng
  function createVNPayUrl(orderId) {
    return apiFetch(PRODUCT_API, '/api/payment/vnpay/create', 'POST', { orderId: orderId });
  }

  // Lấy thông tin chuyển khoản ngân hàng + QR code
  function getBankInfo(orderId) {
    return apiFetch(PRODUCT_API, '/api/payment/bank/info?orderId=' + orderId, 'GET');
  }

  // Khách xác nhận đã chuyển khoản
  function submitBankTransfer(orderId) {
    return apiFetch(PRODUCT_API, '/api/payment/bank/submit', 'POST', { orderId: orderId });
  }

  // Admin xác nhận nhận tiền chuyển khoản
  function adminConfirmBank(orderId) {
    return apiFetch(PRODUCT_API, '/api/payment/admin/bank/confirm/' + orderId, 'POST');
  }

  // Admin: danh sách đơn chờ xác nhận CK
  function getPendingBankOrders() {
    return apiFetch(PRODUCT_API, '/api/payment/admin/bank/pending', 'GET');
  }

  // Kích hoạt auto-expire (gọi định kỳ hoặc khi cần)
  function expireOrders() {
    return apiFetch(PRODUCT_API, '/api/payment/expire', 'POST');
  }

  // Redirect tới trang thanh toán VNPay
  function redirectToVNPay(orderId) {
    return createVNPayUrl(orderId).then(function (res) {
      if (res && res.paymentUrl) {
        window.location.href = res.paymentUrl;
      } else {
        throw new Error('Không tạo được URL thanh toán VNPay');
      }
    });
  }

  // Hiển thị hộp thoại thông tin chuyển khoản
  function showBankTransferModal(orderId, containerEl) {
    return getBankInfo(orderId).then(function (info) {
      if (!containerEl) return info;
      containerEl.innerHTML =
        '<div class="text-center">' +
        '<img src="' + info.qrUrl + '" alt="QR chuyển khoản" style="max-width:220px;border:1px solid #eee;padding:8px;" onerror="this.style.display=\'none\'">' +
        '</div>' +
        '<table class="table table-sm mt-3" style="font-size:13px">' +
        '<tr><td><b>Ngân hàng</b></td><td>' + info.bankName + '</td></tr>' +
        '<tr><td><b>Số tài khoản</b></td><td><b>' + info.accountNumber + '</b></td></tr>' +
        '<tr><td><b>Tên tài khoản</b></td><td>' + info.accountName + '</td></tr>' +
        '<tr><td><b>Số tiền</b></td><td><b>' + info.amount.toLocaleString('vi-VN') + '₫</b></td></tr>' +
        '<tr><td><b>Nội dung CK</b></td><td><b>' + info.orderCode + '</b></td></tr>' +
        '</table>' +
        (info.expireAt ? '<p class="text-danger" style="font-size:12px">⏱ Hết hạn: ' +
          new Date(info.expireAt).toLocaleString('vi-VN') + '</p>' : '');
      return info;
    });
  }

  return {
    createVNPayUrl: createVNPayUrl,
    getBankInfo: getBankInfo,
    submitBankTransfer: submitBankTransfer,
    adminConfirmBank: adminConfirmBank,
    getPendingBankOrders: getPendingBankOrders,
    expireOrders: expireOrders,
    redirectToVNPay: redirectToVNPay,
    showBankTransferModal: showBankTransferModal
  };
})();
