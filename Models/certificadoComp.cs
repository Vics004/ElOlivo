namespace ElOlivo.Models
{
    public class certificadoComp
    {
        public int CertificadoId { get; set; }
        public string CodigoUnico { get; set; }
        public DateTime? FechaEmision { get; set; }
        public string Estado { get; set; }
        public string NombreUsuario { get; set; }
        public string EmailUsuario { get; set; }
        public string Institucion { get; set; }
        public string EventoNombre { get; set; }
        public string SesionTitulo { get; set; }
        public string SesionDescripcion { get; set; }
        public DateTime? FechaInicioSesion { get; set; }
        public DateTime? FechaFinSesion { get; set; }
    }
}
