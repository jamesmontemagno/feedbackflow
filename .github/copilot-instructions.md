# Copilot Instructions

The github repo is jamesmontemagno/feedbackflow and the primary branch that I work off of is main

## Blazor
- Always add component-specific CSS in a corresponding .razor.css file
- When creating a new component, automatically create a matching .razor.css file
- Ignore warnings in Blazor components (they are often false positives)
- Use scoped CSS through the .razor.css pattern instead of global styles
- Make sure light and dark theme are respected throughout by never using hard coded rgb or hex but that they are always defined in the main css

## Code Style
- Prefer async/await over direct Task handling
- Use nullable reference types
- Use var over explicit type declarations 
- Always implement IDisposable when dealing with event handlers or subscriptions
- Prefer using async/await for asynchronous operations
- use latest C# features (e.g., records, pattern matching)
- Use consistent naming conventions (PascalCase for public members, camelCase for private members)
- use meaningful names for variables, methods, and classes
- Use dependency injection for services and components
- Use interfaces for service contracts
- use file scoped namespaces in c# and are pascal cased

## Component Structure
- Keep components small and focused
- Extract reusable logic into services
- Use cascading parameters sparingly
- Prefer component parameters over cascading values

## Error Handling
- Use try-catch blocks in event handlers
- Implement proper error boundaries
- Display user-friendly error messages
- Log errors appropriately

## Performance
- Implement proper component lifecycle methods
- Use @key directive when rendering lists
- Avoid unnecessary renders
- Use virtualization for large lists

## Testing
- Write unit tests for complex component logic only if i ask for tests
- Test error scenarios
- Mock external dependencies
- Use MSTest for component testing
- Create tests in the feedbackflow.tests project

## Documentation
- Document public APIs
- Include usage examples in comments
- Document any non-obvious behavior
- Keep documentation up to date

## Security
- Always validate user input

## Accessibility
- Use semantic HTML
- Include ARIA attributes where necessary
- Ensure keyboard navigation works

## File Organization
- Keep related files together
- Use meaningful file names
- Follow consistent folder structure
- Group components by feature when possible