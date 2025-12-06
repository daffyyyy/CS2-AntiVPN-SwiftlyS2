using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CS2_AntiVPN;

public partial class CS2_AntiVPN
{
    private async Task<(bool exists, bool isUsingVPN, string countryCode)> IsIpInDatabase(string ipAddress)
    {
        try
        {
            IDbConnection connection = Core.Database.GetConnection(_config.DatabaseConnection);
            const string sql = "SELECT `status`, `countryCode` FROM `vpn_check` WHERE address = @ipAddress";
            var result = await connection.QuerySingleOrDefaultAsync(sql, new { ipAddress });

            return result == null
                ? (false, false, "Unknown")
                : ((bool exists, bool isUsingVPN, string countryCode))(true, result.status != 0, result.countryCode);
        }
        catch (Exception e)
        {
            Core.Logger.LogError(e.Message);
        }

        return (true, false, "Unknown");
    }

    private async Task SaveIpToDatabase(string ipAddress, bool isUsingVpn, string countryCode = "Unknown")
    {

        try
        {
            IDbConnection connection = Core.Database.GetConnection(_config.DatabaseConnection);
            const string sql =
                "INSERT INTO `vpn_check` (`address`, `status`, `countryCode`) VALUES (@ipAddress, @status, @countryCode)";
            await connection.ExecuteAsync(sql, new { ipAddress, status = isUsingVpn ? 1 : 0, countryCode });
        }
        catch (Exception e)
        {
            Core.Logger.LogError(e.Message);
        }
    }

    public async Task CreateTable()
    {
        try
        {
            IDbConnection connection = Core.Database.GetConnection(_config.DatabaseConnection);
            const string sql = """
                               CREATE TABLE IF NOT EXISTS `vpn_check` (
                                        `address` varchar(128) NOT NULL,
                                        `status` int(1) NOT NULL DEFAULT 0,
                                        `countryCode` varchar(64) NOT NULL,
                                        `timestamp` TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                        UNIQUE KEY `address` (`address`)
                                       ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
                               """;
            await connection.ExecuteAsync(sql);
        }
        catch (Exception e)
        {
            Core.Logger.LogError(e.Message);
        }
    }
}
