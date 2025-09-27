using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class tipo_proceso
    {
        [Key]
        public int procesoid { get; set; }
        public string? nombre { get; set; }
    }
}

