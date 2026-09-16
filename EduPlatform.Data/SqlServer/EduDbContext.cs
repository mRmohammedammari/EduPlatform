using EduPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace EduPlatform.Data.SqlServer
{
    public class EduDbContext : DbContext
    {
        public EduDbContext(DbContextOptions<EduDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Email).IsRequired().HasMaxLength(200);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).HasConversion<string>();
            });

            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Title).IsRequired().HasMaxLength(300);
                entity.Property(c => c.Price).HasPrecision(10, 2);
                entity.HasMany(c => c.Modules)
                      .WithOne(m => m.Course)
                      .HasForeignKey(m => m.CourseId);
            });

            modelBuilder.Entity<Module>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Title).IsRequired().HasMaxLength(300);
            });

            modelBuilder.Entity<Enrollment>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.CourseId });
                entity.Property(e => e.AmountPaid).HasPrecision(10, 2);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.Enrollments)
                      .HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Course)
                      .WithMany(c => c.Enrollments)
                      .HasForeignKey(e => e.CourseId);
            });

            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(q => q.Id);
                entity.Property(q => q.Text).IsRequired();
                entity.Property(q => q.Options)
                      .HasConversion(
                          v => string.Join(',', v),
                          v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList())
                      .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                          (left, right) => left != null && right != null && left.SequenceEqual(right),
                          value => value == null
                              ? 0
                              : value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                          value => value == null ? new List<string>() : value.ToList()));
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(notification => notification.Id);
                entity.Property(notification => notification.Title).IsRequired().HasMaxLength(200);
                entity.Property(notification => notification.Message).IsRequired().HasMaxLength(2000);
                entity.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
            });
        }
    }
}