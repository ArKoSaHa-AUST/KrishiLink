#!/usr/bin/env bash
set -e

echo -e "\033[1;36m🚀 Starting KrishiLink Deployment Pipeline...\033[0m"

# -----------------------------
# 0. REQUIRED ENV CHECK
# -----------------------------
echo "🔐 Checking required environment variables..."

if [ -z "$DATABASE_URL" ]; then
  echo -e "\033[1;33m⚠️ DATABASE_URL is not set in current shell.\033[0m"
  echo "👉 For testing with real database, export it before running:"
  echo '   export DATABASE_URL="postgresql://postgres.YOUR_REF:YOUR_PASS@YOUR_POOLER_HOST:5432/postgres"'
  echo "   (Skipping live container DB ping; proceeding with build and image validations...)"
fi

# -----------------------------
# 1. CHECK .NET SDK
# -----------------------------
echo "🔍 Checking .NET SDK..."
dotnet --version

# -----------------------------
# 2. RESTORE & BUILD
# -----------------------------
echo "📦 Restoring dependencies..."
dotnet restore KrishiLink.sln

echo "🏗 Building project (Release)..."
dotnet build KrishiLink.sln --configuration Release --no-restore

# -----------------------------
# 3. CODE QUALITY CHECKS
# -----------------------------
echo "🧪 Running formatting & validation checks..."
dotnet format KrishiLink.sln --verify-no-changes

if [ -f ".github/scripts/check_css_classes.py" ]; then
  python3 .github/scripts/check_css_classes.py
fi

if [ -f ".github/scripts/check_localized_tempdata.py" ]; then
  python3 .github/scripts/check_localized_tempdata.py
fi

echo "✅ Code validation passed"

# -----------------------------
# 4. DOCKER CHECK
# -----------------------------
echo "🐳 Checking Docker..."
docker --version

# -----------------------------
# 5. BUILD DOCKER IMAGE
# -----------------------------
echo "📦 Building Docker image..."
docker build -t krishilink:local .

echo "✅ Docker image built successfully"

# -----------------------------
# 6. TEST CONTAINER (IF DATABASE_URL IS SET)
# -----------------------------
if [ -n "$DATABASE_URL" ]; then
  echo "🚀 Testing container with configured database..."

  docker rm -f krishilink-test >/dev/null 2>&1 || true

  docker run -d \
    -p 8080:8080 \
    -e DATABASE_URL="$DATABASE_URL" \
    -e DIRECT_URL="${DIRECT_URL:-$DATABASE_URL}" \
    -e ASPNETCORE_ENVIRONMENT=Production \
    -e App__PublicBaseUrl="${App__PublicBaseUrl:-https://krishilink.onrender.com}" \
    -e AllowedHosts="localhost;127.0.0.1;krishilink.onrender.com" \
    --name krishilink-test \
    krishilink:local

  sleep 6

  echo "📡 Checking container logs..."
  docker logs krishilink-test

  echo "🌐 Testing HTTP /healthz probe..."
  if curl -s -f http://localhost:8080/healthz >/dev/null; then
    echo -e "\033[1;32m✅ App is responding on /healthz!\033[0m"
  else
    echo -e "\033[1;31m❌ App not responding on /healthz\033[0m"
    docker logs krishilink-test
    docker stop krishilink-test >/dev/null 2>&1 || true
    docker rm krishilink-test >/dev/null 2>&1 || true
    exit 1
  fi

  # Cleanup test container
  docker stop krishilink-test >/dev/null 2>&1 || true
  docker rm krishilink-test >/dev/null 2>&1 || true
fi

# -----------------------------
# 7. GIT INTEGRATION & MERGE TO MAIN
# -----------------------------
echo "🔗 Syncing git branches..."

CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)

if [ "$CURRENT_BRANCH" != "main" ]; then
  echo "Switching from $CURRENT_BRANCH to main..."
  git checkout main
  git pull origin main --rebase || true
  git merge "$CURRENT_BRANCH" -m "feat(deploy): merge $CURRENT_BRANCH into main for Render deployment"
fi

git push origin main

# -----------------------------
# 8. FINAL OUTPUT
# -----------------------------
echo -e "\033[1;32m🎉 DEPLOYMENT PIPELINE READY!\033[0m"
echo ""
echo "👉 NEXT STEPS ON RENDER:"
echo "1. Go to https://dashboard.render.com"
echo "2. Click 'New +' > 'Blueprint' > Select 'KrishiLink' repo"
echo "3. Enter your Supabase credentials when prompted (DATABASE_URL, DIRECT_URL, SUPABASE_URL, etc.)"
echo "4. Click 'Apply' to start deployment 🚀"
