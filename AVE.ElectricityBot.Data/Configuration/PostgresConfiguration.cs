namespace AVE.ElectricityBot.Data.Configuration;

using Npgsql;

/// <summary>
/// Конфігурація підключення до PostgreSQL
/// </summary>
public class PostgresConfiguration
{
    /// <summary>
    /// Хост PostgreSQL сервера
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Порт PostgreSQL сервера
    /// </summary>
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Назва бази даних
    /// </summary>
    public string Database { get; set; } = "electricity_bot";

    /// <summary>
    /// Ім'я користувача
    /// </summary>
    public string Username { get; set; } = "postgres";

    /// <summary>
    /// Пароль
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Максимальний розмір пулу з'єднань
    /// </summary>
    public int MaxPoolSize { get; set; } = 50;

    /// <summary>
    /// Чи використовувати пулінг з'єднань
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Побудувати connection string
    /// Використовує NpgsqlConnectionStringBuilder для коректної ескапації та побудови
    /// </summary>
    public string GetConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            Pooling = Pooling,
            MaxPoolSize = MaxPoolSize
        };

        return builder.ConnectionString;
    }
}
