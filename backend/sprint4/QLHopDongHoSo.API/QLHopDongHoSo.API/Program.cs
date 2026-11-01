using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QLHopDongHoSo.API.Data;
using QLHopDongHoSo.API.Services;
using QLHopDongHoSo.API.Configuration;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Không gửi header "Server: Kestrel" (lộ công nghệ máy chủ)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Khóa ký JWT: KHÔNG để trong appsettings.json (file này lên GitHub, ai đọc được là tự tạo token Admin).
// Cấu hình qua biến môi trường Jwt__Key, user-secrets hoặc appsettings.Development.json (bị .gitignore).
// Chưa cấu hình / đang là khóa mẫu / quá ngắn => tự sinh khóa ngẫu nhiên cho lần chạy này
// (khởi động lại backend thì mọi người đăng nhập lại; chạy nhiều máy chủ cùng lúc thì phải cấu hình chung một khóa).
var jwtKey = JwtKeyGuard.Resolve(builder.Configuration["Jwt:Key"], out var jwtKeyGenerated);
builder.Configuration["Jwt:Key"] = jwtKey; // AuthService đọc lại từ cấu hình khi phát hành token

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey))
    };

    // Token còn hạn nhưng tài khoản đã bị khóa, đã đăng xuất hoặc đã đổi vai trò/chi nhánh => từ chối ngay.
    // So "tv" (TokenVersion) trong token với giá trị trong DB (cache 15 giây, xóa ngay khi giá trị đổi).
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
        {
            var id = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(id, out var userId))
            {
                ctx.Fail("Token không hợp lệ.");
                return;
            }

            var cache = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            // null = tài khoản không còn hoạt động
            var currentVersion = await Microsoft.Extensions.Caching.Memory.CacheExtensions.GetOrCreateAsync(cache, UserStatusCache.Key(userId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);
                entry.Size = 1;
                var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                return await db.Users
                    .Where(u => u.UserId == userId && u.Status == "ACTIVE")
                    .Select(u => (int?)u.TokenVersion)
                    .FirstOrDefaultAsync();
            });

            if (currentVersion == null)
            {
                ctx.Fail("Tài khoản đã bị khóa.");
            }
            else if (ctx.Principal?.FindFirst(UserStatusCache.TokenVersionClaim)?.Value != currentVersion.Value.ToString())
            {
                ctx.Fail("Phiên đăng nhập đã hết hiệu lực (đã đăng xuất hoặc quyền vừa thay đổi).");
            }
        }
    };
});

builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<LoginAttemptTracker>();
builder.Services.AddScoped<IDocumentComplianceService, DocumentComplianceService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache(options => options.SizeLimit = 5000);
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// Cho phép FE (Vite dev server / bản build) gọi API
var corsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition"));
});
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Tệp vượt giới hạn Kestrel (30 MB): model binding báo "Request body too large" dạng ProblemDetails tiếng Anh
        // => trả { message } tiếng Việt, 413 như các lỗi khác (giới hạn nghiệp vụ 10 MB kiểm tra ở FileValidationService)
        var defaultFactory = options.InvalidModelStateResponseFactory;
        options.InvalidModelStateResponseFactory = context =>
        {
            var tooLarge = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Any(e => (e.Exception?.Message ?? e.ErrorMessage).Contains("too large", StringComparison.OrdinalIgnoreCase));
            return tooLarge
                ? new Microsoft.AspNetCore.Mvc.ObjectResult(new { message = "Tệp quá lớn. Mỗi tệp tối đa 10 MB." })
                {
                    StatusCode = StatusCodes.Status413PayloadTooLarge
                }
                : defaultFactory(context);
        };
    });

// Lỗi chưa bắt (đồng thời, trùng khóa, dữ liệu quá dài...) => 4xx có message thay vì 500
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// Chống dò mật khẩu: giới hạn số lần gọi /api/Auth/login theo IP (kèm khóa tạm tài khoản trong AuthService)
var loginPerMinute = builder.Configuration.GetValue("Security:LoginRateLimitPerMinute", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Login, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (ctx, token) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Bạn đăng nhập quá nhiều lần. Vui lòng thử lại sau 1 phút." }, token);
    };
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
    ));

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer",
        new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description =
                "Nhập JWT token theo dạng: Bearer {token}"
        });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                "Bearer",
                document)] = []
        });
});

builder.Services.Configure<FileUploadOptions>(
    builder.Configuration.GetSection("FileUpload"));

