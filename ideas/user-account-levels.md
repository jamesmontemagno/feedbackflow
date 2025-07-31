# User Account Levels - Implementation Plan

## Overview
Implement a tiered user account system with usage limits and tracking to enable subscription monetization while providing fair usage for free users.

## Account Tiers

### Free Tier
- **Monthly Analysis Limit**: 10 analyses per month
- **Report Tracking Limit**: 1 active reports
- **Feed Query Limit**: 20 feed queries per month
- **Features**: Basic analysis, no support
- **Price**: Free

### Pro Tier
- **Monthly Analysis Limit**: 75 analyses per month
- **Report Tracking Limit**: 5 active reports
- **Feed Query Limit**: 200 feed queries per month
- **Features**: All Free features + priority processing + basic support
- **Price**: $9.99/month

### Pro+ Tier
- **Monthly Analysis Limit**: 300 analyses per month
- **Report Tracking Limit**: 25 active reports
- **Feed Query Limit**: 1000 feed queries per month
- **Features**: All Pro features + email notifications, advanced analytics
- **Price**: $29.99/month

## Technical Architecture

### 1. User Management System

#### User Model Extension
```csharp
public class UserAccount
{
    public string UserId { get; set; }
    public AccountTier Tier { get; set; } = AccountTier.Free;
    public DateTime SubscriptionStart { get; set; }
    public DateTime? SubscriptionEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastResetDate { get; set; }
    
    // Usage tracking (resets monthly)
    public int AnalysesUsed { get; set; }
    public int FeedQueriesUsed { get; set; }
    public int ActiveReports { get; set; }
    
    // Limits (configurable via environment)
    public int AnalysisLimit { get; set; }
    public int ReportLimit { get; set; }
    public int FeedQueryLimit { get; set; }
}

public enum AccountTier
{
    Free = 0,
    Pro = 1,
    ProPlus = 2
}
```

#### Usage Tracking Model
```csharp
public class UsageRecord
{
    public string UserId { get; set; }
    public DateTime Date { get; set; }
    public UsageType Type { get; set; }
    public string ResourceId { get; set; } // Analysis ID, Report ID, etc.
    public string Details { get; set; } // JSON metadata
}

public enum UsageType
{
    Analysis,
    FeedQuery,
    ReportCreated,
    ReportDeleted
}
```

### 2. Configuration Management

#### Environment Variables
```json
{
  "AccountTiers": {
    "Free": {
      "AnalysisLimit": "10",
      "ReportLimit": "1", 
      "FeedQueryLimit": "20"
    },
    "Pro": {
      "AnalysisLimit": "75",
      "ReportLimit": "5",
      "FeedQueryLimit": "200"
    },
    "ProPlus": {
      "AnalysisLimit": "300",
      "ReportLimit": "25",
      "FeedQueryLimit": "1000"
    }
  },
  "Usage": {
    "ResetPeriodDays": "30",
    "EnableTracking": "true"
  },
  "Development": {
    "UserId": "dev-user-demo",
    "Email": "demo@feedbackflow.dev",
    "Name": "Demo Development User"
  }
}
```

Controlled by the backend and use these as defaults that are pulled.

#### Configuration Service
```csharp
public interface IAccountLimitsService
{
    AccountLimits GetLimitsForTier(AccountTier tier);
    bool IsWithinLimits(string userId, UsageType usageType);
    Task<UsageValidationResult> ValidateUsageAsync(string userId, UsageType usageType);
    Task TrackUsageAsync(string userId, UsageType usageType, string resourceId = null);
}
```

### 3. Backend Implementation

#### Azure Functions Changes

##### New Functions
- **GetUserAccount**: Retrieve user account info and current usage
- **ValidateUsage**: Check if user can perform an action
- **TrackUsage**: Record usage for billing/limits
- **ResetMonthlyUsage**: Timer function to reset usage counters
- **UpgradeAccount**: Handle subscription upgrades

