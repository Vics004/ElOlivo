using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class rol
    {
        [Key]
        public int rolid { get; set; }
        public string? nombre { get; set; }
        public bool? activo { get; set; }
    }
}

