using System.Data.Entity;

namespace ConsoleApp2
{
    public class MyEntities : DbContext
    {
        private static MyEntities context;
        public MyEntities()
            : base("name=MyEntities")
        {
        }
        public static MyEntities GetContext()
        {
            if (context == null)
            {
                context = new MyEntities();
            }
            return context;
        }
        public DbSet<Point> Points { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Information> Informations { get; set; }
        public DbSet<Photo> Photos { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Добавьте конфигурацию модели, если необходимо
            base.OnModelCreating(modelBuilder);
        }
    }
}
