using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

var server = new Server();
await server.Start();

class Client
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string UserName { get; private set; }
    public StreamWriter Writer { get; }
    public StreamReader Reader { get; }

    private TcpClient _client;
    private Server _server;

    public Client(TcpClient tcpClient, Server serverObject)
    {
        _client = tcpClient;
        _server = serverObject;
        var stream = _client.GetStream();
        Reader = new StreamReader(stream, Encoding.Unicode);
        Writer = new StreamWriter(stream, Encoding.Unicode);
    }

    public async Task ProcessAsync()
    {
        try
        {
            string? userName;
            do
            {
                userName = await Reader.ReadLineAsync();
                if (_server.IsUserNameTaken(userName))
                {
                    await Writer.WriteLineAsync("Имя занято");
                    await Writer.FlushAsync();
                }
                else
                {
                    UserName = userName;
                    await Writer.WriteLineAsync("Имя принято");
                    await Writer.FlushAsync();
                    break;
                }
            } while (true);

            string activeUsers = _server.GetActiveUsers();
            await Writer.WriteLineAsync(activeUsers);
            await Writer.FlushAsync();

            string message = $"{UserName} вошел в чат";
            await _server.BroadcastMessageAsync(message, Id);
            Console.WriteLine(message);

            while (true)
            {
                message = await Reader.ReadLineAsync();
                if (message.StartsWith("private|"))
                {
                    var parts = message.Split('|');
                    var targetUserName = parts[1];
                    var privateMessage = parts[2];
                    await _server.SendPrivateMessageAsync(privateMessage, UserName, targetUserName);
                }
                else
                {
                    message = $"{UserName}: {message}";
                    Console.WriteLine(message);
                    await _server.BroadcastMessageAsync(message, Id);
                }
            }
        }
        catch
        {
            string message = $"{UserName} покинул чат";
            Console.WriteLine(message);
            await _server.BroadcastMessageAsync(message, Id);
        }
        finally
        {
            _server.RemoveClient(Id);
        }
    }

    public void Close()
    {
        Writer.Close();
        Reader.Close();
        _client.Close();
    }
}

class Server
{
    private TcpListener _tcpListener = new TcpListener(IPAddress.Any, 11000);
    private Dictionary<string, Client> _clients = new Dictionary<string, Client>();

    public async Task Start()
    {
        try
        {
            _tcpListener.Start();
            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (true)
            {
                TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                Client clientObject = new Client(tcpClient, this);
                Task.Run(clientObject.ProcessAsync);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            DisconnectAll();
        }
    }

    public bool IsUserNameTaken(string userName)
    {
        foreach (var client in _clients.Values)
        {
            if (client.UserName == userName) return true;
        }
        return false;
    }

    public string GetActiveUsers()
    {
        var activeUsers = new StringBuilder("Активные пользователи: ");
        foreach (var client in _clients.Values)
        {
            activeUsers.Append(client.UserName).Append(", ");
        }
        return activeUsers.ToString().TrimEnd(',', ' ');
    }

    public async Task BroadcastMessageAsync(string message, string id)
    {
        foreach (var client in _clients.Values)
        {
            if (client.Id != id)
            {
                await client.Writer.WriteLineAsync($"{client.UserName} ({DateTime.Now:HH:mm}): {message}");
                await client.Writer.FlushAsync();
            }
        }
    }

    public async Task SendPrivateMessageAsync(string message, string fromUser, string toUser)
    {
        var targetClient = _clients.Values.FirstOrDefault(c => c.UserName == toUser);
        if (targetClient != null)
        {
            await targetClient.Writer.WriteLineAsync($"(Private) {fromUser} ({DateTime.Now:HH:mm}): {message}");
            await targetClient.Writer.FlushAsync();
        }
        else
        {
            var senderClient = _clients.Values.FirstOrDefault(c => c.UserName == fromUser);
            if (senderClient != null)
            {
                await senderClient.Writer.WriteLineAsync($"Пользователь {toUser} не найден");
                await senderClient.Writer.FlushAsync();
            }
        }
    }

    public void AddClient(Client client)
    {
        _clients.Add(client.Id, client);
    }

    public void RemoveClient(string id)
    {
        if (_clients.Remove(id, out var client))
        {
            client.Close();
        }
    }

    public void DisconnectAll()
    {
        foreach (var client in _clients.Values)
        {
            client.Close();
        }
        _tcpListener.Stop();
    }
}
