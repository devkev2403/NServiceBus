namespace NServiceBus.Core.Tests.DataBus
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Unicast;
    using NUnit.Framework;
    using Conventions = NServiceBus.Conventions;

    [TestFixture]
    class When_applying_the_databus_message_mutator_to_null_properties 
    {
        [Test]
        public void Should_not_blow_up()
        {
            var context = new OutgoingContext(null, new SendMessageOptions("MyEndpoint"), new Dictionary<string, string>(), "msg id", MessageIntentEnum.Send, null, new MessageWithNullDataBusProperty(), null);
            var sendBehavior = new DataBusSendBehavior
            {
                DataBus = null,
                Conventions = new Conventions(),
                DataBusSerializer = new DefaultDataBusSerializer(),
            };
            
            using (var stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, "test");
                stream.Position = 0;

                sendBehavior.Invoke(context, () => { });            
            }
        }

    }
}