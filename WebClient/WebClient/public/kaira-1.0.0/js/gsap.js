// @ts-nocheck
/* eslint-disable */
/**
 * GlowHub — gsap.js
 * GSAP + Custom Cursor + Magnetic + Reveal
 * KHÔNG dùng Lenis — native scroll
 */
(function () {
  "use strict";

  document.addEventListener("DOMContentLoaded", function () {
    initCursor();
    initNavbar();
    initReveal();
    initHeroParallax();
    initMagnetic();
    initBlurUp();
  });

  /* ══════════════════════════════════════
     1. CUSTOM CURSOR
  ══════════════════════════════════════ */
  function initCursor() {
    if (!window.matchMedia("(hover:hover) and (pointer:fine)").matches) return;

    var dot = document.getElementById("lux-cursor-dot");
    var ring = document.getElementById("lux-cursor-ring");
    if (!dot) {
      dot = document.createElement("div");
      dot.id = "lux-cursor-dot";
      ring = document.createElement("div");
      ring.id = "lux-cursor-ring";
      document.body.appendChild(dot);
      document.body.appendChild(ring);
    }

    var mx = 0,
      my = 0,
      rx = 0,
      ry = 0;

    document.addEventListener("mousemove", function (e) {
      mx = e.clientX;
      my = e.clientY;
      dot.style.left = mx + "px";
      dot.style.top = my + "px";
    });

    (function animRing() {
      rx += (mx - rx) * 0.1;
      ry += (my - ry) * 0.1;
      ring.style.left = rx + "px";
      ring.style.top = ry + "px";
      requestAnimationFrame(animRing);
    })();

    var sel =
      "a,button,.gh-split-panel,.gh-asym-card,.gh-strip-card,.gh-lifestyle-item,.product-card,input,select,textarea,[onclick]";
    document.addEventListener("mouseover", function (e) {
      if (e.target.closest(sel)) document.body.classList.add("cursor-hover");
    });
    document.addEventListener("mouseout", function (e) {
      if (e.target.closest(sel)) document.body.classList.remove("cursor-hover");
    });
  }

  /* ══════════════════════════════════════
     2. NAVBAR — shadow khi scroll
  ══════════════════════════════════════ */
  function initNavbar() {
    var nav = document.querySelector(".gh-nav");
    if (!nav) return;
    window.addEventListener(
      "scroll",
      function () {
        nav.classList.toggle("scrolled", window.scrollY > 40);
      },
      { passive: true },
    );
  }

  /* ══════════════════════════════════════
     3. GSAP SCROLL REVEAL
  ══════════════════════════════════════ */
  function initReveal() {
    if (typeof gsap === "undefined") {
      fallbackReveal();
      return;
    }
    if (typeof ScrollTrigger !== "undefined")
      gsap.registerPlugin(ScrollTrigger);

    // Hero — animate ngay khi load
    var heroContent = document.querySelector(".gh-hero-content");
    if (heroContent) {
      gsap.fromTo(
        heroContent.children,
        { y: 28, opacity: 0 },
        {
          y: 0,
          opacity: 1,
          duration: 0.9,
          ease: "power3.out",
          stagger: 0.12,
          delay: 0.2,
        },
      );
    }

    // Scroll reveal
    document.querySelectorAll(".g-reveal").forEach(function (el) {
      reveal(el, 0, 40);
    });
    document.querySelectorAll(".g-reveal-l").forEach(function (el) {
      reveal(el, -40, 0);
    });
    document.querySelectorAll(".g-reveal-r").forEach(function (el) {
      reveal(el, 40, 0);
    });
    document.querySelectorAll(".gh-sec-hd").forEach(function (el) {
      if (!el.classList.contains("g-reveal")) reveal(el, 0, 30);
    });

    staggerReveal(".gh-asym-card", 0.09);
    staggerReveal(".gh-strip-card", 0.08);
    staggerReveal(".gh-lifestyle-item", 0.1);
    staggerReveal(".product-card", 0.08);
  }

  function reveal(el, x, y) {
    if (typeof ScrollTrigger === "undefined") {
      gsap.fromTo(
        el,
        { x: x, y: y, opacity: 0 },
        { x: 0, y: 0, opacity: 1, duration: 0.85, ease: "power3.out" },
      );
      return;
    }
    gsap.fromTo(
      el,
      { x: x, y: y, opacity: 0 },
      {
        x: 0,
        y: 0,
        opacity: 1,
        duration: 0.85,
        ease: "power3.out",
        scrollTrigger: { trigger: el, start: "top 88%" },
      },
    );
  }

  function staggerReveal(sel, time) {
    var els = document.querySelectorAll(sel);
    if (!els.length) return;
    if (typeof ScrollTrigger === "undefined") {
      gsap.fromTo(
        els,
        { y: 32, opacity: 0 },
        { y: 0, opacity: 1, duration: 0.7, ease: "power3.out", stagger: time },
      );
      return;
    }
    gsap.fromTo(
      els,
      { y: 32, opacity: 0 },
      {
        y: 0,
        opacity: 1,
        duration: 0.75,
        ease: "power3.out",
        stagger: time,
        scrollTrigger: { trigger: els[0], start: "top 86%" },
      },
    );
  }

  function fallbackReveal() {
    var io = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (e) {
          if (e.isIntersecting) {
            e.target.style.transition = "opacity .8s ease, transform .8s ease";
            e.target.style.opacity = "1";
            e.target.style.transform = "none";
            io.unobserve(e.target);
          }
        });
      },
      { threshold: 0.1 },
    );
    document
      .querySelectorAll(".g-reveal,.g-reveal-l,.g-reveal-r")
      .forEach(function (el) {
        el.style.opacity = "0";
        el.style.transform = "translateY(30px)";
        io.observe(el);
      });
  }

  /* ══════════════════════════════════════
     4. HERO PARALLAX
  ══════════════════════════════════════ */
  function initHeroParallax() {
    if (typeof gsap === "undefined" || typeof ScrollTrigger === "undefined")
      return;
    var img = document.querySelector(".gh-hero-media img");
    if (!img) return;
    gsap.to(img, {
      yPercent: 18,
      ease: "none",
      scrollTrigger: {
        trigger: img.closest("section"),
        start: "top top",
        end: "bottom top",
        scrub: 1,
      },
    });
  }

  /* ══════════════════════════════════════
     5. MAGNETIC BUTTONS
  ══════════════════════════════════════ */
  function initMagnetic() {
    if (!window.matchMedia("(hover:hover) and (pointer:fine)").matches) return;
    document
      .querySelectorAll(
        ".gh-btn-primary,.gh-btn-outline,.gh-hero-cta,.gh-view-all",
      )
      .forEach(function (btn) {
        btn.addEventListener("mousemove", function (e) {
          var r = btn.getBoundingClientRect();
          var dx = (e.clientX - (r.left + r.width / 2)) * 0.22;
          var dy = (e.clientY - (r.top + r.height / 2)) * 0.22;
          btn.style.transform = `translate(${dx}px,${dy}px)`;
          btn.style.transition = "transform .1s linear";
        });
        btn.addEventListener("mouseleave", function () {
          btn.style.transform = "";
          btn.style.transition = "transform .5s cubic-bezier(0.25,1,0.5,1)";
        });
      });
  }

  /* ══════════════════════════════════════
     6. IMAGE BLUR-UP
  ══════════════════════════════════════ */
  function initBlurUp() {
    document.querySelectorAll('img[loading="lazy"]').forEach(function (img) {
      if (img.complete) return;
      img.style.filter = "blur(8px)";
      img.style.transition = "filter .5s ease";
      img.addEventListener(
        "load",
        function () {
          img.style.filter = "";
        },
        { once: true },
      );
    });
  }
})();
