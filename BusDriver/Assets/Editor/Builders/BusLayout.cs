using UnityEngine;

namespace BusDriver.Editor.Builders {
    // The bus's geometry (Appendix A.2, D-entry "bus art fit"), measured off the artist's
    // SK_Bus_00 (Bus_Asset_001) so the physics, the seats and every anchor line up with the model.
    // Root-local metres: origin on the ground under the middle of the bus, +Z forward, driver on
    // the left (−X), door on the right (+X). The greybox view is built from the same numbers.
    public static class BusLayout {
        // ------------------------------------------------------------------ body
        public const float Width = 2.9f;
        public const float Length = 11.9f;
        public const float Height = 3.1f;
        public const float HullBottom = 0.4f;
        public const float Top = HullBottom + Height;
        // The body runs z −5.89 … 6.01
        public const float CenterZ = 0.06f;
        public const float FrontZ = CenterZ + Length * 0.5f;
        public const float RearZ = CenterZ - Length * 0.5f;
        // Inside faces of the side walls, the ceiling and the back wall
        public const float InnerHalfWidth = 1.35f;
        public const float Ceiling = 2.9f;
        public const float InnerRearZ = -5.2f;

        // ----------------------------------------------------------------- floor
        public const float FloorTop = 0.8f;
        // The step inside the door is lower than the cabin floor
        public const float DoorWellTop = 0.5f;
        public const float DoorWellInnerX = 0.9f;
        // The rear bench stands on a platform across the full width; the aisle ends at its edge
        public const float RearPlatformTop = 1.26f;
        // The raised platforms over the wheels, under the side-facing seats (|x| 0.7 … 1.35)
        public const float FrontArchPlatformTop = 1.23f;
        public const float RearPlatformFrontZ = -4.5f;
        public const float AisleEndZ = -4.4f;
        // The dash bulkhead in front of the driver and the door
        public const float DashZ = 5.6f;

        // ---------------------------------------------------------------- wheels
        // The art's tyres are 0.59 m; the physics radius is 0.55 so the tyre sits a little into
        // the road under the bus's weight, with the wheel centres exactly where the model has them
        public const float WheelCenterY = 0.55f;
        public const float FrontAxleZ = 3f;
        public const float RearAxleZ = -2.9f;
        public const float FrontTrackHalf = 1.3f;
        // The middle of each dual rear pair (tyres at 0.97 and 1.30)
        public const float RearTrackHalf = 1.13f;

        // ------------------------------------------------------------------ door
        // Two leaves hinged at z 4.2 and 5.4, folding inward
        public const float DoorBack = 4.2f;
        public const float DoorFront = 5.4f;
        public const float DoorZ = 4.8f;
        public const float DoorPanelX = 1.407f;
        public const float DoorLeafOpenAngle = 88f;

        // ---------------------------------------------------------------- driver
        public static readonly Vector3 DriverHeadPos = new Vector3(-0.8f, 2.1f, 4.6f);
        public static readonly Vector3 DriverSeatCenter = new Vector3(-0.8f, 1.45f, 4.43f);
        public static readonly Vector3 DriverSeatSize = new Vector3(0.65f, 1.3f, 0.92f);
        // The rail and divider behind the driver's seat: x −1.35 … −0.13 at z 3.9, up to 1.7
        public const float DriverPartitionZ = 3.9f;
        public const float DriverPartitionInnerX = -0.13f;
        public const float DriverPartitionTop = 1.7f;

        // ----------------------------------------------------------------- seats

        public struct Seat {
            public int Row;
            public int Column;
            public Vector3 Position;
            // Degrees about Y: 0 faces forward, ±90 the side-facing seats over the wheel arches
            public float Yaw;
        }

        // Columns by index: L2, L1, R1, R2 as before (§2.6), plus C, the middle of the rear bench.
        // C is last so the four side columns keep their indices.
        public static readonly string[] SeatColumns = { "L2", "L1", "R1", "R2", "C" };
        public const int L2 = 0;
        public const int L1 = 1;
        public const int R1 = 2;
        public const int R2 = 3;
        public const int C = 4;
        public const int Rows = 8;

        const float ForwardTop = 1.382f;
        const float FrontArchTop = 1.352f;
        const float RearArchTop = 1.36f;
        const float OuterX = 1.062f;
        const float InnerX = 0.516f;
        const float ArchX = 0.913f;

        // 33 seats. R1 and R7 face the aisle from the wheel arches, two per side; R2–R6 are rows
        // of four facing forward; R8 is the five-seat rear bench. In BusCabin's order: rear row
        // first, left to right, as the MVP listed them.
        public static readonly Seat[] Seats = BuildSeats();

