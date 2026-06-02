# Audit

WebApi request audit log — records the IP address, HTTP method, path, response status, and duration of every request handled by the server.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAuditLogAsync(tail?, ct?)` | `Task<string[]>` | Last `tail` lines of the audit log. Default: 100. Pass `0` for the full log. |

## Examples

### Reading the Audit Log

```csharp
// Get the last 100 entries (default)
string[] lines = await client.Audit.GetAuditLogAsync();
foreach (var line in lines)
    Console.WriteLine(line);

// Get the last 200 entries
string[] recent = await client.Audit.GetAuditLogAsync(tail: 200);

// Get the entire log
string[] full = await client.Audit.GetAuditLogAsync(tail: 0);
```

### Log Entry Format

Each line contains: `timestamp  ip  method  path  status  duration`

```
2026-05-29 14:23:01.442 192.168.1.55 POST /api/channels/configure 200 12ms
2026-05-29 14:23:02.019 10.0.0.3 GET /api/application/status 200 3ms
```

The log rotates automatically — up to 20 files of 1 MB each (20 MB total). This endpoint reads only the current (most recent) file.