##### Existing Function Modifications
- **All Analysis Functions**: Add usage validation before processing
- **Feed Functions**: Track and validate feed query usage
- **Report Functions**: Validate report limits before creation

#### Usage Validation Middleware
```csharp
public class UsageValidationAttribute : Attribute
{
    public UsageType UsageType { get; set; }
    public bool Required { get; set; } = true;
}

// Applied to functions like:
[UsageValidation(UsageType = UsageType.Analysis)]
public async Task<HttpResponseData> AnalyzeComments(...)
```

#### Error Responses
```csharp
public class UsageLimitExceededResponse
{
    public string ErrorCode { get; set; } = "USAGE_LIMIT_EXCEEDED";
    public string Message { get; set; }
    public UsageType LimitType { get; set; }
    public int CurrentUsage { get; set; }
    public int Limit { get; set; }
    public DateTime ResetDate { get; set; }
    public AccountTier CurrentTier { get; set; }
    public string UpgradeUrl { get; set; }
}
```

### 4. Storage Strategy

#### Azure Table Storage
- **UserAccounts** table: Store user account information
- **UsageRecords** table: Store detailed usage history
- **SubscriptionEvents** table: Track subscription changes

#### Partition Strategy
- UserAccounts: PartitionKey = UserId
- UsageRecords: PartitionKey = UserId, RowKey = Date_UsageType_Id
- Monthly rollup for efficient querying

### 5. Frontend Implementation

#### Blazor Components

##### Usage Dashboard Component
```razor
@namespace FeedbackFlow.Components.Account

<div class="usage-dashboard">
    <div class="tier-badge tier-@Model.Tier.ToString().ToLower()">
        @Model.Tier Tier
    </div>
    
    <div class="usage-metrics">
        <UsageMetric 
            Label="Analyses" 
            Used="@Model.AnalysesUsed" 
            Limit="@Model.AnalysisLimit" />
        <UsageMetric 
            Label="Reports" 
            Used="@Model.ActiveReports" 
            Limit="@Model.ReportLimit" />
        <UsageMetric 
            Label="Feed Queries" 
            Used="@Model.FeedQueriesUsed" 
            Limit="@Model.FeedQueryLimit" />
    </div>
    
    @if (Model.Tier == AccountTier.Free)
    {
        <UpgradePrompt />
    }
</div>
```

##### Usage Limit Modal
```razor
<div class="modal fade" id="usageLimitModal">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5>Usage Limit Reached</h5>
            </div>
            <div class="modal-body">
                <p>You've reached your @LimitType limit for this month.</p>
                <div class="upgrade-options">
                    @foreach (var tier in AvailableUpgrades)
                    {
                        <div class="tier-option">
                            <h6>@tier.Name</h6>
                            <p>@tier.Description</p>
                            <button class="btn btn-primary" @onclick="() => UpgradeToTier(tier)">
                                Upgrade for $@tier.Price/month
                            </button>
                        </div>
                    }
                </div>
            </div>
        </div>
    </div>
</div>
```

#### Error Handling
- Intercept usage limit errors in HTTP responses
- Show appropriate upgrade prompts
- Graceful degradation of features

### 6. Service Integration Points

#### Web App Service Changes
```csharp
public interface IUsageTrackingService
{
    Task<bool> CanPerformActionAsync(UsageType usageType);
    Task TrackActionAsync(UsageType usageType, string resourceId = null);
    Task<UserAccount> GetUserAccountAsync();
    Task RefreshUsageLimitsAsync();
}
```

#### Frontend Service Registration
```csharp
// In Program.cs
builder.Services.AddScoped<IUsageTrackingService, UsageTrackingService>();
builder.Services.AddScoped<IAccountUpgradeService, AccountUpgradeService>();
```

### 7. Database Schema

#### Azure Table Storage Tables

