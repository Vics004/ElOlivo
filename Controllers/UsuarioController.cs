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
                    EstadoEvento = estado?.nombre ?? "No definido"
                };

                // Verificar si el usuario ya está inscrito
                var inscripcionExistente = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.eventoid == id && i.usuarioid == usuarioId);

                ViewBag.Evento = eventoData;
                ViewBag.YaInscrito = inscripcionExistente != null;
                ViewBag.InscripcionId = inscripcionExistente?.inscripcionid;
                ViewBag.EventoId = id;

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

                // Verificar si ya está inscrito
                var inscripcionExistente = await _elOlivoDbContext.inscripcion
                    .FirstOrDefaultAsync(i => i.eventoid == eventoid && i.usuarioid == usuarioId);

                if (inscripcionExistente != null)
                {
                    TempData["Error"] = "Ya estás inscrito en este evento.";
                    return RedirectToAction("DetallesEvento", new { id = eventoid });
                }

                // Verificar capacidad del evento
                var evento = await _elOlivoDbContext.evento.FindAsync(eventoid);
                var totalInscritos = await _elOlivoDbContext.inscripcion
                    .CountAsync(i => i.eventoid == eventoid);

                if (evento.capacidad_maxima.HasValue && totalInscritos >= evento.capacidad_maxima.Value)
                {
                    TempData["Error"] = "El evento ha alcanzado su capacidad máxima.";
                    return RedirectToAction("DetallesEvento", new { id = eventoid });
                }

                // Crear nueva inscripción
                var nuevaInscripcion = new inscripcion
                {
                    eventoid = eventoid,
                    usuarioid = usuarioId,
                    fecha_inscripcion = DateTime.UtcNow,
                    estadoid = 2, // 2 = Confirmada (ajusta según tus estados)
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
            //Obtener los id de eventoid en los que el usuario está inscrito

            var eventoid = (from i in _elOlivoDbContext.inscripcion
                            where i.usuarioid == HttpContext.Session.GetInt32("usuarioId")
                            select i.eventoid).ToList();

            //Obtener las sesiones de los eventos en los que el usuario está inscrito
            var sesiones = (from s in _elOlivoDbContext.sesion
                            where eventoid.Contains(s.eventoid) && s.activo == true
                            select s).ToList();
            //var sesiones =(from sesion in _elOlivoDbContext.sesion
            // where sesion.
            // select sesion).ToList();

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
                color = "#28a745", // Verde para sesiones
                textColor = "white",
                allDay= false
            });

            return Json(data);
        }

        public ActionResult GetEventosPersonal()
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
                color = "#28a745", // Verde para sesiones
                textColor = "white",
                allDay = false
            });

            return Json(data);
        }


        public IActionResult InfoActividad(int id)
        {
            var actividad = (from a in _elOlivoDbContext.actividad
                             join u in _elOlivoDbContext.usuario on a.ponenteid equals u.usuarioid
                             join ta in _elOlivoDbContext.tipoactividad on a.tipoactividadid equals ta.tipoactividadid
                             where a.agendaid == id
                             select new
                             {
                                 Nombre= a.nombre,
                                 Descripcion= a.descripcion,
                                 Inicio= a.hora_inicio.Value.ToString("h:mm tt", new System.Globalization.CultureInfo("es-ES")),
                                 Fin= a.hora_fin,
                                 Ponente= u.nombre + " " + u.apellido,
                                 Actividad= ta.nombre,
                                 Estado = a.activo
                             }).FirstOrDefault();
            ViewBag.Actividad = actividad;

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
        public IActionResult MiPerfil()
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

            ViewBag.Usuario = usuario;
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
