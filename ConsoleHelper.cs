using System.Buffers;

internal static class ConsoleHelper
{
    public static string ReadLineWithPrompt(string message)
    {
        System.Console.Write(message);
        return System.Console.ReadLine() ?? string.Empty;
    }

    public static char[] ReadPasswordWithPrompt(string message)
    {
        System.Console.Write(message);
        var buffer = ArrayPool<char>.Shared.Rent(32);
        var count = 0;

        try
        {
            while (true)
            {
                var key = System.Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    System.Console.WriteLine();
                    return buffer[..count].ToArray();
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (count == 0)
                    {
                        continue;
                    }

                    count -= 1;
                    buffer[count] = '\0';
                    System.Console.Write("\b \b");
                    continue;
                }

                if (char.IsControl(key.KeyChar))
                {
                    continue;
                }

                if (count == buffer.Length)
                {
                    var expandedBuffer = ArrayPool<char>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, expandedBuffer, buffer.Length);
                    Array.Clear(buffer, 0, buffer.Length);
                    ArrayPool<char>.Shared.Return(buffer);
                    buffer = expandedBuffer;
                }

                buffer[count] = key.KeyChar;
                count += 1;
                System.Console.Write('*');
            }
        }
        finally
        {
            Array.Clear(buffer, 0, buffer.Length);
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
