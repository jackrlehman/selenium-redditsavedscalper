# Reddit Saved Scalper

## About
Reddit Saved Scalper downloads static media from your Reddit saved tab with Selenium. This version has been converted from Python to a C# console application on .NET 8.

## Usage
1. Install the .NET 8 SDK and Google Chrome.
2. Restore dependencies and run the app:

<pre>
dotnet restore
dotnet run
</pre>

3. Enter your Reddit username and password when prompted.
4. Choose whether posts should be unsaved after their media is downloaded.
5. Downloaded files are written to a `Reddit Media` folder in the current working directory.

## Development
Requires .NET 8.

### Package
<pre>
dotnet add package Selenium.WebDriver --version 4.44.0
</pre>

### Build
<pre>
dotnet build
</pre>

Versioning format:
<pre>
v(MONTH).(DAY).(YEAR).(LETTER)
v10.18.2022.A
</pre>
