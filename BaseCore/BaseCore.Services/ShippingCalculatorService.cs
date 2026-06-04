namespace BaseCore.Services
{
    public static class ShippingRegion
    {
        public const string North   = "NORTH";
        public const string Central = "CENTRAL";
        public const string South   = "SOUTH";
        public const string Island  = "ISLAND";
        public const string Other   = "OTHER";

        private static readonly HashSet<string> _validCodes = new(StringComparer.OrdinalIgnoreCase)
            { North, Central, South, Island, Other };

        // ── 63 tỉnh thành Việt Nam → 4 vùng ──────────────────────
        private static readonly Dictionary<string, string[]> _provinceMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // ── MIỀN BẮC (25 tỉnh/thành) ────────────────────────
            [North] = new[]
            {
                // Thủ đô + đồng bằng sông Hồng
                "Hà Nội", "Ha Noi", "Hanoi", "Thành phố Hà Nội",
                "Hải Phòng", "Hai Phong",
                "Quảng Ninh", "Quang Ninh",
                "Hải Dương", "Hai Duong",
                "Hưng Yên", "Hung Yen",
                "Thái Bình", "Thai Binh",
                "Nam Định", "Nam Dinh",
                "Hà Nam", "Ha Nam",
                "Ninh Bình", "Ninh Binh",
                "Vĩnh Phúc", "Vinh Phuc",
                "Bắc Ninh", "Bac Ninh",
                // Trung du + miền núi phía Bắc
                "Bắc Giang", "Bac Giang",
                "Thái Nguyên", "Thai Nguyen",
                "Lạng Sơn", "Lang Son",
                "Cao Bằng", "Cao Bang",
                "Bắc Kạn", "Bac Kan",
                "Tuyên Quang", "Tuyen Quang",
                "Hà Giang", "Ha Giang",
                "Lào Cai", "Lao Cai",
                "Yên Bái", "Yen Bai",
                "Phú Thọ", "Phu Tho",
                "Sơn La", "Son La",
                "Điện Biên", "Dien Bien",
                "Lai Châu", "Lai Chau",
                "Hòa Bình", "Hoa Binh",
            },

            // ── MIỀN TRUNG + TÂY NGUYÊN (19 tỉnh/thành) ────────
            [Central] = new[]
            {
                // Bắc Trung Bộ
                "Thanh Hóa", "Thanh Hoa",
                "Nghệ An", "Nghe An",
                "Hà Tĩnh", "Ha Tinh",
                "Quảng Bình", "Quang Binh",
                "Quảng Trị", "Quang Tri",
                "Thừa Thiên Huế", "Thua Thien Hue", "Thừa Thiên", "Thua Thien",
                "Huế", "Hue",
                // Duyên hải Nam Trung Bộ
                "Đà Nẵng", "Da Nang", "Danang",
                "Quảng Nam", "Quang Nam",
                "Quảng Ngãi", "Quang Ngai",
                "Bình Định", "Binh Dinh", "Quy Nhon", "Quy Nhơn",
                "Phú Yên", "Phu Yen",
                "Khánh Hòa", "Khanh Hoa", "Nha Trang",
                "Ninh Thuận", "Ninh Thuan", "Phan Rang",
                "Bình Thuận", "Binh Thuan", "Phan Thiet", "Phan Thiết",
                // Tây Nguyên
                "Kon Tum",
                "Gia Lai", "Pleiku",
                "Đắk Lắk", "Dak Lak", "Daklak", "Buon Ma Thuot", "Buôn Ma Thuột",
                "Đắk Nông", "Dak Nong",
                "Lâm Đồng", "Lam Dong", "Đà Lạt", "Da Lat", "Dalat",
            },

            // ── MIỀN NAM (19 tỉnh/thành) ─────────────────────────
            [South] = new[]
            {
                // TP.HCM + Đông Nam Bộ
                "Hồ Chí Minh", "Ho Chi Minh", "TP.HCM", "TP HCM", "TPHCM",
                "HCM", "Sài Gòn", "Saigon", "Sai Gon", "Thành phố Hồ Chí Minh",
                "Bình Dương", "Binh Duong",
                "Đồng Nai", "Dong Nai", "Bien Hoa", "Biên Hòa",
                "Bà Rịa", "Ba Ria", "Vũng Tàu", "Vung Tau", "Bà Rịa - Vũng Tàu",
                "Tây Ninh", "Tay Ninh",
                "Bình Phước", "Binh Phuoc",
                // Đồng bằng sông Cửu Long
                "Long An",
                "Tiền Giang", "Tien Giang", "My Tho", "Mỹ Tho",
                "Bến Tre", "Ben Tre",
                "Vĩnh Long", "Vinh Long",
                "Trà Vinh", "Tra Vinh",
                "Đồng Tháp", "Dong Thap", "Cao Lanh", "Cao Lãnh",
                "An Giang", "Long Xuyen", "Long Xuyên",
                "Kiên Giang", "Kien Giang", "Rạch Giá", "Rach Gia",
                "Cần Thơ", "Can Tho",
                "Hậu Giang", "Hau Giang", "Vi Thanh", "Vị Thanh",
                "Sóc Trăng", "Soc Trang",
                "Bạc Liêu", "Bac Lieu",
                "Cà Mau", "Ca Mau",
                // SOUTH vùng
                "South", "SOUTH", "Miền Nam",
            },

            // ── HẢI ĐẢO (đặc biệt) ───────────────────────────────
            [Island] = new[]
            {
                "Phú Quốc", "Phu Quoc",
                "Côn Đảo", "Con Dao",
                "Hoàng Sa", "Hoang Sa",
                "Trường Sa", "Truong Sa",
                "Lý Sơn", "Ly Son",
                "Cát Bà", "Cat Ba",
                "Cồn Cỏ", "Con Co",
                "ISLAND", "Island", "Đảo", "Dao",
            },
        };

        /// <summary>
        /// Chuyển tên tỉnh/thành (hoặc mã vùng đã lưu trong DB) → mã vùng chuẩn.
        /// Nếu input đã là mã vùng hợp lệ (NORTH/CENTRAL/SOUTH/ISLAND/OTHER) → trả về ngay.
        /// </summary>
        public static string Normalize(string? province)
        {
            if (string.IsNullOrWhiteSpace(province)) return Other;

            // Đã là mã vùng hợp lệ → trả về luôn (DB lưu sẵn)
            var trimmed = province.Trim();
            if (_validCodes.Contains(trimmed)) return trimmed.ToUpper();

            // Tìm theo alias
            foreach (var (region, aliases) in _provinceMap)
                if (aliases.Any(a => trimmed.Contains(a, StringComparison.OrdinalIgnoreCase)
                                  || a.Contains(trimmed, StringComparison.OrdinalIgnoreCase)))
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
        // ── Bảng phí theo cặp vùng (key = "FROM→TO") ────────────
        private static readonly Dictionary<string, ShippingRate> _rates = new()
        {
            // Nội vùng
            ["SOUTH→SOUTH"]     = new ShippingRate { BaseFee = 15_000m, ExtraFeePerBlock = 2_500m },
            ["NORTH→NORTH"]     = new ShippingRate { BaseFee = 15_000m, ExtraFeePerBlock = 2_500m },
            ["CENTRAL→CENTRAL"] = new ShippingRate { BaseFee = 18_000m, ExtraFeePerBlock = 3_000m },
            ["ISLAND→ISLAND"]   = new ShippingRate { BaseFee = 22_000m, ExtraFeePerBlock = 4_000m },

            // Bắc ↔ Trung
            ["NORTH→CENTRAL"]   = new ShippingRate { BaseFee = 20_000m, ExtraFeePerBlock = 4_000m },
            ["CENTRAL→NORTH"]   = new ShippingRate { BaseFee = 20_000m, ExtraFeePerBlock = 4_000m },

            // Nam ↔ Trung
            ["SOUTH→CENTRAL"]   = new ShippingRate { BaseFee = 22_000m, ExtraFeePerBlock = 4_000m },
            ["CENTRAL→SOUTH"]   = new ShippingRate { BaseFee = 22_000m, ExtraFeePerBlock = 4_000m },

            // Bắc ↔ Nam (đường dài nhất)
            ["NORTH→SOUTH"]     = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
            ["SOUTH→NORTH"]     = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },

            // Đảo ↔ đất liền (phí cao hơn)
            ["ISLAND→SOUTH"]    = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 6_000m },
            ["SOUTH→ISLAND"]    = new ShippingRate { BaseFee = 30_000m, ExtraFeePerBlock = 6_000m },
            ["ISLAND→CENTRAL"]  = new ShippingRate { BaseFee = 35_000m, ExtraFeePerBlock = 7_000m },
            ["CENTRAL→ISLAND"]  = new ShippingRate { BaseFee = 35_000m, ExtraFeePerBlock = 7_000m },
            ["ISLAND→NORTH"]    = new ShippingRate { BaseFee = 40_000m, ExtraFeePerBlock = 8_000m },
            ["NORTH→ISLAND"]    = new ShippingRate { BaseFee = 40_000m, ExtraFeePerBlock = 8_000m },

            // Fallback
            ["OTHER→OTHER"]     = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
        };

        // Phí mặc định khi không khớp rule nào (OTHER → vùng cụ thể)
        private static readonly Dictionary<string, ShippingRate> _otherRates = new()
        {
            ["NORTH"]   = new ShippingRate { BaseFee = 28_000m, ExtraFeePerBlock = 5_000m },
            ["CENTRAL"] = new ShippingRate { BaseFee = 25_000m, ExtraFeePerBlock = 5_000m },
            ["SOUTH"]   = new ShippingRate { BaseFee = 28_000m, ExtraFeePerBlock = 5_000m },
            ["ISLAND"]  = new ShippingRate { BaseFee = 38_000m, ExtraFeePerBlock = 7_000m },
        };

        private const int BaseWeightGram  = 1000; // 1 kg đầu tính BaseFee
        private const int BlockWeightGram = 500;  // mỗi block tiếp = 500g

        /// <summary>Tính phí ship dựa theo vùng + trọng lượng thực tế</summary>
        public ShippingFeeResult Calculate(string fromRegion, string toRegion, int weightGram)
        {
            fromRegion = (fromRegion?.ToUpper() ?? ShippingRegion.Other).Trim();
            toRegion   = (toRegion?.ToUpper()   ?? ShippingRegion.Other).Trim();
            if (weightGram <= 0) weightGram = 500;

            var key  = $"{fromRegion}→{toRegion}";
            ShippingRate rate;
            if (_rates.TryGetValue(key, out var r))
            {
                rate = r;
            }
            else if (fromRegion == ShippingRegion.Other && _otherRates.TryGetValue(toRegion, out var r2))
            {
                rate = r2;
            }
            else if (toRegion == ShippingRegion.Other && _otherRates.TryGetValue(fromRegion, out var r3))
            {
                rate = r3;
            }
            else
            {
                rate = _rates["OTHER→OTHER"];
            }

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
