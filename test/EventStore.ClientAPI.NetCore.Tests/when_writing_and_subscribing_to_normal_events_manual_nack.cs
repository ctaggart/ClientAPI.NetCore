using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("ClientAPI"), Category("LongRunning")]
    public class when_writing_and_subscribing_to_normal_events_manual_nack : SpecificationWithConnection
    {
        private readonly string StreamName = Guid.NewGuid().ToString();
        private readonly string GroupName = Guid.NewGuid().ToString();
        private const int BufferCount = 10;
        private const int EventWriteCount = BufferCount * 2;

        private readonly ManualResetEvent _eventsReceived = new ManualResetEvent(false);
        private int _eventReceivedCount;

        protected override void When()
        {

        }


        [Test]
        public void Test()
        {
            var settings = PersistentSubscriptionSettings
                .Create()
                .StartFromCurrent()
                .ResolveLinkTos()
                .Build();

            _conn.CreatePersistentSubscriptionAsync(StreamName, GroupName, settings, DefaultData.AdminCredentials)
                .Wait();
            _conn.ConnectToPersistentSubscription(StreamName, GroupName,
                (subscription, resolvedEvent) =>
                {
                    subscription.Fail(resolvedEvent, PersistentSubscriptionNakEventAction.Park, "fail");

                    if (Interlocked.Increment(ref _eventReceivedCount) == EventWriteCount)
                    {
                        _eventsReceived.Set();
                    }
                    return Task.CompletedTask;
                },
                (sub, reason, exception) =>
                    Console.WriteLine("Subscription dropped (reason:{0}, exception:{1}).", reason, exception),
                bufferSize: 10, autoAck: false, userCredentials: DefaultData.AdminCredentials);

            for (var i = 0; i < EventWriteCount; i++)
            {
                var eventData = new EventData(Guid.NewGuid(), "SomeEvent", false, new byte[0], new byte[0]);

                _conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, DefaultData.AdminCredentials, eventData);
            }

            if (!_eventsReceived.WaitOne(TimeSpan.FromSeconds(5)))
            {
                throw new Exception("Timed out waiting for events.");
            }
        }
    }
}