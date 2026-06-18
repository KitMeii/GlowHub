/**
 * GlowHub — layout.js
 * Render header (topbar + navbar + offcanvas-cart + offcanvas-nav) và footer dùng chung
 * cho mọi trang user-facing. SVG sprite cũng inject ở đây.
 *
 * Cách dùng:
 *   <body>
 *     <div id="gh-layout-header"></div>
 *     <main> ...content... </main>
 *     <div id="gh-layout-footer"></div>
 *     <script src="js/api.js"></script>
 *     <script src="js/layout.js"></script>
 *     <script>mountLayout({ active: 'home' });</script>
 *   </body>
 */

(function () {
  const SVG_SPRITE = `
    <svg xmlns="http://www.w3.org/2000/svg" style="display:none">
      <defs>
        <symbol id="gh-i-heart" viewBox="0 0 24 24">
          <path fill="currentColor" d="M20.16 4.61A6.27 6.27 0 0 0 12 4a6.27 6.27 0 0 0-8.16 9.48l7.45 7.45a1 1 0 0 0 1.42 0l7.45-7.45a6.27 6.27 0 0 0 0-8.87Z"/>
        </symbol>
        <symbol id="gh-i-cart" viewBox="0 0 24 24">
          <path fill="currentColor" d="M8.5 19a1.5 1.5 0 1 0 1.5 1.5A1.5 1.5 0 0 0 8.5 19ZM19 16H7a1 1 0 0 1 0-2h8.491a3.013 3.013 0 0 0 2.885-2.176l1.585-5.55A1 1 0 0 0 19 5H6.74a3.007 3.007 0 0 0-2.82-2H3a1 1 0 0 0 0 2h.921a1.005 1.005 0 0 1 .962.725l.155.545v.005l1.641 5.742A3 3 0 0 0 7 18h12a1 1 0 0 0 0-2Zm-1.326-9l-1.22 4.274a1.005 1.005 0 0 1-.963.726H8.754l-.255-.892L7.326 7ZM16.5 19a1.5 1.5 0 1 0 1.5 1.5a1.5 1.5 0 0 0-1.5-1.5Z"/>
        </symbol>
        <symbol id="gh-i-search" viewBox="0 0 24 24">
          <path fill="currentColor" d="M21.71 20.29L18 16.61A9 9 0 1 0 16.61 18l3.68 3.68a1 1 0 0 0 1.42 0a1 1 0 0 0 0-1.39ZM11 18a7 7 0 1 1 7-7a7 7 0 0 1-7 7Z"/>
        </symbol>
        <symbol id="gh-i-user" viewBox="0 0 24 24">
          <path fill="currentColor" d="M15.71 12.71a6 6 0 1 0-7.42 0a10 10 0 0 0-6.22 8.18a1 1 0 0 0 2 .22a8 8 0 0 1 15.9 0a1 1 0 0 0 1 .89h.11a1 1 0 0 0 .88-1.1a10 10 0 0 0-6.25-8.19ZM12 12a4 4 0 1 1 4-4a4 4 0 0 1-4 4Z"/>
        </symbol>
      </defs>
    </svg>
  `;

  const NAV_ITEMS = [
    { key: 'home',    label: 'Trang Chủ',  href: 'index.html' },
    { key: 'shop',    label: 'Cửa Hàng',   href: 'shop.html', dropdown: [
        { label: 'Tất Cả Sản Phẩm', href: 'shop.html' },
        { label: 'Hàng Mới Về',     href: 'shop.html?section=new' },
        { label: 'Bán Chạy Nhất',   href: 'shop.html?section=best' },
      ] },
    { key: 'cart',    label: 'Giỏ Hàng',   href: 'cart.html' },
    { key: 'account', label: 'Tài Khoản',  href: 'checkout.html#account' },
  ];

  function navHtml(active) {
    return NAV_ITEMS.map(item => {
      const isActive = item.key === active ? 'active' : '';
      if (item.dropdown) {
        return `
          <li class="nav-item dropdown">
            <a class="nav-link gh-nav-link dropdown-toggle ${isActive}" href="#" data-bs-toggle="dropdown">${item.label}</a>
            <ul class="dropdown-menu border-0 shadow-sm">
              ${item.dropdown.map(d => `<li><a class="dropdown-item" href="${d.href}">${d.label}</a></li>`).join('')}
            </ul>
          </li>
        `;
      }
      return `
        <li class="nav-item">
          <a class="nav-link gh-nav-link ${isActive}" href="${item.href}">${item.label}</a>
        </li>
      `;
    }).join('');
  }

  function headerHtml({ active, topbarText }) {
    const tb = topbarText || '🌸 Miễn phí vận chuyển cho đơn từ 500.000₫ &nbsp;|&nbsp; Giao hàng toàn quốc';
    return `
      ${SVG_SPRITE}

      <div class="gh-topbar" id="gh-topbar">${tb}</div>

      <!-- Offcanvas Cart -->
      <div class="offcanvas offcanvas-end" tabindex="-1" id="offcanvasCart" aria-labelledby="offcanvasCartLabel">
        <div class="offcanvas-header justify-content-between border-bottom py-3">
          <h5 class="m-0" style="font-family:system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;text-transform:uppercase;letter-spacing:2px" id="offcanvasCartLabel">Giỏ Hàng</h5>
          <button type="button" class="btn-close" data-bs-dismiss="offcanvas" aria-label="Close"></button>
        </div>
        <div class="offcanvas-body">
          <ul class="list-group list-group-flush mb-3" id="cart-list">
            <li class="list-group-item text-center text-muted py-5">Giỏ hàng trống</li>
          </ul>
          <div class="d-flex justify-content-between py-3 border-top border-bottom mb-3">
            <span class="fw-bold text-uppercase" style="letter-spacing:1px">Tổng cộng</span>
            <strong id="cart-total" style="color:var(--gh-primary)">0 ₫</strong>
          </div>
          <a href="checkout.html" class="gh-btn gh-btn--dark gh-btn--block">Thanh Toán Ngay</a>
          <a href="cart.html" class="gh-btn gh-btn--outline gh-btn--block mt-2">Xem Giỏ Hàng</a>
        </div>
      </div>

      <!-- Navbar -->
      <nav class="navbar navbar-expand-lg bg-white border-bottom py-3 sticky-top">
        <div class="container">
          <a class="navbar-brand gh-brand" href="index.html">GLOWHUB</a>

          <!-- Mobile right cluster -->
          <div class="d-flex align-items-center gap-3 d-lg-none">
            <a href="#offcanvasCart" class="position-relative text-dark" data-bs-toggle="offcanvas" data-bs-target="#offcanvasCart">
              <svg width="22" height="22"><use xlink:href="#gh-i-cart"></use></svg>
              <span id="cart-count-mobile" class="position-absolute top-0 start-100 translate-middle badge rounded-pill" style="background:var(--gh-primary);font-size:10px">0</span>
            </a>
            <button class="navbar-toggler border-0" type="button" data-bs-toggle="offcanvas" data-bs-target="#offcanvasNavbar">
              <span class="navbar-toggler-icon"></span>
            </button>
          </div>

          <!-- Mobile offcanvas nav -->
          <div class="offcanvas offcanvas-end" tabindex="-1" id="offcanvasNavbar">
            <div class="offcanvas-header">
              <h5 class="offcanvas-title gh-brand">GLOWHUB</h5>
              <button type="button" class="btn-close" data-bs-dismiss="offcanvas"></button>
            </div>
            <div class="offcanvas-body">
              <ul class="navbar-nav justify-content-center flex-grow-1 gap-1 gap-md-4">
                ${navHtml(active)}
                <li class="nav-item d-lg-none border-top mt-3 pt-3">
                  <div id="nav-user-mobile"></div>
                </li>
              </ul>
            </div>
          </div>

          <!-- Desktop right cluster -->
          <div class="d-none d-lg-flex align-items-center gap-4">
            <div id="nav-user-area"></div>

            <a href="shop.html" class="nav-link gh-nav-link" title="Tìm kiếm">
              <svg width="20" height="20"><use xlink:href="#gh-i-search"></use></svg>
            </a>

            <a href="#offcanvasCart" class="nav-link gh-nav-link position-relative" data-bs-toggle="offcanvas" data-bs-target="#offcanvasCart" title="Giỏ hàng">
              <svg width="20" height="20"><use xlink:href="#gh-i-cart"></use></svg>
              <span id="cart-count" class="position-absolute top-0 start-100 translate-middle badge rounded-pill" style="background:var(--gh-primary);font-size:10px">0</span>
            </a>
          </div>
        </div>
      </nav>
    `;
  }

  function footerHtml() {
    const year = new Date().getFullYear();
    return `
      <footer class="gh-footer">
        <div class="container">
          <div class="row g-5">
            <div class="col-lg-4">
              <h5 class="gh-brand" style="color:#fff;font-size:1.4rem">GLOWHUB</h5>
              <p style="color:#aaa;font-size:13px;margin-top:16px;line-height:1.7">
                Cửa hàng thời trang & làm đẹp dành cho phái đẹp Việt — luôn cập nhật xu hướng mới nhất, chất lượng vượt trội.
              </p>
            </div>
            <div class="col-6 col-lg-2">
              <h6 class="gh-footer__title">Cửa Hàng</h6>
              <ul class="list-unstyled d-flex flex-column gap-2">
                <li><a href="shop.html">Tất Cả Sản Phẩm</a></li>
                <li><a href="shop.html?section=new">Hàng Mới Về</a></li>
                <li><a href="shop.html?section=best">Bán Chạy Nhất</a></li>
              </ul>
            </div>
            <div class="col-6 col-lg-2">
              <h6 class="gh-footer__title">Tài Khoản</h6>
              <ul class="list-unstyled d-flex flex-column gap-2">
                <li><a href="login.html">Đăng Nhập</a></li>
                <li><a href="register.html">Đăng Ký</a></li>
                <li><a href="checkout.html#orders">Đơn Hàng</a></li>
                <li><a href="cart.html">Giỏ Hàng</a></li>
              </ul>
            </div>
            <div class="col-lg-4">
              <h6 class="gh-footer__title">Liên Hệ</h6>
              <p style="color:#aaa;font-size:13px;line-height:1.7;margin:0">
                Hotline: <a href="tel:1900000000">1900 0000</a><br>
                Email: <a href="mailto:hello@glowhub.vn">hello@glowhub.vn</a><br>
                Địa chỉ: 123 Nguyễn Huệ, Q.1, TP. Hồ Chí Minh
              </p>
            </div>
          </div>
          <div class="gh-footer__copy">© ${year} GLOWHUB. All rights reserved.</div>
        </div>
      </footer>
    `;
  }

  /** Mount layout vào trang. Gọi sau khi DOM ready. */
  function mountLayout(opts = {}) {
    const { active = 'home', topbarText = null } = opts;

    const headerHost = document.getElementById('gh-layout-header');
    const footerHost = document.getElementById('gh-layout-footer');

    if (headerHost) headerHost.innerHTML = headerHtml({ active, topbarText });
    if (footerHost) footerHost.innerHTML = footerHtml();

    // Sau khi mount, refresh các UI phụ thuộc DOM:
    try { if (typeof renderNavUser === 'function') renderNavUser(); } catch (_) {}
    try { if (typeof Cart !== 'undefined' && Cart._updateUI) Cart._updateUI(); } catch (_) {}

    // Nếu đã login → fetch cart server-side
    try {
      if (typeof Auth !== 'undefined' && Auth.isLoggedIn() && Cart && Cart.refresh) {
        Cart.refresh().catch(() => {});
      }
    } catch (_) {}

    // Load topbar / promo từ SiteSettings (nếu có)
    try {
      if (typeof SiteSettings !== 'undefined') {
        SiteSettings.getAll('homepage').then(res => {
          const tbKey = (res || []).find(s => (s.key || s.Key) === 'topbar_text');
          if (tbKey) {
            const v = tbKey.value || tbKey.Value;
            const el = document.getElementById('gh-topbar');
            if (el && v) el.innerHTML = v;
          }
        }).catch(() => {});
      }
    } catch (_) {}
  }

  // Expose globally
  window.mountLayout = mountLayout;
})();
