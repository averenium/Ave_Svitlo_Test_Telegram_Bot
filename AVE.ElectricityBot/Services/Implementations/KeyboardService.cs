using AVE.ElectricityBot.Services.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

namespace AVE.ElectricityBot.Services.Implementations;

/// <summary>
/// Сервіс для створення клавіатур Telegram
/// </summary>
public class KeyboardService : IKeyboardService
{
    public InlineKeyboardMarkup GetMainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚡ Поточний стан", "cmd_status"),
                InlineKeyboardButton.WithCallbackData("📋 Розклад", "cmd_schedule")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Сьогодні", "cmd_today"),
                InlineKeyboardButton.WithCallbackData("📅 Завтра", "cmd_tomorrow")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📍 Моя група", "mygroup"),
                InlineKeyboardButton.WithCallbackData("🔍 Інша група", "cmd_other")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚙️ Налаштування", "cmd_settings"),
                InlineKeyboardButton.WithCallbackData("❓ Допомога", "cmd_help")
            }
        });
    }

    public InlineKeyboardMarkup GetGroupSelectionMenu()
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Рядки з групами 1-6
        for (int group = 1; group <= 6; group++)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"Група {group}.1", $"other_{group}_1"),
                InlineKeyboardButton.WithCallbackData($"Група {group}.2", $"other_{group}_2"),
                InlineKeyboardButton.WithCallbackData($"Обидві {group}", $"other_{group}_both")
            });
        }

        // Кнопка повернення
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("« Назад", "cmd_main")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetBackToMainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
        });
    }

    public InlineKeyboardMarkup GetMyGroupSelectionMenu()
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Групи 1-6, підгрупи 1-2
        for (int group = 1; group <= 6; group++)
        {
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{group}.1", $"setgroup_{group}.1"),
                InlineKeyboardButton.WithCallbackData($"{group}.2", $"setgroup_{group}.2")
            };
            buttons.Add(row.ToArray());
        }

        // Кнопка "Назад"
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetDsoSelectionMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚡ ДТЕК", "selectdso_301")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚡ ЦЕК", "selectdso_303")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
            }
        });
    }

    public InlineKeyboardMarkup GetDsoSelectionMenuForOther()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚡ ДТЕК", "selectdso_other_301")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚡ ЦЕК", "selectdso_other_303")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
            }
        });
    }

    public InlineKeyboardMarkup GetGroupSelectionMenuForDso(int dsoId)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Групи 1-6, підгрупи 1-2
        for (int group = 1; group <= 6; group++)
        {
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{group}.1", $"setgroup_{dsoId}_{group}.1"),
                InlineKeyboardButton.WithCallbackData($"{group}.2", $"setgroup_{dsoId}_{group}.2")
            };
            buttons.Add(row.ToArray());
        }

        // Кнопка "Назад"
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️ Змінити провайдера", "mygroup")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetGroupSelectionMenuForOther(int dsoId)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Рядки з групами 1-6
        for (int group = 1; group <= 6; group++)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"Група {group}.1", $"other_{dsoId}_{group}_1"),
                InlineKeyboardButton.WithCallbackData($"Група {group}.2", $"other_{dsoId}_{group}_2"),
                InlineKeyboardButton.WithCallbackData($"Обидві {group}", $"other_{dsoId}_{group}_both")
            });
        }

        // Кнопка повернення
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️ Змінити провайдера", "cmd_other")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public InlineKeyboardMarkup GetScheduleMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Сьогодні", "cmd_today"),
                InlineKeyboardButton.WithCallbackData("📅 Завтра", "cmd_tomorrow")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔍 Інша група", "cmd_other")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("« Головне меню", "cmd_main")
            }
        });
    }

    public InlineKeyboardMarkup GetMyGroupMenuWithChange()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Змінити групу", "changegroup")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
            }
        });
    }

    public InlineKeyboardMarkup GetSettingsKeyboard(bool channelScrapingEnabled)
    {
        var toggleText = channelScrapingEnabled ? "❌ Вимкнути сповіщення" : "✅ Увімкнути сповіщення";
        var toggleCallback = channelScrapingEnabled ? "settings_disable_scraping" : "settings_enable_scraping";

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(toggleText, toggleCallback)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Головне меню", "cmd_main")
            }
        });
    }
}
