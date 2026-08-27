using Enigma.Server.Data.Entities.Administracion;
using Enigma.Server.Data.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Enigma.Server.Data;

public class EnigmaDbContext : IdentityDbContext<Usuario, Rol, int>
{
    public EnigmaDbContext(DbContextOptions<EnigmaDbContext> options)
        : base(options)
    {
    }
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Institucion> Instituciones => Set<Institucion>();
    public DbSet<Membresia> Membresias => Set<Membresia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Membresía explícita Usuario↔Institución (reemplaza la N:M automática):
        // índice único del vínculo + FKs. Cascade en ambos lados: borrar usuario o
        // institución elimina sus membresías (y sus MembresiaRol por cascade).
        modelBuilder.Entity<Membresia>(m =>
        {
            m.HasIndex(x => new { x.UsuarioId, x.InstitucionId }).IsUnique();
            m.HasOne(x => x.Usuario)
                .WithMany(u => u.Membresias)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
            m.HasOne(x => x.Institucion)
                .WithMany(i => i.Membresias)
                .HasForeignKey(x => x.InstitucionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Join Membresia↔Rol con PK compuesta; cascade desde ambos extremos.
        modelBuilder.Entity<MembresiaRol>(mr =>
        {
            mr.HasKey(x => new { x.MembresiaId, x.RolId });
            mr.HasOne(x => x.Membresia)
                .WithMany(m => m.Roles)
                .HasForeignKey(x => x.MembresiaId)
                .OnDelete(DeleteBehavior.Cascade);
            mr.HasOne(x => x.Rol)
                .WithMany()
                .HasForeignKey(x => x.RolId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Auditoría de GenericEntity (Institucion y Membresia): las navegaciones
        // unidireccionales hacia Usuario deben declararse explícitas con Restrict
        // para desambiguarlas de las FKs de membresía y evitar ciclos de cascade.
        foreach (string? navegacion in new[] { "CreadoPor", "ModificadoPor", "BorradoPor" })
        {
            modelBuilder.Entity<Institucion>()
                .HasOne(navegacion)
                .WithMany()
                .HasForeignKey($"{navegacion}Id")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Membresia>()
                .HasOne(navegacion)
                .WithMany()
                .HasForeignKey($"{navegacion}Id")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
