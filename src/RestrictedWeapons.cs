using System.Collections.Concurrent;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RestrictedWeapons;

public partial class RestrictedWeapons : BasePlugin
{
    private RestrictedWeaponsConfig Config = new();

    private readonly ConcurrentDictionary<ulong, DateTime> lastMessageTimes = new();
    private readonly ConcurrentDictionary<ulong, DateTime> lastSoundTimes = new();

    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, byte>> playerWeapons = new();

    public RestrictedWeapons(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        Core.Configuration
            .InitializeWithTemplate("config.jsonc", "config.template.jsonc")
            .Configure(builder =>
            {
                builder.AddJsonFile("config.jsonc", optional: false, reloadOnChange: true);
            });

        ServiceCollection services = new();
        services.AddSwiftly(Core);
        services.AddOptionsWithValidateOnStart<RestrictedWeaponsConfig>().BindConfiguration("RestrictedWeapons");

        var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetService<IOptionsMonitor<RestrictedWeaponsConfig>>();

        if (optionsMonitor != null)
        {
            Config = optionsMonitor.CurrentValue;
            optionsMonitor.OnChange(newConfig =>
            {
                Config = newConfig;
                Core.Logger.LogInformation("[RestrictedWeapons] Конфигурация успешно обновлена.");
            });
        }

        Core.Event.OnMapLoad += OnMapLoad;

        RegisterEvents();

        Core.Logger.LogInformation("[RestrictedWeapons] Плагин успешно загружен. HotReload: {HotReload}", hotReload);
    }

    public override void Unload()
    {
        lastMessageTimes.Clear();
        lastSoundTimes.Clear();
        playerWeapons.Clear();
        Core.Logger.LogInformation("[RestrictedWeapons] Плагин выгружен.");
    }

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        playerWeapons.Clear();
        lastMessageTimes.Clear();
        lastSoundTimes.Clear();
    }

    private int GetWeaponLimit(string weaponName, int playerCount)
    {
        string cleanWeapon = NormalizeWeaponName(weaponName);
        if (string.IsNullOrEmpty(cleanWeapon)) return -1;

        string shortWeapon = cleanWeapon.StartsWith("weapon_") ? cleanWeapon.Substring(7) : cleanWeapon;

        var activeWeaponConfig = Config.weapons;
        if (activeWeaponConfig == null || activeWeaponConfig.Count == 0)
        {
            return -1;
        }

        int bestThreshold = -1;
        int resultLimit = -1;

        foreach (var entry in activeWeaponConfig)
        {
            if (int.TryParse(entry.Key, out int threshold) && playerCount >= threshold)
            {
                if (threshold > bestThreshold)
                {
                    if (entry.Value.TryGetValue(cleanWeapon, out int limit) ||
                        entry.Value.TryGetValue(shortWeapon, out limit))
                    {
                        bestThreshold = threshold;
                        resultLimit = limit;
                    }
                }
            }
        }

        return resultLimit;
    }

    private int GetPlayerCount(byte playerTeam)
    {
        var players = Core.PlayerManager.GetAllPlayers();
        int count = 0;

        foreach (var p in players)
        {
            if (p == null || !p.IsValid) continue;
            if (p.Controller == null) continue;

            if (Config.type_players == 2)
            {
                if (p.Controller.TeamNum == playerTeam) count++;
            }
            else if (!Config.spec_players)
            {
                if (p.Controller.TeamNum >= 2) count++;
            }
            else
            {
                count++;
            }
        }

        return count;
    }

    private int GetOtherPlayersWeaponCount(string cleanTargetWeapon, byte playerTeam, ulong buyerSteamId)
    {
        if (string.IsNullOrEmpty(cleanTargetWeapon)) return 0;

        int otherCount = 0;
        var allPlayers = Core.PlayerManager.GetAllPlayers();

        foreach (var player in allPlayers)
        {
            if (player == null || !player.IsValid) continue;
            if (player.SteamID == buyerSteamId) continue;
            if (player.Controller == null) continue;

            if (Config.type_weapons == 2 && player.Controller.TeamNum != playerTeam)
            {
                continue;
            }

            if (playerWeapons.TryGetValue(player.SteamID, out var weaponsDict) && weaponsDict.ContainsKey(cleanTargetWeapon))
            {
                otherCount++;
            }
        }

        return otherCount;
    }

    private string GetWeaponByDefIndex(int defIndex)
    {
        return defIndex switch
        {
            1 => "weapon_deagle",
            2 => "weapon_elite",
            3 => "weapon_fiveseven",
            4 => "weapon_glock",
            7 => "weapon_ak47",
            8 => "weapon_aug",
            9 => "weapon_awp",
            10 => "weapon_famas",
            11 => "weapon_g3sg1",
            13 => "weapon_galilar",
            14 => "weapon_m249",
            16 => "weapon_m4a1",
            17 => "weapon_mac10",
            19 => "weapon_p90",
            23 => "weapon_mp5sd",
            24 => "weapon_ump45",
            25 => "weapon_xm1014",
            26 => "weapon_bizon",
            27 => "weapon_mag7",
            28 => "weapon_negev",
            29 => "weapon_sawedoff",
            30 => "weapon_tec9",
            31 => "weapon_taser",
            32 => "weapon_hkp2000",
            33 => "weapon_mp7",
            34 => "weapon_mp9",
            35 => "weapon_nova",
            36 => "weapon_p250",
            38 => "weapon_scar20",
            39 => "weapon_sg556",
            40 => "weapon_ssg08",
            60 => "weapon_m4a1_silencer",
            61 => "weapon_usp_silencer",
            63 => "weapon_cz75a",
            64 => "weapon_revolver",
            _ => ""
        };
    }

    private string NormalizeWeaponName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return "";
        string clean = rawName.Trim().ToLowerInvariant();
        if (!clean.StartsWith("weapon_"))
        {
            clean = "weapon_" + clean;
        }
        return clean;
    }
}