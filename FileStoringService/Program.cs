using FileStoringService.Data;
using FileStoringService.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "File Storing Service", Version = "v1" });
});
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection")));

builder.Services.Configure<FormOptions>(options => {
    options.MultipartBodyLengthLimit = 
        builder.Configuration.GetValue<long>("FileStorage:MaxFileSizeMB") * 1024 * 1024;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); 
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthorization();
app.MapControllers();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        
        var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();
        var ex = exceptionHandler?.Error;
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            StatusCode = context.Response.StatusCode,
            Message = "Internal Server Error",
            Detail = ex?.Message
        }));
    });
});

app.Run();

public record UploadResponse(int Id, string Hash);