##### UserAccounts Table
```
PartitionKey: UserId
RowKey: "account"
Properties:
- Tier (int)
- SubscriptionStart (DateTime)
- SubscriptionEnd (DateTime?)
- LastResetDate (DateTime)
- AnalysesUsed (int)
- FeedQueriesUsed (int)
- ActiveReports (int)
- AnalysisLimit (int)
- ReportLimit (int)
- FeedQueryLimit (int)
```

##### UsageRecords Table
```
PartitionKey: UserId
RowKey: Timestamp_UsageType_ResourceId
Properties:
- UsageType (int)
- ResourceId (string)
- Details (string) // JSON
- Timestamp (DateTime)
```

### 8. Migration Strategy

#### Phase 1: Core Infrastructure
1. Create user account models and services
2. Add usage tracking to Azure Functions
3. Implement configuration management
4. Create storage tables

#### Phase 2: Frontend Integration
1. Add usage dashboard to web app
2. Implement error handling for limits
3. Create upgrade flow UI
4. Add usage indicators throughout app

#### Phase 3: Advanced Features
1. Email notifications for Pro+
2. Advanced analytics dashboard
3. Subscription management
4. Payment integration

### 9. Testing Strategy

#### Unit Tests
- Account limit validation logic
- Usage tracking accuracy
- Configuration service behavior
- Monthly reset functionality

#### Integration Tests
- End-to-end usage flow
- Limit enforcement across all endpoints
- Error handling scenarios
- Subscription upgrade flow

### 10. Security Considerations

#### Authentication
- User identification through existing auth system
- Secure subscription status validation
- Rate limiting to prevent abuse

#### Data Protection
- Encrypt sensitive subscription data
- Audit trail for account changes
- Secure payment processing (future)

### 11. Monitoring & Analytics

#### Key Metrics
- Usage by tier and feature
- Conversion rates from Free to paid
- Feature adoption rates
- Limit breach attempts

#### Dashboards
- Real-time usage monitoring
- Subscription health metrics
- Revenue tracking (future)

### 12. Implementation Timeline

#### Week 1-2: Foundation
- User account models
- Basic usage tracking
- Configuration system

#### Week 3-4: Backend Integration
- Function validation middleware
- Storage implementation
- Error handling

#### Week 5-6: Frontend Integration
- Usage dashboard
- Limit enforcement UI
- Upgrade prompts

#### Week 7-8: Testing & Polish
- Comprehensive testing
- Performance optimization
- Documentation

### 13. Future Enhancements

#### Payment Integration
- Stripe or similar payment processor
- Automated subscription management
- Invoice generation

#### Advanced Features
- Custom tier creation for enterprise
- API access for Pro+ users
- White-label options

#### Analytics
- Detailed usage analytics
- Predictive usage modeling
- Churn prediction

## Impact Assessment

### Affected Components

#### shareddump (Shared.csproj)
- New models: UserAccount, UsageRecord, AccountLimits
- New services: IAccountLimitsService, IUsageTrackingService
- New utilities: AccountTierUtils, UsageValidationUtils

#### feedbackfunctions (Functions.csproj)
- Usage validation middleware
- New functions for account management
- Modified existing functions for usage tracking
- Timer functions for monthly resets

#### feedbackwebapp (WebApp.csproj)
- Usage dashboard components
- Upgrade flow pages
- Error handling for usage limits
- Usage indicators in existing components

#### FeedbackFlow.AppHost (AppHost.csproj)
- Configuration for new environment variables
- Service registration for account services

#### Tests
- Unit tests for all new services
- Integration tests for usage flows
- Performance tests for usage validation

### Development Approach

1. **Start with shareddump**: Create models and interfaces
2. **Backend implementation**: Add usage tracking to functions
3. **Frontend integration**: Create UI components and flows
4. **Testing**: Comprehensive test coverage
5. **Configuration**: Environment-based limits
6. **Documentation**: Update all relevant docs

This plan provides a comprehensive foundation for implementing user account levels while maintaining the existing architecture and following established patterns in the codebase.
