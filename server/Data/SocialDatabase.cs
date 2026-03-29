using Npgsql;

namespace inplayed.Server.Data;

internal sealed class SocialDatabase
{
	private readonly string _connectionString;

	public SocialDatabase(IConfiguration configuration)
	{
		_connectionString = configuration.GetConnectionString("SocialDatabase") ?? string.Empty;
	}

	public string ConnectionString => _connectionString;

	public async Task EnsureCreatedAsync()
	{
		await using var connection = CreateConnection();
		await connection.OpenAsync();

		if (await TableExistsAsync(connection, "users"))
		{
			return;
		}

		var schemaPath = Path.Combine(AppContext.BaseDirectory, "sql", "schema.sql");
		var sql = await File.ReadAllTextAsync(schemaPath);

		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	public NpgsqlConnection CreateConnection()
	{
		return new NpgsqlConnection(_connectionString);
	}

	private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string tableName)
	{
		await using var command = new NpgsqlCommand(
			"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @tableName",
			connection);
		command.Parameters.AddWithValue("tableName", tableName);

		var count = Convert.ToInt32(await command.ExecuteScalarAsync());
		return count > 0;
	}
}
