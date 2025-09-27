using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class actividad
    {
        [Key]
        public int agendaid { get; set; }
        public string? nombre { get; set; }
        public string? descripcion { get; set; }
        public string? ponente { get; set; }
        public bool? activo { get; set; }
        public DateTime? hora_inicio { get; set; }
        public DateTime? hora_fin { get; set; }
        public int? sesionid { get; set; }
        public int? tipoactividadid { get; set; }
    }
}


