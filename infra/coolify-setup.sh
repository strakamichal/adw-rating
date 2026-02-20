#!/usr/bin/env bash
set -euo pipefail

# Coolify Setup Script for ADW Rating
# Creates API and Web services via Coolify API, sets environment variables,
# and triggers initial deployment.
#
# Prerequisites:
#   - Coolify running on the VPS with API enabled
#   - MSSQL container running (port 1433)
#   - Docker logged into ghcr.io on the VPS
#   - DNS A records pointing to the VPS IP
#
# Usage:
#   cp infra/coolify-setup.env.example infra/coolify-setup.env
#   # Edit coolify-setup.env with your values
#   bash infra/coolify-setup.sh

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/coolify-setup.env"

if [[ ! -f "$ENV_FILE" ]]; then
    echo "Error: $ENV_FILE not found."
    echo "Copy coolify-setup.env.example to coolify-setup.env and fill in values."
    exit 1
fi

# shellcheck source=/dev/null
source "$ENV_FILE"

# Validate required variables
for var in COOLIFY_URL COOLIFY_TOKEN SERVER_UUID ENVIRONMENT_NAME \
           GHCR_OWNER DOMAIN_WEB DOMAIN_API DB_CONNECTION_STRING; do
    if [[ -z "${!var:-}" ]]; then
        echo "Error: $var is not set in $ENV_FILE"
        exit 1
    fi
done

API_URL="${COOLIFY_URL}/api/v1"
AUTH_HEADER="Authorization: Bearer ${COOLIFY_TOKEN}"
IMAGE_API="ghcr.io/${GHCR_OWNER}/adw-rating/api"
IMAGE_WEB="ghcr.io/${GHCR_OWNER}/adw-rating/web"

api() {
    local method="$1" endpoint="$2"
    shift 2
    local response http_code
    response=$(curl -s -w "\n%{http_code}" -X "$method" \
        -H "$AUTH_HEADER" \
        -H "Content-Type: application/json" \
        "${API_URL}${endpoint}" \
        "$@")
    http_code=$(echo "$response" | tail -1)
    response=$(echo "$response" | sed '$d')
    if [[ "$http_code" -ge 400 ]]; then
        echo "Error (HTTP ${http_code}): ${response}" >&2
        return 1
    fi
    echo "$response"
}

# Create project if UUID not provided
if [[ -z "${PROJECT_UUID:-}" ]]; then
    echo "=== Creating Coolify project ==="
    PROJECT_NAME="${PROJECT_NAME:-ADW Rating}"
    PROJ_RESPONSE=$(api POST /projects -d "$(jq -n --arg name "$PROJECT_NAME" '{name: $name, description: "ADW Rating system"}')")
    PROJECT_UUID=$(echo "$PROJ_RESPONSE" | jq -r '.uuid')
    echo "Project created: UUID = ${PROJECT_UUID}"
fi

echo "=== Creating API service ==="
API_PAYLOAD=$(jq -n \
    --arg proj "$PROJECT_UUID" \
    --arg srv "$SERVER_UUID" \
    --arg env "$ENVIRONMENT_NAME" \
    --arg img "$IMAGE_API" \
    --arg domain "https://${DOMAIN_API}" \
    '{
        project_uuid: $proj, server_uuid: $srv, environment_name: $env,
        docker_registry_image_name: $img, docker_registry_image_tag: "latest",
        name: "adwrating-api", domains: $domain, ports_exposes: "8080",
        health_check_enabled: true, health_check_path: "/health",
        health_check_port: "8080", health_check_method: "GET",
        health_check_return_code: 200, health_check_interval: 30,
        health_check_timeout: 5, health_check_retries: 3,
        health_check_start_period: 30,
        is_force_https_enabled: true, instant_deploy: false
    }')
API_RESPONSE=$(api POST /applications/dockerimage -d "$API_PAYLOAD")
API_UUID=$(echo "$API_RESPONSE" | jq -r '.uuid')
echo "API service created: UUID = ${API_UUID}"

