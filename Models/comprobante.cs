namespace ElOlivo.Models
{
    public class comprobante
    {
        public int InscripcionId { get; set; }
        public string NombreEvento { get; set; }
        public string DescripcionEvento { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public DateTime? FechaInscripcion { get; set; }
        public string NombreUsuario { get; set; }
        public string EmailUsuario { get; set; }
        public string Institucion { get; set; }
        public string CodigoComprobante { get; set; }
        public string Estado { get; set; }
    }
}
