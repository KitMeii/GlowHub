/**
 * ============================================================
 *  GlowHub — api.js  (v3.0 — full e-commerce sync)
 *
 *  Tính năng:
 *  • Auth: login/register/logout + /me + change password
 *  • Cart: hybrid (localStorage cho khách, server-side khi login + sync khi login)
 *  • Order: my-orders / detail / cancel-pending / status-history
 *  • Voucher: validate qua API thật
 *  • Review: public list + user post (chỉ khi đã mua)
 *  • Soft 401 handling: trang công khai không bị hard-redirect
 *  • Login redirect: hỗ trợ ?redirect=... để quay về trang trước
 * ============================================================
 */

// ============================================================
//  CONFIG
// ============================================================
const API_BASE = "http://localhost:5002"; // AuthService
const PRODUCT_API = "http://localhost:5001"; // APIService
const ORDER_API = "http://localhost:5001"; // APIService (alias)

const PUBLIC_PAGES = new Set([
  "",
  "index.html",
  "shop.html",
  "product.html",
  "login.html",
  "register.html",
  "cart.html", // cart hiển thị được khi chưa login
]);

function _currentPage() {
  return (window.location.pathname.split("/").pop() || "index.html").toLowerCase();
}
function _isPublicPage() {
  return PUBLIC_PAGES.has(_currentPage());
}

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
  if (body !== null && body !== undefined) opts.body = JSON.stringify(body);

  const res = await fetch(base + path, opts);

  if (res.status === 401) {
    // Token hết hạn / không hợp lệ
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    if (!_isPublicPage()) {
      const here = encodeURIComponent(window.location.pathname.split("/").pop() || "");
      window.location.href = "login.html?redirect=" + here;
    } else {
      // Trang công khai: chỉ thông báo, không kick ra ngoài
      try {
        if (typeof renderNavUser === "function") renderNavUser();
      } catch (_) {}
    }
    throw new Error("Phiên đăng nhập hết hạn");
  }

  if (!res.ok) {
    let msg = "Lỗi " + res.status;
    try {
      const d = await res.json();
      msg = d.message || d.Message || msg;
    } catch (_) {}
    const err = new Error(msg);
    err.status = res.status;
    throw err;
  }

  const ct = res.headers.get("content-type") || "";
  if (ct.includes("application/json")) return res.json();
  return res.text();
}

// ============================================================
//  AUTH MODULE
// ============================================================
const Auth = {
  async login(credentials) {
    const data = await apiFetch(API_BASE, "/api/Auth/login", "POST", credentials);
    const token = data.token || data.Token || data.accessToken || data.AccessToken;
    const user = data.user || data.User || data;
    if (token) {
      localStorage.setItem("token", token);
      localStorage.setItem("user", JSON.stringify(user));
    }
    return { token, user };
  },

  async register(userData) {
    return apiFetch(API_BASE, "/api/Auth/register", "POST", userData);
  },

  getCurrentUser() {
    try {
      return JSON.parse(localStorage.getItem("user") || "null");
    } catch {
      return null;
    }
  },

  isLoggedIn() {
    return !!localStorage.getItem("token") && !!localStorage.getItem("user");
  },

  isAdmin() {
    const u = this.getCurrentUser();
    if (!u) return false;
    const t = u.userType ?? u.UserType ?? u.role ?? u.Role ?? 0;
    return t === 1 || t === "Admin" || t === "admin";
  },

  logout() {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    window.location.href = "login.html";
  },
};

// ============================================================
//  ACCOUNT (self-service /me + my-orders)
// ============================================================
const Account = {
  getMe()                  { return apiFetch(API_BASE,  "/api/users/me", "GET"); },
  updateMe(data)           { return apiFetch(API_BASE,  "/api/users/me", "PUT", data); },
  changePassword(data)     { return apiFetch(API_BASE,  "/api/users/me/change-password", "POST", data); },

  myOrders()               { return apiFetch(ORDER_API, "/api/orders", "GET"); },
  orderDetail(id)          { return apiFetch(ORDER_API, "/api/orders/" + id, "GET"); },
  cancelMyOrder(id, note)  {
    return apiFetch(ORDER_API, "/api/orders/" + id + "/cancel", "PUT", {
      note: note || null,
      clientCreatedAt: new Date().toISOString(),
    });
  },
  orderStatusLogs(orderId) { return apiFetch(ORDER_API, "/api/orderstatuslogs/by-order/" + orderId, "GET"); },
};

