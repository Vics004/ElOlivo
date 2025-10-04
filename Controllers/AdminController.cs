using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using static ElOlivo.Servicios.AutenticationAttribute;

namespace ElOlivo.Controllers
{
    [Autenticacion]
    public class AdminController : Controller
    {

        private readonly ILogger<AdminController> _logger;
        private readonly ElOlivoDbContext _elOlivoDbContext;

        public AdminController(ILogger<AdminController> logger, ElOlivoDbContext elOlivoDbContext)
        {
            _elOlivoDbContext = elOlivoDbContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard(DateTime? fechaInicio, DateTime? fechaFin)
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioId == null)
                return RedirectToAction("Autenticar", "Login");


            if (!fechaInicio.HasValue)
                fechaInicio = DateTime.Now.AddMonths(-1).ToUniversalTime();
            else
                fechaInicio = fechaInicio.Value.ToUniversalTime();

            if (!fechaFin.HasValue)
                fechaFin = DateTime.Now.ToUniversalTime();
            else
                fechaFin = fechaFin.Value.ToUniversalTime();


            var totalEventos = _elOlivoDbContext.evento
                .Where(e => e.fecha_inicio >= fechaInicio && e.fecha_inicio <= fechaFin)
                .Count();


            // Total de inscripciones y asistencias en el rango de fechas
            var totalInscripciones = (from i in _elOlivoDbContext.inscripcion
                                      join e in _elOlivoDbContext.evento
                                        on i.eventoid equals e.eventoid
                                      where e.fecha_inicio >= fechaInicio && e.fecha_inicio <= fechaFin
                                      select i).Count();



         
            var totalAsistencias = (from a in _elOlivoDbContext.asistencia
                                    join s in _elOlivoDbContext.sesion
                                        on a.sesionid equals s.sesionid
                                    join e in _elOlivoDbContext.evento
                                        on s.eventoid equals e.eventoid
                                    where e.fecha_inicio >= fechaInicio && e.fecha_inicio <= fechaFin
                                    select a).Count();

            // Eventos se menciona "activos" pero hace referencia a aquellos abiertos, en proceso representados en color verde
            var eventosActivos = (from e in _elOlivoDbContext.evento
                                  join est in _elOlivoDbContext.estado
                                      on e.estadoid equals est.estadoid
                                  where (est.nombre == "En proceso"
                                  || est.nombre == "Abierto") &&
                                        e.fecha_inicio >= fechaInicio &&
                                        e.fecha_inicio <= fechaFin
                                  select e).Count();

            // Eventos cancelados, finalizados o inscripción cerrada, representados en color rojo
            var eventosCancelados = (from e in _elOlivoDbContext.evento
                                     join est in _elOlivoDbContext.estado
                                         on e.estadoid equals est.estadoid
                                     where (est.nombre == "Cancelado" || est.nombre == "Finalizado" || est.nombre == "Inscripción Cerrada") &&
                                           e.fecha_inicio >= fechaInicio &&
                                           e.fecha_inicio <= fechaFin
                                     select e).Count();


            var proximosEventos = (from e in _elOlivoDbContext.evento
                                   join est in _elOlivoDbContext.estado
                                       on e.estadoid equals est.estadoid
                                   where e.fecha_inicio >= DateTime.UtcNow
                                   orderby e.fecha_inicio
                                   select new
                                   {
                                       e.eventoid,
                                       e.nombre,
                                       e.fecha_inicio,
                                       estadoNombre = est.nombre,
                                       esActivo = est.nombre != "Cancelado",
                                       totalInscripciones = _elOlivoDbContext.inscripcion
                                           .Where(i => i.eventoid == e.eventoid)
                                           .Count()
                                   })
                       .Take(5)
                       .ToList();



            ViewBag.TotalEventos = totalEventos;
            ViewBag.TotalInscripciones = totalInscripciones;
            ViewBag.TotalAsistencias = totalAsistencias;
            ViewBag.EventosActivos = eventosActivos;
            ViewBag.EventosCancelados = eventosCancelados;
            ViewBag.ProximosEventos = proximosEventos;
            ViewBag.FechaInicio = fechaInicio.Value;
            ViewBag.FechaFin = fechaFin.Value;

            return View();
        }


