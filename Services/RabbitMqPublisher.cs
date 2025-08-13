namespace ChatApp.Services;

using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using ChatApp.Models;

public class RabbitMqPublisher
{
    public async Task SendMessage(ChatMessage message)
    {
        try
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.ExchangeDeclareAsync("chat_exchange", ExchangeType.Direct);
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Kullanıcıya özel route (örneğin user_B)
            await channel.BasicPublishAsync(
                exchange: "chat_exchange",
                routingKey: message.To,
                body: body
            );
            
            Console.WriteLine($"📤 Message sent from {message.From} to {message.To}: {message.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending message: {ex.Message}");
            throw;
        }
    }
}
