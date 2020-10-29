using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace StorageInfo
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            long currentSpace = 0;
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var freeBytes = new DriveInfo(System.AppContext.BaseDirectory).AvailableFreeSpace;

                // Continue if no changes on disk space (compare readable format)
                if (Worker.FormatBytes(currentSpace) == Worker.FormatBytes(freeBytes))
                {
                    continue;
                }

                currentSpace = freeBytes;
                
                var factory = new ConnectionFactory() {HostName = "127.0.0.1", UserName = "guest", Password = "guest"};
                
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    var json = JsonConvert.SerializeObject(new
                    {
                        diskSpace = Worker.FormatBytes(freeBytes),
                        createdAt = DateTimeOffset.Now
                    });

                    // convert json to byte
                    var body = Encoding.UTF8.GetBytes(json);
                    
                    // publish to rabbitmq queue
                    channel.BasicPublish(exchange: "", routingKey: "test", basicProperties: null, body: body);
                    
                    // log to logger
                    _logger.LogInformation("Space {space} :Worker running at: {time}", Worker.FormatBytes(freeBytes),
                        DateTimeOffset.Now);
                }
                
                // polling task each 5 seconds
                await Task.Delay(5000, stoppingToken);
            }
        }

        /// <summary>
        /// Formatting byte to readable value (storage disk space)
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string FormatBytes(long bytes)
        {
            string[] suffix = {"B", "KB", "MB", "GB", "TB"};
            int i;
            double dblSByte = bytes;
            for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return $"{dblSByte:0.##} {suffix[i]}";
        }
    }
}