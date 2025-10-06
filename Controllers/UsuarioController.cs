using ElOlivo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ElOlivo.Servicios;
using static ElOlivo.Servicios.AutenticationAttribute;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;



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
                color = "#007bff",
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
                color = "#007bff",
                allDay = false
            });

            return Json(data);
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
    }
}
