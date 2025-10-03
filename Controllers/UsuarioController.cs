using ElOlivo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ElOlivo.Servicios;
using static ElOlivo.Servicios.AutenticationAttribute;

namespace ElOlivo.Controllers
{
    [Autenticacion]   
    public class UsuarioController : Controller
    {
        private readonly ILogger<UsuarioController> _logger;
        private readonly ElOlivoDbContext _elOlivoDbContext;

        public UsuarioController(ILogger<UsuarioController> logger, ElOlivoDbContext elOlivoDbContext)
        {
            _elOlivoDbContext = elOlivoDbContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {

            return View();
        }
        public IActionResult Buscar_Enventos()
        {

            return View();
        }
        public IActionResult Ver_Sesiones()
        {

            return View();
        }

        // Lista las inscripciones del usuario logueado desde sesión
        public async Task<IActionResult> Inscripciones(string? search, int? estado, DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    return RedirectToAction("Inscripciones");
                }

                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                var query = from i in _elOlivoDbContext.inscripcion
                            join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                            join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                            where i.usuarioid == usuarioId
                            select new
                            {
                                i.inscripcionid,
                                nombre = e.nombre,
                                descripcion = e.descripcion,
                                fecha_inicio = e.fecha_inicio,
                                fecha_fin = e.fecha_fin,
                                EstadoNombre = es.nombre,
                                EstadoId = es.estadoid
                            };

                // ------------Filtros------------
                //buscar
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    query = query.Where(x =>
                        x.nombre.ToLower().Contains(searchLower) ||
                        x.descripcion.ToLower().Contains(searchLower));
                }

                // estado
                if (estado.HasValue)
                {
                    query = query.Where(x => x.EstadoId == estado.Value);
                }

                // fechas
                if (fechaInicio.HasValue)
                {
                    var inicioUtc = DateTime.SpecifyKind(fechaInicio.Value, DateTimeKind.Utc);
                    query = query.Where(x => x.fecha_inicio >= inicioUtc);
                }
                if (fechaFin.HasValue)
                {
                    var finUtc = DateTime.SpecifyKind(fechaFin.Value, DateTimeKind.Utc);
                    query = query.Where(x => x.fecha_fin <= finUtc);
                }

                var inscripciones = await query.ToListAsync();
                return View(inscripciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener inscripciones del usuario");
                return Content(ex.ToString());
            }
        }

        // Cancelar inscripción
        [HttpPost]
        public async Task<IActionResult> CancelarInscripcion(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                var inscripcion = await _elOlivoDbContext.inscripcion
                                    .FirstOrDefaultAsync(i => i.inscripcionid == id && i.usuarioid == usuarioId);

                if (inscripcion == null)
                {
                    _logger.LogWarning("Inscripción no encontrada o no pertenece al usuario: {Id}", id);
                    return NotFound();
                }

                inscripcion.estadoid = 1; // Cancelada
                await _elOlivoDbContext.SaveChangesAsync();

                _logger.LogInformation("Inscripción {Id} cancelada correctamente", id);
                return RedirectToAction(nameof(Inscripciones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar inscripción {Id}", id);
                return Content(ex.ToString());
            }
        }


        public IActionResult Certificados()
        {

            return View();
        }
        public IActionResult MiPerfil()
        {

            return View();
        }
        public IActionResult AcercaDe()
        {

            return View();
        }
    }
}
