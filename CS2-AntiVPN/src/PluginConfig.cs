namespace CS2_AntiVPN;

public class PluginConfig
{
    public string ApiKey { get; set; } = "";
    public bool DetectVpn { get; set; } = true;
    public List<string> BlockedCountry { get; set; } = [];
    public List<string> AllowedCountry { get; set; } = [];
    public List<string> AllowedIps { get; set; } = [];
    public string PunishCommand { get; set; } = "kickid {userid} \"VPN Detected\"";

    public string DatabaseConnection = "cs2-antivpn";
}