// ============================================================
//  PRODUCT MODULE
// ============================================================
const Product = {
  async getAll(params = {}) {
    const qs = new URLSearchParams(params).toString();
    const path = "/api/products" + (qs ? "?" + qs : "");
    return apiFetch(PRODUCT_API, path, "GET");
  },
  async getById(id)       { return apiFetch(PRODUCT_API, "/api/products/" + id, "GET"); },
  async create(data)      { return apiFetch(PRODUCT_API, "/api/products", "POST", data); },
  async update(id, data)  { return apiFetch(PRODUCT_API, "/api/products/" + id, "PUT", data); },
  async delete(id)        { return apiFetch(PRODUCT_API, "/api/products/" + id, "DELETE"); },
};

// ============================================================
//  CATEGORY MODULE
// ============================================================
const Category = {
  async getAll()          { return apiFetch(PRODUCT_API, "/api/categories", "GET"); },
  async getById(id)       { return apiFetch(PRODUCT_API, "/api/categories/" + id, "GET"); },
};

// ============================================================
//  ORDER MODULE
// ============================================================
const Order = {
  async getAll()                      { return apiFetch(ORDER_API, "/api/orders", "GET"); },
  async getById(id)                   { return apiFetch(ORDER_API, "/api/orders/" + id, "GET"); },
  async getMyOrders()                 { return apiFetch(ORDER_API, "/api/orders", "GET"); },
  async checkout(data)                { return apiFetch(ORDER_API, "/api/orders/checkout", "POST", data); },
  async updateStatus(id, status, opts = {}) {
    return apiFetch(ORDER_API, "/api/orders/" + id + "/status", "PUT", {
      status, note: opts.note, clientCreatedAt: opts.clientCreatedAt,
    });
  },
  async cancel(id, note) {
    return apiFetch(ORDER_API, "/api/orders/" + id + "/cancel", "PUT", {
      note: note || null,
      clientCreatedAt: new Date().toISOString(),
    });
  },
};

// ============================================================
//  REVIEW MODULE
// ============================================================
const Review = {
  async getByProduct(productId) {
    return apiFetch(PRODUCT_API, `/api/products/${productId}/reviews`, "GET");
  },
  async submit(productId, data) {
    return apiFetch(PRODUCT_API, `/api/products/${productId}/reviews`, "POST", data);
  },
};

