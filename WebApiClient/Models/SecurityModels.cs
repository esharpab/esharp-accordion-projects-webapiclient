namespace AccordionQ2.WebApiClient.Models;

// The station's security endpoints (firmware 7.1.0, api/security; FW7 CLI-04 follow-up). Field names and shapes are the
// WebApi's records (AccountsController, SecurityController, SecurityHostController, SecurityEventsController,
// ExtensionsController, FactoryResetController); WebApi.Tests SecurityContractTests reads the server's answers into these.
// Values the station sends as enum names (roles, modes, states, kinds) are strings here, so a newer station's value
// still reads.

/// <summary>A role a key or a user has, lowest first; each may do what the ones below it may.</summary>
public enum StationRole
{
    /// <summary>Anyone; never a key's or a user's role, only an endpoint's.</summary>
    Anonymous,
    /// <summary>Reads: values, channels, state, the event stream and subscriptions.</summary>
    Viewer,
    /// <summary>Running tests: values, channel configuration, alias files, the lease, media, acquisitions.</summary>
    Operator,
    /// <summary>The station itself: firmware, system, files, extensions, keys, users.</summary>
    Admin,
}

/// <summary>What the station does about authentication.</summary>
public enum SecurityMode
{
    /// <summary>Nobody is refused (as before 7.0).</summary>
    Off,
    /// <summary>Nobody is refused, but what <see cref="On"/> would refuse is logged.</summary>
    Report,
    /// <summary>Requests without a valid key or login are refused (401), and those with too low a role (403).</summary>
    On,
}

// ---------- logins and users (AccountsController) ----------

/// <summary>
/// A login (<c>POST api/security/login</c>, the claim, renew, an own password change): the token for
/// <c>Authorization: Bearer</c>, when it expires on the station's clock, whose it is, and how many seconds it lasts from
/// the answer (null from a station before <c>expiresIn</c>): time renewals by that, as clocks differ.
/// </summary>
public sealed class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
    public long? ExpiresIn { get; set; }
}

