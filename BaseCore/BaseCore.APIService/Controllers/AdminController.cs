using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Stadium.EFCore;
using Web_Stadium.Filters;

namespace Web_Stadium.Controllers
{
    [YeuCauDangNhap("Admin")]
    public class AdminController : Controller
    {
        private readonly SanBongContext _context;
        private readonly IConfiguration _config;

        public AdminController(SanBongContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        private int GetAdminId() => TokenHelper.LayUserId(Request, _config)!.Value;

        private async Task GhiLog(string hanhDong, string doiTuong, int doiTuongId, string moTa)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = GetAdminId(),
                VaiTro = "Admin",
                HanhDong = hanhDong,
                DoiTuong = doiTuong,
                DoiTuongId = doiTuongId,
                MoTa = moTa,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════
        // DASHBOARD
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var thangNay = new DateTime(now.Year, now.Month, 1);

            ViewBag.TongSan = await _context.SanBongs.CountAsync();
            ViewBag.TongUser = await _context.Users.CountAsync();
            ViewBag.TongDatSan = await _context.DatSans.CountAsync();
            ViewBag.ChoPheDuyet = await _context.SanBongs
                .CountAsync(s => s.TrangThaiDuyet == "ChoDuyet");
            ViewBag.KhieuNaiChoXuLy = await _context.KhieuNais
                .CountAsync(k => k.TrangThai == "ChoXuLy");

            // Doanh thu PitchHub tháng này (hoa hồng thực thu)
            var datSansThang = await _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Where(d => d.ThoiGianTao >= thangNay
                         && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                          || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                .ToListAsync();

            ViewBag.DoanhThuHoaHong = datSansThang.Sum(d => TinhPhiHoaHong(d));
            ViewBag.DoanhThuSan = datSansThang.Sum(d => TinhDoanhThuPhaSinh(d));

            // Biểu đồ 6 tháng
            var bieu6Thang = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var t = now.AddMonths(-i);
                var bd = new DateTime(t.Year, t.Month, 1);
                var kt = bd.AddMonths(1);
                var rows = await _context.DatSans
                    .Include(d => d.KhungGio).ThenInclude(k => k.SanBong)
                    .Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt
                             && (d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                              || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy"))
                    .ToListAsync();
                var phi = rows.Sum(d => TinhPhiHoaHong(d));
                bieu6Thang.Add(new { thang = t.ToString("MM/yyyy"), phi = (double)phi });
            }
            ViewBag.Bieu6Thang = bieu6Thang;

            ViewBag.TopSan = await _context.SanBongs
                .Include(s => s.KhungGios).ThenInclude(k => k.DatSans)
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new
                {
                    Ten = s.TenSan,
                    SoLuot = s.KhungGios.SelectMany(k => k.DatSans).Count()
                })
                .OrderByDescending(x => x.SoLuot).Take(5).ToListAsync();

            return View();
        }

        // Helper tính doanh thu thực phát sinh theo kịch bản KA / KB
        private decimal TinhDoanhThuPhaSinh(DatSan d)
        {
            // KA: Khách hủy — doanh thu = TienCoc
            if (d.TrangThai == "DaHuy") return d.TienCoc;
            // KB: Khách đến đá đủ — doanh thu = TongTien (nếu = 0 thì dùng TienCoc)
            return d.TongTien > 0 ? d.TongTien : d.TienCoc;
        }

        private decimal TinhPhiHoaHong(DatSan d)
        {
            var dt = TinhDoanhThuPhaSinh(d);

            // Lấy tên quận của sân
            var tenQuan = d.KhungGio?.SanBong?.Quan;

            // Tìm VungKhuVuc qua DanhMucQuan
            var tyLe = _context.DanhMucQuans
                .Where(q => q.TenQuan == tenQuan)
                .Select(q => q.VungKhuVuc.TyLeHoaHong)
                .FirstOrDefault();

            return dt * (tyLe == 0 ? 0.10m : tyLe);
        }

        // ══════════════════════════════════════════════════════════
        // MASTER DATA — bao gồm quản lý Vùng
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> MasterData()
        {
            ViewBag.Vungs = await _context.VungKhuVucs
                .Include(v => v.DanhMucQuans)
                .OrderBy(v => v.ThuTu).ToListAsync();
            ViewBag.Quans = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .OrderBy(q => q.ThuTu).ToListAsync();
            ViewBag.LoaiSans = await _context.DanhMucLoaiSans.ToListAsync();
            ViewBag.LoaiCos = await _context.DanhMucLoaiCos.ToListAsync();
            ViewBag.DichVus = await _context.DanhMucDichVus.ToListAsync();
            return View();
        }

