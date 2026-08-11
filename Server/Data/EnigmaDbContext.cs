using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data;

public class EnigmaDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
{
    public EnigmaDbContext(DbContextOptions<EnigmaDbContext> options)
        : base(options)
    {
    }
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Institucion> Instituciones => Set<Institucion>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // N:M Usuario ↔ Institucion: tabla join automática (sin entidad propia).
        modelBuilder.Entity<Usuario>()
            .HasMany(u => u.Instituciones)
            .WithMany(i => i.Usuarios)
            .UsingEntity(j => j.ToTable("UsuarioInstitucion"));

        // Auditoría de GenericEntity: las navegaciones unidireccionales hacia
        // Usuario (CreadoPor/ModificadoPor/BorradoPor) deben configurarse
        // explícitamente para desambiguarlas de la N:M anterior. Restrict evita
        // ciclos de cascade con Identity.
        foreach (var navegacion in new[] { "CreadoPor", "ModificadoPor", "BorradoPor" })
        {
            modelBuilder.Entity<Institucion>()
                .HasOne(navegacion)
                .WithMany()
                .HasForeignKey($"{navegacion}Id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
