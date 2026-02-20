using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdwRating.Data.Mssql.Migrations
{
    /// <inheritdoc />
    public partial class AddIsExcludedToRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExcluded",
                table: "Runs",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExcluded",
                table: "Runs");
        }
    }
}