// ============================================================
//  CART MODULE — HYBRID (localStorage cho khách, server-side khi login)
// ============================================================
const Cart = {
  STORAGE_KEY: "glowhub_cart",
  _serverCache: null,   // cache items từ server cho phiên hiện tại
  _serverPromise: null, // in-flight promise tránh fetch song song

  // ── LOCAL (anonymous) ─────────────────────────────────
  _localGetItems() {
    try { return JSON.parse(localStorage.getItem(this.STORAGE_KEY) || "[]"); }
    catch { return []; }
  },
  _localSave(items) {
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(items));
  },

  // ── REMOTE (server-side, khi đã login) ─────────────────
  async _remoteFetch() {
    if (this._serverPromise) return this._serverPromise;
    this._serverPromise = (async () => {
      try {
        const res = await apiFetch(PRODUCT_API, "/api/cart", "GET");
        const items = (res?.items || []).map((it) => ({
          // CartItem ID server-side để cập nhật/xóa
          cartItemId: it.id ?? it.Id,
          id: it.productId ?? it.ProductId,
          name: it.productName ?? it.ProductName ?? "Sản phẩm",
          price: it.unitPrice ?? it.UnitPrice ?? 0,
          image: it.productImage ?? it.ProductImage ?? "",
          qty: it.quantity ?? it.Quantity ?? 1,
          stock: it.stockAvailable ?? it.StockAvailable ?? 0,
        }));
        this._serverCache = items;
        return items;
      } finally {
        this._serverPromise = null;
      }
    })();
    return this._serverPromise;
  },

  /** Bắt đầu fetch cart từ server (gọi sau login) — non-blocking */
  async refresh() {
    if (!Auth.isLoggedIn()) return this._localGetItems();
    this._serverCache = null;
    const items = await this._remoteFetch();
    this._updateUI();
    return items;
  },

  /** Sync localStorage cart → server (gọi sau khi login thành công) */
  async syncToServer() {
    if (!Auth.isLoggedIn()) return;
    const local = this._localGetItems();
    if (local.length === 0) return;
    for (const item of local) {
      try {
        await apiFetch(PRODUCT_API, "/api/cart/add", "POST", {
          productId: item.id,
          quantity: item.qty || 1,
        });
      } catch (_) {
        // Skip lỗi (vd: sản phẩm hết hàng) — không chặn flow login
      }
    }
    localStorage.removeItem(this.STORAGE_KEY);
    this._serverCache = null;
  },

  /** Lấy items hiện tại (sync) — dùng cache nếu có; UI nên gọi getItemsAsync() để có dữ liệu mới nhất */
  getItems() {
    if (Auth.isLoggedIn()) {
      return this._serverCache || []; // chưa fetch xong → cache rỗng
    }
    return this._localGetItems();
  },

  /** Async — chắc chắn có dữ liệu mới nhất */
  async getItemsAsync() {
    if (Auth.isLoggedIn()) {
      if (this._serverCache) return this._serverCache;
      return await this._remoteFetch();
    }
    return this._localGetItems();
  },

  /** Thêm sản phẩm vào giỏ */
  async add(product, qty = 1) {
    const id = product.Id || product.id || product._id;
    qty = qty || 1;

    if (Auth.isLoggedIn()) {
      try {
        await apiFetch(PRODUCT_API, "/api/cart/add", "POST", {
          productId: id,
          quantity: qty,
        });
        this._serverCache = null;
        await this._remoteFetch();
      } catch (err) {
        showGlobalToast(err.message || "Không thêm được vào giỏ", "error");
        return this._serverCache || [];
      }
    } else {
      const items = this._localGetItems();
      const idx = items.findIndex((i) => (i.id || i.Id) == id);
      if (idx > -1) {
        items[idx].qty = (items[idx].qty || 1) + qty;
      } else {
        items.push({
          id: id,
          name: product.Name || product.name,
          price: product.Price || product.price,
          image: product.Image || product.image || product.ImageUrl || product.imageUrl || "",
          category: product.Category || product.category || "",
          qty: qty,
        });
      }
      this._localSave(items);
    }
    this._updateUI();
    this._flashBadge();
    return this.getItems();
  },

  /** Đổi số lượng. id = productId (local) hoặc cartItemId (remote nếu có) */
  async setQty(productId, qty) {
    if (Auth.isLoggedIn()) {
      const items = await this.getItemsAsync();
      const item = items.find((i) => i.id == productId);
      if (!item) return;
      if (qty <= 0) {
        await this.remove(productId);
        return;
      }
      // Server chỉ có endpoint /add (cộng dồn) và /delete. Để set qty cụ thể:
      // xóa item rồi add lại với qty mới (đơn giản hơn là gọi admin endpoint).
      try {
        await apiFetch(PRODUCT_API, "/api/cart/" + item.cartItemId, "DELETE");
        await apiFetch(PRODUCT_API, "/api/cart/add", "POST", {
          productId: productId,
          quantity: qty,
        });
        this._serverCache = null;
        await this._remoteFetch();
      } catch (err) {
        showGlobalToast(err.message || "Lỗi cập nhật giỏ hàng", "error");
      }
    } else {
      const items = this._localGetItems();
      const idx = items.findIndex((i) => (i.id || i.Id) == productId);
      if (idx > -1) {
        if (qty <= 0) items.splice(idx, 1);
        else items[idx].qty = qty;
      }
      this._localSave(items);
    }
    this._updateUI();
  },

  /** Xóa một sản phẩm khỏi giỏ */
  async remove(productId) {
    if (Auth.isLoggedIn()) {
      const items = await this.getItemsAsync();
      const item = items.find((i) => i.id == productId);
      if (item && item.cartItemId) {
        try {
          await apiFetch(PRODUCT_API, "/api/cart/" + item.cartItemId, "DELETE");
          this._serverCache = null;
          await this._remoteFetch();
        } catch (err) {
          showGlobalToast(err.message || "Lỗi xóa sản phẩm", "error");
        }
      }
    } else {
      const items = this._localGetItems().filter((i) => (i.id || i.Id) != productId);
      this._localSave(items);
    }
    this._updateUI();
  },

  /** Xóa toàn bộ giỏ */
  async clear() {
    if (Auth.isLoggedIn()) {
      try {
        await apiFetch(PRODUCT_API, "/api/cart/clear", "DELETE");
        this._serverCache = [];
      } catch (err) {
        showGlobalToast(err.message || "Lỗi xóa giỏ", "error");
      }
    } else {
      localStorage.removeItem(this.STORAGE_KEY);
    }
    this._updateUI();
  },

  totalCount() {
    return this.getItems().reduce((s, i) => s + (i.qty || 1), 0);
  },
  totalPrice() {
    return this.getItems().reduce((s, i) => s + (i.price || 0) * (i.qty || 1), 0);
  },

  _updateUI() {
    const items = this.getItems();
    const count = this.totalCount();
    const total = this.totalPrice();

    ["cart-count", "cart-count-mobile"].forEach((id) => {
      const el = document.getElementById(id);
      if (el) el.textContent = count;
    });

    const listEl = document.getElementById("cart-list");
    if (listEl) {
      if (items.length === 0) {
        listEl.innerHTML =
          '<li class="list-group-item text-center text-muted py-5">Giỏ hàng trống</li>';
      } else {
        listEl.innerHTML = items
          .map(
            (item) => `
          <li class="list-group-item px-0 py-2">
            <div class="d-flex gap-3 align-items-start">
              <img src="${item.image || "https://via.placeholder.com/52x52/f5f0ea/aaa?text=IMG"}"
                   style="width:52px;height:52px;object-fit:cover;border-radius:4px;"
                   onerror="this.src='https://via.placeholder.com/52x52/f5f0ea/aaa?text=IMG'" />
              <div class="flex-grow-1 min-width-0">
                <div style="font-size:13px;font-weight:500;line-height:1.3" class="text-truncate">${item.name}</div>
                <div style="font-size:12px;color:#f759ab;margin-top:2px">${_fmtMoney(item.price)}</div>
                <div class="d-flex align-items-center gap-2 mt-1">
                  <button onclick="Cart.setQty(${item.id}, ${(item.qty || 1) - 1})"
                    style="width:22px;height:22px;border:1px solid #ddd;background:none;cursor:pointer;border-radius:3px;line-height:1">−</button>
                  <span style="font-size:13px;min-width:20px;text-align:center">${item.qty || 1}</span>
                  <button onclick="Cart.setQty(${item.id}, ${(item.qty || 1) + 1})"
                    style="width:22px;height:22px;border:1px solid #ddd;background:none;cursor:pointer;border-radius:3px;line-height:1">+</button>
                  <button onclick="Cart.remove(${item.id})"
                    style="margin-left:auto;background:none;border:none;color:#999;cursor:pointer;font-size:16px;line-height:1">✕</button>
                </div>
              </div>
            </div>
          </li>
        `,
          )
          .join("");
      }
    }

    const totalEl = document.getElementById("cart-total");
    if (totalEl) totalEl.textContent = _fmtMoney(total);

    // Notify other parts of the page (cart.html re-renders chính nó)
    try { document.dispatchEvent(new CustomEvent("cart:updated")); } catch (_) {}
  },

  _flashBadge() {
    ["cart-count", "cart-count-mobile"].forEach((id) => {
      const el = document.getElementById(id);
      if (!el) return;
      el.style.transform = "scale(1.6)";
      el.style.transition = "transform 0.15s";
      setTimeout(() => { el.style.transform = "scale(1)"; }, 200);
    });
    const offcanvasEl = document.getElementById("offcanvasCart");
    if (offcanvasEl && typeof bootstrap !== "undefined") {
      const oc = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
      oc.show();
    }
  },
};

