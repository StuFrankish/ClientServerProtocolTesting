using Shared.Persistence.Entities;

namespace Shared.DataSeeding;
public static class TestUsers
{
    public static readonly IList<UserAccount> Users =
    [
        new UserAccount("alice", "alice"),
        new UserAccount("bob", "bob"),
        new UserAccount("charlie", "charlie")
    ];
}
