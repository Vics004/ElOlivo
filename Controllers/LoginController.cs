using Microsoft.AspNetCore.Mvc;
using static ElOlivo.Servicios.AutenticationAttribute;
using ElOlivo.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElOlivo.Controllers
{
    public class LoginController : Controller
    {

        private readonly ILogger<LoginController> _logger;
        private readonly ElOlivoDbContext _ElOlivoDbContexto;
        public LoginController(ILogger<LoginController> logger, ElOlivoDbContext ElOlivoDbContexto)
        {
            _ElOlivoDbContexto = ElOlivoDbContexto;
            _logger = logger;
        }

        [Autenticacion]
        public IActionResult Index()
        {
            //Datos inicio de sesion
            var usuarioId = HttpContext.Session.GetInt32("usuarioId");
            var rolNombre = HttpContext.Session.GetString("tipoUsuario");
            var nombreUsuario = HttpContext.Session.GetString("nombre");

            if (usuarioId == null)
            {
                return RedirectToAction("Autenticar", "Login");
            }

            
            var usuarioInfo = (from u in _ElOlivoDbContexto.usuario
                               join r in _ElOlivoDbContexto.rol on u.rolid equals r.rolid
                               where u.usuarioid == usuarioId
                               select new
                               {
                                   rolNombre = r.nombre,
                                   rolId = r.rolid
                               }).FirstOrDefault();

            // Establecer ViewBag con el layout correspondiente basado en el nombre del rol
           switch (usuarioInfo?.rolNombre ?? rolNombre)
            {
                
                case "Usuario":
                    ViewBag.Layout = "_Layout_Usuario";
                    ViewData["tipoUsuario"] = "Usuario";
                    break;
                case "Administrador":
                    ViewBag.Layout = "_Layout_Admin";
                    ViewData["tipoUsuario"] = "Administrador";
                    break;
            }
            

            ViewBag.nombre = nombreUsuario;
            return View();
        }

        public IActionResult Autenticar()
        {
            ViewData["ErrorMessage"] = "";
            return View();

        }

        [HttpPost]
        public async Task<IActionResult> Autenticar(string txtUsuario, string txtClave)
        {
            var usuario = await (from u in _ElOlivoDbContexto.usuario
                                 join r in _ElOlivoDbContexto.rol on u.rolid equals r.rolid
                                 where u.email == txtUsuario
                                 && u.contrasena == txtClave
                                 && (r.nombre == "Usuario" || r.nombre == "Administrador")
                                 && u.activo == true
                                 select new
                                 {
                                     usuario = u,
                                     rolNombre = r.nombre,
                                     rolId = r.rolid
                                 }).FirstOrDefaultAsync();

            if (usuario != null)
            {
                HttpContext.Session.SetString("correo", usuario.usuario.email);
                HttpContext.Session.SetInt32("usuarioId", usuario.usuario.usuarioid);
                HttpContext.Session.SetString("tipoUsuario", usuario.rolNombre);
                HttpContext.Session.SetString("nombre", usuario.usuario.nombre);
                HttpContext.Session.SetString("telefono", usuario.usuario.telefono);
                HttpContext.Session.SetString("correo", usuario.usuario.email);
                HttpContext.Session.SetInt32("rolId", usuario.rolId);
                HttpContext.Session.SetString("rolnombre", usuario.rolNombre);

                return RedirectToAction("Index", "login");
            }
            ViewData["ErrorMessage"] = "Error, usuario inválido";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

    }
}
