using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBridge
{
    [ApiVersion(2, 1)]
    public class TelegramBridge : TerrariaPlugin
    {
        public override string Name => "Telegram Chat Bridge";
        public override string Author => "yomissayy";
        public override string Description => "Двусторонний чат между Terraria и Telegram";
        public override Version Version => new Version(1, 1, 0, 0);

        private ITelegramBotClient? _botClient;
        
        // ТОКЕН И ID ЧАТА
        private const string BotToken = "ТОКЕН_СЮДА";
        private const string ChatId = "АЙДИ_СЮДА"; 

        public TelegramBridge(Main game) : base(game) { }

        public override void Initialize()
        {
            _botClient = new TelegramBotClient(BotToken);

            // Настройка приема сообщений
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Получать все обновления
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync, 
                receiverOptions: receiverOptions
            );

            // Регистрация хука на чат игры
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        // 1. Метод: ТЕЛЕГРАМ -> ИГРА
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Проверяем, что это текстовое сообщение
            if (update.Message is not { } message || string.IsNullOrEmpty(message.Text))
                return;

            // Фильтр по ID чата (чтобы бот не слушал чужие чаты)
            if (message.Chat.Id.ToString() != ChatId)
                return;

            string senderName = message.From?.FirstName ?? "Telegram User";
            string text = message.Text;

            // Выводим в чат игры (Цвет: SkyBlue)
            TShock.Utils.Broadcast($"[TG] {senderName}: {text}", 135, 206, 250);
            
            await Task.CompletedTask;
        }

        // 2. Метод: ИГРА -> ТЕЛЕГРАМ
        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled) return;

            var player = TShock.Players[args.Who];
            if (player == null || !player.Active) return;

            // Игнорируем команды
            if (args.Text.StartsWith("/") || args.Text.StartsWith(".")) return;

            string messageToTg = $"[{player.Name}]: {args.Text}";

            Task.Run(async () =>
            {
                try
                {
                    await _botClient!.SendMessage(ChatId, messageToTg);
                }
                catch (Exception ex)
                {
                    TShock.Log.Error($"[Telegram Bridge] Ошибка отправки: {ex.Message}");
                }
            });
        }

        private async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            TShock.Log.Error($"[Telegram Bridge] Ошибка API: {exception.Message}");
            await Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
            base.Dispose(disposing);
        }
    }
}
