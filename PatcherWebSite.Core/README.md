# Crystal Mir2 - PatcherWebSite

Este proyecto es una versión modernizada del sitio web del patcher para Legend of Mir (Crystal), migrado a ASP.NET Core y compatible con .NET 8.

## Características

- Sitio web estático con soporte para mostrar actualizaciones recientes
- API básica para obtener información del estado del servidor
- Totalmente compatible con .NET 8
- Fácil de personalizar y extender

## Estructura del proyecto

- `/wwwroot`: Contiene todos los archivos estáticos (HTML, CSS, JavaScript, imágenes)
- `Program.cs`: Configuración del servidor ASP.NET Core

## Cómo ejecutar

Para ejecutar el sitio web localmente:

```
cd PatcherWebSite.Core
dotnet run
```

Por defecto, el sitio se ejecutará en `https://localhost:5001` y `http://localhost:5000`.

## Personalización

### Modificar la apariencia

Todos los archivos estáticos se encuentran en la carpeta `/wwwroot`. Puedes modificar:

- `index.html`: Página principal
- `css/style.css`: Estilos del sitio
- `js/script.js`: Funcionalidades JavaScript
- `img/`: Imágenes del sitio

### Añadir nuevas funcionalidades

Para añadir nuevas funcionalidades al servidor:

1. Modifica el archivo `Program.cs` para añadir nuevos endpoints API
2. Añade nuevos archivos HTML en `/wwwroot` para crear nuevas páginas
3. Actualiza los enlaces en `index.html` para apuntar a las nuevas páginas

## Configuración del cliente

El cliente está configurado para usar la URL del sitio web del patcher en:

```csharp
Settings.P_BrowserAddress = "https://www.lomcn.org/mir2-patchsite/";
```

Para usar tu versión local o personalizada, actualiza esta configuración en el cliente.

## Despliegue

Este proyecto puede desplegarse en cualquier servidor que soporte ASP.NET Core 8, incluyendo:

- Azure App Service
- IIS en Windows Server
- Linux con Nginx/Apache como proxy inverso
- Docker containers
