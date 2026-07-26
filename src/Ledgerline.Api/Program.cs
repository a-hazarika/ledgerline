using Ledgerline.Api.Data;
using Ledgerline.Api.Email;
using Ledgerline.Api.Features.Admin;
using Ledgerline.Api.Features.Customers;
using Ledgerline.Api.Features.Emails;
using Ledgerline.Api.Features.Invoices;
using Ledgerline.Api.Features.Settings;
using Ledgerline.Api.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<LedgerlineDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Ledgerline"));
    options.ConfigureWarnings(w =>
        w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});

// Tenancy. TenantContext is filled in by TenantResolutionMiddleware for HTTP requests;
// scopes created outside a request start out unbound.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddSingleton<TenantDirectory>();

builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PlatformReportService>();

// Outbound email. Parsing templates is not free and the set of templates is fixed,
// so the renderer and its cache live for the lifetime of the process.
builder.Services.AddSingleton<TemplateStore>();
builder.Services.AddSingleton<IInvoiceRenderer, InvoiceRenderer>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IEmailQueue, ChannelEmailQueue>();
builder.Services.AddHostedService<EmailSendingWorker>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseStaticFiles();
app.UseMiddleware<TenantResolutionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/health/ready", async (LedgerlineDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapInvoiceEndpoints();
app.MapCustomerEndpoints();
app.MapSettingsEndpoints();
app.MapEmailActivityEndpoints();
app.MapAdminEndpoints();

app.Run();
