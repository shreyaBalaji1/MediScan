using Microsoft.EntityFrameworkCore;
using MediScanApi.Models;

namespace MediScanApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<FavoriteDrug> FavoriteDrugs => Set<FavoriteDrug>();
        public DbSet<DrugNote> DrugNotes => Set<DrugNote>();
        public DbSet<User> Users => Set<User>();
    }
}