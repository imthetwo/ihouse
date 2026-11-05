
using ihouse.Models;
using ihouse.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Data;

public static class DataRoomImages
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        string[] types =
        {
            "Standard Room",
            "Studio",
            "Furnished Studio",
            "1 Bedroom",
            "2 Bedrooms"
        };

        decimal[] rents =
        {
            150m, 250m, 320m, 400m, 550m
        };

        int[] capacities =
        {
            2, 2, 2, 3, 4
        };

        var existingRooms = await context.Rooms.ToListAsync();

        var existingNumbers = new HashSet<string>(
            existingRooms.Select(r => r.RoomNumber),
            StringComparer.OrdinalIgnoreCase
        );

        // Add images to existing rooms.
        for (int i = 0; i < existingRooms.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(existingRooms[i].ImageUrl))
            {
                int imageNumber = i % 12 + 1;

                existingRooms[i].ImageUrl =
                    $"/images/rooms/room-{imageNumber:00}.jpg";
            }
        }

        // Seed 80 rooms across 8 floors.
        for (int i = 0; i < 80; i++)
        {
            int floor = i / 10 + 1;
            int sequence = i % 10 + 1;

            string roomNumber = $"A{floor}{sequence:00}";

            if (existingNumbers.Contains(roomNumber))
                continue;

            int typeIndex = i % types.Length;
            int imageNumber = i % 12 + 1;

            var room = new Room
            {
                RoomNumber = roomNumber,
                Floor = floor,
                RoomType = types[typeIndex],
                Capacity = capacities[typeIndex],
                RentPrice = rents[typeIndex],
                Status = i % 20 == 0
                    ? RoomStatus.Maintenance
                    : RoomStatus.Active,
                Description =
                    $"Sample {types[typeIndex]} rental room on floor {floor}.",
                ImageUrl =
                    $"/images/rooms/room-{imageNumber:00}.jpg"
            };

            context.Rooms.Add(room);
            existingNumbers.Add(roomNumber);
        }

        await context.SaveChangesAsync();
    }
}
