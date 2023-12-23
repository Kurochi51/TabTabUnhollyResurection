using System;
using System.Numerics;
using System.Runtime.InteropServices;

using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Dalamud.Plugin;
using Dalamud.Hooking;
using Dalamud.Game.Config;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Dalamud.Interface.Windowing;
using TabTab2.Windows;
using Dalamud.Memory;

namespace TabTab2;

public sealed class Plugin : IDalamudPlugin
{
    private unsafe delegate long SelectInitialTabTarget(nint targetSystem, nint gameObjects, nint camera, nint a4);
    private unsafe delegate long SelectTabTarget(nint targetSystem, nint camera, nint gameObjects, bool inverse, char a5);

    private unsafe delegate long SelectTabTarget2(long targetSystem, long camera, nuint gameObjects, bool inverse, char a5);

    private unsafe delegate long TargetSortComparator(nint a1, nint a2);
    private unsafe delegate long TargetSortComparatorCone(float* a1, float* a2);
    private unsafe delegate long OnTabTarget(long a1, long a2, int* a3, long a4);

    [Signature("E8 ?? ?? ?? ?? 48 8B C8 48 85 C0 74 27 48 8B 00", DetourName = nameof(SelectTabTargetIgnoreDepthDetour))]
    private readonly Hook<SelectTabTarget>? tabIgnoreDepthHook = null;
    [Signature("E8 ?? ?? ?? ?? EB 4C 41 B1 01", DetourName = nameof(SelectTabTargetConeDetour))]
    private readonly Hook<SelectTabTarget2>? tabConeHook = null;
    [Signature("E8 ?? ?? ?? ?? EB 37 48 85 C9", DetourName = nameof(SelectInitialTabTargetDetour))]
    private readonly Hook<SelectInitialTabTarget>? selectInitialTabTargetHook = null;
    // 40 53 48 83 EC 20 F3 0F 10 01
    [Signature("40 53 48 83 EC 20 F3 0F 10 01 48 8B D9 F3 0F 10", DetourName = nameof(TargetSortComparatorDetour))]
    private readonly Hook<TargetSortComparator>? targetSortComparatorHook = null;
    // 41 54 41 56 41 57 B8 00
    // 41 54 41 56 41 57 B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 8B 81 ?? ?? ?? ??
    [Signature("41 54 41 56 41 57 B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 8B 81 ?? ?? ?? ??", DetourName = nameof(OnTabTargetDetour))]
    private readonly Hook<OnTabTarget>? tabTargetHook = null;

    [Signature("48 83 EC 28 F3 0F 10 01", DetourName = nameof(TargetSortComparatorConeDetour))]
    private readonly Hook<TargetSortComparatorCone>? targetSortComparatorConeHook = null;
    public WindowSystem WindowSystem { get; } = new("TabTab2");
    private const string CommandName = "/ptab";

    private readonly DalamudPluginInterface pluginInterface;
    private readonly Configuration config;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly IGameConfig gameConfig;
    private readonly IFramework framework;
    private readonly IClientState clientState;
    private readonly ITargetManager targetManager;
    private readonly IObjectTable gameObjects;
    private readonly ICondition condition;
    private readonly IPartyList partyList;


    private ConfigWindow ConfigWindow { get; init; }
    private DevWindow DevWindow { get; init; }

    public static Vector2 Resolution { get; private set; }
    private string scttLogMessage1 = "SelectCustomTabTarget not triggered", scttLogMessage2 = "SelectCustomTabTarget not triggered", scttLogMessage3 = "SelectCustomTabTarget not triggered";
    private string tscdLogMessage1 = "TargetSortComparatorDetour not triggered", tscdLogMessage2 = "TargetSortComparatorDetour not triggered", tscdLogMessage3 = "TargetSortComparatorDetour not triggered", tscdLogMessage4 = "TargetSortComparatorDetour not triggered";
    private string ottdLogMessage = "OnTabTargetDetour not triggered";
    private string sttcdMessage = "SelectTabTargetConeDetour not triggered";
    private string targetSortConeMessage = "TargetSortComparatorConeDetour not triggered";

