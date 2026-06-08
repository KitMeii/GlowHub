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

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.Status == OrderStatus.Completed
                         && o.OrderDate.Date >= fromDate
                         && o.OrderDate.Date <= toDate)
                .OrderBy(o => o.OrderDate)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Ngày,Số đơn hoàn thành,Doanh thu (VNĐ)");

            var days = (toDate - fromDate).Days + 1;
            for (int i = 0; i < days; i++)
            {
                var day = fromDate.AddDays(i);
                var dayOrders = orders.Where(o => o.OrderDate.Date == day).ToList();
                var revenue = dayOrders.Sum(o => o.FinalAmount);
                csv.AppendLine($"{day:yyyy-MM-dd},{dayOrders.Count},{revenue:F0}");
            }

            var totalRevenue = orders.Sum(o => o.FinalAmount);
            csv.AppendLine($"TỔNG,{orders.Count},{totalRevenue:F0}");

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

            var query = _db.Orders
                .Include(o => o.User)
                .Where(o => o.OrderDate.Date >= fromDate && o.OrderDate.Date <= toDate);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status.ToUpper());

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

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
            var users = await _db.Users
                .OrderByDescending(u => u.Created)
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
                .ToListAsync();

            var allOrders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.Status == OrderStatus.Completed)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("ID,Tên shop,Chủ shop,Email,Trạng thái,Tỷ lệ hoa hồng (%),Doanh thu (VNĐ),Hoa hồng (VNĐ),Ngày tạo");

            foreach (var s in shops)
            {
                var productIds = await _db.Products
                    .Where(p => p.ShopId == s.Id)
                    .Select(p => p.Id)
                    .ToListAsync();

                var revenue = allOrders
                    .Where(o => o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                    .Sum(o => o.OrderDetails
                        .Where(od => productIds.Contains(od.ProductId))
                        .Sum(od => od.UnitPrice * od.Quantity));

                var commission = revenue * (s.CommissionRate / 100m);
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
