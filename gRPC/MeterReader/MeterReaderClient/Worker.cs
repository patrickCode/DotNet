using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
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
        private readonly ILoggerFactory _loggerFactory;

        public Worker(ILogger<Worker> logger, IConfiguration config, ReadingFactory readingFactory, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _config = config;
            _readingFactory = readingFactory;
            _loggerFactory = loggerFactory;
        }

        protected MeterReadingService.MeterReadingServiceClient Client
        {
            get
            {
                if (_client == null)
                {
                    var opts = new GrpcChannelOptions()
                    {
                        LoggerFactory = _loggerFactory
                    };
                    var channel = GrpcChannel.ForAddress(_config["Service:ServerUrl"], opts);
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

                try
                {
                    var result = Client.AddReading(packet);
                    if (result.Success == ReadingStatus.Success)
                    {
                        _logger.LogInformation($"Success: {result.Message}");
                    }
                    else
                    {
                        _logger.LogInformation($"Failed: {result.Message}");
                    }
                }
                catch (RpcException exception)
                {
                    _logger.LogError($"RPC Exception - Status Code: {exception.StatusCode}");
                    _logger.LogError($"{exception.Trailers}");
                    _logger.LogError(exception.Message);
                }
                await Task.Delay(_config.GetValue<int>("Service:DelayInternal"), stoppingToken);
            }
        }
    }
}
