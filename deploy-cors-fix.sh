#!/bin/bash

# CORS Fix Deployment Script
# This script commits and pushes the CORS configuration changes to trigger automatic deployment

set -e

echo "=========================================="
echo "CORS Fix Deployment Script"
echo "=========================================="
echo ""

# Check if we're in the right directory
if [ ! -f "src/NursingCareBackend.Api/appsettings.json" ]; then
    echo "❌ Error: Must run this script from the NursingCareBackend directory"
    exit 1
fi

echo "✓ Current directory verified"
echo ""

# Show the CORS changes
echo "📋 CORS Configuration Changes:"
echo "-------------------------------------------"
grep -A 6 '"Cors"' src/NursingCareBackend.Api/appsettings.json
echo "-------------------------------------------"
echo ""

# Check git status
echo "📊 Git Status:"
git status --short
echo ""

# Confirm with user
read -p "Do you want to commit and push these changes? (y/n) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Deployment cancelled"
    exit 0
fi

# Get current branch
CURRENT_BRANCH=$(git branch --show-current)
echo "📍 Current branch: $CURRENT_BRANCH"
echo ""

# Commit changes
echo "💾 Committing changes..."
git add src/NursingCareBackend.Api/appsettings.json
git commit -m "fix: Add Azure Static Web Apps URLs to CORS configuration

- Added https://witty-forest-00b6eb010.2.azurestaticapps.net (web app)
- Added https://thankful-forest-0e5e45410.2.azurestaticapps.net (mobile web app)
- Added https://auth.expo.io for mobile OAuth
- Fixes XHR failed loading errors in production"

echo "✓ Changes committed"
echo ""

# Push to remote
echo "🚀 Pushing to remote..."
git push origin "$CURRENT_BRANCH"

echo "✓ Changes pushed successfully"
echo ""

echo "=========================================="
echo "✅ Deployment Initiated!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Monitor the GitHub Actions workflow:"
echo "   https://github.com/YOUR_USERNAME/YOUR_REPO/actions"
echo ""
echo "2. Wait for deployment to complete (usually 3-5 minutes)"
echo ""
echo "3. Verify CORS is working:"
echo "   - Open: https://witty-forest-00b6eb010.2.azurestaticapps.net"
echo "   - Open browser DevTools (F12) → Console"
echo "   - Try logging in or making API calls"
echo "   - Check Network tab for successful requests (200 OK)"
echo ""
echo "4. If issues persist, check:"
echo "   - Backend logs: https://portal.azure.com → NursingCareBackend → Log stream"
echo "   - GitHub Actions logs for deployment errors"
echo ""
