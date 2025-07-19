var builder = WebApplication.CreateBuilder(args);

// Configuración de servicios
builder.Services.AddDirectoryBrowser(); // Permite navegar por directorios

// Configuración simple sin necesidad del paquete CORS explícito
builder.Services.AddCors();

var app = builder.Build();

// Configuración del entorno
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Configuración para servir archivos estáticos
app.UseDefaultFiles(); // Busca index.html, default.html, etc.
app.UseStaticFiles(); // Sirve archivos desde wwwroot

// Configuración CORS simple
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Ruta predeterminada que muestra información sobre el servidor
app.MapGet("/api/info", () => new { 
    Name = "Crystal Mir2 Patcher", 
    Version = "1.0.0",
    Status = "Online",
    LastUpdated = DateTime.UtcNow
});

app.Run();
