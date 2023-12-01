using System.ComponentModel.DataAnnotations;

namespace Bongo.Models.ViewModels
{
    public class SecurityQuestionViewModel
    {
        [Required]
        public string SecurityQuestion { get; set; }

        [Required]
        public string SecurityAnswer { get; set; }
        public string SendingAction { get; set; }
    }
}
