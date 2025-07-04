using Kutip.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kutip.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Bin> Bins { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Truck> Trucks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.HasKey(e => e.s_ID);

                entity.Property(e => e.s_Date)
                    .HasColumnType("date")
                    .IsRequired();

                entity.Property(e => e.s_PickupTime)
                    .HasColumnType("time")
                    .IsRequired();

                entity.Property(e => e.s_PickupEnd)
                    .HasColumnType("time")
                    .IsRequired();

                entity.Property(e => e.PickedUpBins)
                    .IsRequired();

                entity.Property(e => e.TotalBins)
                    .IsRequired();
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.c_ID);

                entity.Property(e => e.c_Name)
                    .IsRequired();

                entity.Property(e => e.c_ContactNo)
                    .IsRequired();

                entity.Property(e => e.c_Email)
                    .IsRequired();
            });

            modelBuilder.Entity<Location>(entity =>
            {
                entity.HasKey(e => e.l_ID);

                entity.Property(e => e.l_Address1)
                    .IsRequired();

                entity.Property(e => e.l_Address2)
                    .IsRequired();

                entity.Property(e => e.l_Postcode)
                    .IsRequired();

                entity.Property(e => e.l_District)
                    .IsRequired();

                entity.Property(e => e.l_State)
                    .IsRequired();

                entity.Property(e => e.l_ColArea)
                    .IsRequired();
            });

            modelBuilder.Entity<Bin>(entity =>
            {
                entity.HasKey(e => e.b_ID);

                entity.Property(e => e.b_PlateNo)
                    .IsRequired()
                    .HasMaxLength(20);
            });

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Bin)
                .WithMany()
                .HasForeignKey(s => s.b_ID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.Location)
                .WithMany()
                .HasForeignKey(s => s.l_ID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bin>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.Bins)
                .HasForeignKey(b => b.c_ID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bin>()
                .HasOne(b => b.Location)
                .WithMany(l => l.Bins)
                .HasForeignKey(b => b.l_ID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
