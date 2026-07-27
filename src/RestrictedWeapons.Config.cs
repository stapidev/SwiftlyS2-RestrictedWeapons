namespace RestrictedWeapons;

public class RestrictedWeaponsConfig
{
    // Звук блокировки покупки/подбора оружия (play event)
    public string block_sound { get; set; } = "sounds/ui/weapon_cant_buy";

    // Интервал в секундах между проигрыванием звука блокировки одному игроку
    public int interval_sound { get; set; } = 3;

    // Тип подсчета игроков: 1 - общее количество игроков, 2 - количество игроков в вашей команде
    public int type_players { get; set; } = 1;

    // Считать ли наблюдателей (spec) при подсчете игроков (true - да, false - нет)
    public bool spec_players { get; set; } = false;

    // Тип подсчета оружия: 1 - общее количество оружия на сервере, 2 - количество оружия в вашей команде
    public int type_weapons { get; set; } = 1;

    // Интервал в секундах между отправкой сообщений об ограничении одному игроку
    public int interval_message { get; set; } = 3;

    // Глобальные пороги ограничений оружия ("онлайн": { "название_оружия": лимит })
    // Лимит: -1 = без ограничений, 0 = запрещено совсем, N = максимум N штук
    public Dictionary<string, Dictionary<string, int>> weapons { get; set; } = new()
    {
        {
            "0", new Dictionary<string, int>
            {
                { "weapon_awp", 0 },
                { "weapon_negev", 0 }
            }
        },
        {
            "8", new Dictionary<string, int>
            {
                { "weapon_awp", 1 },
                { "weapon_negev", 1 }
            }
        },
        {
            "12", new Dictionary<string, int>
            {
                { "weapon_awp", 2 },
                { "weapon_negev", -1 }
            }
        }
    };
}