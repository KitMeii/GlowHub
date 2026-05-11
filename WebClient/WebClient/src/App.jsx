import React, { useState, useEffect } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  LayoutDashboard,
  Package,
  ListTree,
  ShoppingCart,
  Users,
  ShieldCheck,
  LogOut,
  Search,
  Bell,
  ChevronRight,
} from "lucide-react";

// ==========================================
// CONFIG
// ==========================================
const AUTH_API = "http://localhost:5002/api";
const MAIN_API = "http://localhost:5001/api";

// ==========================================
// GLOBAL STYLES (inject once)
// ==========================================
const globalCSS = `
  @import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:…ces:ital,opsz,wght@0,9..144,300;1,9..144,300&display=swap');
  *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: 'Plus Jakarta Sans', sans-serif; background: #fffdf9; }
  :root {
    --pink-50: #fff0f6; --pink-100: #ffd6e7; --pink-200: #ffadd2;
    --pink-300: #ff85bf; --pink-400: #f759ab; --pink-500: #eb2f96; --pink-600: #c41d7f;
    --rose-100: #ffe4e6; --cream: #fffdf9;
    --text-dark: #1a0a12; --text-mid: #6b3a52; --text-light: #b07a97;
    --border: #f0c8dc;
  }
  @keyframes fadeInUp { from { opacity:0; transform:translateY(8px); } to { opacity:1; transform:translateY(0); } }
  @keyframes shine { 0%,100%{opacity:.7} 50%{opacity:1} }
  @keyframes float { 0%,100%{transform:translateY(0)} 50%{transform:translateY(-5px)} }
  .row-enter { animation: fadeInUp .3s ease both; }
`;

function InjectGlobalStyles() {
  useEffect(() => {
    const existing = document.getElementById("blossom-styles");
    if (existing) return;
    const tag = document.createElement("style");
    tag.id = "blossom-styles";
    tag.textContent = globalCSS;
    document.head.appendChild(tag);
  }, []);
  return null;
}

// ==========================================
// LOGIN
// ==========================================
function Login({ onLogin }) {
  const [form, setForm] = useState({ username: "", password: "" });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleLogin = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${AUTH_API}/Auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(form),
      });
      const data = await res.json();
      const token = data.token || data.Token || data.accessToken;
      if (token) {
        localStorage.setItem("token", token);
        onLogin(token);
      } else {
        setError("Sai tên đăng nhập hoặc mật khẩu");
      }
    } catch {
      setError("Không thể kết nối đến máy chủ");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={s.loginPage}>
      {/* Background orbs */}
      <div
        style={{
          ...s.orb,
          width: 400,
          height: 400,
          top: -120,
          right: -120,
          background:
            "radial-gradient(circle, rgba(247,89,171,0.18), transparent)",
        }}
      />
      <div
        style={{
          ...s.orb,
          width: 280,
          height: 280,
          bottom: -80,
          left: -80,
          background:
            "radial-gradient(circle, rgba(255,133,191,0.2), transparent)",
        }}
      />
      <div
        style={{
          ...s.orb,
          width: 180,
          height: 180,
          top: "40%",
          left: "15%",
          background:
            "radial-gradient(circle, rgba(255,200,220,0.25), transparent)",
        }}
      />

      <motion.div
        initial={{ opacity: 0, y: 30, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5, ease: "easeOut" }}
        style={s.loginCard}
      >
        <div
          style={{ ...s.loginIcon, animation: "float 4s ease-in-out infinite" }}
        >
          🌸
        </div>
        <h1 style={s.loginTitle}>Chào mừng trở lại</h1>
        <p style={s.loginSub}>Đăng nhập vào hệ thống quản trị TrMeii</p>

        <input
          style={s.loginInput}
          placeholder="Tên đăng nhập"
          value={form.username}
          onChange={(e) => setForm({ ...form, username: e.target.value })}
          onKeyDown={(e) => e.key === "Enter" && handleLogin()}
        />
        <input
          type="password"
          style={s.loginInput}
          placeholder="Mật khẩu"
          value={form.password}
          onChange={(e) => setForm({ ...form, password: e.target.value })}
          onKeyDown={(e) => e.key === "Enter" && handleLogin()}
        />

        {error && (
          <motion.p
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            style={{
              color: "#c41d7f",
              fontSize: 13,
              marginBottom: 12,
              fontWeight: 600,
            }}
          >
            ⚠ {error}
          </motion.p>
        )}

        <motion.button
          whileHover={{
            scale: 1.02,
            boxShadow: "0 10px 32px rgba(247,89,171,0.55)",
          }}
          whileTap={{ scale: 0.98 }}
          onClick={handleLogin}
          disabled={loading}
          style={s.loginBtn}
        >
          {loading ? "Đang đăng nhập..." : "Đăng nhập ngay ✨"}
        </motion.button>
      </motion.div>
    </div>
  );
}

