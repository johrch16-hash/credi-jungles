using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosCobros.DAL.Repositories;
using System.Linq;

namespace PrestamosCobros.Web.Controllers
{
    [Authorize(Roles = "Admin,Asistente,Cobrador")]
    public class MensajesRecibidosController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly PrestamosCobros.BLL.Services.WhatsAppService _whatsAppService;

        public MensajesRecibidosController(IUnitOfWork unitOfWork, PrestamosCobros.BLL.Services.WhatsAppService whatsAppService)
        {
            _unitOfWork = unitOfWork;
            _whatsAppService = whatsAppService;
        }

        // GET: MensajesRecibidos
        public async Task<IActionResult> Index()
        {
            var mensajes = await _unitOfWork.MensajesRecibidos.Query()
                .Include(m => m.Cliente)
                .OrderByDescending(m => m.FechaRecibido)
                .Take(200)
                .ToListAsync();

            // Intentar resolver clientes para mensajes sin ClienteId
            var sinCliente = mensajes.Where(m => m.ClienteId == null && !string.IsNullOrEmpty(m.TelefonoRemitente)).ToList();
            if (sinCliente.Any())
            {
                var telefonosUnicos = sinCliente.Select(m => m.TelefonoRemitente).Distinct().ToList();
                var clientes = await _unitOfWork.Clientes.Query().Where(c => c.Activo).ToListAsync();

                foreach (var msg in sinCliente)
                {
                    var cleanPhone = msg.TelefonoRemitente;
                    var soloDigitos = new string(cleanPhone.Where(char.IsDigit).ToArray());
                    if (soloDigitos.Length > 8) soloDigitos = soloDigitos[^8..];

                    var cliente = clientes.FirstOrDefault(c => 
                    {
                        if (string.IsNullOrEmpty(c.Telefono)) return false;
                        var cPhone = new string(c.Telefono.Where(char.IsDigit).ToArray());
                        return cPhone.Contains(soloDigitos);
                    });

                    if (cliente != null)
                    {
                        msg.ClienteId = cliente.ClienteId;
                        msg.Cliente = cliente;
                        // Actualizar en DB para futuras consultas
                        _unitOfWork.MensajesRecibidos.Update(msg);
                    }
                }
                await _unitOfWork.SaveChangesAsync();
            }

            // Agrupar por teléfono remitente
            var conversaciones = mensajes
                .GroupBy(m => m.TelefonoRemitente)
                .Select(g => new ConversacionViewModel
                {
                    TelefonoRemitente = g.Key,
                    NombreCliente = g.FirstOrDefault(m => m.Cliente != null)?.Cliente?.NombreCompleto,
                    ClienteId = g.FirstOrDefault(m => m.ClienteId != null)?.ClienteId,
                    Mensajes = g.OrderBy(m => m.FechaRecibido).ToList(),
                    UltimoMensaje = g.Max(m => m.FechaRecibido),
                    NoLeidos = g.Count(m => !m.Leido)
                })
                .OrderByDescending(c => c.UltimoMensaje)
                .ToList();

            return View(conversaciones);
        }

        // POST: MensajesRecibidos/MarcarLeido
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeido(int id)
        {
            var mensaje = await _unitOfWork.MensajesRecibidos.Query()
                .FirstOrDefaultAsync(m => m.MensajeRecibidoId == id);

            if (mensaje == null) return NotFound();

            mensaje.Leido = true;
            await _unitOfWork.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: MensajesRecibidos/MarcarTodosLeidos
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarTodosLeidos(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return BadRequest();

            var mensajes = await _unitOfWork.MensajesRecibidos.Query()
                .Where(m => m.TelefonoRemitente == telefono && !m.Leido)
                .ToListAsync();

            foreach (var msg in mensajes)
            {
                msg.Leido = true;
                _unitOfWork.MensajesRecibidos.Update(msg);
            }

            await _unitOfWork.SaveChangesAsync();

            return Json(new { success = true, count = mensajes.Count });
        }

        // POST: MensajesRecibidos/EliminarConversacion
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Master")]
        public async Task<IActionResult> EliminarConversacion(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return BadRequest();

            var mensajes = await _unitOfWork.MensajesRecibidos.Query()
                .Where(m => m.TelefonoRemitente == telefono)
                .ToListAsync();

            if (mensajes.Any())
            {
                foreach (var msg in mensajes)
                {
                    _unitOfWork.MensajesRecibidos.Remove(msg);
                }
                await _unitOfWork.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> Media(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var (bytes, mimeType) = await _whatsAppService.GetArchivoBytesAsync(id);
                return File(bytes, mimeType);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error fetching media {id}: {ex.Message}");
                return NotFound();
            }
        }
    }

    public class ConversacionViewModel
    {
        public string TelefonoRemitente { get; set; } = string.Empty;
        public string? NombreCliente { get; set; }
        public int? ClienteId { get; set; }
        public List<PrestamosCobros.DAL.Entities.MensajeRecibido> Mensajes { get; set; } = new();
        public DateTime UltimoMensaje { get; set; }
        public int NoLeidos { get; set; }
    }
}
