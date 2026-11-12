namespace TrKarekod.Cli;

internal static class Program
{
    public static int Main(string[] args) => CliApp.Run(args, Console.In, Console.Out, Console.Error);
}
