using Microsoft.EntityFrameworkCore;


namespace ElOlivo.Models
{
    public class ElOlivoDbContext : DbContext
    {
        public ElOlivoDbContext(DbContextOptions options) : base(options){

        }

        public DbSet<usuario> usuario { get; set; }
        public DbSet<evento> evento { get; set; }
        public DbSet<sesion> sesion { get; set; }
        public DbSet<actividad> actividad { get; set; }
        public DbSet<inscripcion> inscripcion { get; set; }
        public DbSet<certificado> certificado { get; set; }
        public DbSet<asistencia> asistencia { get; set; }
        public DbSet<material> material { get; set; }
        public DbSet<estado> estado { get; set; }
        public DbSet<tipo_proceso> tipo_proceso { get; set; }
        public DbSet<tipoactividad> tipoactividad { get; set; }
        public DbSet<tipo_archivo> tipo_archivo { get; set; }
        public DbSet<rol> rol { get; set; }
    }
}
