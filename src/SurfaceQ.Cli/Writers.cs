using System.CommandLine;

namespace SurfaceQ.Cli;

internal readonly record struct Writers(Action<string> Info, Action<string> Trace, Action<string> Warn, Action<string> Error)
{
    public static Writers For(IConsole console, string verbosity)
    {
        Action<string> toStdout = message => console.Out.Write(message + Environment.NewLine);
        Action<string> toStderr = message => console.Error.Write(message + Environment.NewLine);
        Action<string> noop = _ => { };

        var normalized = (verbosity ?? "normal").ToLowerInvariant();
        var quiet = normalized == "quiet";
        var diagnostic = normalized == "diagnostic";

        var info = quiet ? noop : toStdout;
        var trace = diagnostic ? toStdout : noop;
        return new Writers(info, trace, toStderr, toStderr);
    }
}
