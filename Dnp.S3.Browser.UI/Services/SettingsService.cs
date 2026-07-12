using Microsoft.Maui.Storage;
using System.IO;
using Microsoft.Data.Sqlite;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace Dnp.S3.Browser.UI.Services
{
    public class SettingsModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsDefault { get; set; }
        public bool UseLocalS3 { get; set; }
        public string? AccessKey { get; set; }
        public string? SecretKey { get; set; }
        public string? Mfa { get; set; }
        public string? Region { get; set; }
    }
    public class SettingsService
    {
        private readonly string _dbPath;
        private const string TableSql = @"CREATE TABLE IF NOT EXISTS Settings (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT,
            IsDefault INTEGER NOT NULL DEFAULT 0,
            UseLocalS3 INTEGER NOT NULL DEFAULT 0,
            AccessKey TEXT,
            SecretKey TEXT,
            Mfa TEXT,
            Region TEXT
        );";

        public SettingsService()
        {
            var folder = FileSystem.AppDataDirectory;
            _dbPath = Path.Combine(folder, "settings.db");
            EnsureDatabase();
            // Ensure encryption key exists
            EnsureEncryptionKey();
        }

        private void EnsureEncryptionKey()
        {
            try
            {
                // Ensure a symmetric key is stored in SecureStorage under 'settings_db_key'
                var existing = System.Threading.Tasks.Task.Run(async () => await SecureStorage.GetAsync("settings_db_key")).GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(existing))
                {
                    var key = RandomNumberGenerator.GetBytes(32);
                    var b64 = Convert.ToBase64String(key);
                    System.Threading.Tasks.Task.Run(async () => await SecureStorage.SetAsync("settings_db_key", b64)).GetAwaiter().GetResult();
                }
            }
            catch
            {
                // If secure storage isn't available, we proceed without it (DB will store plaintext)
            }
        }

        private void EnsureDatabase()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = TableSql;
            cmd.ExecuteNonQuery();
            // Ensure Name column exists and unique index on Name
            using var colCmd = conn.CreateCommand();
            colCmd.CommandText = "PRAGMA table_info('Settings');";
            using var reader = colCmd.ExecuteReader();
            var hasName = false;
            while (reader.Read())
            {
                var col = reader.GetString(1);
                if (string.Equals(col, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    hasName = true;
                    break;
                }
            }
            reader.Close();
            if (!hasName)
            {
                using var addCol = conn.CreateCommand();
                addCol.CommandText = "ALTER TABLE Settings ADD COLUMN Name TEXT;";
                try { addCol.ExecuteNonQuery(); } catch { }
            }
            // Create unique index on Name for convenience (ignore errors)
            using var idx = conn.CreateCommand();
            idx.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS IX_Settings_Name ON Settings(Name);";
            try { idx.ExecuteNonQuery(); } catch { }
        }

        public SettingsModel? GetSettings()
        {
            if (!File.Exists(_dbPath)) return null;
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, IsDefault, UseLocalS3, AccessKey, SecretKey, Mfa, Region FROM Settings WHERE IsDefault = 1 LIMIT 1;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var s = new SettingsModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                    IsDefault = reader.GetInt32(2) == 1,
                    UseLocalS3 = reader.GetInt32(3) == 1,
                    AccessKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SecretKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Mfa = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Region = reader.IsDBNull(7) ? null : reader.GetString(7)
                };

                // Attempt to decrypt SecretKey if encrypted
                if (!string.IsNullOrEmpty(s.SecretKey))
                {
                    var dec = TryDecrypt(s.SecretKey);
                    if (dec != null) s.SecretKey = dec;
                }

                return s;
            }
            return null;
        }

        public System.Collections.Generic.List<SettingsModel> GetAllSettings()
        {
            var list = new System.Collections.Generic.List<SettingsModel>();
            if (!File.Exists(_dbPath)) return list;
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, IsDefault, UseLocalS3, AccessKey, SecretKey, Mfa, Region FROM Settings ORDER BY Name COLLATE NOCASE;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SettingsModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? null : reader.GetString(1),
                    IsDefault = reader.GetInt32(2) == 1,
                    UseLocalS3 = reader.GetInt32(3) == 1,
                    AccessKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                    SecretKey = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Mfa = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Region = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }
            // Decrypt secret fields where present
            foreach (var item in list)
            {
                if (!string.IsNullOrEmpty(item.SecretKey))
                {
                    var dec = TryDecrypt(item.SecretKey);
                    if (dec != null) item.SecretKey = dec;
                }
            }
            return list;
        }

        public void SetDefaultById(int id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var tran = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Settings SET IsDefault = 0;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "UPDATE Settings SET IsDefault = 1 WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            tran.Commit();
        }

        public void DeleteById(int id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Settings WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void SaveSettings(SettingsModel s)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var tran = conn.BeginTransaction();
            using (var cmd = conn.CreateCommand())
            {
                // If marking default, clear previous defaults
                if (s.IsDefault)
                {
                    cmd.CommandText = "UPDATE Settings SET IsDefault = 0;";
                    cmd.ExecuteNonQuery();
                }

                if (s.Id > 0)
                {
                    cmd.CommandText = "UPDATE Settings SET Name = @name, IsDefault = @isdef, UseLocalS3 = @uselocal, AccessKey = @ak, SecretKey = @sk, Mfa = @mfa, Region = @reg WHERE Id = @id;";
                    cmd.Parameters.AddWithValue("@isdef", s.IsDefault ? 1 : 0);
                    cmd.Parameters.AddWithValue("@uselocal", s.UseLocalS3 ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ak", (object?)s.AccessKey ?? DBNull.Value);
                    // Encrypt secret before storing
                    var secretToStore = (object?)EncryptIfPossible(s.SecretKey) ?? DBNull.Value;
                    cmd.Parameters.AddWithValue("@sk", secretToStore);
                    cmd.Parameters.AddWithValue("@mfa", (object?)s.Mfa ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@reg", (object?)s.Region ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@name", (object?)s.Name ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", s.Id);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    cmd.CommandText = "INSERT INTO Settings (Name, IsDefault, UseLocalS3, AccessKey, SecretKey, Mfa, Region) VALUES (@name, @isdef, @uselocal, @ak, @sk, @mfa, @reg);";
                    cmd.Parameters.AddWithValue("@name", (object?)s.Name ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@isdef", s.IsDefault ? 1 : 0);
                    cmd.Parameters.AddWithValue("@uselocal", s.UseLocalS3 ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ak", (object?)s.AccessKey ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@sk", (object?)EncryptIfPossible(s.SecretKey) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@mfa", (object?)s.Mfa ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@reg", (object?)s.Region ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
            tran.Commit();
        }

        private string? TryDecrypt(string cipherText)
        {
            try
            {
                var keyB64 = System.Threading.Tasks.Task.Run(async () => await SecureStorage.GetAsync("settings_db_key")).GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(keyB64)) return null;
                var key = Convert.FromBase64String(keyB64);
                var packed = Convert.FromBase64String(cipherText);
                // packed format: nonce(12) + tag(16) + ciphertext
                if (packed.Length < 28) return null;
                var nonce = new byte[12];
                var tag = new byte[16];
                Array.Copy(packed, 0, nonce, 0, 12);
                Array.Copy(packed, 12, tag, 0, 16);
                var ctLen = packed.Length - 28;
                var ct = new byte[ctLen];
                Array.Copy(packed, 28, ct, 0, ctLen);
                var plain = new byte[ctLen];
                using var aes = new System.Security.Cryptography.AesGcm(key);
                aes.Decrypt(nonce, ct, tag, plain);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        private string? EncryptIfPossible(string? plain)
        {
            try
            {
                if (string.IsNullOrEmpty(plain)) return null;
                var keyB64 = System.Threading.Tasks.Task.Run(async () => await SecureStorage.GetAsync("settings_db_key")).GetAwaiter().GetResult();
                if (string.IsNullOrEmpty(keyB64)) return plain;
                var key = Convert.FromBase64String(keyB64);
                var nonce = RandomNumberGenerator.GetBytes(12);
                var plainBytes = Encoding.UTF8.GetBytes(plain);
                var ct = new byte[plainBytes.Length];
                var tag = new byte[16];
                using var aes = new AesGcm(key);
                aes.Encrypt(nonce, plainBytes, ct, tag);
                var packed = new byte[nonce.Length + tag.Length + ct.Length];
                Array.Copy(nonce, 0, packed, 0, nonce.Length);
                Array.Copy(tag, 0, packed, nonce.Length, tag.Length);
                Array.Copy(ct, 0, packed, nonce.Length + tag.Length, ct.Length);
                return Convert.ToBase64String(packed);
            }
            catch { return plain; }
        }
    }
}
