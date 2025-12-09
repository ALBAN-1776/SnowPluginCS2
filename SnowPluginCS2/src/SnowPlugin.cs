using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.IO;
using System.Text.Json;

namespace SnowPluginCS2;

public class SnowPlugin : BasePlugin, IPluginConfig<SnowConfig>
{
    public override string ModuleName => "Snow Plugin";
    public override string ModuleVersion => "1.3.0";
    public override string ModuleAuthor => "ALBAN1776";
    public override string ModuleDescription => "Creates snow particle with localization support";

    public SnowConfig Config { get; set; } = new();
    private SnowData _data = new();
    private string _dataFilePath = "";

    private readonly Dictionary<int, uint> _activeParticles = new();

    public void OnConfigParsed(SnowConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _dataFilePath = Path.Combine(ModuleDirectory, "snow_data.json");
        LoadData();

        RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
        RegisterListener<Listeners.OnClientDisconnectPost>(OnClientDisconnect);

        RegisterListener<Listeners.OnServerPrecacheResources>((manifest) =>
        {
            manifest.AddResource(Config.ParticleName);
        });
    }

    private void LoadData()
    {
        if (File.Exists(_dataFilePath))
        {
            try
            {
                var json = File.ReadAllText(_dataFilePath);
                _data = JsonSerializer.Deserialize<SnowData>(json) ?? new SnowData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Snow] Error loading data: {ex.Message}");
            }
        }
    }

    private void SaveData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Snow] Error saving data: {ex.Message}");
        }
    }

    private void OnClientConnected(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null) return;

        if (Config.CreateSnowOnConnect)
        {
            if (GetPlayerSnowState(player.SteamID))
            {
                AddTimer(0.3f, () => CreateSnow(player));
            }
        }
    }

    private void OnClientDisconnect(int slot)
    {
        RemoveSnow(slot);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        RemoveSnow(player.Slot);

        if (GetPlayerSnowState(player.SteamID))
        {
            AddTimer(0.3f, () => CreateSnow(player));
        }

        return HookResult.Continue;
    }

    [ConsoleCommand("css_snow", "Toggle snow effect")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnSnowCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        bool currentState = GetPlayerSnowState(player.SteamID);
        bool newState = !currentState;

        _data.PlayerPreferences[player.SteamID] = newState;
        SaveData();

        string message = newState ? Localizer["snow.enabled"] : Localizer["snow.disabled"];

        if (newState)
        {
            RemoveSnow(player.Slot);
            AddTimer(0.2f, () => CreateSnow(player));
        }
        else
        {
            RemoveSnow(player.Slot);
        }

        player.PrintToChat(message);
    }

    private bool GetPlayerSnowState(ulong steamId)
    {
        if (_data.PlayerPreferences.TryGetValue(steamId, out var enabled))
        {
            return enabled;
        }
        return true;
    }

    private void CreateSnow(CCSPlayerController player)
    {
        if (player?.PlayerPawn?.Value == null || !player.PlayerPawn.IsValid) return;
        var pawn = player.PlayerPawn.Value;
        if (!pawn.IsValid) return;

        RemoveSnow(player.Slot);

        var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (particle == null) return;

        particle.EffectName = Config.ParticleName;

        Vector pos = pawn.AbsOrigin!;
        QAngle ang = pawn.AbsRotation!;

        particle.Teleport(pos, ang, new Vector(0, 0, 0));
        particle.DispatchSpawn();

        Server.NextFrame(() =>
        {
            if (particle != null && particle.IsValid && pawn.IsValid)
            {
                particle.AcceptInput("SetParent", pawn, null, "!activator");
                particle.AcceptInput("Start");
            }
        });

        _activeParticles[player.Slot] = particle.Index;
    }

    private void RemoveSnow(int slot)
    {
        if (_activeParticles.TryGetValue(slot, out var entIndex))
        {
            var entity = Utilities.GetEntityFromIndex<CParticleSystem>((int)entIndex);
            if (entity != null && entity.IsValid)
            {
                entity.AcceptInput("Stop");
                entity.Remove();
            }
            _activeParticles.Remove(slot);
        }
    }
}
