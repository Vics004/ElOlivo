using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class certificado
    {
        [Key]
        public int certificadoid { get; set; }
        public string? codigo_unico { get; set; }
        public DateTime? fecha_emision { get; set; }
        public int? estadoid { get; set; }
        public int? usuarioid { get; set; }
        public int? sesionid { get; set; }
    }
}

