using Dalamud.Game.ClientState.Objects.SubKinds;
using Sheets = Lumina.Excel.Sheets;

namespace ActionEffectRange
{
    internal class Game
    {
        public static bool IsPlayerLoaded
            => ClientState.IsLoggedIn && ObjectTable.LocalPlayer != null;

        public static IPlayerCharacter? LocalPlayer => ObjectTable.LocalPlayer;

        public static bool IsPvPZone
            => DataManager.GetExcelSheet<Sheets.TerritoryType>()
                .GetRowOrDefault(ClientState.TerritoryType)?.IsPvpZone ?? false;

        // GameObjectId used by the game for "no target"
        public const ulong InvalidGameObjectId = 0xE0000000;

        public static bool IsValidGameObjectId(ulong id)
            => id != 0 && id != InvalidGameObjectId;
    }
}
