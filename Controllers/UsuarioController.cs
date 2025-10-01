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
        public async Task<IActionResult> Inscripciones()
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                var inscripciones = await (from i in _elOlivoDbContext.inscripcion
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
                                               EstadoId = es.estadoid,
                                               /*Agregado eventoid para vista ver-mas*/
                                               e.eventoid
                                           }).ToListAsync();

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

        /*Ver_Mas*/
        public IActionResult Ver_Mas(int id)
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioId == null)
                return RedirectToAction("Autenticar", "Login");

            
            var inscripcion = _elOlivoDbContext.inscripcion
                .FirstOrDefault(i => i.eventoid == id && i.usuarioid == usuarioId);

            if (inscripcion == null)
                return RedirectToAction("Inscripciones");

            //Obtener estado para etiqueta
            var inscripcionConEstado = (from i in _elOlivoDbContext.inscripcion
                                        join est in _elOlivoDbContext.estado
                                            on i.estadoid equals est.estadoid
                                        where i.eventoid == id && i.usuarioid == usuarioId
                                        select new
                                        {
                                            inscripcion = i,
                                            estadoNombre = est.nombre
                                        }).FirstOrDefault();


            //Obtener información para mostrar las sesiones con sus actividades
            var evento = _elOlivoDbContext.evento.FirstOrDefault(e => e.eventoid == id);

            
            var sesiones = (from s in _elOlivoDbContext.sesion
                            where s.eventoid == id && s.activo == true
                            orderby s.fecha_inicio
                            select new
                            {
                                s.sesionid,
                                s.titulo,
                                s.descripcion,
                                s.fecha_inicio,
                                s.fecha_fin,
                                actividades = (from a in _elOlivoDbContext.actividad
                                               join u in _elOlivoDbContext.usuario on a.ponenteid equals u.usuarioid
                                               where a.sesionid == s.sesionid && a.activo == true
                                               orderby a.hora_inicio
                                               select new
                                               {
                                                   a.agendaid,
                                                   a.nombre,
                                                   a.descripcion,
                                                   a.hora_inicio,
                                                   a.hora_fin,
                                                   ponenteNombre = u.nombre + " " + u.apellido
                                               }).ToList()
                            }).ToList();

            ViewBag.Evento = evento;
            ViewBag.Sesiones = sesiones;
            ViewBag.InscripcionEstado =
                inscripcionConEstado == null
                ? "Cancelado"
                : (inscripcionConEstado.estadoNombre == "Inscrito" ? "Confirmada"
                : (inscripcionConEstado.estadoNombre == "Pendiente" ? "En proceso"
                : inscripcionConEstado.estadoNombre));
            return View();
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
