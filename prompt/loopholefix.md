# Master Prompt — KrishiLink Loophole Remediation

> **How to use this file.** Paste this whole document into a coding agent working inside the
> KrishiLink repository. Work the phases **in order**. Each finding has a stable ID
> (`SEC-01`, `REL-03`, …) — reference it in commit messages. Do not start a phase until the
> previous phase builds clean and its acceptance checks pass.

---

## 0. Context the agent must load first

**Project:** KrishiLink — a 3-tier ASP.NET Core MVC agriculture marketplace for Bangladesh
(Farmers ↔ Equipment Owners ↔ Godown Owners), with simulated escrow money flow, double-entry
ledger, booking state machine, and bilingual EN/BN UI.

**Stack — this is a hard constraint. Do not introduce anything outside it:**

| Layer | Technology |
|---|---|
| Runtime | .NET 8 LTS (builds on the .NET 9 SDK) |
| Web | ASP.NET Core MVC, Razor views |
| ORM / DB | EF Core 8, Npgsql, Supabase PostgreSQL |
| Auth | Supabase Auth (`SupabaseAuthClient`); ASP.NET Identity tables retained for profiles/roles/cookie only — **no local password authentication** |
| Storage | Supabase Storage (public `listing-images`, private `verification-documents`) |
| Crypto | ASP.NET Data Protection (`KrishiLink.Nid` purpose for NID at rest) |
| PDF / QR | QuestPDF (Community), QRCoder |
| Frontend | **Bootstrap 5.3 + Bootstrap Icons + vanilla JS + jQuery only.** No React/Vue/Angular/Svelte, no npm build step, no Tailwind, no SPA router, no TypeScript toolchain. |
| i18n | `IStringLocalizer<SharedResource>` → `Resources/SharedResource.en.resx`, `SharedResource.bn.resx` |

**Repository facts the agent should verify before editing (they were true at audit time):**

- ~61,000 lines: `Controllers/` 3.7k, `BLL/` 18.2k, `DAL/` 12.3k, `Models/` 4.8k, `Views/` 22.4k.
- **148 controller actions across 22 controllers.**
- **Zero test projects. No `.sln` file.**
- Coupled write workflows are serialized by `DAL/Repositories/WorkflowTransaction.cs`, which takes
  `pg_advisory_xact_lock(1263682376, 1)` — one global lock ID for the whole platform.
- CI: `.github/workflows/ci.yml` (build + format + secret scan + EF migration check),
  `codeql.yml`, `deploy.yml` (publishes an artifact; **does not deploy anywhere**).

**Working rules:**

1. **Minimal diffs.** Fix the defect. Do not reformat, rename, or "improve" untouched code.
   `dotnet format --verify-no-changes` runs in CI — match the existing style exactly.
2. **Reuse what exists.** `AppLinks` for URLs, `BangladeshGeo` for districts, `WorkflowTransaction`
   for coupled writes, `BookingWorkflow.Guard` for state transitions, `IStringLocalizer` for text.
   Adding a parallel helper that duplicates one of these is a defect, not a fix.
3. **Every new user-facing string goes in both `.resx` files.** English and Bangla. No exceptions.
4. `dotnet build` must end **0 warnings, 0 errors** after every phase.
5. Razor runtime compilation is **off** — a `.cshtml` edit needs a rebuild **and an app restart**
   before it is visible. Do not chase "the fix didn't work" without restarting first.
6. When a fix changes behaviour a user can observe, say so explicitly in the phase summary.

---

## 1. Findings — ranked by exploitability × blast radius

### 🔴 CRITICAL

---

#### `SEC-01` — Registration marks e-mail addresses as verified without proving ownership

**Where:** `Controllers/AccountController.cs` (the `Register` POST), `BLL/Services/SupabaseAuthClient.cs`

**What is wrong.** Registration calls `CreateUserAsync(email, password, confirmed: true)` and stores
the local profile with `EmailConfirmed = true`. No code is ever sent, and no code is ever checked.
Anyone can register `victim@bank.com`, and the platform will then assert throughout its UI, its
e-mailed statements, and its QR verification certificates that the address is *verified*.

