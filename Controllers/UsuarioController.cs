using ElOlivo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ElOlivo.Servicios;
using static ElOlivo.Servicios.AutenticationAttribute;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;



namespace ElOlivo.Controllers
{
    [Autenticacion]   
    public class UsuarioController : Controller
    {
        private readonly ILogger<UsuarioController> _logger;
        private readonly SupabaseService _supabaseService;
        private readonly ElOlivoDbContext _elOlivoDbContext;

        public UsuarioController(ILogger<UsuarioController> logger, SupabaseService supabaseService, ElOlivoDbContext elOlivoDbContext)
        {
            _elOlivoDbContext = elOlivoDbContext;
            _supabaseService = supabaseService;
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

        //NUEVO


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
                            where e.estadoid == 3 // SOLO EVENTOS ABIERTOS (estadoid = 3)
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

        public async Task<IActionResult> DetallesEvento(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener el evento
                var evento = await _elOlivoDbContext.evento
                    .FirstOrDefaultAsync(e => e.eventoid == id);

                if (evento == null)
                {
                    TempData["Error"] = "El evento no existe o no se encontró.";
                    return RedirectToAction("Buscar_Enventos");
                }

                // Obtener información del administrador
                var administrador = await _elOlivoDbContext.usuario
                    .FirstOrDefaultAsync(u => u.usuarioid == evento.usuarioadminid);

                // Obtener estado del evento
                var estado = await _elOlivoDbContext.estado
                    .FirstOrDefaultAsync(es => es.estadoid == evento.estadoid);

                // Contar inscripciones confirmadas del evento
                var totalInscritos = await _elOlivoDbContext.inscripcion
                    .CountAsync(i => i.eventoid == id && i.estadoid == 2); // 2 = Confirmada

                // Verificar si se alcanzó la capacidad máxima
                bool capacidadAlcanzada = evento.capacidad_maxima.HasValue &&
                                         totalInscritos >= evento.capacidad_maxima.Value;

                // Si se alcanzó la capacidad, actualizar el estado del evento a 6 (Inscripción Cerrada)
                if (capacidadAlcanzada && evento.estadoid != 6)
                {
                    evento.estadoid = 6; // 6 = Inscripción Cerrada
                    await _elOlivoDbContext.SaveChangesAsync();

                    // Actualizar el objeto estado para la vista
                    estado = await _elOlivoDbContext.estado
                        .FirstOrDefaultAsync(es => es.estadoid == 6);
                }

                // Verificar el estado de la inscripción del usuario
                var inscripcionExistente = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.eventoid == id && i.usuarioid == usuarioId);

                bool yaInscrito = inscripcionExistente != null && inscripcionExistente.estadoid == 2; // Solo Confirmada
                bool inscripcionCancelada = inscripcionExistente != null && inscripcionExistente.estadoid == 1; // Cancelada

                // Preparar datos del evento para la vista
                var eventoData = new
                {
                    evento.eventoid,
                    evento.nombre,
                    evento.descripcion,
                    evento.fecha_inicio,
                    evento.fecha_fin,
                    evento.direccion,
                    evento.ubicacion_url,
                    evento.correo_encargado,
                    evento.capacidad_maxima,
                    EncargadoNombre = administrador != null ?
                        $"{administrador.nombre} {administrador.apellido}" : "No asignado",
                    EncargadoTelefono = administrador?.telefono ?? "No disponible",
                    EstadoEvento = estado?.nombre ?? "No definido",
                    TotalInscritos = totalInscritos,
                    CapacidadAlcanzada = capacidadAlcanzada,
                    EstadoId = evento.estadoid
                };

                ViewBag.Evento = eventoData;
                ViewBag.YaInscrito = yaInscrito;
                ViewBag.InscripcionCancelada = inscripcionCancelada;
                ViewBag.InscripcionId = inscripcionExistente?.inscripcionid;
                ViewBag.EventoId = id;
                ViewBag.CapacidadAlcanzada = capacidadAlcanzada;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del evento");
                TempData["Error"] = "Ocurrió un error al cargar los detalles del evento.";
                return RedirectToAction("Buscar_Enventos");
            }
        }