// ============================================================
//  FORMAT HELPERS
// ============================================================
function _fmtMoney(n) {
  if (!n && n !== 0) return "--";
  return new Intl.NumberFormat("vi-VN").format(n) + " ₫";
}

// ============================================================
//  PRODUCT IMAGE HELPERS
//  ImageUrl trong DB có thể là:
//   - JSON array string: '["url1","url2",...]' (sản phẩm nhiều ảnh)
//   - Single URL string: 'https://...' (backward compatible)
//   - Rỗng/null
// ============================================================
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

function pickFirstImage(raw) {
  const arr = parseProductImages(raw);
  return arr.length ? arr[0] : "";
}

/** Lấy danh sách ảnh từ product object (xét cả PascalCase và camelCase) */
function getProductImageList(p) {
  if (!p) return [];
  const raw = p.ImageUrl || p.imageUrl || p.Image || p.image || "";
  return parseProductImages(raw);
}

// ============================================================
//  TOAST HELPER
// ============================================================
function showGlobalToast(msg, type = "success") {
  let container = document.getElementById("_globalToastContainer");
  if (!container) {
    container = document.createElement("div");
    container.id = "_globalToastContainer";
    container.style.cssText =
      "position:fixed;bottom:24px;right:24px;z-index:9999;display:flex;flex-direction:column;gap:8px";
    document.body.appendChild(container);
  }
  const t = document.createElement("div");
  t.style.cssText = `
    padding: 12px 20px;
    background: ${type === "success" ? "#111" : "#dc2626"};
    color: #fff;
    border-radius: 4px;
    font-size: 13px;
    font-weight: 500;
    box-shadow: 0 4px 16px rgba(0,0,0,0.15);
    border-left: 3px solid ${type === "success" ? "#f759ab" : "#ff6b6b"};
    transform: translateX(120%);
    transition: transform 0.3s ease;
  `;
  t.textContent = (type === "success" ? "✓  " : "✕  ") + msg;
  container.appendChild(t);
  requestAnimationFrame(() => { t.style.transform = "translateX(0)"; });
  setTimeout(() => {
    t.style.transform = "translateX(120%)";
    setTimeout(() => t.remove(), 350);
  }, 3000);
}

