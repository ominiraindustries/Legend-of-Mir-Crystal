# Sistema de Gráficos de Crystal

## Índice
1. [Introducción](#introducción)
2. [Formato de Archivos .lib](#formato-de-archivos-lib)
3. [Sistema de Renderizado](#sistema-de-renderizado)
4. [Gestión de Recursos Gráficos](#gestión-de-recursos-gráficos)
5. [Tipos de Gráficos](#tipos-de-gráficos)
6. [Extracción y Conversión](#extracción-y-conversión)

## Introducción

El sistema gráfico de Crystal se basa en el motor original de Legend of Mir2/3, utilizando Direct3D 9 para el renderizado y un formato propietario (.lib) para almacenar los recursos gráficos. Este documento analiza en detalle cómo funciona este sistema, centrándose en el formato de archivos, el proceso de renderizado y la gestión de recursos.

## Formato de Archivos .lib

### Estructura General

Los archivos .lib son contenedores de imágenes con la siguiente estructura:

```
Estructura de un archivo .lib:
- Cabecera:
  - 2 bytes: Versión (1, 2 o 3)
  - 2 bytes: Número de imágenes
- Tabla de índices (solo en versión 3+):
  - Array de posiciones (8 bytes por imagen)
- Para cada imagen:
  - 2 bytes: Ancho
  - 2 bytes: Alto
  - 2 bytes: Offset X
  - 2 bytes: Offset Y
  - 2 bytes: ShadowX
  - 2 bytes: ShadowY
  - 1 byte: Shadow (bit 7 indica presencia de máscara)
  - 4 bytes: Longitud de datos
  - N bytes: Datos de imagen comprimidos con GZip
  - Si tiene máscara:
    - 2 bytes: Ancho de máscara
    - 2 bytes: Alto de máscara
    - 2 bytes: Offset X de máscara
    - 2 bytes: Offset Y de máscara
    - 4 bytes: Longitud de datos de máscara
    - N bytes: Datos de máscara comprimidos con GZip
```

### Algoritmo de Compresión

Crystal utiliza compresión GZip estándar para los datos de imagen:

```csharp
private static void DecompressImage(byte[] data, Stream destination)
{
    using (var stream = new GZipStream(new MemoryStream(data), CompressionMode.Decompress))
    {
        stream.CopyTo(destination);
    }
}
```

### Formato de Imagen

Las imágenes descomprimidas utilizan el formato ARGB de 32 bits (8 bits por canal):

- Canal A: Transparencia (0 = transparente, 255 = opaco)
- Canal R: Componente rojo (0-255)
- Canal G: Componente verde (0-255)
- Canal B: Componente azul (0-255)

## Sistema de Renderizado

### Inicialización de Direct3D

El sistema de renderizado se inicializa en `DXManager.cs`:

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

### Resoluciones Soportadas

Crystal soporta múltiples resoluciones, que afectan directamente al área visible del mapa:

| Resolución | Área Visible (Tiles) | Recomendación |
|------------|----------------------|---------------|
| 800x600    | ~16x18 tiles         | Equipos antiguos |
| 1024x768   | ~21x24 tiles         | Resolución estándar |
| 1366x768   | ~28x24 tiles         | Pantallas panorámicas |
| 1920x1080  | ~40x33 tiles         | Monitores modernos |

El cálculo del área visible se realiza en `MapControl.cs`:

```csharp
OffSetX = Settings.ScreenWidth / 2 / CellWidth;
OffSetY = Settings.ScreenHeight / 2 / CellHeight - 1;
ViewRangeX = OffSetX + 4;
ViewRangeY = OffSetY + 4;
```

### Pipeline de Renderizado

El proceso de renderizado se ejecuta en cada frame:

1. **Limpieza del buffer**:
   ```csharp
   DXManager.Device.Clear(ClearFlags.Target, Color.Black, 0, 0);
   ```

2. **Inicio de escena**:
   ```csharp
   DXManager.Device.BeginScene();
   DXManager.Sprite.Begin(SpriteFlags.AlphaBlend);
   ```

3. **Renderizado por capas**:
   - Fondo del mapa (DrawFloor)
   - Objetos del mapa (DrawObjects)
   - Personajes y monstruos (DrawObjects)
   - Efectos visuales (DrawEffects)
   - Nombres y etiquetas (DrawName)

4. **Finalización**:
   ```csharp
   DXManager.Sprite.End();
   DXManager.Device.EndScene();
   DXManager.Device.Present();
   ```

### Técnicas de Renderizado

#### Renderizado de Tiles

Los tiles se renderizan en una cuadrícula isométrica:

```csharp
for (int y = User.Movement.Y - ViewRangeY; y <= User.Movement.Y + ViewRangeY; y++)
{
    drawY = (y - User.Movement.Y + OffSetY) * CellHeight + User.OffSetMove.Y;
    
    for (int x = User.Movement.X - ViewRangeX; x <= User.Movement.X + ViewRangeX; x++)
    {
        drawX = (x - User.Movement.X + OffSetX) * CellWidth + User.OffSetMove.X;
        
        Libraries.MapLibs[M2CellInfo[x, y].BackIndex].Draw(index, drawX, drawY);
    }
}
```

#### Alpha Blending

Crystal utiliza diferentes modos de blending:

1. **Blending Normal**:
   ```csharp
   Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
   Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
   ```

2. **Blending para Efectos**:
   ```csharp
   Device.SetRenderState(RenderState.SourceBlend, Blend.BlendFactor);
   Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseBlendFactor);
   ```

## Gestión de Recursos Gráficos

### Carga de Bibliotecas

Las bibliotecas gráficas se cargan al inicio del juego:

```csharp
static Libraries()
{
    // Armaduras de personajes
    InitLibrary(ref CArmours, Settings.CArmourPath, "00");
    
    // Armas
    InitLibrary(ref CWeapons, Settings.CWeaponPath, "00");
    
    // Monstruos
    InitLibrary(ref Monsters, Settings.MonsterPath, "000");
    
    // Mapas
    MapLibs[0] = new MLibrary(Settings.DataPath + "Map\\WemadeMir2\\Tiles");
    // ...
}
```

### Gestión de Memoria

Crystal implementa un sistema de liberación automática de texturas:

```csharp
public void CheckTexture()
{
    if (!TextureValid) return;
    
    if (CMain.Time > CleanTime)
    {
        DisposeTexture();
    }
}
```

## Tipos de Gráficos

### Tiles de Mapa

Los tiles de mapa tienen características específicas:

- **Tamaño base**: 48x32 píxeles (celda lógica)
- **Tamaño real**: Generalmente 96x64 píxeles (con superposición)
- **Organización**: Por tipo de terreno y capa

### Personajes y Monstruos

Los gráficos de personajes y monstruos se organizan por:

- **Dirección**: 8 direcciones (0-7)
- **Acción**: Diferentes acciones (caminar, atacar, morir)
- **Frame**: Frames de animación para cada acción

Ejemplo de acceso a un frame específico:

```csharp
Libraries.Monsters[(ushort)Monster.BoneLord].DrawBlend(
    400 + FrameIndex + (int)Direction * 4,  // Índice base + frame + dirección
    DrawLocation,
    Color.White,
    true
);
```

### Efectos Visuales

Los efectos visuales utilizan:

- **Animaciones**: Secuencias de frames
- **Blending**: Modos especiales de mezcla
- **Máscaras**: Para efectos de iluminación y brillo

## Extracción y Conversión

### Herramienta de Extracción

La herramienta `lib_extractor.py` permite extraer imágenes de archivos .lib:

```python
def extract_all_libs(root_dir, output_dir, extract_masks=True, combine_masks=False):
    """Extrae todos los archivos .lib manteniendo la estructura de directorios"""
    for root, dirs, files in os.walk(root_dir):
        rel_path = os.path.relpath(root, root_dir)
        
        for file in files:
            if file.lower().endswith('.lib'):
                lib_path = os.path.join(root, file)
                
                if rel_path == '.':
                    lib_output_dir = os.path.join(output_dir, os.path.splitext(file)[0])
                else:
                    lib_output_dir = os.path.join(output_dir, rel_path, os.path.splitext(file)[0])
                
                try:
                    extractor = LibExtractor(lib_path)
                    extractor.extract_all(lib_output_dir, extract_masks, combine_masks)
                except Exception as e:
                    print(f"Error al procesar {lib_path}: {e}")
```

### Proceso de Descompresión

El proceso de descompresión utiliza GZip:

```python
with io.BytesIO(compressed_data) as compressed_stream:
    with gzip.GzipFile(fileobj=compressed_stream, mode='rb') as gz_stream:
        image_data = gz_stream.read()
```

### Conversión a Formatos Estándar

Las imágenes extraídas se convierten a PNG:

```python
img = Image.new('RGBA', (width, height), (0, 0, 0, 0))
pixels = img.load()

# Procesar datos de imagen
for y in range(height):
    for x in range(width):
        if y * width + x < len(image_data) // 4:
            index = (y * width + x) * 4
            b = image_data[index]
            g = image_data[index + 1]
            r = image_data[index + 2]
            a = image_data[index + 3]
            pixels[x, y] = (r, g, b, a)

# Guardar imagen
img.save(os.path.join(output_dir, f"{i}.png"), "PNG")
```

---

*Este informe fue generado basado en el análisis del código fuente de Crystal y artículos técnicos sobre Legend of Mir2/3.*
