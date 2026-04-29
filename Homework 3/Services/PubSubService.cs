using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using System.Text.Json;

namespace CloudNote.Services;

public class PubSubService(IConfiguration config)
{
    private readonly string _projectId = config["GoogleCloud:ProjectId"]     ?? throw new InvalidOperationException("ProjectId missing.");
    private readonly string _topicId = config["GoogleCloud:PubSubTopic"]   ?? "cloudnote-events";
    private readonly string _subscriptionId = config["GoogleCloud:PubSubSub"]    ?? "cloudnote-events-sub";

    // Infrastructure setup

    public async Task EnsureTopicExistsAsync()
    {
        PublisherServiceApiClient client = await PublisherServiceApiClient.CreateAsync();
        TopicName topicName = TopicName.FromProjectTopic(_projectId, _topicId);
        try { await client.GetTopicAsync(topicName); }
        catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            await client.CreateTopicAsync(topicName);
        }
    }

    public async Task EnsureSubscriptionExistsAsync()
    {
        SubscriberServiceApiClient client = await SubscriberServiceApiClient.CreateAsync();
        SubscriptionName subName   = SubscriptionName.FromProjectSubscription(_projectId, _subscriptionId);
        TopicName topicName        = TopicName.FromProjectTopic(_projectId, _topicId);
        try { await client.GetSubscriptionAsync(subName); }
        catch (Grpc.Core.RpcException e) when (e.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            await client.CreateSubscriptionAsync(subName, topicName, pushConfig: null, ackDeadlineSeconds: 60);
        }
    }

    // Publishing

    public async Task PublishNoteEventAsync(string eventType, string noteId,
                                             string actorEmail, string? targetEmail = null)
    {
        PublisherClient publisher = await PublisherClient.CreateAsync(
            TopicName.FromProjectTopic(_projectId, _topicId));

        var payload = new
        {
            eventType,
            noteId,
            actorEmail,
            targetEmail,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        string json = JsonSerializer.Serialize(payload);
        PubsubMessage message = new()
        {
            Data        = ByteString.CopyFromUtf8(json),
            Attributes  = { ["eventType"] = eventType }
        };

        await publisher.PublishAsync(message);
        await publisher.ShutdownAsync(TimeSpan.FromSeconds(5));
    }

    // Pulling (background processing)

    public async Task<List<NoteEvent>> PullPendingEventsAsync(int maxMessages = 10)
    {
        SubscriberServiceApiClient client = await SubscriberServiceApiClient.CreateAsync();
        SubscriptionName subName = SubscriptionName.FromProjectSubscription(_projectId, _subscriptionId);

        PullResponse response = await client.PullAsync(subName, maxMessages: maxMessages);
        var events = new List<NoteEvent>();
        var ackIds = new List<string>();

        foreach (ReceivedMessage msg in response.ReceivedMessages)
        {
            string json = msg.Message.Data.ToStringUtf8();
            var ev = JsonSerializer.Deserialize<NoteEvent>(json);
            if (ev is not null)
            {
                events.Add(ev);
                ackIds.Add(msg.AckId);
            }
        }

        if (ackIds.Any())
            await client.AcknowledgeAsync(subName, ackIds);

        return events;
    }
}

public record NoteEvent(
    string EventType,
    string NoteId,
    string ActorEmail,
    string? TargetEmail,
    long Timestamp);
