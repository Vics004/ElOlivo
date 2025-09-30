using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class sesion
    {
        [Key]
        public int sesionid { get; set; }
        public string? titulo { get; set; }
        public string? descripcion { get; set; }
        public DateTime? fecha_inicio { get; set; }
        public DateTime? fecha_fin { get; set; }
        public string? ubicacion { get; set; }
        public string? tipo_sesion { get; set; }
        public int? capacidad { get; set; }
        public int moderadorid { get; set; }
        public bool? activo { get; set; }
        public int? eventoid { get; set; }
    }
}


