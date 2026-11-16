using Messaging;
using NSubstitute;
using Xunit;

namespace ExamTests;

public class MessagingTest
{
    [Fact]
    public async Task HandleAsync_SendsReply_ToReplyQueue_WithSameCorrelationId()
    {
        // Arrange
        var requestHandler = Substitute.For<IRequestHandler<int, string>>();
        requestHandler.HandleAsync(42, Arg.Any<CancellationToken>()).Returns("svar");

        var sender = Substitute.For<IMessageSender>();
        var sut = new ReplyingMessageHandler<int, string>(requestHandler, sender);

        var correlationId = Guid.NewGuid();
        var request = new RequestEnvelope<int>
        {
            CorrelationId = correlationId,
            ReplyQueue = "reply-queue",
            Payload = 42
        };

        // Act
        await sut.HandleAsync(request, CancellationToken.None);

        // Assert
        await sender.Received(1).SendAsync(
            "reply-queue",
            Arg.Is<ReplyEnvelope>(r => r.CorrelationId == correlationId && r.PayloadJson == "\"svar\""),
            Arg.Any<CancellationToken>());
    }
}
