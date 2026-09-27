using Microsoft.EntityFrameworkCore.Migrations;

namespace HotelBooking.Infrastructure.Persistence.Migrations;

internal static class RoomSearchIndexes
{
    private const string Table = "Rooms";

    private const string NotDeleted = "[IsDeleted] = 0";

    public static void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_Capacity",
                table: Table,
                columns: ["HotelId", "AdultCapacity", "ChildrenCapacity"],
                filter: NotDeleted)
            .Annotation("SqlServer:Include", new[] { "BasePrice", "BasePriceCurrency", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_Rooms_Type_BasePrice",
            table: Table,
            columns: ["Type", "BasePrice"],
            filter: NotDeleted);

        migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_BasePrice",
                table: Table,
                columns: ["HotelId", "BasePrice"],
                filter: NotDeleted)
            .Annotation(
                "SqlServer:Include",
                new[] { "Number", "Type", "AdultCapacity", "ChildrenCapacity", "BasePriceCurrency" });
    }

    public static void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Rooms_HotelId_BasePrice", table: Table);
        migrationBuilder.DropIndex(name: "IX_Rooms_Type_BasePrice", table: Table);
        migrationBuilder.DropIndex(name: "IX_Rooms_HotelId_Capacity", table: Table);
    }
}
