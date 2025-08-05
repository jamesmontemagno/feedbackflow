# GitHub Authentication Configuration for Azure Web Apps

This document explains how to configure GitHub authentication with email access for Azure Web Apps using Azure Easy Auth.

## Overview

The FeedbackFlow application uses Azure Web Apps with Azure Easy Auth (App Service Authentication) to handle GitHub OAuth authentication. To access user email addresses from GitHub, the GitHub OAuth application must request the `user:email` scope.

## Configuration Steps

### 1. GitHub OAuth App Configuration

1. Go to your GitHub account/organization settings
2. Navigate to **Developer settings** > **OAuth Apps**
3. Select your existing OAuth application or create a new one
4. Ensure the following settings:
   - **Application name**: FeedbackFlow (or your preferred name)
   - **Homepage URL**: `https://yourapp.azurewebsites.net`
   - **Authorization callback URL**: `https://yourapp.azurewebsites.net/.auth/login/github/callback`

### 2. Azure App Service Authentication Configuration

#### Via Azure Portal:

1. Navigate to your Azure App Service in the Azure portal
2. Go to **Authentication** (under Settings)
3. If not already configured, click **Add identity provider**
4. Select **GitHub** as the identity provider
5. Configure the following settings:
   - **Client ID**: Your GitHub OAuth app client ID
   - **Client secret**: Your GitHub OAuth app client secret
   - **Allowed token audiences**: Leave default
   - **Restrict access**: Choose appropriate option for your needs

#### Configure GitHub Scopes:

Unfortunately, the Azure portal UI doesn't provide a direct way to configure OAuth scopes. You need to use the Azure CLI or REST API:

##### Using Azure CLI:

```bash
# Get your app's resource ID
RESOURCE_GROUP="your-resource-group"
APP_NAME="your-app-name"

# Configure GitHub authentication with email scope
az webapp auth github update \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --client-id "YOUR_GITHUB_CLIENT_ID" \
  --client-secret "YOUR_GITHUB_CLIENT_SECRET" \
  --scopes "user:email"
```

##### Using Azure REST API:

Make a PUT request to configure the authentication settings:

```json
{
  "properties": {
    "enabled": true,
    "httpSettings": {
      "requireHttps": true
    },
    "globalValidation": {
      "requireAuthentication": false,
      "unauthenticatedClientAction": "AllowAnonymous"
    },
    "identityProviders": {
      "gitHub": {
        "enabled": true,
        "registration": {
          "clientId": "YOUR_GITHUB_CLIENT_ID",
          "clientSecretSettingName": "GITHUB_CLIENT_SECRET"
        },
        "login": {
          "scopes": ["user:email"]
        }
      }
    }
  }
}
```

### 3. Application Settings

Ensure the following application settings are configured in your Azure App Service:

- `GITHUB_CLIENT_SECRET`: Your GitHub OAuth app client secret
- `Authentication:UseEasyAuth`: Set to `true` in production

### 4. Code Changes

The application code has been enhanced to better handle GitHub email claims:

#### Frontend (`ServerSideAuthService.cs`):
- Enhanced email extraction logic that looks for multiple claim types
- Provider-specific claim handling for GitHub (`urn:github:email`, `urn:github:primary_email`)
- Fallback to standard email claims

#### Backend (`ClientPrincipal.cs`):
- Enhanced `GetEffectiveUserDetails()` method with improved email parsing
- Added `GetEmailFromClaims()` method for provider-specific email extraction
- Email validation to ensure valid email addresses

#### Backend (`AuthUserManagement.cs`):
- Already configured to set user email as preferred email during registration
- Will automatically use the enhanced email from authentication

## Testing the Configuration

1. Deploy the updated code to your Azure App Service
2. Navigate to your application
3. Attempt to log in with GitHub
4. Check that the user's email is properly captured and set as the preferred email

## Troubleshooting

### Email Not Available
If users still don't have email addresses after configuration:

1. **Check GitHub Privacy Settings**: Users may have their email set to private in GitHub settings
2. **Verify Scope Configuration**: Ensure the `user:email` scope is properly configured in Azure
3. **Check Claims**: Use the application's debug logging to see what claims are received from GitHub

### Authentication Issues
If authentication fails:

1. **Verify Callback URL**: Ensure the GitHub OAuth app callback URL matches Azure's expected format
2. **Check Client ID/Secret**: Verify the GitHub OAuth app credentials in Azure App Service settings
3. **Review Azure Logs**: Check the App Service logs for authentication errors

## Security Considerations

- The `user:email` scope only provides access to the user's email addresses, not other sensitive data
- Users can still choose to make their email private in GitHub, which may result in no email being available
- The application handles cases where email is not available gracefully

## Claims Available with `user:email` Scope

When the `user:email` scope is properly configured, GitHub provides the following email-related claims:

- `email`: Primary email address (if public)
- `urn:github:email`: GitHub-specific email claim
- `urn:github:primary_email`: Primary email address
- Standard claims like `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress`

The enhanced code checks all these claim types to maximize the chance of retrieving the user's email address.