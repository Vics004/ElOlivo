using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ElOlivo.Controllers
{
    public class RegistroController : Controller
    {
        private readonly ILogger<UsuarioController> _logger;
        private readonly ElOlivoDbContext _elOlivoDbContext;

        public RegistroController(ILogger<UsuarioController> logger, ElOlivoDbContext elOlivoDbContext)
        {
            _elOlivoDbContext = elOlivoDbContext;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Crear()
        {
            CargarListas();
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(usuario usuario)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si el email ya existe
                    var usuarioExistente = await _elOlivoDbContext.usuario
                        .FirstOrDefaultAsync(u => u.email == usuario.email);

                    if (usuarioExistente != null)
                    {
                        ModelState.AddModelError("Email", "El email ya está registrado");
                        CargarListas();
                        return View(usuario);
                    }

                    // Establecer valores por defecto (sin hash en la contraseña)
                    usuario.activo = true;
                    usuario.fecha_registro = DateTime.UtcNow;
                    usuario.rolid = 2; // Rol por defecto (usuario normal)

                    // Insertar en la base de datos
                    _elOlivoDbContext.usuario.Add(usuario);
                    await _elOlivoDbContext.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Usuario registrado exitosamente";
                    return RedirectToAction("Dashboard", "Usuario");
                }

                CargarListas();
                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el usuario");
                ModelState.AddModelError("", "Error al registrar el usuario: " + ex.Message);
                CargarListas();
                return View(usuario);
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

    }
}
