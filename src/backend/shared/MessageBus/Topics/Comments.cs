namespace MessageBus.Topics;

public record CommentApprovedEvent : IBusEvent
{
    public Guid CommentId { get; init; }
    public Guid NewsId { get; init; }
}


public static partial class Topics
{
    public static class Comments
    {
        public const string CommentApproved = "comment-approved";
        public const string CommentApprovedGroup = "news-comment-approved-group";
    }
}