namespace KoromoEventScript.Cli.Commands;

public enum CliExitCode
{
    Success = 0,
    GeneralError = 1,
    CommandLineError = 2,
    SyntaxError = 3,
    CompileError = 4,
    RuntimeError = 5,
    FileOrDirectoryError = 6,
    RuntimeLaunchError = 7,
    WarningsAsErrors = 9,
}
