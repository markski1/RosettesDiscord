namespace Rosettes.Modules.Engine;

public class ApplicationAuth
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ulong OwnerId { get; set; }

    public ApplicationAuth(int id, string name, ulong owner_id)
    {
        Id = id;
        Name = name;
        OwnerId = owner_id;
    }
}

public class ApplicationRelation
{
    public int ApplicationId { get; set; }
    public ulong UserId { get; set; }

    public ApplicationRelation(int application_id, ulong user_id)
    {
        ApplicationId = application_id;
        UserId = user_id;
    }
}
