import { defineConfig } from 'vite';

// Cấu hình tối giản: vite serve thư mục public/ (kaira-1.0.0/)
// + proxy /api/auth, /api/users, /api/roles → AuthService 5002, còn lại → APIService 5001
export default defineConfig({
  server: {
    port: 5173,
    open: '/kaira-1.0.0/index.html',
    proxy: {
      '/api/auth':  { target: 'http://localhost:5002', changeOrigin: true },
      '/api/users': { target: 'http://localhost:5002', changeOrigin: true },
      '/api/roles': { target: 'http://localhost:5002', changeOrigin: true },
      '/api':       { target: 'http://localhost:5001', changeOrigin: true },
      // Ảnh upload được backend lưu vào /uploads/* — proxy để <img src="/uploads/..."> hoạt động
      '/uploads':   { target: 'http://localhost:5001', changeOrigin: true },
    },
  },
});
