using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GTX.Models {
    public class ContactModel: BaseModel { 

        public int EmployerId { get; set; }

        [DisplayName("First Name")]
        [Required(ErrorMessage = "Please enter your first name")]

        public string FirstName { get; set; }

        [Required(ErrorMessage = "Please enter your last name")]
        [DisplayName("Last Name")]
        public string LastName { get; set; }

        [DisplayName("Phone#")]
        [DataType(DataType.PhoneNumber)]
        [Required(ErrorMessage = "Please enter your phone number")]
        [RegularExpression(Extensions.RegexPattern.PHONE_NUMBER, ErrorMessage = "Phone number must be in the format (XXX) XXX-XXXX")]
        public string Phone { get; set; }

        [DisplayName("Email Address")]
        [DataType(DataType.EmailAddress)]
        [RegularExpression(Extensions.RegexPattern.EMAIL, ErrorMessage = "Please enter appropriate email address")]
        [Required(ErrorMessage = "Email address reqiured")]
        public string Email { get; set; }

        [DisplayName("Comment")]
        [Required(ErrorMessage = "Please enter comment")]
        public string Comment { get; set; }

        [Display(Name = "Preferred contact date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public String PreferredDate { get; set; }

        public String PreferredTime { get; set; }
    }
}