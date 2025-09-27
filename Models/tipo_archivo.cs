using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class tipo_archivo
    {
        [Key]
        public int tipo_archivoid { get; set; }
        public string? nombre { get; set; }
    }
}

