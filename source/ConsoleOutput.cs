namespace Lisp;

public static class ConsoleOutput
{
    public readonly record struct Segment(string Text, ConsoleColor? Color = null);

    public static bool Enabled { get; set; } = true;

    private static bool UseColor => Enabled && !Console.IsOutputRedirected;

    private static void WriteColored(string text, ConsoleColor color, bool newline)
    {
        if (!UseColor)
        {
            if (newline) Console.WriteLine(text);
            else Console.Write(text);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newline) Console.WriteLine(text);
        else Console.Write(text);
        Console.ForegroundColor = previous;
    }

    public static void Write(string text, ConsoleColor color)
        => WriteColored(text, color, newline: false);

    public static void WriteLine(string text, ConsoleColor color)
        => WriteColored(text, color, newline: true);

    public static void WriteSegments(IEnumerable<Segment> segments)
    {
        if (!UseColor)
        {
            foreach (var segment in segments)
                Console.Write(segment.Text);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            foreach (var segment in segments)
            {
                Console.ForegroundColor = segment.Color ?? previous;
                Console.Write(segment.Text);
            }
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }

    public static void WriteLineSegments(IEnumerable<Segment> segments)
    {
        WriteSegments(segments);
        Console.WriteLine();
    }

    public static void WriteResult(object? value) =>
        WriteLine(Util.Dump(value), ConsoleColor.Yellow);

    public static void WriteTrace(string text) =>
        WriteLine(text, ConsoleColor.DarkYellow);

    public static void WriteStats(string text) =>
        WriteLine(text, ConsoleColor.Cyan);

    public static void WriteStatsTotal(string text) =>
        WriteLine(text, ConsoleColor.DarkCyan);

    public static void WriteDisassemblyHeader(string text) =>
        WriteLine(text, ConsoleColor.Green);

    public static void WriteDisassemblyLine(string text) =>
        WriteLine(text, ConsoleColor.DarkGreen);

    public static int GetDisassemblySourceWidth(string indent, int depth)
    {
        string nestedIndent = indent + new string(' ', Math.Max(0, depth) * 2) + "  ";
        string prefix = nestedIndent + ";; ";
        int windowWidth;
        try
        {
            windowWidth = Console.WindowWidth;
        }
        catch
        {
            windowWidth = 100;
        }

        if (windowWidth <= 0) windowWidth = 100;
        return Math.Max(24, windowWidth - prefix.Length - 1);
    }

    public static void WriteDisassemblySource(string indent, int depth, IEnumerable<string> lines)
    {
        string nestedIndent = indent + new string(' ', Math.Max(0, depth) * 2) + "  ";
        string prefix = nestedIndent + ";; ";

        foreach (string line in lines)
        {
            WriteLineSegments(
            [
                new(prefix, ConsoleColor.Gray),
                new(line, ConsoleColor.DarkGray),
            ]);
        }
    }

    public static void WriteDisassemblyHeader(string indent, string name, int instructionCount)
    {
        WriteLineSegments(
        [
            new Segment(indent, null),
            new Segment("=== ", ConsoleColor.DarkGray),
            new Segment(name, ConsoleColor.Gray),
            new Segment("  (", ConsoleColor.DarkGray),
            new Segment(instructionCount.ToString(), ConsoleColor.DarkYellow),
            new Segment(" instructions) ===", ConsoleColor.DarkGray),
        ]);
    }
}