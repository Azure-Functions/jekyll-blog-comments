using System;

namespace BlogAzureFunctions
{
    public class NewComment
    {
        public NewComment(string postId, string message, string author, string email = "", string url = null)
        {
            Id = Helpers.GetRandomUInt64(); // TODO: Some sequential numbering?
            PostId = postId.Trim();
            Message = message.Trim();
            Author = author.Trim();
            Email = email.Trim();
            if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
                Url = parsedUrl;
            When = DateTime.UtcNow;
        }

        public UInt64 Id { get; }
        public string PostId { get; }
        public string Message { get; }
        public string Author { get; }
        public string Email { get; }
        public Uri Url { get; }
        public DateTime When { get; }
    }
}
