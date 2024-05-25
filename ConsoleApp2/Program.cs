using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConsoleApp2
{
    public class RegistrationState
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Patronymic { get; set; }
        public string PointName { get; set; }
        public bool IsAwaitingPointSelection { get; set; }
    }

    class Program
    {
        private static TelegramBotClient _botClient;
        private static ConcurrentDictionary<long, RegistrationState> _registrationStates = new ConcurrentDictionary<long, RegistrationState>();
        private static ConcurrentDictionary<string, int> _photoCounts = new ConcurrentDictionary<string, int>();
        private static readonly object _fileLock = new object(); // объект для блокировки доступа к файлу

        static async Task Main(string[] args)
        {
            // Инициализация клиента бота
            _botClient = new TelegramBotClient("6936131944:AAENpaoxglJOnSdUxknzPzhpBWtBz6tUmik");

            // Запуск обработчика сообщений
            _botClient.OnMessage += BotOnMessageReceived;
            _botClient.StartReceiving();

            // Планирование периодических запросов
            var timer = new Timer(SendRequests, null, TimeSpan.Zero, TimeSpan.FromHours(2));

            Console.WriteLine("Bot started...");
            Console.ReadLine();

            // Остановка обработки сообщений перед завершением программы
            _botClient.StopReceiving();
        }

        private static async void SendRequests(object state)
        {
            using (var dbContext = MyEntities.GetContext())
            {
                var users = dbContext.Users.ToList();
                foreach (var user in users)
                {
                    await _botClient.SendTextMessageAsync(user.ChatTelegramId, "Пожалуйста, отправьте 5 фотографий и вашу геолокацию.");
                    _photoCounts[user.ChatTelegramId] = 0;
                }
            }
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message.Type == MessageType.Photo)
            {
                if (!_photoCounts.ContainsKey(message.Chat.Id.ToString()))
                {
                    return;
                }

                var photoCount = _photoCounts[message.Chat.Id.ToString()];
                if (photoCount < 5)
                {
                    var file = await _botClient.GetFileAsync(message.Photo.Last().FileId);
                    var filePath = $"./photos/{message.Chat.Id}_{file.FileId}.jpg";

                    lock (_fileLock) // блокировка доступа к файлу
                    {
                        using (var saveImageStream = System.IO.File.Open(filePath, FileMode.Create))
                        {
                            _botClient.DownloadFileAsync(file.FilePath, saveImageStream).Wait();
                        }
                    }

                    await SavePhotoInformationAsync(message.Chat.Id, filePath);

                    _photoCounts[message.Chat.Id.ToString()] = photoCount + 1;
                }
            }
            else if (message.Type == MessageType.Location)
            {
                if (_photoCounts.ContainsKey(message.Chat.Id.ToString()) && _photoCounts[message.Chat.Id.ToString()] >= 5)
                {
                    await SaveLocationInformationAsync(message.Chat.Id, message.Location.Latitude, message.Location.Longitude);

                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Спасибо! Ваши данные успешно сохранены.");
                    _photoCounts.TryRemove(message.Chat.Id.ToString(), out _);
                }
            }
            else if (message.Type == MessageType.Text)
            {
                if (message.Text.Equals("/test", StringComparison.OrdinalIgnoreCase))
                {
                    // Вызов метода SendRequests при получении команды /test
                    SendRequests(null);
                }
                else if (message.Text.StartsWith("/register"))
                {
                    using (var dbContext = MyEntities.GetContext())
                    {
                        var existingUser = dbContext.Users.FirstOrDefault(u => u.ChatTelegramId == message.Chat.Id.ToString());

                        if (existingUser != null)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы уже зарегистрированы.");
                            return;
                        }

                        _registrationStates[message.Chat.Id] = new RegistrationState();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Введите ваше имя:");
                    }
                }
                else if (_registrationStates.ContainsKey(message.Chat.Id))
                {
                    var state = _registrationStates[message.Chat.Id];

                    if (string.IsNullOrEmpty(state.FirstName))
                    {
                        state.FirstName = message.Text;
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Введите вашу фамилию:");
                    }
                    else if (string.IsNullOrEmpty(state.LastName))
                    {
                        state.LastName = message.Text;
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Введите ваше отчество:");
                    }
                    else if (string.IsNullOrEmpty(state.Patronymic))
                    {
                        state.Patronymic = message.Text;

                        using (var dbContext = MyEntities.GetContext())
                        {
                            var points = dbContext.Points.ToList();
                            var keyboardButtons = points.Select(p => new KeyboardButton(p.PointName)).ToArray();
                            var replyKeyboardMarkup = new ReplyKeyboardMarkup(keyboardButtons) { ResizeKeyboard = true, OneTimeKeyboard = true };

                            state.IsAwaitingPointSelection = true;
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Выберите пункт продаж:", replyMarkup: replyKeyboardMarkup);
                        }
                    }
                    else if (state.IsAwaitingPointSelection)
                    {
                        using (var dbContext = MyEntities.GetContext())
                        {
                            var point = dbContext.Points.FirstOrDefault(p => p.PointName.Equals(message.Text, StringComparison.OrdinalIgnoreCase));
                            if (point == null)
                            {
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Пункт продаж не найден. Пожалуйста, выберите пункт продаж из меню.");
                            }
                            else
                            {
                                var newUser = new User
                                {
                                    Firstname = state.FirstName,
                                    Lastname = state.LastName,
                                    Patronymic = state.Patronymic,
                                    ChatTelegramId = message.Chat.Id.ToString(),
                                    PointId = point.PointId
                                };

                                dbContext.Users.Add(newUser);
                                await dbContext.SaveChangesAsync();

                                _registrationStates.TryRemove(message.Chat.Id, out _);

                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы успешно зарегистрированы.", replyMarkup: new ReplyKeyboardRemove());
                            }
                        }
                    }
                }
            }
        }

        private static async Task SavePhotoInformationAsync(long chatId, string filePath)
        {
            using (var dbContext = MyEntities.GetContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.ChatTelegramId == chatId.ToString());
                if (user == null) return;

                var info = new Information
                {
                    CreatedDate = DateTime.Now,
                    UserId = user.UserId
                };

                dbContext.Informations.Add(info);
                await dbContext.SaveChangesAsync();

                var photo = new Photo
                {
                    Path = filePath,
                    InformationId = info.InformationId
                };

                dbContext.Photos.Add(photo);
                await dbContext.SaveChangesAsync();
            }
        }

        private static async Task SaveLocationInformationAsync(long chatId, float latitude, float longitude)
        {
            using (var dbContext = MyEntities.GetContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.ChatTelegramId == chatId.ToString());
                if (user == null) return;

                var info = dbContext.Informations.OrderByDescending(i => i.CreatedDate).FirstOrDefault(i => i.UserId == user.UserId);
                if (info != null)
                {
                    info.Latitude = latitude;
                    info.Longitude = longitude;
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
