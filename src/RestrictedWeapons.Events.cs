using System.Collections.Concurrent;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.Logging;

namespace RestrictedWeapons;

public partial class RestrictedWeapons
{
    private void RegisterEvents()
    {
#pragma warning disable CS0618 
        Core.Event.OnItemServicesCanAcquireHook += (@event) =>
        {
            try
            {
                var pawn = @event.ItemServices?.Pawn;
                if (pawn == null) return;

                var controller = pawn.Controller.Value;
                if (controller == null) return;

                var player = controller.ToPlayer();
                if (player == null || !player.IsValid) return;

                string weaponName = "";

                if (@event.EconItemView != null)
                {
                    int defIndex = (int)@event.EconItemView.ItemDefinitionIndex;
                    weaponName = GetWeaponByDefIndex(defIndex);
                }

                if (string.IsNullOrWhiteSpace(weaponName) && @event.WeaponVData != null)
                {
                    weaponName = @event.WeaponVData.Name.ToString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(weaponName)) return;

                weaponName = NormalizeWeaponName(weaponName);

                byte team = player.Controller?.TeamNum ?? 0;
                int playerCount = GetPlayerCount(team);

                int limit = GetWeaponLimit(weaponName, playerCount);
                if (limit == -1) return;

                int otherCount = GetOtherPlayersWeaponCount(weaponName, team, player.SteamID);

                if (limit == 0 || otherCount >= limit)
                {
                    @event.SetAcquireResult(AcquireResult.NotAllowedByMode);

                    PlayBlockSound(player);
                    NotifyPlayerBlocked(player, weaponName, limit);
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка в OnItemServicesCanAcquireHook.");
            }
        };
#pragma warning restore CS0618

        Core.GameEvent.HookPre<EventItemPickup>((@event) =>
        {
            try
            {
                var player = @event.UserIdPlayer ?? Core.PlayerManager.GetPlayer(@event.UserId);
                if (player == null || !player.IsValid) return HookResult.Continue;

                string weaponName = NormalizeWeaponName(@event.Item);
                if (string.IsNullOrWhiteSpace(weaponName)) return HookResult.Continue;

                byte team = player.Controller?.TeamNum ?? 0;
                int playerCount = GetPlayerCount(team);

                int limit = GetWeaponLimit(weaponName, playerCount);
                if (limit == -1) return HookResult.Continue;

                int otherCount = GetOtherPlayersWeaponCount(weaponName, team, player.SteamID);

                if (limit == 0 || otherCount >= limit)
                {
                    PlayBlockSound(player);
                    NotifyPlayerBlocked(player, weaponName, limit);

                    return HookResult.Stop;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка в HookPre EventItemPickup.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPre<EventItemPurchase>((@event) =>
        {
            try
            {
                var player = @event.UserIdPlayer ?? Core.PlayerManager.GetPlayer(@event.UserId);
                if (player == null || !player.IsValid) return HookResult.Continue;

                string weaponName = NormalizeWeaponName(@event.Weapon);
                if (string.IsNullOrWhiteSpace(weaponName)) return HookResult.Continue;

                byte team = player.Controller?.TeamNum ?? 0;
                int playerCount = GetPlayerCount(team);

                int limit = GetWeaponLimit(weaponName, playerCount);
                if (limit == -1) return HookResult.Continue;

                int otherCount = GetOtherPlayersWeaponCount(weaponName, team, player.SteamID);

                if (limit == 0 || otherCount >= limit)
                {
                    PlayBlockSound(player);
                    NotifyPlayerBlocked(player, weaponName, limit);

                    return HookResult.Stop;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка в HookPre EventItemPurchase.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventItemEquip>((@event) =>
        {
            try
            {
                var player = @event.UserIdPlayer ?? Core.PlayerManager.GetPlayer(@event.UserId);
                if (player != null && player.IsValid && !string.IsNullOrWhiteSpace(@event.Item))
                {
                    string cleanWeapon = NormalizeWeaponName(@event.Item);
                    var dict = playerWeapons.GetOrAdd(player.SteamID, _ => new ConcurrentDictionary<string, byte>());
                    dict[cleanWeapon] = 1;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка при обработке EventItemEquip.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventItemRemove>((@event) =>
        {
            try
            {
                var player = @event.UserIdPlayer ?? Core.PlayerManager.GetPlayer(@event.UserId);
                if (player != null && player.IsValid && !string.IsNullOrWhiteSpace(@event.Item))
                {
                    string cleanWeapon = NormalizeWeaponName(@event.Item);
                    if (playerWeapons.TryGetValue(player.SteamID, out var dict))
                    {
                        dict.TryRemove(cleanWeapon, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка при обработке EventItemRemove.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventPlayerDeath>((@event) =>
        {
            try
            {
                var victim = @event.UserIdPlayer ?? Core.PlayerManager.GetPlayer(@event.UserId);
                if (victim != null)
                {
                    playerWeapons.TryRemove(victim.SteamID, out _);
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка при обработке EventPlayerDeath.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPost<EventRoundStart>((@event) =>
        {
            try
            {
                playerWeapons.Clear();
                lastMessageTimes.Clear();
                lastSoundTimes.Clear();
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка при обработке EventRoundStart.");
            }

            return HookResult.Continue;
        });

        Core.GameEvent.HookPre<EventPlayerDisconnect>((@event) =>
        {
            try
            {
                var player = Core.PlayerManager.GetPlayer(@event.PlayerID);
                if (player != null)
                {
                    playerWeapons.TryRemove(player.SteamID, out _);
                    lastMessageTimes.TryRemove(player.SteamID, out _);
                    lastSoundTimes.TryRemove(player.SteamID, out _);
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "Ошибка при обработке EventPlayerDisconnect.");
            }

            return HookResult.Continue;
        });
    }

    private void PlayBlockSound(IPlayer player)
    {
        if (string.IsNullOrWhiteSpace(Config.block_sound)) return;

        ulong steamId = player.SteamID;
        DateTime now = DateTime.UtcNow;

        if (lastSoundTimes.TryGetValue(steamId, out DateTime lastTime))
        {
            if ((now - lastTime).TotalSeconds < Config.interval_sound)
            {
                return;
            }
        }

        lastSoundTimes[steamId] = now;
        player.ExecuteCommand($"play {Config.block_sound}");
    }

    private void NotifyPlayerBlocked(IPlayer player, string weaponName, int limit)
    {
        if (player.IsFakeClient)
        {
            return;
        }

        ulong steamId = player.SteamID;
        DateTime now = DateTime.UtcNow;

        if (lastMessageTimes.TryGetValue(steamId, out DateTime lastTime))
        {
            if ((now - lastTime).TotalSeconds < Config.interval_message)
            {
                return;
            }
        }

        lastMessageTimes[steamId] = now;

        string cleanWeapon = NormalizeWeaponName(weaponName).Replace("weapon_", "").ToUpperInvariant();

        if (Config.type_weapons == 2)
        {
            player.SendChat($"{Core.Localizer["restrictedweapons.prefix"]} {Core.Localizer["restrictedweapons.blocked_team", cleanWeapon, limit]}");
        }
        else
        {
            player.SendChat($"{Core.Localizer["restrictedweapons.prefix"]} {Core.Localizer["restrictedweapons.blocked", cleanWeapon, limit]}");
        }
    }
}