using FFXIVClientStructs.FFXIV.Client.Game;

namespace ActionEffectRange.Helpers
{
    internal unsafe static class ActionManagerHelper
    {
        private static ActionManager* Mgr => ActionManager.Instance();

        public static nint Address => (nint)Mgr;

        public static ushort CurrentSeq
            => Mgr != null ? Mgr->LastUsedActionSequence : (ushort)0;
        public static ushort LastRecievedSeq
            => Mgr != null ? Mgr->LastHandledActionSequence : (ushort)0;

        public static bool IsCasting
            => Mgr != null && Mgr->CastActionType != 0;
        // Spell id of the action being cast (= Action sheet row id)
        public static uint CastingActionId
            => Mgr != null ? Mgr->CastSpellId : 0u;
        public static ulong CastTargetObjectId
            => Mgr != null ? (ulong)Mgr->CastTargetId : 0ul;
        public static Vector3 CastTargetPosition
            => Mgr != null ? Mgr->CastTargetPosition : Vector3.Zero;
        // The player rotation when casting
        public static float CastRotation
            => Mgr != null ? Mgr->CastRotation : 0f;
    }
}
