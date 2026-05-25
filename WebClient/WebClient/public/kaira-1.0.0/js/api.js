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
const PRODUCT_API = "http://localhost:5001"; // APIService — Products, Orders, Cart
const ORDER_API = "http://localhost:5001"; // APIService — Products, Orders, Cart

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

  const res = await fetch(base + path, opts);

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
    const user = data.user || data.User || data;
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
    const t = u.userType ?? u.UserType ?? u.role ?? u.Role ?? 0;
    return t === 1 || t === "Admin" || t === "admin";
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
    const qs = new URLSearchParams(params).toString();
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
//  ORDER MODULE
// ============================================================
const Order = {
  async getAll() {
    return apiFetch(ORDER_API, "/api/orders", "GET");
  },

  async getById(id) {
    return apiFetch(ORDER_API, "/api/orders/" + id, "GET");
  },

  async getMyOrders() {
    return apiFetch(ORDER_API, "/api/orders/my", "GET");
  },

  async create(data) {
    return apiFetch(ORDER_API, "/api/orders", "POST", data);
  },

  async updateStatus(id, status) {
    return apiFetch(ORDER_API, "/api/orders/" + id + "/status", "PUT", {
      status,
    });
  },

  async cancel(id) {
    return apiFetch(ORDER_API, "/api/orders/" + id + "/cancel", "POST");
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
        image: product.Image || product.image || "",
        category: product.Category || product.category || "",
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
  requestAnimationFrame(() => {
    t.style.transform = "translateX(0)";
  });
  setTimeout(() => {
    t.style.transform = "translateX(120%)";
    setTimeout(() => t.remove(), 350);
  }, 3000);
}

// ============================================================
//  NAVBAR — render user area (index.html)
// ============================================================
function renderNavUser() {
  const user = Auth.getCurrentUser();
  const areas = ["nav-user-area", "nav-user-mobile"];
  areas.forEach((id) => {
    const el = document.getElementById(id);
    if (!el) return;
    if (user) {
      const name =
        user.name || user.Name || user.userName || user.UserName || "Tài khoản";
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
            <li><a class="dropdown-item" href="checkout.html">👤 Hồ sơ / Đơn hàng</a></li>
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
    // API có thể trả array thẳng hoặc { data: [...] } hoặc { items: [...] }
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
    // API chưa sẵn sàng → giữ nguyên HTML tĩnh hiện tại
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
      const img = p.Image || p.image || "";
      const cat = p.Category || p.category || "";
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
            <a href="#" class="product-title d-block mb-1">${name}</a>
            <div class="stars" style="font-size:12px">★★★★★</div>
            <span class="product-price">${_fmtMoney(price)}</span>
          </div>
          <button class="btn-add-cart"
            onclick="Cart.add(${JSON.stringify(p).replace(/"/g, "'")});showGlobalToast('Đã thêm vào giỏ hàng')">
            Thêm Vào Giỏ
          </button>
        </div>
      </div>
    `;
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
        card?.querySelector(".product-title")?.textContent?.trim() ||
        "Sản phẩm";
      const priceText =
        card?.querySelector(".product-price")?.textContent || "0";
      const price = parseInt(priceText.replace(/[^0-9]/g, "")) || 0;
      const img = card?.querySelector("img")?.src || "";
      const id = card?.dataset.productId || Date.now();

      Cart.add({ id, name, price, image: img });
      showGlobalToast('✓ Đã thêm "' + name + '" vào giỏ hàng');
    });
  });
}

// ============================================================
//  BANNER MODULE
// ============================================================
const Banner = {
  async getAll() {
    return apiFetch(PRODUCT_API, "/api/Banners", "GET");
  },
  async create(data) {
    return apiFetch(PRODUCT_API, "/api/Banners", "POST", data);
  },
  async update(id, data) {
    return apiFetch(PRODUCT_API, "/api/Banners/" + id, "PUT", data);
  },
  async delete(id) {
    return apiFetch(PRODUCT_API, "/api/Banners/" + id, "DELETE");
  },
};

// ============================================================
//  SITE SETTINGS MODULE
// ============================================================
const SiteSettings = {
  async getAll(group) {
    const qs = group ? "?group=" + group : "";
    return apiFetch(PRODUCT_API, "/api/SiteSettings" + qs, "GET");
  },
  async update(key, value) {
    return apiFetch(PRODUCT_API, "/api/SiteSettings/" + key, "PUT", { value });
  },
  async bulkUpdate(updates) {
    return apiFetch(PRODUCT_API, "/api/SiteSettings/bulk", "PUT", updates);
  },
};

// ============================================================
//  FEATURED PRODUCTS MODULE
// ============================================================
const Featured = {
  async getBySection(section) {
    return apiFetch(
      PRODUCT_API,
      "/api/FeaturedProducts?section=" + section,
      "GET",
    );
  },
  async add(productId, section) {
    return apiFetch(PRODUCT_API, "/api/FeaturedProducts", "POST", {
      productId,
      section,
    });
  },
  async remove(id) {
    return apiFetch(PRODUCT_API, "/api/FeaturedProducts/" + id, "DELETE");
  },
};

// ============================================================
//  VOUCHER MODULE
// ============================================================
const Voucher = {
  async validate(code, orderAmount) {
    return apiFetch(PRODUCT_API, "/api/Vouchers/validate", "POST", {
      code,
      orderAmount,
    });
  },
  async getAll() {
    return apiFetch(PRODUCT_API, "/api/Vouchers", "GET");
  },
};

// ============================================================
//  ADMIN GUARD
// ============================================================
function adminGuard() {
  if (!Auth.isLoggedIn()) {
    alert("Vui lòng đăng nhập!");
    window.location.href = "login.html";
    return false;
  }
  if (!Auth.isAdmin()) {
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
              if (Auth.isAdmin()) {
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
