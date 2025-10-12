# feedbackflow.playwrighttests

This project contains a minimal Playwright test using MSTest that navigates to the running web app at http://localhost:5265 and takes a screenshot.

Quick start

1. Build the solution and restore packages:

   dotnet restore
   dotnet build

2. Install Playwright browsers for the built project. After building, run the generated PowerShell script from the build output (replace net9.0 if necessary):

   pwsh ./bin/Debug/net9.0/playwright.ps1 install

3. Make sure the web app is running in debug mode on http://localhost:5265.

4. Run the tests:

   dotnet test --filter FullyQualifiedName~FeedbackFlow.PlaywrightTests.PlaywrightLaunchTests

Notes

- The project uses MSTest base classes from Microsoft.Playwright.MSTest.
- Playwright will run browsers in headless mode by default. To see the browser UI, override launch options in tests.
