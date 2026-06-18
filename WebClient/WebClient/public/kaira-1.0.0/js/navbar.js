// @ts-nocheck
/* eslint-disable */
/**
 * GlowHub — js/navbar.js
 * Navbar thống nhất: categories, user, search, drawer, scroll-top
 * 100% data động từ API. Không hardcode.
 */
(function () {
  'use strict';

  // Override khi deploy: <script>window.GH_CONFIG = {API_URL:'...',AUTH_URL:'...'}</script>
  var API_URL  = (window.GH_CONFIG && window.GH_CONFIG.API_URL)  || 'http://localhost:5001';
  var AUTH_URL = (window.GH_CONFIG && window.GH_CONFIG.AUTH_URL) || 'http://localhost:5002';

  /* ─────────────────────────────────
     Helpers
  ───────────────────────────────── */
  function fmt(n) {
    return Number(n || 0).toLocaleString('vi-VN') + '₫';
  }
  function getInitial(name) {
    return ((name || '?').trim()[0]).toUpperCase();
  }
  function getRoleLabel(role) {
    var map = {
      Admin: 'Quản Trị Viên',
      Seller: 'Người Bán',
      User: 'Thành Viên',
      Customer: 'Thành Viên'
    };
    return map[role] || 'Thành Viên';
  }
  function getToken() {
    return localStorage.getItem('token') || '';
  }
  function getUser() {
    try {
      var s = localStorage.getItem('user') || localStorage.getItem('gh_user');
      return s ? JSON.parse(s) : null;
    } catch (e) { return null; }
  }
  function $(id) { return document.getElementById(id); }
  function $$(sel) { return document.querySelectorAll(sel); }

  /* ─────────────────────────────────
     Cart Badge
  ───────────────────────────────── */
  function updateCartBadge() {
    try {
      var cart = JSON.parse(localStorage.getItem('gh_cart') || '[]');
      var count = cart.reduce(function(s, i) { return s + (i.qty || i.quantity || 1); }, 0);
      $$('.gh-cart-badge').forEach(function(el) {
        if (count <= 0) { el.style.display = 'none'; return; }
        var txt = count > 99 ? '99+' : String(count);
        if (el.textContent !== txt) {
          el.textContent = txt;
          el.classList.remove('badge-bump');
          void el.offsetWidth;
          el.classList.add('badge-bump');
        }
        el.style.display = 'flex';
      });
    } catch (e) { }
  }

  function updateWishlistBadge() {
    try {
      var wl = JSON.parse(localStorage.getItem('gh_wishlist') || localStorage.getItem('glowhub_wishlist') || '[]');
      var count = wl.length;
      $$('.gh-wish-dot').forEach(function(el) {
        el.style.display = count > 0 ? 'block' : 'none';
      });
      $$('.gh-wishlist-badge').forEach(function(el) {
        if (count <= 0) { el.style.display = 'none'; return; }
        var txt = count > 99 ? '99+' : String(count);
        if (el.textContent !== txt) {
          el.textContent = txt;
          el.classList.remove('badge-bump');
          void el.offsetWidth;
          el.classList.add('badge-bump');
        }
        el.style.display = 'flex';
      });
    } catch (e) { }
  }

  /* ─────────────────────────────────
     Categories (từ API)
  ───────────────────────────────── */
  function loadCategories() {
    var drops = $$('.gh-cat-dropdown');
    var drawerCats = $('ghDrawerCats');
    if (!drops.length && !drawerCats) return;

    fetch(API_URL + '/api/categories')
      .then(function(r) { return r.json(); })
      .then(function(cats) {
        if (!Array.isArray(cats) || !cats.length) return;
        var html = cats.map(function(c) {
          return '<li><a href="shop.html?category=' + (c.id || c.Id) + '">' + (c.name || c.Name) + '</a></li>';
        }).join('');
        drops.forEach(function(d) { d.innerHTML = html; });
        if (drawerCats) {
          drawerCats.innerHTML = cats.map(function(c) {
            return '<a href="shop.html?category=' + (c.id || c.Id) + '">' + (c.name || c.Name) + '</a>';
          }).join('');
        }
      })
      .catch(function() {
        drops.forEach(function(d) {
          d.innerHTML = '<li><a href="shop.html">Tất cả sản phẩm</a></li>';
        });
      });
  }

  /* ─────────────────────────────────
     Search
  ───────────────────────────────── */
  var _searchTimer = null;

  function initSearch() {
    var btn = $('ghSearchBtn');
    var overlay = $('ghSearchOverlay');
    var closeBtn = $('ghSearchClose');
    var input = $('ghSearchInput');
    var body = $('ghSearchBody');
    if (!btn || !overlay) return;

    btn.addEventListener('click', function() {
      overlay.style.display = 'block';
      if (input) input.focus();
      renderHistory(body);
    });

    if (closeBtn) {
      closeBtn.addEventListener('click', function() {
        overlay.style.display = 'none';
        if (input) input.value = '';
        if (body) body.innerHTML = '';
      });
    }

    document.addEventListener('keydown', function(e) {
      if (e.key === 'Escape' && overlay.style.display !== 'none') {
        overlay.style.display = 'none';
        if (input) input.value = '';
        if (body) body.innerHTML = '';
      }
    });

    if (input) {
      input.addEventListener('input', function(e) {
        clearTimeout(_searchTimer);
        var q = e.target.value.trim();
        if (!q) { renderHistory(body); return; }
        _searchTimer = setTimeout(function() { doSearch(q, body); }, 300);
      });

      input.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
          var q = e.target.value.trim();
          if (!q) return;
          saveHistory(q);
          location.href = 'shop.html?search=' + encodeURIComponent(q);
        }
      });
    }
  }

  function getHistory() {
    try { return JSON.parse(localStorage.getItem('gh_search') || '[]'); }
    catch (e) { return []; }
  }

  function saveHistory(q) {
    try {
      var h = getHistory().filter(function(x) { return x !== q; });
      h.unshift(q);
      localStorage.setItem('gh_search', JSON.stringify(h.slice(0, 5)));
    } catch (e) { }
  }

  function renderHistory(body) {
    if (!body) return;
    var h = getHistory();
    if (!h.length) { body.innerHTML = ''; return; }
    body.innerHTML = '<p class="gh-search-label">Tìm kiếm gần đây</p>' +
      h.map(function(x) {
        return '<div class="gh-search-hist-item" onclick="location.href=\'shop.html?search=' +
          encodeURIComponent(x) + '\'"><span>🕐</span><span>' + x + '</span></div>';
      }).join('');
  }

  function doSearch(q, body) {
    if (!body) return;
    body.innerHTML = '<p class="gh-search-label">Đang tìm kiếm...</p>';
    fetch(API_URL + '/api/products?keyword=' + encodeURIComponent(q) + '&pageSize=5')
      .then(function(r) { return r.json(); })
      .then(function(d) {
        var items = d.items || d.data || (Array.isArray(d) ? d : []);
        if (!items.length) {
          body.innerHTML = '<p class="gh-search-label">Không tìm thấy kết quả cho "' + q + '"</p>';
          return;
        }
        var html = '<p class="gh-search-label">Gợi ý sản phẩm</p>' +
          items.map(function(p) {
            var id    = p.id || p.Id;
            var name  = p.name || p.Name || '';
            var price = p.discountPrice || p.DiscountPrice || p.price || p.Price || 0;
            var img   = p.imageUrl || p.ImageUrl || '';
            return '<a href="product.html?id=' + id + '" class="gh-search-result-item">' +
              '<img src="' + img + '" loading="lazy" onerror="this.style.display=\'none\'">' +
              '<span class="gh-search-result-name">' + name + '</span>' +
              '<span class="gh-search-result-price">' + fmt(price) + '</span>' +
              '</a>';
          }).join('') +
          '<a href="shop.html?search=' + encodeURIComponent(q) + '" class="gh-search-view-all">Xem tất cả kết quả →</a>';
        body.innerHTML = html;
      })
      .catch(function() {
        body.innerHTML = '<p class="gh-search-label">Không thể tải kết quả</p>';
      });
  }

  /* ─────────────────────────────────
     Tracking Nav (auth-gated, injected)
  ───────────────────────────────── */
  function injectTrackingNav() {
    // Desktop gh-menu — insert <li> after vouchers <li>
    var menu = document.querySelector('.gh-menu');
    if (menu && !menu.querySelector('.gh-track-li')) {
      var lis = menu.querySelectorAll('li');
      var vLi = null;
      for (var i = 0; i < lis.length; i++) {
        if (lis[i].querySelector('a[href="vouchers.html"]')) { vLi = lis[i]; break; }
      }
      if (vLi) {
        var li = document.createElement('li');
        li.className = 'gh-track-li';
        li.style.display = 'none';
        var la = document.createElement('a');
        la.href = 'order-tracking.html';
        la.setAttribute('data-page', 'order-tracking');
        la.textContent = 'Theo Dõi Đơn';
        li.appendChild(la);
        vLi.parentNode.insertBefore(li, vLi.nextSibling);
      }
    }
    // Mobile gh-drawer-nav — insert <a> after vouchers <a>
    var drawer = document.querySelector('.gh-drawer-nav');
    if (drawer && !drawer.querySelector('.gh-track-li')) {
      var vA = drawer.querySelector('a[href="vouchers.html"]');
      if (vA) {
        var da = document.createElement('a');
        da.className = 'gh-track-li';
        da.href = 'order-tracking.html';
        da.setAttribute('data-page', 'order-tracking');
        da.style.display = 'none';
        da.textContent = 'Theo Dõi Đơn';
        vA.parentNode.insertBefore(da, vA.nextSibling);
      }
    }
  }

  /* ─────────────────────────────────
     User Menu (100% từ localStorage)
  ───────────────────────────────── */
  function initUser() {
    injectTrackingNav();
    var token   = getToken();
    var user    = getUser();
    var loginBtn  = $('ghLoginBtn');
    var userWrap  = $('ghUserWrap');

    if (!token || !user) {
      if (loginBtn) loginBtn.style.display = 'flex';
      if (userWrap) userWrap.style.display = 'none';
      var dLogin = $('ghDrawerLogin');
      if (dLogin) dLogin.style.display = 'block';
      var dProfile = $('ghDrawerProfile');
      if (dProfile) dProfile.style.display = 'none';
      var dLogout = $('ghDrawerLogout');
      if (dLogout) dLogout.style.display = 'none';
      var dUser = $('ghDrawerUser');
      if (dUser) dUser.style.display = 'none';
      return;
    }

    if (loginBtn) loginBtn.style.display = 'none';
    if (userWrap) userWrap.style.display = 'flex';
    $$('.gh-track-li').forEach(function(el) { el.style.display = ''; });

    var name     = user.name || user.Name || user.username || user.UserName || user.FullName || user.fullName || '';
    var role     = user.role || user.Role || (user.roles && user.roles[0]) || (user.userType === 1 ? 'Admin' : 'User');
    var initial  = getInitial(name);
    var shortName = name.split(' ').pop() || name;

    $$('.gh-avatar').forEach(function(el) { el.textContent = initial; });
    $$('.gh-avatar-lg').forEach(function(el) { el.textContent = initial; });
    $$('.gh-username').forEach(function(el) { el.textContent = shortName; });
    $$('.gh-fullname').forEach(function(el) { el.textContent = name; });
    $$('.gh-role-label').forEach(function(el) { el.textContent = getRoleLabel(role); });

    if (role === 'Seller') {
      $$('.gh-seller-only').forEach(function(el) { el.style.display = 'flex'; });
    }
    if (role === 'Admin') {
      $$('.gh-admin-only').forEach(function(el) { el.style.display = 'flex'; });
    }

    loadPendingBadge(token);
    loadWalletBal(token, role);

    var dUser2 = $('ghDrawerUser');
    if (dUser2) dUser2.style.display = 'flex';
    var drawerName = $('ghDrawerUserName');
    if (drawerName) drawerName.textContent = name;
    $$('#ghDrawerUser .role').forEach(function(el) { el.textContent = getRoleLabel(role); });
    var dLogin2 = $('ghDrawerLogin');
    if (dLogin2) dLogin2.style.display = 'none';
    var dProfile2 = $('ghDrawerProfile');
    if (dProfile2) dProfile2.style.display = 'block';
    var dLogout2 = $('ghDrawerLogout');
    if (dLogout2) dLogout2.style.display = 'block';

    var trigger  = $('ghUserTrigger');
    var dropdown = $('ghUserDropdown');
    if (trigger && dropdown) {
      trigger.addEventListener('click', function(e) {
        e.stopPropagation();
        trigger.classList.toggle('open');
        dropdown.classList.toggle('open');
      });
      document.addEventListener('click', function(e) {
        if (!e.target.closest('.gh-user-wrap')) {
          trigger.classList.remove('open');
          dropdown.classList.remove('open');
        }
      });
    }

    $$('#ghLogoutBtn, #ghDrawerLogout').forEach(function(btn) {
      if (btn) btn.addEventListener('click', function() {
        localStorage.clear();
        location.href = 'index.html';
      });
    });
  }

  function loadPendingBadge(token) {
    fetch(API_URL + '/api/orders/my?status=WAITING_PAYMENT&limit=1', {
      headers: { 'Authorization': 'Bearer ' + token }
    })
    .then(function(r) { return r.json(); })
    .then(function(d) {
      var n = d.totalCount || d.total || (Array.isArray(d) ? d.length : 0);
      if (n > 0) {
        $$('.gh-orders-badge').forEach(function(el) {
          el.textContent = n;
          el.style.display = 'inline-flex';
        });
      }
    })
    .catch(function() { });
  }

  function loadWalletBal(token, role) {
    var url = role === 'Seller'
      ? API_URL + '/api/seller/wallet'
      : API_URL + '/api/customer/wallet';
    fetch(url, { headers: { 'Authorization': 'Bearer ' + token } })
      .then(function(r) { return r.json(); })
      .then(function(d) {
        var bal = d.balance || d.Balance || 0;
        if (bal > 0) {
          $$('.gh-wallet-bal').forEach(function(el) { el.textContent = fmt(bal); });
        }
      })
      .catch(function() { });
  }

  /* ─────────────────────────────────
     Scroll Effect
  ───────────────────────────────── */
  function initScrollEffect() {
    var nav = document.querySelector('.gh-nav');
    if (!nav) return;
    window.addEventListener('scroll', function() {
      nav.classList.toggle('scrolled', window.scrollY > 20);
    }, { passive: true });
  }

  /* ─────────────────────────────────
     Active Page Marking
  ───────────────────────────────── */
  function markActivePage() {
    var page = location.pathname.split('/').pop().replace('.html', '') || 'index';
    $$('.gh-menu a[data-page]').forEach(function(a) {
      if (a.dataset.page === page) a.classList.add('active');
    });
  }

  /* ─────────────────────────────────
     Mobile Drawer
  ───────────────────────────────── */
  function initDrawer() {
    var ham     = $('ghHamburger');
    var drawer  = $('ghDrawer');
    var overlay = $('ghOverlay');
    var closeBtn = $('ghDrawerClose');

    function openDrawer() {
      if (ham) ham.classList.add('open');
      if (drawer) drawer.classList.add('open');
      if (overlay) overlay.classList.add('open');
      document.body.style.overflow = 'hidden';
    }
    function closeDrawer() {
      if (ham) ham.classList.remove('open');
      if (drawer) drawer.classList.remove('open');
      if (overlay) overlay.classList.remove('open');
      document.body.style.overflow = '';
    }

    if (ham) ham.addEventListener('click', openDrawer);
    if (closeBtn) closeBtn.addEventListener('click', closeDrawer);
    if (overlay) overlay.addEventListener('click', closeDrawer);
    document.addEventListener('keydown', function(e) {
      if (e.key === 'Escape' && drawer && drawer.classList.contains('open')) closeDrawer();
    });
  }

  /* ─────────────────────────────────
     Scroll To Top
  ───────────────────────────────── */
  function initScrollTop() {
    var btn = $('ghScrollTop');
    if (!btn) {
      btn = document.createElement('button');
      btn.id = 'ghScrollTop';
      btn.className = 'gh-scroll-top';
      btn.setAttribute('aria-label', 'Lên đầu trang');
      btn.textContent = '↑';
      document.body.appendChild(btn);
    }
    window.addEventListener('scroll', function() {
      btn.classList.toggle('visible', window.scrollY > 300);
    }, { passive: true });
    btn.addEventListener('click', function() {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  /* ─────────────────────────────────
     Init
  ───────────────────────────────── */
  document.addEventListener('DOMContentLoaded', function() {
    initScrollEffect();
    initUser();
    updateCartBadge();
    updateWishlistBadge();
    loadCategories();
    initSearch();
    markActivePage();
    initDrawer();
    initScrollTop();

    window.addEventListener('cart-updated', updateCartBadge);
    window.addEventListener('wishlist-updated', updateWishlistBadge);
    window.addEventListener('storage', function(e) {
      if (e.key === 'gh_cart') updateCartBadge();
      if (e.key === 'gh_wishlist' || e.key === 'glowhub_wishlist') updateWishlistBadge();
    });
  });

})();
