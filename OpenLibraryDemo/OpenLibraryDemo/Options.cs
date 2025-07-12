using CommandLine;

namespace OpenLibraryDemo;

public class Options
{
    [Option('d', "date", Required = false, HelpText = "Optional date parameter.")]
    public DateTime? Date { get; set; }

    [Option('r', "rating", Required = false, HelpText = "Optional rating parameter.")]
    public int? Rating { get; set; }

    [Option('w', "work key", Required = false, HelpText = "Optional work key parameter.")]
    public string WorkKey { get; set; }

    // Omitting long name, defaults to name of property, ie "--verbose"
    [Option(
        Default = false,
        HelpText = "Prints all messages to standard output.")]
    public bool Verbose { get; set; }

    [Option("stdin",
        Default = false,
        HelpText = "Read from stdin")]
    public bool stdin { get; set; }
}