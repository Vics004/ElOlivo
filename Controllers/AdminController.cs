using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.IO;

using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using static ElOlivo.Servicios.AutenticationAttribute;
using ElOlivo.Servicios;

namespace ElOlivo.Controllers
{
    [Autenticacion]
    public class AdminController : Controller
    {

        private readonly ILogger<AdminController> _logger;
        private readonly SupabaseService _supabaseService;
        private readonly ElOlivoDbContext _elOlivoDbContext;

        public AdminController(ILogger<AdminController> logger, SupabaseService supabaseService, ElOlivoDbContext elOlivoDbContext)
        {
            _elOlivoDbContext = elOlivoDbContext;
            _supabaseService = supabaseService;
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


        public async Task<IActionResult> GestionUsuarios(string? search, int? eventId, int? estadoInscripcion, string? asistenciaApto, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            try
            {
                // Detectar si se presionó el botón "Limpiar"
                if (Request.Query.ContainsKey("limpiar"))
                {
                    // Redirige a una versión limpia, preservando solo el eventId si existe
                    return RedirectToAction("GestionUsuarios", new { eventId = eventId });
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
                                         join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                         where eventosIds.Contains(e.eventoid)
                                         select new
                                         {
                                             InscripcionId = i.inscripcionid,
                                             UsuarioId = u.usuarioid,
                                             NombreUsuario = u.nombre + " " + u.apellido,
                                             Email = u.email,
                                             Telefono = u.telefono,
                                             EventoId = e.eventoid,
                                             NombreEvento = e.nombre,
                                             FechaInscripcion = i.fecha_inscripcion,
                                             EstadoInscripcionId = i.estadoid,
                                             EstadoNombreInscripcion = es.nombre
                                         };

                // Aplicar filtro por EventoId (ya existente)
                if (eventId.HasValue)
                {
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.EventoId == eventId.Value);
                }

                // ------------------ FILTROS ------------------

                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    inscripcionesQuery = inscripcionesQuery.Where(x =>
                        x.NombreUsuario.ToLower().Contains(searchLower) ||
                        x.Email.ToLower().Contains(searchLower));
                }

                if (estadoInscripcion.HasValue)
                {
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.EstadoInscripcionId == estadoInscripcion.Value);
                }

 
                if (fechaDesde.HasValue)
                {
                    var fechaDesdeUtc = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.FechaInscripcion >= fechaDesdeUtc);
                }
                if (fechaHasta.HasValue)
                {
                    var fechaHastaUtc = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.FechaInscripcion <= fechaHastaUtc);
                }

                var inscripciones = await inscripcionesQuery.ToListAsync();

                // Paso 3 & 4: Obtener sesiones y asistencias
                var eventosIdsFiltrados = inscripciones.Select(i => i.EventoId).Distinct().ToList();

                var sesionesPorEvento = await _elOlivoDbContext.sesion
                    
                    .Where(s => eventosIdsFiltrados.Contains((int)s.eventoid))
                   
                    .GroupBy(s => (int)s.eventoid)
                    .Select(g => new { EventoId = g.Key, TotalSesiones = g.Count() })
                    .ToDictionaryAsync(x => x.EventoId, x => x.TotalSesiones);

                var todasAsistencias = await (from a in _elOlivoDbContext.asistencia
                                              join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                              
                                              where eventosIdsFiltrados.Contains((int)s.eventoid)
                                              select new
                                              {
                                                  UsuarioId = a.usuarioid,
                                                  EventoId = (int)s.eventoid
                                              }).ToListAsync();

                // Paso 5: Combinar toda la información (EN MEMORIA)
                var usuariosConAsistencia = new List<dynamic>();

                double umbralApto = 75.0;

                foreach (var inscripcion in inscripciones)
                {
                    int totalSesiones = sesionesPorEvento.ContainsKey(inscripcion.EventoId) ? sesionesPorEvento[inscripcion.EventoId] : 0;

                    int asistencias = todasAsistencias
                        .Count(a => a.UsuarioId == inscripcion.UsuarioId && a.EventoId == inscripcion.EventoId);

                    double porcentajeAsistencia = totalSesiones > 0 ? Math.Round((asistencias * 100.0) / totalSesiones, 1) : 0;

                    var usuarioDetalle = new
                    {
                        inscripcion.InscripcionId,
                        inscripcion.UsuarioId,
                        inscripcion.NombreUsuario,
                        inscripcion.Email,
                        inscripcion.Telefono,
                        inscripcion.FechaInscripcion,
                        inscripcion.EstadoInscripcionId,
                        inscripcion.EstadoNombreInscripcion,
                        TotalSesiones = totalSesiones,
                        AsistenciasRegistradas = asistencias,
                        PorcentajeAsistencia = porcentajeAsistencia
                    };

                    // Filtro de asistencia (APTO/NO APTO) en memoria
                    bool esApto = porcentajeAsistencia >= umbralApto;

                    if (string.IsNullOrEmpty(asistenciaApto) ||
                        (asistenciaApto == "Apto" && esApto) ||
                        (asistenciaApto == "No Apto" && !esApto))
                    {
                        usuariosConAsistencia.Add(usuarioDetalle);
                    }
                }

