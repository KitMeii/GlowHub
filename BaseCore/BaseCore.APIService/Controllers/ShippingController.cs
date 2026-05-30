using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/shipping")]
    [ApiController]
    public class ShippingController : ControllerBase
    {
        private readonly ShippingCalculatorService _shipping;
        private readonly MySqlDbContext _db;

        public ShippingController(ShippingCalculatorService shipping, MySqlDbContext db)
        {
            _shipping = shipping;
            _db       = db;
        }

        /// <summary>
        /// GET /api/shipping/calculate?fromRegion=HCM&amp;toRegion=OTHER&amp;weightGram=1500
        /// Tính phí ship theo vùng + trọng lượng
        /// </summary>
        [HttpGet("calculate")]
        public IActionResult Calculate(
            [FromQuery] string fromRegion  = "OTHER",
            [FromQuery] string toRegion    = "OTHER",
            [FromQuery] int    weightGram  = 500)
        {
            if (weightGram <= 0)
                return BadRequest(new { message = "weightGram phải > 0" });

            var result = _shipping.Calculate(fromRegion, toRegion, weightGram);

            return Ok(new {
                fee         = result.Fee,
                baseFee     = result.BaseFee,
                extraFee    = result.ExtraFee,
                weightGram  = result.WeightGram,
                extraBlocks = result.ExtraBlocks,
                fromRegion  = result.FromRegion,
                toRegion    = result.ToRegion
            });
        }

        /// <summary>
        /// POST /api/shipping/calculate-cart
        /// Body: { fromShopId: string, toProvince: string, items: [{productId, qty}] }
        /// Tính phí ship thực tế từ shop → địa chỉ khách
        /// </summary>
        [HttpPost("calculate-cart")]
        public async Task<IActionResult> CalculateCart([FromBody] CalcCartDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return BadRequest(new { message = "Danh sách sản phẩm không được trống" });

            // Lấy region của shop
            string fromRegion = ShippingRegion.Other;
            if (!string.IsNullOrEmpty(dto.FromShopId))
            {
                var shop = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.FromShopId);
                if (shop?.Region != null)
                    fromRegion = ShippingRegion.Normalize(shop.Region);
                else if (shop?.Province != null)
                    fromRegion = ShippingRegion.Normalize(shop.Province);
            }

            // Region của khách hàng từ province string
            string toRegion = ShippingRegion.Normalize(dto.ToProvince);

            // Lấy trọng lượng sản phẩm từ DB
            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
            var products   = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.WeightGram })
                .ToListAsync();

            int totalWeight = dto.Items.Sum(item => {
                var prod = products.FirstOrDefault(p => p.Id == item.ProductId);
                return (prod?.WeightGram ?? 500) * item.Qty;
            });

            var result = _shipping.Calculate(fromRegion, toRegion, totalWeight);

            return Ok(new {
                fee         = result.Fee,
                baseFee     = result.BaseFee,
                extraFee    = result.ExtraFee,
                totalWeight,
                fromRegion  = result.FromRegion,
                toRegion    = result.ToRegion
            });
        }

        /// <summary>GET /api/shipping/regions — Danh sách vùng hợp lệ</summary>
        [HttpGet("regions")]
        public IActionResult GetRegions()
        {
            return Ok(new[] {
                new { code = ShippingRegion.HCM,   label = "TP. Hồ Chí Minh" },
                new { code = ShippingRegion.HN,    label = "Hà Nội" },
                new { code = ShippingRegion.Other, label = "Tỉnh/Thành khác" }
            });
        }
    }

    public class CalcCartDto
    {
        public string?  FromShopId { get; set; }
        public string?  ToProvince { get; set; }
        public List<CartItemWeightDto> Items { get; set; } = new();
    }

    public class CartItemWeightDto
    {
        public int ProductId { get; set; }
        public int Qty       { get; set; } = 1;
    }
}
