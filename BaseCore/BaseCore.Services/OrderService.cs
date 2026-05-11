using MongoDB.Driver;
using BaseCore.Entities;
using BaseCore.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BaseCore.Services
{
    public class OrderService : IOrderService
    {
        private readonly MongoDbContext _context;

        public OrderService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            // Get next ID
            var maxOrder = await _context.Orders
                .Find(_ => true)
                .SortByDescending(o => o.Id)
                .FirstOrDefaultAsync();
            order.Id = (maxOrder?.Id ?? 0) + 1;

            order.OrderDate = DateTime.UtcNow;
            order.Status = "Pending";

            await _context.Orders.InsertOneAsync(order);
            return order;
        }

        public async Task<List<Order>> GetOrdersByUserIdAsync(string userId)
        {
            var orders = await _context.Orders
                .Find(o => o.UserId == userId)
                .SortByDescending(o => o.OrderDate)
                .ToListAsync();

            // Load order details and products
            foreach (var order in orders)
            {
                if (order.OrderDetails != null)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        detail.Product = await _context.Products
                            .Find(p => p.Id == detail.ProductId)
                            .FirstOrDefaultAsync();
                    }
                }
            }

            return orders;
        }

        public async Task<Order> GetOrderByIdAsync(int id)
        {
            var order = await _context.Orders
                .Find(o => o.Id == id)
                .FirstOrDefaultAsync();

            if (order?.OrderDetails != null)
            {
                foreach (var detail in order.OrderDetails)
                {
                    detail.Product = await _context.Products
                        .Find(p => p.Id == detail.ProductId)
                        .FirstOrDefaultAsync();
                }
            }

            return order;
        }
    }
}
