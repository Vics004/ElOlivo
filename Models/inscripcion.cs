using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class inscripcion
    {
        [Key]
        public int inscripcionid { get; set; }
        public DateTime? fecha_inscripcion { get; set; }
        public int? estadoid { get; set; }
        public string? comprobante_url { get; set; }
        public int? usuarioid { get; set; }
        public int? eventoid { get; set; }
    }
}

