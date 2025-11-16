using System.Text.Json;
using AVE.ElectricityBot.YasnoApi.Models;

namespace AVE.ElectricityBot.YasnoApi.Tests;

/// <summary>
/// Приклад для тестування десеріалізації PlannedOutagesResponse
/// </summary>
public static class PlannedOutagesResponseTest
{
    /// <summary>
    /// Тестовий JSON для перевірки десеріалізації
    /// </summary>
    private const string SampleJson = @"{
  ""1.1"": {
    ""today"": {
      ""slots"": [
        {
          ""start"": 0,
          ""end"": 210,
          ""type"": ""Definite""
        },
        {
          ""start"": 210,
          ""end"": 630,
          ""type"": ""NotPlanned""
        }
      ],
      ""date"": ""2025-11-09T00:00:00+02:00"",
      ""status"": ""ScheduleApplies""
    },
    ""tomorrow"": {
      ""slots"": [
        {
          ""start"": 0,
          ""end"": 210,
          ""type"": ""Definite""
        }
      ],
      ""date"": ""2025-11-10T00:00:00+02:00"",
      ""status"": ""WaitingForSchedule""
    },
    ""updatedOn"": ""2025-11-09T15:01:16+00:00""
  },
  ""5.1"": {
    ""today"": {
      ""slots"": [
        {
          ""start"": 0,
          ""end"": 420,
          ""type"": ""NotPlanned""
        }
      ],
      ""date"": ""2025-11-09T00:00:00+02:00"",
      ""status"": ""ScheduleApplies""
    },
    ""tomorrow"": {
      ""slots"": [],
      ""date"": ""2025-11-10T00:00:00+02:00"",
      ""status"": ""WaitingForSchedule""
    },
    ""updatedOn"": ""2025-11-09T15:01:16+00:00""
  }
}";

    /// <summary>
    /// Тестує десеріалізацію PlannedOutagesResponse
    /// </summary>
    public static bool TestDeserialization()
    {
        try
        {
            var result = JsonSerializer.Deserialize<PlannedOutagesResponse>(SampleJson);
            
            if (result == null)
            {
                Console.WriteLine("? Десеріалізація повернула null");
                return false;
            }

            Console.WriteLine($"? Десеріалізовано {result.Groups.Count} груп");

            // Перевірка групи 1.1
            var group1_1 = result.GetSchedule(1, 1);
            if (group1_1 == null)
            {
                Console.WriteLine("? Група 1.1 не знайдена");
                return false;
            }

            Console.WriteLine($"? Група 1.1 знайдена:");
            Console.WriteLine($"   - Слотів сьогодні: {group1_1.Today.Slots.Count}");
            Console.WriteLine($"   - Слотів завтра: {group1_1.Tomorrow.Slots.Count}");
            Console.WriteLine($"   - Статус сьогодні: {group1_1.Today.Status}");
            Console.WriteLine($"   - Оновлено: {group1_1.UpdatedOn}");

            // Перевірка групи 5.1
            var group5_1 = result.GetSchedule(5, 1);
            if (group5_1 == null)
            {
                Console.WriteLine("? Група 5.1 не знайдена");
                return false;
            }

            Console.WriteLine($"? Група 5.1 знайдена:");
            Console.WriteLine($"   - Слотів сьогодні: {group5_1.Today.Slots.Count}");

            // Перевірка типів слотів
            var firstSlot = group1_1.Today.Slots.FirstOrDefault();
            if (firstSlot != null)
            {
                Console.WriteLine($"? Перший слот групи 1.1:");
                Console.WriteLine($"   - Початок: {firstSlot.Start} хв");
                Console.WriteLine($"   - Кінець: {firstSlot.End} хв");
                Console.WriteLine($"   - Тип: {firstSlot.Type}");
                Console.WriteLine($"   - Час початку: {firstSlot.GetStartTime()}");
                Console.WriteLine($"   - Час кінця: {firstSlot.GetEndTime()}");
                Console.WriteLine($"   - Тривалість: {firstSlot.Duration}");
            }

            Console.WriteLine("\n? Всі тести пройдені успішно!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Помилка при десеріалізації: {ex.Message}");
            Console.WriteLine($"   Тип: {ex.GetType().Name}");
            Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Тестує серіалізацію PlannedOutagesResponse
    /// </summary>
    public static bool TestSerialization()
    {
        try
        {
            var response = new PlannedOutagesResponse();
            
            response.Groups["1.1"] = new GroupOutageSchedule
            {
                Today = new DailyOutageSchedule
                {
                    Date = DateTime.Now,
                    Status = OutageScheduleStatus.ScheduleApplies,
                    Slots = new List<OutageSlot>
                    {
                        new() { Start = 0, End = 210, Type = OutageSlotType.Definite }
                    }
                },
                Tomorrow = new DailyOutageSchedule
                {
                    Date = DateTime.Now.AddDays(1),
                    Status = OutageScheduleStatus.WaitingForSchedule,
                    Slots = new List<OutageSlot>()
                },
                UpdatedOn = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
            
            Console.WriteLine("? Серіалізація успішна:");
            Console.WriteLine(json);
            
            // Перевірка що можна десеріалізувати назад
            var deserialized = JsonSerializer.Deserialize<PlannedOutagesResponse>(json);
            if (deserialized == null || !deserialized.HasSchedule(1, 1))
            {
                Console.WriteLine("? Не вдалося десеріалізувати назад");
                return false;
            }

            Console.WriteLine("? Зворотня десеріалізація успішна");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Помилка при серіалізації: {ex.Message}");
            return false;
        }
    }
}
