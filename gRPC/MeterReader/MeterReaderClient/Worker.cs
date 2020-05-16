using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeterReaderClient
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
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                var packet = new ReadingPackets()
                {
                    Notes = "This is called from Worker",
                    Succesful = ReadingStatus.Success,
                };
                for (int index = 0; index < 5; index++)
                {
                    packet.Readings.Add(_readingFactory.Generate());
                }

                var result = Client.AddReading(packet);
                if (result.Success == ReadingStatus.Success)
                {
                    _logger.LogInformation($"Success: {result.Message}");
                }
                else
                {
                    _logger.LogInformation($"Failed: {result.Message}");
                }
                await Task.Delay(_config.GetValue<int>("Service:DelayInternal"), stoppingToken);
            }
        }
    }
}