                return View(usuariosConAsistencia);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios inscritos");
                return Content(ex.ToString());
            }
        }

        [HttpPost]
        public async Task<IActionResult> EnviarCertificados(int eventId, string? search, int? estadoInscripcion, string? asistenciaApto, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            try
            {
                int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                var evento = await _elOlivoDbContext.evento.FindAsync(eventId);
                if (evento == null || evento.usuarioadminid != usuarioAdminId)
                    return Json(new { success = false, mensaje = "Evento no encontrado o no autorizado" });

                var umbralApto = 75.0;

                // Obtener inscripciones con los mismos filtros de GestionUsuarios
                var inscripcionesQuery = from i in _elOlivoDbContext.inscripcion
                                         join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                         join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                         where i.eventoid == eventId
                                         select new
                                         {
                                             InscripcionId = i.inscripcionid,
                                             UsuarioId = u.usuarioid,
                                             i.estadoid,
                                             i.fecha_inscripcion,
                                             EventoId = e.eventoid
                                         };

                // Aplicar filtros
                if (!string.IsNullOrEmpty(search))
                {
                    string searchLower = search.ToLower();
                    var usuariosIds = await _elOlivoDbContext.usuario
                        .Where(u => (u.nombre + " " + u.apellido).ToLower().Contains(searchLower) ||
                                   u.email.ToLower().Contains(searchLower))
                        .Select(u => u.usuarioid)
                        .ToListAsync();
                    inscripcionesQuery = inscripcionesQuery.Where(x => usuariosIds.Contains(x.UsuarioId));
                }

                if (estadoInscripcion.HasValue)
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.estadoid == estadoInscripcion.Value);

                if (fechaDesde.HasValue)
                {
                    var fechaDesdeUtc = DateTime.SpecifyKind(fechaDesde.Value.Date, DateTimeKind.Utc);
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion >= fechaDesdeUtc);
                }

                if (fechaHasta.HasValue)
                {
                    var fechaHastaUtc = DateTime.SpecifyKind(fechaHasta.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                    inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion <= fechaHastaUtc);
                }

                var inscripciones = await inscripcionesQuery.ToListAsync();

                // Obtener sesiones del evento
                var sesiones = await _elOlivoDbContext.sesion
                    .Where(s => s.eventoid == eventId)
                    .ToListAsync();

                if (!sesiones.Any())
                    return Json(new { success = false, mensaje = "El evento no tiene sesiones registradas" });

                var totalSesiones = sesiones.Count;

                // Obtener asistencias
                var asistenciasEventos = await (from a in _elOlivoDbContext.asistencia
                                                join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                                where s.eventoid == eventId && a.usuarioid.HasValue
                                                select new
                                                {
                                                    UsuarioId = a.usuarioid.Value,
                                                    SesionId = a.sesionid.Value
                                                }).ToListAsync();

                int certificadosCreados = 0;
                int certificadosExistentes = 0;
                int usuariosNoAptos = 0;

                // Obtener el estado "Emitido" (estadoid = 8)
                var estadoEmitido = await _elOlivoDbContext.estado
                    .FirstOrDefaultAsync(e => e.estadoid == 8);

                if (estadoEmitido == null)
                    return Json(new { success = false, mensaje = "No se encontró el estado 'Emitido' (ID: 8) en la base de datos" });

                foreach (var inscripcion in inscripciones)
                {
                    // Calcular porcentaje de asistencia
                    int asistencias = asistenciasEventos.Count(a => a.UsuarioId == inscripcion.UsuarioId);
                    double porcentajeAsistencia = totalSesiones > 0 ? Math.Round((asistencias * 100.0) / totalSesiones, 1) : 0;
                    bool esApto = porcentajeAsistencia >= umbralApto;

                    // Aplicar filtro de aptitud si existe
                    if (!string.IsNullOrEmpty(asistenciaApto))
                    {
                        if ((asistenciaApto == "Apto" && !esApto) || (asistenciaApto == "No Apto" && esApto))
                            continue;
                    }

                    // Solo crear certificados para usuarios APTOS
                    if (!esApto)
                    {
                        usuariosNoAptos++;
                        continue;
                    }

                    // Crear un certificado por cada sesión del evento
                    foreach (var sesion in sesiones)
                    {
                        // Verificar si ya existe un certificado para este usuario y sesión
                        var certificadoExistente = await _elOlivoDbContext.certificado
                            .FirstOrDefaultAsync(c => c.usuarioid == inscripcion.UsuarioId &&
                                                    c.sesionid == sesion.sesionid);

                        if (certificadoExistente != null)
                        {
                            certificadosExistentes++;
                            continue;
                        }

                        // Generar código único
                        string codigoUnico = $"CERT-{eventId}-{sesion.sesionid}-{inscripcion.UsuarioId}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                        // Crear nuevo certificado
                        var nuevoCertificado = new certificado
                        {
                            codigo_unico = codigoUnico,
                            fecha_emision = DateTime.UtcNow,
                            estadoid = 8, // Emitido
                            usuarioid = inscripcion.UsuarioId,
                            sesionid = sesion.sesionid
                        };

                        _elOlivoDbContext.certificado.Add(nuevoCertificado);
                        certificadosCreados++;
                    }
                }

                // Guardar todos los certificados
                await _elOlivoDbContext.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    mensaje = $"Proceso completado: {certificadosCreados} certificados creados, {certificadosExistentes} ya existían, {usuariosNoAptos} usuarios no aptos",
                    certificadosCreados = certificadosCreados,
                    certificadosExistentes = certificadosExistentes,
                    usuariosNoAptos = usuariosNoAptos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar certificados");
                return Json(new { success = false, mensaje = $"Error: {ex.Message}" });
            }
        }


        public async Task<IActionResult> VerSesiones(int id) // id es eventoid
        {
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null)
                return RedirectToAction("Login", "Account");

            // 1. Obtener evento (asegurando que pertenezca a este admin)
            var evento = await _elOlivoDbContext.evento
                .Where(e => e.eventoid == id && e.usuarioadminid == usuarioAdminId)
                .FirstOrDefaultAsync();

            if (evento == null)
                return NotFound();

            // 2. Obtener sesiones con sus actividades (CORRECCIÓN: Proyectamos nombre y apellido por separado)
            var sesiones = await (from s in _elOlivoDbContext.sesion
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
                                                         // CORRECCIÓN: Proyectamos las partes del nombre para concatenar en memoria
                                                         ponenteNombre = u.nombre,
                                                         ponenteApellido = u.apellido
                                                     }).ToList()
                                  }).ToListAsync();

            // 3. Post-procesamiento (En memoria) para concatenar nombre y apellido 
            // y asegurar que la vista reciba la estructura esperada (ponenteNombre como un solo campo)
            var sesionesConPonenteNombre = sesiones.Select(s => new
            {
                s.sesionid,
                s.titulo,
                s.descripcion,
                s.fecha_inicio,
                s.fecha_fin,
                // Proyectamos de nuevo las actividades para incluir el nombre completo
                actividades = s.actividades.Select(a => new {
                    a.agendaid,
                    a.nombre,
                    a.descripcion,
                    a.hora_inicio,
                    a.hora_fin,
                    // Concatenación de strings fuera de la consulta de la DB
                    ponenteNombre = $"{a.ponenteNombre} {a.ponenteApellido}"
                }).ToList()
            }).ToList();

            ViewBag.Evento = evento;
            // <--- Usamos el objeto post-procesado --->
            ViewBag.Sesiones = sesionesConPonenteNombre;
            ViewData["Title"] = $"Sesiones de: {evento.nombre}";

            // Y confirmamos la corrección de la vista:
            return View();
        }

        public IActionResult GestionEventos()
        {
            return View();
        }

        public ActionResult GetEventos()
        {
            try
            {
                // Obtener sesiones activas de eventos que no estén cancelados
                var sesiones = (from s in _elOlivoDbContext.sesion
                                join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                where s.activo == true
                                   && e.estadoid != 5 // Excluir eventos cancelados
                                select new
                                {
                                    s.sesionid,
                                    s.titulo,
                                    s.fecha_inicio,
                                    s.fecha_fin,
                                    EstadoEvento = e.estadoid
                                })
                               .ToList();

                var data = sesiones.Select(e => new
                {
                    id = e.sesionid,
                    title = e.titulo,
                    start = e.fecha_inicio.HasValue
                        ? e.fecha_inicio.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    end = e.fecha_fin.HasValue
                        ? e.fecha_fin.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    // Color diferente según el estado del evento
                    color = e.EstadoEvento switch
                    {
                        4 => "#6c757d", // Finalizado - Gris
                        6 => "#ffc107", // Inscripción Cerrada - Amarillo
                        _ => "#007bff"  // Abierto - Azul
                    },
                    allDay = false
                });

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos para el calendario");
                return Json(new List<object>());
            }
        }

        //
        public IActionResult CrearEvento(string fecha)
        {
                DateTime? fechaSeleccionada = null;
                if (!string.IsNullOrEmpty(fecha))
                {
                    fechaSeleccionada = DateTime.Parse(fecha);
                }

                ViewBag.FechaSeleccionada = fechaSeleccionada;

            
                ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
                ViewBag.Descripcion = HttpContext.Session.GetString("Descripcion");
                ViewBag.Correo = HttpContext.Session.GetString("Correo");
                ViewBag.FechaInicio = HttpContext.Session.GetString("FechaInicio");
                ViewBag.HoraInicio = HttpContext.Session.GetString("HoraInicio");
                ViewBag.FechaFin = HttpContext.Session.GetString("FechaFin");
                ViewBag.HoraFin = HttpContext.Session.GetString("HoraFin");
                ViewBag.UbicacionURL = HttpContext.Session.GetString("UbicacionURL");

           
                ViewBag.UsuarioAdminId = HttpContext.Session.GetInt32("UsuarioAdminId");

                return View();
        }
        
        [HttpGet]
        public IActionResult ValidarAdministrador(string correo)
        {
                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new { existe = false });
                }

            
                var usuario = _elOlivoDbContext.usuario
                    .FirstOrDefault(u => u.email == correo && u.activo == true);

                if (usuario == null)
                {
                    return Json(new { existe = false });
                }

            
                var rol = _elOlivoDbContext.rol
                    .FirstOrDefault(r => r.rolid == usuario.rolid && r.nombre == "Administrador");

                if (rol != null)
                {
                    return Json(new
                    {
                        existe = true,
                        usuarioAdminId = usuario.usuarioid
                    });
                }

                return Json(new { existe = false });
        }

        [HttpGet]
        public IActionResult ValidarModerador(string correo)
        {
                if (string.IsNullOrWhiteSpace(correo))
                {
                    return Json(new { existe = false });
                }

           
                var usuario = _elOlivoDbContext.usuario
                    .FirstOrDefault(u => u.email == correo && u.activo == true);

                if (usuario != null)
                {
                    return Json(new
                    {
                        existe = true,
                        usuarioId = usuario.usuarioid
                    });
                }

                return Json(new { existe = false });
            }
        

        [HttpPost]
        public async Task<IActionResult> GuardarFase1()
        {
                try
                {
                    using var reader = new StreamReader(Request.Body);
                    var body = await reader.ReadToEndAsync();

                    var datos = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

                    if (datos == null)
                        return BadRequest(new { mensaje = "No se recibieron datos válidos" });

                    HttpContext.Session.SetString("Nombre", datos["Nombre"].GetString());
                    HttpContext.Session.SetString("Descripcion", datos["Descripcion"].GetString());
                    HttpContext.Session.SetString("Correo", datos["Correo"].GetString());
                    HttpContext.Session.SetInt32("UsuarioAdminId", datos["UsuarioAdminId"].GetInt32());
                    HttpContext.Session.SetString("FechaInicio", datos["FechaInicio"].GetString());
                    HttpContext.Session.SetString("HoraInicio", datos["HoraInicio"].GetString());
                    HttpContext.Session.SetString("FechaFin", datos["FechaFin"].GetString());
                    HttpContext.Session.SetString("HoraFin", datos["HoraFin"].GetString());
                    HttpContext.Session.SetString("UbicacionURL", datos["UbicacionURL"].GetString());

                    return Ok(new { mensaje = "Fase 1 guardada correctamente" });
                }
                catch (Exception ex)
                {
                    var mensaje = ex.Message;

                    if (ex.InnerException != null)
                    {
                        mensaje += " | Inner exception: " + ex.InnerException.Message;
                    }

                    return Json(new { success = false, mensaje });
                }

        }


        
        public IActionResult FaseDos()
        {
                var nombre = HttpContext.Session.GetString("Nombre");
                if (string.IsNullOrEmpty(nombre))
                {
                    return RedirectToAction("CrearEvento");
                }

            
                ViewBag.FechaInicioEvento = HttpContext.Session.GetString("FechaInicio");
                ViewBag.HoraInicioEvento = HttpContext.Session.GetString("HoraInicio");
                ViewBag.FechaFinEvento = HttpContext.Session.GetString("FechaFin");
                ViewBag.HoraFinEvento = HttpContext.Session.GetString("HoraFin");

                return View("CrearEvento2");
        }



        [HttpPost]
        public async Task<IActionResult> GuardarEventoCompleto()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                var datosFase2 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

                
                var nombre = HttpContext.Session.GetString("Nombre");
                if (string.IsNullOrEmpty(nombre))
                {
                    return Json(new { success = false, mensaje = "Los datos de la fase 1 se perdieron. Por favor inicie de nuevo." });
                }

                
                var estadoAbierto = await _elOlivoDbContext.estado
                    .FirstOrDefaultAsync(e => e.nombre == "Abierto" && e.activo == true);

                if (estadoAbierto == null)
                {
                    return Json(new { success = false, mensaje = "No se encontró el estado 'Abierto' en la base de datos." });
                }

               
                var fechaInicio = DateTime.Parse(HttpContext.Session.GetString("FechaInicio"));
                var horaInicio = TimeSpan.Parse(HttpContext.Session.GetString("HoraInicio"));
                var fechaHoraInicio = fechaInicio.Add(horaInicio);

                var fechaFin = DateTime.Parse(HttpContext.Session.GetString("FechaFin"));
                var horaFin = TimeSpan.Parse(HttpContext.Session.GetString("HoraFin"));
                var fechaHoraFin = fechaFin.Add(horaFin);

               
                var fechaHoraInicioUtc = DateTime.SpecifyKind(fechaHoraInicio, DateTimeKind.Utc);
                var fechaHoraFinUtc = DateTime.SpecifyKind(fechaHoraFin, DateTimeKind.Utc);

               
                var nuevoEvento = new evento
                {
                    nombre = nombre,
                    descripcion = HttpContext.Session.GetString("Descripcion"),
                    correo_encargado = HttpContext.Session.GetString("Correo"),
                    usuarioadminid = HttpContext.Session.GetInt32("UsuarioAdminId").Value,
                    fecha_inicio = fechaHoraInicioUtc,
                    fecha_fin = fechaHoraFinUtc,
                    ubicacion_url = HttpContext.Session.GetString("UbicacionURL"),
                    direccion = datosFase2["Direccion"].GetString(),
                    capacidad_maxima = datosFase2["CapacidadMaxima"].GetInt32(),
                    estadoid = estadoAbierto.estadoid
                };

               
                var sesionesJson = HttpContext.Session.GetString("SesionesTemporal");
                if (!string.IsNullOrEmpty(sesionesJson))
                {
                    var sesionesElement = JsonSerializer.Deserialize<JsonElement>(sesionesJson);
                    if (sesionesElement.ValueKind == JsonValueKind.Array)
                    {
                        var sesionesArray = sesionesElement.EnumerateArray();
                        int capacidadTotalSesiones = 0;

                        foreach (var sesionElement in sesionesArray)
                        {
                            if (sesionElement.TryGetProperty("Capacidad", out var capacidadElement))
                            {
                                capacidadTotalSesiones += capacidadElement.GetInt32();
                            }
                        }

                        if (capacidadTotalSesiones > nuevoEvento.capacidad_maxima)
                        {
                            return Json(new
                            {
                                success = false,
                                mensaje = $"La capacidad total de las sesiones ({capacidadTotalSesiones}) excede la capacidad máxima del evento ({nuevoEvento.capacidad_maxima})"
                            });
                        }
                    }
                }

                _elOlivoDbContext.evento.Add(nuevoEvento);

                
                await _elOlivoDbContext.SaveChangesAsync();
                

                
                var sesionesGuardadas = 0;
                var sesionesError = 0;
                var erroresDetalle = new List<string>();

                if (!string.IsNullOrEmpty(sesionesJson))
                {
                    
                    var sesionesElement = JsonSerializer.Deserialize<JsonElement>(sesionesJson);

                    if (sesionesElement.ValueKind == JsonValueKind.Array)
                    {
                        var sesionesArray = sesionesElement.EnumerateArray();
                        var totalSesiones = sesionesElement.GetArrayLength();
                        Console.WriteLine($"Total de sesiones a procesar: {totalSesiones}");

                        foreach (var sesionElement in sesionesArray)
                        {
                            try
                            {
                                
                                if (!sesionElement.TryGetProperty("FechaInicio", out var fechaInicioElement) ||
                                    !sesionElement.TryGetProperty("HoraInicio", out var horaInicioElement) ||
                                    !sesionElement.TryGetProperty("FechaFin", out var fechaFinElement) ||
                                    !sesionElement.TryGetProperty("HoraFin", out var horaFinElement) ||
                                    !sesionElement.TryGetProperty("UsuarioModeradorId", out var moderadorIdElement) ||
                                    !sesionElement.TryGetProperty("Titulo", out var tituloElement))
                                {
                                    var error = "Sesión con campos faltantes";
                                    Console.WriteLine(error);
                                    erroresDetalle.Add(error);
                                    sesionesError++;
                                    continue;
                                }

                                
                                var tituloSesion = tituloElement.GetString();
                                var tipoSesion = sesionElement.TryGetProperty("Tipo", out var tipoElement) ? tipoElement.GetString() : "Sin tipo";
                                var descripcionSesion = sesionElement.TryGetProperty("Descripcion", out var descElement) ? descElement.GetString() : "";
                                var ubicacionSesion = sesionElement.TryGetProperty("Ubicacion", out var ubicElement) ? ubicElement.GetString() : "";
                                var capacidadSesion = sesionElement.TryGetProperty("Capacidad", out var capElement) ? capElement.GetInt32() : 0;

                                
                                var fechaInicioSesion = DateTime.Parse(fechaInicioElement.GetString());
                                var horaInicioSesion = TimeSpan.Parse(horaInicioElement.GetString());
                                var fechaHoraInicioSesion = DateTime.SpecifyKind(fechaInicioSesion.Add(horaInicioSesion), DateTimeKind.Utc);

                                var fechaFinSesion = DateTime.Parse(fechaFinElement.GetString());
                                var horaFinSesion = TimeSpan.Parse(horaFinElement.GetString());
                                var fechaHoraFinSesion = DateTime.SpecifyKind(fechaFinSesion.Add(horaFinSesion), DateTimeKind.Utc);

                              
                                var moderadorId = moderadorIdElement.GetInt32();
                                var moderadorExiste = await _elOlivoDbContext.usuario.AnyAsync(u => u.usuarioid == moderadorId && u.activo == true);

                                if (!moderadorExiste)
                                {
                                    var error = $"Moderador con ID {moderadorId} no existe o no está activo";
                                    Console.WriteLine(error);
                                    erroresDetalle.Add(error);
                                    sesionesError++;
                                    continue;
                                }

                               
                                var nuevaSesion = new sesion
                                {
                                    eventoid = nuevoEvento.eventoid,
                                    titulo = tituloSesion,
                                    tipo_sesion = tipoSesion,
                                    descripcion = descripcionSesion,
                                    fecha_inicio = fechaHoraInicioSesion,
                                    fecha_fin = fechaHoraFinSesion,
                                    ubicacion = ubicacionSesion,
                                    capacidad = capacidadSesion,
                                    moderadorid = moderadorId,
                                    activo = true 
                                };

                                _elOlivoDbContext.sesion.Add(nuevaSesion);
                                sesionesGuardadas++;

                                
                            }
                            catch (Exception exSesion)
                            {
                                var error = $"Error al procesar sesión: {exSesion.Message}";
                                erroresDetalle.Add(error);
                                sesionesError++;
                                continue;
                            }
                        }

                       
                        if (sesionesGuardadas > 0)
                        {
                            try
                            {   
                                await _elOlivoDbContext.SaveChangesAsync();
               
                            }
                            catch (Exception exSave)
                            {
                                var errorMsg = $"Error al guardar sesiones en la base de datos: {exSave.Message}";
                                if (exSave.InnerException != null)
                                {
                                    errorMsg += $" | Inner: {exSave.InnerException.Message}";
                                }
                                
                                return Json(new
                                {
                                    success = false,
                                    mensaje = errorMsg,
                                    erroresDetalle = erroresDetalle
                                });
                            }
                        }
                        
                    }
                    
                }
                

                HttpContext.Session.SetInt32("EventoEnPlanificacionId", nuevoEvento.eventoid);

                
                HttpContext.Session.Remove("Nombre");
                HttpContext.Session.Remove("Descripcion");
                HttpContext.Session.Remove("Correo");
                HttpContext.Session.Remove("UsuarioAdminId");
                HttpContext.Session.Remove("FechaInicio");
                HttpContext.Session.Remove("HoraInicio");
                HttpContext.Session.Remove("FechaFin");
                HttpContext.Session.Remove("HoraFin");
                HttpContext.Session.Remove("UbicacionURL");
                HttpContext.Session.Remove("SesionesTemporal");

                var mensajeFinal = $"Evento creado exitosamente";
                if (sesionesGuardadas > 0)
                {
                    mensajeFinal += $" con {sesionesGuardadas} sesiones";
                }
                else
                {
                    mensajeFinal += " sin sesiones";
                }

                if (sesionesError > 0)
                {
                    mensajeFinal += $" ({sesionesError} sesiones no se pudieron guardar)";
                }

                return Json(new
                {
                    success = true,
                    eventoId = nuevoEvento.eventoid,
                    sesionesGuardadas = sesionesGuardadas,
                    sesionesError = sesionesError,
                    erroresDetalle = erroresDetalle,
                    mensaje = mensajeFinal
                });
            }
            catch (Exception ex)
            {
                var mensaje = ex.Message;
                if (ex.InnerException != null)
                {
                    mensaje += " | Inner exception: " + ex.InnerException.Message;
                }

                
                return Json(new { success = false, mensaje });
            }
        }

        
        public IActionResult Volver()
        {
            return View("GestionEventos");
        }

        [HttpPost]
        public async Task<IActionResult> GuardarSesionTemp()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(body))
                    return Json(new { success = false, mensaje = "No se recibieron datos" });

                var nuevaSesion = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

                if (nuevaSesion == null)
                    return Json(new { success = false, mensaje = "No se recibieron datos válidos" });

                
                if (!nuevaSesion.ContainsKey("Titulo") || string.IsNullOrEmpty(nuevaSesion["Titulo"].GetString()))
                    return Json(new { success = false, mensaje = "El título es requerido" });

                
                var sesionesJson = HttpContext.Session.GetString("SesionesTemporal");
                List<Dictionary<string, object>> sesiones;

                if (string.IsNullOrEmpty(sesionesJson))
                {
                    sesiones = new List<Dictionary<string, object>>();
                }
                else
                {
                    sesiones = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sesionesJson);
                }

               
                var sesionDict = new Dictionary<string, object>
                    {
                        { "Titulo", nuevaSesion["Titulo"].GetString() },
                        { "Tipo", nuevaSesion["Tipo"].GetString() },
                        { "Descripcion", nuevaSesion.ContainsKey("Descripcion") && !string.IsNullOrEmpty(nuevaSesion["Descripcion"].GetString())
                            ? nuevaSesion["Descripcion"].GetString() : "" },
                        { "FechaInicio", nuevaSesion["FechaInicio"].GetString() },
                        { "HoraInicio", nuevaSesion["HoraInicio"].GetString() },
                        { "FechaFin", nuevaSesion["FechaFin"].GetString() },
                        { "HoraFin", nuevaSesion["HoraFin"].GetString() },
                        { "Ubicacion", nuevaSesion["Ubicacion"].GetString() },
                        { "Capacidad", nuevaSesion["Capacidad"].GetInt32() },
                        { "CorreoModerador", nuevaSesion["CorreoModerador"].GetString() },
                        { "UsuarioModeradorId", nuevaSesion["UsuarioModeradorId"].GetInt32() },
                        { "Id", Guid.NewGuid().ToString() }
                    };

                sesiones.Add(sesionDict);

                
                var sesionesActualizadas = JsonSerializer.Serialize(sesiones);
                HttpContext.Session.SetString("SesionesTemporal", sesionesActualizadas);

                return Json(new { success = true, mensaje = "Sesión guardada temporalmente", totalSesiones = sesiones.Count });
            }
            catch (Exception ex)
            {
                var mensaje = ex.Message;
                if (ex.InnerException != null)
                {
                    mensaje += " | Inner exception: " + ex.InnerException.Message;
                }
                return Json(new { success = false, mensaje });
            }
        }


        [HttpGet]
        public IActionResult ObtenerSesionesTemp()
        {
            try
            {
                
                var sesionesJson = HttpContext.Session.GetString("SesionesTemporal");

                if (string.IsNullOrEmpty(sesionesJson))
                {
                    return Json(new
                    {
                        success = true,
                        sesiones = new List<Dictionary<string, object>>()
                    });
                }

                var sesiones = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sesionesJson);

                return Json(new
                {
                    success = true,
                    sesiones = sesiones
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    mensaje = ex.Message
                });
            }
        }


        
        [HttpGet]
        public IActionResult ValidarConflictoEvento(string fechaInicio, string horaInicio, string fechaFin, string horaFin)
        {
            try
            {
                if (string.IsNullOrEmpty(fechaInicio) || string.IsNullOrEmpty(horaInicio) ||
                    string.IsNullOrEmpty(fechaFin) || string.IsNullOrEmpty(horaFin))
                {
                    return Json(new { tieneConflicto = false });
                }

                
                var fechaInicioDt = DateTime.Parse(fechaInicio);
                var horaInicioTs = TimeSpan.Parse(horaInicio);
                var fechaHoraInicio = DateTime.SpecifyKind(fechaInicioDt.Add(horaInicioTs), DateTimeKind.Utc);

                var fechaFinDt = DateTime.Parse(fechaFin);
                var horaFinTs = TimeSpan.Parse(horaFin);
                var fechaHoraFin = DateTime.SpecifyKind(fechaFinDt.Add(horaFinTs), DateTimeKind.Utc);

                
                var estadoAbierto = _elOlivoDbContext.estado
                    .FirstOrDefault(e => e.nombre == "Abierto" && e.activo == true);

                if (estadoAbierto == null)
                {
                    return Json(new { tieneConflicto = false });
                }

                
                var eventoDuplicado = _elOlivoDbContext.evento
                    .Where(e => e.estadoid == estadoAbierto.estadoid &&
                                e.fecha_inicio == fechaHoraInicio &&
                                e.fecha_fin == fechaHoraFin)
                    .Select(e => new
                    {
                        e.nombre,
                        e.fecha_inicio,
                        e.fecha_fin
                    })
                    .FirstOrDefault();

                if (eventoDuplicado != null)
                {
                    
                    var fechaInicioEvento = eventoDuplicado.fecha_inicio ?? DateTime.MinValue;
                    var fechaFinEvento = eventoDuplicado.fecha_fin ?? DateTime.MinValue;

                  
                    var fechaInicioStr = fechaInicioEvento.ToString("dd/MM/yyyy");
                    var horaInicioStr = fechaInicioEvento.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                    var fechaFinStr = fechaFinEvento.ToString("dd/MM/yyyy");
                    var horaFinStr = fechaFinEvento.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                  
                    string mensaje;
                    if (fechaInicioStr == fechaFinStr)
                    {
                        mensaje = $"Ya existe el evento '{eventoDuplicado.nombre}' programado el {fechaInicioStr} de {horaInicioStr} a {horaFinStr}";
                    }
                    else
                    {
                        mensaje = $"Ya existe el evento '{eventoDuplicado.nombre}' programado del {fechaInicioStr} al {fechaFinStr} de {horaInicioStr} a {horaFinStr}";
                    }

                    return Json(new
                    {
                        tieneConflicto = true,
                        mensaje = mensaje,
                        eventoExistente = eventoDuplicado
                    });
                }

                return Json(new { tieneConflicto = false });
            }
            catch (Exception ex)
            {
               
                return Json(new { tieneConflicto = false });
            }
        }
        

        [HttpGet]
        public IActionResult ValidarConflictoSesion(string fechaInicio, string horaInicio, string fechaFin, string horaFin)
        {
            try
            {
                if (string.IsNullOrEmpty(fechaInicio) || string.IsNullOrEmpty(horaInicio) ||
                    string.IsNullOrEmpty(fechaFin) || string.IsNullOrEmpty(horaFin))
                {
                    return Json(new { tieneConflicto = false });
                }

                
                var fechaInicioDt = DateTime.Parse(fechaInicio);
                var horaInicioTs = TimeSpan.Parse(horaInicio);
                var fechaHoraInicio = DateTime.SpecifyKind(fechaInicioDt.Add(horaInicioTs), DateTimeKind.Utc);

                var fechaFinDt = DateTime.Parse(fechaFin);
                var horaFinTs = TimeSpan.Parse(horaFin);
                var fechaHoraFin = DateTime.SpecifyKind(fechaFinDt.Add(horaFinTs), DateTimeKind.Utc);

                
                var sesionesConflictivasBD = _elOlivoDbContext.sesion
                    .Where(s => s.activo == true &&
                        s.fecha_inicio.Value.Date == fechaHoraInicio.Date &&
                        s.fecha_fin.Value.Date == fechaHoraFin.Date &&
                        s.fecha_inicio.Value.TimeOfDay == fechaHoraInicio.TimeOfDay &&
                        s.fecha_fin.Value.TimeOfDay == fechaHoraFin.TimeOfDay)
                    .Select(s => new
                    {
                        s.titulo,
                        s.fecha_inicio,
                        s.fecha_fin,
                        s.tipo_sesion
                    })
                    .ToList();

                if (sesionesConflictivasBD.Any())
                {
                    var sesionConflicto = sesionesConflictivasBD.First();

                   
                    var fechaInicioEvento = sesionConflicto.fecha_inicio ?? DateTime.MinValue;
                    var fechaFinEvento = sesionConflicto.fecha_fin ?? DateTime.MinValue;

                    var fechaInicioStr = fechaInicioEvento.ToString("dd/MM/yyyy");
                    var horaInicioStr = fechaInicioEvento.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                    var fechaFinStr = fechaFinEvento.ToString("dd/MM/yyyy");
                    var horaFinStr = fechaFinEvento.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                    string mensaje;
                    if (fechaInicioStr == fechaFinStr)
                    {
                        mensaje = $"Ya existe la sesión '{sesionConflicto.titulo}' ({sesionConflicto.tipo_sesion}) de otro evento programada el {fechaInicioStr} de {horaInicioStr} a {horaFinStr}";
                    }
                    else
                    {
                        mensaje = $"Ya existe la sesión '{sesionConflicto.titulo}' ({sesionConflicto.tipo_sesion}) de otro evento programada del {fechaInicioStr} al {fechaFinStr} de {horaInicioStr} a {horaFinStr}";
                    }

                    return Json(new
                    {
                        tieneConflicto = true,
                        mensaje = mensaje,
                        sesionExistente = sesionConflicto
                    });
                }

                
                var sesionesJson = HttpContext.Session.GetString("SesionesTemporal");
                if (!string.IsNullOrEmpty(sesionesJson))
                {
                    var sesionesTemporales = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(sesionesJson);

                    foreach (var sesionTemp in sesionesTemporales)
                    {
                        if (sesionTemp.ContainsKey("FechaInicio") && sesionTemp.ContainsKey("HoraInicio") &&
                            sesionTemp.ContainsKey("FechaFin") && sesionTemp.ContainsKey("HoraFin") &&
                            sesionTemp.ContainsKey("Titulo") && sesionTemp.ContainsKey("Tipo"))
                        {
                            var fechaInicioTemp = DateTime.Parse(sesionTemp["FechaInicio"].ToString());
                            var horaInicioTemp = TimeSpan.Parse(sesionTemp["HoraInicio"].ToString());
                            var fechaHoraInicioTemp = DateTime.SpecifyKind(fechaInicioTemp.Add(horaInicioTemp), DateTimeKind.Utc);

                            var fechaFinTemp = DateTime.Parse(sesionTemp["FechaFin"].ToString());
                            var horaFinTemp = TimeSpan.Parse(sesionTemp["HoraFin"].ToString());
                            var fechaHoraFinTemp = DateTime.SpecifyKind(fechaFinTemp.Add(horaFinTemp), DateTimeKind.Utc);

                            
                            bool mismoRangoExacto = (fechaHoraInicio.Date == fechaHoraInicioTemp.Date &&
                                                    fechaHoraFin.Date == fechaHoraFinTemp.Date &&
                                                    fechaHoraInicio.TimeOfDay == fechaHoraInicioTemp.TimeOfDay &&
                                                    fechaHoraFin.TimeOfDay == fechaHoraFinTemp.TimeOfDay);

                            if (mismoRangoExacto)
                            {
                                var tituloSesion = sesionTemp["Titulo"].ToString();
                                var tipoSesion = sesionTemp["Tipo"].ToString();

                               
                                var fechaInicioStr = fechaHoraInicioTemp.ToString("dd/MM/yyyy");
                                var horaInicioStr = fechaHoraInicioTemp.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                                var fechaFinStr = fechaHoraFinTemp.ToString("dd/MM/yyyy");
                                var horaFinStr = fechaHoraFinTemp.ToString("hh:mm tt", new System.Globalization.CultureInfo("es-ES"));

                                string mensaje;
                                if (fechaInicioStr == fechaFinStr)
                                {
                                    mensaje = $"Ya agregaste la sesión '{tituloSesion}' ({tipoSesion}) para este evento el {fechaInicioStr} de {horaInicioStr} a {horaFinStr}";
                                }
                                else
                                {
                                    mensaje = $"Ya agregaste la sesión '{tituloSesion}' ({tipoSesion}) para este evento del {fechaInicioStr} al {fechaFinStr} de {horaInicioStr} a {horaFinStr}";
                                }

                                return Json(new
                                {
                                    tieneConflicto = true,
                                    mensaje = mensaje,
                                    sesionExistente = new
                                    {
                                        titulo = tituloSesion,
                                        tipo_sesion = tipoSesion,
                                        fecha_inicio = fechaHoraInicioTemp,
                                        fecha_fin = fechaHoraFinTemp
                                    }
                                });
                            }
                        }
                    }
                }

                return Json(new { tieneConflicto = false });
            }
            catch (Exception ex)
            {
                
                return Json(new { tieneConflicto = false });
            }
        }


        [HttpPost]
        public async Task<IActionResult> GuardarTipoActividadTemp()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(body))
                    return Json(new { success = false, mensaje = "No se recibieron datos" });

                var tipoActividad = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

                if (tipoActividad == null)
                    return Json(new { success = false, mensaje = "Datos inválidos" });

                
                if (!tipoActividad.ContainsKey("nombre") || string.IsNullOrEmpty(tipoActividad["nombre"]))
                    return Json(new { success = false, mensaje = "El nombre es obligatorio" });

                
                var tiposJson = HttpContext.Session.GetString("TiposActividadTemp");
                List<Dictionary<string, object>> tiposTemp;

                if (string.IsNullOrEmpty(tiposJson))
                    tiposTemp = new List<Dictionary<string, object>>();
                else
                    tiposTemp = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tiposJson);

                
                var nuevoTipo = new Dictionary<string, object>
                {
                    { "IdTemp", Guid.NewGuid().ToString() }, 
                    { "nombre", tipoActividad["nombre"] },
                    { "descripcion", tipoActividad.ContainsKey("descripcion") ? tipoActividad["descripcion"] : "" },
                    { "activo", true }
                };

                tiposTemp.Add(nuevoTipo);

                
                HttpContext.Session.SetString("TiposActividadTemp", JsonSerializer.Serialize(tiposTemp));

                return Json(new { success = true, total = tiposTemp.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ObtenerTiposActividadTemp()
        {
            var tiposJson = HttpContext.Session.GetString("TiposActividadTemp");
            var tiposTemp = string.IsNullOrEmpty(tiposJson)
                ? new List<Dictionary<string, object>>()
                : JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tiposJson);

            return Json(tiposTemp);
        }


        public async Task<IActionResult> PlanificacionActividades()
        {
            
            var eventoId = HttpContext.Session.GetInt32("EventoEnPlanificacionId");

            if (eventoId == null)
            {
                
                ViewBag.NombreEvento = "Evento no encontrado";
                ViewBag.FechasEvento = "";
                ViewBag.Sesiones = new List<sesion>();
                return View();
            }

           
            var evento = await _elOlivoDbContext.evento
                .FirstOrDefaultAsync(e => e.eventoid == eventoId.Value);

            if (evento == null)
            {
                ViewBag.NombreEvento = "Evento no encontrado";
                ViewBag.FechasEvento = "";
                ViewBag.Sesiones = new List<sesion>();
                return View();
            }

            var fechaInicioStr = evento.fecha_inicio?.ToString("dd/MM/yyyy") ?? "";
            var horaInicioStr = evento.fecha_inicio?.ToString("hh:mm tt", new CultureInfo("es-ES")) ?? "";
            var fechaFinStr = evento.fecha_fin?.ToString("dd/MM/yyyy") ?? "";
            var horaFinStr = evento.fecha_fin?.ToString("hh:mm tt", new CultureInfo("es-ES")) ?? "";

           
            ViewBag.NombreEvento = evento.nombre;
            ViewBag.FechasEvento = $"{fechaInicioStr} {horaInicioStr} - {fechaFinStr} {horaFinStr}";


           
            var sesiones = await _elOlivoDbContext.sesion
                .Where(s => s.eventoid == eventoId.Value && s.fecha_inicio != null && s.fecha_fin != null)
                .OrderBy(s => s.fecha_inicio)
                .ToListAsync();

            ViewBag.Sesiones = sesiones;

            return View();
        }



        [HttpGet]
        public async Task<IActionResult> ObtenerAgendasPorSesion(int sesionId)
        {
            try
            {
               
                var agendasBD = await _elOlivoDbContext.actividad
                    .Where(a => a.sesionid == sesionId && a.activo == true)
                    .OrderBy(a => a.hora_inicio)
                    .Select(a => new
                    {
                        a.agendaid,
                        a.nombre,
                        a.descripcion,
                        hora_inicio = a.hora_inicio,
                        hora_fin = a.hora_fin,
                        a.ponenteid,
                        a.tipoactividadid,
                        esTemp = false
                    })
                    .ToListAsync();

                
                var agendasJson = HttpContext.Session.GetString("AgendasTemp");
                var agendasTemp = new List<object>();

                if (!string.IsNullOrEmpty(agendasJson))
                {
                    var agendasTempList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(agendasJson);
                    agendasTemp = agendasTempList
                        .Where(a => a.ContainsKey("sesionId") && a["sesionId"].GetInt32() == sesionId)
                        .Select(a => new
                        {
                            IdTemp = a["IdTemp"].GetString(),
                            nombre = a["nombre"].GetString(),
                            hora_inicio = a["horaInicio"].GetString(),
                            hora_fin = a["horaFin"].GetString(),
                            esTemp = true
                        })
                        .ToList<object>();

                    
                }
                
                var todasLasAgendas = agendasBD.Cast<object>().Concat(agendasTemp).ToList();

              

                return Json(new { success = true, agendas = todasLasAgendas });
            }
            catch (Exception ex)
            {
               
                return Json(new { success = false, mensaje = ex.Message });
            }
        }



        [HttpPost]
        public async Task<IActionResult> GuardarAgendaTemp()
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                
                if (string.IsNullOrEmpty(body))
                    return Json(new { success = false, mensaje = "No se recibieron datos" });

                var agenda = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);

                if (agenda == null)
                    return Json(new { success = false, mensaje = "Datos inválidos" });

                
                if (!agenda.ContainsKey("nombre") || string.IsNullOrEmpty(agenda["nombre"].GetString()))
                    return Json(new { success = false, mensaje = "El nombre es obligatorio" });

                if (!agenda.ContainsKey("sesionId"))
                    return Json(new { success = false, mensaje = "Debe seleccionar una sesión" });

           
                int sesionId = agenda["sesionId"].GetInt32();
                

                var sesion = await _elOlivoDbContext.sesion
                    .FirstOrDefaultAsync(s => s.sesionid == sesionId);

                if (sesion == null)
                {
                    
                    return Json(new { success = false, mensaje = "Sesión no encontrada" });
                }

               
                string horaInicioStr = agenda["horaInicio"].GetString(); 
                string horaFinStr = agenda["horaFin"].GetString();       

              

                DateTime fechaSesion = DateTime.SpecifyKind(sesion.fecha_inicio.Value.Date, DateTimeKind.Utc);

                
                var horaInicioParts = horaInicioStr.Split(':');
                var horaFinParts = horaFinStr.Split(':');

                DateTime horaInicioCompleta = fechaSesion
                    .AddHours(int.Parse(horaInicioParts[0]))
                    .AddMinutes(int.Parse(horaInicioParts[1]));

                DateTime horaFinCompleta = fechaSesion
                    .AddHours(int.Parse(horaFinParts[0]))
                    .AddMinutes(int.Parse(horaFinParts[1]));

               
                var horaInicioUtc = DateTime.SpecifyKind(horaInicioCompleta, DateTimeKind.Utc);
                var horaFinUtc = DateTime.SpecifyKind(horaFinCompleta, DateTimeKind.Utc);

               

               
                var agendasJson = HttpContext.Session.GetString("AgendasTemp");
                List<Dictionary<string, JsonElement>> agendasTemp;

                if (string.IsNullOrEmpty(agendasJson))
                {
                    agendasTemp = new List<Dictionary<string, JsonElement>>();
                }
                else
                {
                    agendasTemp = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(agendasJson);
                }

                
                foreach (var agendaExistente in agendasTemp)
                {
                    if (agendaExistente.ContainsKey("sesionId") &&
                        agendaExistente["sesionId"].GetInt32() == sesionId)
                    {
                        DateTime existenteInicio = DateTime.Parse(agendaExistente["horaInicio"].GetString());
                        DateTime existenteFin = DateTime.Parse(agendaExistente["horaFin"].GetString());

                        if (HorariosSeSuperponen(horaInicioUtc, horaFinUtc, existenteInicio, existenteFin))
                        {
                            string nombreConflicto = agendaExistente["nombre"].GetString();
                            

                            return Json(new
                            {
                                success = false,
                                conflicto = true,
                                mensaje = $"Conflicto de horario con: {nombreConflicto} ({existenteInicio:HH:mm} - {existenteFin:HH:mm})"
                            });
                        }
                    }
                }

                

                var actividadesPermanentes = await (
                    from a in _elOlivoDbContext.actividad
                    join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                    where a.sesionid.HasValue
                          && a.activo == true
                          && a.hora_inicio.HasValue
                          && a.hora_fin.HasValue
                          && s.fecha_inicio.HasValue
                          && s.fecha_inicio.Value.Date == fechaSesion
                    select new
                    {
                        a.nombre,
                        a.hora_inicio,
                        a.hora_fin,
                        SesionTitulo = s.titulo
                    }
                ).ToListAsync();




                foreach (var actividadPermanente in actividadesPermanentes)
                {
                    if (HorariosSeSuperponen(
                        horaInicioUtc, horaFinUtc,
                        actividadPermanente.hora_inicio.Value, actividadPermanente.hora_fin.Value))
                    {
                        

                        return Json(new
                        {
                            success = false,
                            conflicto = true,
                            mensaje = $"Conflicto de horario con actividad guardada: {actividadPermanente.nombre} ({actividadPermanente.hora_inicio:HH:mm} - {actividadPermanente.hora_fin:HH:mm})"
                        });
                    }
                }

               
                var nuevaAgenda = new Dictionary<string, JsonElement>
            {
                { "IdTemp", JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()) },
                { "sesionId", JsonSerializer.SerializeToElement(sesionId) },
                { "nombre", JsonSerializer.SerializeToElement(agenda["nombre"].GetString()) },
                { "descripcion", JsonSerializer.SerializeToElement(agenda.ContainsKey("descripcion") ? agenda["descripcion"].GetString() : "") },
                { "tipoActividadIdTemp", JsonSerializer.SerializeToElement(agenda.ContainsKey("tipoActividadId") ? agenda["tipoActividadId"].GetString() : "") },
                { "horaInicio", JsonSerializer.SerializeToElement(horaInicioUtc.ToString("yyyy-MM-dd HH:mm:ss")) },
                { "horaFin", JsonSerializer.SerializeToElement(horaFinUtc.ToString("yyyy-MM-dd HH:mm:ss")) },
                { "ponenteId", JsonSerializer.SerializeToElement(agenda["ponenteId"].GetInt32()) },
                { "ponenteCorreo", JsonSerializer.SerializeToElement(agenda.ContainsKey("ponenteCorreo") ? agenda["ponenteCorreo"].GetString() : "") }
            };

                agendasTemp.Add(nuevaAgenda);

               
                HttpContext.Session.SetString("AgendasTemp", JsonSerializer.Serialize(agendasTemp));

               
                var agendasDeSesion = agendasTemp
                    .Where(a => a.ContainsKey("sesionId") && a["sesionId"].GetInt32() == sesionId)
                    .Select(a => new
                    {
                        IdTemp = a["IdTemp"].GetString(),
                        nombre = a["nombre"].GetString(),
                        horaInicio = a["horaInicio"].GetString(),
                        horaFin = a["horaFin"].GetString()
                    })
                    .ToList();

              

                return Json(new
                {
                    success = true,
                    total = agendasTemp.Count,
                    agendas = agendasDeSesion
                });
            }
            catch (Exception ex)
            {
               
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        private bool HorariosSeSuperponen(DateTime inicio1, DateTime fin1, DateTime inicio2, DateTime fin2)
        {
            return inicio1 < fin2 && fin1 > inicio2;
        }


        [HttpGet]
        public IActionResult ObtenerAgendasTemp()
        {
            var agendasJson = HttpContext.Session.GetString("AgendasTemp");
            var agendasTemp = string.IsNullOrEmpty(agendasJson)
                ? new List<Dictionary<string, object>>()
                : JsonSerializer.Deserialize<List<Dictionary<string, object>>>(agendasJson);

            return Json(new { success = true, agendas = agendasTemp });
        }


       
        [HttpGet]
        public IActionResult ValidarPonente(string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                return Json(new { existe = false });
            }

           
            var usuario = _elOlivoDbContext.usuario
                .FirstOrDefault(u => u.email == correo && u.activo == true);

            if (usuario != null)
            {
                return Json(new
                {
                    existe = true,
                    usuarioId = usuario.usuarioid,
                    nombreCompleto = $"{usuario.nombre} {usuario.apellido}"
                });
            }

            return Json(new { existe = false });
        }


        [HttpPost]
        public async Task<IActionResult> PublicarEventoCompleto()
        {
            try
            {
                var eventoId = HttpContext.Session.GetInt32("EventoEnPlanificacionId");

                if (eventoId == null)
                    return Json(new { success = false, mensaje = "No se encontró el evento en planificación" });

               
                var tiposJson = HttpContext.Session.GetString("TiposActividadTemp");
                var mapeoTipos = new Dictionary<string, int>(); 

                if (!string.IsNullOrEmpty(tiposJson))
                {
                    var tiposTemp = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(tiposJson);
                    Console.WriteLine($"📋 Tipos de actividad a guardar: {tiposTemp.Count}");

                    foreach (var tipoTemp in tiposTemp)
                    {
                        var nuevoTipo = new tipoactividad
                        {
                            nombre = tipoTemp["nombre"].GetString(),
                            descripcion = tipoTemp.ContainsKey("descripcion") ? tipoTemp["descripcion"].GetString() : "",
                            activo = true
                        };

                        _elOlivoDbContext.tipoactividad.Add(nuevoTipo);
                        await _elOlivoDbContext.SaveChangesAsync();

                        
                        string idTemp = tipoTemp["IdTemp"].GetString();
                        mapeoTipos[idTemp] = nuevoTipo.tipoactividadid;

                        Console.WriteLine($"✅ Tipo guardado: {nuevoTipo.nombre} (Temp: {idTemp} -> Real: {nuevoTipo.tipoactividadid})");
                    }
                }

               
                var agendasJson = HttpContext.Session.GetString("AgendasTemp");
                var agendasGuardadas = 0;

                if (!string.IsNullOrEmpty(agendasJson))
                {
                    var agendasTemp = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(agendasJson);
                    Console.WriteLine($"📋 Agendas a guardar: {agendasTemp.Count}");

                    foreach (var agendaTemp in agendasTemp)
                    {
                       
                        string tipoActividadIdTemp = agendaTemp.ContainsKey("tipoActividadIdTemp")
                            ? agendaTemp["tipoActividadIdTemp"].GetString()
                            : null;

                        int? tipoActividadIdReal = null;

                        if (!string.IsNullOrEmpty(tipoActividadIdTemp) && mapeoTipos.ContainsKey(tipoActividadIdTemp))
                        {
                            tipoActividadIdReal = mapeoTipos[tipoActividadIdTemp];
                           
                        }

                        var nuevaActividad = new actividad
                        {
                            nombre = agendaTemp["nombre"].GetString(),
                            descripcion = agendaTemp.ContainsKey("descripcion") ? agendaTemp["descripcion"].GetString() : "",
                            sesionid = agendaTemp["sesionId"].GetInt32(),
                            ponenteid = agendaTemp["ponenteId"].GetInt32(),
                            tipoactividadid = tipoActividadIdReal,
                            hora_inicio = DateTime.SpecifyKind(
                                DateTime.Parse(agendaTemp["horaInicio"].GetString()),
                                DateTimeKind.Utc
                            ),
                            hora_fin = DateTime.SpecifyKind(
                                DateTime.Parse(agendaTemp["horaFin"].GetString()),
                                DateTimeKind.Utc
                            ),
                            activo = true
                        };

                        _elOlivoDbContext.actividad.Add(nuevaActividad);
                        agendasGuardadas++;

                        
                    }

                    await _elOlivoDbContext.SaveChangesAsync();
                   
                }

                
                HttpContext.Session.Remove("TiposActividadTemp");
                HttpContext.Session.Remove("AgendasTemp");
                HttpContext.Session.Remove("EventoEnPlanificacionId");

               

                return Json(new
                {
                    success = true,
                    mensaje = $"Evento publicado exitosamente con {mapeoTipos.Count} tipos de actividad y {agendasGuardadas} agendas",
                    tiposGuardados = mapeoTipos.Count,
                    agendasGuardadas = agendasGuardadas
                });
            }
            catch (Exception ex)
            {
               
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> LimpiarSesionesTemp()
        {
            try
            {
                
                HttpContext.Session.Remove("SesionesTemporal");

              
                return Json(new { success = true, message = "Sesiones temporales limpiadas" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Nueva acción para redirigir desde sesionid a eventoid
        public async Task<IActionResult> RedirigirAVerSesiones(int id) // id es sesionid
        {
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null)
                return RedirectToAction("Login", "Account");

            // Obtener el eventoid de la sesión
            var sesion = await _elOlivoDbContext.sesion
                .Where(s => s.sesionid == id)
                .Select(s => new { s.sesionid, s.eventoid })
                .FirstOrDefaultAsync();

            if (sesion == null || !sesion.eventoid.HasValue)
            {
                TempData["ErrorMessage"] = "Sesión no encontrada";
                return RedirectToAction("GestionEventos");
            }

            // Verificar que el evento pertenezca al administrador
            var eventoPerteneceAlAdmin = await _elOlivoDbContext.evento
                .AnyAsync(e => e.eventoid == sesion.eventoid.Value && e.usuarioadminid == usuarioAdminId);

            if (!eventoPerteneceAlAdmin)
            {
                TempData["ErrorMessage"] = "No tiene permisos para ver este evento";
                return RedirectToAction("GestionEventos");
            }

            // Redirigir a VerSesiones con el eventoid
            return RedirectToAction("VerSesiones", new { id = sesion.eventoid.Value });
        }

        //


        public async Task<IActionResult> GestionInscripciones(string? search)
        {
            try
            {
                
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

        public async Task<IActionResult> GestionAsistencias(int eventoId, int sesionId)
        {
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null)
                return RedirectToAction("Login", "Account");

            var evento = await _elOlivoDbContext.evento.FindAsync(eventoId);
            var sesion = await _elOlivoDbContext.sesion.FindAsync(sesionId);

            if (evento == null || evento.usuarioadminid != usuarioAdminId)
                return RedirectToAction("GestionInscripciones");

            if (sesion == null || sesion.eventoid != eventoId)
                return RedirectToAction("VerSesiones", new { id = eventoId });

            // VERIFICACIÓN: Si el evento está Finalizado (estadoid = 4), no permitir edición
            bool eventoFinalizado = evento.estadoid == 4;
            ViewBag.EventoFinalizado = eventoFinalizado;

            ViewBag.Evento = evento;
            ViewBag.Sesion = sesion;
            ViewBag.EventoId = eventoId;

            // 2. OBTENER INSCRITOS EN EL EVENTO (SOLO CONFIRMADOS: estadoid == 2)
            var inscritos = await (from i in _elOlivoDbContext.inscripcion
                                   join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                   where i.eventoid == eventoId && i.estadoid == 2
                                   select new
                                   {
                                       i.inscripcionid,
                                       u.usuarioid,
                                       NombreCompleto = u.nombre + " " + u.apellido,
                                       u.email
                                   }).ToListAsync();

            // 3. Obtener el estado de asistencia actual para esta sesión
            var asistenciasActuales = await _elOlivoDbContext.asistencia
                .Where(a => a.sesionid == sesionId && a.usuarioid.HasValue)
                .Select(a => a.usuarioid.Value)
                .ToHashSetAsync();

            // 4. Combinar la lista de inscritos con su estado de asistencia
            var modeloAsistencia = inscritos.Select(u => new
            {
                u.inscripcionid,
                u.usuarioid,
                u.NombreCompleto,
                u.email,
                Asistio = asistenciasActuales.Contains(u.usuarioid)
            }).ToList();

            // 5. Manejo de alumnos no encontrados
            if (!modeloAsistencia.Any())
            {
                ViewBag.MensajeNoAlumnos = $"No hay usuarios con inscripción CONFIRMADA (Estado ID 2) para el evento '{evento.nombre}'.";
            }

            ViewData["Title"] = $"Asistencia: {sesion.titulo}";

            return View(modeloAsistencia);
        }

        // NUEVA ACCIÓN POST para guardar la asistencia
        [HttpPost]
        public async Task<IActionResult> GuardarAsistencias(int sesionId, int eventoId, List<int>? asistieron)
        {
            // VERIFICACIÓN: Si el evento está Finalizado, no permitir guardar cambios
            var evento = await _elOlivoDbContext.evento.FindAsync(eventoId);
            if (evento == null || evento.estadoid == 4)
            {
                TempData["MensajeError"] = "No se puede modificar la asistencia porque el evento está FINALIZADO.";
                return RedirectToAction("GestionAsistencias", new { eventoId = eventoId, sesionId = sesionId });
            }

            // 1. Obtener asistencias EXISTENTES para esta sesión
            var asistenciasSesion = await _elOlivoDbContext.asistencia
                .Where(a => a.sesionid == sesionId)
                .ToListAsync();

            // Usamos el set de usuarios que DEBERÍAN haber asistido (enviado desde el formulario)
            var asistieronSet = asistieron?.ToHashSet() ?? new HashSet<int>();

            // 2. Determinar cuáles eliminar (Estaban marcados, ahora no)
            var aEliminar = asistenciasSesion
                .Where(a => a.usuarioid.HasValue && !asistieronSet.Contains(a.usuarioid.Value))
                .ToList();

            // 3. Determinar cuáles agregar (Están en la lista, no estaban en DB)
            var existentesSet = asistenciasSesion
                .Where(a => a.usuarioid.HasValue)
                .Select(a => a.usuarioid.Value)
                .ToHashSet();

            var aAgregar = asistieronSet
                .Where(usuarioId => !existentesSet.Contains(usuarioId))
                .Select(usuarioId => new asistencia
                {
                    sesionid = sesionId,
                    usuarioid = usuarioId,
                    fecha_hora_registro = DateTime.UtcNow
                })
                .ToList();

            // 4. Ejecutar cambios
            _elOlivoDbContext.asistencia.RemoveRange(aEliminar);
            _elOlivoDbContext.asistencia.AddRange(aAgregar);

            await _elOlivoDbContext.SaveChangesAsync();

            TempData["MensajeExito"] = "Asistencia guardada correctamente.";
            return RedirectToAction("GestionAsistencias", new { eventoId = eventoId, sesionId = sesionId });
        }


        [HttpPost]
        public async Task<IActionResult> CambiarEstadoEvento(int eventoId, int estadoId)
        {
            // Usar una transacción para asegurar la consistencia de datos
            using var transaction = await _elOlivoDbContext.Database.BeginTransactionAsync();

            try
            {
                int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                // Verificar que el evento pertenece al administrador
                var evento = await _elOlivoDbContext.evento
                    .FirstOrDefaultAsync(e => e.eventoid == eventoId && e.usuarioadminid == usuarioAdminId);

                if (evento == null)
                    return Json(new { success = false, mensaje = "Evento no encontrado o no autorizado" });

                // VERIFICACIÓN: Si el evento ya está Cancelado (5) o Finalizado (4), no permitir cambios
                if (evento.estadoid == 4 || evento.estadoid == 5)
                {
                    string estadoActual = evento.estadoid == 4 ? "Finalizado" : "Cancelado";
                    return Json(new
                    {
                        success = false,
                        mensaje = $"No se puede cambiar el estado de un evento {estadoActual.ToLower()}. El evento ya está {estadoActual.ToLower()}."
                    });
                }

                // Verificar que el estado existe
                var estado = await _elOlivoDbContext.estado
                    .FirstOrDefaultAsync(e => e.estadoid == estadoId);

                if (estado == null)
                    return Json(new { success = false, mensaje = "Estado no válido" });

                // Cambiar el estado del evento
                evento.estadoid = estadoId;

                int inscripcionesCanceladas = 0;
                int actividadesDesactivadas = 0;
                int sesionesDesactivadas = 0;

                // Si el evento se cancela (estadoId = 5), cancelar inscripciones y desactivar actividades/sesiones
                if (estadoId == 5) // Cancelado
                {
                    // 1. Cancelar todas las inscripciones del evento (enfoque tradicional)
                    var inscripciones = await _elOlivoDbContext.inscripcion
                        .Where(i => i.eventoid == eventoId)
                        .ToListAsync();

                    inscripcionesCanceladas = inscripciones.Count;

                    foreach (var inscripcion in inscripciones)
                    {
                        inscripcion.estadoid = 1; // Cancelada
                    }

                    // 2. Desactivar todas las sesiones del evento
                    var sesiones = await _elOlivoDbContext.sesion
                        .Where(s => s.eventoid == eventoId && s.activo == true)
                        .ToListAsync();

                    sesionesDesactivadas = sesiones.Count;

                    foreach (var sesion in sesiones)
                    {
                        sesion.activo = false;
                    }

                    // 3. Desactivar todas las actividades de las sesiones del evento
                    var sesionesIds = sesiones.Select(s => s.sesionid).ToList();

                    if (sesionesIds.Any())
                    {
                        var actividades = await _elOlivoDbContext.actividad
                            .Where(a => sesionesIds.Contains(a.sesionid.Value) && a.activo == true)
                            .ToListAsync();

                        actividadesDesactivadas = actividades.Count;

                        foreach (var actividad in actividades)
                        {
                            actividad.activo = false;
                        }
                    }
                }

                // Guardar todos los cambios
                await _elOlivoDbContext.SaveChangesAsync();

                // Confirmar la transacción
                await transaction.CommitAsync();

                // Mensaje según el estado
                string mensaje = estadoId switch
                {
                    3 => "Evento marcado como Abierto",
                    4 => "Evento marcado como Finalizado",
                    5 => $"Evento cancelado. Se cancelaron {inscripcionesCanceladas} inscripciones, se desactivaron {sesionesDesactivadas} sesiones y {actividadesDesactivadas} actividades.",
                    6 => "Evento marcado como Inscripción Cerrada",
                    _ => "Estado del evento actualizado"
                };

                return Json(new { success = true, mensaje = mensaje });
            }
            catch (Exception ex)
            {
                // Revertir la transacción en caso de error
                await transaction.RollbackAsync();

                _logger.LogError(ex, "Error al cambiar estado del evento {EventoId} a {EstadoId}", eventoId, estadoId);
                return Json(new { success = false, mensaje = $"Error interno: {ex.Message}" });
            }
        }


        public IActionResult VerPerfilUsuario(int id)
        {
            // Verificación de sesión de administrador (ya está en [Autenticacion])
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null)
                return RedirectToAction("Login", "Account");

            // NOTA: Se obtiene el perfil de CUALQUIER usuario, por eso no se usa 'usuarioAdminId'
            var usuario = (from u in _elOlivoDbContext.usuario
                               // Unir con la tabla rol
                           join r in _elOlivoDbContext.rol on u.rolid equals r.rolid
                           where u.usuarioid == id
                           select new
                           {
                               u.usuarioid,
                               u.nombre,
                               u.apellido,
                               u.email,
                               u.telefono,
                               u.institucion,
                               u.pais,
                               u.foto_url,
                               u.fecha_registro,
                               u.rolid,
                               RolNombre = r.nombre
                           })
                   .FirstOrDefault();

            if (usuario == null)
            {
                TempData["MensajeError"] = "Usuario no encontrado.";
                return RedirectToAction("GestionUsuarios");
            }

            ViewBag.Usuario = usuario;
            return View("PerfilUsuarioSoloLectura"); // <-- Carga la nueva vista simplificada
        }

        [HttpPost]
        public async Task<IActionResult> CancelarInscripcion(int inscripcionId)
        {
            try
            {
                int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return RedirectToAction("Login", "Account");

                // 1. Encontrar la inscripción a cancelar
                var inscripcion = await _elOlivoDbContext.inscripcion
                                    .FirstOrDefaultAsync(i => i.inscripcionid == inscripcionId);

                if (inscripcion == null)
                {
                    TempData["MensajeError"] = "Inscripción no encontrada.";
                    return RedirectToAction("GestionUsuarios"); // Redirige a la lista general
                }

                // 2. Opcional: Verificar que el evento pertenezca al administrador
                var evento = await _elOlivoDbContext.evento.FindAsync(inscripcion.eventoid);
                if (evento == null || evento.usuarioadminid != usuarioAdminId)
                {
                    // Aunque se encontró la inscripción, no es de un evento del admin logueado
                    TempData["MensajeError"] = "No tienes permiso para modificar esta inscripción.";
                    return RedirectToAction("GestionUsuarios");
                }

                // 3. Modificar el estado: 1 = Cancelada (según su indicación)
                inscripcion.estadoid = 1;
                await _elOlivoDbContext.SaveChangesAsync();

                TempData["MensajeExito"] = "Inscripción cancelada correctamente.";

                // Redirige de vuelta a la lista de usuarios/inscripciones
                return RedirectToAction("GestionUsuarios", new { eventId = inscripcion.eventoid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar inscripción {Id}", inscripcionId);
                TempData["MensajeError"] = "Error interno al cancelar la inscripción.";
                return RedirectToAction("GestionUsuarios");
            }
        }

        public async Task<IActionResult> ExportarExcelUsuarios(int eventId, string? search, int? estadoInscripcion, string? asistenciaApto, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // 1. LÓGICA DE FILTRADO (Manteniendo la lógica anterior)
            int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null) return RedirectToAction("Login", "Account");

            var evento = await _elOlivoDbContext.evento.FindAsync(eventId);
            if (evento == null || evento.usuarioadminid != usuarioAdminId) return NotFound("Evento no encontrado o no autorizado.");

            var umbralApto = 75.0;

            // Paso 2: Obtener las inscripciones
            var inscripcionesQuery = from i in _elOlivoDbContext.inscripcion
                                     join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                     join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                     join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                     where i.eventoid == eventId
                                     select new
                                     {
                                         InscripcionId = i.inscripcionid,
                                         UsuarioId = u.usuarioid,
                                         NombreUsuario = u.nombre + " " + u.apellido,
                                         u.email,
                                         u.telefono,
                                         i.fecha_inscripcion,
                                         EstadoInscripcionId = i.estadoid,
                                         EstadoNombreInscripcion = es.nombre,
                                         i.eventoid
                                     };

            // Paso 3: Aplicar filtros
            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                inscripcionesQuery = inscripcionesQuery.Where(x =>
                    x.NombreUsuario.ToLower().Contains(searchLower) ||
                    x.email.ToLower().Contains(searchLower) ||
                    x.telefono.Contains(searchLower)
                );
            }
            if (estadoInscripcion.HasValue) inscripcionesQuery = inscripcionesQuery.Where(x => x.EstadoInscripcionId == estadoInscripcion.Value);
            if (fechaDesde.HasValue) inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion.HasValue && x.fecha_inscripcion.Value.Date >= fechaDesde.Value.Date);
            if (fechaHasta.HasValue) inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion.HasValue && x.fecha_inscripcion.Value.Date <= fechaHasta.Value.Date);

            var todasLasInscripciones = await inscripcionesQuery.ToListAsync();

            // Paso 4: Obtener datos de asistencia (JOIN corregido)
            var totalSesiones = await _elOlivoDbContext.sesion.CountAsync(s => s.eventoid == eventId);

            var asistenciasEventos = await (from a in _elOlivoDbContext.asistencia
                                            join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                            where s.eventoid == eventId && a.usuarioid.HasValue
                                            select new
                                            {
                                                UsuarioId = a.usuarioid.Value,
                                                SesionId = a.sesionid.Value
                                            }).ToListAsync();

            var usuariosConAsistencia = new List<dynamic>();

            // Paso 5: Calcular asistencia y crear objeto dinámico
            foreach (var inscripcion in todasLasInscripciones)
            {
                int asistencias = asistenciasEventos.Count(a => a.UsuarioId == inscripcion.UsuarioId);
                double porcentajeAsistencia = totalSesiones > 0 ? Math.Round((asistencias * 100.0) / totalSesiones, 1) : 0;
                bool esApto = porcentajeAsistencia >= umbralApto;

                var usuarioDetalle = new
                {
                    inscripcion.UsuarioId,
                    inscripcion.NombreUsuario,
                    inscripcion.email,
                    inscripcion.telefono,
                    FechaInscripcion = inscripcion.fecha_inscripcion?.ToString("dd/MM/yyyy"),
                    // La propiedad en el objeto dinámico se llama 'EstadoInscripcion'
                    EstadoInscripcion = inscripcion.EstadoNombreInscripcion,
                    TotalSesiones = totalSesiones,
                    AsistenciasRegistradas = asistencias,
                    PorcentajeAsistencia = porcentajeAsistencia.ToString("F1") + "%",
                    EstadoFinal = esApto ? "APTO" : "NO APTO"
                };

                if (string.IsNullOrEmpty(asistenciaApto) || (asistenciaApto == "Apto" && esApto) || (asistenciaApto == "No Apto" && !esApto))
                {
                    usuariosConAsistencia.Add(usuarioDetalle);
                }
            }

            if (!usuariosConAsistencia.Any()) return NotFound("No hay usuarios que coincidan con los filtros para exportar.");

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("UsuariosInscritos");

                // Encabezados
                worksheet.Cells["A1"].Value = $"Reporte de Inscripciones - Evento: {evento.nombre}";
                worksheet.Cells["A1:L1"].Merge = true;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.Font.Size = 14;

                // Cargar datos a partir de la fila 3
                worksheet.Cells["A3"].LoadFromCollection(usuariosConAsistencia.Select(u => new
                {
                    u.UsuarioId,
                    u.NombreUsuario,
                    u.email,
                    u.telefono,
                    u.FechaInscripcion,
                    u.EstadoInscripcion, // <-- CORREGIDO: Se accede con el nombre de propiedad 'EstadoInscripcion'
                    u.TotalSesiones,
                    u.AsistenciasRegistradas,
                    u.PorcentajeAsistencia,
                    u.EstadoFinal
                }), true);

                worksheet.Cells.AutoFitColumns();

                // 3. RETORNAR EL ARCHIVO
                var content = package.GetAsByteArray();
                return File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Reporte_Usuarios_{evento.nombre}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
        }

        public async Task<IActionResult> ExportarExcelAsistencias(int eventoId, int sesionId)
        {
            // 1. REPLICAR LÓGICA DE OBTENCIÓN DE ASISTENCIAS (DE GestionAsistencias)
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null) return RedirectToAction("Login", "Account");

            var evento = await _elOlivoDbContext.evento.FindAsync(eventoId);
            var sesion = await _elOlivoDbContext.sesion.FindAsync(sesionId);

            if (evento == null || evento.usuarioadminid != usuarioAdminId || sesion == null || sesion.eventoid != eventoId)
                return NotFound("Datos inválidos.");

            // Obtener datos de asistencia...
            var inscritos = await (from i in _elOlivoDbContext.inscripcion
                                   join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                   where i.eventoid == eventoId && i.estadoid == 2
                                   select new { u.usuarioid, NombreCompleto = u.nombre + " " + u.apellido, u.email }).ToListAsync();

            var asistenciasActuales = await _elOlivoDbContext.asistencia
                .Where(a => a.sesionid == sesionId && a.usuarioid.HasValue)
                .Select(a => a.usuarioid.Value)
                .ToHashSetAsync();

            var modeloAsistencia = inscritos.Select(u => new
            {
                u.usuarioid,
                u.NombreCompleto,
                u.email,
                Asistio = asistenciasActuales.Contains(u.usuarioid) ? "SI" : "NO"
            }).ToList();

            if (!modeloAsistencia.Any()) return NotFound("No hay datos para exportar.");


            using (var package = new ExcelPackage()) // Volver al constructor sin parámetros
            {
                var worksheet = package.Workbook.Worksheets.Add("Asistencias");

                // Títulos
                worksheet.Cells["A1"].Value = "Reporte de Asistencia";
                worksheet.Cells["A2"].Value = $"Evento: {evento.nombre}";
                worksheet.Cells["A3"].Value = $"Sesión: {sesion.titulo}";
                worksheet.Cells["A1:D1"].Merge = true;
                worksheet.Cells["A2:D2"].Merge = true;
                worksheet.Cells["A3:D3"].Merge = true;
                worksheet.Cells["A1:A3"].Style.Font.Bold = true;

                // Cargar datos a partir de la fila 5
                worksheet.Cells["A5"].LoadFromCollection(modeloAsistencia.Select(u => new
                {
                    u.usuarioid,
                    u.NombreCompleto,
                    u.email,
                    u.Asistio
                }), true);

                worksheet.Cells.AutoFitColumns();

                // 3. RETORNAR EL ARCHIVO
                var content = package.GetAsByteArray();
                return File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Reporte_Asistencia_{sesionId}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
        }

        public async Task<IActionResult> ExportarPDFUsuarios(int eventId, string? search, int? estadoInscripcion, string? asistenciaApto, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            // 1. REPLICAR LÓGICA DE FILTRADO (Igual que ExportarExcelUsuarios)
            int? usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null) return RedirectToAction("Login", "Account");

            var evento = await _elOlivoDbContext.evento.FindAsync(eventId);
            if (evento == null || evento.usuarioadminid != usuarioAdminId) return NotFound("Evento no encontrado o no autorizado.");

            // Usar la misma lógica de obtención de datos y filtros que en ExportarExcelUsuarios
            var umbralApto = 75.0;

            var inscripcionesQuery = from i in _elOlivoDbContext.inscripcion
                                     join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                     join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                     join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                     where i.eventoid == eventId
                                     select new
                                     {
                                         InscripcionId = i.inscripcionid,
                                         UsuarioId = u.usuarioid,
                                         NombreUsuario = u.nombre + " " + u.apellido,
                                         u.email,
                                         u.telefono,
                                         i.fecha_inscripcion,
                                         EstadoInscripcionId = i.estadoid,
                                         EstadoNombreInscripcion = es.nombre,
                                         i.eventoid
                                     };

            if (!string.IsNullOrEmpty(search))
            {
                string searchLower = search.ToLower();
                inscripcionesQuery = inscripcionesQuery.Where(x =>
                    x.NombreUsuario.ToLower().Contains(searchLower) ||
                    x.email.ToLower().Contains(searchLower) ||
                    x.telefono.Contains(searchLower)
                );
            }

            if (estadoInscripcion.HasValue)
            {
                inscripcionesQuery = inscripcionesQuery.Where(x => x.EstadoInscripcionId == estadoInscripcion.Value);
            }

            if (fechaDesde.HasValue)
            {
                inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion.HasValue && x.fecha_inscripcion.Value.Date >= fechaDesde.Value.Date);
            }

            if (fechaHasta.HasValue)
            {
                inscripcionesQuery = inscripcionesQuery.Where(x => x.fecha_inscripcion.HasValue && x.fecha_inscripcion.Value.Date <= fechaHasta.Value.Date);
            }

            var todasLasInscripciones = await inscripcionesQuery.ToListAsync();

            var totalSesiones = await _elOlivoDbContext.sesion.CountAsync(s => s.eventoid == eventId);

            var asistenciasEventos = await (from a in _elOlivoDbContext.asistencia
                                            join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                            where s.eventoid == eventId && a.usuarioid.HasValue
                                            select new
                                            {
                                                UsuarioId = a.usuarioid.Value,
                                                SesionId = a.sesionid.Value
                                            }).ToListAsync();

            var usuariosConAsistencia = new List<dynamic>();

            foreach (var inscripcion in todasLasInscripciones)
            {
                // Conteo usando la colección asistenciasEventos filtrada por JOIN
                int asistencias = asistenciasEventos
                    .Count(a => a.UsuarioId == inscripcion.UsuarioId);

                double porcentajeAsistencia = totalSesiones > 0 ? Math.Round((asistencias * 100.0) / totalSesiones, 1) : 0;
                bool esApto = porcentajeAsistencia >= umbralApto;

                var usuarioDetalle = new
                {
                    inscripcion.UsuarioId,
                    inscripcion.NombreUsuario,
                    inscripcion.email,
                    FechaInscripcion = inscripcion.fecha_inscripcion?.ToString("dd/MM/yyyy"),
                    EstadoNombreInscripcion = inscripcion.EstadoNombreInscripcion,
                    Asistencias = $"{asistencias}/{totalSesiones}",
                    PorcentajeAsistencia = porcentajeAsistencia.ToString("F1") + "%",
                    EstadoFinal = esApto ? "APTO" : "NO APTO"
                };

                if (string.IsNullOrEmpty(asistenciaApto) ||
                    (asistenciaApto == "Apto" && esApto) ||
                    (asistenciaApto == "No Apto" && !esApto))
                {
                    usuariosConAsistencia.Add(usuarioDetalle);
                }
            }

            if (!usuariosConAsistencia.Any()) return NotFound("No hay usuarios que coincidan con los filtros para exportar.");

            // 2. GENERAR EL PDF CON QUESTPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);

                    page.Header()
                        .Column(headerCol =>
                        {
                            headerCol.Item().Text("Reporte de Usuarios Inscritos").FontSize(18).SemiBold().FontColor(Colors.Blue.Medium).AlignCenter();
                            headerCol.Item().PaddingBottom(5).Text($"Evento: {evento.nombre}").FontSize(12).SemiBold();
                            // Muestra los filtros aplicados
                            if (!string.IsNullOrEmpty(search) || estadoInscripcion.HasValue || !string.IsNullOrEmpty(asistenciaApto))
                            {
                                headerCol.Item().Text(text =>
                                {
                                    text.Span("Filtros Aplicados: ").SemiBold().FontSize(8);
                                    if (!string.IsNullOrEmpty(search)) text.Span($"Busqueda='{search}' | ").FontSize(8);
                                    if (estadoInscripcion.HasValue) text.Span($"Estado Inscripción='{estadoInscripcion.Value}' | ").FontSize(8);
                                    if (!string.IsNullOrEmpty(asistenciaApto)) text.Span($"Apto Asistencia='{asistenciaApto}'").FontSize(8);
                                });
                            }
                        });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(5);

                        // Tabla de datos
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f); // Nombre
                                columns.RelativeColumn(2.5f); // Email
                                columns.RelativeColumn(1.5f); // Fecha Insc.
                                columns.RelativeColumn(1.5f); // Estado Insc.
                                columns.RelativeColumn(1f); // Asist.
                                columns.RelativeColumn(1f); // % Asist.
                                columns.RelativeColumn(1f); // Estado Final
                            });

                            // Estilo de encabezados
                            TextStyle headerStyle = TextStyle.Default.SemiBold().FontSize(8).BackgroundColor(Colors.Grey.Lighten3);
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(2).Text("Nombre").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Email").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Fecha Insc.").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Estado Insc.").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Asistencias").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("% Asistencia").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Estado Final").Style(headerStyle);
                            });

                            // Datos
                            foreach (var u in usuariosConAsistencia)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.NombreUsuario).FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.email).FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.FechaInscripcion).FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.EstadoNombreInscripcion).FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.Asistencias).FontSize(8);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.PorcentajeAsistencia).FontSize(8);

                                // Estado Final con color
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text((string)u.EstadoFinal).FontSize(8)
        .FontColor((string)u.EstadoFinal == "APTO" ? Colors.Green.Darken2 : Colors.Red.Darken2)
        .SemiBold();
                            }
                        });
                    });

                    page.Footer()
                        .AlignRight()
                        .Text(x => { x.CurrentPageNumber().FontSize(8); x.Span(" / ").FontSize(8); x.TotalPages().FontSize(8); });
                });
            });

            // 3. RETORNAR EL ARCHIVO
            byte[] pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Reporte_Usuarios_{evento.nombre}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

        public async Task<IActionResult> ExportarPDFAsistencias(int eventoId, int sesionId)
        {
            // 1. REPLICAR LÓGICA DE OBTENCIÓN DE ASISTENCIAS (igual que para Excel)
            var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
            if (usuarioAdminId == null) return RedirectToAction("Login", "Account");

            var evento = await _elOlivoDbContext.evento.FindAsync(eventoId);
            var sesion = await _elOlivoDbContext.sesion.FindAsync(sesionId);

            if (evento == null || evento.usuarioadminid != usuarioAdminId || sesion == null || sesion.eventoid != eventoId)
                return NotFound("Datos inválidos.");

            // Obtener datos de asistencia...
            var inscritos = await (from i in _elOlivoDbContext.inscripcion
                                   join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                   where i.eventoid == eventoId && i.estadoid == 2
                                   select new { u.usuarioid, NombreCompleto = u.nombre + " " + u.apellido, u.email }).ToListAsync();

            var asistenciasActuales = await _elOlivoDbContext.asistencia
                .Where(a => a.sesionid == sesionId && a.usuarioid.HasValue)
                .Select(a => a.usuarioid.Value)
                .ToHashSetAsync();

            var modeloAsistencia = inscritos.Select(u => new
            {
                u.usuarioid,
                u.NombreCompleto,
                u.email,
                Asistio = asistenciasActuales.Contains(u.usuarioid) ? "SI" : "NO"
            }).ToList();

            if (!modeloAsistencia.Any()) return NotFound("No hay datos para exportar.");

            // 2. GENERAR EL PDF CON QUESTPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.Header()
                        .Text("Reporte de Asistencia a Sesión")
                        .FontSize(18).SemiBold().FontColor(Colors.Green.Medium).AlignCenter();

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(5);

                        // Información de la sesión
                        column.Item().Text(text =>
                        {
                            text.Span("Evento: ").SemiBold();
                            text.Span(evento.nombre).FontSize(10);
                        });
                        column.Item().Text(text =>
                        {
                            text.Span("Sesión: ").SemiBold();
                            text.Span(sesion.titulo).FontSize(10);
                        });
                        column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        // Tabla de datos
                        column.Item().Table(table =>
                        {
                            // Propiedades de la tabla
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1); // ID
                                columns.RelativeColumn(4); // Nombre
                                columns.RelativeColumn(3); // Email
                                columns.RelativeColumn(1.5f); // Asistió
                            });

                            // Estilo de encabezados
                            TextStyle headerStyle = TextStyle.Default.SemiBold().FontSize(10).BackgroundColor(Colors.Grey.Lighten3);
                            table.Header(header =>
                            {
                                header.Cell().BorderBottom(1).Padding(2).Text("ID Usuario").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Nombre Completo").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Email").Style(headerStyle);
                                header.Cell().BorderBottom(1).Padding(2).Text("Asistió").Style(headerStyle);
                            });

                            // Datos
                            foreach (var u in modeloAsistencia)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text(u.usuarioid.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text(u.NombreCompleto);
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text(u.email);
                                // Resalta el estado
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text(u.Asistio)
                                    .FontColor(u.Asistio == "SI" ? Colors.Green.Darken2 : Colors.Red.Darken2)
                                    .SemiBold();
                            }
                        });
                    });

                    page.Footer()
                        .AlignRight()
                        .Text(x => { x.CurrentPageNumber().FontSize(8); x.Span(" / ").FontSize(8); x.TotalPages().FontSize(8); });
                });
            });

            // 3. RETORNAR EL ARCHIVO
            byte[] pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", $"Reporte_Asistencia_{sesionId}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        }

        // Agregar al final de AdminController.cs

        // ============================================
        // GESTIÓN DE ACTIVIDADES CON ARCHIVOS Y LINKS
        // ============================================

        public async Task<IActionResult> GestionActividad(int id)
        {
            try
            {
                var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener actividad con información relacionada
                var actividad = await (from a in _elOlivoDbContext.actividad
                                       join u in _elOlivoDbContext.usuario on a.ponenteid equals u.usuarioid
                                       join ta in _elOlivoDbContext.tipoactividad on a.tipoactividadid equals ta.tipoactividadid into taGroup
                                       from ta in taGroup.DefaultIfEmpty()
                                       join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                       join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                       where a.agendaid == id && e.usuarioadminid == usuarioAdminId
                                       select new
                                       {
                                           AgendaId = a.agendaid,
                                           Nombre = a.nombre,
                                           Descripcion = a.descripcion,
                                           Inicio = a.hora_inicio,
                                           Fin = a.hora_fin,
                                           Ponente = u.nombre + " " + u.apellido,
                                           Actividad = ta != null ? ta.nombre : "Sin tipo",
                                           Estado = a.activo,
                                           SesionId = s.sesionid,
                                           EventoId = e.eventoid
                                       }).FirstOrDefaultAsync();

                if (actividad == null)
                {
                    TempData["ErrorMessage"] = "Actividad no encontrada o no tiene permisos para editarla";
                    return RedirectToAction("VerSesiones");
                }

                // Obtener materiales (archivos y links)
                var materiales = await (from m in _elOlivoDbContext.material
                                        join ta in _elOlivoDbContext.tipo_archivo on m.tipo_archivoid equals ta.tipo_archivoid
                                        where m.agendaid == id
                                        orderby m.fecha_subida descending
                                        select new
                                        {
                                            m.materialid,
                                            m.nombre,
                                            m.url_archivo,
                                            m.fecha_subida,
                                            m.publico,
                                            TipoNombre = ta.nombre,
                                            EsLink = ta.nombre == "Link" || ta.nombre == "URL"
                                        }).ToListAsync();

                ViewBag.Actividad = actividad;
                ViewBag.Materiales = materiales;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar actividad {Id}", id);
                return Content(ex.ToString());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AgregarLink(int agendaid, string nombre, string url)
        {
            try
            {
                var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                // Validar que la actividad pertenece a un evento del admin
                var esValido = await (from a in _elOlivoDbContext.actividad
                                      join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                      join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                      where a.agendaid == agendaid && e.usuarioadminid == usuarioAdminId
                                      select e.eventoid).AnyAsync();

                if (!esValido)
                    return Json(new { success = false, mensaje = "No tiene permisos para editar esta actividad" });

                // Obtener o crear el tipo de archivo "Link"
                var tipoLink = await _elOlivoDbContext.tipo_archivo
                    .FirstOrDefaultAsync(t => t.nombre == "Link" || t.nombre == "URL");

                if (tipoLink == null)
                {
                    tipoLink = new tipo_archivo { nombre = "Link" };
                    _elOlivoDbContext.tipo_archivo.Add(tipoLink);
                    await _elOlivoDbContext.SaveChangesAsync();
                }

                // Crear el material
                var nuevoMaterial = new material
                {
                    nombre = nombre,
                    url_archivo = url,
                    tipo_archivoid = tipoLink.tipo_archivoid,
                    fecha_subida = DateTime.UtcNow,
                    publico = true,
                    agendaid = agendaid
                };

                _elOlivoDbContext.material.Add(nuevoMaterial);
                await _elOlivoDbContext.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    mensaje = "Link agregado correctamente",
                    materialid = nuevoMaterial.materialid
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar link");
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubirArchivo(int agendaid, IFormFile archivo)
        {
            try
            {
                var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                if (archivo == null || archivo.Length == 0)
                    return Json(new { success = false, mensaje = "No se recibió ningún archivo" });

                // Validar que la actividad pertenece a un evento del admin
                var esValido = await (from a in _elOlivoDbContext.actividad
                                      join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                      join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                      where a.agendaid == agendaid && e.usuarioadminid == usuarioAdminId
                                      select e.eventoid).AnyAsync();

                if (!esValido)
                    return Json(new { success = false, mensaje = "No tiene permisos para editar esta actividad" });

                // Validar extensión
                var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                var extensionesPermitidas = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".rar" };

                if (!extensionesPermitidas.Contains(extension))
                    return Json(new { success = false, mensaje = "Tipo de archivo no permitido" });

                // Validar tamaño (máximo 10MB)
                if (archivo.Length > 10 * 1024 * 1024)
                    return Json(new { success = false, mensaje = "El archivo no puede ser mayor a 10MB" });

                // Determinar tipo de archivo
                var tipoNombre = extension switch
                {
                    ".pdf" => "PDF",
                    ".doc" or ".docx" => "Word",
                    ".ppt" or ".pptx" => "PowerPoint",
                    ".xls" or ".xlsx" => "Excel",
                    ".zip" or ".rar" => "Comprimido",
                    _ => "Documento"
                };

                var tipoArchivo = await _elOlivoDbContext.tipo_archivo
                    .FirstOrDefaultAsync(t => t.nombre == tipoNombre);

                if (tipoArchivo == null)
                {
                    tipoArchivo = new tipo_archivo { nombre = tipoNombre };
                    _elOlivoDbContext.tipo_archivo.Add(tipoArchivo);
                    await _elOlivoDbContext.SaveChangesAsync();
                }

                // Generar nombre único para el archivo
                var nombreArchivo = $"actividad_{agendaid}_{Guid.NewGuid()}{extension}";

                // Subir a Supabase bucket "files"
                var urlArchivo = await _supabaseService.SubirArchivoFiles(archivo, nombreArchivo);

                if (string.IsNullOrEmpty(urlArchivo))
                    return Json(new { success = false, mensaje = "Error al subir el archivo" });

                // Crear el material
                var nuevoMaterial = new material
                {
                    nombre = archivo.FileName,
                    url_archivo = urlArchivo,
                    tipo_archivoid = tipoArchivo.tipo_archivoid,
                    fecha_subida = DateTime.UtcNow,
                    publico = true,
                    agendaid = agendaid
                };

                _elOlivoDbContext.material.Add(nuevoMaterial);
                await _elOlivoDbContext.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    mensaje = "Archivo subido correctamente",
                    materialid = nuevoMaterial.materialid,
                    nombre = archivo.FileName,
                    url = urlArchivo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo");
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarMaterial(int materialid)
        {
            try
            {
                var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                // Validar permisos
                var material = await (from m in _elOlivoDbContext.material
                                      join a in _elOlivoDbContext.actividad on m.agendaid equals a.agendaid
                                      join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                      join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                      where m.materialid == materialid && e.usuarioadminid == usuarioAdminId
                                      select m).FirstOrDefaultAsync();

                if (material == null)
                    return Json(new { success = false, mensaje = "Material no encontrado o sin permisos" });

                // Si es un archivo (no link), eliminar de Supabase
                var tipoArchivo = await _elOlivoDbContext.tipo_archivo
                    .FirstOrDefaultAsync(t => t.tipo_archivoid == material.tipo_archivoid);

                if (tipoArchivo != null && tipoArchivo.nombre != "Link" && tipoArchivo.nombre != "URL")
                {
                    var nombreArchivo = ObtenerNombreArchivoDesdeUrl(material.url_archivo);
                    if (!string.IsNullOrEmpty(nombreArchivo))
                    {
                        await _supabaseService.EliminarArchivoFiles(nombreArchivo);
                    }
                }

                // Eliminar de la base de datos
                _elOlivoDbContext.material.Remove(material);
                await _elOlivoDbContext.SaveChangesAsync();

                return Json(new { success = true, mensaje = "Material eliminado correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar material");
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        private string ObtenerNombreArchivoDesdeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                var uri = new Uri(url);
                return Path.GetFileName(uri.LocalPath);
            }
            catch
            {
                return null;
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarActividad(int agendaid, string nombre, string descripcion)
        {
            try
            {
                var usuarioAdminId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioAdminId == null)
                    return Json(new { success = false, mensaje = "No autenticado" });

                // Validar permisos
                var actividad = await (from a in _elOlivoDbContext.actividad
                                       join s in _elOlivoDbContext.sesion on a.sesionid equals s.sesionid
                                       join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                       where a.agendaid == agendaid && e.usuarioadminid == usuarioAdminId
                                       select a).FirstOrDefaultAsync();

                if (actividad == null)
                    return Json(new { success = false, mensaje = "Actividad no encontrada o sin permisos" });

                actividad.nombre = nombre;
                actividad.descripcion = descripcion;

                await _elOlivoDbContext.SaveChangesAsync();

                return Json(new { success = true, mensaje = "Actividad actualizada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar actividad");
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

    }
}
