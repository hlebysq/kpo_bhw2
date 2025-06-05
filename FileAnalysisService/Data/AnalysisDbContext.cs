using FileAnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace FileAnalysisService.Data;

public class AnalysisDbContext : DbContext
{
    public DbSet<AnalysisRecord> AnalysisResults { get; set; }
    public DbSet<FileRecord> FileRecords { get; set; }

    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRecord>()
            .HasIndex(a => a.OriginalFileHash)
            .IsUnique();
    }
}