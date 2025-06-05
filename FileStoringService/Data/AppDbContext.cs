using FileStoringService.Models;
using Microsoft.EntityFrameworkCore;

namespace FileStoringService.Data;

public class AppDbContext : DbContext
{
    public DbSet<FileRecord> Files { get; set; }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}