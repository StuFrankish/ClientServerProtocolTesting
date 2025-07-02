using WorldServerHost;

namespace WorldServerHost;

public partial class Form1 : Form
{
    private readonly WorldServerMonitorService _monitorService;

    public Form1()
    {
        InitializeComponent();


        _monitorService = new WorldServerMonitorService("192.168.11.215", 15002);

        _monitorService.OnMessageReceived += async message =>
        {
            if (InvokeRequired)
            {
                Invoke(new Action(async () => await HandleMessageAsync(message)));
            }
            else
            {
                await HandleMessageAsync(message);
            }
        };

    }

    private void Form1_Load(object sender, EventArgs e)
    {
        //_ = _monitorService.ConnectAsync();
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        _monitorService.Disconnect();
    }

    private async Task HandleMessageAsync(string message)
    {
        // Query active players from the world server
        var activePlayers = await _monitorService.QueryActivePlayersAsync();

        // Display the list of active players
        var playerNames = string.Join(", ", activePlayers.Select(p => p.UserId));
        MessageBox.Show($"Active Players: {playerNames}", "World Server Players", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void button1_Click(object sender, EventArgs e)
    {
        await _monitorService.ConnectAsync();
    }

    private void button2_Click(object sender, EventArgs e)
    {
        _monitorService.Disconnect();
    }
}