        static Seat[] BuildSeats() {
            var seats = new System.Collections.Generic.List<Seat>();
            // R8, the rear bench
            const float benchX = 0.533f;
            Add(seats, 8, L2, -1.069f, ForwardTop, -4.713f, 0f);
            Add(seats, 8, L1, -benchX, ForwardTop, -4.713f, 0f);
            Add(seats, 8, C, 0f, ForwardTop, -4.713f, 0f);
            Add(seats, 8, R1, benchX, ForwardTop, -4.713f, 0f);
            Add(seats, 8, R2, 1.069f, ForwardTop, -4.713f, 0f);
            // R7 over the rear wheels: L2/R2 the rear pair, L1/R1 the front pair, facing the aisle
            Add(seats, 7, L2, -ArchX, RearArchTop, -3.406f, 90f);
            Add(seats, 7, L1, -ArchX, RearArchTop, -2.806f, 90f);
            Add(seats, 7, R1, ArchX, RearArchTop, -2.8f, -90f);
            Add(seats, 7, R2, ArchX, RearArchTop, -3.4f, -90f);
            // R6 … R2
            float[] rowZ = { 1.887f, 0.887f, -0.113f, -1.113f, -2.063f };
            for (int row = 6; row >= 2; row--) {
                float z = rowZ[row - 2];
                Add(seats, row, L2, -OuterX, ForwardTop, z, 0f);
                Add(seats, row, L1, -InnerX, ForwardTop, z, 0f);
                Add(seats, row, R1, InnerX, ForwardTop, z, 0f);
                Add(seats, row, R2, OuterX, ForwardTop, z, 0f);
            }
            // R1 over the front wheels
            Add(seats, 1, L2, -ArchX, FrontArchTop, 2.997f, 90f);
            Add(seats, 1, L1, -ArchX, FrontArchTop, 3.597f, 90f);
            Add(seats, 1, R1, ArchX, FrontArchTop, 3.603f, -90f);
            Add(seats, 1, R2, ArchX, FrontArchTop, 3.003f, -90f);
            return seats.ToArray();
        }

        static void Add(System.Collections.Generic.List<Seat> seats, int row, int column, float x, float y, float z, float yaw) {
            seats.Add(new Seat { Row = row, Column = column, Position = new Vector3(x, y, z), Yaw = yaw });
        }

        public static string SeatName(int row, int column) {
            return $"Anchor_Seat_R{row}_{SeatColumns[column]}";
        }

        // The walking colliders over the seats. The player is 0.56 m wide plus a 3 cm skin each
        // side, so the gap between the left and right boxes must stay over 0.62 m:
        // - along the forward rows (R2–R6) they stop 0.11 m short of the cushions' inner edges
        //   (±0.24), leaving 0.7 m; the player brushes the cushion corners, unseen
        // - over the wheel arches (R1, R7) the side-facing seats only start at ±0.66, which also
        //   clears the driver's partition rail (it reaches x −0.13 at z 3.9)
        public const float BenchInnerX = 0.35f;
        public const float ArchBenchInnerX = 0.66f;
        public const float BenchTop = 1.95f;
        public const float BenchFrontZ = 3.9f;
        public const float FrontArchRearZ = 2.2f;
        public const float RearArchFrontZ = -2.5f;
        public const float BenchRearZ = -3.72f;
        public const float RearBenchFrontZ = -4.45f;

        // ---------------------------------------------------------------- cameras
        // Placed where the artist's CCTV cameras hang, just under the ceiling, facing straight down
        // the aisle as before. The ceiling is 0.4 m lower than the greybox's, so they look further
        // down (22°/28°/22° before) or the top of every feed would be ceiling.
        public static readonly Vector3 CctvFront = new Vector3(0.25f, 3.06f, 5.24f);
        public static readonly Vector3 CctvMid = new Vector3(0f, 3.08f, 0.5f);
        public static readonly Vector3 CctvRear = new Vector3(0f, 3.08f, -5.08f);
        public const float CctvFrontPitch = 30f;
        public const float CctvMidPitch = 34f;
        public const float CctvRearPitch = 30f;

        // ---------------------------------------------------------------- lights
        public static readonly Vector3 HeadlightLeft = new Vector3(-1f, 1.1f, 6f);
        public static readonly float[] CabinLightZ = { -4f, 0f, 3.5f };
        public const float CabinLightY = 2.85f;
        public static readonly Vector3 DashLight = new Vector3(-0.83f, 1.71f, 5.4f);

        // ----------------------------------------------------------- dash screens
        // Each at the centre of a flat area on the model, +Z toward the driver, scale x/y the size
        // in metres (D38). The sizes keep each canvas's aspect ratio (DashPrefabBuilder).
        public struct DashSpot {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector2 Size;
        }

        // The Clock and the fare box have no screen of their own on Bus_Asset_001: the clock sits on
        // the switch panel right of the gauges, the fare box on the radio's face. The GPS fills the
        // dash monitor, the mirror the interior rear-view mirror.
        public static readonly DashSpot DashClock = new DashSpot {
            Position = new Vector3(-0.445f, 1.757f, 5.458f),
            Rotation = Quaternion.Euler(-60f, 180f, 0f),
            Size = new Vector2(0.16f, 0.06f),
        };
        public static readonly DashSpot DashFareBox = new DashSpot {
            Position = new Vector3(-0.188f, 1.532f, 5.091f),
            Rotation = Quaternion.LookRotation(new Vector3(-0.44f, 0f, -0.9f), Vector3.up),
            Size = new Vector2(0.18f, 0.1f),
        };
        public static readonly DashSpot DashGps = new DashSpot {
            Position = new Vector3(-0.083f, 1.812f, 5.363f),
            Rotation = Quaternion.LookRotation(new Vector3(-0.2f, 0.842f, -0.504f), new Vector3(0.353f, 0.541f, 0.764f)),
            Size = new Vector2(0.25f, 0.156f),
        };
        public static readonly DashSpot DashMirror = new DashSpot {
            Position = new Vector3(-0.015f, 2.59f, 5.335f),
            Rotation = Quaternion.LookRotation(new Vector3(-0.21f, -0.12f, -0.97f), Vector3.up),
            Size = new Vector2(0.4f, 0.12f),
        };
    }
}
