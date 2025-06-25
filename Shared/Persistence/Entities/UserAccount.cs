namespace Shared.Persistence.Entities;
public class UserAccount
{
    public UserAccount() { }

    public UserAccount(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