// ============================================================
//  NAVBAR — render user area
// ============================================================
function renderNavUser() {
  const user = Auth.getCurrentUser();
  const areas = ["nav-user-area", "nav-user-mobile"];
  areas.forEach((id) => {
    const el = document.getElementById(id);
    if (!el) return;
    if (user) {
      const name = user.name || user.Name || user.userName || user.UserName || "Tài khoản";
      el.innerHTML = `
        <div class="dropdown">
          <a href="#" class="nav-link dropdown-toggle d-flex align-items-center gap-1"
             data-bs-toggle="dropdown" style="font-size:13px">
            <span style="width:28px;height:28px;background:#f759ab;border-radius:50%;
                         display:inline-flex;align-items:center;justify-content:center;
                         color:#fff;font-size:11px;font-weight:600">
              ${name.charAt(0).toUpperCase()}
            </span>
            ${name}
          </a>
          <ul class="dropdown-menu dropdown-menu-end border-0 shadow-sm" style="min-width:180px">
            <li><a class="dropdown-item" href="checkout.html#orders">📦 Đơn hàng của tôi</a></li>
            <li><a class="dropdown-item" href="checkout.html#account">👤 Tài khoản</a></li>
            ${Auth.isAdmin() ? '<li><a class="dropdown-item" href="admin.html">⚙️ Quản trị</a></li>' : ""}
            <li><hr class="dropdown-divider"></li>
            <li><a class="dropdown-item" href="#" onclick="Auth.logout();return false"
                   style="color:#dc2626">🚪 Đăng xuất</a></li>
          </ul>
        </div>
      `;
    } else {
      el.innerHTML = `
        <div class="d-flex gap-2 align-items-center">
          <a href="login.html" class="nav-link" style="font-size:13px">Đăng nhập</a>
          <a href="register.html" class="btn btn-sm"
             style="background:#111;color:#fff;border-radius:0;font-size:12px;
                    letter-spacing:1px;padding:7px 14px;text-transform:uppercase">Đăng ký</a>
        </div>
      `;
    }
  });
}

