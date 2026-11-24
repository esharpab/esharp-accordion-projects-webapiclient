using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient;

/// <summary>
/// The recovery drive's file and hash (firmware 7.1.0): the client makes the drive, never the station. A random secret
/// of <see cref="SecretBytes"/> bytes stays with the client (<see cref="RecoveryDriveKey"/>); the file
/// <c>accordion-recovery.json</c> holds the station, when it was made (UTC, whole seconds) and <see cref="Hash"/>, never
/// the secret. The station recomputes the hash from the secret a reset sends, so only the client that made the drive
/// can use it.
/// </summary>
public static class RecoveryDrive
{
    /// <summary>The file's name, in the top folder of a FAT32 or exFAT USB drive.</summary>
    public const string FileName = "accordion-recovery.json";

    /// <summary>The file's <c>kind</c>.</summary>
    public const string Kind = "accordion-recovery";

    /// <summary>The secret's length in bytes (sent as 64 hex digits).</summary>
    public const int SecretBytes = 32;

    /// <summary>A time as the file carries it: <c>yyyy-MM-ddTHH:mm:ssZ</c> (UTC).</summary>
    public static string FormatCreated(DateTimeOffset at) =>
        at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// The drive's hash: lower-case hex of SHA-256 over the secret's bytes, a 0x00 byte, the station's name in upper case
    /// (UTF-8), a 0x00 byte and <paramref name="created"/> as the file carries it (UTF-8). The station, the Web UI and
    /// the Python client compute the same.
    /// </summary>
    public static string Hash(byte[] secret, string station, string created)
    {
        if (secret is null) throw new ArgumentNullException(nameof(secret));
        if (station is null) throw new ArgumentNullException(nameof(station));
        if (created is null) throw new ArgumentNullException(nameof(created));
        var name = Encoding.UTF8.GetBytes(station.Trim().ToUpperInvariant());
        var time = Encoding.UTF8.GetBytes(created);
        var data = new byte[secret.Length + 1 + name.Length + 1 + time.Length];
        Buffer.BlockCopy(secret, 0, data, 0, secret.Length);
        Buffer.BlockCopy(name, 0, data, secret.Length + 1, name.Length);
        Buffer.BlockCopy(time, 0, data, secret.Length + 1 + name.Length + 1, time.Length);
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(data));
    }

    /// <summary>
    /// A new drive for <paramref name="station"/> (its host name, which is its serial number; <c>GET
    /// /api/security/recovery</c> answers it as <c>station</c>), made at <paramref name="now"/> (default: this computer's
    /// clock): the file's bytes and the key to keep.
    /// </summary>
    public static (byte[] File, RecoveryDriveKey Key) Make(string station, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(station)) throw new ArgumentException("The station's name is needed", nameof(station));
        var secret = new byte[SecretBytes];
        using (var random = RandomNumberGenerator.Create())
            random.GetBytes(secret);
        var created = FormatCreated(now ?? DateTimeOffset.UtcNow);
        var hash = Hash(secret, station, created);
        var file = new JObject
        {
            ["kind"] = Kind,
            ["station"] = station.Trim(),
            ["created"] = created,
            ["hash"] = hash,
        };
        var key = new RecoveryDriveKey(station.Trim().ToUpperInvariant(), created, ToHex(secret));
        return (Encoding.UTF8.GetBytes(file.ToString(Formatting.None) + "\n"), key);
    }

    internal static string ToHex(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            text.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return text.ToString();
    }
}

/// <summary>
/// What the client keeps of a recovery drive it made: the station, when it was made and the secret (64 hex digits). It
/// is the drive's password: keep it where only you can read it, and drop it once the drive is used or 24 hours old.
/// </summary>
public sealed class RecoveryDriveKey
{
    /// <summary>The key file's <c>kind</c>.</summary>
    public const string KeyKind = "accordion-recovery-key";

    public RecoveryDriveKey(string station, string created, string secret)
    {
        Station = station;
        Created = created;
        Secret = secret;
    }

    public string Station { get; }
    public string Created { get; }
    public string Secret { get; }

    /// <summary>The key as JSON: kind, station, created, secret (the Python client reads and writes the same).</summary>
    public string ToJson() => new JObject
    {
        ["kind"] = KeyKind,
        ["station"] = Station,
        ["created"] = Created,
        ["secret"] = Secret,
    }.ToString(Formatting.None);

    /// <summary>A key from <see cref="ToJson"/>.</summary>
    /// <exception cref="FormatException">Not a recovery drive key.</exception>
    public static RecoveryDriveKey FromJson(string json)
    {
        JObject o;
        try
        {
            o = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })
                ?? throw new FormatException("Not a recovery drive key");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Not a recovery drive key: " + ex.Message, ex);
        }
        if ((string?)o["kind"] != KeyKind || o["station"]?.Type != JTokenType.String || o["created"]?.Type != JTokenType.String
            || o["secret"]?.Type != JTokenType.String)
            throw new FormatException("Not a recovery drive key");
        var secret = (string)o["secret"]!;
        if (secret.Length != 2 * RecoveryDrive.SecretBytes || !secret.All(Uri.IsHexDigit))
            throw new FormatException("The recovery drive key's secret isn't 64 hex digits");
        return new RecoveryDriveKey((string)o["station"]!, (string)o["created"]!, secret.ToLowerInvariant());
    }

    /// <summary>
    /// Writes the key to <paramref name="path"/> (the caller chooses where; it holds the secret). On Linux and macOS the
    /// file is made readable by its owner only (0600) before the secret goes in, also when it was there already; on
    /// Windows it gets the folder's permissions, so choose a folder only you can read (your profile, not a shared one).
    /// </summary>
    /// <exception cref="IOException">The file couldn't be made owner-only: the secret wasn't written.</exception>
    public void Save(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // An empty file first, owner-only, then the secret: File.WriteAllText truncates it and keeps its mode
            using (new FileStream(path, FileMode.Create, FileAccess.Write)) { }
            if (OwnerOnly.Chmod(path, OwnerOnly.UserReadWrite) != 0)
                throw new IOException($"Couldn't make {path} readable by its owner only (error {Marshal.GetLastWin32Error()}): the recovery drive key wasn't written");
        }
        File.WriteAllText(path, ToJson() + "\n", new UTF8Encoding(false));
    }

    /// <summary>Reads a key <see cref="Save"/> wrote.</summary>
    public static RecoveryDriveKey Load(string path) => FromJson(File.ReadAllText(path));
}

/// <summary>chmod(2) on Linux and macOS (netstandard2.0 has no File.SetUnixFileMode).</summary>
internal static class OwnerOnly
{
    /// <summary>S_IRUSR | S_IWUSR: 0600.</summary>
    public const uint UserReadWrite = 0x180;

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int ChmodLibc(string path, uint mode);

    /// <summary>0 when done, else -1 (the error in Marshal.GetLastWin32Error).</summary>
    public static int Chmod(string path, uint mode) => ChmodLibc(path, mode);
}
