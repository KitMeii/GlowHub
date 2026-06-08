using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/reviews")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _context;
        public AdminReviewsController(MySqlDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var reviews = await (from r in _context.Reviews
                                 join p in _context.Products on r.ProductId equals p.Id
                                 join u in _context.Users on r.UserId equals u.Id
                                 orderby r.CreatedAt descending
                                 select new
                                 {
                                     r.Id,
                                     ProductId = p.Id,
                                     ProductName = p.Name,
                                     UserId = u.Id,
                                     UserName = u.Name,
                                     r.Rating,
                                     r.Comment,
                                     r.CreatedAt
                                 }).ToListAsync();
            return Ok(reviews);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