    public Plugin(DalamudPluginInterface _pluginInterface,
        ICommandManager _commandManager,
        IPluginLog _pluginLog,
        IGameConfig _gameConfig,
        IFramework _framework,
        IClientState _clientState,
        ITargetManager _targetManager,
        IObjectTable _gameObjects,
        ICondition _condition,
        IPartyList _partyList,
        IGameInteropProvider _interopProvider)
    {
        pluginInterface = _pluginInterface;
        commandManager = _commandManager;
        log = _pluginLog;
        gameConfig = _gameConfig;
        framework = _framework;
        clientState = _clientState;
        targetManager = _targetManager;
        gameObjects = _gameObjects;
        condition = _condition;
        partyList = _partyList;

        _interopProvider.InitializeFromAttributes(this);

        tabIgnoreDepthHook?.Enable();
        tabConeHook?.Enable();
        selectInitialTabTargetHook?.Enable();
        targetSortComparatorHook?.Enable();
        targetSortComparatorConeHook?.Enable();
        tabTargetHook?.Enable();

        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ConfigWindow = new ConfigWindow(config, pluginInterface);
        DevWindow = new DevWindow();
        WindowSystem.AddWindow(DevWindow);
        WindowSystem.AddWindow(ConfigWindow);

        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        gameConfig.SystemChanged += CheckResolutionChange;
        framework.Update += OnFramework;


        // No longer needed sigs, but used for testing in dev
        // (as of 5.4 hotfix)
        // TargetSelect: E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0
        // SwitchTarget: E8 ?? ?? ?? ?? 48 39 B7 ?? ?? ?? ?? 74 6A
        // TargetSortComparator for cone mode: 48 83 EC 28 F3 0F 10 01

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the TabTab plugin configuration.",
        });

        InitializeResolution();
    }

    private unsafe void OnFramework(IFramework _framework)
    {
        DevWindowThings();
    }

    private unsafe long SelectTabTargetIgnoreDepthDetour(nint targetSystem, nint camera, nint gameObjects, bool inverse, char a5)
    {
        log.Debug("SelectTabTargetIgnoreDepthDetour triggered");
        /*log.Debug($"SelectCustomTabTarget - targetSystem: {targetSystem:X} , gameObjects:  {gameObjects:X} , camera:  {camera:X}");
        var goCount = gameObjects;
        log.Debug ($"GameObject count: {goCount}");
        for (var i = 0; i < goCount; i++)
        {
            var val = gameObjects + (8 * (i + 1));
            log.Debug ($"Obj index {i}: {val:X}");
        }*/
        //if (!config.Enabled)
        {
            return tabIgnoreDepthHook!.Original(targetSystem, camera, gameObjects, inverse, a5);
        }

        //return SelectCustomTabTarget(targetSystem, camera, (uint*)gameObjects, inverse, a5);
    }

    private unsafe long SelectTabTargetConeDetour(long targetSystem, long camera, nuint gameObjects, bool inverse, char a5)
    {
        // targetSystem seems to be the actual ITargetManager instance address
        var result = tabConeHook!.Original(targetSystem, camera, gameObjects, inverse, a5);
        if (result is 0)
        {
            return result;
        }
        var currentHp = Marshal.ReadInt32((nint)result + 444);
        var maxHP = Marshal.ReadInt32((nint)result + 448);
        var name = MemoryHelper.ReadStringNullTerminated((nint)result + 48) ?? "no";
        log.Debug("SelectTabTargetConeDetour triggered");
        sttcdMessage = $"\nresult: {result:X}" +
            $"\nHP: {currentHp}/{maxHP}" +
            $"\nName: {name}";
        //if (!config.Enabled)
        {
            return result;
        }

        //return SelectCustomTabTarget(targetSystem, camera, gameObjects, inverse, a5);
    }

    private unsafe long SelectInitialTabTargetDetour(nint targetSystem, nint gameObjects, nint camera, nint a4)
    {
        log.Debug("SelectInitialTabTargetDetour triggered");
        /*log.Debug($"SelectCustomTabTarget - targetSystem: {targetSystem:X}, gameObjects: {gameObjects:X}, camera: {camera:X}");
        var goCount = gameObjects;
        log.Debug($"GameObject count: {goCount}");
        for (var i = 0; i < goCount; i++)
        {
            var val = gameObjects + (8 * (i + 1));
            log.Debug($"Obj index {i}: {val:X}");
        }*/
        //if (!config.Enabled)
        {
            return selectInitialTabTargetHook!.Original(targetSystem, gameObjects, camera, a4);
        }
        //return SelectCustomTabTarget(targetSystem, camera, (uint*)gameObjects, (char)0, (char)1);
    }

    private unsafe long SelectCustomTabTarget(IntPtr targetSystem, IntPtr camera, IntPtr gameObjects, bool inverse, char a5)
    {
        /*log.Debug($"SelectCustomTabTarget - targetSystem: {targetSystem.ToInt64():X}, gameObjects: {gameObjects.ToInt64():X}, camera: {camera.ToInt64():X}");
        var goCount = Marshal.ReadInt64(*gameObjects);
        log.Debug($"GameObject count: {goCount}");

        for (int i = 0; i < goCount; i++)
        {
            var val = Marshal.ReadIntPtr(gameObjects + (8 * (i + 1)));
            log.Debug($"Obj index {i}: {val.ToInt64():X}");
        }*/

        return tabIgnoreDepthHook!.Original(targetSystem, camera, gameObjects, inverse, a5);
    }

    private unsafe long TargetSortComparatorConeDetour(float* a1, float* a2)
    {
        //log.Debug("TargetSortComparatorConeDetour triggered");
        var result = targetSortComparatorConeHook!.Original(a1, a2);
        targetSortConeMessage = $"Actor 1 {*a1} or {*a1:X}" +
            $"\nActor 2 {*a2} or {*a2:X}" +
            $"\nResult {result} or {result:X}";
        return result;
    }

    private unsafe long TargetSortComparatorDetour(nint a1, nint a2)
    {
        // Only triggers on controller target switch
        log.Debug("TargetSortComparatorDetour triggered");
        if (config.TargettingMode is 1 or 2)
        {
            var actorId1 = Marshal.ReadIntPtr(a1 + DevWindow.actorIdOffset);
            var actorId2 = Marshal.ReadIntPtr(a2 + DevWindow.actorIdOffset);

            var actorCurHp1 = Marshal.ReadInt32(actorId1 + 452);
            var actorMaxHp1 = Marshal.ReadInt32(actorId1 + 456);
            var actor1Hp = (float)actorCurHp1 / actorMaxHp1;

            var actorCurHp2 = Marshal.ReadInt32(actorId2 + 452);
            var actorMaxHp2 = Marshal.ReadInt32(actorId2 + 456);
            var actor2Hp = (float)actorCurHp2 / actorMaxHp2;

            // Ensure we don't call the original method until after checking actor info, otherwise we risk a race condition crash
            // The original method should still be called anyways, otherwise the allocated memory for the actor array might not be freed?
            var origResult = targetSortComparatorHook!.Original(a1, a2);

            tscdLogMessage1 = $"TargetSortComparator: comparing a1 {a1.ToInt64():X} {actorId1.ToInt64():X} to a2 {a2.ToInt64():X} {actorId2.ToInt64():X} = result of {origResult:X}";
            tscdLogMessage2 = $"Actor 1 ({actorId1.ToInt64():X}: {actorCurHp1} / {actorMaxHp1} HP / {actor1Hp}";
            tscdLogMessage3 = $"Actor 2 ({actorId2.ToInt64():X}: {actorCurHp2} / {actorMaxHp2} HP / {actor2Hp}";
            tscdLogMessage4 = $"Random stuff: {actorId1.ToInt64():X} with offset {DevWindow.actorIdOffset}" +
                $"\nRandom stuff: {actorId2.ToInt64():X} with offset {DevWindow.actorIdOffset}" +
                $"\na1: {a1:X} and a2: {a2:X}";

            if (config.Enabled && actor1Hp > actor2Hp)
            {
                return config.TargettingMode == 1 ? 1 : -1;
            }
            if (config.Enabled && actor1Hp < actor2Hp)
            {
                return config.TargettingMode == 1 ? -1 : 1;
            }
            return origResult;
        }

        return targetSortComparatorHook!.Original(a1, a2);
    }

    private unsafe long OnTabTargetDetour(long a1, long a2, int* a3, long a4)
    {
        var org = tabTargetHook!.Original(a1, a2, a3, a4);
        log.Debug("OnTabTargetDetour triggered with {yes}", org);
        ottdLogMessage = $"TabTarget - a1: {a1:X}, a2: {a2:X}, a3: {(nint)a3:X}, a4: {a4:X}";
        return org;
    }

    /// <summary>
    ///     Grab the resolution from the <see cref="Device.SwapChain"/> on plugin init, and propagate it as the <see cref="Window.WindowSizeConstraints.MaximumSize"/>
    ///     to the appropriate windows.
    /// </summary>
    private unsafe void InitializeResolution()
    {
        Resolution = new Vector2(Device.Instance()->SwapChain->Width, Device.Instance()->SwapChain->Height);
        ChangeWindowConstraints(ConfigWindow, Resolution);
        ChangeWindowConstraints(DevWindow, Resolution);
    }

    /// <summary>
    ///     Retrieve the resolution from the <see cref="Device.SwapChain"/> on resolution or screen mode changes.
    /// </summary>
    private unsafe void CheckResolutionChange(object? sender, ConfigChangeEvent e)
    {
        var configOption = e.Option.ToString();
        if (configOption is "FullScreenWidth" or "FullScreenHeight" or "ScreenWidth" or "ScreenHeight" or "ScreenMode")
        {
            Resolution = new Vector2(Device.Instance()->SwapChain->Width, Device.Instance()->SwapChain->Height);
        }
        ChangeWindowConstraints(ConfigWindow, Resolution);
        ChangeWindowConstraints(DevWindow, Resolution);
    }

    private unsafe void DevWindowThings()
    {
        DevWindow.IsOpen = true;
        DevWindow.Print("Swapchain resolution: " + Resolution.X + "x" + Resolution.Y);

        DevWindow.Print(sttcdMessage);

        DevWindow.Separator();

        DevWindow.Print(scttLogMessage1);
        DevWindow.Print(scttLogMessage2);
        DevWindow.Print(scttLogMessage3);

        DevWindow.Separator();

        DevWindow.Print(tscdLogMessage1);
        DevWindow.Print(tscdLogMessage2);
        DevWindow.Print(tscdLogMessage3);
        DevWindow.Print(tscdLogMessage4);

        DevWindow.Separator();

        DevWindow.Print(targetSortConeMessage);

        DevWindow.Separator();

        DevWindow.Print(ottdLogMessage);
        DevWindow.Print("Current offset: " + DevWindow.actorIdOffset);
        var x = clientState?.LocalPlayer?.TargetObject?.ObjectId;
        if (x is not null)
        {
            DevWindow.Print($"{x} or {x:X}");
        }
    }

    private void OnCommand(string command, string args)
    {
        ConfigWindow.Toggle();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public void DrawConfigUI()
    {
        ConfigWindow.Toggle();
    }

    public void Dispose()
    {
        tabIgnoreDepthHook?.Disable();
        tabIgnoreDepthHook?.Dispose();
        tabConeHook?.Disable();
        tabConeHook?.Dispose();
        selectInitialTabTargetHook?.Disable();
        selectInitialTabTargetHook?.Dispose();
        tabTargetHook?.Disable();
        tabTargetHook?.Dispose();
        targetSortComparatorHook?.Disable();
        targetSortComparatorHook?.Dispose();
        targetSortComparatorConeHook?.Disable();
        targetSortComparatorConeHook?.Dispose();

        commandManager.RemoveHandler(CommandName);
        WindowSystem.RemoveAllWindows();

        framework.Update -= OnFramework;
        gameConfig.SystemChanged -= CheckResolutionChange;
        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
    }

    /// <summary>
    ///     Change the <see cref="Window.SizeConstraints"/> of <paramref name="window"/> to the provided <paramref name="sizeLimit"/>.
    /// </summary>
    /// <param name="window">The desired <see cref="Window"/> to change size constraints</param>
    /// <param name="sizeLimit">The maximum size of the <paramref name="window"/></param>
    public static void ChangeWindowConstraints(Window window, Vector2 sizeLimit)
    {
        if (window.SizeConstraints.HasValue)
        {
            window.SizeConstraints = new Window.WindowSizeConstraints
            {
                MinimumSize = window.SizeConstraints.Value.MinimumSize,
                MaximumSize = sizeLimit,
            };
        }
    }
}
