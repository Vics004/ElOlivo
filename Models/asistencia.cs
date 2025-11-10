using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class asistencia
    {
        [Key]
        
        public int asistenciaid { get; set; }
        public DateTime? fecha_hora_registro { get; set; }
        public int? usuarioid { get; set; }
        public int? sesionid { get; set; }
    }
}

