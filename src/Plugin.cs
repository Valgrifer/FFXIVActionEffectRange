global using static ActionEffectRange.Game;
global using static ActionEffectRange.Plugin;
global using ActionEffectRange.Utils;
global using System;
global using System.Numerics;

using ActionEffectRange.Actions;
using ActionEffectRange.Actions.Data;
using ActionEffectRange.Drawing;
using ActionEffectRange.Helpers;
using ActionEffectRange.UI;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using KamiToolKit;

namespace ActionEffectRange
{
    public class Plugin : IDalamudPlugin
    {
        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService]
                internal static IGameInteropProvider InteropProvider { get; private set; } = null!;
        [PluginService]
        internal static IFramework Framework { get; private set; } = null!;
        [PluginService]
        internal static IClientState ClientState { get; private set; } = null!;
        [PluginService]
        internal static IObjectTable ObjectTable { get; private set; } = null!;
        [PluginService]
        internal static IBuddyList BuddyList { get; private set; } = null!;
        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;
        

        public string Name => "ActionEffectRange"
#if DEBUG
            + " [DEV]";
#elif TEST
            + " [TEST]";
#else
            ;
#endif

        private const string commandToggleConfig = "/actioneffectrange";

        private static bool _enabled;
        internal static bool Enabled
        {
            get => _enabled;
            set
            {
                if (value != _enabled)
                {
                    if (value) 
                    {
                        EffectRangeDrawing.Reset();
                        ActionWatcher.Enable();
                    }
                    else
                    {
                        EffectRangeDrawing.Reset();
                        ActionWatcher.Disable();
                    }
                    _enabled = value;
                }
            }
        }
        internal static bool DrawWhenCasting;
        
        internal static Configuration Config = null!;
        private static ConfigWindow? configWindow;

        public Plugin()
        {
            Config = PluginInterface.GetPluginConfig() as Configuration 
                ?? new Configuration();

            InitializeCommands();

            _ = InitializeConfigWindow();
            PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenConfigUi;

            PluginInterface.UiBuilder.Draw += EffectRangeDrawing.OnTick;

            ClientState.Logout += OnLogOut;
            ClientState.TerritoryChanged += CheckTerritory;

            RefreshConfig(true);
        }

        private static void InitializeCommands()
        {
            CommandManager.AddHandler(commandToggleConfig, 
                new CommandInfo((_, _) => configWindow?.Toggle())
            {
                HelpMessage = "Toggle the Configuration Window of ActionEffectRange",
                ShowInHelp = true
            });
        }

        private static async System.Threading.Tasks.Task InitializeConfigWindow()
        {
            try
            {
                await KamiToolKitLibrary.InitializeAsync(PluginInterface);
                configWindow = new ConfigWindow
                {
                    InternalName = "ActionEffectRangeConfig",
                    Title = "ActionEffectRange",
                    Subtitle = "Configuration",
                    Size = new Vector2(520, 560),
                };
            }
            catch (Exception e)
            {
                PluginLog.Error($"Failed to initialise the configuration window: {e}");
            }
        }

        private static void OnOpenConfigUi()
        {
            configWindow?.Open();
        }

        private static void CheckTerritory(uint terr)
        {
            if (IsPvPZone)
            {
                Enabled = Config.Enabled && Config.EnabledPvP;
                DrawWhenCasting = false;
            }
            else
            {
                Enabled = Config.Enabled;
                DrawWhenCasting = Config.DrawWhenCasting;
            }
        }

        private static void OnLogOut(int type, int code)
        {
            EffectRangeDrawing.Reset();
        }

        internal static void RefreshConfig(bool reloadSavedList = false)
        {
            EffectRangeDrawing.RefreshConfig();
            CheckTerritory(ClientState.TerritoryType); 

            if (reloadSavedList)
                ActionData.ReloadCustomisedData();
        }

        public static void LogUserDebug(string msg)
        {
            if (Config.LogDebug) PluginLog.Debug(msg);
        }


        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            PluginInterface.SavePluginConfig(Config);

            CommandManager.RemoveHandler(commandToggleConfig);
            
            PluginInterface.UiBuilder.Draw -= EffectRangeDrawing.OnTick;

            ClientState.Logout -= OnLogOut;
            ClientState.TerritoryChanged -= CheckTerritory;

            PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            PluginInterface.UiBuilder.OpenMainUi -= OnOpenConfigUi;

            ActionWatcher.Dispose();
            ClassJobWatcher.Dispose();

            if (ThreadSafety.IsMainThread)
            {
                configWindow?.Dispose();
                KamiToolKitLibrary.Dispose();
            }
            else
            {
                configWindow?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                KamiToolKitLibrary.DisposeAsync().GetAwaiter().GetResult();
            }
            configWindow = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
