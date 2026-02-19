using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdwRating.Data.Mssql.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    Organization = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Dogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CallName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalizedCallName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegisteredName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SizeCategory = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    SizeCategoryOverride = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Handlers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Handlers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RatingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Mu0 = table.Column<float>(type: "real", nullable: false),
                    Sigma0 = table.Column<float>(type: "real", nullable: false),
                    LiveWindowDays = table.Column<int>(type: "int", nullable: false),
                    MinRunsForLiveRanking = table.Column<int>(type: "int", nullable: false),
                    MinFieldSize = table.Column<int>(type: "int", nullable: false),
                    MajorEventWeight = table.Column<float>(type: "real", nullable: false),
                    SigmaDecay = table.Column<float>(type: "real", nullable: false),
                    SigmaMin = table.Column<float>(type: "real", nullable: false),
                    DisplayBase = table.Column<float>(type: "real", nullable: false),
                    DisplayScale = table.Column<float>(type: "real", nullable: false),
                    RatingSigmaMultiplier = table.Column<float>(type: "real", nullable: false),
                    PodiumBoostBase = table.Column<float>(type: "real", nullable: false),
                    PodiumBoostRange = table.Column<float>(type: "real", nullable: false),
                    PodiumBoostTarget = table.Column<float>(type: "real", nullable: false),
                    ProvisionalSigmaThreshold = table.Column<float>(type: "real", nullable: false),
                    NormTargetMean = table.Column<float>(type: "real", nullable: false),
                    NormTargetStd = table.Column<float>(type: "real", nullable: false),
                    EliteTopPercent = table.Column<float>(type: "real", nullable: false),
                    ChampionTopPercent = table.Column<float>(type: "real", nullable: false),
                    ExpertTopPercent = table.Column<float>(type: "real", nullable: false),
                    CountryTopN = table.Column<int>(type: "int", nullable: false),
                    MinTeamsForCountryRanking = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    NewHandlersCount = table.Column<int>(type: "int", nullable: false),
                    NewDogsCount = table.Column<int>(type: "int", nullable: false),
                    NewTeamsCount = table.Column<int>(type: "int", nullable: false),
                    Errors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Warnings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportLogs_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RunNumber = table.Column<int>(type: "int", nullable: false),
                    RoundKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeCategory = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Discipline = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsTeamRound = table.Column<bool>(type: "bit", nullable: false),
                    Judge = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sct = table.Column<float>(type: "real", nullable: true),
                    Mct = table.Column<float>(type: "real", nullable: true),
                    CourseLength = table.Column<float>(type: "real", nullable: true),
                    OriginalSizeCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Runs_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DogAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AliasName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CanonicalDogId = table.Column<int>(type: "int", nullable: false),
                    AliasType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DogAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DogAliases_Dogs_CanonicalDogId",
                        column: x => x.CanonicalDogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandlerAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AliasName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CanonicalHandlerId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandlerAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandlerAliases_Handlers_CanonicalHandlerId",
                        column: x => x.CanonicalHandlerId,
                        principalTable: "Handlers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandlerId = table.Column<int>(type: "int", nullable: false),
                    DogId = table.Column<int>(type: "int", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Mu = table.Column<float>(type: "real", nullable: false),
                    Sigma = table.Column<float>(type: "real", nullable: false),
                    Rating = table.Column<float>(type: "real", nullable: false),
                    PrevMu = table.Column<float>(type: "real", nullable: false),
                    PrevSigma = table.Column<float>(type: "real", nullable: false),
                    PrevRating = table.Column<float>(type: "real", nullable: false),
                    RunCount = table.Column<int>(type: "int", nullable: false),
                    FinishedRunCount = table.Column<int>(type: "int", nullable: false),
                    Top3RunCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsProvisional = table.Column<bool>(type: "bit", nullable: false),
                    TierLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PeakRating = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Dogs_DogId",
                        column: x => x.DogId,
                        principalTable: "Dogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teams_Handlers_HandlerId",
                        column: x => x.HandlerId,
                        principalTable: "Handlers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RunResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Faults = table.Column<int>(type: "int", nullable: true),
                    Refusals = table.Column<int>(type: "int", nullable: true),
                    TimeFaults = table.Column<float>(type: "real", nullable: true),
                    TotalFaults = table.Column<float>(type: "real", nullable: true),
                    Time = table.Column<float>(type: "real", nullable: true),
                    Speed = table.Column<float>(type: "real", nullable: true),
                    Eliminated = table.Column<bool>(type: "bit", nullable: false),
                    StartNo = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunResults_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RunResults_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    RunResultId = table.Column<int>(type: "int", nullable: false),
                    CompetitionId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Mu = table.Column<float>(type: "real", nullable: false),
                    Sigma = table.Column<float>(type: "real", nullable: false),
                    Rating = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingSnapshots_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RatingSnapshots_RunResults_RunResultId",
                        column: x => x.RunResultId,
                        principalTable: "RunResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RatingSnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_Slug",
                table: "Competitions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DogAliases_AliasName_AliasType",
                table: "DogAliases",
                columns: new[] { "AliasName", "AliasType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DogAliases_CanonicalDogId",
                table: "DogAliases",
                column: "CanonicalDogId");

            migrationBuilder.CreateIndex(
                name: "IX_HandlerAliases_AliasName",
                table: "HandlerAliases",
                column: "AliasName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandlerAliases_CanonicalHandlerId",
                table: "HandlerAliases",
                column: "CanonicalHandlerId");

            migrationBuilder.CreateIndex(
                name: "IX_Handlers_NormalizedName_Country",
                table: "Handlers",
                columns: new[] { "NormalizedName", "Country" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Handlers_Slug",
                table: "Handlers",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportLogs_CompetitionId",
                table: "ImportLogs",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingConfigurations_IsActive",
                table: "RatingConfigurations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RatingSnapshots_CompetitionId",
                table: "RatingSnapshots",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingSnapshots_RunResultId",
                table: "RatingSnapshots",
                column: "RunResultId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingSnapshots_TeamId_RunResultId",
                table: "RatingSnapshots",
                columns: new[] { "TeamId", "RunResultId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunResults_RunId_TeamId",
                table: "RunResults",
                columns: new[] { "RunId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunResults_TeamId",
                table: "RunResults",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Runs_CompetitionId_RoundKey",
                table: "Runs",
                columns: new[] { "CompetitionId", "RoundKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DogId",
                table: "Teams",
                column: "DogId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_HandlerId_DogId",
                table: "Teams",
                columns: new[] { "HandlerId", "DogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Slug",
                table: "Teams",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DogAliases");

            migrationBuilder.DropTable(
                name: "HandlerAliases");

            migrationBuilder.DropTable(
                name: "ImportLogs");

            migrationBuilder.DropTable(
                name: "RatingConfigurations");

            migrationBuilder.DropTable(
                name: "RatingSnapshots");

            migrationBuilder.DropTable(
                name: "RunResults");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Competitions");

            migrationBuilder.DropTable(
                name: "Dogs");

            migrationBuilder.DropTable(
                name: "Handlers");
        }
    }
}