/// <summary>A user as listed: never the password or its hash.</summary>
public sealed class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Viewer, Operator or Admin.</summary>
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>Who a request was identified as: <c>Key</c> or <c>User</c>; <see cref="ExpiresAt"/> for a login.</summary>
public sealed class CallerDto
{
    public string Kind { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary><c>GET api/security/me</c>: the mode, whether an admin user exists, who this request is (null: no or no valid credential), whether logins are possible.</summary>
public sealed class MeDto
{
    public string Mode { get; set; } = "Off";
    public bool Claimed { get; set; }
    public CallerDto? Caller { get; set; }
    public bool LoginAvailable { get; set; }
}

// ---------- mode, keys and certificate (SecurityController) ----------

/// <summary>
/// <c>GET api/security</c>: the mode, the keys and the users (never a key's secret or a password hash); after a mode
/// change, <see cref="Warnings"/>. <see cref="Throttled"/>: the clients blocked now for failed credentials (null for none).
/// </summary>
public sealed class SecurityStateDto
{
    public string Mode { get; set; } = "Off";
    public List<ApiKeyDto> Keys { get; set; } = [];
    public List<string>? Warnings { get; set; }
    public List<UserDto>? Users { get; set; }
    public bool Claimed { get; set; }
    public List<ThrottledClientDto>? Throttled { get; set; }
    /// <summary>Until when the extension upload window is open; null: closed.</summary>
    public DateTimeOffset? ExtensionUploadsUntil { get; set; }
    /// <summary>The role module images need in On: Admin or Operator.</summary>
    public string ModuleImageRole { get; set; } = "Admin";
    /// <summary>The last extension check; null before the first.</summary>
    public ExtensionCheckSummaryDto? ExtensionCheck { get; set; }
}

/// <summary>An API key as listed; <see cref="IssuedOver"/> is Https, Loopback or Http (null for keys made before 7.0.5).</summary>
public sealed class ApiKeyDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? IssuedOver { get; set; }
}

/// <summary>A new API key: <see cref="Key"/> is shown this once only; store it.</summary>
public sealed class CreatedApiKeyDto
{
    public ApiKeyDto Info { get; set; } = new();
    public string Key { get; set; } = string.Empty;
    public List<string>? Warnings { get; set; }
}

/// <summary>A client the failed-credential throttle blocks now: its address key, the purpose (Key or Password), the failures, until when.</summary>
public sealed class ThrottledClientDto
{
    public string Client { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int Failures { get; set; }
    public DateTimeOffset Until { get; set; }
}

/// <summary>The last extension check: the release checked against (null when none applies) and why not, the folders that failed, the Unsigned folders a module loads from.</summary>
public sealed class ExtensionCheckSummaryDto
{
    public string? Release { get; set; }
    public string? Problem { get; set; }
    public List<string> Failed { get; set; } = [];
    public List<string> Unsigned { get; set; } = [];
}

/// <summary>
/// The serving TLS certificate, public parts only. <see cref="Sha256"/> is the fingerprint to pin
/// (<see cref="AccordionQ2ClientOptions.CertificateSha256"/>); <see cref="Chain"/> the subjects, leaf first;
/// <see cref="OpenConnectionsClosed"/> the TLS connections a change closed.
/// </summary>
public sealed class CertificateDto
{
    /// <summary>SelfSigned or Site.</summary>
    public string Source { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public bool Expired { get; set; }
    public List<string> DnsNames { get; set; } = [];
    public List<string> IpAddresses { get; set; } = [];
    public List<string> Chain { get; set; } = [];
    public string? SelfSignedSha256 { get; set; }
    public bool NameMatchesHost { get; set; }
    /// <summary>Set when this start found the certificate made on another board (a cloned SD card) and made a new one.</summary>
    public ClonedFromDto? Cloned { get; set; }
    public string CertificatePem { get; set; } = string.Empty;
    public int? OpenConnectionsClosed { get; set; }
    /// <summary>A change's warnings; on a GET (firmware 7.1.0): an expired certificate, or one that expires within 30 days, with the date.</summary>
    public List<string>? Warnings { get; set; }
    /// <summary>The names a certificate request without names gets (firmware 7.1.0); null from older firmware.</summary>
    public List<string>? DefaultRequestNames { get; set; }
}

/// <summary>
/// A certificate request made on the station (firmware 7.1.0): <see cref="Names"/> (the first the subject's common
/// name), when and by whom, the CSR to give the CA (<see cref="CsrPem"/>), and the SHA-256 over its public key; never
/// the key. <see cref="Replaced"/> (when made): it replaced a pending request, whose key is gone.
/// </summary>
public sealed class CertificateRequestDto
{
    public List<string> Names { get; set; } = [];
    public DateTimeOffset Created { get; set; }
    public string? CreatedBy { get; set; }
    public string CsrPem { get; set; } = string.Empty;
    public string PublicKeySha256 { get; set; } = string.Empty;
    public bool? Replaced { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class ClonedFromDto
{
    public string MadeOnBoard { get; set; } = string.Empty;
    public string ThisBoard { get; set; } = string.Empty;
}

/// <summary>
/// The site's certificate to install: PEM (<see cref="CertificatePem"/> with the leaf and its intermediates,
/// <see cref="PrivateKeyPem"/>, <see cref="Password"/> for an encrypted key) or PKCS#12 (<see cref="PfxBase64"/>, <see cref="Password"/>).
/// </summary>
public sealed class InstallCertificateDto
{
    public string? CertificatePem { get; set; }
    public string? PrivateKeyPem { get; set; }
    public string? PfxBase64 { get; set; }
    public string? Password { get; set; }
}

// ---------- security events (SecurityEventsController) ----------

/// <summary>Security events, newest first, and what the log keeps: <see cref="MaxAgeDays"/> days or <see cref="MaxBytes"/> in all; <see cref="Oldest"/> the oldest kept.</summary>
public sealed class SecurityEventsDto
{
    public List<SecurityEventDto> Events { get; set; } = [];
    public int MaxAgeDays { get; set; }
    public long MaxBytes { get; set; }
    public DateTimeOffset? Oldest { get; set; }
}

/// <summary>
/// One security event: when, what (<see cref="Kind"/>, e.g. <c>LoginFailed</c>, <c>ModeChanged</c>), who (<c>user:id:name</c>,
/// <c>key:id:name</c>, <c>-</c>, <c>station</c>, ...), from which client, the X-Forwarded-For a client claimed (never trusted),
/// the mode, the endpoint, and details.
/// </summary>
public sealed class SecurityEventDto
{
    public DateTimeOffset Time { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Who { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string? ForwardedFor { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public Dictionary<string, string>? Data { get; set; }
}

// ---------- the firewall (SecurityHostController) ----------

/// <summary>
/// The firewall settings, applied in mode On: the XVC ports an admin opens (closed when empty) and from where (CIDRs or
/// addresses, empty or null for any). In On the firewall drops connections forwarded to containers from outside the station.
/// </summary>
public sealed class HostSettingsDto
{
    public List<int>? XvcPorts { get; set; }
    public List<string>? XvcFrom { get; set; }
}

/// <summary>The firewall's or the service access's state (NotApplied, Applied, Failed, Unsupported), the helper's problem, when it was last reconciled.</summary>
public sealed class HostPartDto
{
    public string State { get; set; } = string.Empty;
    public string? Problem { get; set; }
    public DateTimeOffset? At { get; set; }
}

/// <summary>
/// <c>GET api/security/host</c>: the settings, what is in effect (the firewall, and <see cref="Ssh"/>: E-Sharp's service
/// access taking keys only), the TCP ports the firewall opens in On, the service access's port, warnings for On.
/// </summary>
public sealed class HostSecurityDto
{
    public HostSettingsDto Settings { get; set; } = new();
    public HostPartDto Firewall { get; set; } = new();
    public HostPartDto Ssh { get; set; } = new();
    public List<int> TcpPorts { get; set; } = [];
    public int SshPort { get; set; }
    public List<string>? Warnings { get; set; }
}

// ---------- extensions (ExtensionsController) ----------

/// <summary><c>GET api/security/extensions</c>: the release checked against, the folders of hw/additional, the modules loading from outside it.</summary>
public sealed class ExtensionsReportDto
{
    public ExtensionReleaseDto Release { get; set; } = new();
    public List<ExtensionFolderDto> Folders { get; set; } = [];
    public List<ExtensionReferenceDto> Outside { get; set; } = [];
}

/// <summary>The release: its version, where its manifest was read, signer and time, or why it can't be used.</summary>
public sealed class ExtensionReleaseDto
{
    public string? Version { get; set; }
    public string? Source { get; set; }
    public string? Signer { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
    public string? Problem { get; set; }
}

/// <summary>
/// A folder of hw/additional (<c>""</c> for the files at its top): its state (Signed, Accepted, Unsigned, Changed,
/// Unverified), the <see cref="Digest"/> an admin accepts, and what differs from the signed release.
/// </summary>
public sealed class ExtensionFolderDto
{
    public string Folder { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Digest { get; set; } = string.Empty;
    public int Files { get; set; }
    public List<string> ChangedFiles { get; set; } = [];
    public List<string> MissingFiles { get; set; } = [];
    public List<string> ExtraFiles { get; set; } = [];
    public List<string> Links { get; set; } = [];
    public string? AcceptedBy { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public List<string> ReferencedBy { get; set; } = [];
    public List<string> Unreadable { get; set; } = [];
}

/// <summary>A module entry that loads an assembly; <see cref="Folder"/> null when outside hw/additional.</summary>
public sealed class ExtensionReferenceDto
{
    public string Module { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? Folder { get; set; }
}

public sealed class AcceptedExtensionDto
{
    public ExtensionFolderDto Folder { get; set; } = new();
    public List<string> Warnings { get; set; } = [];
}

/// <summary>Until when the extension upload window is open (null: closed).</summary>
public sealed class ExtensionUploadsDto
{
    public DateTimeOffset? ExtensionUploadsUntil { get; set; }
    public List<string>? Warnings { get; set; }
}

public sealed class ModuleImageRoleDto
{
    public string ModuleImageRole { get; set; } = string.Empty;
    public List<string>? Warnings { get; set; }
}

// ---------- factory reset (FactoryResetController) ----------

/// <summary>What a factory reset would erase and keep on the station now: counts only, no secrets.</summary>
public sealed class FactoryResetPreviewDto
{
    /// <summary>The host name to send as the confirmation.</summary>
    public string HostName { get; set; } = string.Empty;
    public int Keys { get; set; }
    public int Users { get; set; }
    public bool Certificate { get; set; }
    public bool StationSettings { get; set; }
    public int AliasFiles { get; set; }
    public int StateMachines { get; set; }
    public int Captures { get; set; }
    public int CalibrationFiles { get; set; }
    public List<string> UnshippedExtensions { get; set; } = [];
    public bool ExtensionListPresent { get; set; }
    public int DrummerFiles { get; set; }
    public bool AuthorizedKeys { get; set; }
    public List<string> Erased { get; set; } = [];
    public List<string> Kept { get; set; } = [];
}

/// <summary>The reset's systemd unit and what happens now.</summary>
public sealed class FactoryResetStartedDto
{
    public string Unit { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>The last factory reset's record: when, who and from where, the firmware, whether calibration went, what was erased and kept.</summary>
public sealed class FactoryResetRecordDto
{
    public DateTimeOffset Time { get; set; }
    public string By { get; set; } = string.Empty;
    public string? Client { get; set; }
    public string? Version { get; set; }
    public bool CalibrationErased { get; set; }
    public List<string> Erased { get; set; } = [];
    public List<string> Kept { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool Rebooted { get; set; }
}

/// <summary>
/// The recovery window (firmware 7.1.0, <c>GET /api/security/recovery</c>): <c>Window</c> is "open" while a recovery
/// drive for the station is in it (until 24 hours after the drive was made, until the drive is taken out, one reset per
/// insertion), else "closed"; <c>Until</c> null while closed; <c>UsbPortsOff</c> whether the drive needs a restart of
/// the station; <c>Station</c> the station's name, for <see cref="SecurityGroup.MakeRecoveryDrive"/>; <c>Created</c>
/// when the drive in the station was made; <c>Why</c> the window is closed (<c>None</c> while open, <c>NoDrive</c>,
/// <c>Expired</c>, <c>DriveGone</c>, <c>Used</c>, <c>Invalid</c>, <c>Outdated</c>: made by an earlier firmware).
/// </summary>
public sealed class RecoveryDto
{
    public string Window { get; set; } = "closed";
    public DateTimeOffset? Until { get; set; }
    public bool UsbPortsOff { get; set; }
    public string Hint { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = [];
    public string? Station { get; set; }
    public DateTimeOffset? Created { get; set; }
    public string? Why { get; set; }

    /// <summary>
    /// The station's clock (UTC) when it answered (firmware 7.1.0): a drive's time is judged by it (a time ahead of it is
    /// accepted). When it is more than an hour ahead of this computer's clock, pass it to
    /// <see cref="SecurityGroup.MakeRecoveryDrive"/> as <c>now</c>.
    /// </summary>
    public DateTimeOffset? Now { get; set; }

    /// <summary>Whether an admin password can be set now (<see cref="SecurityGroup.RecoveryResetAdminAsync"/>).</summary>
    public bool IsOpen => string.Equals(Window, "open", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What <c>POST /api/security/recovery/reset-admin</c> answers: the user whose password was set, whether it was made, and what to do now.</summary>
public sealed class RecoveryResetDto
{
    public UserDto User { get; set; } = new();
    public bool Created { get; set; }
    public string Message { get; set; } = string.Empty;
}
