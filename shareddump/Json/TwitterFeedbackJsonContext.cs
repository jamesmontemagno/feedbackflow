using System.Text.Json.Serialization;
using SharedDump.Models.TwitterFeedback;

namespace SharedDump.Json
{
    [JsonSerializable(typeof(TwitterFeedbackItem))]
    [JsonSerializable(typeof(List<TwitterFeedbackItem>))]
    [JsonSerializable(typeof(TwitterFeedbackRequest))]
    [JsonSerializable(typeof(TwitterFeedbackResponse))]
    public partial class TwitterFeedbackJsonContext : JsonSerializerContext { }
}
