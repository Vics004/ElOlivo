using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class estado
    {
        [Key]
        public int estadoid { get; set; }
        public bool? activo { get; set; }
        public string? nombre { get; set; }
        public int? procesoid { get; set; }
    }
}