// ============================================================
//  INDEX PAGE — render sản phẩm từ API vào grid
// ============================================================
async function renderProductsOnIndex() {
  const grid = document.getElementById("new-products-grid");
  if (!grid) return;

  let products = [];
  try {
    const result = await Product.getAll({ isActive: true });
    products = Array.isArray(result)
      ? result
      : result.data
        ? result.data
        : result.items
          ? result.items
          : result.products
            ? result.products
            : [];
  } catch (_) {
    _attachStaticCartButtons();
    return;
  }

  if (!products || products.length === 0) {
    _attachStaticCartButtons();
    return;
  }

  grid.innerHTML = products
    .map((p) => {
      const id = p.Id || p.id;
      const name = p.Name || p.name || "Sản phẩm";
      const price = p.Price || p.price || 0;
      const img = pickFirstImage(p.Image || p.image || p.ImageUrl || p.imageUrl || "");
      const isNew = p.IsNew || p.isNew;
      const sale = p.SalePercent || p.salePercent || p.discount;

      return `
      <div class="col-6 col-md-3">
        <div class="product-card bg-white" data-product-id="${id}">
          ${isNew ? '<span class="badge-new">Mới</span>' : ""}
          ${sale ? `<span class="badge-sale">-${sale}%</span>` : ""}
          <img src="${img}" alt="${name}"
               onerror="this.src='https://images.unsplash.com/photo-1598033129183-c4f50c736f10?w=400&q=80'" />
          <div class="p-3">
            <a href="product.html?id=${id}" class="product-title d-block mb-1">${name}</a>
            <div class="stars" style="font-size:12px">★★★★★</div>
            <span class="product-price">${_fmtMoney(price)}</span>
          </div>
          <button class="btn-add-cart"
            onclick='Cart.add(${JSON.stringify(p)}).then(()=>showGlobalToast("Đã thêm vào giỏ hàng"))'>
            Thêm Vào Giỏ
          </button>
        </div>
      </div>
    `;
    })
    .join("");
}

function _attachStaticCartButtons() {
  document.querySelectorAll(".btn-add-cart").forEach((btn) => {
    if (btn.dataset.cartBound) return;
    btn.dataset.cartBound = "1";
    btn.addEventListener("click", function () {
      const card = this.closest(".product-card") || this.closest(".swiper-slide");
      const name = card?.querySelector(".product-title")?.textContent?.trim() || "Sản phẩm";
      const priceText = card?.querySelector(".product-price")?.textContent || "0";
      const price = parseInt(priceText.replace(/[^0-9]/g, "")) || 0;
      const img = card?.querySelector("img")?.src || "";
      const id = card?.dataset.productId || Date.now();

      Cart.add({ id, name, price, image: img }).then(() => {
        showGlobalToast('✓ Đã thêm "' + name + '" vào giỏ hàng');
      });
    });
  });
}

// ============================================================
//  BANNER / SETTINGS / FEATURED / VOUCHER
// ============================================================
const Banner = {
  async getAll()        { return apiFetch(PRODUCT_API, "/api/Banners", "GET"); },
  async create(data)    { return apiFetch(PRODUCT_API, "/api/Banners", "POST", data); },
  async update(id, d)   { return apiFetch(PRODUCT_API, "/api/Banners/" + id, "PUT", d); },
  async delete(id)      { return apiFetch(PRODUCT_API, "/api/Banners/" + id, "DELETE"); },
};

const SiteSettings = {
  async getAll(group)   { return apiFetch(PRODUCT_API, "/api/SiteSettings" + (group ? "?group=" + group : ""), "GET"); },
  async update(k, v)    { return apiFetch(PRODUCT_API, "/api/SiteSettings/" + k, "PUT", { value: v }); },
  async bulkUpdate(o)   { return apiFetch(PRODUCT_API, "/api/SiteSettings/bulk", "PUT", o); },
};

const Featured = {
  async getBySection(s) { return apiFetch(PRODUCT_API, "/api/FeaturedProducts?section=" + s, "GET"); },
  async add(pid, s)     { return apiFetch(PRODUCT_API, "/api/FeaturedProducts", "POST", { productId: pid, section: s }); },
  async remove(id)      { return apiFetch(PRODUCT_API, "/api/FeaturedProducts/" + id, "DELETE"); },
};

const Voucher = {
  async validate(code, orderAmount) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/validate", "POST", { code, orderAmount });
  },
  async getAll() { return apiFetch(PRODUCT_API, "/api/Vouchers", "GET"); },
};

