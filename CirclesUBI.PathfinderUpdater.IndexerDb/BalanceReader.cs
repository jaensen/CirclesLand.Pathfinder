using System.Data;
using System.Diagnostics;
using System.Numerics;
using CirclesUBI.Pathfinder.Models;
using Npgsql;

namespace CirclesUBI.PathfinderUpdater;

public class BalanceReader : IDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly string _queryString;
    private readonly Dictionary<string, uint> _addressIndexes;

    public BalanceReader(string connectionString, string queryString, Dictionary<string, uint> addressIndexes)
    {
        _connection = new NpgsqlConnection(connectionString);
        _connection.Open();

        _queryString = queryString;
        _addressIndexes = addressIndexes;
    }

    public async Task<IEnumerable<Balance>> ReadBalances(string version,
        Stopwatch? queryStopWatch = null)
    {
        queryStopWatch?.Start();

        var cmd = new NpgsqlCommand(_queryString, _connection);
        var capacityReader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);

        queryStopWatch?.Stop();

        return CreateBalanceReader(capacityReader, version);
    }

    private static BigInteger ParsePgBigInt(string str)
    {
        var decimalPointIndex = str.IndexOf(".", StringComparison.Ordinal);
        if (decimalPointIndex > -1)
        {
            str = str.Substring(0, decimalPointIndex);
        }

        if (!BigInteger.TryParse(str, out var capacityBigInteger))
        {
            throw new Exception($"Couldn't parse string {str} as BigInteger value.");
        }

        return capacityBigInteger;
    }

    private IEnumerable<Balance> CreateBalanceReader(NpgsqlDataReader capacityReader, string version)
    {
        while (true)
        {
            var end = !capacityReader.Read();
            if (end)
            {
                break;
            }

            var safeAddress = capacityReader.GetString(0).Substring(2);
            var balance = capacityReader.GetString(2);
            var balanceBn = ParsePgBigInt(balance);
            string tokenOwnerAddress = "";
            if (version == "v1")
            {
                tokenOwnerAddress = capacityReader.GetString(1).Substring(2);
            }
            else if (version == "v2")
            {
                var tokenId = capacityReader.GetString(1);
                var tokenIdBigInt = BigInteger.Parse(tokenId);

                // Convert the 160-bit 'tokenIdBigInt' to an Ethereum address
                tokenOwnerAddress = tokenIdBigInt.ToString("x").PadLeft(40, '0');
                if (tokenOwnerAddress.Length > 40)
                {
                    var startIndex = tokenOwnerAddress.Length - 40;
                    tokenOwnerAddress = tokenOwnerAddress.Substring(startIndex);
                }
            }
            else
            {
                throw new Exception("Unknown version");
            }

            if (!_addressIndexes.TryGetValue(safeAddress, out var safeAddressIdx)
                || !_addressIndexes.TryGetValue(tokenOwnerAddress, out var tokenOwnerAddressIdx))
            {
                Console.WriteLine(
                    $"Ignoring balance of holder: {safeAddress}; Token {tokenOwnerAddress}; Balance: {balanceBn};  because the holder can't be found in the _addressIndexes.");
                continue;
            }
            // else
            // {
            //      Console.WriteLine($"Holder: {safeAddress}; Token: {tokenOwnerAddress}; Balance: {balanceBn}");
            // }

            yield return new Balance(safeAddressIdx, tokenOwnerAddressIdx, balanceBn);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}