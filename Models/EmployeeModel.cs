using Services;
using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public class EmployeeModel {

        public int Id { get; set; }

        [Required, Display(Name = "First Name"), StringLength(50)]
        public string FirstName { get; set; }

        [Required, Display(Name = "Last Name"), StringLength(50)]
        public string LastName { get; set; }

        [Required, StringLength(50)]
        public string Position { get; set; }

        [Required, StringLength(10)]
        public string Phone { get; set; }

        [Required, StringLength(50)]
        public string Email { get; set; }

        [StringLength(255)]
        public string PhotoPath { get; set; }

        public bool Active { get; set; }

        public static Employee ToEntity(EmployeeModel model)
        {
            if (model == null) return null;

            return new Employee
            {
                Id = model.Id,
                FirstName = model.FirstName?.Trim(),
                LastName = model.LastName?.Trim(),
                Position = model.Position?.Trim(),
                Phone = model.Phone?.Trim(),
                Email = model.Email?.Trim(),
                PhotoPath = string.IsNullOrWhiteSpace(model.PhotoPath) ? null : model.PhotoPath.Trim(),
                Active = model.Active
            };
        }

        public static EmployeeModel FromEntity(Employee entity)
        {
            if (entity == null) return null;

            return new EmployeeModel
            {
                Id = entity.Id,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                Position = entity.Position,
                Phone = entity.Phone,
                Email = entity.Email,
                PhotoPath = entity.PhotoPath,
                Active = entity.Active
            };
        }
    }
}