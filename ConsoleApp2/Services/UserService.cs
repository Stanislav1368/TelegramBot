using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleApp2.Services
{
    public static class UserService
    {
        private static readonly MyEntities context = MyEntities.GetContext();

        public static User GetUser(string chatId)
        {
            return context.Users.FirstOrDefault(u => u.ChatTelegramId == chatId);
        }

        public static async Task AddUser(User user)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
    }
}
