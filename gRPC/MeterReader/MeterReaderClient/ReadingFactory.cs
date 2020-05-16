using Google.Protobuf.WellKnownTypes;
using MeterReaderWeb.Services;
using Microsoft.Extensions.Configuration;
using System;

namespace MeterReaderClient
{
    public class ReadingFactory
    {
        private readonly IConfiguration _config;

        public ReadingFactory(IConfiguration config)
        {
            _config = config;
        }

        public ReadingMessage Generate()
        {
            return new ReadingMessage
            {
                CustomerId = _config.GetValue<int>("Service:CustomerId"),
                ReadinTime = Timestamp.FromDateTime(DateTime.UtcNow),
                ReadinValue = new Random().Next(1000)
            };
        }
    }
}
