using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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

        public IActionResult GestionAsistencias()
        {
            return View();
        }

    }
}
