using Shared;
using Spectre.Console;
using System.Net.Sockets;
using TestClient;
using static Shared.Enums;

class Program
{
    static async Task Main()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.IsCancellationRequested)
        {
            // Clear and display a fancy header on first launch
            AnsiConsole.Clear();
            AnsiConsole.Write(
                new FigletText("Test Client")
                    .Centered()
                    .Color(Color.Cyan1)
            );
            AnsiConsole.Write(
                new Rule("[yellow]Main Menu[/]")
                    .RuleStyle("grey")
                    .Centered()
            );
            {
                // === Main Menu ===
                var mainChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[green]Options[/]")
                        .AddChoices("Login", "Exit")
                );
                if (mainChoice == "Exit")
                {
                    AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                    return;
                }

                // === Login Flow ===
                var host = AnsiConsole.Prompt(
                    new TextPrompt<string>("Login server host:")
                        .DefaultValue("localhost")
                        .ShowDefaultValue()
                );

                var user = AnsiConsole.Ask<string>("Username:");
                var pass = AnsiConsole.Prompt(
                    new TextPrompt<string>("Password:")
                        .PromptStyle("red")
                        .Secret()
                );

                TcpClient loginClient = default!;
                NetworkStream loginStream = default!;

                try
                {
                    loginClient = new TcpClient();
                    await loginClient.ConnectAsync(host, 14002, cts.Token);
                    loginStream = loginClient.GetStream();

                    // Show a spinner while logging in
                    bool loginOk = await ClientService.LoginAsync(loginStream, user, pass, cts.Token);

                    if (!loginOk)
                    {
                        AnsiConsole.MarkupLine("[red]Login failed.[/]");
                        continue;  // back to Main Menu
                    }

                    AnsiConsole.MarkupLine("[green]Login successful![/]");

                    // === Realm-List Loop ===
                    bool loggedIn = true;
                    while (loggedIn && !cts.IsCancellationRequested)
                    {
                        AnsiConsole.Write(new Rule("World List").Centered().RuleStyle("grey"));

                        // 1) FETCH REALMS
                        var realms = await ClientService.RequestRealmListAsync(loginStream, cts.Token);

                        // 2) DISPLAY REALMS
                        var table = new Table()
                            .AddColumn("ID")
                            .AddColumn("Name")
                            .AddColumn("Address")
                            .AddColumn("State")
                            .AddColumn("Capacity");

                        foreach (var r in realms)
                        {
                            table.AddRow(
                                r.Id.ToString(),
                                r.Name,
                                $"{r.IP}:{r.Port}",
                                r.State.ToString(),
                                r.CurrentUsers + "/" + r.MaxUsers
                            );
                        }

                        table.MarkdownBorder();

                        AnsiConsole.Write(table);

                        // 3) REALM MENU
                        var realmChoice = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("[green]Realm Menu[/]")
                                .AddChoices("Connect to world", "Logout")
                        );
                        if (realmChoice == "Logout")
                        {
                            AnsiConsole.MarkupLine("[yellow]Logged out.[/]");
                            break;   // break realm-list loop; dispose login below
                        }

                        // 4) SELECT REALM ID (with retry and logout)
                        byte selId;
                        WorldInfo selectedRealm = default!;

                        while (true)
                        {
                            selId = AnsiConsole.Ask<byte>("Select world ID (or 0 to logout):");
                            if (selId == 0) { loggedIn = false; break; }

                            selectedRealm = realms.FirstOrDefault(x => x.Id == selId);

                            if (selectedRealm == null)
                                AnsiConsole.MarkupLine("[red]Invalid realm ID. Please try again.[/]");
                            else if (selectedRealm.State == WorldState.Offline)
                                AnsiConsole.MarkupLine("[red]Selected realm is offline. Please choose another.[/]");
                            else break;
                        }

                        if (!loggedIn)
                            break;

                        // 5) CONNECT TO SELECTED WORLD
                        TcpClient worldClient = null;
                        try
                        {
                            worldClient = await ClientService.ConnectWorldAsync(selectedRealm, cts.Token);

                            // === World-Menu Loop ===
                            bool inWorld = true;
                            while (inWorld && !cts.IsCancellationRequested)
                            {
                                var worldChoice = AnsiConsole.Prompt(
                                    new SelectionPrompt<string>()
                                        .Title("[green]World Menu[/]")
                                        .AddChoices("Ping", "Set State", "Disconnect")
                                );

                                switch (worldChoice)
                                {
                                    case "Ping":
                                        var pong = await ClientService.PingAsync(worldClient, cts.Token);
                                        AnsiConsole.MarkupLine(
                                            pong ? "[green]Pong![/]" : "[red]No response.[/]"
                                        );
                                        break;

                                    case "Set State":
                                        var stateChoice = AnsiConsole.Prompt(
                                            new SelectionPrompt<string>()
                                                .Title("[yellow]Select new state[/]")
                                                .AddChoices("Available", "Closed")
                                        );
                                        var newState = stateChoice == "Available"
                                            ? WorldState.Available
                                            : WorldState.Closed;

                                        await ClientService.SetWorldStateAsync(worldClient, newState, cts.Token);
                                        AnsiConsole.MarkupLine($"[green]State change request sent: {newState}[/]");
                                        break;

                                    case "Disconnect":
                                        await ClientService.DisconnectWorldAsync(worldClient, cts.Token);
                                        AnsiConsole.MarkupLine("[yellow]Disconnected from world.[/]");
                                        inWorld = false;
                                        loggedIn = false;
                                        break;
                                }
                            }
                        }
                        catch (SocketException)
                        {
                            AnsiConsole.MarkupLine("[yellow]Connection to world was closed by server.[/]");
                            loggedIn = false;
                        }
                        finally
                        {
                            if (worldClient != null) try { worldClient.Close(); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                }
                finally
                {
                    if (loginStream != null) try { loginStream.Close(); } catch { }
                    if (loginClient != null) try { loginClient.Close(); } catch { }
                }
            }
        }
    }
}
