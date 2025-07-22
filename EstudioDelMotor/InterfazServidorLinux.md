# Interfaz del Servidor Crystal para Linux

## Índice
1. [Introducción](#introducción)
2. [Opciones de Interfaz](#opciones-de-interfaz)
3. [Implementación de Interfaz de Consola](#implementación-de-interfaz-de-consola)
4. [Implementación de Interfaz Web](#implementación-de-interfaz-web)
5. [Implementación de API REST](#implementación-de-api-rest)
6. [Recomendaciones](#recomendaciones)

## Introducción

El servidor Crystal actual utiliza una interfaz gráfica basada en WinForms para la administración y monitoreo. Sin embargo, al migrar a Linux, especialmente en entornos VPS que suelen funcionar en modo consola, necesitamos replantear la interfaz de administración. Este documento explora las diferentes opciones y propone soluciones para mantener la funcionalidad de administración en un entorno Linux.

## Opciones de Interfaz

### 1. Interfaz de Consola

Una interfaz de línea de comandos (CLI) robusta que permita administrar todas las funciones del servidor.

**Ventajas:**
- Compatible con cualquier entorno Linux, incluso sin interfaz gráfica
- Bajo consumo de recursos
- Fácil de automatizar mediante scripts
- Ideal para servidores VPS remotos

**Desventajas:**
- Menos intuitiva que una interfaz gráfica
- Curva de aprendizaje para administradores acostumbrados a GUI
- Limitaciones para visualizar datos complejos

### 2. Interfaz Web

Una interfaz web que permita administrar el servidor desde cualquier navegador.

**Ventajas:**
- Accesible desde cualquier dispositivo con navegador
- Familiar para usuarios acostumbrados a interfaces gráficas
- Permite visualizaciones ricas y dashboards
- No requiere software adicional en el cliente

**Desventajas:**
- Requiere configurar un servidor web
- Consideraciones de seguridad adicionales
- Mayor consumo de recursos que una CLI

### 3. API REST + Cliente Separado

Una API REST en el servidor que permita la administración a través de clientes dedicados.

**Ventajas:**
- Separación clara entre servidor y herramientas de administración
- Permite desarrollar múltiples clientes (web, desktop, móvil)
- Facilita la automatización y la integración con otras herramientas
- Escalable y flexible

**Desventajas:**
- Mayor complejidad de implementación
- Requiere desarrollar clientes separados

### 4. Enfoque Híbrido

Combinar varias opciones para ofrecer flexibilidad máxima.

**Ventajas:**
- Adaptable a diferentes escenarios y preferencias
- Proporciona alternativas si una interfaz no está disponible
- Maximiza la compatibilidad

**Desventajas:**
- Mayor esfuerzo de desarrollo y mantenimiento
- Posible duplicación de código

## Implementación de Interfaz de Consola

### Estructura Básica

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Server.ConsoleInterface
{
    public class ConsoleManager
    {
        private readonly Envir _envir;
        private bool _running = true;
        private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

        public ConsoleManager(Envir envir)
        {
            _envir = envir;
        }

        public void Start()
        {
            // Iniciar hilo de procesamiento de comandos
            Task.Run(() => ProcessCommands());
            
            // Mostrar información inicial
            DisplayServerInfo();
        }

        private void ProcessCommands()
        {
            while (_running)
            {
                Console.Write("Crystal> ");
                string command = Console.ReadLine();
                
                if (string.IsNullOrEmpty(command))
                    continue;
                
                ProcessCommand(command);
            }
            
            _shutdownEvent.Set();
        }

        private void ProcessCommand(string command)
        {
            string[] parts = command.Split(' ');
            string cmd = parts[0].ToLower();
            
            switch (cmd)
            {
                case "help":
                    DisplayHelp();
                    break;
                case "status":
                    DisplayStatus();
                    break;
                case "players":
                    DisplayPlayers();
                    break;
                case "shutdown":
                    InitiateShutdown();
                    break;
                case "save":
                    SaveData();
                    break;
                case "broadcast":
                    BroadcastMessage(command.Substring(9));
                    break;
                case "ban":
                    if (parts.Length > 1)
                        BanPlayer(parts[1]);
                    break;
                case "unban":
                    if (parts.Length > 1)
                        UnbanPlayer(parts[1]);
                    break;
                default:
                    Console.WriteLine("Comando desconocido. Escribe 'help' para ver la lista de comandos.");
                    break;
            }
        }

        private void DisplayHelp()
        {
            Console.WriteLine("Comandos disponibles:");
            Console.WriteLine("  help                - Muestra esta ayuda");
            Console.WriteLine("  status              - Muestra el estado del servidor");
            Console.WriteLine("  players             - Lista los jugadores conectados");
            Console.WriteLine("  shutdown            - Apaga el servidor");
            Console.WriteLine("  save                - Guarda todos los datos");
            Console.WriteLine("  broadcast <mensaje> - Envía un mensaje a todos los jugadores");
            Console.WriteLine("  ban <jugador>       - Banea a un jugador");
            Console.WriteLine("  unban <jugador>     - Desbanea a un jugador");
        }

        private void DisplayStatus()
        {
            Console.WriteLine($"Estado del servidor Crystal:");
            Console.WriteLine($"Tiempo en línea: {_envir.Uptime}");
            Console.WriteLine($"Jugadores conectados: {_envir.Players.Count}");
            Console.WriteLine($"Memoria en uso: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
            Console.WriteLine($"Versión: {_envir.Version}");
        }

        private void DisplayPlayers()
        {
            Console.WriteLine("Jugadores conectados:");
            Console.WriteLine("ID\tNombre\t\tNivel\tClase\tMapa");
            
            foreach (var player in _envir.Players)
            {
                Console.WriteLine($"{player.Index}\t{player.Name}\t\t{player.Level}\t{player.Class}\t{player.CurrentMap?.Info.FileName}");
            }
        }

        private void InitiateShutdown()
        {
            Console.WriteLine("Iniciando apagado del servidor...");
            _running = false;
            _envir.ShutDown();
        }

        private void SaveData()
        {
            Console.WriteLine("Guardando datos...");
            _envir.SaveAccounts();
            _envir.SaveGuilds();
            _envir.SaveGoods();
            Console.WriteLine("Datos guardados correctamente.");
        }

        private void BroadcastMessage(string message)
        {
            _envir.Broadcast(message);
            Console.WriteLine($"Mensaje enviado: {message}");
        }

        private void BanPlayer(string playerName)
        {
            var player = _envir.GetPlayerByName(playerName);
            if (player != null)
            {
                player.Account.Banned = true;
                player.Account.BanReason = "Baneado desde consola";
                player.Account.ExpiryDate = DateTime.Now.AddYears(10);
                _envir.SaveAccounts();
                player.Connection.SendDisconnect(4);
                Console.WriteLine($"Jugador {playerName} baneado.");
            }
            else
            {
                Console.WriteLine($"Jugador {playerName} no encontrado.");
            }
        }

        private void UnbanPlayer(string playerName)
        {
            // Buscar en la base de datos
            var account = _envir.GetAccountByCharacter(playerName);
            if (account != null)
            {
                account.Banned = false;
                account.BanReason = string.Empty;
                _envir.SaveAccounts();
                Console.WriteLine($"Jugador {playerName} desbaneado.");
            }
            else
            {
                Console.WriteLine($"Jugador {playerName} no encontrado.");
            }
        }

        private void DisplayServerInfo()
        {
            Console.Clear();
            Console.WriteLine("=================================================");
            Console.WriteLine("             CRYSTAL SERVER - LINUX              ");
            Console.WriteLine("=================================================");
            Console.WriteLine($"Versión: {_envir.Version}");
            Console.WriteLine("Servidor iniciado correctamente.");
            Console.WriteLine("Escribe 'help' para ver la lista de comandos.");
            Console.WriteLine("=================================================");
        }

        public void WaitForShutdown()
        {
            _shutdownEvent.WaitOne();
        }
    }
}
```

### Integración con el Servidor Existente

```csharp
public class Program
{
    public static void Main(string[] args)
    {
        var envir = new Envir();
        envir.Initialize();
        
        // Iniciar la interfaz de consola
        var consoleManager = new ConsoleManager(envir);
        consoleManager.Start();
        
        // Iniciar el servidor
        envir.Start();
        
        // Esperar a que se solicite el apagado
        consoleManager.WaitForShutdown();
    }
}
```

## Implementación de Interfaz Web

### Configuración Básica con ASP.NET Core

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Server.WebInterface
{
    public class WebServer
    {
        private readonly Envir _envir;
        private readonly string _url;
        private IHost _host;

        public WebServer(Envir envir, string url = "http://localhost:5000")
        {
            _envir = envir;
            _url = url;
        }

        public void Start()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(_url);
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddSingleton(_envir);
                        services.AddControllers();
                        services.AddRazorPages();
                    });
                })
                .Build();

            _host.Start();
            Console.WriteLine($"Interfaz web iniciada en {_url}");
        }

        public async Task StopAsync()
        {
            await _host.StopAsync();
        }
    }

    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });
        }
    }
}
```

### Controladores para la Interfaz Web

```csharp
using Microsoft.AspNetCore.Mvc;

namespace Server.WebInterface.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServerController : ControllerBase
    {
        private readonly Envir _envir;

        public ServerController(Envir envir)
        {
            _envir = envir;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                Uptime = _envir.Uptime,
                PlayerCount = _envir.Players.Count,
                MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024,
                Version = _envir.Version
            });
        }

        [HttpGet("players")]
        public IActionResult GetPlayers()
        {
            var players = _envir.Players.Select(p => new
            {
                p.Name,
                p.Level,
                p.Class,
                Map = p.CurrentMap?.Info.FileName,
                p.Account.LastIP
            });

            return Ok(players);
        }

        [HttpPost("broadcast")]
        public IActionResult Broadcast([FromBody] BroadcastModel model)
        {
            _envir.Broadcast(model.Message);
            return Ok(new { Success = true });
        }

        [HttpPost("save")]
        public IActionResult Save()
        {
            _envir.SaveAccounts();
            _envir.SaveGuilds();
            _envir.SaveGoods();
            return Ok(new { Success = true });
        }

        [HttpPost("shutdown")]
        public IActionResult Shutdown()
        {
            Task.Run(() => _envir.ShutDown());
            return Ok(new { Success = true });
        }
    }

    public class BroadcastModel
    {
        public string Message { get; set; }
    }
}
```

## Implementación de API REST

### Definición de Endpoints

```csharp
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ServerApiController : ControllerBase
    {
        private readonly Envir _envir;

        public ServerApiController(Envir envir)
        {
            _envir = envir;
        }

        [HttpGet("server/status")]
        public IActionResult GetServerStatus()
        {
            return Ok(new
            {
                status = "online",
                uptime = _envir.Uptime.ToString(),
                players_online = _envir.Players.Count,
                memory_usage_mb = GC.GetTotalMemory(false) / 1024 / 1024,
                version = _envir.Version
            });
        }

        [HttpGet("players")]
        public IActionResult GetPlayers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var players = _envir.Players
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    id = p.Index,
                    name = p.Name,
                    level = p.Level,
                    class_name = p.Class.ToString(),
                    map = p.CurrentMap?.Info.FileName,
                    guild = p.MyGuild?.Name
                });

            return Ok(new
            {
                total = _envir.Players.Count,
                page,
                page_size = pageSize,
                data = players
            });
        }

        [HttpGet("players/{name}")]
        public IActionResult GetPlayerByName(string name)
        {
            var player = _envir.GetPlayerByName(name);
            if (player == null)
                return NotFound(new { error = "Player not found" });

            return Ok(new
            {
                id = player.Index,
                name = player.Name,
                level = player.Level,
                class_name = player.Class.ToString(),
                map = player.CurrentMap?.Info.FileName,
                guild = player.MyGuild?.Name,
                gold = player.Account.Gold,
                creation_date = player.Account.CreationDate
            });
        }

        [HttpPost("broadcast")]
        public IActionResult Broadcast([FromBody] BroadcastRequest request)
        {
            if (string.IsNullOrEmpty(request.Message))
                return BadRequest(new { error = "Message cannot be empty" });

            _envir.Broadcast(request.Message);
            return Ok(new { success = true });
        }

        [HttpPost("players/{name}/ban")]
        public IActionResult BanPlayer(string name, [FromBody] BanRequest request)
        {
            var player = _envir.GetPlayerByName(name);
            if (player == null)
                return NotFound(new { error = "Player not found" });

            player.Account.Banned = true;
            player.Account.BanReason = request.Reason ?? "Banned via API";
            player.Account.ExpiryDate = DateTime.Now.AddDays(request.Days ?? 36500);
            _envir.SaveAccounts();
            
            if (request.Disconnect ?? true)
                player.Connection.SendDisconnect(4);

            return Ok(new { success = true });
        }

        [HttpPost("players/{name}/unban")]
        public IActionResult UnbanPlayer(string name)
        {
            var account = _envir.GetAccountByCharacter(name);
            if (account == null)
                return NotFound(new { error = "Player not found" });

            account.Banned = false;
            account.BanReason = string.Empty;
            _envir.SaveAccounts();

            return Ok(new { success = true });
        }

        [HttpPost("server/save")]
        public IActionResult SaveServer()
        {
            _envir.SaveAccounts();
            _envir.SaveGuilds();
            _envir.SaveGoods();
            return Ok(new { success = true });
        }

        [HttpPost("server/shutdown")]
        public IActionResult ShutdownServer([FromQuery] int delay = 0)
        {
            if (delay > 0)
            {
                _envir.Broadcast($"El servidor se apagará en {delay} segundos.");
                Task.Run(async () =>
                {
                    await Task.Delay(delay * 1000);
                    _envir.ShutDown();
                });
            }
            else
            {
                Task.Run(() => _envir.ShutDown());
            }

            return Ok(new { success = true });
        }
    }

    public class BroadcastRequest
    {
        public string Message { get; set; }
    }

    public class BanRequest
    {
        public string Reason { get; set; }
        public int? Days { get; set; }
        public bool? Disconnect { get; set; }
    }
}
```

## Recomendaciones

### Enfoque Recomendado: Híbrido

Para un servidor Crystal en Linux, recomendamos implementar un enfoque híbrido:

1. **Interfaz de Consola Básica**:
   - Implementar una CLI robusta para operaciones básicas
   - Ideal para administración directa en el servidor
   - Útil para scripts y automatización

2. **API REST Completa**:
   - Implementar una API REST completa para todas las funcionalidades
   - Asegurar con autenticación y autorización
   - Documentar con Swagger/OpenAPI

3. **Interfaz Web Simple**:
   - Desarrollar una interfaz web básica que consuma la API REST
   - Enfocada en monitoreo y operaciones comunes
   - Accesible desde cualquier navegador

### Consideraciones de Seguridad

1. **Autenticación**:
   - Implementar JWT o autenticación similar para la API y la interfaz web
   - Limitar el acceso a la consola mediante usuarios del sistema

2. **Firewall**:
   - Configurar el firewall para limitar el acceso a los puertos de administración
   - Considerar el uso de VPN para acceso administrativo

3. **HTTPS**:
   - Configurar HTTPS para la interfaz web y API
   - Utilizar Let's Encrypt para certificados gratuitos

### Implementación Gradual

1. **Fase 1**: Implementar la interfaz de consola básica
2. **Fase 2**: Desarrollar la API REST con endpoints esenciales
3. **Fase 3**: Crear una interfaz web simple
4. **Fase 4**: Expandir funcionalidades según necesidades

### Herramientas y Bibliotecas Recomendadas

1. **Para la API REST**:
   - ASP.NET Core
   - Swagger para documentación
   - JWT para autenticación

2. **Para la Interfaz Web**:
   - Blazor Server para una interfaz web simple
   - Bootstrap para el diseño
   - SignalR para actualizaciones en tiempo real

3. **Para la Interfaz de Consola**:
   - System.CommandLine para parsing de comandos
   - Spectre.Console para UI de consola mejorada

---

*Este informe presenta opciones para reemplazar la interfaz gráfica del servidor Crystal al migrar a Linux, con énfasis en soluciones compatibles con entornos VPS sin interfaz gráfica.*
