using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public class EmployeeModel: BaseModel {

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
    }
}