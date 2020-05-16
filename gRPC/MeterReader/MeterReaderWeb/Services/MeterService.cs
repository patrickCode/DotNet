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
            catch (Exception error)
            {
                result.Message = $"Exception thrown while adding reading";
                _logger.LogError($"Exception thrown: {error.Message}");
            }

            return result;
        }
    }
}