// ==========================================
// STAT CARD
// ==========================================
const statItems = [
  {
    icon: "📦",
    label: "Sản phẩm",
    value: "1,248",
    change: "↑ 12% tháng này",
    color: "rgba(247,89,171,0.22)",
  },
  {
    icon: "✅",
    label: "Hoạt động",
    value: "98.2%",
    change: "↑ 0.5% hôm nay",
    color: "rgba(82,196,26,0.18)",
  },
  {
    icon: "🛒",
    label: "Đơn mới",
    value: "342",
    change: "↑ 8% tuần này",
    color: "rgba(250,173,20,0.18)",
  },
  {
    icon: "👥",
    label: "Người dùng",
    value: "5,120",
    change: "↑ 23 hôm nay",
    color: "rgba(24,144,255,0.18)",
  },
];

function StatCards() {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "repeat(4,1fr)",
        gap: 12,
        marginBottom: 24,
      }}
    >
      {statItems.map((item, i) => (
        <motion.div
          key={i}
          whileHover={{ y: -3, boxShadow: "0 10px 28px rgba(247,89,171,0.14)" }}
          style={{ ...s.statCard, "--orb-color": item.color }}
        >
          <div
            style={{
              position: "absolute",
              top: -24,
              right: -24,
              width: 80,
              height: 80,
              borderRadius: "50%",
              background: `radial-gradient(circle, ${item.color}, transparent)`,
            }}
          />
          <div style={{ fontSize: 20, marginBottom: 8 }}>{item.icon}</div>
          <div
            style={{
              fontSize: 26,
              fontWeight: 800,
              color: "var(--text-dark)",
              lineHeight: 1,
            }}
          >
            {item.value}
          </div>
          <div
            style={{
              fontSize: 10,
              color: "var(--text-light)",
              marginTop: 4,
              fontWeight: 700,
              letterSpacing: 1,
              textTransform: "uppercase",
            }}
          >
            {item.label}
          </div>
          <div
            style={{
              fontSize: 11,
              fontWeight: 700,
              color: "#52c41a",
              marginTop: 6,
            }}
          >
            {item.change}
          </div>
        </motion.div>
      ))}
    </div>
  );
}

// ==========================================
// MAIN APP
// ==========================================
const menu = [
  {
    id: "products",
    name: "Kho hàng",
    icon: <Package size={16} />,
    api: `${MAIN_API}/Products`,
    section: "main",
  },
  {
    id: "categories",
    name: "Danh mục",
    icon: <ListTree size={16} />,
    api: `${MAIN_API}/Categories`,
    section: "main",
  },
  {
    id: "orders",
    name: "Đơn hàng",
    icon: <ShoppingCart size={16} />,
    api: `${MAIN_API}/Orders`,
    section: "main",
  },
  {
    id: "users",
    name: "Người dùng",
    icon: <Users size={16} />,
    api: `${AUTH_API}/User`,
    section: "system",
  },
  {
    id: "roles",
    name: "Bảo mật",
    icon: <ShieldCheck size={16} />,
    api: `${AUTH_API}/Roles`,
    section: "system",
  },
];

const tabTitles = {
  products: { vi: "Kho hàng", en: "Inventory" },
  categories: { vi: "Danh mục", en: "Categories" },
  orders: { vi: "Đơn hàng", en: "Orders" },
  users: { vi: "Người dùng", en: "Users" },
  roles: { vi: "Bảo mật", en: "Security" },
};

