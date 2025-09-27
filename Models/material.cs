using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class material
    {
        [Key]
        public int materialid { get; set; }
        public string? nombre { get; set; }
        public int? tipo_archivoid { get; set; }
        public string? url_archivo { get; set; }
        public DateTime? fecha_subida { get; set; }
        public bool? publico { get; set; }
        public int? agendaid { get; set; }
    }
}

