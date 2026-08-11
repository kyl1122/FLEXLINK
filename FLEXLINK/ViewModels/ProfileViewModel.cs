using System.ComponentModel.DataAnnotations;

namespace FLEXLINK.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone Number is required.")]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits and contain only numbers.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }

        // Optional — trainer may not fill this in
        public string? Expertise { get; set; }

        // Nullable — trainer may not have uploaded a picture yet
        public string? ExistingProfilePicture { get; set; }

        // Not required — trainer may skip uploading a new photo
        public IFormFile? ProfileImage { get; set; }
    }
}
