# Custom Logout Screen Implementation

## Overview
Implement a custom logout experience that leverages Azure App Service's built-in authentication system while providing a branded, user-friendly logout flow.

## Current State
- Application uses Azure App Service authentication with `.auth/logout` endpoint
- Default behavior redirects to system-generated `.auth/logout/complete` page
- Need custom logout experience that aligns with FeedbackFlow branding

## Research Findings

### Azure App Service Logout Behavior
- Built-in logout endpoint: `/.auth/logout`
- Can customize redirect destination using `post_logout_redirect_uri` parameter
- System-generated `.auth/logout/complete` page cannot be directly customized
- Can bypass default page by always using redirect parameter

### Logout URL Structure
```
/.auth/logout?post_logout_redirect_uri=/custom-goodbye-page
```

This approach:
- Clears authentication cookies
- Removes tokens from token store  
- Redirects user to custom page instead of default completion page

## Implementation Plan

### 1. Custom Logout Page Component
Create a new Blazor page component at `/Components/Pages/Logout.razor`:

```
@page "/logout"
@namespace FeedbackWebApp.Components.Pages

<PageTitle>Signing Out - FeedbackFlow</PageTitle>

<!-- Custom logout experience with:
- FeedbackFlow branding
- Confirmation message
- Optional feedback form
- Links to sign back in
- Session cleanup status
-->
```

### 2. Logout Service Integration
Add logout functionality to existing authentication services:

```csharp
// In Services/AuthenticationService.cs or similar
public class LogoutService
{
    public string GetLogoutUrl(string? customRedirectUri = null)
    {
        var redirectUri = customRedirectUri ?? "/logout";
        return $"/.auth/logout?post_logout_redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }
}
```

### 3. Navigation Updates
Update navigation components to use custom logout URL:

```html
<!-- Replace existing logout links -->
<a href="@logoutService.GetLogoutUrl()" class="nav-link">
    <i class="bi bi-box-arrow-right"></i> Sign Out
</a>
```

### 4. Advanced Features (Optional)

#### Auto-logout on Inactivity
```javascript
// Client-side inactivity detection
let inactivityTimer;
const INACTIVITY_LIMIT = 15 * 60 * 1000; // 15 minutes

function resetInactivityTimer() {
    clearTimeout(inactivityTimer);
    inactivityTimer = setTimeout(() => {
        window.location.href = '/.auth/logout?post_logout_redirect_uri=/session-expired';
    }, INACTIVITY_LIMIT);
}

// Reset timer on user activity
document.addEventListener('click', resetInactivityTimer);
document.addEventListener('keypress', resetInactivityTimer);
```

#### Enhanced Logout with Hints
For Microsoft Entra ID, add logout hints to avoid account chooser:

```csharp
public string GetLogoutUrlWithHint(string userEmail, string? redirectUri = null)
{
    var redirect = redirectUri ?? "/logout";
    return $"/.auth/logout?post_logout_redirect_uri={Uri.EscapeDataString(redirect)}&logout_hint={Uri.EscapeDataString(userEmail)}";
}
```

## File Structure

### New Files
- `/Components/Pages/Logout.razor` - Custom logout page
- `/Components/Pages/Logout.razor.css` - Logout page styles
- `/Components/Pages/SessionExpired.razor` - Inactivity logout page
- `/Services/LogoutService.cs` - Logout URL generation service
- `/Services/ILogoutService.cs` - Logout service interface

### Modified Files
- `/Components/Layout/NavMenu.razor` - Update logout links
- `/Components/Layout/MainLayout.razor` - Add inactivity detection (optional)
- `/Program.cs` - Register logout service
- `/Components/_Imports.razor` - Add logout service using statements

## User Experience Flow

1. **User clicks "Sign Out"**
   - Navigates to custom logout URL with redirect parameter
   - Azure App Service clears authentication state
   - User redirected to custom `/logout` page

2. **Custom Logout Page**
   - Displays branded goodbye message
   - Shows session cleanup confirmation
   - Provides options to sign back in
   - Optional: Brief feedback form about session

3. **Session Expiry (Optional)**
   - JavaScript detects inactivity
   - Automatically triggers logout with custom redirect
   - Shows session expired page with re-login options

## Benefits

- **Branded Experience**: Custom logout aligns with FeedbackFlow design
- **User Feedback**: Opportunity to collect logout reasons/feedback
- **Security**: Proper session cleanup with visual confirmation
- **Flexibility**: Can extend with additional logout logic
- **Analytics**: Track logout patterns and reasons

## Implementation Priority

1. **Phase 1**: Basic custom logout page with redirect
2. **Phase 2**: Enhanced styling and user messaging
3. **Phase 3**: Optional inactivity detection
4. **Phase 4**: Logout analytics and feedback collection

## Technical Considerations

- Ensure logout works across all authentication providers (GitHub, Microsoft, etc.)
- Test logout behavior in both development and production environments
- Consider mobile responsiveness for logout experience
- Validate that custom redirect doesn't interfere with authentication flow
- Document logout URL generation for other developers

## Related Documentation
- [Azure App Service Authentication Customization](https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-customize-sign-in-out)
- [FeedbackFlow Authentication Architecture](../docs/authentication-flow.md) (if exists)
