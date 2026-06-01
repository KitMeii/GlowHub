(function () {
  'use strict';

  var CART_MAX  = 99;
  var CART_WARN = 90;
  var WL_MAX    = 99;
  var WL_WARN   = 90;

  /* ── Inject CSS once ──────────────────────────────────────── */
  (function injectCSS() {
    if (document.getElementById('glm-style')) return;
    var s = document.createElement('style');
    s.id = 'glm-style';
    s.textContent = [
      '#ghLimitModal{position:fixed;inset:0;z-index:9999;display:flex;align-items:center;justify-content:center}',
      '.glm-overlay{position:absolute;inset:0;background:rgba(0,0,0,.5)}',
      '.glm-box{position:relative;background:#fff;padding:40px 36px;max-width:400px;width:90%;text-align:center;transform:translateY(24px);opacity:0;transition:all .3s ease}',
      '.glm-box.show{transform:translateY(0);opacity:1}',
      '.glm-icon{font-size:48px;margin-bottom:16px}',
      '.glm-title{font-family:"Cormorant Garamond",Georgia,serif;font-size:1.3rem;font-weight:400;color:#111;margin-bottom:12px}',
      '.glm-desc{font-size:13px;color:rgba(0,0,0,.6);font-family:"Jost",sans-serif;line-height:1.7;margin-bottom:10px}',
      '.glm-hint{font-size:12px;color:rgba(0,0,0,.45);font-family:"Jost",sans-serif;line-height:1.6;margin-bottom:28px;padding:12px 16px;background:#f8f5f0;border-left:3px solid #f759ab;text-align:left}',
      '.glm-actions{display:flex;gap:10px;justify-content:center;flex-wrap:wrap}',
      '.glm-btn-primary{background:#111;color:#fff;padding:12px 24px;font-size:9px;letter-spacing:2px;text-transform:uppercase;font-family:"Jost",sans-serif;text-decoration:none;border:none;cursor:pointer;transition:background .2s;display:inline-flex;align-items:center}',
      '.glm-btn-primary:hover{background:#f759ab;color:#fff}',
      '.glm-btn-secondary{background:none;color:#111;padding:12px 20px;font-size:9px;letter-spacing:2px;text-transform:uppercase;font-family:"Jost",sans-serif;border:1px solid rgba(0,0,0,.15);cursor:pointer;transition:all .2s}',
      '.glm-btn-secondary:hover{border-color:#111;background:#111;color:#fff}'
    ].join('');
    document.head.appendChild(s);
  })();

  /* ── Limit Modal ────────────────────────────────────────── */
  function showLimitModal(opts) {
    var existing = document.getElementById('ghLimitModal');
    if (existing) existing.remove();

    var el = document.createElement('div');
    el.id = 'ghLimitModal';
    el.innerHTML =
      '<div class="glm-overlay"></div>' +
      '<div class="glm-box">' +
        '<div class="glm-icon">' + opts.icon + '</div>' +
        '<h3 class="glm-title">' + opts.title + '</h3>' +
        '<p class="glm-desc">' + opts.desc + '</p>' +
        '<p class="glm-hint">' + opts.hint + '</p>' +
        '<div class="glm-actions">' +
          '<a href="' + opts.btnHref + '" class="glm-btn-primary">' + opts.btnText + '</a>' +
          '<button class="glm-btn-secondary" id="glmClose">Đóng</button>' +
        '</div>' +
      '</div>';
    document.body.appendChild(el);

    var close = function () { el.remove(); };
    var closeBtn = el.querySelector('#glmClose');
    if (closeBtn) closeBtn.addEventListener('click', close);
    el.querySelector('.glm-overlay').addEventListener('click', close);
    document.addEventListener('keydown', function handler(e) {
      if (e.key === 'Escape') { close(); document.removeEventListener('keydown', handler); }
    });
    requestAnimationFrame(function () {
      var box = el.querySelector('.glm-box');
      if (box) box.classList.add('show');
    });
  }

  /* ── Warning Toast ──────────────────────────────────────── */
  function showWarn(msg) {
    if (typeof showGlobalToast === 'function') showGlobalToast(msg, 'warn');
    else console.warn(msg);
  }

  /* ── CART ───────────────────────────────────────────────── */
  window.CartLimit = {
    KEY: 'gh_cart',

    _read: function () {
      try { return JSON.parse(localStorage.getItem(this.KEY) || '[]'); } catch (e) { return []; }
    },
    _write: function (arr) {
      localStorage.setItem(this.KEY, JSON.stringify(arr));
      window.dispatchEvent(new Event('cart-updated'));
    },

    add: function (item) {
      try {
        var cart = this._read();
        var idx = cart.findIndex(function (x) { return String(x.id) === String(item.id); });
        if (idx >= 0) {
          cart[idx].qty = (cart[idx].qty || 1) + (item.qty || 1);
          this._write(cart);
          return { success: true, action: 'updated' };
        }
        if (cart.length >= CART_MAX) {
          showLimitModal({
            icon: '🛒',
            title: 'Giỏ hàng đã đầy',
            desc: 'Bạn đã có ' + CART_MAX + ' loại sản phẩm trong giỏ — đây là giới hạn tối đa của GlowHub.',
            hint: 'Vui lòng thanh toán hoặc xóa bớt sản phẩm trước khi thêm mới.',
            btnText: 'Xem Giỏ Hàng',
            btnHref: 'cart.html'
          });
          return { success: false, reason: 'cart_full' };
        }
        if (cart.length >= CART_WARN) {
          showWarn('⚠️ Giỏ hàng gần đầy — còn ' + (CART_MAX - cart.length) + ' chỗ trống');
        }
        cart.push(Object.assign({}, item, { qty: item.qty || 1 }));
        this._write(cart);
        return { success: true, action: 'added' };
      } catch (e) {
        console.error(e);
        return { success: false, reason: 'error' };
      }
    },

    getItemCount: function () {
      return this._read().length;
    },

    getTotalQty: function () {
      return this._read().reduce(function (s, i) { return s + (i.qty || 1); }, 0);
    }
  };

  /* ── WISHLIST ────────────────────────────────────────────── */
  window.WishlistLimit = {
    KEY: 'gh_wishlist',

    _read: function () {
      try { return JSON.parse(localStorage.getItem(this.KEY) || '[]'); } catch (e) { return []; }
    },
    _write: function (arr) {
      localStorage.setItem(this.KEY, JSON.stringify(arr));
      window.dispatchEvent(new Event('wishlist-updated'));
    },

    toggle: function (item) {
      try {
        var wl = this._read();
        var idx = wl.findIndex(function (x) { return String(x.id) === String(item.id); });
        if (idx >= 0) {
          wl.splice(idx, 1);
          this._write(wl);
          return { success: true, action: 'removed' };
        }
        if (wl.length >= WL_MAX) {
          showLimitModal({
            icon: '❤️',
            title: 'Danh sách yêu thích đã đầy',
            desc: 'Bạn đã lưu ' + WL_MAX + ' sản phẩm yêu thích — đây là giới hạn tối đa.',
            hint: 'Vui lòng xóa bớt sản phẩm trong danh sách yêu thích để thêm mới.',
            btnText: 'Xem Yêu Thích',
            btnHref: 'profile.html#wishlist'
          });
          return { success: false, reason: 'wishlist_full' };
        }
        if (wl.length >= WL_WARN) {
          showWarn('⚠️ Yêu thích gần đầy — còn ' + (WL_MAX - wl.length) + ' chỗ');
        }
        wl.push(item);
        this._write(wl);
        return { success: true, action: 'added' };
      } catch (e) {
        console.error(e);
        return { success: false, reason: 'error' };
      }
    },

    isInList: function (id) {
      try {
        return this._read().some(function (x) { return String(x.id) === String(id); });
      } catch (e) { return false; }
    },

    getCount: function () {
      return this._read().length;
    }
  };

})();
