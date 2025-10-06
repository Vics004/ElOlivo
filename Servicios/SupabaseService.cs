using Supabase;

namespace ElOlivo.Servicios
{
    public class SupabaseService
    {
        private readonly Supabase.Client _supabase;
        private readonly string _bucketName = "avatars";
        private readonly ILogger<SupabaseService> _logger;

        public SupabaseService(IConfiguration configuration, ILogger<SupabaseService> logger)
        {
            _logger = logger;

            var options = new SupabaseOptions
            {
                AutoConnectRealtime = true,
                AutoRefreshToken = true
            };

            // USAR SERVICE KEY EN LUGAR DE ANON KEY
            var supabaseUrl = configuration["Supabase:Url"];
            var supabaseKey = configuration["Supabase:ServiceKey"]; // Cambiado a ServiceKey

            if (string.IsNullOrEmpty(supabaseKey))
            {
                // Fallback a la key normal si ServiceKey no existe
                supabaseKey = configuration["Supabase:Key"];
                _logger.LogWarning("Usando Key en lugar de ServiceKey");
            }

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                throw new ArgumentException("Supabase URL and Key must be configured in appsettings.json");
            }

            _supabase = new Supabase.Client(
                supabaseUrl,
                supabaseKey,
                options
            );

            _supabase.InitializeAsync().Wait();
            _logger.LogInformation("SupabaseService inicializado con ServiceKey");
        }

        public async Task<string> SubirArchivo(IFormFile archivo, string nombreArchivo)
        {
            try
            {
                _logger.LogInformation($"Iniciando subida de archivo: {nombreArchivo}");

                // Verificar que el bucket existe, si no crearlo
                var buckets = await _supabase.Storage.ListBuckets();
                _logger.LogInformation($"Buckets encontrados: {buckets.Count}");

                if (!buckets.Any(b => b.Name == _bucketName))
                {
                    _logger.LogInformation($"Creando bucket: {_bucketName}");
                    await _supabase.Storage.CreateBucket(_bucketName, new Supabase.Storage.BucketUpsertOptions
                    {
                        Public = true
                    });
                }

                // Convertir IFormFile a byte[]
                using var memoryStream = new MemoryStream();
                await archivo.CopyToAsync(memoryStream);
                var bytes = memoryStream.ToArray();
                _logger.LogInformation($"Archivo convertido a bytes: {bytes.Length} bytes");

                // Subir archivo
                var resultado = await _supabase.Storage
                    .From(_bucketName)
                    .Upload(bytes, nombreArchivo, new Supabase.Storage.FileOptions
                    {
                        CacheControl = "3600",
                        Upsert = false
                    });

               

                if (resultado != null)
                {
                    // Obtener URL pública
                    var urlPublica = _supabase.Storage
                        .From(_bucketName)
                        .GetPublicUrl(nombreArchivo);

                    _logger.LogInformation($"URL pública generada: {urlPublica}");
                    return urlPublica;
                }

                _logger.LogWarning("La subida del archivo retornó null");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir archivo");
                throw new Exception($"Error al subir archivo: {ex.Message}");
            }
        }

        public async Task<bool> EliminarArchivo(string nombreArchivo)
        {
            try
            {
                _logger.LogInformation($"Eliminando archivo: {nombreArchivo}");
                await _supabase.Storage
                    .From(_bucketName)
                    .Remove(new List<string> { nombreArchivo });

                _logger.LogInformation("Archivo eliminado exitosamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar archivo");
                return false;
            }
        }

        public string GenerarNombreArchivo(int usuarioId, string extension)
        {
            return $"avatar_{usuarioId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
        }
    }
}