This was a deliberate change made because the Supabase confirmation e-mail was not being delivered.
It unblocked registration; it did not solve the underlying delivery problem, and it left three holes:

- **Address squatting / impersonation.** An attacker claims an address they do not control, then
  operates as a "verified" owner or farmer under someone else's identity.
- **Notification misdirection.** Booking notifications, monthly QuestPDF statements, payout
  confirmations and receipts are e-mailed to an address nobody proved they own.
- **Recovery ambiguity.** `ForgotPassword` → `SendRecoveryAsync` still e-mails the *real* owner of
  that address, who receives recovery codes for an account they never created.

**Fix — implement all three parts:**

1. **Diagnose the real delivery failure first.** Do not design around it. Check, in order:
   Supabase Dashboard → Authentication → Providers → Email → *Confirm email* toggle; then
   Authentication → Email Templates (a malformed template silently drops sends); then the built-in
   SMTP rate limit (Supabase's shared sender is ~3–4 e-mails/hour on the free tier and fails
   silently past it); then Authentication → Logs for the actual send errors.
   **Fix:** configure a custom SMTP provider in Supabase (Resend / Brevo / SendGrid / Amazon SES —
   all have workable free tiers). This is a dashboard change, not a code change, and it is the
   actual root cause.

2. **Restore verification as a gate, but keep the account usable.** Re-introduce
   `SendConfirmationAsync` after the profile transaction commits. Create the Supabase user with
   `confirmed: false` and the local profile with `EmailConfirmed = false`. Let the user sign in and
   browse immediately, but block the actions where a wrong address causes real harm, via a policy:

   ```
   AddAuthorization(o => o.AddPolicy("VerifiedEmail",
       p => p.RequireAuthenticatedUser().RequireClaim("email_verified", "true")))
   ```

   Apply `[Authorize(Policy = "VerifiedEmail")]` to: creating a listing, submitting a rental or
   storage request, paying, requesting a payout, and submitting NID verification. Leave browsing,
   advisory, and profile editing open. Surface a dismissible banner in `_Layout.cshtml`:
   *"Confirm your e-mail to book and list — resend code"*.
   Note `SupabaseSessionService.SignInAsync` currently throws when `remote.EmailConfirmedAt == null`;
   that guard must be relaxed to allow the unverified-but-signed-in state, and the policy above
   becomes the real gate.

3. **Remediate the accounts already created under the current behaviour.** Add a one-off
   Development-only admin action that lists every `ApplicationUser` whose Supabase record has no
   `email_confirmed_at`, and either re-sends confirmation or flips `EmailConfirmed` back to `false`.
   Do **not** leave a permanent endpoint that can confirm an arbitrary address — that recreates the
   hole with an admin wrapper.

**Acceptance:** a brand-new registration receives a real code; an unverified account can sign in and
browse but is refused at booking/payment/listing with a clear localized message; no code path sets
`EmailConfirmed = true` without a verified Supabase `email_confirmed_at`.

---

#### `SEC-02` — Authorization is opt-in, so a forgotten attribute silently publishes an endpoint

**Where:** all 22 controllers.

**What is wrong.** **No controller carries a class-level `[Authorize]`.** All 148 actions rely on a
per-action attribute. 52 actions currently have no `[Authorize]` anywhere in their inheritance chain.
Most of those are legitimately public (`Login`, `Register`, `Equipment/Index`, `Verify/Receipt`), but
the model is "secure by exception": the day someone adds an action and forgets the attribute, it ships
open, and nothing — not the compiler, not CI, not CodeQL — will say a word.

**Fix — invert the default globally:**

```csharp
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter(
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
});
```

Then add `[AllowAnonymous]` deliberately — and only — to the genuinely public surface:

