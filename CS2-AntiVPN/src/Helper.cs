using System.Net;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared.Players;

namespace CS2_AntiVPN;

public class Helper
{
    private readonly PluginConfig _config;

    public Helper(IOptions<PluginConfig> options)
    {
        _config = options.Value;
    }

    public bool IsIpAllowed(string ip)
    {
        foreach (var rule in _config.AllowedIps)
        {
            if (rule.Contains("/"))
            {
                if (IsInCidrRange(ip, rule)) return true;
            }
            else if (rule.Contains("*"))
            {
                if (IsWildCardMatch(ip, rule)) return true;
            }
            else
            {
                if (rule == ip) return true;
            }
        }

        return false;
    }

    private bool IsWildCardMatch(string ip, string pattern)
    {
        var ipParts = ip.Split('.');
        var patternParts = pattern.Split('.');

        if (ipParts.Length != 4 || patternParts.Length != 4) return false;
        for (int i = 0; i < 4; i++)
        {
            if (patternParts[i] == "*") continue;
            if (ipParts[i] != patternParts[i]) return false;
        }

        return true;
    }

    private bool IsInCidrRange(string ip, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            var baseIp = IPAddress.Parse(parts[0]);
            int prefixLength = int.Parse(parts[1]);

            var ipBytes = IPAddress.Parse(ip).GetAddressBytes();
            var baseIpBytes = baseIp.GetAddressBytes();

            if (ipBytes.Length != baseIpBytes.Length) return false;

            int bitsToCompare = prefixLength;
            for (int i = 0; i < ipBytes.Length && bitsToCompare > 0; i++)
            {
                int bits = Math.Min(8, bitsToCompare);
                int mask = 0xFF << (8 - bits);

                if ((ipBytes[i] & mask) != (baseIpBytes[i] & mask))
                    return false;

                bitsToCompare -= bits;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public string GetPunishCommand(IPlayer player)
    {
        var punishCommand = _config.PunishCommand.Replace("{userid}", player.PlayerID.ToString())
            .Replace("{steamid}", player.SteamID.ToString())
            .Replace("{name}", player.Controller.PlayerName);
		
        return punishCommand;
    }

}