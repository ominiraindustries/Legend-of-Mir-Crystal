# Plan de Migración del Servidor Crystal a Linux

## Índice
1. [Visión General](#visión-general)
2. [Fase 1: Preparación](#fase-1-preparación)
3. [Fase 2: Implementación de API](#fase-2-implementación-de-api)
4. [Fase 3: Adaptación de GUI](#fase-3-adaptación-de-gui)
5. [Fase 4: Versión Linux del Servidor](#fase-4-versión-linux-del-servidor)
6. [Fase 5: Pruebas y Optimización](#fase-5-pruebas-y-optimización)
7. [Cronograma Estimado](#cronograma-estimado)

## Visión General

Este plan detalla los pasos necesarios para migrar el servidor Crystal a Linux, manteniendo la interfaz gráfica de administración en Windows. El enfoque se basa en separar la lógica del servidor de la interfaz de usuario, implementar una API de comunicación, y crear una versión del servidor específica para Linux.

## Fase 1: Preparación

### Paso 1: Auditoría de Dependencias de Windows

**Objetivo**: Identificar todas las dependencias específicas de Windows en Server.Library.

**Tareas**:
1. Revisar todas las referencias a APIs específicas de Windows
2. Identificar uso de System.IO.File y otras clases que puedan tener comportamientos diferentes en Linux
3. Documentar todas las dependencias de terceros y verificar su compatibilidad con Linux
4. Identificar código que asuma rutas de Windows (barras invertidas, letras de unidad)

**Entregables**:
- Lista de dependencias específicas de Windows
- Plan de reemplazo para cada dependencia

### Paso 2: Refactorización de Server.Library

**Objetivo**: Hacer que Server.Library sea compatible con múltiples plataformas.

**Tareas**:
1. Crear abstracciones para operaciones específicas de sistema operativo:
   ```csharp
   public interface IFileSystem
   {
       bool FileExists(string path);
       string ReadAllText(string path);
       void WriteAllText(string path, string content);
       // etc.
   }

   public class WindowsFileSystem : IFileSystem { /* implementación */ }
   public class LinuxFileSystem : IFileSystem { /* implementación */ }
   ```

2. Reemplazar rutas codificadas con construcciones multiplataforma:
   ```csharp
   // Antes
   string path = "C:\\Data\\Accounts.dat";
   
   // Después
   string path = Path.Combine(Settings.DataPath, "Accounts.dat");
   ```

3. Implementar manejo de rutas independiente de plataforma:
   ```csharp
   public static class PathHelper
   {
       public static string NormalizePath(string path)
       {
           return path.Replace('\\', Path.DirectorySeparatorChar)
                      .Replace('/', Path.DirectorySeparatorChar);
       }
   }
   ```

**Entregables**:
- Versión refactorizada de Server.Library compatible con múltiples plataformas

### Paso 3: Implementación de SQLite

**Objetivo**: Reemplazar el sistema de serialización binaria con SQLite para almacenamiento de datos.

**Tareas**:
1. Añadir el paquete NuGet System.Data.SQLite.Core al proyecto Server.Library:
   ```xml
   <ItemGroup>
     <PackageReference Include="System.Data.SQLite.Core" Version="1.0.117" />
   </ItemGroup>
   ```

2. Implementar la clase SQLiteDatabase en MirDatabase:
   ```csharp
   public class SQLiteDatabase
   {
       private readonly string _connectionString;
       private static readonly object _locker = new object();

       public SQLiteDatabase(string databasePath)
       {
           // Asegurar que el directorio existe
           string directory = Path.GetDirectoryName(databasePath);
           if (!Directory.Exists(directory))
               Directory.CreateDirectory(directory);

           // Crear la cadena de conexión
           _connectionString = $"Data Source={databasePath};Version=3;";

           // Crear la base de datos si no existe
           if (!File.Exists(databasePath))
               CreateDatabase();
       }

       // Implementar métodos para gestionar cuentas, personajes, items, etc.
   }
   ```

3. Crear esquema de base de datos con tablas para Accounts, Characters, Items, etc.

4. Implementar métodos para guardar y cargar datos:
   ```csharp
   public void SaveAccount(AccountInfo account)
   {
       lock (_locker)
       {
           // Implementación usando transacciones SQLite
       }
   }

   public AccountInfo GetAccount(string accountId)
   {
       lock (_locker)
       {
           // Implementación usando consultas SQLite
       }
   }
   ```

5. Crear utilidad para migrar datos existentes:
   ```csharp
   public void MigrateFromBinaryToSQLite()
   {
       // Cargar datos del sistema antiguo
       List<AccountInfo> accounts = LoadAccountsFromBinary();
       
       // Guardar en SQLite
       foreach (var account in accounts)
       {
           SaveAccount(account);
       }
   }
   ```

**Entregables**:
- Implementación de SQLiteDatabase
- Esquema de base de datos
- Herramienta de migración de datos
- Pruebas de integridad de datos

### Paso 4: Actualización a .NET 6+

**Objetivo**: Actualizar los proyectos Server.Library y Server.MirForms a .NET 6 o superior.

**Tareas**:
1. Actualizar Server.Library.csproj:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net6.0</TargetFramework>
       <Nullable>disable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     <!-- Resto del archivo -->
   </Project>
   ```

2. Actualizar Server.csproj (MirForms):
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <OutputType>WinExe</OutputType>
       <TargetFramework>net6.0-windows</TargetFramework>
       <Nullable>disable</Nullable>
       <UseWindowsForms>true</UseWindowsForms>
       <ImplicitUsings>enable</ImplicitUsings>
     </PropertyGroup>
     <!-- Resto del archivo -->
   </Project>
   ```

3. Resolver problemas de compatibilidad que surjan durante la actualización

**Entregables**:
- Proyectos actualizados a .NET 6+
- Informe de problemas resueltos

## Fase 2: Implementación de API

### Paso 5: Diseño de API REST

**Objetivo**: Diseñar una API REST completa para todas las funcionalidades administrativas.

**Tareas**:
1. Identificar todas las operaciones administrativas necesarias
2. Diseñar endpoints RESTful para cada operación
3. Definir modelos de datos para solicitudes y respuestas
4. Diseñar sistema de autenticación y autorización

**Entregables**:
- Documento de especificación de API
- Diagrama de endpoints y modelos

### Paso 6: Implementación de API en Server.Library

**Objetivo**: Implementar la API REST en Server.Library.

**Tareas**:
1. Añadir paquetes NuGet necesarios:
   ```
   Microsoft.AspNetCore.Mvc.Core
   Swashbuckle.AspNetCore
   Microsoft.AspNetCore.Authentication.JwtBearer
   ```

2. Crear clase base para la API:
   ```csharp
   public class ServerApi
   {
       private readonly Envir _envir;
       
       public ServerApi(Envir envir)
       {
           _envir = envir;
       }
       
       public void ConfigureServices(IServiceCollection services)
       {
           services.AddSingleton(_envir);
           services.AddControllers();
           services.AddSwaggerGen();
           services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
               .AddJwtBearer(options => {
                   // Configuración JWT
               });
       }
       
       public void Configure(IApplicationBuilder app)
       {
           app.UseSwagger();
           app.UseSwaggerUI();
           app.UseRouting();
           app.UseAuthentication();
           app.UseAuthorization();
           app.UseEndpoints(endpoints => endpoints.MapControllers());
       }
   }
   ```

3. Implementar controladores para cada grupo de funcionalidades:
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   [Authorize]
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
           return Ok(new {
               Uptime = _envir.Uptime,
               PlayerCount = _envir.Players.Count,
               // etc.
           });
       }
       
       // Otros endpoints
   }
   ```

**Entregables**:
- API REST implementada en Server.Library
- Documentación Swagger generada

### Paso 7: Pruebas de API

**Objetivo**: Verificar que la API funciona correctamente.

**Tareas**:
1. Crear proyecto de pruebas unitarias para la API
2. Implementar pruebas para cada endpoint
3. Verificar autenticación y autorización
4. Probar rendimiento y concurrencia

**Entregables**:
- Suite de pruebas para la API
- Informe de resultados de pruebas

## Fase 3: Adaptación de GUI

### Paso 8: Cliente API para Server.MirForms

**Objetivo**: Crear un cliente de API para que Server.MirForms se comunique con el servidor.

**Tareas**:
1. Crear clase ApiClient:
   ```csharp
   public class ApiClient
   {
       private readonly HttpClient _client;
       private readonly string _baseUrl;
       private string _token;
       
       public ApiClient(string serverUrl)
       {
           _baseUrl = serverUrl;
           _client = new HttpClient();
       }
       
       public async Task<bool> LoginAsync(string username, string password)
       {
           var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/auth/login", 
               new { Username = username, Password = password });
               
           if (response.IsSuccessStatusCode)
           {
               var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
               _token = result.Token;
               _client.DefaultRequestHeaders.Authorization = 
                   new AuthenticationHeaderValue("Bearer", _token);
               return true;
           }
           return false;
       }
       
       public async Task<ServerStatus> GetStatusAsync()
       {
           var response = await _client.GetAsync($"{_baseUrl}/api/server/status");
           response.EnsureSuccessStatusCode();
           return await response.Content.ReadFromJsonAsync<ServerStatus>();
       }
       
       // Otros métodos para cada función administrativa
   }
   ```

2. Crear modelos de datos para la comunicación:
   ```csharp
   public class ServerStatus
   {
       public TimeSpan Uptime { get; set; }
       public int PlayerCount { get; set; }
       public long MemoryUsage { get; set; }
       // etc.
   }
   
   public class LoginResponse
   {
       public string Token { get; set; }
       public DateTime Expiration { get; set; }
   }
   
   // Otros modelos
   ```

**Entregables**:
- Cliente API implementado
- Modelos de datos para comunicación

### Paso 9: Modificación de Server.MirForms

**Objetivo**: Adaptar Server.MirForms para usar el cliente API en lugar de acceder directamente a Server.Library.

**Tareas**:
1. Añadir configuración para la conexión al servidor:
   ```csharp
   public class ServerConfig
   {
       public string ServerUrl { get; set; } = "http://localhost:5000";
       public string Username { get; set; } = "admin";
       public string Password { get; set; } = "";
       public bool RememberCredentials { get; set; } = false;
   }
   ```

2. Implementar formulario de conexión:
   ```csharp
   public partial class ConnectionForm : Form
   {
       private ServerConfig _config;
       
       public ConnectionForm(ServerConfig config)
       {
           InitializeComponent();
           _config = config;
           txtServerUrl.Text = _config.ServerUrl;
           txtUsername.Text = _config.Username;
           chkRemember.Checked = _config.RememberCredentials;
       }
       
       private void btnConnect_Click(object sender, EventArgs e)
       {
           _config.ServerUrl = txtServerUrl.Text;
           _config.Username = txtUsername.Text;
           _config.Password = txtPassword.Text;
           _config.RememberCredentials = chkRemember.Checked;
           
           DialogResult = DialogResult.OK;
       }
   }
   ```

3. Modificar la clase principal para usar el cliente API:
   ```csharp
   public partial class MainForm : Form
   {
       private ApiClient _apiClient;
       private ServerConfig _config;
       private System.Windows.Forms.Timer _statusTimer;
       
       public MainForm()
       {
           InitializeComponent();
           LoadConfig();
           ConnectToServer();
       }
       
       private void LoadConfig()
       {
           if (File.Exists("config.json"))
           {
               _config = JsonSerializer.Deserialize<ServerConfig>(
                   File.ReadAllText("config.json"));
           }
           else
           {
               _config = new ServerConfig();
           }
       }
       
       private void SaveConfig()
       {
           if (_config.RememberCredentials)
           {
               File.WriteAllText("config.json", 
                   JsonSerializer.Serialize(_config));
           }
       }
       
       private void ConnectToServer()
       {
           using (var form = new ConnectionForm(_config))
           {
               if (form.ShowDialog() != DialogResult.OK)
               {
                   Application.Exit();
                   return;
               }
           }
           
           _apiClient = new ApiClient(_config.ServerUrl);
           try
           {
               var success = _apiClient.LoginAsync(_config.Username, _config.Password).Result;
               if (!success)
               {
                   MessageBox.Show("Error de autenticación");
                   Application.Exit();
                   return;
               }
               
               SaveConfig();
               StartStatusTimer();
           }
           catch (Exception ex)
           {
               MessageBox.Show($"Error de conexión: {ex.Message}");
               Application.Exit();
           }
       }
       
       private void StartStatusTimer()
       {
           _statusTimer = new System.Windows.Forms.Timer();
           _statusTimer.Interval = 5000; // 5 segundos
           _statusTimer.Tick += async (s, e) => {
               try
               {
                   var status = await _apiClient.GetStatusAsync();
                   UpdateStatusDisplay(status);
               }
               catch
               {
                   // Manejar error de conexión
               }
           };
           _statusTimer.Start();
       }
       
       private void UpdateStatusDisplay(ServerStatus status)
       {
           lblUptime.Text = status.Uptime.ToString();
           lblPlayerCount.Text = status.PlayerCount.ToString();
           // etc.
       }
       
       // Otros métodos para manejar acciones de la interfaz
   }
   ```

**Entregables**:
- Versión modificada de Server.MirForms que se conecta remotamente
- Configuración para conexión remota

## Fase 4: Versión Linux del Servidor

### Paso 10: Crear Proyecto Server.Console

**Objetivo**: Crear un nuevo proyecto de consola para ejecutar el servidor en Linux.

**Tareas**:
1. Crear nuevo proyecto .NET 6:
   ```bash
   dotnet new console -n Server.Console
   ```

2. Añadir referencias:
   ```bash
   dotnet add Server.Console.csproj reference ../Server/Server.Library.csproj
   dotnet add Server.Console.csproj reference ../Shared/Shared.csproj
   ```

3. Implementar programa principal:
   ```csharp
   using Microsoft.AspNetCore.Builder;
   using Microsoft.Extensions.DependencyInjection;
   using Server.MirDatabase;
   using Server.MirEnvir;
   using System;
   using System.Threading;
   using System.Threading.Tasks;

   namespace Server.Console
   {
       class Program
       {
           static async Task Main(string[] args)
           {
               System.Console.WriteLine("Iniciando servidor Crystal...");
               
               var envir = new Envir();
               envir.Initialize();
               
               // Configurar API
               var builder = WebApplication.CreateBuilder(args);
               var serverApi = new ServerApi(envir);
               serverApi.ConfigureServices(builder.Services);
               var app = builder.Build();
               serverApi.Configure(app);
               
               // Iniciar API en segundo plano
               var apiTask = app.RunAsync("http://0.0.0.0:5000");
               System.Console.WriteLine("API iniciada en http://0.0.0.0:5000");
               
               // Iniciar servidor
               envir.Start();
               System.Console.WriteLine("Servidor iniciado correctamente");
               
               // Configurar consola interactiva
               var consoleManager = new ConsoleManager(envir);
               consoleManager.Start();
               
               // Esperar señal de terminación
               var quitEvent = new ManualResetEvent(false);
               System.Console.CancelKeyPress += (sender, e) => {
                   quitEvent.Set();
                   e.Cancel = true;
               };
               quitEvent.WaitOne();
               
               // Apagar servidor
               System.Console.WriteLine("Apagando servidor...");
               envir.SaveAndQuit();
               await app.StopAsync();
               
               System.Console.WriteLine("Servidor apagado correctamente");
           }
       }
   }
   ```

4. Implementar gestor de consola:
   ```csharp
   public class ConsoleManager
   {
       private readonly Envir _envir;
       private bool _running = true;
       
       public ConsoleManager(Envir envir)
       {
           _envir = envir;
       }
       
       public void Start()
       {
           Task.Run(() => ProcessCommands());
       }
       
       private void ProcessCommands()
       {
           while (_running)
           {
               System.Console.Write("Crystal> ");
               string command = System.Console.ReadLine();
               
               if (string.IsNullOrEmpty(command))
                   continue;
               
               ProcessCommand(command);
           }
       }
       
       private void ProcessCommand(string command)
       {
           // Implementación de comandos
           // Similar a lo mostrado en InterfazServidorLinux.md
       }
   }
   ```

**Entregables**:
- Proyecto Server.Console funcional
- Documentación de comandos de consola

### Paso 11: Configuración para Linux

**Objetivo**: Configurar el proyecto para su ejecución en Linux.

**Tareas**:
1. Crear script de inicio para Linux:
   ```bash
   #!/bin/bash
   cd "$(dirname "$0")"
   dotnet Server.Console.dll
   ```

2. Crear archivo de servicio systemd:
   ```
   [Unit]
   Description=Crystal MMO Server
   After=network.target

   [Service]
   Type=simple
   User=crystal
   WorkingDirectory=/opt/crystal
   ExecStart=/usr/bin/dotnet /opt/crystal/Server.Console.dll
   Restart=on-failure
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=crystal-server

   [Install]
   WantedBy=multi-user.target
   ```

3. Crear configuración para logs:
   ```xml
   <?xml version="1.0" encoding="utf-8" ?>
   <log4net>
     <appender name="FileAppender" type="log4net.Appender.RollingFileAppender">
       <file value="logs/server.log" />
       <appendToFile value="true" />
       <rollingStyle value="Size" />
       <maxSizeRollBackups value="10" />
       <maximumFileSize value="10MB" />
       <staticLogFileName value="true" />
       <layout type="log4net.Layout.PatternLayout">
         <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
       </layout>
     </appender>
     <appender name="ConsoleAppender" type="log4net.Appender.ConsoleAppender">
       <layout type="log4net.Layout.PatternLayout">
         <conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
       </layout>
     </appender>
     <root>
       <level value="INFO" />
       <appender-ref ref="FileAppender" />
       <appender-ref ref="ConsoleAppender" />
     </root>
   </log4net>
   ```

**Entregables**:
- Scripts de inicio para Linux
- Configuración de servicio systemd
- Configuración de logs

## Fase 5: Pruebas y Optimización

### Paso 12: Pruebas en Entorno Linux

**Objetivo**: Verificar que el servidor funciona correctamente en Linux.

**Tareas**:
1. Configurar entorno de pruebas Linux (puede ser una VM o un contenedor Docker)
2. Instalar .NET 6 SDK
3. Desplegar y ejecutar Server.Console
4. Conectarse desde Server.MirForms en Windows
5. Realizar pruebas funcionales completas

**Entregables**:
- Informe de pruebas en Linux
- Lista de problemas encontrados y soluciones

### Paso 13: Optimización para Linux

**Objetivo**: Optimizar el rendimiento del servidor en Linux.

**Tareas**:
1. Perfilar el rendimiento del servidor
2. Identificar cuellos de botella
3. Implementar mejoras específicas para Linux:
   ```csharp
   // Ejemplo: Optimización de hilos para Linux
   if (OperatingSystem.IsLinux())
   {
       ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount);
   }
   ```

4. Implementar manejo eficiente de archivos:
   ```csharp
   // Ejemplo: Uso de FileOptions.Asynchronous en Linux
   if (OperatingSystem.IsLinux())
   {
       using var fileStream = new FileStream(
           path, 
           FileMode.Open, 
           FileAccess.Read, 
           FileShare.Read,
           4096,
           FileOptions.Asynchronous | FileOptions.SequentialScan);
       // ...
   }
   ```

**Entregables**:
- Versión optimizada del servidor para Linux
- Informe de mejoras de rendimiento

## Cronograma Estimado

| Fase | Duración Estimada |
|------|-------------------|
| Fase 1: Preparación | 2-3 semanas |
| Fase 2: Implementación de API | 3-4 semanas |
| Fase 3: Adaptación de GUI | 2-3 semanas |
| Fase 4: Versión Linux del Servidor | 2-3 semanas |
| Fase 5: Pruebas y Optimización | 2-3 semanas |
| **Total** | **11-16 semanas** |

Este cronograma es aproximado y puede ajustarse según la complejidad encontrada durante la implementación.

---

*Este plan de migración fue creado para guiar el proceso de migración del servidor Crystal a Linux, manteniendo la interfaz gráfica de administración en Windows.*
