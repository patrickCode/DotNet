using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MeterReaderWeb.Data;
using MeterReaderWeb.Data.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MeterReaderWeb.Services
{
    public class MeterService : MeterReadingService.MeterReadingServiceBase
    {
        private readonly ILogger<MeterService> _logger;
        private readonly IReadingRepository _repository;

        public MeterService(ILogger<MeterService> logger, IReadingRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public override async Task<StatusMessage> AddReading(ReadingPackets request, ServerCallContext context)
        {
            var result = new StatusMessage
            {
                Success = ReadingStatus.Failure
            };
            try
            {
                if (request.Succesful == ReadingStatus.Success)
                {
                    foreach (var r in request.Readings)
                    {
                        if (r.ReadinValue < 1000)
                        {
                            _logger.LogError("RPC Exception - Out of range");
                            // Keys must not contain spaces
                            var trailer = new Metadata()
                            {
                                { "BadValue", r.ReadinValue.ToString() },
                                { "Field", "ReadingValue" },
                                { "CustomerId", r.CustomerId.ToString() }
                            };
                            throw new RpcException(new Status(StatusCode.OutOfRange, "Value too low"), trailer);
                        }

                        var reading = new MeterReading
                        {
                            Value = r.ReadinValue,
                            ReadingDate = r.ReadinTime.ToDateTime(),
                            CustomerId = r.CustomerId
                        };

                        _repository.AddEntity(reading);
                    }

                    if (await _repository.SaveAllAsync())
                    {
                        result.Success = ReadingStatus.Success;
                    }
                }
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception error)
            {   
                _logger.LogError($"Exception thrown: {error.Message}");
                throw new RpcException(Status.DefaultCancelled, $"Exception thrown: {error.Message}");
            }

            return result;
        }

        public override async Task<Empty> SendDiagnostics(IAsyncStreamReader<ReadingMessage> requestStream, ServerCallContext context)
        {
            await Task.Run(async () =>
            {
                await foreach(var reading in requestStream.ReadAllAsync())
                {
                    _logger.LogInformation($"Receing stream - {reading.ReadinValue}");
                }
            });

            return new Empty();
        }
    }
}
