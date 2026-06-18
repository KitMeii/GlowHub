using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/export")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ExportController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public ExportController(MySqlDbContext db) => _db = db;

        // GET /api/admin/export/revenue?from=&to=
        [HttpGet("revenue")]
        public async Task<IActionResult> ExportRevenue(
            [FromQuery] string? from = null,
            [FromQuery] string? to   = null)
        {
            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime().Date : DateTime.UtcNow.AddDays(-29).Date;
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().Date : DateTime.UtcNow.Date;

            // ✅ Aggregate trong DB theo từng ngày (không load toàn bộ Orders)
            var daily = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed
                         && o.OrderDate.Date >= fromDate
                         && o.OrderDate.Date <= toDate)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new {
                    day = g.Key,
                    count = g.Count(),
                    revenue = g.Sum(o => o.FinalAmount)
                })
                .ToDictionaryAsync(x => x.day, x => new { x.count, x.revenue });

            var csv = new StringBuilder();
            csv.AppendLine("Ngày,Số đơn hoàn thành,Doanh thu (VNĐ)");

            int totalCount = 0;
            decimal totalRevenue = 0m;
            var days = (toDate - fromDate).Days + 1;
            for (int i = 0; i < days; i++)
            {
                var day = fromDate.AddDays(i);
                var c = daily.TryGetValue(day, out var v) ? v.count : 0;
                var r = daily.TryGetValue(day, out var v2) ? v2.revenue : 0m;
                csv.AppendLine($"{day:yyyy-MM-dd},{c},{r:F0}");
                totalCount   += c;
                totalRevenue += r;
            }
            csv.AppendLine($"TỔNG,{totalCount},{totalRevenue:F0}");

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"revenue_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
        }

        // GET /api/admin/export/orders?from=&to=&status=
        [HttpGet("orders")]
        public async Task<IActionResult> ExportOrders(
            [FromQuery] string? from   = null,
            [FromQuery] string? to     = null,
            [FromQuery] string? status = null)
        {
            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime().Date : DateTime.UtcNow.AddDays(-29).Date;
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().Date : DateTime.UtcNow.Date;

            const int EXPORT_CAP = 50_000;

            var query = _db.Orders
                .Include(o => o.User)
                .Where(o => o.OrderDate.Date >= fromDate && o.OrderDate.Date <= toDate)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status.ToUpper());

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Take(EXPORT_CAP)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Mã đơn,Ngày đặt,Khách hàng,Email,Trạng thái,Thanh toán,Tổng tiền (VNĐ),Địa chỉ giao");

            foreach (var o in orders)
            {
                var customer = o.User?.Name ?? o.UserId;
                var email    = o.User?.Email ?? "";
                csv.AppendLine($"{o.OrderCode},{o.OrderDate:yyyy-MM-dd HH:mm},{EscapeCsv(customer)},{EscapeCsv(email)},{o.Status},{o.PaymentStatus},{o.FinalAmount:F0},{EscapeCsv(o.ShippingAddress ?? "")}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"orders_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
        }

        // GET /api/admin/export/users
        [HttpGet("users")]
        public async Task<IActionResult> ExportUsers()
        {
            const int EXPORT_CAP = 50_000;

            var users = await _db.Users
                .OrderByDescending(u => u.Created)
                .Take(EXPORT_CAP)
                .AsNoTracking()
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("ID,Tên,Username,Email,Điện thoại,Loại tài khoản,Trạng thái,Ngày tạo");

            foreach (var u in users)
            {
                var role   = u.UserType == 1 ? "Admin" : (u.UserType == 2 ? "Seller" : "Customer");
                var active = u.IsActive ? "Hoạt động" : "Đã khóa";
                csv.AppendLine($"{u.Id},{EscapeCsv(u.Name ?? "")},{EscapeCsv(u.UserName)},{EscapeCsv(u.Email ?? "")},{u.Phone ?? ""},{role},{active},{u.Created:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"users_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // GET /api/admin/export/shops
        [HttpGet("shops")]
        public async Task<IActionResult> ExportShops()
        {
            var shops = await _db.Shops
                .Include(s => s.Seller)
                .OrderByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // ✅ Doanh thu per-shop từ SubOrder.ProductRevenue (không scan toàn bộ Orders)
            var revenueMap = await _db.SubOrders
                .Where(s => s.Order.Status == OrderStatus.Completed && s.ShopId != null)
                .GroupBy(s => s.ShopId!)
                .Select(g => new { shopId = g.Key, revenue = g.Sum(s => s.ProductRevenue), commission = g.Sum(s => s.CommissionAmount) })
                .ToDictionaryAsync(x => x.shopId, x => new { x.revenue, x.commission });

            var csv = new StringBuilder();
            csv.AppendLine("ID,Tên shop,Chủ shop,Email,Trạng thái,Tỷ lệ hoa hồng (%),Doanh thu (VNĐ),Hoa hồng (VNĐ),Ngày tạo");

            foreach (var s in shops)
            {
                var revenue    = revenueMap.TryGetValue(s.Id, out var r) ? r.revenue : 0m;
                var commission = revenueMap.TryGetValue(s.Id, out var r2) ? r2.commission : 0m;
                var statusText = s.Status == ShopStatus.Active ? "Hoạt động" : (s.Status == ShopStatus.Banned ? "Bị khóa" : "Chờ duyệt");
                var sellerName  = s.Seller?.Name ?? s.SellerId;
                var sellerEmail = s.Seller?.Email ?? "";

                csv.AppendLine($"{s.Id},{EscapeCsv(s.ShopName)},{EscapeCsv(sellerName)},{EscapeCsv(sellerEmail)},{statusText},{s.CommissionRate},{revenue:F0},{commission:F0},{s.CreatedAt:yyyy-MM-dd}");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"shops_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