// ============================================================
//  AUTH GUARDS
// ============================================================
function adminGuard() {
  if (!Auth.isLoggedIn()) {
    alert("Vui lòng đăng nhập!");
    window.location.href = "login.html?redirect=admin.html";
    return false;
  }
  if (!Auth.isAdmin()) {
    alert("Bạn không có quyền truy cập khu vực này!");
    window.location.href = "index.html";
    return false;
  }
  return true;
}

/** Trang cần login user (không nhất thiết admin). Trả về true nếu pass. */
function requireLogin(currentFile) {
  if (Auth.isLoggedIn()) return true;
  const redirect = currentFile || (window.location.pathname.split("/").pop() || "index.html");
  window.location.href = "login.html?redirect=" + encodeURIComponent(redirect);
  return false;
}

// ============================================================
//  ADMIN TOPBAR
// ============================================================
function renderAdminTopbar() {
  const user = Auth.getCurrentUser();
  const nameEl = document.getElementById("adminName");
  const avatarEl = document.getElementById("adminAvatar");
  if (!user) return;
  const name = user.name || user.Name || user.userName || user.UserName || "Admin";
  if (nameEl) nameEl.textContent = name;
  if (avatarEl) avatarEl.textContent = name.charAt(0).toUpperCase();
}

// ============================================================
//  AUTO-INIT
// ============================================================
document.addEventListener("DOMContentLoaded", function () {
  const page = _currentPage();

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

        if (errorDiv) errorDiv.style.display = "none";
        btn.disabled = true;
        btn.textContent = "Đang đăng nhập...";

        try {
          const res = await Auth.login({ Username: username, Password: password });
          if (res && res.user) {
            // Sync cart local → server
            try { await Cart.syncToServer(); } catch (_) {}

            showGlobalToast("Đăng nhập thành công!");

            // Đọc ?redirect= để quay lại trang trước (nếu hợp lệ)
            const params = new URLSearchParams(window.location.search);
            const redirect = params.get("redirect");
            setTimeout(() => {
              if (Auth.isAdmin()) {
                window.location.href = "admin.html";
              } else if (redirect && !redirect.includes("admin.html")) {
                window.location.href = redirect;
              } else {
                window.location.href = "index.html";
              }
            }, 400);
          } else {
            throw new Error("Tài khoản hoặc mật khẩu không đúng!");
          }
        } catch (err) {
          if (errorSpan) errorSpan.textContent = err.message || "Tài khoản hoặc mật khẩu không đúng!";
          if (errorDiv) {
            errorDiv.style.display = "block";
            errorDiv.classList.remove("shake-animation");
            void errorDiv.offsetWidth;
            errorDiv.classList.add("shake-animation");
          }
          btn.disabled = false;
          btn.textContent = "Đăng nhập ngay";
        }
      });
    }
    return;
  }

  // ---- REGISTER PAGE ----
  if (page === "register.html") {
    const form = document.getElementById("register-form");
    if (form) {
      form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const fd = new FormData(e.target);
        const userData = Object.fromEntries(fd.entries());

        // Validate confirm password nếu có
        if (userData.ConfirmPassword !== undefined) {
          if (userData.Password !== userData.ConfirmPassword) {
            showGlobalToast("Mật khẩu xác nhận không khớp", "error");
            return;
          }
          delete userData.ConfirmPassword;
        }

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
    Cart._updateUI();
    renderAdminTopbar();
    const logoutBtn = document.getElementById("adminLogoutBtn");
    if (logoutBtn) logoutBtn.addEventListener("click", Auth.logout.bind(Auth));
    return;
  }

  // ---- INDEX + CÁC TRANG KHÁC ----

  // Khởi động cart UI ngay (sẽ tự update khi server fetch xong)
  Cart._updateUI();
  // Nếu đã login → load cart từ server
  if (Auth.isLoggedIn()) {
    Cart.refresh().catch(() => {});
  }

  renderNavUser();

  if (document.getElementById("new-products-grid")) {
    renderProductsOnIndex().then(() => {
      _attachStaticCartButtons();
    });
  } else {
    _attachStaticCartButtons();
  }
});
