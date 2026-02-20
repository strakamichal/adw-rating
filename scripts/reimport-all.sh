#!/usr/bin/env bash
set -euo pipefail

# Reimport all competitions from scratch.
# Usage: ./scripts/reimport-all.sh
# Requires: ADW_RATING_CONNECTION env var set, or pass --connection to override.

cd "$(dirname "$0")/.."
CLI="dotnet run --project src/AdwRating.Cli --"
DATA="data"

echo "=== Dropping and recreating database ==="
# Use sqlcmd or dotnet ef, but simplest: use EF EnsureDeleted + EnsureCreated via seed-config
# We need a reset command. Let's use raw SQL instead.
CONNECTION="${ADW_RATING_CONNECTION:?Set ADW_RATING_CONNECTION env var}"

# Extract server and database from connection string (macOS-compatible)
DB_NAME=$(echo "$CONNECTION" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
SERVER=$(echo "$CONNECTION" | sed -n 's/.*Server=\([^;]*\).*/\1/p')
USER=$(echo "$CONNECTION" | sed -n 's/.*User Id=\([^;]*\).*/\1/p')
PASS=$(echo "$CONNECTION" | sed -n 's/.*Password=\([^;]*\).*/\1/p')

echo "Dropping database $DB_NAME..."
python3 -c "
import pyodbc
conn = pyodbc.connect('DRIVER={ODBC Driver 17 for SQL Server};SERVER=${SERVER};UID=${USER};PWD=${PASS};TrustServerCertificate=Yes', autocommit=True)
cursor = conn.cursor()
cursor.execute(\"\"\"
IF EXISTS (SELECT 1 FROM sys.databases WHERE name = '${DB_NAME}')
BEGIN
    ALTER DATABASE [${DB_NAME}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [${DB_NAME}];
END
\"\"\")
conn.close()
print('Database dropped.')
"

echo "=== Seeding config (creates DB + schema) ==="
$CLI seed-config

import() {
    local slug="$1" name="$2" date="$3" tier="$4" country="$5" file="$6"
    local end_date="${7:-}" org="${8:-}"

    echo ""
    echo "--- Importing: $name ($slug) ---"

    local cmd="$CLI import \"$DATA/$file\" --competition \"$slug\" --name \"$name\" --date \"$date\" --tier $tier"
    [ -n "$country" ] && cmd="$cmd --country \"$country\""
    [ -n "$end_date" ] && cmd="$cmd --end-date \"$end_date\""
    [ -n "$org" ] && cmd="$cmd --organization \"$org\""

    eval $cmd
}

echo ""
echo "=== Importing competitions (chronological order) ==="

# 2024
import "proseccup-2024" "ProsecCup 2024" "2024-01-19" 2 "ITA" "proseccup_2024/proseccup_2024_results.csv" "2024-01-21" "FCI"
import "polish-open-2024-inl" "Polish Open 2024 (I & L)" "2024-02-09" 2 "POL" "polish_open_2024_inl/polish_open_2024_inl_results.csv" "2024-02-11" "FCI"
import "polish-open-2024-xsm" "Polish Open 2024 (XS, S & M)" "2024-02-09" 2 "POL" "polish_open_2024_xsm/polish_open_2024_xsm_results.csv" "2024-02-11" "FCI"
import "hungarian-open-2024" "Hungarian Open 2024" "2024-02-23" 2 "HUN" "hungarian_open_2024/hungarian_open_2024_results.csv" "2024-02-25" "FCI"
import "fmbb-2024" "FMBB World Championship 2024" "2024-04-25" 2 "ITA" "fmbb_2024/fmbb_2024_results.csv" "2024-04-28" "FCI"
import "alpine-agility-open-2024" "Alpine Agility Open 2024" "2024-06-07" 2 "ITA" "alpine_agility_open_2024/alpine_agility_open_2024_results.csv" "2024-06-09" "FCI"
import "midsummer-dog-sports-festival-2024" "Midsummer Dog Sports Festival 2024" "2024-06-19" 2 "FIN" "midsummer_dog_sports_festival_2024/midsummer_dog_sports_festival_2024_results.csv" "2024-06-23" "FCI"
import "croatian-open-2024" "Croatian Open 2024" "2024-06-21" 2 "HRV" "croatian_open_2024/croatian_open_2024_results.csv" "2024-06-23" "FCI"
import "slovenian-open-2024" "Slovenian Agility Open 2024" "2024-06-28" 2 "SVN" "slovenian_open_2024/slovenian_open_2024_results.csv" "2024-06-30" "FCI"
import "moravia-open-2024" "Moravia Open 2024" "2024-07-05" 2 "CZE" "moravia-open-2024/moravia-open-2024_results.csv" "2024-07-07" "FCI"
import "joawc-soawc-2024" "Junior & Senior Open AWC 2024" "2024-07-18" 1 "BEL" "joawc_soawc_2024/joawc_soawc_2024_results.csv" "2024-07-21" "FCI"
import "prague-agility-party-2024" "Prague Agility Party 2024" "2024-07-19" 2 "CZE" "prague-agility-party-2024/prague-agility-party-2024_results.csv" "2024-07-21" "FCI"
import "border-collie-classic-2024" "Border Collie Classic 2024" "2024-07-26" 2 "GBR" "border_collie_classic_2024/border_collie_classic_2024_results.csv" "2024-07-28" "FCI"
import "eo-2024" "FCI Agility European Open 2024" "2024-08-01" 1 "GBR" "eo2024/eo2024_results.csv" "2024-08-04" "FCI"
import "nac-2024" "Nordic Agility Championship 2024" "2024-08-17" 2 "DNK" "nordic_agility_championship_2024/nordic_agility_championship_2024_results.csv" "2024-08-18" "FCI"
import "awc-2024" "FCI Agility World Championship 2024" "2024-10-01" 1 "BEL" "awc2024/awc2024_results.csv" "2024-10-06" "FCI"
import "norwegian-open-2024" "Norwegian Open 2024" "2024-10-11" 2 "NOR" "norwegian_open_2024/norwegian_open_2024_results.csv" "2024-10-13" "FCI"
import "polish-open-soft-2024-inl" "Polish Open SOFT 2024 (I & L)" "2024-11-09" 2 "POL" "polish_open_soft_2024_inl/polish_open_soft_2024_inl_results.csv" "2024-11-10" "FCI"
import "polish-open-soft-2024-xsm" "Polish Open SOFT 2024 (XS, S & M)" "2024-11-09" 2 "POL" "polish_open_soft_2024_xsm/polish_open_soft_2024_xsm_results.csv" "2024-11-10" "FCI"

# 2025
import "proseccup-2025" "ProsecCup 2025" "2025-01-17" 2 "ITA" "proseccup_2025/proseccup_2025_results.csv" "2025-01-19" "FCI"
import "polish-open-2025-inl" "Polish Open 2025 (I & L)" "2025-02-07" 2 "POL" "polish_open_2025_inl/polish_open_2025_inl_results.csv" "2025-02-09" "FCI"
import "polish-open-2025-xsm" "Polish Open 2025 (XS, S & M)" "2025-02-07" 2 "POL" "polish_open_2025_xsm/polish_open_2025_xsm_results.csv" "2025-02-09" "FCI"
import "hungarian-open-2025" "Hungarian Open 2025" "2025-02-21" 2 "HUN" "hungarian_open_2025/hungarian_open_2025_results.csv" "2025-02-23" "FCI"
import "fmbb-2025" "FMBB World Championship 2025" "2025-05-06" 2 "GRC" "fmbb_2025/fmbb_2025_results.csv" "2025-05-11" "FCI"
import "alpine-agility-open-2025" "Alpine Agility Open 2025" "2025-06-06" 2 "ITA" "alpine_agility_open_2025/alpine_agility_open_2025_results.csv" "2025-06-08" "FCI"
import "austrian-agility-open-2025" "Austrian Agility Open 2025" "2025-06-13" 2 "AUT" "austrian_agility_open_2025/austrian_agility_open_2025_results.csv" "2025-06-15" "FCI"
import "midsummer-dog-sports-festival-2025" "Midsummer Dog Sports Festival 2025" "2025-06-19" 2 "FIN" "midsummer_dog_sports_festival_2025/midsummer_dog_sports_festival_2025_results.csv" "2025-06-22" "FCI"
import "slovenian-open-2025" "Slovenian Agility Open 2025" "2025-06-27" 2 "SVN" "slovenian_open_2025/slovenian_open_2025_results.csv" "2025-06-29" "FCI"
import "dutch-open-2025" "Dutch Open 2025" "2025-07-03" 2 "NLD" "dutch_open_2025/dutch_open_2025_results.csv" "2025-07-06" "FCI"
import "moravia-open-2025" "Moravia Open 2025" "2025-07-04" 2 "CZE" "moravia-open-2025/moravia-open-2025_results.csv" "2025-07-06" "FCI"
import "joawc-soawc-2025" "Junior & Senior Open AWC 2025" "2025-07-09" 1 "PRT" "joawc_soawc_2025/joawc_soawc_2025_results.csv" "2025-07-13" "FCI"
import "eo-2025" "FCI Agility European Open 2025" "2025-07-16" 1 "PRT" "eo2025/eo2025_results.csv" "2025-07-20" "FCI"
import "prague-agility-party-2025" "Prague Agility Party 2025" "2025-08-08" 2 "CZE" "prague-agility-party-2025/prague-agility-party-2025_results.csv" "2025-08-10" "FCI"
import "helvetic-agility-masters-2025" "Helvetic Agility Masters 2025" "2025-08-15" 2 "CHE" "helvetic_agility_masters_2025/helvetic_agility_masters_2025_results.csv" "2025-08-17" "FCI"
import "nac-2025" "Nordic Agility Championship 2025" "2025-08-22" 2 "NOR" "nordic_agility_championship_2025/nordic_agility_championship_2025_results.csv" "2025-08-24" "FCI"
import "awc-2025" "FCI Agility World Championship 2025" "2025-09-17" 1 "SWE" "awc2025/awc2025_results.csv" "2025-09-21" "FCI"
import "norwegian-open-2025" "Norwegian Open 2025" "2025-10-10" 2 "NOR" "norwegian_open_2025/norwegian_open_2025_results.csv" "2025-10-12" "FCI"
import "lotw-i-2025-2026" "Lord of the Winter I 2025/2026" "2025-10-01" 2 "SVK" "lotw_i_2025_2026/lotw_i_2025_2026_results.csv" "" "FCI"
import "polish-open-soft-2025-inl" "Polish Open SOFT 2025 (I & L)" "2025-11-07" 2 "POL" "polish_open_soft_2025_inl/polish_open_soft_2025_inl_results.csv" "2025-11-09" "FCI"
import "polish-open-soft-2025-xsm" "Polish Open SOFT 2025 (XS, S & M)" "2025-11-07" 2 "POL" "polish_open_soft_2025_xsm/polish_open_soft_2025_xsm_results.csv" "2025-11-09" "FCI"
import "lotw-ii-2025-2026" "Lord of the Winter II 2025/2026" "2025-12-01" 2 "SVK" "lotw_ii_2025_2026/lotw_ii_2025_2026_results.csv" "" "FCI"

# 2026
import "lotw-iii-2025-2026" "Lord of the Winter III 2025/2026" "2026-02-01" 2 "SVK" "lotw_iii_2025_2026/lotw_iii_2025_2026_results.csv" "" "FCI"
import "polish-open-2026-inl" "Polish Open 2026 (I & L)" "2026-02-13" 2 "POL" "polish_open_2026_inl/polish_open_2026_inl_results.csv" "2026-02-15" "FCI"
import "polish-open-2026-xsm" "Polish Open 2026 (XS, S & M)" "2026-02-13" 2 "POL" "polish_open_2026_xsm/polish_open_2026_xsm_results.csv" "2026-02-15" "FCI"

echo ""
echo "=== All imports complete ==="
