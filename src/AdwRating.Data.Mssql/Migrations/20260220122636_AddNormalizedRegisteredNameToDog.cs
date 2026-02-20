using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdwRating.Data.Mssql.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedRegisteredNameToDog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedRegisteredName",
                table: "Dogs",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedRegisteredName",
                table: "Dogs");
        }
    }
}
