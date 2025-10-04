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

        public async Task<IActionResult> Dashboard(string? search)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Paso 1: Obtener las inscripciones confirmadas del usuario
                var inscripciones = await (from i in _elOlivoDbContext.inscripcion
                                           join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                           join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                           where i.usuarioid == usuarioId && i.estadoid == 2 // 2 = Confirmada
                                           select new
                                           {
                                               EventoId = e.eventoid,
                                               EventoNombre = e.nombre,
                                               EventoDescripcion = e.descripcion,
                                               InscripcionEstado = es.nombre
                                           }).ToListAsync();

                if (!inscripciones.Any())
                {
                    return View(new List<dynamic>());
                }

                var eventosIds = inscripciones.Select(i => i.EventoId).ToList();

                // Paso 2: Obtener las sesiones de los eventos en los que está inscrito
                var sesionesQuery = from s in _elOlivoDbContext.sesion
                                    join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                    where eventosIds.Contains(e.eventoid) && s.activo == true
                                    select new
                                    {
                                        SesionId = s.sesionid,
                                        SesionTitulo = s.titulo,
                                        SesionDescripcion = s.descripcion,
                                        FechaInicio = s.fecha_inicio,
                                        FechaFin = s.fecha_fin,
                                        Ubicacion = s.ubicacion,
                                        TipoSesion = s.tipo_sesion,
                                        Capacidad = s.capacidad,
                                        EventoId = e.eventoid,
                                        EventoNombre = e.nombre,
                                        EventoDescripcion = e.descripcion
                                    };

                // Filtro de búsqueda
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    sesionesQuery = sesionesQuery.Where(x =>
                        x.SesionTitulo.ToLower().Contains(searchLower) ||
                        x.EventoNombre.ToLower().Contains(searchLower));
                }

                var sesiones = await sesionesQuery.ToListAsync();

                // Paso 3: Combinar la información en memoria
                var eventosConSesiones = sesiones
                    .GroupBy(x => new { x.EventoId, x.EventoNombre, x.EventoDescripcion })
                    .Select(g => new
                    {
                        Evento = new
                        {
                            eventoid = g.Key.EventoId,
                            nombre = g.Key.EventoNombre,
                            descripcion = g.Key.EventoDescripcion
                        },
                        Sesiones = g.Select(s => new
                        {
                            sesionid = s.SesionId,
                            titulo = s.SesionTitulo,
                            descripcion = s.SesionDescripcion,
                            fecha_inicio = s.FechaInicio,
                            fecha_fin = s.FechaFin,
                            ubicacion = s.Ubicacion,
                            tipo_sesion = s.TipoSesion,
                            capacidad = s.Capacidad,
                            InscripcionEstado = inscripciones.First(i => i.EventoId == s.EventoId).InscripcionEstado
                        }).ToList()
                    }).ToList();

                return View(eventosConSesiones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener dashboard del usuario");
                return Content(ex.ToString());
            }
        }


        public async Task<IActionResult> Buscar_Enventos(string? search)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    return RedirectToAction("Buscar_Enventos");
                }

                var query = from e in _elOlivoDbContext.evento
                            join es in _elOlivoDbContext.estado on e.estadoid equals es.estadoid
                            select new
                            {
                                e.eventoid,
                                e.nombre,
                                e.descripcion,
                                e.fecha_inicio,
                                e.fecha_fin,
                                EstadoId = e.estadoid,
                                EstadoNombre = es.nombre
                            };

                // ------------Filtro de búsqueda------------
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    query = query.Where(x => x.nombre.ToLower().Contains(searchLower));
                }

                var eventos = await query.ToListAsync();
                return View(eventos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos");
                return Content(ex.ToString());
            }
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
                                EstadoId = es.estadoid,
                                /*Agregado eventoid para vista ver-mas*/
                                e.eventoid
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


        public async Task<IActionResult> Certificados(string? search, int? estado, DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    return RedirectToAction("Certificados");
                }

                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                var query = from c in _elOlivoDbContext.certificado
                            join s in _elOlivoDbContext.sesion on c.sesionid equals s.sesionid
                            join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                            join es in _elOlivoDbContext.estado on c.estadoid equals es.estadoid
                            where c.usuarioid == usuarioId
                            select new
                            {
                                c.certificadoid,
                                c.codigo_unico,
                                c.fecha_emision,
                                EstadoId = c.estadoid,
                                EstadoNombre = es.nombre,
                                EventoNombre = e.nombre,
                                SesionTitulo = s.titulo,
                                // Aseguramos que fecha_emision se mantenga como nullable
                                FechaEmision = c.fecha_emision
                            };

                // ------------Filtros------------
                //buscar
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    query = query.Where(x =>
                        x.codigo_unico.ToLower().Contains(searchLower) ||
                        x.EventoNombre.ToLower().Contains(searchLower) ||
                        x.SesionTitulo.ToLower().Contains(searchLower));
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
                    query = query.Where(x => x.FechaEmision >= inicioUtc);
                }
                if (fechaFin.HasValue)
                {
                    var finUtc = DateTime.SpecifyKind(fechaFin.Value, DateTimeKind.Utc);
                    query = query.Where(x => x.FechaEmision <= finUtc);
                }

                var certificados = await query.ToListAsync();
                return View(certificados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener certificados del usuario");
                return Content(ex.ToString());
            }
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
