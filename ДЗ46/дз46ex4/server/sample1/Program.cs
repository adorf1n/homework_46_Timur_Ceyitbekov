using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatServerApp
{
    class Program
    {
        private const string usersFileName = "users.json";
        private static Dictionary<string, string> users = LoadUsers();

        static async Task Main(string[] args)
        {
            var server = new Server(users);
            await server.Start();
        }

        private static Dictionary<string, string> LoadUsers()
        {
            if (File.Exists(usersFileName))
            {
                string json = File.ReadAllText(usersFileName);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            return new Dictionary<string, string>();
        }

        public static void SaveUsers(Dictionary<string, string> users)
        {
            string json = JsonSerializer.Serialize(users);
            File.WriteAllText(usersFileName, json);
        }
    }

    class Server
    {
        private TcpListener _tcpListener = new TcpListener(IPAddress.Any, 11000);
        private Dictionary<string, string> _users;

        public Server(Dictionary<string, string> users)
        {
            _users = users;
        }

        protected internal async Task Start()
        {
            try
            {
                _tcpListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    Task.Run(() => HandleClient(tcpClient));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task HandleClient(TcpClient tcpClient)
        {
            var stream = tcpClient.GetStream();
            var reader = new StreamReader(stream, Encoding.Unicode);
            var writer = new StreamWriter(stream, Encoding.Unicode) { AutoFlush = true };

            string userName = await reader.ReadLineAsync();

            if (_users.ContainsKey(userName))
            {
                int attempts = 3;
                while (attempts > 0)
                {
                    await writer.WriteLineAsync("Введите пароль:");
                    string password = await reader.ReadLineAsync();

                    if (_users[userName] == password)
                    {
                        await writer.WriteLineAsync("Добро пожаловать в чат!");
                        Console.WriteLine($"{userName} вошел в чат.");
                        break;
                    }
                    else
                    {
                        attempts--;
                        await writer.WriteLineAsync(attempts > 0 ? $"Неверный пароль. Осталось попыток: {attempts}" : "Вы исчерпали все попытки.");
                    }

                    if (attempts == 0)
                    {
                        tcpClient.Close();
                        return;
                    }
                }
            }
            else
            {
                await writer.WriteLineAsync("Пользователь не найден. Хотите зарегистрироваться? (Y/N)");
                string response = await reader.ReadLineAsync();

                if (response.ToUpper() == "Y")
                {
                    await writer.WriteLineAsync("Введите пароль:");
                    string newPassword = await reader.ReadLineAsync();
                    _users.Add(userName, newPassword);
                    Program.SaveUsers(_users);
                    await writer.WriteLineAsync("Вы успешно зарегистрированы!");
                    Console.WriteLine($"{userName} зарегистрировался.");
                }
                else
                {
                    tcpClient.Close();
                    return;
                }
            }

        }
    }
}