| Controller | Actions to mark `[AllowAnonymous]` |
|---|---|
| `HomeController` | `Index`, `Privacy`, `SetLanguage`, `Error` |
| `AccountController` (+ `.Auth`, `.EmailLinks`) | `Register` (GET/POST), `Login` (GET/POST), `AccessDenied`, `VerifyEmail` (GET/POST), `ResendConfirmation`, `ForgotPassword` (GET/POST), `ResetPassword` (GET/POST), `AuthCallback`, `EmailLink` (GET/POST) |
| `EquipmentController` | `Index`, `Details`, `FilterData`, `Quote`, `FreeUnits` |
| `GodownController` | `Index`, `Details`, `FilterData` |
| `AdvisoryController` | `Index` (GET/POST), `Calendar`, `Alerts`, `Suggestions`, `CropDetail`, `WeatherSuggestionsJson`, `WeatherAlertsJson` |
| `LeaderboardController` | `Index`, `OwnerBadges` |
| `ReviewsController` | `List` (already marked) |
| `VerifyController` | `Index`, `Receipt` (the QR/token check inside the service is the real gate) |

`Logout` must stay authenticated. `VerifyController.QuickAction` must **not** be anonymous — it
currently returns `Challenge()` by hand; delete that manual check once the filter covers it.

**Guard the invariant so it cannot regress.** Add a test (see `REL-01`) that reflects over every
`Controller` subclass, enumerates its public action methods, and asserts each one either resolves to
an authorization policy or appears in an explicit, reviewed allow-list constant. A new unguarded
action then fails CI instead of shipping.

---

#### `SEC-03` — Host-header injection poisons QR codes, verification links and e-mails

**Where:** `appsettings.json` (`"AllowedHosts": "*"`), `Controllers/BookingsController.cs`,
`Controllers/VerifyController.cs`, and every `$"{Request.Scheme}://{Request.Host}"` call site.

**What is wrong.** Absolute URLs baked into QR codes, gate passes, warehouse receipts and
notification e-mails are built from the **client-supplied `Host` header**, and `AllowedHosts` is
`"*"` so ASP.NET never rejects a forged one. An attacker sends
`Host: attacker.example` to `/Bookings/Confirmation`, and the PDF/QR that a farmer later scans at a
godown gate points at the attacker's site — carrying the booking's verification token in the URL.

**Fix:**

1. Set `AllowedHosts` to the real hostnames (comma-separated), per environment. Never `*` outside
   Development.
2. Stop deriving the public origin from the request. `AppOptions.PublicBaseUrl` already exists —
   make it authoritative. Add a single helper (for example `AppLinks.PublicOrigin(IOptions<AppOptions>)`)
   that returns the configured base URL and only falls back to `Request.Host` when
   `IWebHostEnvironment.IsDevelopment()` (which the LAN-access workflow genuinely needs).
3. Replace **every** `$"{Request.Scheme}://{Request.Host}"` with that helper. Grep to confirm none
   remain.

---

#### `SEC-04` — The rate limiter collapses behind a reverse proxy

**Where:** `BLL/Services/SupabaseAuthRateLimitFilter.cs`, `Program.cs`

**What is wrong.** `SupabaseAuthRateLimiter` partitions on
`context.Connection.RemoteIpAddress`, and `Program.cs` **never calls `UseForwardedHeaders`**. Behind
any load balancer, reverse proxy, Cloudflare or a container ingress — i.e. in every realistic
production topology — `RemoteIpAddress` is the *proxy's* address, identical for every visitor. All
users therefore share one 10-requests-per-minute bucket. Two consequences, both bad:

- Brute-force protection is gone (the attacker is in the same bucket as everyone, and the limit is
  per-action, so a distributed attempt is invisible).
- Any single user can lock every other user out of login by spending the shared budget — a trivial
  denial of service.