        public ActionResult GetSesionesEvento(int eventoid)
        {
            try
            {
                // Obtener las sesiones del evento específico
                var sesiones = (from s in _elOlivoDbContext.sesion
                                where s.eventoid == eventoid && (s.activo == null || s.activo == true)
                                select s).ToList();

                var data = sesiones.Select(e => new
                {
                    id = e.sesionid,
                    title = e.titulo ?? "Sesión sin título",
                    start = e.fecha_inicio.HasValue
                        ? e.fecha_inicio.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    end = e.fecha_fin.HasValue
                        ? e.fecha_fin.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    description = e.descripcion ?? "Sin descripción",
                    ubicacion = e.ubicacion ?? "Ubicación no definida",
                    tipo = e.tipo_sesion ?? "Tipo no definido",
                    color = "#28a745", // Verde para sesiones
                    textColor = "white",
                    allDay = false
                });

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sesiones del evento");
                return Json(new { error = "Error al cargar las sesiones" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> InscribirseEvento(int eventoid)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener el evento actualizado
                var evento = await _elOlivoDbContext.evento.FindAsync(eventoid);
                if (evento == null)
                {
                    TempData["Error"] = "El evento no existe.";
                    return RedirectToAction("Buscar_Enventos");
                }

                // Verificar si las inscripciones están cerradas
                if (evento.estadoid == 6) // 6 = Inscripción Cerrada
                {
                    TempData["Error"] = "Las inscripciones para este evento están cerradas.";
                    return RedirectToAction("DetallesEvento", new { id = eventoid });
                }

                // Verificar si ya está inscrito (estado Confirmada)
                var inscripcionExistente = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.eventoid == eventoid && i.usuarioid == usuarioId);

                if (inscripcionExistente != null)
                {
                    if (inscripcionExistente.estadoid == 2) // Ya está confirmado
                    {
                        TempData["Error"] = "Ya estás inscrito en este evento.";
                        return RedirectToAction("DetallesEvento", new { id = eventoid });
                    }
                    else if (inscripcionExistente.estadoid == 1) // Está cancelado, reactivar
                    {
                        inscripcionExistente.estadoid = 2; // Cambiar a Confirmada
                        inscripcionExistente.fecha_inscripcion = DateTime.UtcNow; // Actualizar fecha

                        await _elOlivoDbContext.SaveChangesAsync();

                        TempData["Success"] = "¡Te has vuelto a inscribir exitosamente al evento!";
                        return RedirectToAction("InscripcionExitosa", new { id = eventoid });
                    }
                }

                // Contar inscripciones confirmadas
                var totalInscritos = await _elOlivoDbContext.inscripcion
                    .CountAsync(i => i.eventoid == eventoid && i.estadoid == 2);

                // Verificar capacidad del evento
                if (evento.capacidad_maxima.HasValue && totalInscritos >= evento.capacidad_maxima.Value)
                {
                    // Actualizar estado del evento a Inscripción Cerrada
                    evento.estadoid = 6;
                    await _elOlivoDbContext.SaveChangesAsync();

                    TempData["Error"] = "El evento ha alcanzado su capacidad máxima. Las inscripciones están cerradas.";
                    return RedirectToAction("DetallesEvento", new { id = eventoid });
                }

                // Crear nueva inscripción (primera vez)
                var nuevaInscripcion = new inscripcion
                {
                    eventoid = eventoid,
                    usuarioid = usuarioId,
                    fecha_inscripcion = DateTime.UtcNow,
                    estadoid = 2, // 2 = Confirmada
                    comprobante_url = null
                };

                _elOlivoDbContext.inscripcion.Add(nuevaInscripcion);
                await _elOlivoDbContext.SaveChangesAsync();

                TempData["Success"] = "¡Te has inscrito exitosamente al evento!";
                return RedirectToAction("InscripcionExitosa", new { id = eventoid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inscribirse al evento");
                TempData["Error"] = "Ocurrió un error al inscribirse. Intenta nuevamente.";
                return RedirectToAction("DetallesEvento", new { id = eventoid });
            }
        }

        public async Task<IActionResult> InscripcionExitosa(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                var evento = await (from e in _elOlivoDbContext.evento
                                    where e.eventoid == id
                                    select new
                                    {
                                        e.eventoid,
                                        e.nombre,
                                        e.descripcion,
                                        e.fecha_inicio,
                                        e.fecha_fin,
                                        e.direccion
                                    }).FirstOrDefaultAsync();

                if (evento == null)
                {
                    return RedirectToAction("Buscar_Enventos");
                }

                ViewBag.Evento = evento;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar página de inscripción exitosa");
                return Content(ex.ToString());
            }
        }

        public IActionResult Ver_Mas(int id) // id puede ser sesionid o eventoid
        {
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Autenticar", "Login");

                int eventoid;

                // Verificar si el id es de una sesión (buscar el eventoid correspondiente)
                var sesion = _elOlivoDbContext.sesion.FirstOrDefault(s => s.sesionid == id);
                if (sesion != null)
                {
                    // El id es un sesionid, obtener el eventoid de la sesión
                    eventoid = sesion.eventoid.Value;
                }
                else
                {
                    // El id es un eventoid (comportamiento original)
                    eventoid = id;
                }

                var inscripcion = _elOlivoDbContext.inscripcion
                    .FirstOrDefault(i => i.eventoid == eventoid && i.usuarioid == usuarioId);

               

                //Obtener estado para etiqueta
                var inscripcionConEstado = (from i in _elOlivoDbContext.inscripcion
                                            join est in _elOlivoDbContext.estado
                                                on i.estadoid equals est.estadoid
                                            where i.eventoid == eventoid && i.usuarioid == usuarioId
                                            select new
                                            {
                                                inscripcion = i,
                                                estadoNombre = est.nombre
                                            }).FirstOrDefault();

                //Obtener información para mostrar las sesiones con sus actividades
                var evento = _elOlivoDbContext.evento.FirstOrDefault(e => e.eventoid == eventoid);

                var sesiones = (from s in _elOlivoDbContext.sesion
                                where s.eventoid == eventoid && s.activo == true
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

                // Pasar el sesionid seleccionado si existe
                if (sesion != null)
                {
                    ViewBag.SesionSeleccionada = id; // sesionid
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Ver_Mas");
                return RedirectToAction("Inscripciones");
            }
        }

        public IActionResult Ver_Mas2(int idEvento) // id puede ser sesionid o eventoid
        {
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Autenticar", "Login");





                var inscripcion = _elOlivoDbContext.inscripcion
                    .FirstOrDefault(i => i.eventoid == idEvento && i.usuarioid == usuarioId);



                //Obtener estado para etiqueta
                var inscripcionConEstado = (from i in _elOlivoDbContext.inscripcion
                                            join est in _elOlivoDbContext.estado
                                                on i.estadoid equals est.estadoid
                                            where i.eventoid == idEvento && i.usuarioid == usuarioId
                                            select new
                                            {
                                                inscripcion = i,
                                                estadoNombre = est.nombre
                                            }).FirstOrDefault();

                //Obtener información para mostrar las sesiones con sus actividades
                var evento = _elOlivoDbContext.evento.FirstOrDefault(e => e.eventoid == idEvento);

                var sesiones = (from s in _elOlivoDbContext.sesion
                                where s.eventoid == idEvento && s.activo == true
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



                return View("Ver_Mas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Ver_Mas");
                return RedirectToAction("Inscripciones");
            }
        }

        //NUEVO



        public IActionResult Ver_Sesiones()
        {
            return View();
        }
        public IActionResult Ver_SesionesPersonales()
        {
            return View();
        }

        public ActionResult GetEventos()
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                {
                    return Json(new List<object>());
                }

                // Obtener los id de eventoid en los que el usuario está inscrito y NO cancelados
                var eventoid = (from i in _elOlivoDbContext.inscripcion
                                join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                where i.usuarioid == usuarioId
                                      && i.estadoid != 1 // No cancelada
                                      && e.estadoid != 5 // Evento no cancelado
                                select i.eventoid).ToList();

                // Obtener las sesiones de los eventos en los que el usuario está inscrito
                var sesiones = (from s in _elOlivoDbContext.sesion
                                where eventoid.Contains(s.eventoid)
                                      && s.activo == true
                                select s).ToList();

                var data = sesiones.Select(e => new
                {
                    id = e.sesionid,
                    title = e.titulo ?? "Sesión sin título",
                    start = e.fecha_inicio.HasValue
                        ? e.fecha_inicio.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    end = e.fecha_fin.HasValue
                        ? e.fecha_fin.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    color = "#28a745", // Verde para sesiones
                    textColor = "white",
                    allDay = false
                });

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos");
                return Json(new { error = "Error al cargar los eventos" });
            }
        }

        public ActionResult GetEventosPersonal()
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                {
                    return Json(new List<object>());
                }

                // Obtener las sesiones filtrando eventos cancelados e inscripciones canceladas
                var sesiones = (from s in _elOlivoDbContext.sesion
                                join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                join i in _elOlivoDbContext.inscripcion on e.eventoid equals i.eventoid into inscripciones
                                from i in inscripciones.Where(x => x.usuarioid == usuarioId).DefaultIfEmpty()
                                where s.activo == true
                                      && e.estadoid != 5 // Excluir eventos cancelados (estadoid = 5)
                                      && (i == null || i.estadoid != 1) // Excluir si la inscripción está cancelada (estadoid = 1)
                                select new
                                {
                                    s.sesionid,
                                    s.titulo,
                                    s.fecha_inicio,
                                    s.fecha_fin,
                                    s.descripcion,
                                    s.ubicacion,
                                    s.tipo_sesion,
                                    EventoEstadoId = e.estadoid,
                                    InscripcionEstadoId = (int?)i.estadoid
                                }).ToList();

                var data = sesiones.Select(e => new
                {
                    id = e.sesionid,
                    title = e.titulo ?? "Sesión sin título",
                    start = e.fecha_inicio.HasValue
                        ? e.fecha_inicio.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    end = e.fecha_fin.HasValue
                        ? e.fecha_fin.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                        : null,
                    description = e.descripcion ?? "Sin descripción",
                    ubicacion = e.ubicacion ?? "Ubicación no definida",
                    tipo = e.tipo_sesion ?? "Tipo no definido",
                    color = GetColorForEvent(e.EventoEstadoId, e.InscripcionEstadoId),
                    textColor = "white",
                    allDay = false
                });

                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos personales");
                return Json(new List<object>());
            }
        }

        // Método auxiliar para determinar el color según el estado
        private string GetColorForEvent(int? eventoEstadoId, int? inscripcionEstadoId)
        {
            // Si el evento está cancelado (no debería aparecer por el filtro, pero por seguridad)
            if (eventoEstadoId == 5)
                return "#6c757d"; // Gris para cancelados

            // Si la inscripción está cancelada (no debería aparecer por el filtro, pero por seguridad)
            if (inscripcionEstadoId == 1)
                return "#6c757d"; // Gris para cancelados

            // Colores según el estado del evento
            return eventoEstadoId switch
            {
                3 => "#28a745", // Verde para Abierto
                4 => "#17a2b8", // Azul para Finalizado
                6 => "#dc3545", // Rojo para Inscripción Cerrada
                _ => "#28a745"  // Verde por defecto
            };
        }


        public async Task<IActionResult> InfoActividad(int id)
        {
            try
            {
                _logger.LogInformation($"Cargando información de actividad {id}");

                var actividad = await (from a in _elOlivoDbContext.actividad
                                       join u in _elOlivoDbContext.usuario on a.ponenteid equals u.usuarioid
                                       join ta in _elOlivoDbContext.tipoactividad on a.tipoactividadid equals ta.tipoactividadid
                                       where a.agendaid == id
                                       select new
                                       {
                                           AgendaId = a.agendaid,
                                           Nombre = a.nombre,
                                           Descripcion = a.descripcion,
                                           Inicio = a.hora_inicio.HasValue ?
                                               a.hora_inicio.Value.ToString("h:mm tt", new System.Globalization.CultureInfo("es-ES")) : "No definido",
                                           Fin = a.hora_fin,
                                           Ponente = u.nombre + " " + u.apellido,
                                           Actividad = ta.nombre,
                                           Estado = a.activo
                                       }).FirstOrDefaultAsync();

                if (actividad == null)
                {
                    _logger.LogWarning($"Actividad {id} no encontrada");
                    TempData["Error"] = "Actividad no encontrada";
                    return RedirectToAction("Dashboard");
                }

                // Obtener materiales (archivos y links) - SIN filtro por público
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
                _logger.LogError(ex, "Error al cargar información de actividad {Id}", id);
                TempData["Error"] = "Error al cargar la actividad";
                return RedirectToAction("Dashboard");
            }
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

                // Obtener el evento relacionado
                var evento = await _elOlivoDbContext.evento
                                    .FirstOrDefaultAsync(e => e.eventoid == inscripcion.eventoid);

                if (evento == null)
                {
                    _logger.LogWarning("Evento no encontrado para inscripción: {Id}", id);
                    return NotFound();
                }

                // Contar inscripciones confirmadas actuales
                var totalInscritosConfirmados = await _elOlivoDbContext.inscripcion
                    .CountAsync(i => i.eventoid == evento.eventoid && i.estadoid == 2); // 2 = Confirmada

                // Verificar si el evento tenía inscripciones cerradas por capacidad
                bool estabaEnCapacidadMaxima = evento.estadoid == 6 &&
                                              evento.capacidad_maxima.HasValue &&
                                              totalInscritosConfirmados >= evento.capacidad_maxima.Value;

                // Cancelar la inscripción
                inscripcion.estadoid = 1; // 1 = Cancelada

                // Si el evento tenía inscripciones cerradas por capacidad máxima, reabrirlas
                // porque ahora hay un espacio disponible
                if (estabaEnCapacidadMaxima)
                {
                    evento.estadoid = 3; // 3 = Abierto
                    _logger.LogInformation("Reabriendo inscripciones para evento {EventoId} después de cancelación. Espacios disponibles: {Disponibles}",
                                          evento.eventoid, evento.capacidad_maxima - totalInscritosConfirmados + 1);
                }

                await _elOlivoDbContext.SaveChangesAsync();

                _logger.LogInformation("Inscripción {Id} cancelada correctamente. Evento reabierto: {Reabierto}",
                                      id, estabaEnCapacidadMaxima);

                // Mensaje informativo
                if (estabaEnCapacidadMaxima)
                {
                    TempData["Success"] = "Inscripción cancelada. El evento ahora tiene espacios disponibles y está abierto para nuevas inscripciones.";
                }
                else
                {
                    TempData["Success"] = "Inscripción cancelada correctamente.";
                }

                return RedirectToAction(nameof(Inscripciones));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cancelar inscripción {Id}", id);
                TempData["Error"] = "Ocurrió un error al cancelar la inscripción.";
                return RedirectToAction(nameof(Inscripciones));
            }
        }

        /*
        //Ver_Mas
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
        */


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



        [Autenticacion]
        public async Task<IActionResult> MiPerfil()
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");

            var usuario = _elOlivoDbContext.usuario
                .Where(u => u.usuarioid == usuarioId)
                .Select(u => new
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
                    u.rolid
                })
                .FirstOrDefault();

            if (usuario == null) return NotFound();

            // Obtener los certificados del usuario
            var certificados = await (from c in _elOlivoDbContext.certificado
                                      join s in _elOlivoDbContext.sesion on c.sesionid equals s.sesionid
                                      join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                      join es in _elOlivoDbContext.estado on c.estadoid equals es.estadoid
                                      where c.usuarioid == usuarioId
                                      orderby c.fecha_emision descending
                                      select new
                                      {
                                          c.certificadoid,
                                          c.codigo_unico,
                                          c.fecha_emision,
                                          EstadoId = c.estadoid,
                                          EstadoNombre = es.nombre,
                                          EventoNombre = e.nombre,
                                          SesionTitulo = s.titulo,
                                          FechaEmision = c.fecha_emision
                                      }).Take(5).ToListAsync(); // Mostrar solo los últimos 5 certificados

            ViewBag.Usuario = usuario;
            ViewBag.Certificados = certificados;

            return View();
        }

        [HttpGet]
        [Autenticacion]
        public IActionResult EditarPerfil()
        {
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");

            var usuario = _elOlivoDbContext.usuario.Find(usuarioId);
            if (usuario == null) return NotFound();

            CargarListas();
            return View(usuario);
        }

        [HttpPost]
        [Autenticacion]
        public async Task<IActionResult> EditarPerfil(usuario model, IFormFile archivoFoto)
        {
            _logger.LogInformation("Iniciando EditarPerfil POST");

            // Ejecutar validaciones personalizadas
            var errores = ValidarUsuario(model);
            foreach (var error in errores)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }

            // Remover la validación requerida para archivoFoto si no se subió un nuevo archivo
            if (archivoFoto == null || archivoFoto.Length == 0)
            {
                ModelState.Remove("archivoFoto");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var usuarioId = HttpContext.Session.GetInt32("usuarioId");
                    _logger.LogInformation($"Usuario ID: {usuarioId}");

                    var usuario = _elOlivoDbContext.usuario.Find(usuarioId);
                    if (usuario == null)
                    {
                        _logger.LogWarning("Usuario no encontrado en la base de datos");
                        return NotFound();
                    }

                    // Procesar archivo de foto solo si se subió uno nuevo
                    if (archivoFoto != null && archivoFoto.Length > 0)
                    {
                        _logger.LogInformation($"Archivo recibido: {archivoFoto.FileName}, Tamaño: {archivoFoto.Length}");

                        // Validar tipo de archivo
                        var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                        var extension = Path.GetExtension(archivoFoto.FileName).ToLowerInvariant();

                        if (!extensionesPermitidas.Contains(extension))
                        {
                            _logger.LogWarning($"Tipo de archivo no permitido: {extension}");
                            ModelState.AddModelError("archivoFoto", "Solo se permiten archivos JPG, JPEG, PNG o GIF");
                            CargarListas();
                            return View(model);
                        }

                        // Validar tamaño (máximo 5MB)
                        if (archivoFoto.Length > 5 * 1024 * 1024)
                        {
                            _logger.LogWarning($"Archivo demasiado grande: {archivoFoto.Length} bytes");
                            ModelState.AddModelError("archivoFoto", "El archivo no puede ser mayor a 5MB");
                            CargarListas();
                            return View(model);
                        }

                        // Eliminar foto anterior si existe
                        if (!string.IsNullOrEmpty(usuario.foto_url))
                        {
                            _logger.LogInformation($"Eliminando foto anterior: {usuario.foto_url}");
                            var nombreArchivoAnterior = ObtenerNombreArchivoDesdeUrl(usuario.foto_url);
                            if (!string.IsNullOrEmpty(nombreArchivoAnterior))
                            {
                                await _supabaseService.EliminarArchivo(nombreArchivoAnterior);
                            }
                        }

                        // Generar nombre único para el archivo
                        var nombreArchivo = _supabaseService.GenerarNombreArchivo(usuarioId.Value, extension);
                        _logger.LogInformation($"Nombre de archivo generado: {nombreArchivo}");

                        // Subir archivo a Supabase
                        var urlFoto = await _supabaseService.SubirArchivo(archivoFoto, nombreArchivo);

                        if (!string.IsNullOrEmpty(urlFoto))
                        {
                            _logger.LogInformation($"Foto subida exitosamente. URL: {urlFoto}");
                            usuario.foto_url = urlFoto;
                        }
                        else
                        {
                            _logger.LogError("La subida de la foto retornó una URL nula o vacía");
                            ModelState.AddModelError("archivoFoto", "Error al subir la imagen");
                            CargarListas();
                            return View(model);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No se recibió archivo de foto - manteniendo foto actual");
                        // IMPORTANTE: No actualizar foto_url si no se subió nueva foto
                        // Dejar la foto_url actual del usuario como está
                        // NO hacer: usuario.foto_url = model.foto_url;
                        _logger.LogInformation($"Manteniendo foto actual: {usuario.foto_url}");
                    }

                    // Actualizar solo los campos editables (excepto foto_url si no hay nueva imagen)
                    usuario.nombre = model.nombre;
                    usuario.apellido = model.apellido;
                    usuario.email = model.email;
                    usuario.telefono = model.telefono;
                    usuario.institucion = model.institucion;
                    usuario.pais = model.pais;

                    _logger.LogInformation("Guardando cambios en la base de datos...");
                    _elOlivoDbContext.SaveChanges();
                    _logger.LogInformation("Cambios guardados exitosamente");

                    // Actualizar datos en sesión
                    HttpContext.Session.SetString("nombre", usuario.nombre ?? "");
                    HttpContext.Session.SetString("email", usuario.email ?? "");

                    TempData["SuccessMessage"] = "Perfil actualizado correctamente";
                    return RedirectToAction("MiPerfil");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al actualizar el perfil");
                    ModelState.AddModelError("", "Error al actualizar el perfil: " + ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("ModelState no es válido");
                foreach (var error in ModelState)
                {
                    _logger.LogWarning($"Error en {error.Key}: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                }
            }

            CargarListas();
            return View(model);
        }

        [HttpPost]
        [Autenticacion]
        public async Task<IActionResult> EliminarFoto()
        {
            try
            {
                var usuarioId = HttpContext.Session.GetInt32("usuarioId");
                var usuario = _elOlivoDbContext.usuario.Find(usuarioId);

                if (usuario == null) return NotFound();

                if (!string.IsNullOrEmpty(usuario.foto_url))
                {
                    var nombreArchivo = ObtenerNombreArchivoDesdeUrl(usuario.foto_url);
                    if (!string.IsNullOrEmpty(nombreArchivo))
                    {
                        await _supabaseService.EliminarArchivo(nombreArchivo);
                    }

                    usuario.foto_url = null;
                    _elOlivoDbContext.SaveChanges();

                    TempData["SuccessMessage"] = "Foto de perfil eliminada correctamente";
                }

                return RedirectToAction("EditarPerfil");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error al eliminar la foto: " + ex.Message;
                return RedirectToAction("EditarPerfil");
            }
        }

        [HttpGet]
        [Autenticacion]
        public IActionResult CambiarContrasena()
        {
            return View();
        }

        [HttpPost]
        [Autenticacion]
        public IActionResult CambiarContrasena(string contrasenaActual, string nuevaContrasena, string confirmarContrasena)
        {
            if (string.IsNullOrEmpty(nuevaContrasena) || nuevaContrasena != confirmarContrasena)
            {
                ModelState.AddModelError("", "Las contraseñas no coinciden");
                return View();
            }

            var usuarioId = HttpContext.Session.GetInt32("usuarioId");

            var usuario = _elOlivoDbContext.usuario.Find(usuarioId);
            if (usuario == null) return NotFound();

            // Verificar contraseña actual (en producción usa hashing)
            if (usuario.contrasena != contrasenaActual)
            {
                ModelState.AddModelError("", "La contraseña actual es incorrecta");
                return View();
            }

            // Actualizar contraseña (en producción usa hashing)
            usuario.contrasena = nuevaContrasena;
            _elOlivoDbContext.SaveChanges();

            TempData["SuccessMessage"] = "Contraseña actualizada correctamente";
            return RedirectToAction("MiPerfil");
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

        //Validaciones
        private Dictionary<string, string> ValidarUsuario(usuario usuario)
        {
            var errores = new Dictionary<string, string>();

            // Validar nombre (solo letras y espacios)
            if (!string.IsNullOrEmpty(usuario.nombre))
            {
                if (!Regex.IsMatch(usuario.nombre, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                {
                    errores.Add("nombre", "El nombre solo puede contener letras y espacios");
                }
                else if (usuario.nombre.Length > 50)
                {
                    errores.Add("nombre", "El nombre no puede exceder 50 caracteres");
                }
            }

            // Validar apellido (solo letras y espacios)
            if (!string.IsNullOrEmpty(usuario.apellido))
            {
                if (!Regex.IsMatch(usuario.apellido, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$"))
                {
                    errores.Add("apellido", "El apellido solo puede contener letras y espacios");
                }
                else if (usuario.apellido.Length > 50)
                {
                    errores.Add("apellido", "El apellido no puede exceder 50 caracteres");
                }
            }

            // Validar email (formato básico)
            if (!string.IsNullOrEmpty(usuario.email))
            {
                if (!usuario.email.Contains("@") || !usuario.email.Contains("."))
                {
                    errores.Add("email", "El formato del email no es válido");
                }
                else if (usuario.email.Length > 100)
                {
                    errores.Add("email", "El email no puede exceder 100 caracteres");
                }
            }

            // Validar contraseña (mínimo 6 caracteres)
            if (!string.IsNullOrEmpty(usuario.contrasena))
            {
                if (usuario.contrasena.Length < 6)
                {
                    errores.Add("contrasena", "La contraseña debe tener al menos 6 caracteres");
                }
                else if (usuario.contrasena.Length > 100)
                {
                    errores.Add("contrasena", "La contraseña no puede exceder 100 caracteres");
                }
            }

            // Validar país(obligatorio)
            if (string.IsNullOrEmpty(usuario.pais))
            {
                errores.Add("pais", "Debe seleccionar un país");
            }

            // Validar teléfono (formato XXXX-XXXX)
            if (!string.IsNullOrEmpty(usuario.telefono))
            {
                if (!Regex.IsMatch(usuario.telefono, @"^\d{4}-\d{4}$"))
                {
                    errores.Add("telefono", "El formato del teléfono debe ser XXXX-XXXX");
                }
            }

            // Validar institución (longitud máxima)
            if (!string.IsNullOrEmpty(usuario.institucion) && usuario.institucion.Length > 100)
            {
                errores.Add("institucion", "La institución no puede exceder 100 caracteres");
            }

            return errores;
        }

        //lista de paises
        private void CargarListas()
        {
            var paises = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "Seleccione un país", Selected = true },
                new SelectListItem { Value = "México", Text = "México" },
                new SelectListItem { Value = "España", Text = "España" },
                new SelectListItem { Value = "Argentina", Text = "Argentina" },
                new SelectListItem { Value = "Colombia", Text = "Colombia" },
                new SelectListItem { Value = "Chile", Text = "Chile" },
                new SelectListItem { Value = "Perú", Text = "Perú" },
                new SelectListItem { Value = "Estados Unidos", Text = "Estados Unidos" },
                new SelectListItem { Value = "El Salvador", Text = "El Salvador" }
            };

            ViewBag.Paises = paises;
        }



        public IActionResult AcercaDe()
        {

            return View();
        }

        //Comprobantes y certificados

        //Todo para comprobantes

        [HttpGet]
        public async Task<IActionResult> Comprobante(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener la inscripción para validar estado
                var inscripcion = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.inscripcionid == id && i.usuarioid == usuarioId);

                if (inscripcion == null)
                {
                    return Json(new { success = false, message = "Comprobante no encontrado" });
                }

                // Validar que el estado sea "Confirmada" (estadoid = 2)
                if (inscripcion.estadoid != 2)
                {
                    return Json(new { success = false, message = "Solo se pueden ver comprobantes de inscripciones confirmadas" });
                }

                // Obtener datos de las tablas existentes
                var comprobanteData = await (from i in _elOlivoDbContext.inscripcion
                                             join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                             join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                             join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                             where i.inscripcionid == id && i.usuarioid == usuarioId
                                             select new comprobante
                                             {
                                                 InscripcionId = i.inscripcionid,
                                                 NombreEvento = e.nombre,
                                                 DescripcionEvento = e.descripcion,
                                                 FechaInicio = e.fecha_inicio,
                                                 FechaFin = e.fecha_fin,
                                                 FechaInscripcion = i.fecha_inscripcion,
                                                 NombreUsuario = u.nombre + " " + u.apellido,
                                                 EmailUsuario = u.email,
                                                 Institucion = u.institucion,
                                                 Estado = es.nombre,
                                                 CodigoComprobante = $"ELOLIVO-{i.inscripcionid:D6}"
                                             }).FirstOrDefaultAsync();

                if (comprobanteData == null)
                {
                    return Json(new { success = false, message = "Comprobante no encontrado" });
                }

                return PartialView("Comprobante", comprobanteData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar comprobante para inscripción {Id}", id);
                return Json(new { success = false, message = "Error al generar el comprobante" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DescargarComprobante(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener la inscripción primero para validar el estado
                var inscripcion = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.inscripcionid == id && i.usuarioid == usuarioId);

                if (inscripcion == null)
                {
                    return NotFound();
                }

                // Obtener el nombre del estado desde la base de datos
                var estado = await _elOlivoDbContext.estado
                    .Where(e => e.estadoid == inscripcion.estadoid)
                    .Select(e => e.nombre)
                    .FirstOrDefaultAsync();

                // Validar que el estado sea "Confirmada" (estadoid = 2)
                if (inscripcion.estadoid != 2) // 2 = Confirmada
                {
                    TempData["ErrorMessage"] = "Solo se pueden descargar comprobantes de inscripciones confirmadas";
                    return RedirectToAction("Inscripciones");
                }

                var comprobanteData = await (from i in _elOlivoDbContext.inscripcion
                                             join e in _elOlivoDbContext.evento on i.eventoid equals e.eventoid
                                             join u in _elOlivoDbContext.usuario on i.usuarioid equals u.usuarioid
                                             join es in _elOlivoDbContext.estado on i.estadoid equals es.estadoid
                                             where i.inscripcionid == id && i.usuarioid == usuarioId
                                             select new comprobante
                                             {
                                                 InscripcionId = i.inscripcionid,
                                                 NombreEvento = e.nombre,
                                                 DescripcionEvento = e.descripcion,
                                                 FechaInicio = e.fecha_inicio,
                                                 FechaFin = e.fecha_fin,
                                                 FechaInscripcion = i.fecha_inscripcion,
                                                 NombreUsuario = u.nombre + " " + u.apellido,
                                                 EmailUsuario = u.email,
                                                 Institucion = u.institucion,
                                                 Estado = es.nombre,
                                                 CodigoComprobante = $"ELOLIVO-{i.inscripcionid:D6}"
                                             }).FirstOrDefaultAsync();

                if (comprobanteData == null)
                {
                    return NotFound();
                }

                // Generar PDF
                var pdfBytes = await GenerarPdfComprobante(comprobanteData);

                var fileName = $"Comprobante_{comprobanteData.NombreEvento?.Replace(" ", "_") ?? "Evento"}_{DateTime.Now:yyyyMMdd}.pdf";

                // Actualizar URL si es necesario
                // inscripcion.comprobante_url = $"/comprobantes/{fileName}";
                // await _elOlivoDbContext.SaveChangesAsync();

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar comprobante para inscripción {Id}", id);
                return StatusCode(500, "Error al generar el PDF");
            }
        }

        private async Task<byte[]> GenerarPdfComprobante(comprobante comprobanteData)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    // Header institucional
                    page.Header()
                        .Height(4, Unit.Centimetre)
                        .Column(column =>
                        {
                            // Línea superior institucional
                            column.Item().Background(Colors.Green.Darken4).Padding(10).AlignCenter().Text("EL OLIVO - SISTEMA DE EVENTOS").FontColor(Colors.White).Bold().FontSize(12);

                            // Título principal
                            column.Item().PaddingVertical(5).AlignCenter().Text("COMPROBANTE DE INSCRIPCIÓN").Bold().FontSize(16).FontColor(Colors.Green.Darken4);

                            // Línea informativa
                            column.Item().Background(Colors.Green.Lighten5).Padding(5).Row(row =>
                            {
                                row.RelativeItem().AlignLeft().Text(text =>
                                {
                                    text.Span("Código: ").FontSize(9);
                                    text.Span(comprobanteData.CodigoComprobante).Bold().FontSize(9);
                                });
                                row.RelativeItem().AlignCenter().Text(text =>
                                {
                                    text.Span("Fecha emisión: ").FontSize(9);
                                    text.Span(DateTime.Now.ToString("dd/MM/yyyy")).Bold().FontSize(9);
                                });
                                row.RelativeItem().AlignRight().Text(text =>
                                {
                                    text.Span("Estado: ").FontSize(9);
                                    text.Span(comprobanteData.Estado?.ToUpper() ?? "PENDIENTE").Bold().FontSize(9);
                                });
                            });
                        });

                    // Contenido principal - Diseño tipo formulario
                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(15);

                            // Sección 1: Datos del Participante
                            column.Item().Column(participanteSection =>
                            {
                                participanteSection.Item().PaddingBottom(5).Text("INFORMACIÓN DEL PARTICIPANTE").Bold().FontSize(12).FontColor(Colors.Green.Darken4);
                                participanteSection.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(3, Unit.Centimetre);
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(3, Unit.Centimetre);
                                        columns.RelativeColumn();
                                    });

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text("Nombre:").Bold().FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(comprobanteData.NombreUsuario ?? "N/A").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text("Institución:").Bold().FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(comprobanteData.Institucion ?? "N/A").FontSize(9);

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text("Email:").Bold().FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(comprobanteData.EmailUsuario ?? "N/A").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text("Teléfono:").Bold().FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text("Registrado").FontSize(9);
                                });
                            });

                            // Línea separadora
                            column.Item().LineHorizontal(1).LineColor(Colors.Green.Lighten1);

                            // Sección 2: Detalles del Evento
                            column.Item().Column(eventoSection =>
                            {
                                eventoSection.Item().PaddingBottom(5).Text("DETALLES DEL EVENTO").Bold().FontSize(12).FontColor(Colors.Green.Darken4);

                                eventoSection.Item().Background(Colors.Green.Lighten5).Padding(10).Column(eventoDetails =>
                                {
                                    eventoDetails.Item().Text(text =>
                                    {
                                        text.Span("Evento: ").Bold().FontSize(10);
                                        text.Span(comprobanteData.NombreEvento ?? "N/A").FontSize(10);
                                    });
                                    eventoDetails.Item().PaddingTop(3).Text(text =>
                                    {
                                        text.Span("Descripción: ").Bold().FontSize(10);
                                        text.Span(comprobanteData.DescripcionEvento ?? "N/A").FontSize(10);
                                    });
                                    eventoDetails.Item().PaddingTop(3).Row(row =>
                                    {
                                        row.RelativeItem().Text(text =>
                                        {
                                            text.Span("Fecha inicio: ").Bold().FontSize(10);
                                            text.Span(comprobanteData.FechaInicio?.ToString("dd/MM/yyyy") ?? "N/A").FontSize(10);
                                        });
                                        row.RelativeItem().AlignRight().Text(text =>
                                        {
                                            text.Span("Fecha fin: ").Bold().FontSize(10);
                                            text.Span(comprobanteData.FechaFin?.ToString("dd/MM/yyyy") ?? "N/A").FontSize(10);
                                        });
                                    });
                                });
                            });

                            // Línea separadora
                            column.Item().LineHorizontal(1).LineColor(Colors.Green.Lighten1);

                            // Sección 3: Información de la Inscripción
                            column.Item().Column(inscripcionSection =>
                            {
                                inscripcionSection.Item().PaddingBottom(5).Text("INFORMACIÓN DE LA INSCRIPCIÓN").Bold().FontSize(12).FontColor(Colors.Green.Darken4);
                                inscripcionSection.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });

                                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Green.Lighten5).Padding(5).AlignCenter().Text("Fecha de Inscripción").Bold().FontSize(9);
                                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Green.Lighten5).Padding(5).AlignCenter().Text("Estado Actual").Bold().FontSize(9);

                                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignCenter().Text(comprobanteData.FechaInscripcion?.ToString("dd/MM/yyyy") ?? "N/A").FontSize(9);
                                    table.Cell().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(5).AlignCenter().Text(comprobanteData.Estado ?? "N/A").FontSize(9);
                                    
                                });
                            });

                            // Sección 4: Notas importantes
                            column.Item().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(notesSection =>
                            {
                                notesSection.Item().Text("NOTAS IMPORTANTES").Bold().FontSize(10).FontColor(Colors.Green.Darken4);
                                notesSection.Item().PaddingTop(3).Text("• Este comprobante confirma su registro oficial en el sistema.").FontSize(8);
                                notesSection.Item().Text("• Presente este documento en caso de requerir verificación.").FontSize(8);
                                notesSection.Item().Text("• Para consultas o modificaciones, utilice el código proporcionado.").FontSize(8);
                            });
                        });

                    // Footer institucional
                    page.Footer()
                        .Height(1.5f, Unit.Centimetre)
                        .BorderTop(1)
                        .BorderColor(Colors.Green.Darken3)
                        .PaddingTop(5)
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("El Olivo Sistema Académico • ").FontSize(8).FontColor(Colors.Green.Darken4);
                            text.Span("Documento generado automáticamente • ").FontSize(8).FontColor(Colors.Grey.Medium);
                            text.Span("Válido para fines de verificación").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                });
            });

            return document.GeneratePdf();
        }


        //Todo para certificados

        // En UsuarioController.cs - Agregar estos métodos

        [HttpGet]
        public async Task<IActionResult> Certificado(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener datos del certificado
                var certificadoData = await (from c in _elOlivoDbContext.certificado
                                             join s in _elOlivoDbContext.sesion on c.sesionid equals s.sesionid
                                             join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                             join u in _elOlivoDbContext.usuario on c.usuarioid equals u.usuarioid
                                             join es in _elOlivoDbContext.estado on c.estadoid equals es.estadoid
                                             where c.certificadoid == id && c.usuarioid == usuarioId
                                             select new certificadoComp
                                             {
                                                 CertificadoId = c.certificadoid,
                                                 CodigoUnico = c.codigo_unico,
                                                 FechaEmision = c.fecha_emision,
                                                 Estado = es.nombre,
                                                 NombreUsuario = u.nombre + " " + u.apellido,
                                                 EmailUsuario = u.email,
                                                 Institucion = u.institucion,
                                                 EventoNombre = e.nombre,
                                                 SesionTitulo = s.titulo,
                                                 SesionDescripcion = s.descripcion,
                                                 FechaInicioSesion = s.fecha_inicio,
                                                 FechaFinSesion = s.fecha_fin
                                             }).FirstOrDefaultAsync();

                if (certificadoData == null)
                {
                    return Json(new { success = false, message = "Certificado no encontrado" });
                }

                // Verificar que el certificado esté emitido
                if (certificadoData.Estado != "Emitido")
                {
                    return Json(new { success = false, message = "El certificado no está disponible para descarga" });
                }

                return PartialView("CertificadoComp", certificadoData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar certificado {Id}", id);
                return Json(new { success = false, message = "Error al generar el certificado" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DescargarCertificado(int id)
        {
            try
            {
                int? usuarioId = HttpContext.Session.GetInt32("usuarioId");
                if (usuarioId == null)
                    return RedirectToAction("Login", "Account");

                // Obtener el certificado de la base de datos
                var certificado = await _elOlivoDbContext.certificado
                    .FirstOrDefaultAsync(c => c.certificadoid == id && c.usuarioid == usuarioId);

                if (certificado == null)
                {
                    return NotFound();
                }

                // Verificar que el certificado esté emitido (estadoid = 8)
                if (certificado.estadoid != 8)
                {
                    TempData["ErrorMessage"] = "El certificado no está disponible para descarga";
                    return RedirectToAction("Certificados");
                }

                var certificadoData = await (from c in _elOlivoDbContext.certificado
                                             join s in _elOlivoDbContext.sesion on c.sesionid equals s.sesionid
                                             join e in _elOlivoDbContext.evento on s.eventoid equals e.eventoid
                                             join u in _elOlivoDbContext.usuario on c.usuarioid equals u.usuarioid
                                             join es in _elOlivoDbContext.estado on c.estadoid equals es.estadoid
                                             where c.certificadoid == id && c.usuarioid == usuarioId
                                             select new certificadoComp
                                             {
                                                 CertificadoId = c.certificadoid,
                                                 CodigoUnico = c.codigo_unico,
                                                 FechaEmision = c.fecha_emision,
                                                 Estado = es.nombre,
                                                 NombreUsuario = u.nombre + " " + u.apellido,
                                                 EmailUsuario = u.email,
                                                 Institucion = u.institucion,
                                                 EventoNombre = e.nombre,
                                                 SesionTitulo = s.titulo,
                                                 SesionDescripcion = s.descripcion,
                                                 FechaInicioSesion = s.fecha_inicio,
                                                 FechaFinSesion = s.fecha_fin
                                             }).FirstOrDefaultAsync();

                if (certificadoData == null)
                {
                    return NotFound();
                }

                // Generar PDF del certificado
                var pdfBytes = await GenerarPdfCertificado(certificadoData);

                var fileName = $"Certificado_{certificadoData.EventoNombre?.Replace(" ", "_") ?? "Evento"}_{DateTime.Now:yyyyMMdd}.pdf";

                // Actualizar URL si es necesario (similar a comprobantes)
                // certificado.certificado_url = $"/certificados/{fileName}";
                // await _elOlivoDbContext.SaveChangesAsync();

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar certificado {Id}", id);
                return StatusCode(500, "Error al generar el certificado");
            }
        }

        private async Task<byte[]> GenerarPdfCertificado(certificadoComp certificadoData)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    // Marco simple
                    page.Background()
                        .Border(2)
                        .BorderColor(Colors.Green.Darken3);

                    // Header compacto
                    page.Header()
                        .Height(2.5f, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Item().AlignCenter().Text("CERTIFICADO DE EXCELENCIA").Bold().FontSize(20).FontColor(Colors.Green.Darken4);
                            column.Item().AlignCenter().Text("Reconocimiento Académico").FontSize(10).FontColor(Colors.Green.Darken2);
                        });

                    // Contenido ultra compacto
                    page.Content()
                        .PaddingVertical(0.5f, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(0.6f, Unit.Centimetre);

                            // Mensaje
                            column.Item().AlignCenter().Text("Se otorga este reconocimiento a:").FontSize(12);

                            // Nombre
                            column.Item().AlignCenter()
                                .Text(certificadoData.NombreUsuario.ToUpper())
                                .Bold().FontSize(18).FontColor(Colors.Green.Darken4);

                            // Programa
                            column.Item().AlignCenter().Text("Por completar exitosamente:").FontSize(11);
                            column.Item().AlignCenter()
                                .Text(certificadoData.EventoNombre)
                                .SemiBold().FontSize(14).FontColor(Colors.Green.Darken3);

                            // Sesión
                            column.Item().AlignCenter().Text(text =>
                            {
                                text.Span("Sesión: ").FontSize(10);
                                text.Span(certificadoData.SesionTitulo).SemiBold().FontSize(10);
                            });

                            // Línea
                            column.Item().PaddingVertical(0.3f, Unit.Centimetre)
                                .LineHorizontal(1)
                                .LineColor(Colors.Green.Lighten1);

                            // Mensaje corto
                            column.Item().AlignCenter().Text("En reconocimiento a su dedicación y excelencia académica").FontSize(10).Italic();

                            // Fecha
                            column.Item().AlignCenter().Text(text =>
                            {
                                text.Span("Completado: ").FontSize(9);
                                text.Span(certificadoData.FechaInicioSesion?.ToString("dd/MM/yyyy") ?? "N/A").SemiBold().FontSize(9);
                            });

                            // Código
                            column.Item().AlignCenter().PaddingTop(0.3f, Unit.Centimetre).Text(text =>
                            {
                                text.Span("Código: ").FontSize(8);
                                text.Span(certificadoData.CodigoUnico).Bold().FontSize(8).FontColor(Colors.Green.Darken3);
                            });
                        });

                    // Footer compacto
                    page.Footer()
                        .Height(2f, Unit.Centimetre)
                        .Column(column =>
                        {
                            

                            // Info
                            column.Item().AlignCenter().Text(text =>
                            {
                                text.Span("Emitido: ").FontSize(7);
                                text.Span(DateTime.Now.ToString("dd/MM/yyyy")).SemiBold().FontSize(7);
                            });
                        });
                });
            });

            return document.GeneratePdf();
        }


    }

}