PasswordHasher.WorkFactor = builder.Configuration.GetValue("Security:PasswordWorkFactor", 10);

var app = builder.Build();

if (jwtKeyGenerated)
{
    app.Logger.LogWarning(
        "Jwt:Key chưa được cấu hình (hoặc là khóa mẫu công khai) => dùng khóa ngẫu nhiên cho lần chạy này. " +
        "Token cũ hết hiệu lực khi khởi động lại. Đặt khóa riêng qua biến môi trường Jwt__Key nếu cần giữ phiên đăng nhập.");
}

app.UseExceptionHandler();

// Header bảo mật cho mọi phản hồi; API không cho trình duyệt/proxy lưu cache (dữ liệu khách hàng)
const string ContentSecurityPolicy =
    "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
    "font-src 'self' data: https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self'; " +
    "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
app.Use((ctx, next) =>
{
    var headers = ctx.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    // Swagger UI cần script nội tuyến nên không áp CSP cho trang /swagger (chỉ bật ở Development)
    if (!ctx.Request.Path.StartsWithSegments("/swagger"))
    {
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
    }

    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        headers.CacheControl = "no-store";
    }

    return next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Giao diện React đã build (ClientApp/dist) được chép vào wwwroot
// => chạy backend là mở được web luôn, không cần chạy thêm `npm run dev`
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("Frontend");

app.UseRateLimiter();

// Trình duyệt vừa ghi dữ liệu gửi "X-Fresh: 1" => số liệu tổng hợp phải mới nhất (không dùng bản cache cũ ≤ 1 giây)
app.Use((ctx, next) =>
{
    DataVersion.RequireFresh = ctx.Request.Headers.ContainsKey("X-Fresh");
    return next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Đường dẫn /api/... không khớp API nào => 404 JSON (trước đây rơi vào trang web index.html, trả 200)
app.Map("/api/{**rest}", () => Results.NotFound(new { message = "Không tìm thấy API." }));

// React Router: đường dẫn không phải /api và không phải file tĩnh thì trả về index.html
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "", "index.html")))
{
    app.MapFallbackToFile("index.html");
}
else
{
    // Chưa build giao diện vào wwwroot (vd vừa giải nén mã nguồn / clone từ GitHub) => trang hướng dẫn thay vì 404
    app.MapGet("/", () => Results.Content(
        """
        <!doctype html><html lang="vi"><head><meta charset="utf-8"><title>QLHD Nhóm 6 – Backend đang chạy</title>
        <style>body{font-family:Segoe UI,Arial,sans-serif;max-width:720px;margin:48px auto;padding:0 16px;line-height:1.6;color:#0B2545}
        code{background:#eef2f6;padding:2px 6px;border-radius:4px}a{color:#0f7c7a}</style></head><body>
        <h1>Backend API đang chạy ✔</h1>
        <p>Giao diện web (React) <b>chưa được build</b> vào thư mục <code>wwwroot</code> của backend nên địa chỉ này chưa có trang web.</p>
        <p>Giao diện được build tự động khi build backend (F5 / <code>dotnet run</code>) từ thư mục <code>ClientApp</code>.
        Thường là do máy <b>chưa cài Node.js 20+</b> (xem cảnh báo trong cửa sổ Build) hoặc thiếu thư mục <code>ClientApp</code>.
        Cài Node.js từ <a href="https://nodejs.org">nodejs.org</a> rồi build/chạy lại.</p>
        <p>Thử API: <a href="/swagger">/swagger</a> (môi trường Development). Hướng dẫn đầy đủ: <code>docs/DEMO.md</code>.</p>
        </body></html>
        """, "text/html; charset=utf-8"));
}

// Khi máy vừa bật, SQL Server có thể chưa sẵn sàng (lỗi 40) => thử lại thay vì dừng hẳn
const int maxDbAttempts = 30;
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var context =
            scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

        var storagePath = app.Configuration["FileUpload:StoragePath"] ?? "Storage/Documents";

        await DbInitializer.InitializeAsync(
            context,
            app.Environment.ContentRootPath,
            storagePath);
        break;
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (attempt < maxDbAttempts)
    {
        app.Logger.LogWarning(
            "Chưa kết nối được SQL Server (lần {Attempt}/{Max}): {Message}. Thử lại sau 10 giây...",
            attempt, maxDbAttempts, ex.Message);
        await Task.Delay(TimeSpan.FromSeconds(10));
    }
}

app.Run();