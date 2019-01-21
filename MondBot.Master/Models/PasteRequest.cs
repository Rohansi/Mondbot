using System.ComponentModel.DataAnnotations;

namespace MondBot.Master.Models
{
    public class PasteRequest
    {
        [Required]
        public string Content { get; set; }
    }
}