export default function App() {
  const [token, setToken] = useState(localStorage.getItem("token"));
  const [activeTab, setActiveTab] = useState("products");
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!token) return;
    setLoading(true);
    const target = menu.find((m) => m.id === activeTab);
    fetch(target.api, { headers: { Authorization: `Bearer ${token}` } })
      .then((res) => res.json())
      .then((resData) => {
        setData(Array.isArray(resData) ? resData : resData.items || []);
        setLoading(false);
      })
      .catch(() => setLoading(false));
  }, [activeTab, token]);

  if (!token)
    return (
      <>
        <InjectGlobalStyles />
        <Login onLogin={setToken} />
      </>
    );

  const title = tabTitles[activeTab];
  const mainMenu = menu.filter((m) => m.section === "main");
  const sysMenu = menu.filter((m) => m.section === "system");

  return (
    <>
      <InjectGlobalStyles />
      <div style={s.layout}>
        {/* ── SIDEBAR ── */}
        <aside style={s.sidebar}>
          <div style={s.sidebarOrb1} />
          <div style={s.sidebarOrb2} />

          {/* Logo */}
          <div style={s.logoSection}>
            <div
              style={{
                ...s.logoMark,
                animation: "shine 3s ease-in-out infinite",
              }}
            >
              🌸
            </div>
            <div>
              <div style={s.logoText}>TrMeii</div>
              <div style={s.logoSub}>Admin Panel</div>
            </div>
          </div>

          {/* Nav */}
          <nav style={{ flex: 1, padding: "0 12px" }}>
            <NavGroup
              label="Quản lý chính"
              items={mainMenu}
              activeTab={activeTab}
              setActiveTab={setActiveTab}
            />
            <NavGroup
              label="Hệ thống"
              items={sysMenu}
              activeTab={activeTab}
              setActiveTab={setActiveTab}
            />
          </nav>

          <button
            style={s.logoutBtn}
            onClick={() => {
              localStorage.clear();
              window.location.reload();
            }}
          >
            <LogOut size={16} /> Đăng xuất
          </button>
        </aside>

        {/* ── MAIN ── */}
        <main style={s.mainContent}>
          {/* Topbar */}
          <header style={s.topbar}>
            <div style={s.searchBar}>
              <Search size={16} color="var(--text-light)" />
              <input placeholder="Tìm kiếm..." style={s.searchInput} />
            </div>
            <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
              <div style={s.notifBtn}>
                <Bell size={17} />
                <span style={s.notifDot} />
              </div>
              <div style={s.avatarChip}>
                <div style={s.avatarImg}>Ad</div>
                <span
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: "var(--text-dark)",
                  }}
                >
                  Admin
                </span>
              </div>
            </div>
          </header>

          {/* Content */}
          <div style={{ padding: "28px 32px", overflowY: "auto", flex: 1 }}>
            <div style={{ marginBottom: 24 }}>
              <h1 style={s.pageTitle}>
                {title.vi} <span style={s.pageTitleAccent}>{title.en}</span>
              </h1>
              <p style={s.pageSub}>
                Đồng bộ dữ liệu thời gian thực từ microservices
              </p>
            </div>

            <StatCards />

            <AnimatePresence mode="wait">
              <motion.div
                key={activeTab}
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -12 }}
                transition={{ duration: 0.22 }}
                style={s.tableCard}
              >
                <div style={s.tableHead}>
                  <span style={s.tableTitle}>{title.vi} — danh sách</span>
                  <span style={s.liveBadge}>● Trực tiếp</span>
                </div>

                {loading ? (
                  <div style={s.loader}>Đang đồng bộ dữ liệu...</div>
                ) : (
                  <table style={{ width: "100%", borderCollapse: "collapse" }}>
                    <thead>
                      <tr>
                        {[
                          "ID",
                          "Tên / Email",
                          "Giá trị / Mô tả",
                          "Trạng thái",
                          "",
                        ].map((h) => (
                          <th key={h} style={s.th}>
                            {h}
                          </th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {data.map((row, i) => (
                        <DataRow key={i} row={row} index={i} />
                      ))}
                      {data.length === 0 && (
                        <tr>
                          <td
                            colSpan={5}
                            style={{
                              textAlign: "center",
                              padding: "40px 0",
                              color: "var(--text-light)",
                              fontSize: 14,
                            }}
                          >
                            Không có dữ liệu
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                )}
              </motion.div>
            </AnimatePresence>
          </div>
        </main>
      </div>
    </>
  );
}

// ==========================================
// NAV GROUP
// ==========================================
function NavGroup({ label, items, activeTab, setActiveTab }) {
  return (
    <>
      <p style={s.navLabel}>{label}</p>
      {items.map((item) => {
        const isActive = activeTab === item.id;
        return (
          <motion.div
            key={item.id}
            whileHover={{ x: 4, backgroundColor: "rgba(247,89,171,0.07)" }}
            onClick={() => setActiveTab(item.id)}
            style={{
              ...s.navItem,
              color: isActive ? "var(--pink-500)" : "var(--text-mid)",
              fontWeight: isActive ? 700 : 500,
              borderLeft: isActive
                ? "3px solid var(--pink-500)"
                : "3px solid transparent",
              background: isActive
                ? "linear-gradient(135deg,rgba(247,89,171,0.12),rgba(235,47,150,0.05))"
                : "transparent",
            }}
          >
            <div
              style={{
                ...s.navIcon,
                background: isActive
                  ? "linear-gradient(135deg,#f759ab,#eb2f96)"
                  : "transparent",
                color: isActive ? "#fff" : "var(--text-light)",
                boxShadow: isActive
                  ? "0 3px 10px rgba(247,89,171,0.35)"
                  : "none",
              }}
            >
              {item.icon}
            </div>
            {item.name}
          </motion.div>
        );
      })}
    </>
  );
}

// ==========================================
// DATA ROW
// ==========================================
function DataRow({ row, index }) {
  const id = row.id || row.Id || `#${index + 1}`;
  const name = row.name || row.Name || row.username || row.UserName || "—";
  const value =
    row.price || row.Price || row.email || row.Email || row.description || "—";

  return (
    <motion.tr
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.04 }}
      whileHover={{ backgroundColor: "var(--pink-50)" }}
      style={{
        borderBottom: "1px solid rgba(240,200,220,0.35)",
        cursor: "pointer",
      }}
    >
      <td style={s.td}>
        <span style={s.idChip}>{id}</span>
      </td>
      <td style={{ ...s.td, fontWeight: 600, color: "var(--text-dark)" }}>
        {name}
      </td>
      <td style={{ ...s.td, color: "var(--text-mid)" }}>{value}</td>
      <td style={s.td}>
        <span style={s.statusActive}>● Hoạt động</span>
      </td>
      <td style={s.td}>
        <motion.span
          whileHover={{ x: 3 }}
          style={{ color: "var(--pink-500)", display: "inline-block" }}
        >
          <ChevronRight size={18} />
        </motion.span>
      </td>
    </motion.tr>
  );
}

// ==========================================
// STYLES
// ==========================================
const s = {
  /* Layout */
  layout: {
    display: "flex",
    height: "100vh",
    fontFamily: "'Plus Jakarta Sans', sans-serif",
    background: "var(--cream)",
    color: "var(--text-dark)",
  },

  /* Sidebar */
  sidebar: {
    width: 260,
    background: "linear-gradient(160deg,#fff0f6 0%,#ffe4ef 60%,#ffd6e7 100%)",
    borderRight: "1px solid var(--border)",
    display: "flex",
    flexDirection: "column",
    position: "relative",
    overflow: "hidden",
  },
  sidebarOrb1: {
    position: "absolute",
    top: -70,
    right: -70,
    width: 220,
    height: 220,
    borderRadius: "50%",
    background: "radial-gradient(circle,rgba(247,89,171,0.18),transparent)",
    pointerEvents: "none",
  },
  sidebarOrb2: {
    position: "absolute",
    bottom: -50,
    left: -50,
    width: 160,
    height: 160,
    borderRadius: "50%",
    background: "radial-gradient(circle,rgba(255,133,191,0.15),transparent)",
    pointerEvents: "none",
  },
  logoSection: {
    padding: "32px 20px 20px",
    display: "flex",
    alignItems: "center",
    gap: 12,
  },
  logoMark: {
    width: 44,
    height: 44,
    borderRadius: 14,
    background: "linear-gradient(135deg,#f759ab,#eb2f96)",
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    fontSize: 22,
    boxShadow: "0 4px 16px rgba(247,89,171,0.4)",
    flexShrink: 0,
  },
  logoText: {
    fontFamily: "'Fraunces',serif",
    fontSize: 20,
    fontWeight: 300,
    color: "var(--text-dark)",
    lineHeight: 1.1,
  },
  logoSub: {
    fontSize: 10,
    color: "var(--text-light)",
    letterSpacing: 1.8,
    textTransform: "uppercase",
    fontWeight: 700,
  },
  navLabel: {
    fontSize: 10,
    fontWeight: 700,
    color: "var(--text-light)",
    letterSpacing: 2,
    textTransform: "uppercase",
    padding: "14px 14px 6px",
  },
  navItem: {
    display: "flex",
    alignItems: "center",
    gap: 10,
    padding: "10px 14px",
    marginBottom: 3,
    borderRadius: 12,
    cursor: "pointer",
    fontSize: 13,
    transition: "all .2s",
  },
  navIcon: {
    width: 30,
    height: 30,
    borderRadius: 9,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    flexShrink: 0,
    transition: "all .2s",
  },
  logoutBtn: {
    margin: "12px 16px 20px",
    padding: "10px 14px",
    borderRadius: 12,
    background: "rgba(220,50,50,0.06)",
    border: "1px solid rgba(220,50,50,0.15)",
    color: "#c0392b",
    fontSize: 12,
    fontWeight: 700,
    cursor: "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    fontFamily: "inherit",
    transition: "all .2s",
  },

  /* Main */
  mainContent: {
    flex: 1,
    display: "flex",
    flexDirection: "column",
    background:
      "radial-gradient(ellipse at top right, rgba(247,89,171,0.04), transparent 60%)",
    overflow: "hidden",
  },
  topbar: {
    height: 66,
    padding: "0 32px",
    borderBottom: "1px solid var(--border)",
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    background: "rgba(255,253,249,0.85)",
    backdropFilter: "blur(12px)",
  },
  searchBar: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    background: "#fff",
    border: "1px solid var(--border)",
    borderRadius: 30,
    padding: "8px 16px",
    width: 230,
  },
  searchInput: {
    border: "none",
    outline: "none",
    fontSize: 13,
    color: "var(--text-dark)",
    fontFamily: "inherit",
    background: "transparent",
    width: "100%",
  },
  notifBtn: {
    width: 36,
    height: 36,
    borderRadius: "50%",
    background: "#fff",
    border: "1px solid var(--border)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    cursor: "pointer",
    position: "relative",
    color: "var(--text-mid)",
  },
  notifDot: {
    width: 8,
    height: 8,
    background: "#f759ab",
    borderRadius: "50%",
    position: "absolute",
    top: 6,
    right: 6,
    border: "2px solid #fff",
  },
  avatarChip: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: "4px 12px 4px 5px",
    background: "#fff",
    border: "1px solid var(--border)",
    borderRadius: 30,
    cursor: "pointer",
  },
  avatarImg: {
    width: 28,
    height: 28,
    borderRadius: "50%",
    background: "linear-gradient(135deg,#f759ab,#ff85bf)",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    fontSize: 10,
    fontWeight: 800,
    color: "#fff",
  },

  /* Page header */
  pageTitle: {
    fontFamily: "'Fraunces',serif",
    fontSize: 34,
    fontWeight: 300,
    color: "var(--text-dark)",
    letterSpacing: -0.5,
    lineHeight: 1.1,
  },
  pageTitleAccent: {
    background: "linear-gradient(135deg,#f759ab,#eb2f96)",
    WebkitBackgroundClip: "text",
    WebkitTextFillColor: "transparent",
    backgroundClip: "text",
  },
  pageSub: {
    fontSize: 13,
    color: "var(--text-light)",
    marginTop: 4,
    fontWeight: 500,
  },

  /* Stat cards */
  statCard: {
    background: "#fff",
    border: "1px solid var(--border)",
    borderRadius: 16,
    padding: "16px 18px",
    position: "relative",
    overflow: "hidden",
    cursor: "pointer",
  },

  /* Table */
  tableCard: {
    background: "#fff",
    border: "1px solid var(--border)",
    borderRadius: 20,
    overflow: "hidden",
  },
  tableHead: {
    padding: "16px 22px",
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    borderBottom: "1px solid var(--border)",
  },
  tableTitle: { fontSize: 14, fontWeight: 700, color: "var(--text-dark)" },
  liveBadge: {
    padding: "4px 12px",
    borderRadius: 20,
    fontSize: 11,
    fontWeight: 700,
    background: "rgba(247,89,171,0.1)",
    color: "#eb2f96",
  },
  th: {
    padding: "12px 22px",
    textAlign: "left",
    fontSize: 10,
    fontWeight: 700,
    color: "var(--text-light)",
    letterSpacing: 1.5,
    textTransform: "uppercase",
    borderBottom: "1px solid var(--border)",
  },
  td: { padding: "13px 22px", fontSize: 13 },
  idChip: {
    background: "#ffe4e6",
    color: "#c41d7f",
    padding: "3px 9px",
    borderRadius: 8,
    fontSize: 11,
    fontWeight: 700,
    fontFamily: "monospace",
  },
  statusActive: {
    display: "inline-flex",
    alignItems: "center",
    gap: 4,
    padding: "4px 10px",
    borderRadius: 20,
    fontSize: 11,
    fontWeight: 700,
    background: "rgba(82,196,26,0.1)",
    color: "#389e0d",
  },
  loader: {
    padding: "60px 0",
    textAlign: "center",
    color: "var(--text-light)",
    fontSize: 14,
    fontWeight: 600,
    letterSpacing: 0.5,
  },

  /* Login */
  loginPage: {
    minHeight: "100vh",
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    background: "linear-gradient(135deg,#fff0f6 0%,#ffd6e7 50%,#ffe4f3 100%)",
    position: "relative",
    overflow: "hidden",
    fontFamily: "'Plus Jakarta Sans', sans-serif",
  },
  orb: { position: "absolute", borderRadius: "50%", pointerEvents: "none" },
  loginCard: {
    width: 400,
    background: "rgba(255,255,255,0.88)",
    backdropFilter: "blur(20px)",
    border: "1px solid rgba(255,255,255,0.95)",
    borderRadius: 28,
    padding: "44px 40px",
    textAlign: "center",
    boxShadow: "0 20px 60px rgba(247,89,171,0.18)",
    position: "relative",
    zIndex: 1,
  },
  loginIcon: {
    width: 68,
    height: 68,
    borderRadius: 22,
    background: "linear-gradient(135deg,#f759ab,#eb2f96)",
    margin: "0 auto 20px",
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    fontSize: 30,
    boxShadow: "0 8px 28px rgba(247,89,171,0.4)",
  },
  loginTitle: {
    fontFamily: "'Fraunces',serif",
    fontSize: 30,
    fontWeight: 300,
    color: "var(--text-dark)",
    marginBottom: 6,
  },
  loginSub: {
    fontSize: 13,
    color: "var(--text-light)",
    marginBottom: 28,
    fontWeight: 500,
  },
  loginInput: {
    display: "block",
    width: "100%",
    padding: "13px 16px",
    marginBottom: 12,
    border: "1.5px solid var(--border)",
    borderRadius: 12,
    fontSize: 14,
    fontFamily: "inherit",
    color: "var(--text-dark)",
    background: "#fff",
    outline: "none",
    transition: "border-color .2s",
  },
  loginBtn: {
    width: "100%",
    padding: "14px",
    borderRadius: 12,
    background: "linear-gradient(135deg,#f759ab,#eb2f96)",
    color: "#fff",
    border: "none",
    fontSize: 14,
    fontWeight: 700,
    fontFamily: "inherit",
    cursor: "pointer",
    boxShadow: "0 6px 20px rgba(247,89,171,0.4)",
  },
};
