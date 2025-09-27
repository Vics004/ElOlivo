using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class usuario
    {
        [Key]
        public int usuarioid { get; set; }
        public string? nombre { get; set; }
        public string? apellido { get; set; }
        public string? email { get; set; }
        public string? contrasena { get; set; }
        public string? telefono { get; set; }
        public string? institucion { get; set; }
        public string? pais { get; set; }
        public string? foto_url { get; set; }
        public bool? activo { get; set; }
        public DateTime? fecha_registro { get; set; } = DateTime.UtcNow;
        public int? rolid { get; set; }
    }
}

