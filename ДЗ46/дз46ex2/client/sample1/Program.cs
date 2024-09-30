using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

string fileName = "serverconfig.json";
ServerConfig? config = null;

if (File.Exists(fileName))
{
    string json = File.ReadAllText(fileName);
    config = JsonSerializer.Deserialize<ServerConfig>(json);
}

TcpClient client = null;
StreamWriter writer = null;
StreamReader reader = null;
bool connected = false;

while (!connected)
{
    try
    {
        string ip;
        int port;

        if (config != null)
        {
            Console.WriteLine($"Попытка подключения к {config.IP}:{config.Port}...");
            ip = config.IP;
            port = config.Port;
        }
        else
        {
            Console.WriteLine("Введите IP сервера:");
            ip = Console.ReadLine();

            Console.WriteLine("Введите порт сервера:");
            port = int.Parse(Console.ReadLine());
        }

        client = new TcpClient(ip, port);
        var stream = client.GetStream();
        reader = new StreamReader(stream, Encoding.Unicode);
        writer = new StreamWriter(stream, Encoding.Unicode);
        connected = true;

        config = new ServerConfig { IP = ip, Port = port };
        string json = JsonSerializer.Serialize(config);
        File.WriteAllText(fileName, json);
    }
    catch
    {
        Console.WriteLine("Не удалось подключиться. Попробовать снова? (Y/N)");
        if (Console.ReadLine().ToUpper() != "Y") return;
        config = null;
    }
}

string userName = null;
do
{
    Console.WriteLine("Введите имя пользователя:");
    userName = Console.ReadLine();

    await writer.WriteLineAsync(userName);
    await writer.FlushAsync();

    string response = await ReadWithTimeoutAsync(reader, 10000);
    if (response == null)
    {
        Console.WriteLine("Сервер не ответил в течение 10 секунд. Соединение закрыто.");
        client.Close();
        return;
    }
    else if (response == "Имя занято")
    {
        Console.WriteLine("Имя уже используется. Попробуйте снова.");
    }
} while (userName == null);

string activeUsers = await ReadWithTimeoutAsync(reader, 10000);
if (activeUsers == null)
{
    Console.WriteLine("Сервер не ответил в течение 10 секунд. Соединение закрыто.");
    client.Close();
    return;
}
Console.WriteLine(activeUsers);

Task.Run(async () =>
{
    while (true)
    {
        try
        {
            string serverMessage = await ReadWithTimeoutAsync(reader, 10000);
            if (serverMessage == null)
            {
                Console.WriteLine("Соединение с сервером потеряно.");
                break;
            }
            Console.WriteLine(serverMessage);
        }
        catch
        {
            Console.WriteLine("Соединение потеряно.");
            break;
        }
    }
});

while (true)
{
    string message = Console.ReadLine();
    if (message.StartsWith("->"))
    {
        var split = message.Split(":", 2);
        var targets = split[0].Substring(2).Split(",");
        foreach (var target in targets)
        {
            await writer.WriteLineAsync($"private|{target.Trim()}|{split[1].Trim()}");
        }
    }
    else
    {
        await writer.WriteLineAsync(message);
    }
    await writer.FlushAsync();
}

async Task<string?> ReadWithTimeoutAsync(StreamReader reader, int timeoutMs)
{
    var cts = new CancellationTokenSource();
    cts.CancelAfter(timeoutMs);

    try
    {
        return await Task.Run(() => reader.ReadLineAsync(), cts.Token);
    }
    catch (TaskCanceledException)
    {
        return null;
    }
}

class ServerConfig
{
    public string IP { get; set; }
    public int Port { get; set; }
}
