using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class evento
    {
        [Key]
        public int eventoid { get; set; }
        public string? nombre { get; set; }
        public string? descripcion { get; set; }
        public string? correo_encargado { get; set; }
        public DateTime? fecha_inicio { get; set; }
        public DateTime? fecha_fin { get; set; }
        public string? ubicacion_url { get; set; }
        public string? direccion { get; set; }
        public int? capacidad_maxima { get; set; }
        public int? usuarioadminid { get; set; }
        public int? estadoid { get; set; }
    }
}


