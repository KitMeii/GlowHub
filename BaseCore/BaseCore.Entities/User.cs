using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class User
    {
        [Key]
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Password { get; set; } = "";
        public byte[]? Salt { get; set; }
        public string Contact { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Position { get; set; } = "";
        public string Image { get; set; } = "";
        public bool IsActive { get; set; }
        public int UserType { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;

        // Tăng mỗi khi admin đổi role / ban user → JWT cũ có "tv" khác sẽ bị reject ở OnTokenValidated.
        public int TokenVersion { get; set; } = 0;

        /// <summary>GOOGLE | FACEBOOK | null (đăng nhập thường)</summary>
        [MaxLength(20)]
        public string? OAuthProvider { get; set; }

        /// <summary>User ID từ OAuth provider</summary>
        [MaxLength(200)]
        public string? OAuthId { get; set; }
    }
}
