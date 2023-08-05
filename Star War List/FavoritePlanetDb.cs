using Microsoft.EntityFrameworkCore;

namespace Star_War_List
{
    public class FavoritePlanetDb : DbContext
    {
        public FavoritePlanetDb(DbContextOptions<FavoritePlanetDb> options) : base(options) { }

        public DbSet<FavoritePlanet> FavoritePlanets => Set<FavoritePlanet>();
    }
}
