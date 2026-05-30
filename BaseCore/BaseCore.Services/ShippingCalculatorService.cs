namespace BaseCore.Services
{
    public static class ShippingRegion
    {
        public const string HCM   = "HCM";
        public const string HN    = "HN";
        public const string Other = "OTHER";

        private static readonly Dictionary<string, string[]> _provinceMap = new(StringComparer.OrdinalIgnoreCase)
        {
            [HCM] = new[] { "Hồ Chí Minh", "Ho Chi Minh", "TP.HCM", "TP HCM", "Thành phố Hồ Chí Minh" },
            [HN]  = new[] { "Hà Nội", "Ha Noi", "Hanoi", "Thành phố Hà Nội" }
        };

        public static string Normalize(string? province)
        {
            if (string.IsNullOrWhiteSpace(province)) return Other;
            foreach (var (region, aliases) in _provinceMap)
                if (aliases.Any(a => province.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    return region;
            return Other;
        }
    }

    /// <summary>Bảng phí vận chuyển theo vùng + khối lượng</summary>
    public class ShippingRate
    {
        /// <summary>Phí cơ bản cho gói ≤ 1 kg (1000g)</summary>
        public decimal BaseFee { get; init; }
        /// <summary>Phí thêm cho mỗi 500g (hoặc phần lẻ) vượt 1 kg đầu tiên</summary>
        public decimal ExtraFeePerBlock { get; init; }
    }

    public class ShippingFeeResult
    {
        public decimal Fee          { get; init; }
        public decimal BaseFee      { get; init; }
        public decimal ExtraFee     { get; init; }
        public int     WeightGram   { get; init; }
        public int     ExtraBlocks  { get; init; }
        public string  FromRegion   { get; init; } = "";
        public string  ToRegion     { get; init; } = "";
    }

    public class ShippingCalculatorService
    {
        // ── Bảng phí: key = "FROM→TO" ──────────────────────────
        private static readonly Dictionary<string, ShippingRate> _rates = new()
        {
            // Nội thành cùng vùng
            ["HCM→HCM"] = new ShippingRate { BaseFee = 15_000m, ExtraFeePerBlock = 2_500m },
            ["HN→HN"]   = new ShippingRate { BaseFee = 15_000m, ExtraFeePerBlock = 2_500m },
            // Hai đầu HCM ↔ HN
            ["HCM→HN"]  = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
            ["HN→HCM"]  = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
            // Tỉnh lẻ xuất phát HCM/HN
            ["HCM→OTHER"] = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 5_000m },
            ["HN→OTHER"]  = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 5_000m },
            // Từ tỉnh lẻ → các vùng
            ["OTHER→HCM"] = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 5_000m },
            ["OTHER→HN"]  = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 5_000m },
            ["OTHER→OTHER"] = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
        };

        private const int BaseWeightGram  = 1000; // 1 kg đầu tính BaseFee
        private const int BlockWeightGram = 500;  // mỗi block tiếp = 500g

        /// <summary>Tính phí ship dựa theo vùng + trọng lượng thực tế</summary>
        public ShippingFeeResult Calculate(string fromRegion, string toRegion, int weightGram)
        {
            fromRegion = fromRegion?.ToUpper() ?? ShippingRegion.Other;
            toRegion   = toRegion?.ToUpper()   ?? ShippingRegion.Other;
            if (weightGram <= 0) weightGram = 500;

            var key  = $"{fromRegion}→{toRegion}";
            var rate = _rates.TryGetValue(key, out var r) ? r : _rates["OTHER→OTHER"];

            var extraGram   = Math.Max(0, weightGram - BaseWeightGram);
            var extraBlocks = (int)Math.Ceiling((double)extraGram / BlockWeightGram);
            var extraFee    = extraBlocks * rate.ExtraFeePerBlock;
            var totalFee    = rate.BaseFee + extraFee;

            return new ShippingFeeResult
            {
                Fee         = totalFee,
                BaseFee     = rate.BaseFee,
                ExtraFee    = extraFee,
                WeightGram  = weightGram,
                ExtraBlocks = extraBlocks,
                FromRegion  = fromRegion,
                ToRegion    = toRegion
            };
        }

        /// <summary>Tính tổng trọng lượng đơn hàng từ danh sách (productId → weightGram, qty)</summary>
        public static int TotalWeight(IEnumerable<(int weightGram, int qty)> items)
            => items.Sum(i => i.weightGram * i.qty);
    }
}