echo "=== Setting API environment variables ==="
ENV_PAYLOAD=$(jq -n \
    --arg conn "$DB_CONNECTION_STRING" \
    '{data: [
        {key: "ADW_RATING_CONNECTION", value: $conn, is_build_time: false, is_literal: true},
        {key: "ASPNETCORE_ENVIRONMENT", value: "Production", is_build_time: false}
    ]}')
api PATCH "/applications/${API_UUID}/envs/bulk" -d "$ENV_PAYLOAD" > /dev/null
echo "API env vars set."

if [[ -n "${DB_ADMIN_CONNECTION_STRING:-}" ]]; then
    echo "=== Setting API bootstrap connection string ==="
    ADMIN_PAYLOAD=$(jq -n \
        --arg conn "$DB_ADMIN_CONNECTION_STRING" \
        '{data: [
            {key: "ADW_RATING_ADMIN_CONNECTION", value: $conn, is_build_time: false, is_literal: true}
        ]}')
    api PATCH "/applications/${API_UUID}/envs/bulk" -d "$ADMIN_PAYLOAD" > /dev/null
    echo "Admin connection string set (remove after first successful deploy)."
fi

echo "=== Deploying API ==="
api GET "/deploy?uuid=${API_UUID}&force=false" > /dev/null
echo "API deployment triggered. Waiting for it to become healthy..."
echo "(Check Coolify UI for progress)"

echo ""
echo "=== Creating Web service ==="
WEB_PAYLOAD=$(jq -n \
    --arg proj "$PROJECT_UUID" \
    --arg srv "$SERVER_UUID" \
    --arg env "$ENVIRONMENT_NAME" \
    --arg img "$IMAGE_WEB" \
    --arg domain "https://${DOMAIN_WEB}" \
    '{
        project_uuid: $proj, server_uuid: $srv, environment_name: $env,
        docker_registry_image_name: $img, docker_registry_image_tag: "latest",
        name: "adwrating-web", domains: $domain, ports_exposes: "8080",
        is_force_https_enabled: true, instant_deploy: false
    }')
WEB_RESPONSE=$(api POST /applications/dockerimage -d "$WEB_PAYLOAD")
WEB_UUID=$(echo "$WEB_RESPONSE" | jq -r '.uuid')
echo "Web service created: UUID = ${WEB_UUID}"

echo "=== Setting Web environment variables ==="
WEB_ENV_PAYLOAD=$(jq -n '{data: [
    {key: "ApiBaseUrl", value: "http://adwrating-api:8080", is_build_time: false},
    {key: "ASPNETCORE_ENVIRONMENT", value: "Production", is_build_time: false}
]}')
api PATCH "/applications/${WEB_UUID}/envs/bulk" -d "$WEB_ENV_PAYLOAD" > /dev/null
echo "Web env vars set."

echo "=== Deploying Web ==="
api GET "/deploy?uuid=${WEB_UUID}&force=false" > /dev/null
echo "Web deployment triggered."

echo ""
echo "============================================"
echo "Setup complete!"
echo ""
echo "API UUID: ${API_UUID}"
echo "Web UUID: ${WEB_UUID}"
echo ""
echo "IMPORTANT - Manual steps in Coolify UI:"
echo "  1. Set Container Name for API service to: adwrating-api"
echo "  2. Set Container Name for Web service to: adwrating-web"
echo "  3. Redeploy both services after setting container names"
echo ""
echo "GitHub Actions secrets to set:"
echo "  COOLIFY_TOKEN    = (use your API token)"
echo "  COOLIFY_API_UUID = ${API_UUID}"
echo "  COOLIFY_WEB_UUID = ${WEB_UUID}"
echo ""
echo "Verify:"
echo "  curl https://${DOMAIN_API}/health"
echo "  open https://${DOMAIN_WEB}"
echo "============================================"
