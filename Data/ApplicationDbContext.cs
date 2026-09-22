using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuseumAdmin.Models;



namespace MuseumAdmin.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ClinicalNote> ClinicalNotes { get; set; }
    public DbSet<ClinicalNoteVersion> ClinicalNoteVersions { get; set; }
    public DbSet<TherapistTimeLog> TherapistTimeLogs { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}

