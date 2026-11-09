using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public sealed class SiteRoleModel {
        public int Id { get; set; }
        [Required, RegularExpression(@"^[A-Za-z][A-Za-z0-9 _-]{0,39}$", ErrorMessage = "Use 1–40 letters, numbers, spaces, underscores or hyphens; start with a letter.")]
        public string Name { get; set; }
        [StringLength(128, MinimumLength = 6), DataType(DataType.Password)]
        public string Password { get; set; }
        public string[] Permissions { get; set; } = new string[0];
        public string PasswordHash { get; set; }
        public string Revision { get; set; }
    }
}
