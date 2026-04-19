using System.Diagnostics;

namespace SurfaceQ.Sidecar;

public sealed class SidecarClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;

    public SidecarClient(string scriptPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(scriptPath);
        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start node sidecar");
        _process = process;
        _stdin = process.StandardInput;
        _stdout = process.StandardOutput;
    }

    public string Send(string jsonLine)
    {
        _stdin.WriteLine(jsonLine);
        _stdin.Flush();
        var response = _stdout.ReadLine()
            ?? throw new InvalidOperationException("sidecar closed stdout without responding");
        return response;
    }

    public void Dispose()
    {
        try
        {
            _stdin.Close();
        }
        catch
        {
        }
        try
        {
            if (!_process.WaitForExit(2000))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        _process.Dispose();
    }
}
