# Implementación de SQLite para Crystal

## Índice
1. [Introducción](#introducción)
2. [Ventajas de SQLite](#ventajas-de-sqlite)
3. [Diseño de la Base de Datos](#diseño-de-la-base-de-datos)
4. [Implementación en C#](#implementación-en-c)
5. [Migración de Datos Existentes](#migración-de-datos-existentes)
6. [Optimizaciones](#optimizaciones)
7. [Compatibilidad Multiplataforma](#compatibilidad-multiplataforma)

## Introducción

Este documento detalla la implementación de SQLite como sistema de persistencia de datos para Crystal, reemplazando el sistema actual de serialización binaria. Esta implementación está diseñada para ser compatible tanto con Windows como con Linux.

## Ventajas de SQLite

SQLite ofrece numerosas ventajas sobre la serialización binaria:

1. **Integridad de datos**: Transacciones ACID que garantizan la consistencia de los datos
2. **Consultas flexibles**: Capacidad para realizar consultas complejas con SQL
3. **Rendimiento**: Optimizado para operaciones de lectura/escritura frecuentes
4. **Compatibilidad multiplataforma**: Funciona igual en Windows y Linux
5. **Escalabilidad**: Soporta bases de datos de hasta 140 terabytes
6. **Mantenimiento**: Herramientas para backup, compactación y reparación
7. **Seguridad**: Soporte para cifrado (con extensiones)

## Diseño de la Base de Datos

### Esquema de Tablas

```sql
-- Tabla de Cuentas
CREATE TABLE Accounts (
    AccountID TEXT PRIMARY KEY,
    Password TEXT NOT NULL,
    UserName TEXT,
    SecretQuestion TEXT,
    SecretAnswer TEXT,
    EMailAddress TEXT,
    CreationIP TEXT,
    CreationDate TEXT,
    LastIP TEXT,
    LastDate TEXT,
    Banned INTEGER DEFAULT 0,
    BanReason TEXT,
    ExpiryDate TEXT,
    ActivationID TEXT,
    WrongPasswordCount INTEGER DEFAULT 0
);

-- Tabla de Personajes
CREATE TABLE Characters (
    CharacterID INTEGER PRIMARY KEY AUTOINCREMENT,
    AccountID TEXT NOT NULL,
    Name TEXT NOT NULL UNIQUE,
    Level INTEGER DEFAULT 1,
    Class INTEGER,
    Gender INTEGER,
    Hair INTEGER,
    CurrentMapID INTEGER,
    CurrentLocationX INTEGER,
    CurrentLocationY INTEGER,
    Direction INTEGER,
    HP INTEGER,
    MP INTEGER,
    Experience BIGINT,
    Gold BIGINT,
    LastLoginDate TEXT,
    LastLogoutDate TEXT,
    Deleted INTEGER DEFAULT 0,
    DeleteDate TEXT,
    FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID)
);

-- Tabla de Estadísticas de Personajes
CREATE TABLE CharacterStats (
    CharacterID INTEGER PRIMARY KEY,
    AC INTEGER DEFAULT 0,
    MAC INTEGER DEFAULT 0,
    DC INTEGER DEFAULT 0,
    MC INTEGER DEFAULT 0,
    SC INTEGER DEFAULT 0,
    Accuracy INTEGER DEFAULT 0,
    Agility INTEGER DEFAULT 0,
    HP INTEGER DEFAULT 0,
    MP INTEGER DEFAULT 0,
    MaxHP INTEGER DEFAULT 0,
    MaxMP INTEGER DEFAULT 0,
    BagWeight INTEGER DEFAULT 0,
    WearWeight INTEGER DEFAULT 0,
    HandWeight INTEGER DEFAULT 0,
    FOREIGN KEY (CharacterID) REFERENCES Characters(CharacterID)
);

-- Tabla de Items
CREATE TABLE Items (
    ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
    CharacterID INTEGER,
    ItemIndex INTEGER NOT NULL,
    CurrentDura INTEGER,
    MaxDura INTEGER,
    Count INTEGER DEFAULT 1,
    Slots TEXT,
    Rare INTEGER DEFAULT 0,
    Identified INTEGER DEFAULT 0,
    Cursed INTEGER DEFAULT 0,
    WeddingRing INTEGER DEFAULT 0,
    ExpireInfo TEXT,
    SealedInfo TEXT,
    RentalInfo TEXT,
    Flags INTEGER DEFAULT 0,
    Location INTEGER,
    Grid INTEGER,
    FOREIGN KEY (CharacterID) REFERENCES Characters(CharacterID)
);

-- Tabla de Gremios
CREATE TABLE Guilds (
    GuildID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    LeaderID INTEGER NOT NULL,
    Notice TEXT,
    CreationDate TEXT,
    Gold BIGINT DEFAULT 0,
    Level INTEGER DEFAULT 0,
    Experience BIGINT DEFAULT 0,
    FOREIGN KEY (LeaderID) REFERENCES Characters(CharacterID)
);

-- Tabla de Miembros de Gremio
CREATE TABLE GuildMembers (
    CharacterID INTEGER NOT NULL,
    GuildID INTEGER NOT NULL,
    Rank INTEGER DEFAULT 0,
    JoinDate TEXT,
    PRIMARY KEY (CharacterID, GuildID),
    FOREIGN KEY (CharacterID) REFERENCES Characters(CharacterID),
    FOREIGN KEY (GuildID) REFERENCES Guilds(GuildID)
);
```

### Índices para Optimización

```sql
-- Índices para mejorar el rendimiento de consultas comunes
CREATE INDEX idx_characters_account ON Characters(AccountID);
CREATE INDEX idx_items_character ON Items(CharacterID);
CREATE INDEX idx_guild_members_guild ON GuildMembers(GuildID);
```

## Implementación en C#

### Configuración de SQLite

```csharp
using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace Server.MirDatabase
{
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

        private void CreateDatabase()
        {
            SQLiteConnection.CreateFile(_connectionString.Replace("Data Source=", "").Replace(";Version=3;", ""));
            
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                
                // Crear tablas
                ExecuteNonQuery(connection, CreateAccountsTableSQL);
                ExecuteNonQuery(connection, CreateCharactersTableSQL);
                ExecuteNonQuery(connection, CreateCharacterStatsTableSQL);
                ExecuteNonQuery(connection, CreateItemsTableSQL);
                ExecuteNonQuery(connection, CreateGuildsTableSQL);
                ExecuteNonQuery(connection, CreateGuildMembersTableSQL);
                
                // Crear índices
                ExecuteNonQuery(connection, "CREATE INDEX idx_characters_account ON Characters(AccountID);");
                ExecuteNonQuery(connection, "CREATE INDEX idx_items_character ON Items(CharacterID);");
                ExecuteNonQuery(connection, "CREATE INDEX idx_guild_members_guild ON GuildMembers(GuildID);");
            }
        }

        private void ExecuteNonQuery(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        // Definiciones de SQL para crear tablas
        private const string CreateAccountsTableSQL = @"
            CREATE TABLE Accounts (
                AccountID TEXT PRIMARY KEY,
                Password TEXT NOT NULL,
                UserName TEXT,
                SecretQuestion TEXT,
                SecretAnswer TEXT,
                EMailAddress TEXT,
                CreationIP TEXT,
                CreationDate TEXT,
                LastIP TEXT,
                LastDate TEXT,
                Banned INTEGER DEFAULT 0,
                BanReason TEXT,
                ExpiryDate TEXT,
                ActivationID TEXT,
                WrongPasswordCount INTEGER DEFAULT 0
            );";

        // Otras definiciones de tablas...
    }
}
```

### Métodos para Gestión de Cuentas

```csharp
public class SQLiteDatabase
{
    // ... código anterior ...

    public void SaveAccount(AccountInfo account)
    {
        lock (_locker)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Verificar si la cuenta existe
                        bool exists = false;
                        using (var command = new SQLiteCommand("SELECT COUNT(*) FROM Accounts WHERE AccountID = @AccountID", connection))
                        {
                            command.Parameters.AddWithValue("@AccountID", account.AccountID);
                            exists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                        }

                        // Insertar o actualizar la cuenta
                        string sql = exists
                            ? "UPDATE Accounts SET Password = @Password, UserName = @UserName, SecretQuestion = @SecretQuestion, SecretAnswer = @SecretAnswer, EMailAddress = @EMailAddress, CreationIP = @CreationIP, CreationDate = @CreationDate, LastIP = @LastIP, LastDate = @LastDate, Banned = @Banned, BanReason = @BanReason, ExpiryDate = @ExpiryDate, ActivationID = @ActivationID, WrongPasswordCount = @WrongPasswordCount WHERE AccountID = @AccountID"
                            : "INSERT INTO Accounts (AccountID, Password, UserName, SecretQuestion, SecretAnswer, EMailAddress, CreationIP, CreationDate, LastIP, LastDate, Banned, BanReason, ExpiryDate, ActivationID, WrongPasswordCount) VALUES (@AccountID, @Password, @UserName, @SecretQuestion, @SecretAnswer, @EMailAddress, @CreationIP, @CreationDate, @LastIP, @LastDate, @Banned, @BanReason, @ExpiryDate, @ActivationID, @WrongPasswordCount)";

                        using (var command = new SQLiteCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@AccountID", account.AccountID);
                            command.Parameters.AddWithValue("@Password", account.Password);
                            command.Parameters.AddWithValue("@UserName", account.UserName ?? "");
                            command.Parameters.AddWithValue("@SecretQuestion", account.SecretQuestion ?? "");
                            command.Parameters.AddWithValue("@SecretAnswer", account.SecretAnswer ?? "");
                            command.Parameters.AddWithValue("@EMailAddress", account.EMailAddress ?? "");
                            command.Parameters.AddWithValue("@CreationIP", account.CreationIP ?? "");
                            command.Parameters.AddWithValue("@CreationDate", account.CreationDate.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@LastIP", account.LastIP ?? "");
                            command.Parameters.AddWithValue("@LastDate", account.LastDate.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@Banned", account.Banned ? 1 : 0);
                            command.Parameters.AddWithValue("@BanReason", account.BanReason ?? "");
                            command.Parameters.AddWithValue("@ExpiryDate", account.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.Parameters.AddWithValue("@ActivationID", account.ActivationID ?? "");
                            command.Parameters.AddWithValue("@WrongPasswordCount", account.WrongPasswordCount);
                            command.ExecuteNonQuery();
                        }

                        // Guardar personajes
                        foreach (var character in account.Characters)
                        {
                            SaveCharacter(connection, character, account.AccountID);
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception($"Error al guardar cuenta {account.AccountID}: {ex.Message}", ex);
                    }
                }
            }
        }
    }

    public AccountInfo GetAccount(string accountId)
    {
        lock (_locker)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                
                // Obtener datos de la cuenta
                using (var command = new SQLiteCommand("SELECT * FROM Accounts WHERE AccountID = @AccountID", connection))
                {
                    command.Parameters.AddWithValue("@AccountID", accountId);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null;
                        
                        var account = new AccountInfo
                        {
                            AccountID = reader["AccountID"].ToString(),
                            Password = reader["Password"].ToString(),
                            UserName = reader["UserName"].ToString(),
                            SecretQuestion = reader["SecretQuestion"].ToString(),
                            SecretAnswer = reader["SecretAnswer"].ToString(),
                            EMailAddress = reader["EMailAddress"].ToString(),
                            CreationIP = reader["CreationIP"].ToString(),
                            CreationDate = DateTime.Parse(reader["CreationDate"].ToString()),
                            LastIP = reader["LastIP"].ToString(),
                            LastDate = DateTime.Parse(reader["LastDate"].ToString()),
                            Banned = Convert.ToBoolean(reader["Banned"]),
                            BanReason = reader["BanReason"].ToString(),
                            ExpiryDate = DateTime.Parse(reader["ExpiryDate"].ToString()),
                            ActivationID = reader["ActivationID"].ToString(),
                            WrongPasswordCount = Convert.ToInt32(reader["WrongPasswordCount"])
                        };
                        
                        // Cargar personajes
                        account.Characters = GetCharacters(connection, accountId);
                        
                        return account;
                    }
                }
            }
        }
    }

    // Otros métodos para gestión de cuentas, personajes, items, etc.
}
```

### Integración con el Sistema Existente

```csharp
public class Envir
{
    // ... código existente ...

    private SQLiteDatabase _database;

    public void InitializeDatabase()
    {
        string dbPath = Path.Combine(Settings.DataPath, "Crystal.db");
        _database = new SQLiteDatabase(dbPath);
    }

    public void SaveAccounts()
    {
        foreach (var account in AccountList)
        {
            _database.SaveAccount(account);
        }
    }

    public void LoadAccounts()
    {
        AccountList = _database.GetAllAccounts();
    }

    public AccountInfo GetAccount(string accountId)
    {
        return _database.GetAccount(accountId);
    }

    // Otros métodos adaptados para usar SQLite
}
```

## Migración de Datos Existentes

Para migrar los datos del sistema actual de serialización binaria a SQLite:

```csharp
public void MigrateFromBinaryToSQLite()
{
    // Cargar datos del sistema antiguo
    List<AccountInfo> accounts = LoadAccountsFromBinary();
    
    // Guardar en SQLite
    foreach (var account in accounts)
    {
        _database.SaveAccount(account);
    }
}

private List<AccountInfo> LoadAccountsFromBinary()
{
    if (File.Exists(AccountPath))
    {
        using (FileStream stream = File.OpenRead(AccountPath))
        {
            return (List<AccountInfo>)new BinaryFormatter().Deserialize(stream);
        }
    }
    
    return new List<AccountInfo>();
}
```

## Optimizaciones

### Uso de Transacciones

Para operaciones que afectan a múltiples registros, es crucial usar transacciones:

```csharp
using (var connection = new SQLiteConnection(_connectionString))
{
    connection.Open();
    using (var transaction = connection.BeginTransaction())
    {
        try
        {
            // Múltiples operaciones aquí
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

### Consultas Parametrizadas

Siempre usar consultas parametrizadas para evitar inyección SQL:

```csharp
using (var command = new SQLiteCommand("SELECT * FROM Characters WHERE AccountID = @AccountID", connection))
{
    command.Parameters.AddWithValue("@AccountID", accountId);
    // ...
}
```

### Índices Estratégicos

Crear índices para campos frecuentemente consultados:

```sql
CREATE INDEX idx_characters_name ON Characters(Name);
CREATE INDEX idx_items_itemindex ON Items(ItemIndex);
```

### Configuración de Caché

Optimizar la configuración de caché de SQLite:

```csharp
using (var command = new SQLiteCommand("PRAGMA cache_size = 10000", connection))
{
    command.ExecuteNonQuery();
}
```

## Compatibilidad Multiplataforma

SQLite es inherentemente multiplataforma, pero hay algunas consideraciones adicionales:

### Rutas de Archivo

Usar `Path.Combine` y `Path.DirectorySeparatorChar` para rutas compatibles:

```csharp
string dbPath = Path.Combine(Settings.DataPath, "Crystal.db");
```

### Permisos de Archivo

En Linux, asegurarse de que los permisos de archivo son correctos:

```csharp
// Verificar permisos en Linux
if (OperatingSystem.IsLinux())
{
    try
    {
        // Intentar crear un archivo temporal para verificar permisos
        string testPath = Path.Combine(Path.GetDirectoryName(dbPath), "test.tmp");
        File.WriteAllText(testPath, "test");
        File.Delete(testPath);
    }
    catch (UnauthorizedAccessException)
    {
        throw new Exception($"No tienes permisos para escribir en {Path.GetDirectoryName(dbPath)}");
    }
}
```

### Bloqueo de Archivos

Manejar adecuadamente el bloqueo de archivos:

```csharp
public void BackupDatabase()
{
    string backupPath = Path.Combine(Settings.DataPath, $"Crystal_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
    
    using (var connection = new SQLiteConnection(_connectionString))
    {
        connection.Open();
        using (var command = new SQLiteCommand("BEGIN IMMEDIATE", connection))
        {
            command.ExecuteNonQuery();
        }
        
        using (var backupConnection = new SQLiteConnection($"Data Source={backupPath};Version=3;"))
        {
            backupConnection.Open();
            connection.BackupDatabase(backupConnection, "main", "main", -1, null, 0);
        }
    }
}
```

---

*Este informe fue generado basado en el análisis del código fuente de Crystal y las mejores prácticas para implementación de SQLite en aplicaciones .NET multiplataforma.*
