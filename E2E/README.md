# Requirements
Building or rebuilding the solution should generate the playwright-powershellscript
This must be run first, on the machine doing the E2E-testing

./bin/Debug/net8.0/playwright.ps1 install

NOTE: If you get an error related to script running being disabled, do this to permit it for the current session:
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

The app you're testing should also be running.
For this project, you can compose up the entire project - this should result in the MVC running on the appropriate port.

# Running the E2E tests
To run the test, run the following command from the E2E directory:

dotnet test

