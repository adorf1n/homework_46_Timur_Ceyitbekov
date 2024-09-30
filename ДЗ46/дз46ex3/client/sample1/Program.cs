using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatClientApp
{
    class Program
    {
        private const string credentialsFileName = "credentials.json";

        static async Task Main(string[] args)
        {
            var credentials = LoadCredentials();

            string serverIp = "127.0.0.1";
            int serverPort = 11000;

            if (credentials != null)
            {
                Console.WriteLine("Попытка автоматического логина...");
                if (await TryLoginAsync(credentials.UserName, credentials.Password, serverIp, serverPort))
                {
                    Console.WriteLine("Успешный автоматический вход.");
                    return;
                }
                else
                {
                    Console.WriteLine("Автоматический логин не удался. Переход в ручной режим.");
                }
            }

            Console.WriteLine("Введите имя пользователя:");
            string userName = Console.ReadLine();

            Console.WriteLine("Введите пароль:");
            string password = ReadPassword();

            if (await TryLoginAsync(userName, password, serverIp, serverPort))
            {
                SaveCredentials(new Credentials { UserName = userName, Password = password });
            }
        }

        private static string ReadPassword()
        {
            StringBuilder password = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                password.Append(key.KeyChar);
                Console.Write("*");
            }
            Console.WriteLine();
            return password.ToString();
        }

        private static Credentials LoadCredentials()
        {
            if (File.Exists(credentialsFileName))
            {
                string json = File.ReadAllText(credentialsFileName);
                return JsonSerializer.Deserialize<Credentials>(json);
            }
            return null;
        }

        private static void SaveCredentials(Credentials credentials)
        {
            string json = JsonSerializer.Serialize(credentials);
            File.WriteAllText(credentialsFileName, json);
        }

        private static async Task<bool> TryLoginAsync(string userName, string password, string serverIp, int serverPort)
        {
            try
            {
                using TcpClient client = new TcpClient();
                await client.ConnectAsync(serverIp, serverPort);
                using NetworkStream stream = client.GetStream();
                using StreamReader reader = new StreamReader(stream, Encoding.Unicode);
                using StreamWriter writer = new StreamWriter(stream, Encoding.Unicode) { AutoFlush = true };

                await writer.WriteLineAsync(userName);

                string response = await reader.ReadLineAsync();
                if (response == "Введите пароль:")
                {
                    await writer.WriteLineAsync(password);

                    response = await reader.ReadLineAsync();
                    if (response == "Добро пожаловать в чат!")
                    {
                        Console.WriteLine(response);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения или логина: {ex.Message}");
            }

            return false;
        }
    }

    public class Credentials
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
