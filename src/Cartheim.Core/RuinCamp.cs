using System;
using System.Collections.Generic;

namespace Cartheim.Core
{
    // A cart settled into a ruin: for each kind of ruin the game places (a location, always built the same way, though
    // placed anywhere and turned any way), where the cart stands and what's added around it, in the ruin's own space
    // (metres; x across, z from its front to its back, as the ruin survey logs them). The added pieces cost nothing and
    // are taken away again, with nothing back, when the cart returns to cart form.
    public class RuinCamp
    {
        public const float Reach = 20f;

        public string Name;
        // The cart: where it stands and how it's turned from the ruin.
        public float CartX, CartZ, CartYaw;
        // A wood door, in the ruin's doorway.
        public float DoorX, DoorZ, DoorYaw;
        // A wood fence, run by run, each from where the last ended.
        public List<((float x, float z) from, (float x, float z) to)> Fence;
        // The tarp (the tent's cloth, when the cart has a tent): over this much of the ruin, low at its -x side, high at +x.
        public float TarpMinX, TarpMaxX, TarpMinZ, TarpMaxZ, TarpLow, TarpHigh;
        // The floor inside the walls.
        public float RoomMinX, RoomMaxX, RoomMinZ, RoomMaxZ;
        // A campfire (burning without wood) with a cooking station over it, tucked into a corner under the tarp's high side.
        public float FireX, FireZ;
        // The cart's rug, laid in the room (square, this big each way), and its bed on it, lengthwise along z.
        public float RugX, RugZ, RugSize;
        public float BedX, BedZ;

        public static bool Near(float distance) => distance <= Reach;

        public static RuinCamp For(string location)
        {
            if (string.IsNullOrEmpty(location))
                return null;
            var name = location.Replace("(Clone)", "").Trim();
            return Camps.TryGetValue(name, out var camp) ? camp : null;
        }

        // A Black Forest stone house: four stone walls a metre thick (2.5 m tall on the left, 3.5 m on the right) round a
        // floor 6 m by 4 m, an arched doorway in the front, and the back wall broken from its left stub across to the right
        // wall. The cart stands across that break facing in (its sconce lights the room), with a wood fence round the back
        // of it; the campfire is in the front right corner, under the tarp's high side, which stops short of the right wall
        // so the smoke rising up it gets out; the bed lies on the rug by the left wall.
        private static readonly Dictionary<string, RuinCamp> Camps = new Dictionary<string, RuinCamp>
        {
            ["StoneHouse4"] = new RuinCamp
            {
                Name = "StoneHouse4",
                CartX = 0.28f, CartZ = 3.67f, CartYaw = 270f,
                DoorX = 0f, DoorZ = -1.8f, DoorYaw = 0f,
                Fence = new List<((float, float), (float, float))>
                {
                    ((-1.7f, 3.2f), (-1.7f, 6.0f)),
                    ((-1.7f, 6.0f), (3.5f, 6.0f)),
                    ((3.5f, 6.0f), (3.5f, 4.05f)),
                },
                TarpMinX = -4.7f, TarpMaxX = 2.5f, TarpMinZ = -3.0f, TarpMaxZ = 4.4f, TarpLow = 2.55f, TarpHigh = 3.55f,
                RoomMinX = -3.0f, RoomMaxX = 3.0f, RoomMinZ = -1.3f, RoomMaxZ = 2.7f,
                FireX = 2.2f, FireZ = -0.5f,
                RugX = -1.0f, RugZ = 0.7f, RugSize = 3.4f,
                BedX = -2.0f, BedZ = 0.7f,
            },
        };
    }

    // Fence pieces covering a straight run end to end (overlapping a little where they don't fit exactly), each turned
    // along it: 0 along x, 90 along z.
    public static class FenceRun
    {
        public static List<(float x, float z, float yaw)> Pieces((float x, float z) from, (float x, float z) to, float pieceLength)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            var pieces = new List<(float, float, float)>();
            if (length <= 0.01f)
                return pieces;
            var yaw = (float)(Math.Atan2(dz, dx) * 180.0 / Math.PI);
            yaw = ((yaw % 180f) + 180f) % 180f;
            var count = Math.Max(1, (int)Math.Ceiling(length / pieceLength - 0.001f));
            var first = count == 1 ? length / 2f : pieceLength / 2f;
            var step = count == 1 ? 0f : (length - pieceLength) / (count - 1);
            for (var i = 0; i < count; i++)
            {
                var along = (first + i * step) / length;
                pieces.Add((from.x + dx * along, from.z + dz * along, yaw));
            }
            return pieces;
        }
    }
}
