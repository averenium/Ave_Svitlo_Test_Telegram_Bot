using System.Text.Json;
using System.Text.Json.Serialization;

namespace AVE.ElectricityBot.YasnoApi.Models;

/// <summary>
/// Відповідь API з плановими відключеннями для всіх груп
/// </summary>
[JsonConverter(typeof(PlannedOutagesResponseConverter))]
public class PlannedOutagesResponse
{
    /// <summary>
    /// Словник з розкладами для всіх груп та підгруп
    /// Ключ у форматі "група.підгрупа" (наприклад, "1.1", "5.2")
    /// </summary>
    public Dictionary<string, GroupOutageSchedule> Groups { get; set; } = new();

    /// <summary>
    /// Отримує розклад для конкретної групи та підгрупи
    /// </summary>
    public GroupOutageSchedule? GetSchedule(int group, int subgroup)
    {
        var key = $"{group}.{subgroup}";
        return Groups.TryGetValue(key, out var schedule) ? schedule : null;
    }

    /// <summary>
    /// Перевіряє чи є розклад для конкретної групи та підгрупи
    /// </summary>
    public bool HasSchedule(int group, int subgroup)
    {
        return Groups.ContainsKey($"{group}.{subgroup}");
    }
}

/// <summary>
/// Кастомний JSON конвертер для PlannedOutagesResponse
/// </summary>
public class PlannedOutagesResponseConverter : JsonConverter<PlannedOutagesResponse>
{
    public override PlannedOutagesResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var response = new PlannedOutagesResponse();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return response;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var propertyName = reader.GetString();
            if (string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            reader.Read();

            // Десеріалізуємо GroupOutageSchedule для кожної групи
            var schedule = JsonSerializer.Deserialize<GroupOutageSchedule>(ref reader, options);
            if (schedule != null)
            {
                response.Groups[propertyName] = schedule;
            }
        }

        throw new JsonException("Expected EndObject token");
    }

    public override void Write(Utf8JsonWriter writer, PlannedOutagesResponse value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value.Groups)
        {
            writer.WritePropertyName(kvp.Key);
            JsonSerializer.Serialize(writer, kvp.Value, options);
        }

        writer.WriteEndObject();
    }
}
