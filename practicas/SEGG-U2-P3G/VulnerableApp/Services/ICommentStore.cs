namespace VulnerableApp.Services;

public interface ICommentStore
{
    IReadOnlyCollection<string> GetAll();
    void Add(string comment);
}
