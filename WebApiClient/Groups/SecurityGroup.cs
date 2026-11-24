using System.Globalization;
using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The station's security endpoints (firmware 7.1.0, <c>api/security</c>; FW7 CLI-04 follow-up): logins and users
/// (SEC-03, SEC-05), the mode, API keys and the TLS certificate (SEC-01, NET-01), the security events (CRA-10), the
/// firewall (HOST-01), extensions (HOST-04, CRA-07) and the factory reset (CRA-02). Every
/// call needs Admin unless it says otherwise (in mode Off that is everyone). None needs the lease. A station before
/// 7.1.0 answers 404.
/// </summary>
/// <remarks>
/// A call that carries a secret (login, claim, own password, a user's password, a new user, installing a certificate)
/// needs HTTPS or the station itself: over plain HTTP from another host the station refuses it in every mode, but only
/// once the secret has crossed the network. So the client doesn't send it: such a call throws
/// <see cref="InvalidOperationException"/> when the base address is <c>http://</c> and not <c>localhost</c> or a
/// loopback address.
/// </remarks>
public sealed class SecurityGroup : ApiGroupBase
{
    private DateTimeOffset? _tokenExpiresAt;

    internal SecurityGroup(ApiTransport transport) : base(transport) { }

    // ---------- the login token ----------

    /// <summary>
    /// The login token this client sends as <c>Authorization: Bearer</c>: set by <see cref="LoginAsync"/>,
    /// <see cref="ClaimAsync"/>, <see cref="RenewAsync"/> and <see cref="ChangeOwnPasswordAsync"/>, cleared by
    /// <see cref="LogoutAsync"/>; set it yourself to use a token from elsewhere (null sends none). With an API key as
    /// well, the client sends both and the station uses the key. The client never puts it in an exception message.
    /// </summary>
    /// <exception cref="ArgumentException">The token is empty or has characters a header can't carry.</exception>
    public string? Token
    {
        get => _transport.Token;
        set
        {
            _transport.Token = value;
            _tokenExpiresAt = null;
        }
    }

    /// <summary>
    /// When <see cref="Token"/> expires, on this computer's clock (from the answer's <c>expiresIn</c>, so a station
    /// whose clock differs doesn't matter); null when unknown. A token lasts 15 minutes; renew it before this with
    /// <see cref="RenewAsync"/>, which works until 12 hours after the login. The client doesn't renew by itself.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt => _tokenExpiresAt;

    private LoginResponseDto Use(LoginResponseDto login)
    {
        _transport.Token = string.IsNullOrEmpty(login.Token) ? null : login.Token;
        _tokenExpiresAt = login.ExpiresIn is { } seconds ? DateTimeOffset.UtcNow.AddSeconds(seconds) : login.ExpiresAt;
        return login;
    }

    // ---------- logins (AccountsController) ----------

