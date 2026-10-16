using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace MaevexTester;

/// <summary>
/// Banc de test : joue le rôle du programme SIMPL du slot 1 face à MatroxMaevex (slot 2).
/// Commande console : maevextest host|login|password|method|path|body
/// </summary>
public class ControlSystem : CrestronControlSystem
{
    private EthernetIntersystemCommunications _eisc;
    private readonly ManualResetEventSlim _done = new();
    private string _result = "";
    private readonly List<string> _trace = new();

    public ControlSystem() : base()
    {
        try
        {
            _eisc = new EthernetIntersystemCommunications(0xA0, "127.0.0.2", this) { Description = "EISC tester" };
            _eisc.SigChange += Eisc_SigChange;
            _eisc.OnlineStatusChange += (d, a) => CrestronConsole.PrintLine("TESTER EISC A0 : {0}", a.DeviceOnLine ? "online" : "offline");
            _eisc.Register();
            CrestronConsole.AddNewConsoleCommand(Test, "maevextest", "host|login|password|method|path|body", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(_ => CrestronConsole.ConsoleCommandResponse("EISC online = {0}", _eisc.IsOnline), "maevexstatus", "État EISC", ConsoleAccessLevelEnum.AccessOperator);
        }
        catch (Exception e)
        {
            ErrorLog.Error("Tester constructor: {0}", e.Message);
        }
    }

    private void Eisc_SigChange(BasicTriList dev, SigEventArgs args)
    {
        var sig = args.Sig;
        lock (_trace)
        {
            _trace.Add(sig.Type switch
            {
                eSigType.Bool => $"D{sig.Number}={(sig.BoolValue ? 1 : 0)}",
                eSigType.UShort => $"A{sig.Number}={sig.UShortValue}",
                _ => $"S{sig.Number}({(sig.StringValue ?? "").Length} car.)",
            });
        }

        if (sig.Type == eSigType.Bool && sig.BoolValue && (sig.Number == 2 || sig.Number == 3))
        {
            // Lecture sur le front, comme le ferait SIMPL.
            _result = string.Format("{0} | HTTP {1} | erreur='{2}' | réponse ({3} car.) = {4}",
                sig.Number == 2 ? "SUCCESS" : "ERROR",
                _eisc.UShortOutput[1].UShortValue,
                _eisc.StringOutput[2].StringValue,
                (_eisc.StringOutput[1].StringValue ?? "").Length,
                Truncate(_eisc.StringOutput[1].StringValue, 300));
            _done.Set();
        }
    }

    private static string Truncate(string s, int n) => s == null ? "" : s.Length <= n ? s : s[..n] + "…";

    private void Test(string args)
    {
        if (!_eisc.IsOnline)
        {
            CrestronConsole.ConsoleCommandResponse("EISC offline");
            return;
        }

        var f = (args ?? "").Split('|', 6);
        Array.Resize(ref f, 6);
        for (uint i = 0; i < 6; i++)
        {
            _eisc.StringInput[i + 1].StringValue = f[i] ?? "";
        }
        Thread.Sleep(100);

        _done.Reset();
        lock (_trace) _trace.Clear();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _eisc.BooleanInput[1].Pulse();

        var ok = _done.Wait(TimeSpan.FromSeconds(15));
        Thread.Sleep(300); // laisse retomber Busy
        string trace;
        lock (_trace) trace = string.Join(" ", _trace);
        CrestronConsole.ConsoleCommandResponse("{0} ({1} ms)\r\nSignaux : {2}\r\n",
            ok ? _result : "AUCUNE IMPULSION SUCCESS/ERROR en 15 s", sw.ElapsedMilliseconds, trace);
    }
}
