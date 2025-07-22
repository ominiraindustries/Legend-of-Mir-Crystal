# Análisis Técnico del Motor de Crystal

## Índice
1. [Introducción](#introducción)
2. [Arquitectura General](#arquitectura-general)
3. [Sistema de Renderizado](#sistema-de-renderizado)
4. [Sistema de Archivos Gráficos](#sistema-de-archivos-gráficos)
5. [Sistema de Red](#sistema-de-red)
6. [Bucle Principal del Juego](#bucle-principal-del-juego)
7. [Resoluciones y Configuraciones Gráficas](#resoluciones-y-configuraciones-gráficas)
8. [Conclusiones y Recomendaciones](#conclusiones-y-recomendaciones)

## Introducción

Crystal es un MMORPG basado en el código fuente de Legend of Mir2/3, desarrollado en C#. Este informe analiza en profundidad la estructura técnica del motor, centrándose en los sistemas de renderizado, gestión de archivos gráficos, red y bucle principal del juego.

## Arquitectura General

### Estructura Cliente-Servidor

Crystal implementa una arquitectura cliente-servidor clásica:

- **Servidor**: 
  - Gestiona la lógica del juego, estado del mundo, NPCs, monstruos y persistencia de datos
  - Implementado en C# con .NET Framework
  - Utiliza un modelo de procesamiento multihilo para manejar múltiples conexiones

- **Cliente**: 
  - Maneja la renderización, entrada del usuario y comunicación con el servidor
  - Implementado como una aplicación WinForms con Direct3D 9 para renderizado
  - Utiliza un bucle de eventos basado en Application.Idle

### Componentes Principales

| Componente | Descripción | Archivos Principales |
|------------|-------------|---------------------|
| **MirEnvir** | Entorno del juego | `Envir.cs`, `Map.cs` |
| **MirObjects** | Objetos del juego | `PlayerObject.cs`, `MonsterObject.cs` |
| **MirDatabase** | Sistema de persistencia | `AccountInfo.cs`, `CharacterInfo.cs` |
| **MirNetwork** | Gestión de red | `MirConnection.cs`, `Packet.cs` |
| **MirGraphics** | Sistema de renderizado | `MLibrary.cs`, `MImage.cs` |

## Sistema de Renderizado

### Pipeline de Renderizado

El pipeline de renderizado de Crystal utiliza Direct3D 9 a través de SlimDX:

1. **Inicialización**:
   ```csharp
   DXManager.Device.Clear(ClearFlags.Target, Color.Black, 0, 0);
   DXManager.Device.BeginScene();
   DXManager.Sprite.Begin(SpriteFlags.AlphaBlend);
   ```

2. **Renderizado por capas**:
   - **Capa 1**: Fondo del mapa (tiles)
   - **Capa 2**: Objetos del mapa (edificios, árboles)
   - **Capa 3**: Personajes y monstruos
   - **Capa 4**: Efectos visuales y partículas

3. **Finalización**:
   ```csharp
   DXManager.Sprite.End();
   DXManager.Device.EndScene();
   DXManager.Device.Present();
   ```

### Método de Renderizado de Mapas

Los mapas se construyen utilizando tiles de tamaño fijo:

```csharp
public const int CellWidth = 48;
public const int CellHeight = 32;
```

El proceso de renderizado de mapas incluye:

1. **Cálculo de rango visible**:
   ```csharp
   OffSetX = Settings.ScreenWidth / 2 / CellWidth;
   OffSetY = Settings.ScreenHeight / 2 / CellHeight - 1;
   ViewRangeX = OffSetX + 4;
   ViewRangeY = OffSetY + 4;
   ```

2. **Renderizado de tiles visibles**:
   ```csharp
   for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY; y++)
   {
       for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
       {
           // Cálculo de posición en pantalla
           drawX = (x - User.Movement.X + OffSetX) * CellWidth + User.OffSetMove.X;
           drawY = (y - User.Movement.Y + OffSetY) * CellHeight + User.OffSetMove.Y;
           
           // Renderizado del tile
           Libraries.MapLibs[M2CellInfo[x, y].BackIndex].Draw(index, drawX, drawY);
       }
   }
   ```

3. **Optimización mediante texturas de control**:
   - Se utiliza una textura de control para evitar re-renderizar elementos estáticos
   - Solo se actualiza cuando hay cambios en la escena

### Técnicas de Blending y Efectos

Crystal utiliza varias técnicas de blending para efectos visuales:

1. **Alpha Blending estándar**:
   ```csharp
   Device.SetRenderState(RenderState.SourceBlend, SlimDX.Direct3D9.Blend.SourceAlpha);
   Device.SetRenderState(RenderState.DestinationBlend, SlimDX.Direct3D9.Blend.InverseSourceAlpha);
   ```

2. **Blending personalizado para efectos especiales**:
   ```csharp
   Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
   Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseBlendFactor);
   ```

## Sistema de Archivos Gráficos

### Formato de Archivos .lib

Los archivos .lib son contenedores de imágenes con la siguiente estructura:

```
Estructura de un archivo .lib:
- Cabecera (2 bytes: versión, 2 bytes: número de imágenes)
- Tabla de índices (para versiones 3+)
- Para cada imagen:
  - Dimensiones (2 bytes: ancho, 2 bytes: alto)
  - Coordenadas de offset (2 bytes: X, 2 bytes: Y)
  - Información de sombra (2 bytes: ShadowX, 2 bytes: ShadowY, 1 byte: Shadow)
  - Longitud de datos (4 bytes)
  - Datos de imagen comprimidos con GZip
  - Opcionalmente, datos de máscara comprimidos con GZip
```

### Carga y Gestión de Imágenes

La clase `MLibrary` gestiona la carga de archivos .lib:

```csharp
public void Initialize()
{
    using (FileStream stream = new FileStream(_fileName, FileMode.Open, FileAccess.Read))
    using (BinaryReader reader = new BinaryReader(stream))
    {
        Version = reader.ReadInt16();
        Count = reader.ReadInt16();
        
        Images = new MImage[Count];
        
        if (Version > 2)
        {
            // Leer tabla de índices
            Positions = new long[Count];
            for (int i = 0; i < Count; i++)
                Positions[i] = reader.ReadInt64();
        }
        
        for (int i = 0; i < Count; i++)
        {
            Images[i] = new MImage(reader);
        }
    }
}
```

### Descompresión de Imágenes

El método `DecompressImage` utiliza GZip para descomprimir los datos:

```csharp
private static void DecompressImage(byte[] data, Stream destination)
{
    using (var stream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
    {
        stream.CopyTo(destination);
    }
}
```

## Sistema de Red

### Arquitectura de Red

Crystal utiliza TCP/IP para la comunicación cliente-servidor:

1. **Servidor**:
   - Utiliza `TcpListener` para aceptar conexiones
   - Cada conexión se maneja como un `MirConnection`
   - Implementa colas de mensajes para envío y recepción

2. **Cliente**:
   - Utiliza `TcpClient` para conectarse al servidor
   - Procesa paquetes en el bucle principal
   - Mantiene colas separadas para envío y recepción

### Formato de Paquetes

Los paquetes tienen una estructura común:

```
Estructura de paquete:
- 2 bytes: Longitud del paquete
- 2 bytes: ID del paquete
- [Datos específicos del paquete]
```

### Procesamiento de Paquetes

El procesamiento de paquetes se realiza de forma asíncrona:

```csharp
private void ReceiveData(IAsyncResult result)
{
    int dataRead = _client.Client.EndReceive(result);
    
    // Procesar datos recibidos
    byte[] temp = _rawData;
    _rawData = new byte[dataRead + temp.Length];
    Buffer.BlockCopy(temp, 0, _rawData, 0, temp.Length);
    Buffer.BlockCopy(rawBytes, 0, _rawData, temp.Length, dataRead);
    
    // Extraer paquetes completos
    Packet p;
    while ((p = Packet.ReceivePacket(_rawData, out _rawData)) != null)
        _receiveList.Enqueue(p);
    
    // Continuar recibiendo
    BeginReceive();
}
```

## Bucle Principal del Juego

### Servidor

El bucle principal del servidor se ejecuta en un hilo separado:

```csharp
private void WorkLoop()
{
    StartEnvir();
    StartNetwork();
    
    try
    {
        while (Running)
        {
            // Procesar conexiones
            ProcessNetwork();
            
            // Procesar lógica del juego
            Process();
            
            // Procesar mensajes
            ProcessCommands();
            
            // Pequeña pausa para evitar consumo excesivo de CPU
            Thread.Sleep(1);
        }
    }
    catch (Exception ex)
    {
        // Manejo de errores
    }
    
    StopNetwork();
    StopEnvir();
}
```

### Cliente

El bucle principal del cliente se basa en el evento `Application.Idle`:

```csharp
private static void Application_Idle(object sender, EventArgs e)
{
    try
    {
        while (AppStillIdle)
        {
            // Actualizar tiempo global
            UpdateTime();
            
            // Procesar red y lógica
            UpdateEnviroment();
            
            // Renderizar
            RenderEnvironment();
        }
    }
    catch (Exception ex)
    {
        // Manejo de errores
    }
}
```

## Resoluciones y Configuraciones Gráficas

### Resoluciones Soportadas

Crystal soporta múltiples resoluciones, definidas en `Settings.cs`:

| Resolución | Descripción |
|------------|-------------|
| 800x600    | Resolución mínima |
| 1024x768   | Resolución estándar |
| 1366x768   | Resolución panorámica |
| 1920x1080  | Full HD |

### Configuración de Renderizado

La configuración de renderizado se establece en `DXManager.cs`:

```csharp
public static void Create()
{
    PresentParameters parameters = new PresentParameters
    {
        BackBufferFormat = Format.X8R8G8B8,
        BackBufferWidth = Settings.ScreenWidth,
        BackBufferHeight = Settings.ScreenHeight,
        DeviceWindowHandle = Program.Form.Handle,
        Windowed = !Settings.FullScreen,
        SwapEffect = SwapEffect.Discard,
        PresentationInterval = Settings.VSync ? PresentInterval.Default : PresentInterval.Immediate,
        AutoDepthStencilFormat = Format.D16,
        EnableAutoDepthStencil = true
    };
    
    Device = new Device(
        new Direct3D(),
        0,
        DeviceType.Hardware,
        Program.Form.Handle,
        CreateFlags.HardwareVertexProcessing,
        parameters
    );
}
```

### Optimizaciones Gráficas

Crystal implementa varias optimizaciones para mejorar el rendimiento:

1. **Texturas de control**: Reutilización de renderizado para elementos estáticos
2. **Culling de objetos**: Solo se renderizan objetos visibles en pantalla
3. **Gestión de recursos**: Liberación automática de texturas no utilizadas
4. **Compresión de texturas**: Uso de formatos comprimidos para texturas

## Conclusiones y Recomendaciones

### Fortalezas del Motor

1. **Arquitectura modular**: Separación clara entre cliente y servidor
2. **Sistema de renderizado eficiente**: Optimizado para hardware de gama baja
3. **Formato de archivos compacto**: Los archivos .lib proporcionan buena compresión

### Limitaciones

1. **Tecnología obsoleta**: Direct3D 9 está desactualizado
2. **Escalabilidad limitada**: El diseño monolítico dificulta extensiones
3. **Dependencia de WinForms**: Limita la portabilidad

### Recomendaciones para Modernización

1. **Migración a Direct3D 11/12 o Vulkan**: Para mejor rendimiento y compatibilidad
2. **Implementación de un sistema de componentes**: Para mejorar la modularidad
3. **Separación de lógica y presentación**: Para facilitar la portabilidad
4. **Uso de formatos estándar**: Para mejorar la interoperabilidad

### Posibles Mejoras

1. **Sistema de iluminación avanzado**: Implementar iluminación dinámica
2. **Efectos de partículas mejorados**: Para efectos visuales más impresionantes
3. **Soporte para shaders personalizados**: Para efectos visuales avanzados
4. **Sistema de animación mejorado**: Para animaciones más fluidas y complejas

---

*Este informe fue generado basado en el análisis del código fuente de Crystal y artículos técnicos sobre Legend of Mir2/3.*
