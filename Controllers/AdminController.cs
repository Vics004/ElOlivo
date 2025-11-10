using OfficeOpenXml;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.IO;

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
            var sesiones = _elOlivoDbContext.sesion.ToList();

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
                color = "#007bff",
                allDay = false
            });

            return Json(data);
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

            ViewBag.Evento = evento;
            ViewBag.Sesion = sesion;
            ViewBag.EventoId = eventoId;


            // 2. OBTENER INSCRITOS EN EL EVENTO (SOLO CONFIRMADOS: estadoid == 2)
            var inscritos = await (from i in _elOlivoDbContext.inscripcion
                                   join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                   // Filtramos por el ID del Evento
                                   where i.eventoid == eventoId
                                   // Filtramos por el estado Confirmada
                                   && i.estadoid == 2
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
                // Se envía este mensaje si la lista está vacía
                ViewBag.MensajeNoAlumnos = $"No hay usuarios con inscripción CONFIRMADA (Estado ID 2) para el evento '{evento.nombre}'.";
            }

            ViewData["Title"] = $"Asistencia: {sesion.titulo}";

            // Usará GestionAsistencias.cshtml por convención
            return View(modeloAsistencia);
        }

        // NUEVA ACCIÓN POST para guardar la asistencia
        [HttpPost]
        public async Task<IActionResult> GuardarAsistencias(int sesionId, int eventoId, List<int>? asistieron)
        {

            // 1. Obtener asistencias EXISTENTES para esta sesión
            var asistenciasSesion = await _elOlivoDbContext.asistencia
                .Where(a => a.sesionid == sesionId)
                .ToListAsync();

            // Usamos el set de usuarios que DEBERÍAN haber asistido (enviado desde el formulario)
            var asistieronSet = asistieron?.ToHashSet() ?? new HashSet<int>();

            // 2. Determinar cuáles eliminar (Estaban marcados, ahora no)
            var aEliminar = asistenciasSesion
                // Usamos HasValue y .Value para manejar el int?
                .Where(a => a.usuarioid.HasValue && !asistieronSet.Contains(a.usuarioid.Value))
                .ToList();

            // 3. Determinar cuáles agregar (Están en la lista, no estaban en DB)
            var existentesSet = asistenciasSesion
                .Where(a => a.usuarioid.HasValue)
                .Select(a => a.usuarioid.Value)
                .ToHashSet();

            // Nota: Usamos fecha_hora_registro en lugar de fecha_asistencia
            var aAgregar = asistieronSet
                .Where(usuarioId => !existentesSet.Contains(usuarioId))
                .Select(usuarioId => new asistencia { sesionid = sesionId, usuarioid = usuarioId, fecha_hora_registro = DateTime.UtcNow })
                .ToList();

            // 4. Ejecutar cambios
            _elOlivoDbContext.asistencia.RemoveRange(aEliminar);
            _elOlivoDbContext.asistencia.AddRange(aAgregar);

            await _elOlivoDbContext.SaveChangesAsync();

            TempData["MensajeExito"] = "Asistencia guardada correctamente.";
            return RedirectToAction("GestionAsistencias", new { eventoId = eventoId, sesionId = sesionId });
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



    }
}
