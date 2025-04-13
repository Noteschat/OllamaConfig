public class Logger
{
    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"INFO    | {message}");
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"WARN    | {message}");
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR   | {message}");
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void Fatal(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"FATAL   | {message}");
        Console.ForegroundColor = ConsoleColor.White;
    }

    public static void Message(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"MESSAGE | {message}");
        Console.ForegroundColor = ConsoleColor.White;
    }
}
