
using Kutip.Models; 
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Schedule>().Ignore(s => s.PickupStatus);

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.Property(e => e.s_Date)
                    .HasColumnType("date");

                //entity.Property(e => e.s_Day)
                //    .HasMaxLength(10);

                //entity.Property(e => e.PickupStatus)
                //    .HasMaxLength(20)
                //    .HasDefaultValue("Pending");
            });

            modelBuilder.Entity<Schedule>()
                .HasOne(s => s.AssignedUser)
                .WithMany() 
                .HasForeignKey(s => s.AssignedUser_ID)
                .OnDelete(DeleteBehavior.Restrict);

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
};