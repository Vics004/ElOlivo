using Microsoft.AspNetCore.Mvc;

namespace ElOlivo.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {

            return View();
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
