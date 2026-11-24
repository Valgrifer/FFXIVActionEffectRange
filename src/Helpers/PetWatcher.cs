using Dalamud.Game.ClientState.Objects.Types;
using Lumina.Excel;
using Sheets = Lumina.Excel.Sheets;

namespace ActionEffectRange.Helpers
{
    // Pets grouped by the job of their owner, for the pet drawing options
    public enum PetGroup { None, AcnSmn, Sch, Mch, Drk, Other }

    internal static class PetWatcher
    {
        private static readonly ExcelSheet<Sheets.Pet> petSheet
            = DataManager.GetExcelSheet<Sheets.Pet>();

        public static bool HasPetPresent => BuddyList.PetBuddy != null;

        public static IGameObject? GetPet()
            => BuddyList.PetBuddy?.GameObject;

        public static uint GetPetObjectId()
            => GetPet()?.EntityId ?? 0;

        public static Vector3 GetPetPosition()
            => GetPet()?.Position ?? new();

        public static float GetPetRotation()
            => GetPet()?.Rotation ?? 0;

        // Pet sheet key id
        public static uint GetPetType()
            => BuddyList.PetBuddy?.DataID ?? 0;

        public static bool IsNamelessPet(uint petType)
            => petType > 0 
            && string.IsNullOrEmpty(petSheet.GetRowOrDefault(petType)?.Name.ExtractText());

        public static bool IsBunshin(uint petType)
            => petType == 19 || petType == 22;

        // Bunshin is named but has no interactable models
        public static bool IsNamelessPetOrBunshin(uint petType)
            => IsNamelessPet(petType) || IsBunshin(petType);

        public static bool IsCurrentPetNameless()
            => IsNamelessPet(GetPetType());

        public static bool IsCurrentPetNamelessOrBunshin()
            => IsNamelessPetOrBunshin(GetPetType());

        // Can't find out how to reliably check from pets so just check the job
        public static bool IsCurrentPetACNPet()
            => !IsCurrentPetNamelessOrBunshin() 
            && ClassJobWatcher.IsCurrentClassJobACNRelated();

        public static bool IsCurrentPetNonACNNamedPet()
            => !IsCurrentPetNamelessOrBunshin()
            && !ClassJobWatcher.IsCurrentClassJobACNRelated();

        // Group of the current named pet; None for nameless pets (ground placed
        //  objects, handled by the ground-targeted option) and NIN's bunshin
        public static PetGroup GetCurrentPetGroup()
        {
            if (!HasPetPresent || IsCurrentPetNamelessOrBunshin()) return PetGroup.None;
            return ClassJobWatcher.CurrentClassJobId switch
            {
                26 or 27 => PetGroup.AcnSmn,    // ACN, SMN
                28 => PetGroup.Sch,
                31 => PetGroup.Mch,
                32 => PetGroup.Drk,
                _ => PetGroup.Other,
            };
        }

        public static bool IsCurrentPet(uint objectId)
            => GetPetObjectId() == objectId;

    }
}
