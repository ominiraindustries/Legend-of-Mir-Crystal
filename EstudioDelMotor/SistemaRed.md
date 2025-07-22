# Sistema de Red de Crystal

## Índice
1. [Introducción](#introducción)
2. [Arquitectura de Red](#arquitectura-de-red)
3. [Formato de Paquetes](#formato-de-paquetes)
4. [Procesamiento de Paquetes](#procesamiento-de-paquetes)
5. [Seguridad y Encriptación](#seguridad-y-encriptación)
6. [Optimizaciones](#optimizaciones)

## Introducción

El sistema de red de Crystal es fundamental para su funcionamiento como MMORPG. Basado en el diseño original de Legend of Mir2/3, utiliza una arquitectura cliente-servidor con comunicación TCP/IP. Este documento analiza en detalle cómo funciona este sistema, desde la estructura de los paquetes hasta el procesamiento de mensajes.

## Arquitectura de Red

### Componentes Principales

El sistema de red de Crystal se compone de:

1. **Servidor**:
   - `MirConnection`: Gestiona conexiones individuales de clientes
   - `Envir`: Coordina todas las conexiones y procesa mensajes

2. **Cliente**:
   - `Network`: Gestiona la conexión con el servidor
   - `Packet`: Procesa y serializa/deserializa paquetes

### Inicialización del Servidor

El servidor inicializa su sistema de red en el método `StartNetwork()`:

```csharp
private void StartNetwork()
{
    Connections.Clear();
    LoadAccounts();
    LoadGuilds();
    LoadConquests();
    
    _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
    _listener.Start();
    _listener.BeginAcceptTcpClient(Connection, null);
    
    if (StatusPortEnabled)
    {
        _StatusPort = new TcpListener(IPAddress.Parse(Settings.IPAddress), 3000);
        _StatusPort.Start();
        _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
    }
    
    MessageQueue.Enqueue("Network Started.");
}
```

### Inicialización del Cliente

El cliente inicializa su conexión en el método `Connect()`:

```csharp
public static void Connect()
{
    if (_client != null)
        Disconnect();
    
    ConnectAttempt++;
    
    _client = new TcpClient {NoDelay = true};
    _client.BeginConnect(Settings.IPAddress, Settings.Port, Connection, null);
}
```

### Gestión de Conexiones

Cada conexión de cliente se gestiona como un objeto `MirConnection`:

```csharp
public MirConnection(int sessionID, TcpClient client)
{
    SessionID = sessionID;
    IPAddress = client.Client.RemoteEndPoint.ToString().Split(':')[0];
    
    _client = client;
    _client.NoDelay = true;
    
    TimeConnected = Envir.Time;
    TimeOutTime = TimeConnected + Settings.TimeOut;
    
    _receiveList = new ConcurrentQueue<Packet>();
    _sendList = new ConcurrentQueue<Packet>();
    _retryList = new Queue<Packet>();
    
    _sendList.Enqueue(new S.Connected());
    
    Connected = true;
    BeginReceive();
}
```

## Formato de Paquetes

### Estructura Básica

Todos los paquetes en Crystal siguen una estructura común:

```
Estructura de paquete:
- 2 bytes: Longitud del paquete (incluyendo cabecera)
- 2 bytes: ID del paquete
- N bytes: Datos específicos del paquete
```

### Serialización

La serialización de paquetes se realiza en el método `GetPacketBytes()`:

```csharp
public byte[] GetPacketBytes()
{
    using (MemoryStream stream = new MemoryStream())
    {
        stream.Write(new byte[2], 0, 2);
        
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((short)Index);
            WritePacket(writer);
        }
        
        byte[] data = stream.ToArray();
        short len = (short)data.Length;
        
        data[0] = (byte)(len & 0xFF);
        data[1] = (byte)((len >> 8) & 0xFF);
        
        return data;
    }
}
```

### Deserialización

La deserialización de paquetes se realiza en el método `ReceivePacket()`:

```csharp
public static Packet ReceivePacket(byte[] rawBytes, out byte[] extra)
{
    extra = rawBytes;
    Packet p;
    
    if (rawBytes.Length < 4)
        return null;
    
    // 2 bytes: Longitud, 2 bytes: ID
    int length = (rawBytes[1] << 8) + rawBytes[0];
    
    if (length > rawBytes.Length || length < 2)
        return null;
    
    using (MemoryStream stream = new MemoryStream(rawBytes, 2, length - 2))
    using (BinaryReader reader = new BinaryReader(stream))
    {
        try
        {
            short id = reader.ReadInt16();
            p = IsServer ? GetClientPacket(id) : GetServerPacket(id);
            
            if (p == null)
                return null;
            
            p.ReadPacket(reader);
        }
        catch
        {
            return null;
        }
    }
    
    extra = new byte[rawBytes.Length - length];
    Buffer.BlockCopy(rawBytes, length, extra, 0, rawBytes.Length - length);
    
    return p;
}
```

## Procesamiento de Paquetes

### Servidor

El servidor procesa los paquetes en el método `ProcessNetwork()`:

```csharp
private void ProcessNetwork()
{
    Packet p;
    
    for (int i = 0; i < Connections.Count; i++)
    {
        MirConnection connection = Connections[i];
        
        if (connection == null || !connection.Connected)
        {
            Connections.RemoveAt(i);
            continue;
        }
        
        // Procesar paquetes recibidos
        while (connection.DataAvailable && !connection.Disconnecting)
        {
            if (!connection._receiveList.TryDequeue(out p))
                break;
            
            ProcessPacket(connection, p);
        }
        
        // Enviar paquetes pendientes
        while (connection._sendList.Count > 0)
        {
            if (!connection._sendList.TryDequeue(out p))
                break;
            
            connection.Enqueue(p);
        }
        
        // Verificar timeout
        if (connection.TimeOutTime < Time && connection.TimeOutTime != 0)
        {
            connection.Disconnect("Timeout");
            continue;
        }
    }
}
```

### Cliente

El cliente procesa los paquetes en el método `Process()`:

```csharp
public static void Process()
{
    if (_client == null || !_client.Connected)
        return;
    
    while (_receiveList.Count > 0)
    {
        Packet p = _receiveList.Dequeue();
        
        if (p == null)
            continue;
        
        switch (p.Index)
        {
            case (short)ServerPacketIds.Connected:
                ProcessConnected((S.Connected)p);
                break;
            case (short)ServerPacketIds.ClientVersion:
                ProcessClientVersion((S.ClientVersion)p);
                break;
            // Otros casos...
        }
    }
    
    while (_sendList.Count > 0)
    {
        Packet p = _sendList.Dequeue();
        
        if (p == null)
            continue;
        
        _rawData = p.GetPacketBytes();
        
        try
        {
            _client.Client.BeginSend(_rawData, 0, _rawData.Length, SocketFlags.None, SendData, null);
        }
        catch
        {
            Disconnect();
            return;
        }
    }
}
```

## Seguridad y Encriptación

### Verificación de Versión

Crystal implementa una verificación de versión para asegurar la compatibilidad entre cliente y servidor:

```csharp
public void ProcessClientVersion(C.ClientVersion p)
{
    if (p.VersionHash != Settings.VersionHash)
    {
        Disconnecting = true;
        Enqueue(new S.ClientVersionMismatch());
        return;
    }
    
    Enqueue(new S.ClientVersion());
}
```

### Autenticación

El proceso de autenticación verifica las credenciales del usuario:

```csharp
public void ProcessLogin(C.Login p)
{
    if (Account != null)
        return;
    
    if (p.AccountID.Length > Globals.MaxAccountIDLength)
    {
        Disconnecting = true;
        return;
    }
    
    if (p.Password.Length > Globals.MaxPasswordLength)
    {
        Disconnecting = true;
        return;
    }
    
    Account = Envir.GetAccount(p.AccountID);
    
    if (Account == null)
    {
        Enqueue(new S.LoginFailed { Reason = 0 });
        return;
    }
    
    if (!Account.MatchPassword(p.Password))
    {
        Enqueue(new S.LoginFailed { Reason = 1 });
        return;
    }
    
    // Verificaciones adicionales...
    
    Enqueue(new S.LoginSuccess { Characters = characters });
}
```

## Optimizaciones

### Manejo de Paquetes Fragmentados

Crystal implementa un sistema para manejar paquetes TCP fragmentados:

```csharp
private void ReceiveData(IAsyncResult result)
{
    int dataRead;
    
    try
    {
        dataRead = _client.Client.EndReceive(result);
    }
    catch
    {
        Disconnecting = true;
        return;
    }
    
    if (dataRead == 0)
    {
        Disconnecting = true;
        return;
    }
    
    byte[] rawBytes = result.AsyncState as byte[];
    byte[] temp = _rawData;
    _rawData = new byte[dataRead + temp.Length];
    Buffer.BlockCopy(temp, 0, _rawData, 0, temp.Length);
    Buffer.BlockCopy(rawBytes, 0, _rawData, temp.Length, dataRead);
    
    Packet p;
    while ((p = Packet.ReceivePacket(_rawData, out _rawData)) != null)
        _receiveList.Enqueue(p);
    
    BeginReceive();
}
```

### Colas Concurrentes

Crystal utiliza colas concurrentes para evitar problemas de sincronización:

```csharp
_receiveList = new ConcurrentQueue<Packet>();
_sendList = new ConcurrentQueue<Packet>();
```

### Reintentos de Envío

El sistema implementa reintentos para paquetes importantes:

```csharp
public void Enqueue(Packet p)
{
    if (!Connected || p == null)
        return;
    
    if (p.Index != (short)ServerPacketIds.Disconnect && p.Index != (short)ServerPacketIds.ClientVersion)
        _retryList.Enqueue(p);
    
    _rawData = p.GetPacketBytes();
    
    try
    {
        _client.Client.BeginSend(_rawData, 0, _rawData.Length, SocketFlags.None, SendData, null);
    }
    catch
    {
        Disconnecting = true;
    }
}
```

---

*Este informe fue generado basado en el análisis del código fuente de Crystal y artículos técnicos sobre Legend of Mir2/3.*