        // ── Vùng khu vực ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemVung(string tenVung, string? moTa,
            decimal tyLeHoaHong, string mauSac, double lat, double lng, int thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenVung))
            { TempData["Error"] = "Tên vùng không được để trống."; return RedirectToAction("MasterData"); }
            if (await _context.VungKhuVucs.AnyAsync(v => v.TenVung == tenVung))
            { TempData["Error"] = $"Vùng \"{tenVung}\" đã tồn tại."; return RedirectToAction("MasterData"); }

            var vung = new VungKhuVuc
            {
                TenVung = tenVung,
                MoTa = moTa,
                TyLeHoaHong = tyLeHoaHong,
                MauSac = mauSac,
                Lat = lat,
                Lng = lng,
                ThuTu = thuTu
            };
            _context.VungKhuVucs.Add(vung);
            await _context.SaveChangesAsync();
            await GhiLog("ThemVung", "VungKhuVuc", vung.Id, $"Thêm vùng: {tenVung} — {tyLeHoaHong:P0}");
            TempData["Success"] = $"Đã thêm vùng \"{tenVung}\" ({tyLeHoaHong:P0})!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> SuaVung(int id, decimal tyLeHoaHong, string? moTa, string mauSac)
        {
            var vung = await _context.VungKhuVucs.FindAsync(id);
            if (vung == null) return NotFound();
            var oldTyLe = vung.TyLeHoaHong;
            vung.TyLeHoaHong = tyLeHoaHong;
            vung.MoTa = moTa;
            vung.MauSac = mauSac;
            await _context.SaveChangesAsync();
            await GhiLog("SuaVung", "VungKhuVuc", id,
                $"Sửa vùng {vung.TenVung}: {oldTyLe:P0} → {tyLeHoaHong:P0}");
            TempData["Success"] = $"Đã cập nhật vùng \"{vung.TenVung}\": {tyLeHoaHong:P0}";
            return RedirectToAction("MasterData");
        }

        // ── Gán Quận vào Vùng ────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> GanQuanVaoVung(int quanId, int? vungId)
        {
            var quan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc).FirstOrDefaultAsync(q => q.Id == quanId);
            if (quan == null) return NotFound();
            var oldVung = quan.VungKhuVuc?.TenVung ?? "Chưa có";
            quan.VungKhuVucId = vungId;
            await _context.SaveChangesAsync();
            var newVung = vungId.HasValue
                ? (await _context.VungKhuVucs.FindAsync(vungId))?.TenVung : "Bỏ gán";
            await GhiLog("GanQuanVaoVung", "DanhMucQuan", quanId,
                $"{quan.TenQuan}: {oldVung} → {newVung}");
            TempData["Success"] = $"Đã gán \"{quan.TenQuan}\" vào vùng \"{newVung}\"";
            return RedirectToAction("MasterData");
        }

        // AJAX — tra tỷ lệ hoa hồng theo QuanId (dùng trong form đăng ký sân của Owner)
        [HttpGet]
        public async Task<IActionResult> GetTyLeTheoQuan(int quanId)
        {
            var quan = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .FirstOrDefaultAsync(q => q.Id == quanId);
            if (quan?.VungKhuVuc == null)
                return Json(new { tyLe = 0.10, tenVung = "Chưa phân vùng", mauSac = "#aaa" });
            return Json(new
            {
                tyLe = (double)quan.VungKhuVuc.TyLeHoaHong,
                tenVung = quan.VungKhuVuc.TenVung,
                mauSac = quan.VungKhuVuc.MauSac
            });
        }

        // ── Quận / Huyện ────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemQuan(string tenQuan, string thanhPho, int thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenQuan))
            { TempData["Error"] = "Tên quận không được để trống."; return RedirectToAction("MasterData"); }
            if (await _context.DanhMucQuans.AnyAsync(q => q.TenQuan == tenQuan))
            { TempData["Error"] = $"Quận \"{tenQuan}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var q = new DanhMucQuan { TenQuan = tenQuan, ThanhPho = thanhPho, ThuTu = thuTu };
            _context.DanhMucQuans.Add(q);
            await _context.SaveChangesAsync();
            await GhiLog("ThemQuan", "DanhMucQuan", q.Id, $"Thêm quận: {tenQuan}");
            TempData["Success"] = $"Đã thêm quận \"{tenQuan}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiQuan(int id, bool isActive)
        {
            var q = await _context.DanhMucQuans.FindAsync(id);
            if (q == null) return NotFound();
            q.IsActive = isActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"{(isActive ? "Bật" : "Tắt")} quận \"{q.TenQuan}\"";
            return RedirectToAction("MasterData");
        }

        // ── Loại Sân ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemLoaiSan(string maLoai, string tenLoai)
        {
            if (await _context.DanhMucLoaiSans.AnyAsync(l => l.MaLoai == maLoai))
            { TempData["Error"] = $"Mã \"{maLoai}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var l = new DanhMucLoaiSan { MaLoai = maLoai, TenLoai = tenLoai };
            _context.DanhMucLoaiSans.Add(l);
            await _context.SaveChangesAsync();
            await GhiLog("ThemLoaiSan", "DanhMucLoaiSan", l.Id, $"Thêm: {maLoai}");
            TempData["Success"] = $"Đã thêm loại sân \"{tenLoai}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiLoaiSan(int id, bool isActive)
        {
            var l = await _context.DanhMucLoaiSans.FindAsync(id);
            if (l == null) return NotFound();
            l.IsActive = isActive; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(isActive ? "Bật" : "Tắt")} loại sân \"{l.TenLoai}\"";
            return RedirectToAction("MasterData");
        }

        // ── Loại Cỏ ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemLoaiCo(string maLoai, string tenLoai)
        {
            if (await _context.DanhMucLoaiCos.AnyAsync(l => l.MaLoai == maLoai))
            { TempData["Error"] = $"Mã \"{maLoai}\" đã tồn tại."; return RedirectToAction("MasterData"); }
            var l = new DanhMucLoaiCo { MaLoai = maLoai, TenLoai = tenLoai };
            _context.DanhMucLoaiCos.Add(l);
            await _context.SaveChangesAsync();
            await GhiLog("ThemLoaiCo", "DanhMucLoaiCo", l.Id, $"Thêm: {maLoai}");
            TempData["Success"] = $"Đã thêm loại cỏ \"{tenLoai}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiLoaiCo(int id, bool isActive)
        {
            var l = await _context.DanhMucLoaiCos.FindAsync(id);
            if (l == null) return NotFound();
            l.IsActive = isActive; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(isActive ? "Bật" : "Tắt")} loại cỏ \"{l.TenLoai}\"";
            return RedirectToAction("MasterData");
        }

        // ── Danh mục dịch vụ ────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> ThemDanhMucDichVu(string tenDichVu, string? icon, string? moTa)
        {
            if (string.IsNullOrWhiteSpace(tenDichVu))
            { TempData["Error"] = "Tên dịch vụ không được để trống."; return RedirectToAction("MasterData"); }
            var dv = new DanhMucDichVu { TenDichVu = tenDichVu, Icon = icon, MoTa = moTa };
            _context.DanhMucDichVus.Add(dv);
            await _context.SaveChangesAsync();
            await GhiLog("ThemDanhMucDichVu", "DanhMucDichVu", dv.Id, $"Thêm: {tenDichVu}");
            TempData["Success"] = $"Đã thêm dịch vụ \"{tenDichVu}\"!";
            return RedirectToAction("MasterData");
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiDichVu(int id, bool isActive)
        {
            var dv = await _context.DanhMucDichVus.FindAsync(id);
            if (dv == null) return NotFound();
            dv.IsActive = isActive; await _context.SaveChangesAsync();
            TempData["Success"] = $"{(isActive ? "Bật" : "Tắt")} \"{dv.TenDichVu}\"";
            return RedirectToAction("MasterData");
        }

        // ══════════════════════════════════════════════════════════
        // FRANCHISE — Duyệt sân + khoá/mở TK
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> DuyetSan()
        {
            var list = await _context.SanBongs
                .Include(s => s.Owner)
                .Where(s => s.TrangThaiDuyet == "ChoDuyet")
                .OrderBy(s => s.Id).ToListAsync();

            ViewBag.TyLeMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(q => q.TenQuan, q => q.VungKhuVuc!.TyLeHoaHong);

            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> PheDuyet(int id, string trangThai, decimal? tyLeOverride)
        {
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (san == null) return NotFound();

            san.TrangThaiDuyet = trangThai;

            // Tìm đúng 1 bản ghi VungKhuVuc của sân này
            var vungKhuVuc = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.TenQuan == san.Quan && q.VungKhuVuc != null)
                .Select(q => q.VungKhuVuc)
                .FirstOrDefaultAsync();

            // Admin override tỷ lệ hoa hồng trước khi duyệt (nếu cần)
            if (tyLeOverride.HasValue && tyLeOverride > 0 && vungKhuVuc != null)
                vungKhuVuc.TyLeHoaHong = tyLeOverride.Value;

            // Lấy tyLe để log
            var tyLeLog = vungKhuVuc?.TyLeHoaHong ?? 0.10m;

            // Kích hoạt Owner khi duyệt sân đầu tiên
            if (trangThai == "DaDuyet" && san.Owner != null && !san.Owner.IsActive)
            {
                san.Owner.IsActive = true;
                await GhiLog("KichHoatOwner", "User", san.Owner.Id,
                    $"Kích hoạt Owner {san.Owner.HoTen} khi duyệt sân đầu tiên");
            }

            await _context.SaveChangesAsync();

            await GhiLog(trangThai == "DaDuyet" ? "PheDuyetSan" : "TuChoiSan",
                "SanBong", id, $"{trangThai}: {san.TenSan} — HH: {tyLeLog:P0}");

            TempData["Success"] = trangThai == "DaDuyet"
                ? $"Đã duyệt \"{san.TenSan}\" (hoa hồng {tyLeLog:P0})"
                : $"Đã từ chối \"{san.TenSan}\"";

            return RedirectToAction("DuyetSan");
        }

        // Xem hợp đồng Owner đã ký (trong trang DuyetSan)
        public async Task<IActionResult> XemHopDong(int sanId)
        {
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == sanId);
            if (san == null) return NotFound();
            return View(san);
        }

        // Admin: Danh sach tat ca hop dong + filter
        public async Task<IActionResult> TatCaHopDong(string? tuKhoa, string? sapXep)
        {
            var query = _context.SanBongs
                .Include(s => s.Owner)
                .Where(s => s.DaKyHopDong && !string.IsNullOrEmpty(s.NoiDungHopDong))
                .AsQueryable();

            if (!string.IsNullOrEmpty(tuKhoa))
                query = query.Where(s => s.TenSan.Contains(tuKhoa)
                                      || s.Owner.HoTen.Contains(tuKhoa)
                                      || s.Quan.Contains(tuKhoa));

            query = sapXep == "cu_nhat"
                ? query.OrderBy(s => s.NgayKyHopDong)
                : query.OrderByDescending(s => s.NgayKyHopDong);

            var list = await query.ToListAsync();
            ViewBag.TuKhoa = tuKhoa;
            ViewBag.SapXep = sapXep;
            ViewBag.TongHD = list.Count;
            return View(list);
        }

        // Admin: Xuat Word (RTF) hop dong
        public async Task<IActionResult> XuatWordHopDong(int sanId)
        {
            var san = await _context.SanBongs
                .Include(s => s.Owner)
                .FirstOrDefaultAsync(s => s.Id == sanId);

            if (san == null || string.IsNullOrEmpty(san.NoiDungHopDong))
                return NotFound();

            var tenFile = $"HopDong_{san.TenSan.Replace(" ", "_")}_{san.NgayKyHopDong:yyyyMMdd}.rtf";
            var bytes = TaoRtfHopDong(san, san.NoiDungHopDong);
            return File(bytes, "application/rtf", tenFile);
        }

        private static byte[] TaoRtfHopDong(Web_Stadium.EFCore.SanBong san, string noiDung)
        {
            // RTF header
            var header = new System.Text.StringBuilder();
            header.AppendLine(@"{\rtf1\ansi\ansicpg1252\deff0\deflang1066");
            header.AppendLine(@"{\fonttbl{\f0\froman\fprq2\fcharset0 Times New Roman;}}");
            header.AppendLine(@"{\colortbl ;\red0\green128\blue0;\red100\green100\blue100;}");
            header.AppendLine(@"\paperw11906\paperh16838");
            header.AppendLine(@"\margl1800\margr1800\margt1440\margb1440");
            header.AppendLine(@"\widowctrl\hyphauto");

            var body = new System.Text.StringBuilder();

            // Phan noi dung hop dong chinh (tu NoiDungHopDong)
            var lines = noiDung.Replace("\r", "").Split('\n');
            foreach (var raw in lines)
            {
                var l = raw.TrimEnd();
                if (string.IsNullOrWhiteSpace(l))
                { body.AppendLine(@"\pard\par"); continue; }

                var esc = RtfEsc(l);

                // Bo qua duong ke ngang (───)
                if (l.Contains("\u2500\u2500\u2500") || l.Contains("───"))
                    continue;

                // Quoc hieu, doc lap
                if (l.StartsWith("C\u1ed8NG HO\u00c0") || l.StartsWith("C\u1ed8NG H\u00d2A") ||
                    l.StartsWith("\u0110\u1ed9c l\u1eadp"))
                    body.AppendLine($@"\pard\qc\f0\fs24\b {esc}\b0\par");
                // Tieu de hop dong lon
                else if (l.Contains("H\u1ee2P \u0110\u1ed2NG H\u1ee2P T\u00c1C") || l.StartsWith("S\u1ed1:"))
                    body.AppendLine($@"\pard\qc\f0\fs28\b {esc}\b0\par");
                // Dieu khoan (Dieu 1. Dieu 2. ...)
                else if (System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^\u0110i\u1ec1u [0-9]+\."))
                    body.AppendLine($@"\pard\ql\sb200\f0\fs24\b {esc}\b0\par");
                // Tieu muc (1.1. 2.3. ...)
                else if (System.Text.RegularExpressions.Regex.IsMatch(l.TrimStart(), @"^[0-9]+\.[0-9]+\."))
                    body.AppendLine($@"\pard\ql\sb100\f0\fs22\b {esc}\b0\par");
                // Dong gach dau
                else if (l.TrimStart().StartsWith("  -") || l.TrimStart().StartsWith("- ") ||
                         l.TrimStart().StartsWith("   -"))
                    body.AppendLine($@"\pard\ql\li720\fi-360\f0\fs22 {esc}\par");
                // Phan chu ky / dia diem ngay thang
                else if (l.TrimStart().StartsWith("H\u00e0 N\u1ed9i") || l.TrimStart().StartsWith("Th\u00e0nh ph\u1ed1") ||
                         l.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caNA") || l.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caN B") ||
                         l.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N A") || l.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N B") ||
                         l.Contains("(K\u00fd,") || l.Contains("C\u00f4ng ty TNHH") || l.Contains("Gi\u00e1m \u0111\u1ed1c") ||
                         l.Contains("Ch\u1ee7 s\u00e2n"))
                    body.AppendLine($@"\pard\ql\f0\fs22 {esc}\par");
                else
                    body.AppendLine($@"\pard\ql\f0\fs22 {esc}\par");
            }

            // Phan chu ky cuoi hop dong - them tu dong neu chua co trong noiDung
            if (!noiDung.Contains("\u0110\u1ea0I DI\u1ec6N B\u00caNA") && !noiDung.Contains("\u0110\u1ea0I DI\u1ec6N B\u1ec2N A"))
            {
                var ngayKy = san.NgayKyHopDong?.ToString("dd/MM/yyyy") ?? System.DateTime.Now.ToString("dd/MM/yyyy");
                var tenOwner = RtfEsc(san.Owner?.HoTen ?? "");
                var tenSan = RtfEsc(san.TenSan ?? "");

                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22                 {RtfEsc("H\u00e0 N\u1ed9i, ng\u00e0y")} {RtfEsc(ngayKy.Split('/')[0])} {RtfEsc("th\u00e1ng")} {RtfEsc(ngayKy.Split('/')[1])} {RtfEsc("n\u0103m")} {RtfEsc(ngayKy.Split('/')[2])}\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22         {RtfEsc("\u0110\u1ea0I DI\u1ec6N B\u00caN A")}                    {RtfEsc("\u0110\u1ea0I DI\u1ec6N B\u00caN B")}\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("(K\u00fd, ghi r\u00f5 h\u1ecd t\u00ean, \u0111\u00f3ng d\u1ea5u)")}      {RtfEsc("(K\u00fd, ghi r\u00f5 h\u1ecd t\u00ean)")}\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine(@"\pard\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("C\u00f4ng ty TNHH PitchHub")}                  {tenOwner}\par");
                body.AppendLine($@"\pard\ql\f0\fs22    {RtfEsc("Gi\u00e1m \u0111\u1ed1c \u0111i\u1ec1u h\u00e0nh")}                     {RtfEsc("Ch\u1ee7 s\u00e2n")} {tenSan}\par");
            }

            var rtf = header.ToString() + body.ToString() + "}";
            return System.Text.Encoding.GetEncoding(1252).GetBytes(rtf);
        }

        private static string RtfEsc(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\\') sb.Append(@"\\");
                else if (ch == '{') sb.Append(@"\{");
                else if (ch == '}') sb.Append(@"\}");
                else if (ch > 127) sb.Append("\\u" + (int)ch + "?");
                else sb.Append(ch);
            }
            return sb.ToString();
        }


        public async Task<IActionResult> QuanLyUser(string? vaiTro, string? tuKhoa)
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(vaiTro)) query = query.Where(u => u.VaiTro == vaiTro);
            if (!string.IsNullOrEmpty(tuKhoa))
                query = query.Where(u => u.HoTen.Contains(tuKhoa) || u.Email.Contains(tuKhoa));
            ViewBag.VaiTro = vaiTro; ViewBag.TuKhoa = tuKhoa;
            return View(await query.OrderByDescending(u => u.NgayTao).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> DoiTrangThaiTaiKhoan(int id, bool isActive)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            if (user.Id == GetAdminId())
            { TempData["Error"] = "Không thể khoá tài khoản đang đăng nhập!"; return RedirectToAction("QuanLyUser"); }
            if (user.VaiTro == "Admin")
            { TempData["Error"] = "Không thể khoá tài khoản Admin!"; return RedirectToAction("QuanLyUser"); }
            user.IsActive = isActive;
            await _context.SaveChangesAsync();
            await GhiLog(isActive ? "MoKhoaTaiKhoan" : "KhoaTaiKhoan", "User", id,
                $"{(isActive ? "Mở" : "Khoá")} TK: {user.HoTen} ({user.VaiTro})");
            TempData["Success"] = $"{(isActive ? "Mở khoá" : "Khoá")} \"{user.HoTen}\"!";
            return RedirectToAction("QuanLyUser");
        }

        // ══════════════════════════════════════════════════════════
        // KHIẾU NẠI & HOÀN CỌC
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> KhieuNai(string? trangThai)
        {
            trangThai ??= "ChoXuLy";
            var list = await _context.KhieuNais
                .Include(k => k.User)
                .Include(k => k.DatSan).ThenInclude(d => d.KhungGio).ThenInclude(k => k.SanBong)
                .Include(k => k.AdminXuLy)
                .Where(k => trangThai == "TatCa" || k.TrangThai == trangThai)
                .OrderByDescending(k => k.NgayGui).ToListAsync();
            ViewBag.TrangThaiFilter = trangThai;
            ViewBag.SoChoXuLy = await _context.KhieuNais.CountAsync(k => k.TrangThai == "ChoXuLy");
            return View(list);
        }

        [HttpPost]
        public async Task<IActionResult> XuLyKhieuNai(int id, string ketQua, decimal? soTienHoan, string? ghiChu)
        {
            var kn = await _context.KhieuNais
                .Include(k => k.DatSan).Include(k => k.User)
                .FirstOrDefaultAsync(k => k.Id == id);
            if (kn == null) return NotFound();
            kn.TrangThai = ketQua;
            kn.SoTienHoan = soTienHoan;
            kn.GhiChuAdmin = ghiChu;
            kn.NgayXuLy = DateTime.Now;
            kn.AdminXuLyId = GetAdminId();
            if (ketQua == "DaHoanCoc" && kn.DatSan != null) kn.DatSan.TrangThai = "DaHuy";
            await _context.SaveChangesAsync();
            await GhiLog(ketQua == "DaHoanCoc" ? "HoanCoc" : "TuChoiHoanCoc",
                "KhieuNai", id, $"{kn.User?.HoTen} — {soTienHoan:N0}đ");
            TempData["Success"] = ketQua == "DaHoanCoc"
                ? $"Đã hoàn {soTienHoan:N0}đ cho \"{kn.User?.HoTen}\""
                : "Đã từ chối khiếu nại.";
            return RedirectToAction("KhieuNai");
        }

        // ══════════════════════════════════════════════════════════
        // BÁO CÁO — KA/KB + xếp hạng sân theo hoa hồng
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> BaoCao(
            string loai = "thang", int? nam = null, int? thang = null)
        {
            var now = DateTime.Now;
            nam ??= now.Year;
            thang ??= now.Month;

            ViewBag.Loai = loai; ViewBag.Nam = nam; ViewBag.Thang = thang;

            DateTime batDau, ketThuc;
            var data = new List<object>();
            string tieuDe;

            // Load tất cả đơn kèm SanBong (cần TyLeHoaHong)
            IQueryable<DatSan> baseQ = _context.DatSans
                .Include(d => d.KhungGio).ThenInclude(k => k.SanBong).ThenInclude(s => s.Owner)
                .Where(d => d.TrangThai == "DaXacNhan" || d.TrangThai == "HoanThanh"
                         || d.TrangThai == "DangSuDung" || d.TrangThai == "DaHuy");

            switch (loai)
            {
                case "ngay":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo ngày — Tháng {thang}/{nam}";
                    for (int ng = 1; ng <= DateTime.DaysInMonth(nam.Value, thang.Value); ng++)
                    {
                        var bd = new DateTime(nam.Value, thang.Value, ng);
                        var kt = bd.AddDays(1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildDataPoint($"{ng}/{thang}", rows));
                    }
                    break;

                case "tuan":
                    batDau = new DateTime(nam.Value, thang.Value, 1);
                    ketThuc = batDau.AddMonths(1);
                    tieuDe = $"Theo tuần — Tháng {thang}/{nam}";
                    int tuan = 1; var cur = batDau;
                    while (cur < ketThuc)
                    {
                        var kt = cur.AddDays(7) < ketThuc ? cur.AddDays(7) : ketThuc;
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= cur && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildDataPoint($"T{tuan} ({cur:dd/MM}–{kt.AddDays(-1):dd/MM})", rows));
                        cur = kt; tuan++;
                    }
                    break;

                case "quy":
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo quý — Năm {nam}";
                    for (int q = 1; q <= 4; q++)
                    {
                        var bd = new DateTime(nam.Value, (q - 1) * 3 + 1, 1);
                        var kt = bd.AddMonths(3);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildDataPoint($"Q{q}/{nam}", rows));
                    }
                    break;

                case "nam":
                    batDau = new DateTime(now.Year - 4, 1, 1);
                    ketThuc = new DateTime(now.Year + 1, 1, 1);
                    tieuDe = "Theo năm — 5 năm gần nhất";
                    for (int y = now.Year - 4; y <= now.Year; y++)
                    {
                        var bd = new DateTime(y, 1, 1);
                        var kt = new DateTime(y + 1, 1, 1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildDataPoint($"{y}", rows));
                    }
                    break;

                default: // thang
                    batDau = new DateTime(nam.Value, 1, 1);
                    ketThuc = new DateTime(nam.Value + 1, 1, 1);
                    tieuDe = $"Theo tháng — Năm {nam}";
                    for (int t = 1; t <= 12; t++)
                    {
                        var bd = new DateTime(nam.Value, t, 1);
                        var kt = bd.AddMonths(1);
                        var rows = await baseQ.Where(d => d.ThoiGianTao >= bd && d.ThoiGianTao < kt).ToListAsync();
                        data.Add(BuildDataPoint($"T{t}", rows));
                    }
                    break;
            }

            ViewBag.Data = data; ViewBag.TieuDe = tieuDe;
            ViewBag.TongPhiHoaHong = (double)data.Sum(d => (double)((dynamic)d).phi);
            ViewBag.TongDoanhThuSan = (double)data.Sum(d => (double)((dynamic)d).dtSan);
            ViewBag.TongLuot = (int)data.Sum(d => (int)((dynamic)d).soLuot);
            ViewBag.DiemCaoNhat = data.OrderByDescending(d => (double)((dynamic)d).phi).FirstOrDefault();

            // Aliases cho BaoCao View
            ViewBag.DoanhThuNam = data.Select(d => new
            {
                thang = (string)((dynamic)d).nhan,
                dt = (double)((dynamic)d).dtSan,
                soLuot = (int)((dynamic)d).soLuot
            }).Cast<dynamic>().ToList();

            ViewBag.DoanhThuTheoOwner = ViewBag.XepHangSan; // duoc set ben duoi


            // Load TyLeHoaHong map từ VungKhuVuc trước
            var tyLeMap = await _context.DanhMucQuans
                .Include(q => q.VungKhuVuc)
                .Where(q => q.VungKhuVuc != null)
                .ToDictionaryAsync(
                    q => q.TenQuan,
                    q => q.VungKhuVuc!.TyLeHoaHong
                );

            // Xếp hạng sân theo hoa hồng
            var allRows = await baseQ
                .Where(d => d.ThoiGianTao >= batDau && d.ThoiGianTao < ketThuc)
                .ToListAsync();

            ViewBag.XepHangSan = allRows
                .GroupBy(d => new
                {
                    TenSan = d.KhungGio?.SanBong?.TenSan ?? "?",
                    Owner = d.KhungGio?.SanBong?.Owner?.HoTen ?? "?",
                    TyLe = tyLeMap.TryGetValue(
                                 d.KhungGio?.SanBong?.Quan ?? "",
                                 out var tl) ? tl : 0.10m   // ✅ lấy từ VungKhuVuc
                })
                .Select(g => new
                {
                    TenSan = g.Key.TenSan,
                    Owner = g.Key.Owner,
                    TyLeHH = (double)g.Key.TyLe,
                    TongDT = g.Sum(d => (double)TinhDoanhThuPhaSinh(d)),
                    PhiHoaHong = g.Sum(d => (double)TinhPhiHoaHong(d)),
                    SoLuot = g.Count(),
                    SoLuotKA = g.Count(d => d.TrangThai == "DaHuy"),
                    SoLuotKB = g.Count(d => d.TrangThai == "HoanThanh"
                                            || d.TrangThai == "DangSuDung")
                })
                .OrderByDescending(x => x.PhiHoaHong)
                .ToList();

            ViewBag.TyLeLapDay = await _context.SanBongs
                .Include(s => s.KhungGios)
                .Where(s => s.TrangThaiDuyet == "DaDuyet")
                .Select(s => new
                {
                    Ten = s.TenSan,
                    Tong = s.KhungGios.Count,
                    DaDat = s.KhungGios.Count(k => k.TrangThai == "DaDat")
                }).ToListAsync();
            ViewBag.TyLe = ViewBag.TyLeLapDay;
            ViewBag.DoanhThuTheoOwner = ViewBag.XepHangSan;

            return View();
        }

        private object BuildDataPoint(string nhan, List<DatSan> rows) => new
        {
            nhan = nhan,
            dtSan = (double)rows.Sum(d => TinhDoanhThuPhaSinh(d)),
            phi = (double)rows.Sum(d => TinhPhiHoaHong(d)),
            soLuot = rows.Count,
            soKA = rows.Count(d => d.TrangThai == "DaHuy"),
            soKB = rows.Count(d => d.TrangThai == "HoanThanh" || d.TrangThai == "DangSuDung")
        };

        // ══════════════════════════════════════════════════════════
        // AUDIT LOG
        // ══════════════════════════════════════════════════════════
        public async Task<IActionResult> AuditLog(
            string? hanhDong, int? userId,
            DateTime? tuNgay, DateTime? denNgay, int trang = 1)
        {
            var query = _context.AuditLogs.Include(a => a.User).AsQueryable();
            if (!string.IsNullOrEmpty(hanhDong)) query = query.Where(a => a.HanhDong == hanhDong);
            if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
            if (tuNgay.HasValue) query = query.Where(a => a.ThoiGian >= tuNgay.Value);
            if (denNgay.HasValue) query = query.Where(a => a.ThoiGian < denNgay.Value.AddDays(1));

            const int pageSize = 20;
            ViewBag.TongSo = await query.CountAsync();
            ViewBag.Trang = trang;
            ViewBag.TongTrang = (int)Math.Ceiling((double)ViewBag.TongSo / pageSize);
            ViewBag.HanhDong = hanhDong; ViewBag.UserId = userId;
            ViewBag.TuNgay = tuNgay?.ToString("yyyy-MM-dd");
            ViewBag.DenNgay = denNgay?.ToString("yyyy-MM-dd");
            ViewBag.DanhSachHanhDong = await _context.AuditLogs
                .Select(a => a.HanhDong).Distinct().OrderBy(h => h).ToListAsync();

            return View(await query.OrderByDescending(a => a.ThoiGian)
                .Skip((trang - 1) * pageSize).Take(pageSize).ToListAsync());
        }
    }
}