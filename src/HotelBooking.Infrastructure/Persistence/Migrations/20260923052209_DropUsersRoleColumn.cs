using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBooking.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Deliberately separate from AddUserRoles: UserRoles has to be filled and deployed before the
    /// column it replaces disappears, so that an instance of the previous build never selects a
    /// column that no longer exists
    /// </summary>
    /// <inheritdoc />
    public partial class DropUsersRoleColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // The column holds one role, so a rollback keeps the strongest one the user was granted
            migrationBuilder.Sql(
                """
                UPDATE Users
                SET Role = (SELECT MAX(Role) FROM UserRoles WHERE UserRoles.UserId = Users.Id)
                WHERE EXISTS (SELECT 1 FROM UserRoles WHERE UserRoles.UserId = Users.Id);
                """);
        }
    }
}
