using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.EthernetCommunication;

namespace MatroxMaevex;

/// <summary>
/// Liaison EISC (Ethernet Intersystem Communications) avec le programme SIMPL
/// tournant sur le même processeur.
///
/// Les deux programmes doivent déclarer le même IP-ID avec l'adresse 127.0.0.2
/// (adresse interne du processeur pour l'intersystem entre slots).
///
/// Sens des signaux :
/// * Set*() côté C#  -> sorties du symbole EISC côté SIMPL
/// * entrées du symbole EISC côté SIMPL -> événements *Changed côté C#
/// </summary>
public class Intersystem
{
    public const string LocalAddress = "127.0.0.2";

    private readonly EthernetIntersystemCommunications _eisc;

    public event Action<uint, bool> BoolChanged;
    public event Action<uint, ushort> UShortChanged;
    public event Action<uint, string> StringChanged;
    public event Action<bool> OnlineChanged;

    public bool IsOnline => _eisc.IsOnline;

    public Intersystem(uint ipId, CrestronControlSystem controlSystem)
    {
        _eisc = new EthernetIntersystemCommunications(ipId, LocalAddress, controlSystem)
        {
            Description = "EISC SIMPL"
        };
        _eisc.SigChange += Eisc_SigChange;
        _eisc.OnlineStatusChange += Eisc_OnlineStatusChange;
    }

    /// <summary>
    /// À appeler dans le constructeur de ControlSystem.
    /// </summary>
    public void Register()
    {
        var result = _eisc.Register();
        if (result != eDeviceRegistrationUnRegistrationResponse.Success)
        {
            ErrorLog.Error("EISC IP-ID {0:X2} : échec d'enregistrement ({1})", _eisc.ID, result);
        }
    }

    public void SetBool(uint join, bool value) => _eisc.BooleanInput[join].BoolValue = value;

    public void PulseBool(uint join) => _eisc.BooleanInput[join].Pulse();

    public void SetUShort(uint join, ushort value) => _eisc.UShortInput[join].UShortValue = value;

    public void SetString(uint join, string value) => _eisc.StringInput[join].StringValue = value;

    /// <summary>
    /// Dernière valeur reçue de SIMPL sur ce serial.
    /// </summary>
    public string GetString(uint join) => _eisc.StringOutput[join].StringValue ?? "";

    private void Eisc_SigChange(BasicTriList currentDevice, SigEventArgs args)
    {
        var sig = args.Sig;
        switch (sig.Type)
        {
            case eSigType.Bool:
                BoolChanged?.Invoke(sig.Number, sig.BoolValue);
                break;
            case eSigType.UShort:
                UShortChanged?.Invoke(sig.Number, sig.UShortValue);
                break;
            case eSigType.String:
                StringChanged?.Invoke(sig.Number, sig.StringValue);
                break;
        }
    }

    private void Eisc_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
    {
        CrestronConsole.PrintLine("EISC IP-ID {0:X2} : {1}", _eisc.ID, args.DeviceOnLine ? "online" : "offline");
        OnlineChanged?.Invoke(args.DeviceOnLine);
    }
}
