using System.ComponentModel.DataAnnotations;

namespace ElOlivo.Models
{
    public class tipoactividad
    {
        [Key]
        public int tipoactividadoid { get; set; }
        public string? nombre { get; set; }
        public string? descripcion { get; set; }
        public bool? activo { get; set; }
    }
}

