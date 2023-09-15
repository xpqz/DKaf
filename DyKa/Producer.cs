using Confluent.Kafka;
using System;

namespace DyKa
{
    public class Producer : IDisposable
    {
        public IProducer<string, byte[]>? UnderlyingProducer { get; private set; }
        private ProducerConfig _config;

        public Producer(ProducerConfig conf)
        {
            _config = conf;
            UnderlyingProducer = new ProducerBuilder<string, byte[]>(_config).Build();
        }

        public Producer(string bootstrapServers)
        {
            _config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers
            };

            UnderlyingProducer = new ProducerBuilder<string, byte[]>(_config).Build();
        }

        public void Produce(string topic, byte[] value, string key)
        {
            UnderlyingProducer?.Produce(topic, new Message<string, byte[]>
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
