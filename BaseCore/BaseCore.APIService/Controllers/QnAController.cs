using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using BaseCore.Services;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/qna")]
    [ApiController]
    public class QnAController : ControllerBase
    {
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;
        private readonly NotificationService _notificationService;

        public QnAController(IShopRepositoryEF shopRepository, MySqlDbContext db, NotificationService notificationService)
        {
            _shopRepository      = shopRepository;
            _db                  = db;
            _notificationService = notificationService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var shop = await _shopRepository.GetBySellerIdAsync(GetUserId()!);
            if (shop == null) return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active) return (null, BadRequest(new { message = "Shop chưa được duyệt" }));
            return (shop, null);
        }

        // GET /api/qna/shop?page=1&limit=10&answered=false
        [HttpGet("shop")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetShopQuestions(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] bool? answered = null)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var query = _db.QnAs
                .Include(q => q.Product)
                .Where(q => productIds.Contains(q.ProductId) && q.IsActive);

            if (answered == true)  query = query.Where(q => q.Answer != null);
            if (answered == false) query = query.Where(q => q.Answer == null);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(q => q.AskedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(q => new {
                    questionId   = q.Id,
                    productName  = q.Product != null ? q.Product.Name  : "",
                    productImage = q.Product != null ? q.Product.ImageUrl : "",
                    customerName = q.CustomerId,
                    q.Question,
                    askedAt    = q.AskedAt,
                    q.Answer,
                    answeredAt = q.AnsweredAt
                })
                .ToListAsync();

            return Ok(new {
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                items
            });
        }

        // POST /api/qna/{questionId}/answer
        [HttpPost("{questionId:int}/answer")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> Answer(int questionId, [FromBody] AnswerQnADto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Answer))
                return BadRequest(new { message = "Câu trả lời không được rỗng" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var qna = await _db.QnAs.FirstOrDefaultAsync(q => q.Id == questionId && productIds.Contains(q.ProductId));
            if (qna == null) return NotFound(new { message = "Câu hỏi không tồn tại hoặc không thuộc shop của bạn" });

            qna.Answer     = dto.Answer.Trim();
            qna.AnsweredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Câu trả lời đã được gửi" });
        }

        // GET /api/qna/customer/{productId}  — Public (paged)
        [HttpGet("customer/{productId:int}")]
        [HttpGet("product/{productId:int}")]
        public async Task<IActionResult> GetProductQnA(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 5)
        {
            var query = _db.QnAs
                .Include(q => q.Customer)
                .Where(q => q.ProductId == productId && q.IsActive)
                .AsQueryable();

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(q => q.AskedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(q => new {
                    q.Id,
                    customerName = q.Customer != null ? (q.Customer.Name ?? q.Customer.UserName) : q.CustomerId,
                    q.Question,
                    askedAt    = q.AskedAt,
                    q.Answer,
                    answeredAt = q.AnsweredAt
                })
                .ToListAsync();

            return Ok(new {
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                items
            });
        }

        // POST /api/qna/customer/ask  — Requires login
        [HttpPost("customer/ask")]
        [HttpPost("ask")]
        [Authorize]
        public async Task<IActionResult> Ask([FromBody] AskQnADto dto)
        {
            if (dto.ProductId <= 0) return BadRequest(new { message = "ProductId không hợp lệ" });
            if (string.IsNullOrWhiteSpace(dto.Question)) return BadRequest(new { message = "Câu hỏi không được rỗng" });

            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại" });

            var customerId = GetUserId()!;
            var qna = new QnA
            {
                ProductId  = dto.ProductId,
                CustomerId = customerId,
                Question   = dto.Question.Trim(),
                AskedAt    = DateTime.UtcNow
            };
            _db.QnAs.Add(qna);
            await _db.SaveChangesAsync();

            // Notify seller
            if (product.ShopId != null)
            {
                var shop = await _db.Shops.FindAsync(product.ShopId);
                if (shop != null)
                {
                    await _notificationService.CreateAsync(
                        shop.SellerId,
                        NotificationType.NewQuestion,
                        "Câu hỏi mới về sản phẩm",
                        $"Khách hàng hỏi về \"{product.Name}\"",
                        "seller-dashboard.html#qna"
                    );
                }
            }

            return Ok(new { message = "Câu hỏi đã được gửi", id = qna.Id });
        }

        // GET /api/qna/shop/stats
        [HttpGet("shop/stats")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetStats()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var allQnA = await _db.QnAs
                .Where(q => productIds.Contains(q.ProductId) && q.IsActive)
                .ToListAsync();

            var answered = allQnA.Where(q => q.Answer != null).ToList();
            var avgResponseTime = 0.0;
            if (answered.Any())
            {
                avgResponseTime = answered
                    .Where(q => q.AnsweredAt.HasValue)
                    .Select(q => (q.AnsweredAt!.Value - q.AskedAt).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average();
            }

            return Ok(new {
                totalQuestions  = allQnA.Count,
                answeredCount   = answered.Count,
                pendingCount    = allQnA.Count - answered.Count,
                avgResponseTime = Math.Round(avgResponseTime, 1)
            });
        }
    }

    public class AnswerQnADto { public string Answer { get; set; } = ""; }

    public class AskQnADto
    {
        public int ProductId { get; set; }
        public string Question { get; set; } = "";
    }
}