**Fix:**

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();          // then add ONLY your real proxy network
    o.KnownProxies.Clear();           // then add ONLY your real proxy address
});
```

and `app.UseForwardedHeaders();` **before** `UseRouting`/`UseAuthentication`.
Leaving `KnownProxies`/`KnownNetworks` empty-and-cleared without re-adding your proxy makes the
middleware a no-op; leaving them at the permissive default lets a client spoof `X-Forwarded-For`
and bypass the limiter entirely. Configure them explicitly for the deployment target and document
the value in the README.

Additionally partition by **user ID when authenticated, IP when not** — an authenticated abuser
behind a shared NAT should not punish their neighbours.

---

### 🟠 HIGH

---

#### `REL-01` — Zero automated tests on a platform that moves money

**Where:** the whole repository.

**What is wrong.** There is no test project, no `.sln`, and no test framework reference. The CI job
is literally named **"Build, Test & Security Audit"** and runs no `dotnet test` — so the green badge
on every push is asserting something that was never checked. Meanwhile the code carries a
double-entry ledger, an escrow conservation invariant, a booking state machine with undo semantics,
capacity-aware auto-rejection, loyalty re-pricing on modification, and payout settlement. All of it
is verified by hand, if at all.

**Fix — add `KrishiLink.Tests` (xUnit) and a `KrishiLink.sln`.** Target the invariants that are
expensive to get wrong, not line coverage:

1. **Ledger conservation.** Property-style test over randomized sequences of
   pay → complete → undo → refund → payout, asserting after every step:
   `Σ PaymentIn − Σ Refund = EscrowBalance + Σ CommissionEarned − Σ CommissionReversed + Σ PayoutOut`
   (the same identity `/Home/LedgerCheck` exposes).
2. **Booking state machine.** Table-driven over `BookingWorkflow.Next` / `.Guard`: every
   (status, action, actor-role, paid?, payout?) combination, asserting the illegal ones are refused.
   Explicitly cover: complete-without-payment, undo-after-payout, modify-while-paid,
   review-then-reopen.
3. **Pricing.** `BookingPricing` with Season-over-Weekend-over-base precedence, `MinRentalDays`,
   `Units × DailyRate × Days`, and the accept-time re-quote snapshot.
4. **Availability & capacity.** Overlapping-range maths, `bookedOnDay = Σ Units` per date,
   blocked-date exclusion, `excludeBookingId` self-exclusion, and the capacity-aware
   auto-rejection ordering.
5. **Authorization invariant.** The reflection test from `SEC-02`.
6. **Geo + normalization.** All 64 districts map to exactly one division; `NormalizePhone` round-trips
   `+8801…`, `8801…`, `01…`, and `1…` to one canonical form.

Use EF Core's **Npgsql test container or a disposable schema**, not the InMemory provider — the
InMemory provider does not enforce the unique filtered indexes, transactions or advisory locks that
several of these invariants depend on, so it would give false confidence.

Then wire it in CI, and make the job name honest:

```yaml
- name: Run Tests
  run: dotnet test KrishiLink.sln --configuration Release --no-build
        --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage"
```

---

#### `REL-02` — One global advisory lock serializes every write on the platform

**Where:** `DAL/Repositories/WorkflowTransaction.cs:35`

**What is wrong.** Every coupled workflow — booking create/accept/modify, payment, review, loyalty
redemption, payout settlement, storage intake, availability blocking — takes the **same**
`pg_advisory_xact_lock(1263682376, 1)`. The source comment is candid about it
(*"favors correctness over write throughput: unrelated owners also wait for each other"*), and as a
correctness decision it was the right call. As a production posture it is a hard ceiling: a farmer in
Rangpur booking a tiller blocks an unrelated godown owner in Khulna from recording an intake. One
slow transaction (a QuestPDF render or an e-mail inside the lock) stalls the entire platform, and
lock waits surface to users as timeouts.

**Fix — shard the lock by the resource actually being contended.** `pg_advisory_xact_lock` takes two
`int4` keys; the second is currently a constant `1`. Use it:

```csharp
// key1 = domain discriminator, key2 = the contended entity id
await db.Database.ExecuteSqlRawAsync(
    "SELECT pg_advisory_xact_lock({0}, {1})", domainKey, entityId, ct);
