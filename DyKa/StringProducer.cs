using Confluent.Kafka;
using System;

namespace DyKa
{
    public class StringProducer : IDisposable
    {
        public IProducer<string, string>? UnderlyingProducer { get; private set; }
        private ProducerConfig _config;

        public StringProducer(ProducerConfig conf)
        {
            _config = conf;
            UnderlyingProducer = new ProducerBuilder<string, string>(_config).Build();
        }

        public StringProducer(string bootstrapServers)
        {
            _config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers
            };

            UnderlyingProducer = new ProducerBuilder<string, string>(_config).Build();
        }

        public void Produce(string topic, string value, string key)
        {
            UnderlyingProducer?.Produce(topic, new Message<string, string>
            {
                Key = key,
                Value = value
            },
            deliveryReport =>
            {
                if (deliveryReport.Error.Code != ErrorCode.NoError)
                {
                    Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
                }
                else
                {
                    Console.WriteLine($"Produced message to: {deliveryReport.TopicPartitionOffset}");
                }
            });
        }

        public void Flush(TimeSpan timeout)
        {
            UnderlyingProducer?.Flush(timeout);
        }

        public void Dispose()
        {
            UnderlyingProducer?.Dispose();
        }
    }
}
