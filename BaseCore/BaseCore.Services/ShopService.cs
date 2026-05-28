using BaseCore.Entities;
using BaseCore.Repository.EFCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaseCore.Services
{
    public interface IShopService
    {
        Task<Shop?> GetByIdAsync(string id);
        Task<Shop?> GetBySellerIdAsync(string sellerId);
        Task<Shop> RegisterAsync(string sellerId, string shopName, string description, string logo, string address, string phone);
        Task UpdateAsync(Shop shop);
        Task<(List<Shop> Shops, int TotalCount)> GetAllPagedAsync(int page, int pageSize);
        Task ApproveAsync(string shopId);
        Task BanAsync(string shopId);
        Task AddProductAsync(string shopId, int productId);
        Task RemoveProductAsync(string shopId, int productId);
    }

    public class ShopService : IShopService
    {
        private readonly IShopRepositoryEF _repo;

        public ShopService(IShopRepositoryEF repo)
        {
            _repo = repo;
        }

        public async Task<Shop?> GetByIdAsync(string id)
            => await _repo.GetByIdAsync(id);

        public async Task<Shop?> GetBySellerIdAsync(string sellerId)
            => await _repo.GetBySellerIdAsync(sellerId);

        public async Task<Shop> RegisterAsync(string sellerId, string shopName, string description, string logo, string address, string phone)
        {
            var existing = await _repo.GetBySellerIdAsync(sellerId);
            if (existing != null)
                throw new InvalidOperationException("Seller already has a shop.");

            var shop = new Shop
            {
                Id = Guid.NewGuid().ToString(),
                SellerId = sellerId,
                ShopName = shopName,
                Description = description ?? "",
                Logo = logo ?? "",
                Address = address ?? "",
                Phone = phone ?? "",
                Status = ShopStatus.Pending,
                CommissionRate = 10m,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.AddAsync(shop);
            return shop;
        }

        public async Task UpdateAsync(Shop shop)
        {
            shop.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(shop);
        }

        public async Task<(List<Shop> Shops, int TotalCount)> GetAllPagedAsync(int page, int pageSize)
            => await _repo.GetAllPagedAsync(page, pageSize);

        public async Task ApproveAsync(string shopId)
        {
            var shop = await _repo.GetByIdAsync(shopId)
                ?? throw new KeyNotFoundException("Shop not found.");
            shop.Status = ShopStatus.Active;
            shop.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(shop);
        }

        public async Task BanAsync(string shopId)
        {
            var shop = await _repo.GetByIdAsync(shopId)
                ?? throw new KeyNotFoundException("Shop not found.");
            shop.Status = ShopStatus.Banned;
            shop.UpdatedAt = DateTime.UtcNow;
            await _repo.UpdateAsync(shop);
        }

        public async Task AddProductAsync(string shopId, int productId)
        {
            var sp = new ShopProduct
            {
                ShopId = shopId,
                ProductId = productId,
                AddedAt = DateTime.UtcNow
            };
            await _repo.AddProductAsync(sp);
        }

        public async Task RemoveProductAsync(string shopId, int productId)
            => await _repo.RemoveProductAsync(shopId, productId);
    }
}
