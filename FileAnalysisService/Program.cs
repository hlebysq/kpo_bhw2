using FileAnalysisService.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "File Analysis Service", Version = "v1" });
});

builder.Services.AddDbContext<AnalysisDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SqliteConnection")));

builder.Services.AddHttpClient("FileStorage", client => {
    client.BaseAddress = new Uri(builder.Configuration["Services:FileStoringService"]!);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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