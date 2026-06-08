using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace BaseCore.Services
{
    public class VNPayService
    {
        private readonly string _tmnCode;
        private readonly string _hashSecret;
        private readonly string _baseUrl;
        private readonly string _returnUrl;

        public VNPayService(IConfiguration config)
        {
            _tmnCode    = config["VNPay:TmnCode"]    ?? "VNPAYTEST";
            _hashSecret = config["VNPay:HashSecret"] ?? "";
            _baseUrl    = config["VNPay:BaseUrl"]    ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            _returnUrl  = config["VNPay:ReturnUrl"]  ?? "";
        }

        /// <summary>Tạo URL thanh toán VNPay</summary>
        public string CreatePaymentUrl(int orderId, decimal amount, string orderInfo, string ipAddr)
        {
            var now = DateTime.UtcNow.AddHours(7); // Vietnam time (UTC+7)

            var vnpParams = new SortedDictionary<string, string>
            {
                ["vnp_Version"]    = "2.1.0",
                ["vnp_Command"]    = "pay",
                ["vnp_TmnCode"]    = _tmnCode,
                ["vnp_Amount"]     = ((long)(amount * 100)).ToString(),
                ["vnp_CurrCode"]   = "VND",
                ["vnp_TxnRef"]     = orderId.ToString(),
                ["vnp_OrderInfo"]  = orderInfo,
                ["vnp_OrderType"]  = "other",
                ["vnp_Locale"]     = "vn",
                ["vnp_ReturnUrl"]  = _returnUrl,
                ["vnp_IpAddr"]     = ipAddr,
                ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
                ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
            };

            var queryStr = string.Join("&", vnpParams.Select(kv =>
                $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

            var signature = ComputeHmacSha512(_hashSecret, queryStr);
            return $"{_baseUrl}?{queryStr}&vnp_SecureHash={signature}";
        }

        /// <summary>Xác minh chữ ký IPN/Return từ VNPay (từ IQueryCollection)</summary>
        public bool VerifySignature(IQueryCollection query)
        {
            var secureHash = query["vnp_SecureHash"].ToString();
            if (string.IsNullOrEmpty(secureHash)) return false;

            var sorted = query
                .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value.ToString())}");

            var expected = ComputeHmacSha512(_hashSecret, string.Join("&", sorted));
            return string.Equals(expected, secureHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Xác minh chữ ký từ Dictionary (dùng cho IPN server-to-server)</summary>
        public bool VerifySignature(Dictionary<string, string> query)
        {
            if (!query.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrEmpty(secureHash))
                return false;

            var sorted = query
                .Where(kv => kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}");

            var expected = ComputeHmacSha512(_hashSecret, string.Join("&", sorted));
            return string.Equals(expected, secureHash, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeHmacSha512(string key, string message)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return Convert.ToHexString(hash).ToLower();
        }
    }
}
