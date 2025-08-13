namespace ChatApp.Services;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using ChatApp.Models;
using Microsoft.AspNetCore.SignalR;
using ChatApp.Hubs;

public class RabbitMqConsumer
{
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly IHubContext<ChatHub>? _hubContext;

    // Constructor - dependency injection için
    public RabbitMqConsumer(IHubContext<ChatHub>? hubContext = null)
    {
        _hubContext = hubContext;
    }

    public async Task Start(string userQueue)
    {
        try
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync("chat_exchange", ExchangeType.Direct);
            await _channel.QueueDeclareAsync(queue: userQueue, durable: false, exclusive: false, autoDelete: false);
            await _channel.QueueBindAsync(queue: userQueue, exchange: "chat_exchange", routingKey: userQueue);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonSerializer.Deserialize<ChatMessage>(json);
                    
                    Console.WriteLine($"📩 RabbitMQ: {message?.From} → {message?.To}: {message?.Message}");

                    // SignalR ile de yayınla (eğer hub context varsa)
                    if (_hubContext != null && message != null)
                    {
                        await _hubContext.Clients.Group(message.To).SendAsync("ReceiveMessage", message);
                        Console.WriteLine($"📡 SignalR: Message forwarded to {message.To}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing message: {ex.Message}");
                }
                await Task.CompletedTask;
            };

            await _channel.BasicConsumeAsync(queue: userQueue, autoAck: true, consumer: consumer);
            Console.WriteLine($"🔍 Listening for messages on {userQueue}...");
            
            // Keep the connection alive
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error starting consumer: {ex.Message}");
            throw;
        }
    }

    public async Task Stop()
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                await _channel.DisposeAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error stopping consumer: {ex.Message}");
        }
    }
}
