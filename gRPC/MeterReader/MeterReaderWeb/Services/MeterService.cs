using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MeterReaderLib;
using MeterReaderLib.Models;
using MeterReaderWeb.Data;
using MeterReaderWeb.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MeterReaderWeb.Services
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MeterService : MeterReadingService.MeterReadingServiceBase
    {
        private readonly ILogger<MeterService> _logger;
        private readonly IReadingRepository _repository;
        private readonly JwtTokenValidationService _validationService;

        public MeterService(ILogger<MeterService> logger, IReadingRepository repository, JwtTokenValidationService validationService)
        {
            _logger = logger;
            _repository = repository;
            _validationService = validationService;
        }

        [AllowAnonymous]
        public override async Task<TokenResponse> CreateToken(TokenRequest request, ServerCallContext context)
        {
            var credential = new CredentialModel()
            {
                UserName = request.Username,
                Passcode = request.Password
            };
            var response = await _validationService.GenerateTokenModelAsync(credential);
            if (response.Success)
            {
                return new TokenResponse
                {
                    Token = response.Token,
                    Sucess = true,
                    ExpirationTime = Timestamp.FromDateTime(response.Expiration)
                };
            }
            return new TokenResponse
            {
                Sucess = false
            };
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

        [AllowAnonymous]
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
