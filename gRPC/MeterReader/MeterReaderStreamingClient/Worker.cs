using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeterReaderStreamingClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private MeterReadingService.MeterReadingServiceClient _client = null;
        private readonly ReadingFactory _readingFactory;

        public Worker(ILogger<Worker> logger, IConfiguration config, ReadingFactory readingFactory)
        {
            _logger = logger;
            _config = config;
            _readingFactory = readingFactory;
        }

        protected MeterReadingService.MeterReadingServiceClient Client
        {
            get
            {
                if (_client == null)
                {
                    var channel = GrpcChannel.ForAddress(_config["Service:ServerUrl"]);
                    _client = new MeterReadingService.MeterReadingServiceClient(channel);
                }
                return _client;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int counter = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                if (counter % 5 == 0)
                {
                    var stream = Client.SendDiagnostics();
                    for (int index = 0; index < 5; index++)
                    {
                        var reading = _readingFactory.Generate();
                        await stream.RequestStream.WriteAsync(reading);
                    }

                    await stream.RequestStream.CompleteAsync();
                }
                
                counter++;
                await Task.Delay(_config.GetValue<int>("Service:DelayInternal"), stoppingToken);
            }
        }
    }
}
