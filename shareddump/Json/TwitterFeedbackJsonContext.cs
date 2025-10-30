using System.Text.Json.Serialization;
using SharedDump.Models.TwitterFeedback;

namespace SharedDump.Json
{
    [JsonSerializable(typeof(TwitterFeedbackItem))]
    [JsonSerializable(typeof(List<TwitterFeedbackItem>))]
    [JsonSerializable(typeof(TwitterFeedbackRequest))]
    [JsonSerializable(typeof(TwitterFeedbackResponse))]
    [JsonSerializable(typeof(TwitterSearchResponse))]
    [JsonSerializable(typeof(TwitterSearchTweet))]
    [JsonSerializable(typeof(TwitterSearchUser))]
    public partial class TwitterFeedbackJsonContext : JsonSerializerContext { }
}
