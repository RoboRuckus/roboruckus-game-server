using System.ComponentModel.DataAnnotations;

namespace RoboRuckus.Models
{
    /// <summary>
    /// Data model for info a bot uses to describe itself to the server.
    /// </summary>
    public class botDescriptionModel
    {
        [Required]
        public string ip { get; set; }
        [Required]
        public string name { get; set; }
    }

    public class botNumberModel
    {
        [Required]
        public int bot { get; set; }
    }
}
