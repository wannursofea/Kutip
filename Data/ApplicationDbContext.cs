
﻿using Kutip.Models;  // Assuming your models are in this namespace
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

        public DbSet<Product> Products { get; set; }

        public DbSet<Schedule> Schedules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Schedule>(entity =>
            {
                entity.Property(e => e.Date)
                    .HasColumnType("date");

                entity.Property(e => e.Day)
                    .HasMaxLength(10);

                entity.Property(e => e.PickupStatus)
                    .HasMaxLength(20)
                    .HasDefaultValue("Pending");
            });
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Bin> Bins { get; set; }
        public DbSet<Location> Locations { get; set; }


    }
};