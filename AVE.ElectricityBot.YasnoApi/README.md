# AVE.ElectricityBot.YasnoApi

Бібліотека для роботи з публічним API Yasno для отримання інформації про графіки відключень електроенергії.

## Використання

### Базове використання

```csharp
using AVE.ElectricityBot.YasnoApi;
using AVE.ElectricityBot.YasnoApi.Models;

// Створення клієнта
var client = new YasnoApiClient();

// Отримання розкладу відключень
var outages = await client.GetPlannedOutagesAsync(
    regionId: 3,
    dsoId: 301
);

if (outages != null)
{
    var schedule = outages.GetSchedule(5, 1); // Група 5.1
    if (schedule != null)
    {
        Console.WriteLine($"Розклад на сьогодні:");
        foreach (var slot in schedule.Today.Slots.Where(s => s.Type == OutageSlotType.Definite))
        {
            Console.WriteLine($"  {slot.GetStartTime():hh\\:mm} - {slot.GetEndTime():hh\\:mm}");
        }
    }
}
```

### Використання з Dependency Injection

```csharp
using AVE.ElectricityBot.YasnoApi.Extensions;

// В Program.cs або Startup.cs
builder.Services.AddYasnoApiClient();

// В вашому сервісі або контролері
public class MyService
{
    private readonly IYasnoApiClient _yasnoClient;
    
    public MyService(IYasnoApiClient yasnoClient)
    {
        _yasnoClient = yasnoClient;
    }
    
    public async Task<string> GetOutageInfoAsync()
    {
        // Отримуємо розклад
        var outages = await _yasnoClient.GetPlannedOutagesAsync(3, 301);
        var schedule = outages?.GetSchedule(group.Group, group.Subgroup);
        
        return $"Група {group?.Group}.{group?.Subgroup}: {schedule?.Today.Slots.Count} слотів";
    }
}
```

## API Методи

### GetShutdownGroupAsync

Отримує інформацію про групу відключень для вказаної адреси.

```csharp
Task<YasnoGroupResponse?> GetShutdownGroupAsync(
    YasnoAddressRequest request,
    CancellationToken cancellationToken = default)

Task<YasnoGroupResponse?> GetShutdownGroupAsync(
    int regionId,
    int cityId,
    int streetId,
    int houseId,
    int dsoId,
    CancellationToken cancellationToken = default)
```

### GetPlannedOutagesAsync

Отримує планові відключення для всіх груп регіону.

```csharp
Task<PlannedOutagesResponse?> GetPlannedOutagesAsync(
    int regionId,
    int dsoId,
    CancellationToken cancellationToken = default)
```

## Моделі

### YasnoAddressRequest

```csharp
public class YasnoAddressRequest
{
    public int RegionId { get; set; }      // ID регіону
    public int CityId { get; set; }        // ID міста
    public int StreetId { get; set; }      // ID вулиці
    public int HouseId { get; set; }       // ID будинку
    public int DsoId { get; set; }         // ID оператора DSO
}
```

### YasnoGroupResponse

```csharp
public class YasnoGroupResponse
{
    public int Group { get; set; }         // Номер групи відключень
    public int Subgroup { get; set; }      // Номер підгрупи
}
```

### PlannedOutagesResponse

```csharp
public class PlannedOutagesResponse
{
    public Dictionary<string, GroupOutageSchedule> Groups { get; set; }
    
    // Отримати розклад для конкретної групи
    public GroupOutageSchedule? GetSchedule(int group, int subgroup)
}
```

### GroupOutageSchedule

```csharp
public class GroupOutageSchedule
{
    public DailyOutageSchedule Today { get; set; }
    public DailyOutageSchedule Tomorrow { get; set; }
    public DateTime UpdatedOn { get; set; }
}
```

### DailyOutageSchedule

```csharp
public class DailyOutageSchedule
{
    public List<OutageSlot> Slots { get; set; }
    public DateTime Date { get; set; }
    public OutageScheduleStatus Status { get; set; }
}
```

### OutageSlot

```csharp
public class OutageSlot
{
    public int Start { get; set; }              // Початок в хвилинах (0-1440)
    public int End { get; set; }                // Кінець в хвилинах (0-1440)
    public OutageSlotType Type { get; set; }    // Definite, NotPlanned, Maybe
    
    public TimeSpan GetStartTime()              // Конвертує в TimeSpan
    public TimeSpan GetEndTime()
    public TimeSpan Duration { get; }
}
```

### OutageSlotType (Enum)

- `Definite` - Визначене відключення
- `NotPlanned` - Відключення не заплановане
- `Maybe` - Можливе відключення

### OutageScheduleStatus (Enum)

- `ScheduleApplies` - Розклад застосовується
- `WaitingForSchedule` - Очікування розкладу
- `Unavailable` - Розклад недоступний
- `EmergencyShutdowns` - Аварійні відключення (графік не діє)
