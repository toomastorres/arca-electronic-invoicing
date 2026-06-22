using FacturacionArca.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FacturacionArca.Infrastructure.FileWatching;

public sealed class ProformaFileWatcher : IProformaWatcher
{
    private static readonly string[] Patrones = { "*.xml", "*.XML" };

    private readonly ILogger<ProformaFileWatcher> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly System.Threading.Timer _pollTimer;
    private readonly HashSet<string> _yaProcesados = new(StringComparer.OrdinalIgnoreCase);
    private string? _carpeta;

    public event EventHandler<ProformaDetectadaEventArgs>? ProformaDetectada;

    public ProformaFileWatcher(ILogger<ProformaFileWatcher> logger)
    {
        _logger = logger;
        _pollTimer = new System.Threading.Timer(PollCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Iniciar(string carpeta)
    {
        Detener();
        if (string.IsNullOrWhiteSpace(carpeta))
        {
            _logger.LogWarning("FileWatcher no iniciado: carpeta vacía.");
            return;
        }
        if (!Directory.Exists(carpeta))
        {
            _logger.LogWarning("FileWatcher no iniciado: la carpeta {Carpeta} no existe.", carpeta);
            return;
        }

        _carpeta = carpeta;
        foreach (var patron in Patrones)
        {
            var w = new FileSystemWatcher(carpeta, patron)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };
            w.Created += OnCambio;
            w.Changed += OnCambio;
            _watchers.Add(w);
        }
        _pollTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _logger.LogInformation("FileWatcher iniciado en {Carpeta}.", carpeta);
    }

    public void Detener()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public async Task EscanearExistentesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_carpeta) || !Directory.Exists(_carpeta)) return;

        foreach (var f in Directory.EnumerateFiles(_carpeta, "*.xml", SearchOption.TopDirectoryOnly)
                                  .Concat(Directory.EnumerateFiles(_carpeta, "*.XML", SearchOption.TopDirectoryOnly))
                                  .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            await EmitirSiCorrespondeAsync(f, ct);
        }
    }

    private async void OnCambio(object sender, FileSystemEventArgs e)
    {
        try
        {
            await Task.Delay(500);
            await EmitirSiCorrespondeAsync(e.FullPath, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileWatcher: error procesando {Archivo}", e.FullPath);
        }
    }

    private async Task EmitirSiCorrespondeAsync(string fullPath, CancellationToken ct)
    {
        if (_yaProcesados.Contains(fullPath)) return;

        for (int intento = 0; intento < 3; intento++)
        {
            try
            {
                var contenido = await File.ReadAllTextAsync(fullPath, ct);
                if (string.IsNullOrWhiteSpace(contenido)) return;

                _yaProcesados.Add(fullPath);
                ProformaDetectada?.Invoke(this, new ProformaDetectadaEventArgs
                {
                    ArchivoCompleto = fullPath,
                    Contenido = contenido,
                });
                return;
            }
            catch (IOException) when (intento < 2)
            {
                await Task.Delay(500, ct);
            }
        }
    }

    private async void PollCallback(object? state)
    {
        try { await EscanearExistentesAsync(CancellationToken.None); }
        catch (Exception ex) { _logger.LogError(ex, "FileWatcher: error en poll de respaldo."); }
    }

    public void Dispose()
    {
        Detener();
        _pollTimer.Dispose();
    }
}
