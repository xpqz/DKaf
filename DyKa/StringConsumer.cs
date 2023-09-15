using Confluent.Kafka;
using System;

namespace DyKa
{
    public class StringConsumer : IDisposable
    {
        public IConsumer<string, string>? UnderlyingConsumer { get; private set; }
        private ConsumerConfig _config;

        public StringConsumer(string bootstrapServers, string groupId, string topic)
        {
            _config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            UnderlyingConsumer = new ConsumerBuilder<string, string>(_config).Build();
            UnderlyingConsumer.Subscribe(topic);
        }

        public ConsumeResult<string, string>? Consume(int timeout)
        {
            if (timeout == 0)
            {
                return UnderlyingConsumer?.Consume();
            }
            return UnderlyingConsumer?.Consume(TimeSpan.FromSeconds(timeout));
        }

        public void Dispose()
        {
            UnderlyingConsumer?.Close();
            UnderlyingConsumer?.Dispose();
        }
    }
}
