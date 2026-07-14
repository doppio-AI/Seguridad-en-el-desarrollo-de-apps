namespace VulnerableApp.Services;

public sealed class InMemoryCommentStore : ICommentStore
{
    private const int MaxComments = 50;
    private const int MaxCommentLength = 500;
    private readonly object _syncRoot = new();
    private readonly List<string> _comments = new();

    public IReadOnlyCollection<string> GetAll()
    {
        lock (_syncRoot)
        {
            return _comments.ToList();
        }
    }

    public void Add(string comment)
    {
        var normalizedComment = comment.Trim();
        if (normalizedComment.Length == 0)
        {
            return;
        }

        if (normalizedComment.Length > MaxCommentLength)
        {
            normalizedComment = normalizedComment[..MaxCommentLength];
        }

        lock (_syncRoot)
        {
            if (_comments.Count >= MaxComments)
            {
                _comments.RemoveAt(0);
            }

            _comments.Add(normalizedComment);
        }
    }
}