    /// <summary>
    /// Logs in with a user name and password (anyone; over HTTPS or on the station) and from then on sends the token
    /// (<see cref="Token"/>). Sent without the client's key or token. A wrong name or password throws
    /// <see cref="AccordionQ2SecurityException"/> with <see cref="AccordionQ2SecurityReason.WrongPassword"/>; too many
    /// throw <see cref="AccordionQ2ThrottledException"/>; 503 when logins are off on the station.
    /// </summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host: the password isn't sent.</exception>
    public async Task<LoginResponseDto> LoginAsync(string name, string password, CancellationToken ct = default)
    {
        NeedSecureChannel("A login");
        return Use(await SendJsonAsync<LoginResponseDto>(HttpMethod.Post, "api/security/login", new { name, password }, Credentials.None,
            passwordCheck: true, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// A new token for the same login (Viewer; until 12 hours after the login), sent with <see cref="Token"/> only. 401
    /// (<see cref="AccordionQ2SecurityReason.InvalidToken"/>) once the login can't be renewed: log in again.
    /// </summary>
    /// <exception cref="InvalidOperationException">This client has no login token.</exception>
    public async Task<LoginResponseDto> RenewAsync(CancellationToken ct = default)
    {
        NeedToken("Renewing");
        return Use(await SendJsonAsync<LoginResponseDto>(HttpMethod.Post, "api/security/token/renew", null, Credentials.TokenOnly,
            passwordCheck: false, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Logs out (Viewer): this token and every token renewed from the same login stop working; the client forgets
    /// <see cref="Token"/>, also when the station can't be reached. Without a token it does nothing.
    /// </summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (_transport.Token is null)
            return;
        try
        {
            await SendJsonAsync<object>(HttpMethod.Post, "api/security/logout", null, Credentials.TokenOnly, passwordCheck: false, ct).ConfigureAwait(false);
        }
        finally
        {
            Token = null;
        }
    }

    /// <summary>Who this client is to the station (a key, a login, or null), the mode, whether the station is claimed, whether logins are possible (anyone).</summary>
    public Task<MeDto> GetMeAsync(CancellationToken ct = default)
        => GetAsync<MeDto>("api/security/me", ct);

    /// <summary>
    /// Claims an unclaimed station in security mode Off or Report (anyone, over HTTPS): makes its first admin user with
    /// <paramref name="name"/> (null makes <c>admin</c>) and <paramref name="password"/>, and logs in as that admin. No
    /// setup code (firmware 7.1.0). 400 for a password the station refuses, 409 when already claimed, and 409 in mode On,
    /// where an unclaimed station is claimed with a recovery drive (<see cref="RecoveryResetAdminAsync"/> while the drive
    /// is in the station).
    /// </summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host: the password isn't sent.</exception>
    public async Task<LoginResponseDto> ClaimAsync(string? name, string password, CancellationToken ct = default)
    {
        NeedSecureChannel("A claim");
        return Use(await SendJsonAsync<LoginResponseDto>(HttpMethod.Post, "api/security/claim", new { name, password }, Credentials.None,
            passwordCheck: false, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Signs everyone out (Admin; firmware 7.1.0): every login token on the station ends at once, also after a restart,
    /// this client's own too (it forgets <see cref="Token"/>). API keys keep working.
    /// </summary>
    public async Task SignOutAllAsync(CancellationToken ct = default)
    {
        await PostAsync("api/security/sessions/sign-out-all", ct: ct).ConfigureAwait(false);
        Token = null;
    }

    /// <summary>
    /// Changes the logged-in user's own password (Viewer; over HTTPS or on the station), sent with <see cref="Token"/>
    /// only. The user's other logins end; the answer is a new token for this one, which the client uses. A wrong
    /// current password throws <see cref="AccordionQ2SecurityException"/> with <see cref="AccordionQ2SecurityReason.WrongPassword"/>
    /// (the token stays valid); 400 for a new password the station refuses.
    /// </summary>
    /// <exception cref="InvalidOperationException">This client has no login token: an API key has no password; or the base
    /// address is plain HTTP to another host: the passwords aren't sent.</exception>
    public async Task<LoginResponseDto> ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        NeedToken("Changing the own password");
        NeedSecureChannel("Changing the own password");
        return Use(await SendJsonAsync<LoginResponseDto>(HttpMethod.Put, "api/security/password", new { currentPassword, newPassword },
            Credentials.TokenOnly, passwordCheck: true, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Stops a call that carries a password or private key before it leaves this client over plain HTTP to
    /// another host (FW7 CLI-04 follow-up): the station would refuse it (SEC-03: 400, or 308 in On), but only after the
    /// secret had crossed the network in clear text. As the Web UI's certificate button is disabled there (UI-01).
    /// </summary>
    private void NeedSecureChannel(string what)
    {
        if (!_transport.IsPlainHttpToAnotherHost)
            return;
        var at = _transport.BaseAddress;
        var https = new UriBuilder(at) { Scheme = Uri.UriSchemeHttps, Port = at.Port == 5000 ? 5001 : -1 }.Uri;
        throw new InvalidOperationException(
            $"{what} carries a secret, and this client's base address ({at.GetLeftPart(UriPartial.Authority)}) is plain HTTP to another host: " +
            "the secret would cross the network in clear text before the station refused it. Use the station's HTTPS address " +
            $"(e.g. {https.GetLeftPart(UriPartial.Authority)}) or run on the station (http://localhost)");
    }

    private void NeedToken(string what)
    {
        if (_transport.Token is null)
            throw new InvalidOperationException($"{what} needs a login: call LoginAsync first (or set Token)");
    }

    // ---------- users (AccountsController) ----------

    /// <summary>The users (never their passwords or hashes).</summary>
    public Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default)
        => GetAsync<List<UserDto>>("api/security/users", ct);

    /// <summary>
    /// Makes a user (over HTTPS or on the station). 400 for a name or password the station refuses, 409 for the first
    /// admin of an unclaimed station unless this client has an admin API key in mode On (claim the station instead).
    /// </summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host: the password isn't sent.</exception>
    public async Task<UserDto> CreateUserAsync(string name, StationRole role, string password, CancellationToken ct = default)
    {
        NeedSecureChannel("Making a user");
        return await PostAsync<UserDto>("api/security/users", new { name, role, password }, ct).ConfigureAwait(false);
    }

    /// <summary>Sets a user's password (over HTTPS or on the station): that user's logins end everywhere. 404 for an unknown id.</summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host: the password isn't sent.</exception>
    public async Task SetUserPasswordAsync(string id, string password, CancellationToken ct = default)
    {
        NeedSecureChannel("Setting a user's password");
        await PutAsync($"api/security/users/{Uri.EscapeDataString(id)}/password", new { password }, ct).ConfigureAwait(false);
    }

    /// <summary>Sets a user's role; 409 for the last admin user while the station has no admin API key, 404 for an unknown id.</summary>
    public Task<UserDto> SetUserRoleAsync(string id, StationRole role, CancellationToken ct = default)
        => PutAsync<UserDto>($"api/security/users/{Uri.EscapeDataString(id)}/role", new { role }, ct);

    /// <summary>Removes a user: their logins end at once. 409 for the last admin user while the station has no admin API key, 404 for an unknown id.</summary>
    public Task RemoveUserAsync(string id, CancellationToken ct = default)
        => DeleteAsync($"api/security/users/{Uri.EscapeDataString(id)}", ct);

    // ---------- mode and API keys (SecurityController) ----------

    /// <summary>The mode, the keys and the users, the clients blocked now, the extension upload window and check.</summary>
    public Task<SecurityStateDto> GetStateAsync(CancellationToken ct = default)
        => GetAsync<SecurityStateDto>("api/security", ct);

    /// <summary>
    /// Sets the mode. <see cref="SecurityMode.On"/> needs an admin key or login on this client, HTTPS available, and
    /// this request over HTTPS or on the station (400 otherwise, saying why). The answer's
    /// <see cref="SecurityStateDto.Warnings"/> says what to look at.
    /// </summary>
    public Task<SecurityStateDto> SetModeAsync(SecurityMode mode, CancellationToken ct = default)
        => PutAsync<SecurityStateDto>("api/security/mode", new { mode }, ct);

    /// <summary>Makes an API key; the answer's <see cref="CreatedApiKeyDto.Key"/> is shown only this once. Make keys over HTTPS.</summary>
    public Task<CreatedApiKeyDto> CreateKeyAsync(string name, StationRole role, CancellationToken ct = default)
        => PostAsync<CreatedApiKeyDto>("api/security/keys", new { name, role }, ct);

    /// <summary>Revokes an API key by its id: it stops working at once. 404 for an unknown id.</summary>
    public Task RevokeKeyAsync(string id, CancellationToken ct = default)
        => DeleteAsync($"api/security/keys/{Uri.EscapeDataString(id)}", ct);

    // ---------- the TLS certificate (SecurityController) ----------

    /// <summary>The certificate HTTPS serves, its fingerprint, and the station's own self-signed one's. 404 without one.</summary>
    public Task<CertificateDto> GetCertificateAsync(CancellationToken ct = default)
        => GetAsync<CertificateDto>("api/security/certificate", ct);

    /// <summary>
    /// Installs the site's own certificate. It serves from the next handshake and open TLS connections are closed:
    /// a client pinned to the old fingerprint must be given the new <see cref="CertificateDto.Sha256"/>. 400 for a
    /// certificate the station refuses.
    /// </summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host: the private key isn't
    /// sent (the station refuses an install there in every mode, SEC-03).</exception>
    public async Task<CertificateDto> InstallCertificateAsync(InstallCertificateDto certificate, CancellationToken ct = default)
    {
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate));
        NeedSecureChannel("Installing a certificate");
        return await PutAsync<CertificateDto>("api/security/certificate", certificate, ct).ConfigureAwait(false);
    }

    /// <summary>Removes the site certificate: the self-signed one serves again. 404 without a site certificate.</summary>
    public Task<CertificateDto> RemoveCertificateAsync(CancellationToken ct = default)
        => SendJsonAsync<CertificateDto>(HttpMethod.Delete, "api/security/certificate", null, Credentials.All, passwordCheck: false, ct);

    /// <summary>
    /// Makes a new self-signed certificate, with extra DNS names or IP addresses (at most 16, no wildcards): a new
    /// fingerprint. A site certificate that is installed keeps serving.
    /// </summary>
    public Task<CertificateDto> RegenerateCertificateAsync(IEnumerable<string>? extraNames = null, CancellationToken ct = default)
        => PostAsync<CertificateDto>("api/security/certificate/regenerate", new { extraNames = extraNames?.ToArray() }, ct);

    // ---------- the certificate request (firmware 7.1.0, FW7 CERT-01) ----------

    /// <summary>
    /// Makes a certificate request on the station: a new key pair whose private key never leaves the station, and the
    /// CSR (<see cref="CertificateRequestDto.CsrPem"/>) for the site's CA to sign. <paramref name="names"/> are DNS names
    /// or IP addresses, the first one the subject's common name (at most 32, no wildcards); none: the station's default
    /// names (<see cref="CertificateDto.DefaultRequestNames"/>). It replaces a pending request, whose key is then deleted.
    /// Install the signed certificate with <see cref="InstallSignedCertificateAsync"/>. Over HTTPS or on the station:
    /// 400 over plain HTTP from another host (no secret is sent, but the station keeps one rule for certificate changes).
    /// </summary>
    public Task<CertificateRequestDto> RequestCertificateAsync(IEnumerable<string>? names = null, CancellationToken ct = default)
        => PostAsync<CertificateRequestDto>("api/security/certificate/request", new { names = names?.ToArray() }, ct);

    /// <summary>The pending certificate request (its names, when, by whom, the CSR; never the key); null when none is pending.</summary>
    public Task<CertificateRequestDto?> GetCertificateRequestAsync(CancellationToken ct = default)
        => GetOrNullAsync<CertificateRequestDto>("api/security/certificate/request", ct);

    /// <summary>Discards the pending certificate request: its key is deleted, so a certificate signed for it no longer installs. 404 when none is pending.</summary>
    public Task DeleteCertificateRequestAsync(CancellationToken ct = default)
        => DeleteAsync("api/security/certificate/request", ct);

    /// <summary>
    /// Installs the certificate a CA signed for the pending request: <paramref name="certificatePem"/> is the leaf and
    /// its intermediates as PEM, no key (the key is the request's, on the station). It then serves as a site certificate
    /// (the same checks and warnings as <see cref="InstallCertificateAsync"/>; open TLS connections are closed) and the
    /// request is used up. 400 when no request is pending or the certificate's public key isn't the request's, and over
    /// plain HTTP from another host.
    /// </summary>
    public Task<CertificateDto> InstallSignedCertificateAsync(string certificatePem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(certificatePem))
            throw new ArgumentException("The signed certificate (PEM) is empty", nameof(certificatePem));
        return PutAsync<CertificateDto>("api/security/certificate", new { certificatePem }, ct);
    }

    // ---------- security events (SecurityEventsController) ----------

    /// <summary>
    /// The security events, newest first: at most <paramref name="tail"/> (1 to 5000), optionally since a time and of
    /// some kinds (e.g. <c>LoginFailed</c>, <c>Throttled</c>; the station's names), with how long the log keeps them.
    /// 400 for a tail out of range or a kind the station doesn't know.
    /// </summary>
    public Task<SecurityEventsDto> GetEventsAsync(int tail = 200, DateTimeOffset? since = null, IEnumerable<string>? kinds = null,
        CancellationToken ct = default)
    {
        var query = new List<string> { "tail=" + tail.ToString(CultureInfo.InvariantCulture) };
        if (since is { } s)
            query.Add("since=" + Uri.EscapeDataString(s.ToString("O", CultureInfo.InvariantCulture)));
        foreach (var kind in kinds ?? [])
            query.Add("kind=" + Uri.EscapeDataString(kind));
        return GetAsync<SecurityEventsDto>("api/security/events?" + string.Join("&", query), ct);
    }

    // ---------- the firewall (SecurityHostController) ----------

    /// <summary>The firewall settings (the XVC ports), whether the firewall is in effect, and the ports it opens in On.</summary>
    public Task<HostSecurityDto> GetHostAsync(CancellationToken ct = default)
        => GetAsync<HostSecurityDto>("api/security/host", ct);

    /// <summary>Sets the firewall settings (the XVC ports): stored in every mode, applied at once in On. 400 for a setting the station refuses.</summary>
    public Task<HostSecurityDto> SetHostAsync(HostSettingsDto settings, CancellationToken ct = default)
        => PutAsync<HostSecurityDto>("api/security/host", settings ?? throw new ArgumentNullException(nameof(settings)), ct);

    // ---------- extensions (ExtensionsController) ----------

    /// <summary>The release the extensions are checked against, every folder of hw/additional with its state and digest, and the modules loading from outside it.</summary>
    public Task<ExtensionsReportDto> GetExtensionsAsync(CancellationToken ct = default)
        => GetAsync<ExtensionsReportDto>("api/security/extensions", ct);

    /// <summary>
    /// Accepts a folder's content as it is now (<c>""</c> for the files at the top of hw/additional) by the digest read
    /// from <see cref="GetExtensionsAsync"/>: it then loads in mode On while its content stays the same. 409 when it
    /// changed since, 400 for the signed release's folder or one with a link or an unreadable file, 404 for no such folder.
    /// </summary>
    public Task<AcceptedExtensionDto> AcceptExtensionAsync(string folder, string digest, CancellationToken ct = default)
        => PostAsync<AcceptedExtensionDto>("api/security/extensions/accept", new { folder = folder ?? "", digest }, ct);

    /// <summary>Withdraws a folder's acceptance: in mode On it no longer loads unless it is the signed release's. 404 when it isn't accepted.</summary>
    public Task WithdrawExtensionAsync(string folder, CancellationToken ct = default)
        => DeleteAsync("api/security/extensions/accept?folder=" + Uri.EscapeDataString(folder ?? ""), ct);

    /// <summary>
    /// Opens the file API's changes to the extensions folder in mode On for 1 to 1440 minutes, or closes them (0).
    /// While open, an admin's upload, new folder, move or delete there is accepted as unsigned code.
    /// </summary>
    public Task<ExtensionUploadsDto> SetExtensionUploadsAsync(int minutes, CancellationToken ct = default)
        => PutAsync<ExtensionUploadsDto>("api/security/extension-uploads", new { minutes }, ct);

    /// <summary>The role module images (FPGA images, Drummer packages) need in mode On: Admin (the default) or Operator.</summary>
    public Task<ModuleImageRoleDto> SetModuleImageRoleAsync(StationRole role, CancellationToken ct = default)
        => PutAsync<ModuleImageRoleDto>("api/security/module-images", new { role }, ct);

    // ---------- factory reset (FactoryResetController) ----------

    /// <summary>What a factory reset would erase and keep on the station now (counts only), with the host name to confirm with.</summary>
    public Task<FactoryResetPreviewDto> GetFactoryResetPreviewAsync(CancellationToken ct = default)
        => GetAsync<FactoryResetPreviewDto>("api/security/factory-reset/preview", ct);

    /// <summary>
    /// Resets the station to its production state: <paramref name="confirmHostName"/> is its host name (its serial),
    /// typed to confirm. Answers once the reset has started: the station erases its data and reboots, and comes back in
    /// mode Off, unclaimed, on DHCP, with a new certificate, so this client loses it. <paramref name="eraseCalibration"/>
    /// also erases hw/calibration. 400 for a wrong name, 409 while an update runs or a client holds the lease, 501 off a station.
    /// </summary>
    public Task<FactoryResetStartedDto> StartFactoryResetAsync(string confirmHostName, bool eraseCalibration = false, CancellationToken ct = default)
        => PostAsync<FactoryResetStartedDto>("api/security/factory-reset", new { confirm = confirmHostName, eraseCalibration }, ct);

    /// <summary>The last factory reset's record; null when the station has none (and from a station before 7.1.0, which has no such endpoint).</summary>
    public Task<FactoryResetRecordDto?> GetLastFactoryResetAsync(CancellationToken ct = default)
        => GetOrNullAsync<FactoryResetRecordDto>("api/security/factory-reset", ct);

    // ---------- the recovery drive (RecoveryController) ----------

    /// <summary>
    /// Whether the recovery window is open (anyone, every mode): a recovery drive for this station is in it. Null from a
    /// station before 7.1.0, which has no such endpoint.
    /// </summary>
    public async Task<RecoveryDto?> GetRecoveryAsync(CancellationToken ct = default)
        => await GetOrNullAsync<RecoveryDto>("api/security/recovery", ct).ConfigureAwait(false);

    /// <summary>
    /// The key of the recovery drive this client made (<see cref="MakeRecoveryDrive"/>) or loaded
    /// (<see cref="LoadRecoveryKey"/>): <see cref="RecoveryResetAdminAsync"/> sends its secret. Null when none.
    /// </summary>
    public RecoveryDriveKey? RecoveryKey { get; set; }

    /// <summary>
    /// Makes a recovery drive for <paramref name="station"/> (its host name, which is its serial number: <see
    /// cref="RecoveryDto.Station"/> of <see cref="GetRecoveryAsync"/>), here, without the station: returns the bytes of
    /// <c>accordion-recovery.json</c> (save them under that name in the top folder of a FAT32 or exFAT USB drive) and
    /// keeps the drive's secret in <see cref="RecoveryKey"/> (save it with <see cref="SaveRecoveryKey"/> to reset from
    /// another process). The file holds a hash, never the secret. A drive opens the station's recovery window until 24
    /// hours after it was made, on the station's clock (this computer's time may be ahead of the station's by any amount).
    /// </summary>
    public byte[] MakeRecoveryDrive(string station, DateTimeOffset? now = null)
    {
        var (file, key) = RecoveryDrive.Make(station, now);
        RecoveryKey = key;
        return file;
    }

    /// <summary>Writes <see cref="RecoveryKey"/> to <paramref name="path"/>, which the caller chooses: it holds the drive's secret.</summary>
    /// <exception cref="InvalidOperationException">This client has no recovery drive key.</exception>
    public void SaveRecoveryKey(string path)
        => (RecoveryKey ?? throw new InvalidOperationException("This client hasn't made a recovery drive (MakeRecoveryDrive)")).Save(path);

    /// <summary>Reads a key <see cref="SaveRecoveryKey"/> wrote into <see cref="RecoveryKey"/>.</summary>
    public RecoveryDriveKey LoadRecoveryKey(string path) => RecoveryKey = RecoveryDriveKey.Load(path);

    /// <summary>
    /// The recovery drive's file for this station (anyone, every mode): reads the station's name
    /// (<see cref="GetRecoveryAsync"/>) and calls <see cref="MakeRecoveryDrive"/>. Firmware 7.1.0's final build makes no
    /// drive file itself.
    /// </summary>
    [Obsolete("The station no longer makes the drive's file: use MakeRecoveryDrive(station), which keeps the drive's secret in RecoveryKey")]
    public async Task<byte[]> DownloadRecoveryDriveFileAsync(CancellationToken ct = default)
    {
        var window = await GetRecoveryAsync(ct).ConfigureAwait(false);
        if (window?.Station is not { Length: > 0 } station)
            throw new InvalidOperationException("The station doesn't name itself for a recovery drive: it needs firmware 7.1.0 or later");
        return MakeRecoveryDrive(station);
    }

    /// <summary>
    /// Sets an admin's password while a recovery drive for this station is in it (anyone; over HTTPS or on the station),
    /// sent without the client's key or token, with the secret of <see cref="RecoveryKey"/>: only the client that made
    /// the drive can use it. <paramref name="user"/> null means <c>admin</c>; a missing user is made as Admin, another
    /// role is made Admin, and that user's logins end; the mode, the keys and the other users stay. One reset per
    /// insertion of the drive. 409 when no recovery drive is in the station, 403 when the drive was made elsewhere (it
    /// counts like a wrong password), 400 for a password the station refuses. The client doesn't log in: call
    /// <see cref="LoginAsync"/> with the new password.
    /// </summary>
    /// <exception cref="InvalidOperationException">The base address is plain HTTP to another host (nothing is sent), or there is no <see cref="RecoveryKey"/>.</exception>
    public async Task<RecoveryResetDto> RecoveryResetAdminAsync(string? user, string password, CancellationToken ct = default)
    {
        NeedSecureChannel("A password reset with the recovery drive");
        var key = RecoveryKey ?? throw new InvalidOperationException(
            "This client has no recovery drive key: make the drive with MakeRecoveryDrive, or load its key with LoadRecoveryKey");
        return await SendJsonAsync<RecoveryResetDto>(HttpMethod.Post, "api/security/recovery/reset-admin", new { user, password, secret = key.Secret },
            Credentials.None, passwordCheck: false, ct).ConfigureAwait(false);
    }
}