        [HttpGet]
        public JsonResult ObtenerEventosPorFecha(DateTime? fechaFiltro)
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioId == null)
                return Json(new { error = "No autenticado" });


            if (!fechaFiltro.HasValue)
            {
                var eventosSinFiltro = (from e in _elOlivoDbContext.evento
                                        join est in _elOlivoDbContext.estado
                                            on e.estadoid equals est.estadoid
                                        orderby e.fecha_inicio
                                        select new
                                        {
                                            e.eventoid,
                                            nombre = e.nombre,
                                            estadoNombre = est.nombre,
                                            esActivo = est.activo,

                                            totalInscripciones = _elOlivoDbContext.inscripcion
                                                .Count(i => i.eventoid == e.eventoid)
                                        }).ToList();

                return Json(new { success = true, eventos = eventosSinFiltro });
            }


            DateTime fechaBuscada = fechaFiltro.Value.Date;
            fechaBuscada = DateTime.SpecifyKind(fechaBuscada, DateTimeKind.Utc);


            DateTime inicioDia = fechaBuscada;
            DateTime finDia = fechaBuscada.AddDays(1).AddTicks(-1);


            var eventosFiltrados = (from e in _elOlivoDbContext.evento
                                    join est in _elOlivoDbContext.estado
                                        on e.estadoid equals est.estadoid
                                    where e.fecha_inicio <= finDia && e.fecha_fin >= inicioDia
                                    orderby e.fecha_inicio
                                    select new
                                    {
                                        e.eventoid,
                                        nombre = e.nombre,
                                        estadoNombre = est.nombre,
                                        esActivo = est.activo,

                                        totalInscripciones = _elOlivoDbContext.inscripcion
                                            .Count(i => i.eventoid == e.eventoid)
                                    }).ToList();

            return Json(new { success = true, eventos = eventosFiltrados });
        }










        public async Task<IActionResult> GestionUsuarios(string? search, string? eventoSearch)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    return RedirectToAction("GestionUsuarios");
                }

                int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return RedirectToAction("Login", "Account");

                // Paso 1: Obtener los eventos del administrador
                var eventosAdmin = await _elOlivoDbContext.evento
                    .Where(e => e.usuarioadminid == usuarioAdminId)
                    .Select(e => new { e.eventoid, e.nombre })
                    .ToListAsync();

                if (!eventosAdmin.Any())
                {
                    return View(new List<dynamic>());
                }

                var eventosIds = eventosAdmin.Select(e => e.eventoid).ToList();

                // Paso 2: Obtener las inscripciones a los eventos del admin
                var inscripcionesQuery = from i in _elOlivoDbContext.inscripcion
                                         join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                         join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                         where eventosIds.Contains(e.eventoid)
                                         select new
                                         {
                                             InscripcionId = i.inscripcionid,
                                             UsuarioId = u.usuarioid,
                                             NombreUsuario = u.nombre + " " + u.apellido,
                                             Email = u.email,
                                             Telefono = u.telefono,
                                             EventoId = e.eventoid,
                                             NombreEvento = e.nombre
                                         };

                // Aplicar filtros
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    inscripcionesQuery = inscripcionesQuery.Where(x =>
                        x.NombreUsuario.ToLower().Contains(searchLower));
                }

                if (!string.IsNullOrEmpty(eventoSearch))
                {
                    string eventSearchLower = eventoSearch.ToLower();
                    inscripcionesQuery = inscripcionesQuery.Where(x =>
                        x.NombreEvento.ToLower().Contains(eventSearchLower));
                }

                var inscripciones = await inscripcionesQuery.ToListAsync();

                // Paso 3: Obtener las sesiones de cada evento
                var sesionesPorEvento = await _elOlivoDbContext.sesion
                    .Where(s => eventosIds.Contains(s.eventoid.Value) && s.activo == true)
                    .GroupBy(s => s.eventoid)
                    .ToDictionaryAsync(g => g.Key, g => g.Count());

                // Paso 4: Obtener las asistencias de cada usuario (CORREGIDO - nombres correctos)
                var usuariosIds = inscripciones.Select(i => i.UsuarioId).Distinct().ToList();

                var todasAsistencias = await (from a in _elOlivoDbContext.asistencia
                                              join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                              where eventosIds.Contains(s.eventoid.Value) && usuariosIds.Contains(a.usuarioid.Value)
                                              select new
                                              {
                                                  UsuarioId = a.usuarioid,
                                                  EventoId = s.eventoid
                                              }).ToListAsync();

                // Paso 5: Combinar toda la información (EN MEMORIA)
                var usuariosConAsistencia = new List<dynamic>();

                foreach (var inscripcion in inscripciones)
                {
                    int totalSesiones = sesionesPorEvento.ContainsKey(inscripcion.EventoId) ? sesionesPorEvento[inscripcion.EventoId] : 0;

                    // Contar asistencias para este usuario en este evento
                    int asistencias = todasAsistencias
                        .Count(a => a.UsuarioId == inscripcion.UsuarioId && a.EventoId == inscripcion.EventoId);

                    double porcentajeAsistencia = totalSesiones > 0 ? Math.Round((asistencias * 100.0) / totalSesiones, 1) : 0;

                    usuariosConAsistencia.Add(new
                    {
                        inscripcion.InscripcionId,
                        inscripcion.NombreUsuario,
                        inscripcion.Email,
                        inscripcion.Telefono,
                        inscripcion.NombreEvento,
                        TotalSesiones = totalSesiones,
                        AsistenciasRegistradas = asistencias,
                        PorcentajeAsistencia = porcentajeAsistencia
                    });
                }

                return View(usuariosConAsistencia);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios inscritos");
                return Content(ex.ToString());
            }
        }

        public IActionResult GestionEventos()
        {
            return View();
        }

        public async Task<IActionResult> GestionInscripciones(string? search)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    return RedirectToAction("GestionInscripciones");
                }

                int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return RedirectToAction("Login", "Account");

                var query = from e in _elOlivoDbContext.evento
                            join es in _elOlivoDbContext.estado on e.estadoid equals es.estadoid
                            where e.usuarioadminid == usuarioAdminId // Solo eventos del administrador
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
                _logger.LogError(ex, "Error al obtener eventos del administrador");
                return Content(ex.ToString());
            }
        }

        public IActionResult GestionAsistencias()
        {
            return View();
        }

    }
}
