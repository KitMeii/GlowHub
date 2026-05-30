using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FlashSaleController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public FlashSaleController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        /// <summary>GET /api/flashsale/active — Flash sale đang diễn ra</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var now = DateTime.UtcNow;
            var sale = await _db.FlashSales
                .Where(fs => fs.IsActive && fs.StartTime <= now && fs.EndTime > now)
                .OrderByDescending(fs => fs.StartTime)
                .FirstOrDefaultAsync();

            if (sale == null) return Ok(null);

            var products = await _db.FlashSaleProducts
                .Include(p => p.Product)
                .Where(p => p.FlashSaleId == sale.Id && p.IsActive)
                .ToListAsync();

            return Ok(new
            {
                id = sale.Id,
                name = sale.Name,
                startTime = sale.StartTime,
                endTime = sale.EndTime,
                secondsRemaining = (int)(sale.EndTime - now).TotalSeconds,
                products = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Product.Name,
                    image = p.Product.ImageUrl,
                    originalPrice = p.OriginalPrice,
                    salePrice = p.SalePrice,
                    discountPercent = p.OriginalPrice > 0
                        ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                        : 0,
                    soldCount = p.SoldCount,
                    totalQuantity = p.Quantity,
                    remainingQuantity = p.RemainingQuantity
                })
            });
        }

        /// <summary>GET /api/flashsale/upcoming — Flash sale sắp diễn ra (24h tới)</summary>
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming()
        {
            var now = DateTime.UtcNow;
            var in24h = now.AddHours(24);

            var sales = await _db.FlashSales
                .Where(fs => fs.IsActive && fs.StartTime > now && fs.StartTime <= in24h)
                .OrderBy(fs => fs.StartTime)
                .ToListAsync();

            var result = new List<object>();
            foreach (var sale in sales)
            {
                var products = await _db.FlashSaleProducts
                    .Include(p => p.Product)
                    .Where(p => p.FlashSaleId == sale.Id && p.IsActive)
                    .ToListAsync();

                result.Add(new
                {
                    id = sale.Id,
                    name = sale.Name,
                    startTime = sale.StartTime,
                    endTime = sale.EndTime,
                    secondsUntilStart = (int)(sale.StartTime - now).TotalSeconds,
                    products = products.Select(p => new
                    {
                        productId = p.ProductId,
                        name = p.Product.Name,
                        image = p.Product.ImageUrl,
                        originalPrice = p.OriginalPrice,
                        salePrice = p.SalePrice,
                        discountPercent = p.OriginalPrice > 0
                            ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                            : 0,
                        soldCount = p.SoldCount,
                        totalQuantity = p.Quantity,
                        remainingQuantity = p.RemainingQuantity
                    })
                });
            }

            return Ok(result);
        }

        /// <summary>GET /api/flashsale/{id} — Chi tiết 1 flash sale</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var now = DateTime.UtcNow;
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound();

            var products = await _db.FlashSaleProducts
                .Include(p => p.Product)
                .Where(p => p.FlashSaleId == id && p.IsActive)
                .ToListAsync();

            return Ok(new
            {
                id = sale.Id,
                name = sale.Name,
                startTime = sale.StartTime,
                endTime = sale.EndTime,
                isActive = sale.IsActive,
                secondsRemaining = sale.EndTime > now ? (int)(sale.EndTime - now).TotalSeconds : 0,
                products = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Product.Name,
                    image = p.Product.ImageUrl,
                    originalPrice = p.OriginalPrice,
                    salePrice = p.SalePrice,
                    discountPercent = p.OriginalPrice > 0
                        ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                        : 0,
                    soldCount = p.SoldCount,
                    totalQuantity = p.Quantity,
                    remainingQuantity = p.RemainingQuantity
                })
            });
        }

        /// <summary>POST /api/flashsale — Tạo flash sale (Admin)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateFlashSaleDto dto)
        {
            var sale = new FlashSale
            {
                Name = dto.Name,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetUserId()
            };
            _db.FlashSales.Add(sale);
            await _db.SaveChangesAsync();

            if (dto.Products != null)
            {
                foreach (var p in dto.Products)
                {
                    var product = await _db.Products.FindAsync(p.ProductId);
                    if (product == null) continue;

                    _db.FlashSaleProducts.Add(new FlashSaleProduct
                    {
                        FlashSaleId   = sale.Id,
                        ProductId     = p.ProductId,
                        SalePrice     = p.SalePrice,
                        OriginalPrice = product.Price,
                        Quantity      = p.Quantity,
                        SoldCount     = 0,
                        IsActive      = true
                    });
                }
                await _db.SaveChangesAsync();
            }

            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_CREATE", "FlashSale", sale.Id.ToString(),
                null, new { name = sale.Name, startTime = sale.StartTime, endTime = sale.EndTime });

            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, new { id = sale.Id });
        }

        /// <summary>GET /api/admin/flashsales — Danh sách tất cả flash sale (Admin)</summary>
        [HttpGet("admin/list")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminList([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            var now = DateTime.UtcNow;
            var total = await _db.FlashSales.CountAsync();
            var sales = await _db.FlashSales
                .OrderByDescending(fs => fs.StartTime)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var result = new List<object>();
            foreach (var fs in sales)
            {
                var productCount = await _db.FlashSaleProducts.CountAsync(p => p.FlashSaleId == fs.Id);
                var soldCount    = await _db.FlashSaleProducts.Where(p => p.FlashSaleId == fs.Id).SumAsync(p => p.SoldCount);
                string saleState = fs.StartTime > now ? "upcoming" : (fs.EndTime > now && fs.IsActive ? "active" : "ended");

                result.Add(new {
                    fs.Id, fs.Name, fs.StartTime, fs.EndTime, fs.IsActive,
                    productCount, soldCount, state = saleState
                });
            }

            return Ok(new { items = result, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>PUT /api/flashsale/{id} — Cập nhật flash sale (Admin)</summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateFlashSaleDto dto)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            if (!string.IsNullOrWhiteSpace(dto.Name)) sale.Name = dto.Name;
            if (dto.StartTime.HasValue) sale.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) sale.EndTime = dto.EndTime.Value;
            if (dto.IsActive.HasValue) sale.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_UPDATE", "FlashSale", id.ToString(),
                null, new { name = sale.Name, isActive = sale.IsActive });
            return Ok(new { message = "Đã cập nhật flash sale" });
        }

        /// <summary>DELETE /api/flashsale/{id} — Xóa flash sale (Admin)</summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_DELETE", "FlashSale", id.ToString(),
                new { name = sale.Name }, null);
            _db.FlashSales.Remove(sale);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa flash sale" });
        }

        /// <summary>PUT /api/flashsale/{id}/toggle — Bật/tắt flash sale (Admin)</summary>
        [HttpPut("{id:int}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Toggle(int id)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            sale.IsActive = !sale.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { message = sale.IsActive ? "Đã bật flash sale" : "Đã tắt flash sale", isActive = sale.IsActive });
        }

        /// <summary>
        /// POST /api/flashsale/buy — Mua flash sale (atomic, chống race condition)
        /// Body: { flashSaleProductId, quantity, shippingAddress, receiverName, receiverPhone }
        /// </summary>
        [HttpPost("buy")]
        [Authorize]
        public async Task<IActionResult> Buy([FromBody] BuyFlashSaleDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (dto.Quantity <= 0)
                return BadRequest(new { message = "Số lượng phải lớn hơn 0" });
            if (string.IsNullOrWhiteSpace(dto.ShippingAddress))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng" });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // ── BƯỚC 1: Kiểm tra flash sale còn hiệu lực ─────────
                var now = DateTime.UtcNow;
                var fsp = await _db.FlashSaleProducts
                    .Include(p => p.FlashSale)
                    .Include(p => p.Product)
                    .FirstOrDefaultAsync(p => p.Id == dto.FlashSaleProductId && p.IsActive);

                if (fsp == null)
                    return NotFound(new { message = "Không tìm thấy sản phẩm flash sale" });

                if (!fsp.FlashSale.IsActive || fsp.FlashSale.StartTime > now || fsp.FlashSale.EndTime <= now)
                    return BadRequest(new { message = "Flash sale chưa bắt đầu hoặc đã kết thúc" });

                if (fsp.Product == null || !fsp.Product.IsActive)
                    return BadRequest(new { message = "Sản phẩm không còn bán" });

                // ── BƯỚC 2: ATOMIC DECREMENT — chống race condition ──
                // Dùng SQL trực tiếp với điều kiện RemainingQuantity >= qty
                // Nếu rowsAffected = 0 → hết hàng (bị người khác mua trước)
                var rowsAffected = await _db.Database.ExecuteSqlRawAsync(
                    @"UPDATE FlashSaleProducts
                      SET RemainingQuantity = RemainingQuantity - @qty,
                          SoldCount         = SoldCount + @qty
                      WHERE Id = @fspId
                        AND RemainingQuantity >= @qty
                        AND IsActive = 1",
                    new SqlParameter("@qty",   dto.Quantity),
                    new SqlParameter("@fspId", fsp.Id));

                if (rowsAffected == 0)
                    return BadRequest(new { message = "Sản phẩm đã hết hàng hoặc không đủ số lượng" });

                // ── BƯỚC 3: Deduct Product.Stock ─────────────────────
                var product = fsp.Product;
                if (product.Stock < dto.Quantity)
                {
                    // Hoàn lại RemainingQuantity nếu stock kho không đủ
                    await _db.Database.ExecuteSqlRawAsync(
                        @"UPDATE FlashSaleProducts
                          SET RemainingQuantity = RemainingQuantity + @qty,
                              SoldCount         = SoldCount - @qty
                          WHERE Id = @fspId",
                        new SqlParameter("@qty",   dto.Quantity),
                        new SqlParameter("@fspId", fsp.Id));
                    return BadRequest(new { message = $"Kho chỉ còn {product.Stock} sản phẩm" });
                }

                product.Stock     -= dto.Quantity;
                product.SoldCount += dto.Quantity;

                // ── BƯỚC 4: Tạo Order ─────────────────────────────────
                var shop           = await _db.Shops.FirstOrDefaultAsync(s => s.Id == product.ShopId);
                decimal commRate   = shop?.CommissionRate > 0 ? shop!.CommissionRate : 10m;
                decimal salePrice  = fsp.SalePrice;
                decimal totalAmt   = salePrice * dto.Quantity;
                decimal productRev = totalAmt;
                decimal commAmt    = Math.Round(productRev * commRate / 100m, 2);
                decimal payout     = productRev - commAmt;

                var order = new Order {
                    UserId             = userId,
                    ShopId             = product.ShopId,
                    OrderDate          = DateTime.UtcNow,
                    TotalAmount        = totalAmt,
                    ShippingFee        = 0m,     // flash sale miễn phí ship
                    FinalAmount        = totalAmt,
                    Status             = OrderStatus.Pending,
                    PayoutStatus       = PayoutStatusValue.Pending,
                    CommissionRate     = commRate,
                    ProductRevenue     = productRev,
                    CommissionAmount   = commAmt,
                    SellerPayoutAmount = payout,
                    PaymentMethod      = "COD",
                    PaymentStatus      = "UNPAID",
                    ShippingAddress    = dto.ShippingAddress,
                    ReceiverName       = dto.ReceiverName ?? "",
                    ReceiverPhone      = dto.ReceiverPhone ?? "",
                    Note               = $"[Flash Sale] {fsp.FlashSale.Name}",
                    EstimatedDelivery  = DateTime.UtcNow.AddDays(3),
                    OrderDetails = new List<OrderDetail> {
                        new() { ProductId = product.Id, Quantity = dto.Quantity, UnitPrice = salePrice }
                    }
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                order.OrderCode = "FS-" + order.Id.ToString("D6");

                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = order.Id,
                    Status    = OrderStatus.Pending,
                    Note      = $"Đặt hàng Flash Sale: {fsp.FlashSale.Name}",
                    ChangedBy = userId,
                    ChangedAt = DateTime.UtcNow
                });

                // Notify seller
                if (shop != null)
                {
                    _db.Notifications.Add(new Notification {
                        UserId    = shop.SellerId,
                        Title     = "Đơn Flash Sale mới",
                        Message   = $"Đơn {order.OrderCode} - {totalAmt:N0}₫",
                        Link      = "/seller-dashboard.html",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                // Reload để lấy RemainingQuantity sau update
                await _db.Entry(fsp).ReloadAsync();

                return Ok(new {
                    message           = "Đặt hàng Flash Sale thành công!",
                    orderId           = order.Id,
                    orderCode         = order.OrderCode,
                    totalAmount       = totalAmt,
                    salePrice,
                    quantity          = dto.Quantity,
                    remainingQuantity = fsp.RemainingQuantity
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class CreateFlashSaleDto
    {
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<FlashSaleProductDto>? Products { get; set; }
    }

    public class FlashSaleProductDto
    {
        public int ProductId { get; set; }
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateFlashSaleDto
    {
        public string? Name { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool? IsActive { get; set; }
    }

    public class BuyFlashSaleDto
    {
        public int    FlashSaleProductId { get; set; }
        public int    Quantity           { get; set; } = 1;
        public string ShippingAddress    { get; set; } = "";
        public string? ReceiverName     { get; set; }
        public string? ReceiverPhone    { get; set; }
    }
}
