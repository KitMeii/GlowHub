using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class CustomerVoucher
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = "";

        public int VoucherId { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        public bool IsUsed { get; set; } = false;

        public User User { get; set; } = null!;

        public Voucher Voucher { get; set; } = null!;
    }
}
