using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

TcpClient client = null;
StreamWriter writer = null;
StreamReader reader = null;
bool connected = false;

while (!connected)
{
    try
    {
        Console.WriteLine("Введите IP сервера:");
        string ip = Console.ReadLine();

        Console.WriteLine("Введите порт сервера:");
        int port = int.Parse(Console.ReadLine());

        client = new TcpClient(ip, port);
        var stream = client.GetStream();
        reader = new StreamReader(stream, Encoding.Unicode);
        writer = new StreamWriter(stream, Encoding.Unicode) { AutoFlush = true };
        connected = true;
    }
    catch
    {
        Console.WriteLine("Не удалось подключиться. Попробовать снова? (Y/N)");
        if (Console.ReadLine().ToUpper() != "Y") return;
    }
}

Console.WriteLine("Введите имя пользователя:");
string userName = Console.ReadLine();
await writer.WriteLineAsync(userName);

string response = await reader.ReadLineAsync();
if (response == "Введите пароль:")
{
    int attempts = 3;
    while (attempts > 0)
    {
        Console.WriteLine(response);
        string password = Console.ReadLine();
        await writer.WriteLineAsync(password);

        response = await reader.ReadLineAsync();
        if (response.Contains("Добро пожаловать"))
        {
            Console.WriteLine(response);
            break;
        }
        else
        {
            Console.WriteLine(response);
            attempts--;
        }

        if (attempts == 0)
        {
            Console.WriteLine("Соединение разорвано.");
            client.Close();
            return;
        }
    }
}
else if (response.Contains("Пользователь не найден"))
{
    Console.WriteLine(response);
    string answer = Console.ReadLine();
    await writer.WriteLineAsync(answer);

    if (answer.ToUpper() == "Y")
    {
        Console.WriteLine(await reader.ReadLineAsync());
        string newPassword = Console.ReadLine();
        await writer.WriteLineAsync(newPassword);

        Console.WriteLine(await reader.ReadLineAsync());
    }
    else
    {
        client.Close();
        return;
    }
}
