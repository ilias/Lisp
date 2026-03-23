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

    public static void WriteDisassemblySource(string indent, int depth, string text)
    {
        string nestedIndent = indent + new string(' ', Math.Max(0, depth) * 2) + "  ";
        WriteLineSegments(
        [
            new(nestedIndent + ";; ", ConsoleColor.DarkGray),
            new(text, ConsoleColor.Gray),
        ]);
    }

    public static void WriteDisassemblyHeader(string indent, string name, int instructionCount)
    {
        WriteLineSegments(
        [
            new Segment(indent, null),
            new Segment("=== ", ConsoleColor.DarkGreen),
            new Segment(name, ConsoleColor.Green),
            new Segment("  (", ConsoleColor.DarkGreen),
            new Segment(instructionCount.ToString(), ConsoleColor.Yellow),
            new Segment(" instructions) ===", ConsoleColor.DarkGreen),
        ]);
    }
}