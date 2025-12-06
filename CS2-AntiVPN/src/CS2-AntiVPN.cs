using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using System.Text.Json;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CS2_AntiVPN;

[PluginMetadata(Id = "CS2_AntiVPN", Version = "1.0.0", Name = "CS2-AntiVPN", Author = "daffyy",
    Description = "Detect & punish vpn usage")]
public partial class CS2_AntiVPN : BasePlugin
{
	private readonly PluginConfig _config = null!;
    private readonly Helper _helper = null!;

    public CS2_AntiVPN(ISwiftlyCore core) : base(core)
    {
        const string configFileName = "config.jsonc";
        const string configSection = "CS2-AntiVPN";

        Core.Configuration
            .InitializeJsonWithModel<PluginConfig>(configFileName, configSection)
            .Configure(builder => {
                builder.AddJsonFile(Core.Configuration.GetConfigPath(configFileName), optional: false, reloadOnChange: true);
            });

        ServiceCollection services = new();
        services.AddSwiftly(Core).AddOptionsWithValidateOnStart<PluginConfig>().BindConfiguration(configSection);
        services.AddSingleton<Helper>();

        var provider = services.BuildServiceProvider();
        _config = provider.GetRequiredService<IOptions<PluginConfig>>().Value;
        _helper = provider.GetRequiredService<Helper>();
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
	    _ = CreateTable();
	    
        if (hotReload)
        {
	        var players = Core.PlayerManager.GetAllPlayers()
		        .Where(p => !p.IsFakeClient && p.Controller.Connected == PlayerConnectedState.PlayerConnected)
		        .ToList();

	        foreach (var player in players)
	        {
		        var ipAddress = player.IPAddress.Split(":")[0];
		        if (string.IsNullOrEmpty(ipAddress) || _helper.IsIpAllowed(ipAddress)) continue;

		        _ = VpnAction(ipAddress, player);
	        }
        }
    }

    public override void Unload()
    {
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult EventPlayerConnectFull(EventPlayerConnectFull @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || player.IsFakeClient)
            return HookResult.Continue;

        var ipAddress = player.IPAddress.Split(":")[0];
        if (_helper.IsIpAllowed(ipAddress))
            return HookResult.Continue;

        Task.Run(async () =>
        {
            await VpnAction(ipAddress, player);
        });
        
        return HookResult.Continue;
    }

	private async Task VpnAction(string ipAddress, IPlayer player)
	{
        var (exists, isUsingVpn, countryCode) = await IsIpInDatabase(ipAddress);
		var punishCommand = _helper.GetPunishCommand(player);
		
		if (exists)
		{
			if (_config.DetectVpn && isUsingVpn || _config.BlockedCountry.Any(country =>
				                                   country.Equals(countryCode,
					                                   StringComparison.OrdinalIgnoreCase))

			                                   || _config.AllowedCountry.Count > 0 &&
			                                   !_config.AllowedCountry.Any(country =>
				                                   country.Equals(countryCode,
					                                   StringComparison.OrdinalIgnoreCase))
			   )
			{
				Core.Scheduler.NextWorldUpdate(() =>
				{
					if (player is not { IsValid: true })
						return;
					
					Core.Engine.ExecuteCommand(punishCommand);
				});
			}
		}
		else
		{
			var info = await CheckVpn(ipAddress);
			if (_config.DetectVpn && info.status || _config.BlockedCountry.Any(country =>
				                                    country.Equals(info.countryCode,
					                                    StringComparison.OrdinalIgnoreCase))
			                                    || _config.AllowedCountry.Count > 0 &&
			                                    !_config.AllowedCountry.Any(country =>
				                                    country.Equals(info.countryCode,
					                                    StringComparison.OrdinalIgnoreCase))
			   )
			{
				Core.Scheduler.NextWorldUpdate(() =>
				{
					if (player is not { IsValid: true })
						return;

					Core.Engine.ExecuteCommand(punishCommand);
				});
			}
		
			_ = SaveIpToDatabase(ipAddress, info.status, info.countryCode);
		}
	}
	
	private async Task<(bool status, string countryCode)> CheckVpn(string ipAddress)
	{
		using var client = new HttpClient();
		var url = string.IsNullOrEmpty(_config.ApiKey) ? $"https://proxycheck.io/v2/{ipAddress}?vpn=2&asn=1" : $"http://proxycheck.io/v2/{ipAddress}?key={_config.ApiKey}&vpn=2&asn=1";

		try
		{
			var response = await client.GetAsync(url);
			var responseBody = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				using var jsonDoc = JsonDocument.Parse(responseBody);
				var root = jsonDoc.RootElement;

				if (root.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "ok")
				{
					if (root.TryGetProperty(ipAddress, out var ipProp))
					{
						var isProxy = ipProp.TryGetProperty("proxy", out var proxyProp) &&
									proxyProp.GetString() == "yes";

						var countryCode = ipProp.TryGetProperty("isocode", out var isoProp)
							? isoProp.GetString() ?? "Unknown"
							: "Unknown";

						return (isProxy, countryCode);
					}
				}
			}
		}
		catch (Exception)
		{
			Core.Logger.LogError($"Unable to fetch ip `{ipAddress} info!");
		}

		return (false, "Unknown");
	}
}