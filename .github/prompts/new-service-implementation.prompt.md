---
mode: agent
tools: ['edit', 'runNotebooks', 'search', 'new', 'runCommands', 'runTasks', 'usages', 'vscodeAPI', 'think', 'problems', 'changes', 'testFailure', 'openSimpleBrowser', 'fetch', 'githubRepo', 'extensions', 'todos', 'runTests']
---


# New Service Implementation Guide

This guide outlines the steps needed to implement a new service feature across the FeedbackFlow solution. Use this as a checklist and reference for adding any new service, whether for Feedback or ContentFeed. Adjust steps as needed for your specific scenario.

---

## 0. Determine Service Type
- Is this a **Feedback** service (analyzes or aggregates feedback/comments for a single item or thread)?
  - Place models/services under `shareddump/Models/{ServiceName}/` and integrate with `FeedbackFunctions` and `feedbackwebapp/Services/Feedback`.
- Is this a **ContentFeed** service (provides lists of trending/recent content, not just comments)?
  - Place models/services under `shareddump/Models/{ServiceName}/` and integrate with `ContentFeedFunctions` and `feedbackwebapp/Services/ContentFeed`.

---

## 1. Shared Models and Types
- [ ] Add new models to `shareddump/Models/{ServiceName}/`.
- [ ] Document model properties and relationships.
- [ ] Add JSON serialization/deserialization support in `shareddump/Json/` if needed.

## 2. Service-Specific Data Fetcher
- [ ] Implement a service in `shareddump/Models/{ServiceName}/` to fetch and transform data (API, RSS, etc.).
- [ ] Use async/await, nullable reference types, and error handling.
- [ ] Add logic for parent/child relationships if needed (e.g., nested comments).
- [ ] Add logging and exception handling.
- [ ] Add unit tests for transformation/validation logic.

## 3. Azure Functions Integration
- [ ] For Feedback: Add endpoint to `feedbackfunctions/FeedbackFunctions.cs`.
- [ ] For ContentFeed: Add endpoint to `feedbackfunctions/ContentFeedFunctions.cs`.
- [ ] Validate input, handle errors, and return structured JSON.
- [ ] Require API code via configuration for security.

## 4. MCP Service Integration (if needed)
- [ ] Add new service methods to `feedbackmcp/FeedbackFlowTool.cs`.
- [ ] Update `ApiConfiguration.cs` and service registration if needed.

## 5. Web Application Components
### 5.1 Service Layer
- [ ] Add a service interface in `feedbackwebapp/Services/Interfaces/` into `IFeedbackService`
- [ ] Implement the real service in `feedbackwebapp/Services/Feedback/` or `ContentFeed/` as appropriate.
- [ ] Implement a mock service in `feedbackwebapp/Services/Mock/`.
- [ ] Register the service in `FeedbackServiceProvider` or `ContentFeedServiceProvider`.
- [ ] Use configuration for API code if required.

### 5.2 Form Components
- [ ] Create a form component in `feedbackwebapp/Components/Feedback/Forms/` or `ContentFeed/Forms/`.
- [ ] Add a matching `.razor.css` file for scoped styles.
- [ ] Use shared validators and normalization logic if needed.
- [ ] Provide inline validation and help text.

### 5.3 Results Components
- [ ] Create a results component in `feedbackwebapp/Components/Feedback/Results/` or `ContentFeed/Results/`.
- [ ] Add a matching `.razor.css` file for scoped styles.
- [ ] Support recursive rendering for nested data if needed.

### 5.4 Integration
- [ ] Update `Home.razor` (for Feedback) or `ContentFeeds.razor` (for ContentFeed) to use the new service and components via the provider pattern.
- [ ] Add the new service to `SourceSelector.razor` or similar selector UI.
- [ ] Ensure state management and error boundaries are in place.

## 6. Testing
- [ ] Add unit tests for validators and service logic in `feedbackflow.tests/`.
- [ ] Ensure mock services return realistic data (including nested data if applicable).

## 7. Documentation
- [ ] Update this guide with any new patterns or lessons learned.
- [ ] Document configuration and usage in code comments.

## 8. Infrastructure
- [ ] Use dependency injection for service registration.
- [ ] Require API code in configuration for secure access.

## 9. Security
- [ ] Validate all user input (normalization and validation).
- [ ] Require API code for backend endpoints.

## 10. Performance
- [ ] Use async/await and proper disposal patterns.
- [ ] Ensure UI uses efficient rendering for nested or large data sets.

---

## Best Practices Checklist
- [ ] Follow async/await patterns
- [ ] Use nullable reference types
- [ ] Implement proper disposal patterns
- [ ] Follow consistent naming conventions
- [ ] Use dependency injection
- [ ] Keep components small and focused
- [ ] Use proper error handling
- [ ] Follow SOLID principles
- [ ] Ensure code reusability
- [ ] Maintain separation of concerns

## Deployment Considerations
- [ ] Update configuration files
- [ ] Review resource requirements
- [ ] Plan deployment strategy
- [ ] Consider backward compatibility
- [ ] Plan rollback strategy

## Notes
- Follow the existing project structure and patterns
- Keep components focused and maintainable
- Use proper error boundaries and fallbacks
- Consider both online and offline scenarios
- Test across different browsers and devices
- Consider localization requirements
- Follow established coding standards

This guide serves as a template for implementing new service features. Adjust steps as needed based on the specific requirements of your service implementation.