using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public sealed class SiteRoleModel : IValidatableObject {
        public int Id { get; set; }
        [Required, RegularExpression(@"^[A-Za-z][A-Za-z0-9 _-]{0,39}$", ErrorMessage = "Use 1–40 letters, numbers, spaces, underscores or hyphens; start with a letter.")]
        public string Name { get; set; }
        [StringLength(32, MinimumLength = 8, ErrorMessage = "Use 8–32 characters."), DataType(DataType.Password)]
        [RegularExpression(@"\A(?=[\s\S]*[A-Z])(?=[\s\S]*[a-z])(?=[\s\S]*[0-9])(?=[\s\S]*[!-/:-@\[-`{-~])[^\s\u0085\uFEFF]+\z", ErrorMessage = "Include uppercase and lowercase letters, a number and a symbol, with no whitespace.")]
        public string Password { get; set; }
        [DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
        public string[] Permissions { get; set; } = new string[0];
        public string PasswordHash { get; set; }
        public string Revision { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            if (Id == 0 && string.IsNullOrEmpty(Password))
                yield return new ValidationResult("A password is required for a new role.", new[] { "Password" });
        }
    }
}