```

Suggested domains: equipment listing id, godown listing id, payout owner id, ledger account.
Two rules make this safe:

- **Always acquire multiple locks in a globally fixed order** (ascending `(domain, id)`), or you
  trade a throughput problem for a deadlock.
- **Keep PDF rendering, e-mail dispatch and HTTP calls outside the lock.** Audit each
  `BeginWorkflowAsync` scope and move that work after `CommitAsync` (the `IEmailQueue` channel
  already exists for exactly this).

Ship it behind a config flag (`Database:ShardedWorkflowLocks`) so it can be reverted in one setting,
and do not attempt it until the `REL-01` concurrency tests exist to prove it.

---

#### `REL-03` — Sync-over-async on the payout path

**Where:** `BLL/Services/OwnerRevenueService.cs:333-334`

```csharp
public string? RequestPayout(string ownerId, string method, string? account) =>
    RequestPayoutAsync(ownerId, method, account).GetAwaiter().GetResult();
```

**What is wrong.** This blocks a thread-pool thread on a method that opens a database transaction
*and* takes the global advisory lock. Under concurrency this is the classic recipe for thread-pool
starvation and, with the shared lock, genuine deadlock. `OwnerRevenueControllerBase.RequestPayout`
happens to call the async overload, so the sync one is currently dead — which makes it worse, not
better: it is an unexploded charge that the next caller sets off.

**Fix:** delete the sync overload and its interface member. Then sweep the same file's synchronous
EF surface — `GetListings`, `GetBookings`, `GetPayouts`, `GetExpenses`, `GetExpense`, `GetReport`,
`MarkBookingsPaid`, `AddPayout` all execute blocking database I/O from request threads. Convert them
to `Task`-returning members and `await` them through `OwnerRevenueControllerBase`. Grep for
`.Result`, `.Wait()` and `.GetAwaiter().GetResult()` afterwards and confirm zero hits outside
`Program.cs`.

---

#### `SEC-05` — No security-response headers anywhere

**Where:** `Program.cs` (middleware pipeline)

**What is wrong.** The pipeline sets no `Content-Security-Policy`, `X-Frame-Options`,
`X-Content-Type-Options`, `Referrer-Policy` or `Permissions-Policy`. Three endpoints in
`AccountController` set one or two headers by hand; everything else — including every page that
renders user-supplied listing names, review text and farmer names — ships bare. The app is
clickjackable, and any XSS that slips past Razor encoding executes unconstrained.

**Fix:** one small middleware registered right after `UseStaticFiles()`:

```
Content-Security-Policy: default-src 'self';
  script-src 'self' https://cdn.jsdelivr.net;
  style-src  'self' https://cdn.jsdelivr.net https://fonts.googleapis.com;
  font-src   'self' https://cdn.jsdelivr.net https://fonts.gstatic.com;
  img-src    'self' data: https://*.supabase.co;
  connect-src 'self' https://*.supabase.co;
  frame-ancestors 'none'; base-uri 'self'; form-action 'self'
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
Referrer-Policy: strict-origin-when-cross-origin
Permissions-Policy: geolocation=(), camera=(), microphone=(), payment=()
```

**Do this in two steps, and budget for the second.** **24 view files use inline `onclick=` handlers**,
and `_Layout.cshtml` has inline `<script>` blocks, so a strict `script-src 'self'` will break the UI
immediately. Ship the policy in `Content-Security-Policy-Report-Only` first, collect violations,
migrate inline handlers to `addEventListener` in `wwwroot/js/site.js` (or per-page `@section Scripts`
files), then flip to enforcing. Do **not** take the shortcut of adding `'unsafe-inline'` and calling
it done — that concedes the entire benefit.

---

#### `SEC-06` — Data Protection keys are unprotected local files

**Where:** `Program.cs`, `appsettings.json` → `DataProtection:KeyPath = "App_Data/keys"`

**What is wrong.** `PersistKeysToFileSystem` writes unencrypted key XML to the app's own directory.
These keys protect **NID numbers at rest** (`KrishiLink.Nid`) and the auth cookie. Three failures
follow:

- Anyone who reads the container filesystem or a backup decrypts every stored National ID.
- A redeploy onto fresh storage generates new keys, and every previously encrypted NID becomes
  permanently undecryptable — silent data loss on the most sensitive column in the schema.
- Two instances generate two key rings, so cookies and ciphertext do not interoperate.

**Fix:** persist the key ring to durable shared storage and encrypt it at rest — on Linux hosting
that means a mounted persistent volume plus `ProtectKeysWithCertificate(...)`, or a key-vault
provider. At minimum, mount `App_Data/keys` on a persistent volume, restrict it to the app user,
include it in backups, and document in the README that losing it means losing NID data. Add a
startup check that logs loudly if the key directory is empty in a non-Development environment.

---

#### `SEC-07` — Sessions live in process memory, so the app cannot scale or restart cleanly

**Where:** `BLL/Services/SupabaseAuthentication.cs` — `SupabaseSessionStore`

**What is wrong.** Sessions are a `ConcurrentDictionary` in a singleton. The class comment states
the consequence plainly: *"Restarting the process signs everyone out; deploy as one instance."* So
every deploy, crash and autoscale event force-logs-out every user mid-booking, and horizontal
scaling is impossible — which also means the only mitigation for a traffic spike is a bigger box.

**Fix:** move the session store behind an interface with two implementations — the existing in-memory
one for Development, and a distributed one for production. Given the stack, PostgreSQL is the natural
backing store (a `SupabaseSession` table with `Id`, `UserId`, `SecurityStamp`, encrypted tokens,
`TokenExpiresAt`, `ExpiresAt`, plus an expiry sweep in an existing hosted service). Encrypt the stored
Supabase tokens with Data Protection. Keep the `SemaphoreSlim` refresh gate semantics by moving to a
short row-level lock on refresh.

---

### 🟡 MEDIUM

---

#### `SEC-08` — Rate limiting covers only the auth endpoints

12 Supabase auth actions carry `[ServiceFilter(typeof(SupabaseAuthRateLimitFilter))]`. Nothing else
does. `GET /Equipment/Quote`, `GET /Equipment/FreeUnits`, `GET /Equipment/FilterData`,
`GET /Godown/FilterData`, `GET /Advisory/WeatherSuggestionsJson`, `POST /Reviews/*`,
`POST /HarvestPlan/Submit` and the booking endpoints are unthrottled. The quote and filter endpoints
run non-trivial database work per call, and `HarvestPlan/Submit` takes the global lock.

**Fix:** adopt .NET 8 built-in rate limiting (`builder.Services.AddRateLimiter`) with named policies —
`auth` (strict), `write` (moderate, per user), `read-json` (generous, per user/IP) — and apply
`[EnableRateLimiting("…")]` per controller. Migrate the bespoke `SupabaseAuthRateLimiter` onto it so
there is one mechanism, not two.

---

#### `SEC-09` — Anonymous cache invalidation

`POST /Leaderboard/Refresh` is anonymous and calls `_leaderboardService.InvalidateCache()`. An
unauthenticated client can loop it and force permanent leaderboard recomputation — a cheap
cache-stampede lever.

**Fix:** require authentication (it falls out of `SEC-02`), and add a short server-side cooldown so
the cache cannot be invalidated more than once per N seconds regardless of who asks.

---

#### `SEC-10` — Personal data in application logs

`EmailSender.cs:78` and `EmailDispatchService.cs:53` log recipient e-mail addresses;
`Admin/VerificationsController.cs:70` logs free-text rejection reasons. Logs are typically shipped to
a third party and retained far longer than the data-minimization story implies.

**Fix:** log a stable hash or the user ID instead of the address; log a rejection *reason code*
rather than free text. Add a short "what must never be logged" note to the README (NID, tokens,
passwords, full e-mail, phone).

---

#### `REL-04` — No optimistic concurrency token on any entity

No entity declares `IsConcurrencyToken` / `RowVersion` / `xmin`. Correctness currently rests
*entirely* on the global advisory lock. The moment `REL-02` shards that lock, any write path that
was not re-analysed becomes a lost-update.

**Fix:** map PostgreSQL's system column as a concurrency token on the entities that are mutated
concurrently — `EquipmentBooking`, `GodownBooking`, `Equipment`, `Godown`, `Payment`, `Transaction`,
`HarvestPlan`:

```csharp
builder.Entity<EquipmentBooking>().UseXminAsConcurrencyToken();
```

Handle `DbUpdateConcurrencyException` at the service boundary with a localized *"this booking was
just updated — please review and retry"* rather than a 500. Do this **before** `REL-02`.

---

#### `REL-05` — Startup migrations and seeding race across instances

`Program.cs` runs `MigrateAsync()` when `Database:ApplyMigrationsOnStartup` is true, then
`InitializeBucketsAsync()` and `DbInitializer.InitializeAsync()` on **every** boot. With more than
one instance these race; `DbInitializer` guards with a transaction, but bucket creation and migration
do not.

**Fix:** keep `ApplyMigrationsOnStartup` **false** in production (CI already generates an idempotent
script — apply it as a reviewed deployment step). Wrap seeding and bucket initialization in a
`pg_advisory_lock` on a dedicated key so only one instance performs them.

---

#### `REL-06` — No health endpoint

Nothing exposes readiness/liveness, so an orchestrator cannot tell a warming instance from a broken
one, and a Supabase outage surfaces as user-facing 500s instead of a failed health probe.

**Fix:** `AddHealthChecks()` with a database check and a Supabase Auth reachability check;
`MapHealthChecks("/healthz")` (liveness, anonymous) and `/readyz` (readiness). Exclude both from the
`SEC-02` global authorize filter explicitly.

---

#### `OPS-01` — The vulnerability audit cannot fail the build

`.github/workflows/ci.yml` runs `dotnet list package --vulnerable --include-transitive`, which
**prints** advisories and exits `0`. A vulnerable transitive package produces a green build.

**Fix:**

```yaml
- name: Audit Vulnerable NuGet Packages
  run: |
    dotnet list package --vulnerable --include-transitive 2>&1 | tee audit.log
    if grep -qiE '(Critical|High|Moderate)' audit.log; then
      echo "::error::Vulnerable NuGet packages detected."; exit 1
    fi
```

While there: the CI job name promises "Test" — either add `REL-01`'s tests or rename the job. A
misleading green check is worse than a missing one.

---

#### `OPS-02` — The deploy pipeline does not deploy

`deploy.yml` publishes an artifact and echoes reminders. The badge reads "Deploy to Production
Environment / Successful", which will be believed by someone eventually.

**Fix:** either implement the real target (container build + push + host deploy, with the EF
idempotent script applied over a session/direct connection, *not* the transaction pooler) or rename
the job to "Build Production Artifact" and drop the `environment.url`.

---

### 🟢 LOW / QUALITY

---

#### `QUA-01` — ~950 uses of utility classes Bootstrap does not ship

The views use Tailwind-style half-step spacing (`gap-1.5`, `px-2.5`, `p-3.5`, `py-0.5`), plus
`opacity-10`, `shadow-xs`, `tracking-wider` and `pointer-events-none`. Bootstrap 5.3 defines **none**
of them, so every one was a silent no-op until a compatibility shim was appended to
`wwwroot/css/site.css`. The shim is correct and should stay, but the underlying habit will keep
producing invisible bugs.

**Fix:** keep the shim, document it at the top of that section (done), and add a cheap CI guard —
a script that greps the views for class tokens matching the utility grammar, checks them against the
set Bootstrap + `site.css` actually define, and fails on an unknown one.

---

#### `QUA-02` — Localization is roughly one-third complete in controllers

44 `TempData["SuccessMessage"] = "…"` / `["ErrorMessage"] = "…"` assignments are hardcoded English
against 18 localized ones. A Bangla-language user hits English system messages at exactly the moments
that matter — payment, rejection, verification.

**Fix:** route every one through `IStringLocalizer<SharedResource>` and add both `.resx` entries. Add
a CI grep that fails on a new hardcoded literal in a `TempData` message assignment.

---

#### `QUA-03` — No status-code pages; AJAX errors return HTML

There is no `UseStatusCodePages`, and the JSON endpoints return HTML error pages on failure, which
the `fetch` callers in `site.js` then fail to parse — producing a silent dead UI instead of a message.

**Fix:** `UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}")` for navigations, plus an
exception filter that returns `application/problem+json` when the request accepts JSON or carries
`X-Requested-With: XMLHttpRequest`.

---

#### `QUA-04` — `.cshtml` edits need a full rebuild in Development

Razor runtime compilation is not enabled, so every view tweak costs a rebuild and restart. This
directly caused wasted debugging cycles during recent UI work (edits appeared not to apply because
the old compiled views were still being served).

**Fix:** add `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` and enable it **for Development
only**:

```csharp
var mvc = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment()) mvc.AddRazorRuntimeCompilation();
```

---

## 2. Execution plan

Work the phases in order. Each ends with `dotnet build` at **0 warnings, 0 errors**, a green CI run,
and a short written summary of observable behaviour changes.

| Phase | Findings | Why this order |
|---|---|---|
| **1 — Safety net** | `REL-01`, `OPS-01` | Nothing else can be verified without tests. Build the ledger/state-machine/authorization suites *first* so every later phase has a regression net. |
| **2 — Lock the doors** | `SEC-02`, `SEC-03`, `SEC-04`, `SEC-05`, `SEC-09` | Highest exploitability, lowest coupling. `SEC-02`'s reflection test comes from Phase 1. Ship CSP report-only here; enforce later. |
| **3 — Identity truth** | `SEC-01` | Needs the Supabase SMTP fix first (dashboard, not code), then the policy gate, then the backfill. Touches auth flow, so it wants Phase 1's tests in place. |
| **4 — Correctness under concurrency** | `REL-04`, `REL-03`, `REL-02` | Strict order: concurrency tokens, then remove sync-over-async, then shard the lock. Sharding without tokens and tests is how you turn a slow system into a wrong one. |
| **5 — Production readiness** | `SEC-06`, `SEC-07`, `REL-05`, `REL-06`, `SEC-08`, `SEC-10` | Everything needed to run more than one instance safely. |
| **6 — Quality & DX** | `QUA-01`…`QUA-04`, `OPS-02`, CSP enforce | Cheap, low-risk, do last. |

---

## 3. Definition of done

- [ ] `dotnet build` — 0 warnings, 0 errors.
- [ ] `dotnet test` — all green; ledger, state machine, pricing, availability, authorization and geo
      suites present and meaningful.
- [ ] `dotnet format --verify-no-changes` — clean.
- [ ] Every controller action resolves to an authorization policy or a reviewed `[AllowAnonymous]`,
      enforced by a test.
- [ ] No absolute URL in the codebase is derived from `Request.Host` outside Development.
- [ ] `AllowedHosts` is not `*` outside Development.
- [ ] Security headers present on every response; CSP enforcing, with no `'unsafe-inline'` in
      `script-src`.
- [ ] `EmailConfirmed` is never `true` without a verified Supabase `email_confirmed_at`.
- [ ] No `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` outside `Program.cs`.
- [ ] Data Protection keys persist to durable, access-restricted, encrypted storage; documented.
- [ ] `/healthz` and `/readyz` respond; both anonymous.
- [ ] Every user-facing string exists in **both** `SharedResource.en.resx` and `SharedResource.bn.resx`.
- [ ] CI fails on vulnerable packages, and the job name matches what it actually runs.
- [ ] README updated: `AllowedHosts`, forwarded headers, key persistence, migration procedure.

---

## 4. Out of scope — do not do these

- Do **not** replace Supabase Auth with local ASP.NET Identity passwords.
- Do **not** introduce a frontend framework, npm build step, or CSS framework other than Bootstrap 5.3.
- Do **not** rewrite the 3-tier structure or the repository pattern.
- Do **not** change the ledger's account model or the booking state machine's semantics — only its
  enforcement and test coverage.
- Do **not** bulk-reformat files. CI runs `dotnet format --verify-no-changes`; a formatting-only diff
  buries the real change.
