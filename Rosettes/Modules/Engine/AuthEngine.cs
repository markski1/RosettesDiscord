namespace Rosettes.Modules.Engine;

public class ApplicationAuth(int id, string name, ulong ownerId)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
    public ulong OwnerId { get; set; } = ownerId;
}

public class ApplicationRelation(int applicationId, ulong userId)
{
    public int ApplicationId { get; set; } = applicationId;
    public ulong UserId { get; set; } = userId;
}
