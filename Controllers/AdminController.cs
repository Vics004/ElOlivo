using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc;
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

        
        public IActionResult GestionUsuarios()
        {
            return View();
        }

        public IActionResult GestionEventos()
        {
            return View();
        }

        public IActionResult GestionInscripciones()
        {
            return View();
        }

        public IActionResult GestionAsistencias()
        {
            return View();
        }

    }
}
