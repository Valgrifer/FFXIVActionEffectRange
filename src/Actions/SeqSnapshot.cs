using ActionEffectRange.Helpers;

namespace ActionEffectRange.Actions
{
    public class SeqSnapshot
    {
        public readonly ushort Seq;
        public readonly Vector3 PlayerPosition;
        public readonly float PlayerRotation;
        public readonly ulong TargetObjectId;
        public readonly Vector3 TargetPosition;
        public readonly uint PetObjectId;
        public readonly Vector3 PetPosition;
        public readonly float PetRotation;
        public readonly DateTime CreatedTime = DateTime.Now;
        public double ElapsedSeconds 
            => (DateTime.Now - CreatedTime).TotalSeconds;
        public bool HadValidTarget => IsValidGameObjectId(TargetObjectId);
        public bool WasPetPresent => PetObjectId != 0
            && PetObjectId != InvalidGameObjectId;

        public SeqSnapshot(ushort seq, Vector3 playerPos, float playerRotation,
            ulong targetObjId, Vector3 targetPos,
            uint petObjId, Vector3 petPos, float petRotation)
        {
            Seq = seq;
            PlayerPosition = playerPos;
            PlayerRotation = playerRotation;
            TargetObjectId = targetObjId;
            TargetPosition = targetPos;
            PetObjectId = petObjId;
            PetPosition = petPos;
            PetRotation = petRotation;
        }

        public SeqSnapshot(ushort seq, ulong targetObjId, Vector3 targetPos)
            : this(seq, LocalPlayer?.Position ?? new(), LocalPlayer?.Rotation ?? 0,
                  targetObjId, targetPos,
                  PetWatcher.GetPetObjectId(),
                  PetWatcher.GetPetPosition(),
                  PetWatcher.GetPetRotation())
        { }

        // Snapshot targeting the player itself
        public SeqSnapshot(ushort seq)
            : this(seq, LocalPlayer?.GameObjectId ?? 0, LocalPlayer?.Position ?? new())
        { }

        public override string ToString()
            => $"Seq#{Seq} player={PlayerPosition} rot={PlayerRotation:F3} " +
            $"target={TargetObjectId:X}@{TargetPosition} pet={PetObjectId:X}@{PetPosition}";
    }
}
