internal static class AppPrompts
{
    public static string Prompt(string message) => ConsoleHelper.ReadLineWithPrompt(message);

    public static char[] PromptPassword(string message) => ConsoleHelper.ReadPasswordWithPrompt(message);

    public static bool PromptForUnsavePreference()
    {
        while (true)
        {
            var response = Prompt("Would you like to unsave the post after download? (y/n): ").Trim().ToLowerInvariant();

            if (response == "y")
            {
                return true;
            }

            if (response == "n")
            {
                return false;
            }

            Console.WriteLine($"{response} is not a valid input.");
        }
    }
}
