using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Runtime.InteropServices;

namespace HiBunny
{
    internal class Program
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        private static ILogger<Program>? _logger { get; set; }
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
               //.UseContentRoot(AppContext.BaseDirectory)
               .UseConsoleLifetime()
               .ConfigureLogging((context, logbuilder) =>
               {
                   logbuilder.ClearProviders();
                   logbuilder.AddDebug();
                   logbuilder.AddConsole();
                   logbuilder.AddFile(context.Configuration.GetSection("Logging"));
               })
               .ConfigureServices((context, services) => Program.ConfigureServices(context, services))
               .Build();

            Program.ServiceProvider = host.Services;
            Program._logger = host.Services.GetRequiredService<ILogger<Program>>();
            _logger.LogInformation($"Host Initialized.");
            await host.StartAsync();

            //RabbitMQ 

            string topicName = "portal.quoterequests";
            string queueName = "portal.quoterequests.bunny";

            ConnectionFactory factory = new ConnectionFactory();
            factory.UserName = "guest";
            factory.Password = "guest";
            factory.VirtualHost = "/";
            factory.HostName = "localhost";
            factory.ContinuationTimeout = TimeSpan.FromSeconds(30);

            IConnection conn = factory.CreateConnection();

            IModel channel = conn.CreateModel();

            channel.QueueDeclare(queueName, true, false, false, null);
            channel.QueueBind(queueName, topicName, "#");

            var bd = new ReadOnlyMemory<byte>(System.Text.Encoding.UTF8.GetBytes("This is test message from Hi Bunny."));

            channel.BasicPublish(topicName, "", body: bd);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (ch, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                string bodyString = System.Text.Encoding.UTF8.GetString(body);
                _logger.LogTrace($"Message Received:");
                _logger.LogTrace(bodyString);
                channel.BasicAck(ea.DeliveryTag, false);
            };

            string consumeTag = channel.BasicConsume(queueName, false, consumer);

            await host.WaitForShutdownAsync();

            channel.Close();
            conn.Close();
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
        }
    }
}