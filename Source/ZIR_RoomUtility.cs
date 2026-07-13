using RimWorld;
using Verse;

namespace ZealousInnocenceRegression
{
    /// <summary>
    /// Shared nursery-room detection used by MapComponent_RegressionEnvironment
    /// and ThoughtWorker_ZIR_NurseryRoom.
    /// </summary>
    public static class ZIR_RoomUtility
    {
        private static RoomRoleDef _nurseryRole;
        private static RoomRoleDef _bedroomRole;
        private static RoomRoleDef _classroomRole;
        private static RoomRoleDef _prisonCellRole;
        private static RoomRoleDef _barracksRole;
        private static RoomRoleDef _roomRole;

        private static RoomRoleDef NurseryRole   => _nurseryRole   ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("Nursery");
        private static RoomRoleDef BedroomRole   => _bedroomRole   ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("Bedroom");
        private static RoomRoleDef ClassroomRole => _classroomRole ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("Classroom");
        private static RoomRoleDef PrisonCellRole => _prisonCellRole ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("PrisonCell");
        private static RoomRoleDef BarracksRole  => _barracksRole  ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("Barracks");
        
        private static RoomRoleDef RoomRole  => _roomRole  ??= DefDatabase<RoomRoleDef>.GetNamedSilentFail("Room");

        /// <summary>
        /// Returns true when <paramref name="room"/> qualifies as a nursery / child space.
        /// Rules:
        ///   1. Room role == Nursery (Biotech) — always qualifies.
        ///   2. Room role == Bedroom or Classroom AND the room contains childish furniture.
        /// </summary>
        public static bool IsNurseryRoom(Room room)
        {
            if (room == null) return false;
            RoomRoleDef role = room.Role;

            if (NurseryRole != null && role == NurseryRole) return true;

            bool isChildRole = (BedroomRole   != null && role == BedroomRole)
                            || (ClassroomRole != null && role == ClassroomRole)
                            || (PrisonCellRole != null && role == PrisonCellRole)
                            || (BarracksRole  != null && role == BarracksRole)
                            || (RoomRole  != null && role == RoomRole);
            if (isChildRole)
                return ContainsChildishFurniture(room);

            return false;
        }

        private static bool ContainsChildishFurniture(Room room)
        {
            foreach (Thing thing in room.ContainedAndAdjacentThings)
                if (IsChildishFurnitureDef(thing.def.defName))
                    return true;
            return false;
        }

        private static bool IsChildishFurnitureDef(string defName) => defName switch
        {
            "Crib"            => true,   // Biotech vanilla crib
            "ToyBox"          => true,   // Biotech toy box
            "CribBasic"       => true,   // ZI crib
            "ChangingMat"     => true,   // ZI changing mat
            "FountainOfYouth" => true,   // ZI regression machine
            "ZIR_PottyChair"  => true,   // ZIR potty
            _                 => false
        };
    }
}
