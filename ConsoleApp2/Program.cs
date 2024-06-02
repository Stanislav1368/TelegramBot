using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.InputFiles;

namespace ConsoleApp2
{
    public class RegistrationState
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Patronymic { get; set; }
        public string PointName { get; set; }
        public bool IsAwaitingPointSelection { get; set; }
        public bool IsAwaitingLocation { get; set; }
    }

    public class UserState
    {
        public int InformationId { get; set; }
    }

    class Program
    {
        private static TelegramBotClient _botClient;
        private static ConcurrentDictionary<long, RegistrationState> _registrationStates = new ConcurrentDictionary<long, RegistrationState>();
        private static ConcurrentDictionary<long, UserState> _userStates = new ConcurrentDictionary<long, UserState>();
        private static ConcurrentDictionary<long, int> _photoCounts = new ConcurrentDictionary<long, int>();
        private static readonly object _fileLock = new object();
        private const long AdminChatId = 1992708847; // Замените на нужный ChatID администратора

        static async Task Main(string[] args)
        {
            // Инициализация клиента бота
            _botClient = new TelegramBotClient("6565397184:AAEIgeI2tRkmM16vEhtesbgbxfEXIMiBdmQ");

            // Запуск обработчика сообщений
            _botClient.OnMessage += BotOnMessageReceived;
            _botClient.StartReceiving();

            // Планирование задач
            ScheduleTasks();

            Console.WriteLine("Bot started...");
            Console.ReadLine();

            // Остановка обработки сообщений перед завершением программы
            _botClient.StopReceiving();
        }

        private static void ScheduleTasks()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    var now = DateTime.UtcNow.AddHours(3); // Время по МСК
                    if (now.Hour == 8 && now.Minute == 0 || now.Hour == 10 && now.Minute == 0 || now.Hour == 12 && now.Minute == 0 || now.Hour == 14 && now.Minute == 0 || now.Hour == 16 && now.Minute == 0 || now.Hour == 18 && now.Minute == 0)
                    {
                        await RequestDataFromUsers();
                        await Task.Delay(3600000); // Wait for an hour
                    }
                    else if (now.Hour == 19 && now.Minute == 0)
                    {
                        await SendDataToAdmin();
                        await Task.Delay(3600000); // Wait for an hour
                    }
                    await Task.Delay(60000); // Check every minute
                }
            });
        }

        private static async Task RequestDataFromUsers()
        {
            using (var dbContext = MyEntities.GetContext())
            {
                var users = dbContext.Users.ToList();
                foreach (var user in users)
                {
                    if (long.TryParse(user.ChatTelegramId, out var chatId))
                    {
                        await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, отправьте 5 фото и вашу геолокацию.");
                        _photoCounts[chatId] = 0;

                        // Создаем запись Information для каждого пользователя
                        var info = new Information
                        {
                            CreatedDate = DateTime.Now,
                            UserId = user.UserId
                        };

                        dbContext.Informations.Add(info);
                        await dbContext.SaveChangesAsync();

                        // Сохраняем ID новой записи Information
                        _userStates[chatId] = new UserState { InformationId = info.InformationId };
                    }
                }
            }
        }

        private static async Task SendDataToAdmin()
        {
            using (var dbContext = MyEntities.GetContext())
            {
                var today = DateTime.Today.Day;
                var informations = dbContext.Informations.Where(i => i.CreatedDate.Day == today && i.Latitude != 0).ToList();
                foreach (var info in informations)
                {
                    var message = $"Пользователь: {info.User.Firstname} {info.User.Lastname} {info.User.Patronymic}\n" +
                                  $"Точка: {info.User.Point.PointName} Адрес: {info.User.Point.Adress} Координаты: {info.User.Point.Latitude} {info.User.Point.Longitude} \n" +
                                  $"Дата: {info.CreatedDate}\n" +
                                  $"Локация: {info.Latitude}, {info.Longitude}\n" +
                                  $"Проврека геолокации:  {IsWithinRange(info.Latitude, info.Longitude, info.User.Point.Latitude, info.User.Point.Longitude)}\n";
                    await _botClient.SendTextMessageAsync(AdminChatId, message);

                    foreach (var photo in info.Photos)
                    {
                        using (var photoStream = new FileStream(photo.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var inputOnlineFile = new InputOnlineFile(photoStream, Path.GetFileName(photo.Path));
                            await _botClient.SendPhotoAsync(AdminChatId, inputOnlineFile);
                        }
                    }
                }
            }
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs e)
        {
            var message = e.Message;

            if (message.Type == MessageType.Photo)
            {
                if (!_photoCounts.ContainsKey(message.Chat.Id))
                {
                    return;
                }

                var photoCount = _photoCounts[message.Chat.Id];
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

                    _photoCounts[message.Chat.Id] = photoCount + 1;

                    if (photoCount + 1 == 5)
                    {
                        var state = GetOrCreateState(message.Chat.Id);
                        state.IsAwaitingLocation = true;

                        var locationButton = new KeyboardButton("Отправить геолокацию") { RequestLocation = true };
                        var replyKeyboardMarkup = new ReplyKeyboardMarkup(locationButton) { ResizeKeyboard = true, OneTimeKeyboard = true };
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте вашу геолокацию.", replyMarkup: replyKeyboardMarkup);
                    }
                }
            }
            else if (message.Type == MessageType.Location)
            {
                var state = GetOrCreateState(message.Chat.Id);
                if (_photoCounts.ContainsKey(message.Chat.Id) && _photoCounts[message.Chat.Id] >= 5 && state.IsAwaitingLocation)
                {
                    await SaveLocationInformationAsync(message.Chat.Id, message.Location.Latitude, message.Location.Longitude);

                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Спасибо! Ваши данные успешно сохранены.", replyMarkup: new ReplyKeyboardRemove());
                    _photoCounts.TryRemove(message.Chat.Id, out _);
                    state.IsAwaitingLocation = false;
                }
            }
            else if (message.Type == MessageType.Text)
            {
                using (var dbContext = MyEntities.GetContext())
                {
                    var existingUser = dbContext.Users.FirstOrDefault(u => u.ChatTelegramId == message.Chat.Id.ToString());

                    if (message.Text.Equals("/test", StringComparison.OrdinalIgnoreCase))
                    {
                        // Вызов метода SendRequests при получении команды /test
                        await RequestDataFromUsers();
                    }
                    if (message.Text.Equals("/test2", StringComparison.OrdinalIgnoreCase))
                    {
                        // Вызов метода SendRequests при получении команды /test2
                        await SendDataToAdmin();
                    }
                    else if (message.Text.StartsWith("/register"))
                    {
                        if (existingUser != null)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы уже зарегистрированы.");
                            return;
                        }

                        _registrationStates[message.Chat.Id] = new RegistrationState();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Введите ваше имя:");
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

                            var points = dbContext.Points.ToList();
                            var keyboardButtons = points.Select(p => new KeyboardButton(p.PointName)).ToArray();
                            var replyKeyboardMarkup = new ReplyKeyboardMarkup(keyboardButtons) { ResizeKeyboard = true, OneTimeKeyboard = true };

                            state.IsAwaitingPointSelection = true;
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Выберите пункт продаж:", replyMarkup: replyKeyboardMarkup);
                        }
                        else if (state.IsAwaitingPointSelection)
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

                if (!_userStates.TryGetValue(chatId, out var userState))
                {
                    return;
                }

                var info = dbContext.Informations.FirstOrDefault(i => i.InformationId == userState.InformationId);
                if (info == null)
                {
                    return;
                }

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

                if (!_userStates.TryGetValue(chatId, out var userState))
                {
                    return;
                }

                var info = dbContext.Informations.FirstOrDefault(i => i.InformationId == userState.InformationId);
                if (info != null)
                {
                    info.Latitude = latitude;
                    info.Longitude = longitude;
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private static RegistrationState GetOrCreateState(long chatId)
        {
            if (!_registrationStates.ContainsKey(chatId))
            {
                _registrationStates[chatId] = new RegistrationState();
            }
            return _registrationStates[chatId];
        }
        static bool IsWithinRange(double lat1, double lon1, double lat2, double lon2)
        {
            // Радиус Земли в километрах
            const double radius = 6371;

            // Преобразование градусов в радианы
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            // Вычисление расстояния с помощью формулы гаверсинусов
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = radius * c;

            // Проверяем, находится ли точка 1 в пределах 200 метров от точки 2
            return distance <= 0.2; // Переводим 200 метров в километры
        }

        static double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
    }
}
