using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using CrestronThread = Crestron.SimplSharpPro.CrestronThread;

namespace MatroxMaevex;

public class ControlSystem : CrestronControlSystem
{
    /// <summary>
    /// IP-ID de l'EISC, identique à celui du symbole "Ethernet Intersystem
    /// Communications" du programme SIMPL (adresse 127.0.0.2).
    /// </summary>
    private const uint EiscIpId = 0xA0;

    private Intersystem _intersystem;
    private HttpRequester _httpRequester;

    /// <summary>
    /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
    /// Use the constructor to:
    /// * Initialize the maximum number of threads (max = 400)
    /// * Register devices
    /// * Register event handlers
    /// * Add console commands
    ///
    /// Please be aware that the constructor needs to exit quickly; if it doesn't
    /// exit in time, the SIMPL#Pro program will exit.
    ///
    /// You cannot send / receive data in the constructor
    /// </summary>
    public ControlSystem()
        : base()
    {
        try
        {
            CrestronThread.Thread.MaxNumberOfUserThreads = 20;

            CrestronEnvironment.ProgramStatusEventHandler += ControlSystem_ControllerProgramEventHandler;
            CrestronEnvironment.SystemEventHandler += ControlSystem_ControllerSystemEventHandler;
            CrestronEnvironment.EthernetEventHandler += ControlSystem_ControllerEthernetEventHandler;

            _intersystem = new Intersystem(EiscIpId, this);
            _intersystem.BoolChanged += (join, value) => CrestronConsole.PrintLine("EISC digital {0} = {1}", join, value);
            _intersystem.UShortChanged += (join, value) => CrestronConsole.PrintLine("EISC analog {0} = {1}", join, value);
            _intersystem.StringChanged += (join, value) => CrestronConsole.PrintLine("EISC serial {0} = {1}", join, value);
            _intersystem.Register();

            _httpRequester = new HttpRequester(_intersystem);
        }
        catch (Exception e)
        {
            ErrorLog.Error("Error in the constructor: {0}", e.Message);
        }
    }

    /// <summary>
    /// InitializeSystem - this method gets called after the constructor
    /// has finished.
    ///
    /// Use InitializeSystem to:
    /// * Start threads
    /// * Configure ports, such as serial and verisports
    /// * Start and initialize socket connections
    /// Send initial device configurations
    ///
    /// Please be aware that InitializeSystem needs to exit quickly also;
    /// if it doesn't exit in time, the SIMPL#Pro program will exit.
    /// </summary>
    public override void InitializeSystem()
    {
        try
        {
            ErrorLog.Notice("MatroxMaevex démarré.");
        }
        catch (Exception e)
        {
            ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
        }
    }

    /// <summary>
    /// Event Handler for Ethernet events: Link Up and Link Down.
    /// Use these events to close / re-open sockets, etc.
    /// </summary>
    private void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
    {
        switch (ethernetEventArgs.EthernetEventType)
        {
            case eEthernetEventType.LinkDown:
                break;
            case eEthernetEventType.LinkUp:
                break;
        }
    }

    /// <summary>
    /// Event Handler for system events, such as disk inserted / removed in
    /// case the system has an SD card slot.
    /// </summary>
    private void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
    {
        switch (systemEventType)
        {
            case eSystemEventType.DiskInserted:
                break;
            case eSystemEventType.DiskRemoved:
                break;
            case eSystemEventType.Rebooting:
                break;
        }
    }

    /// <summary>
    /// Event Handler for Programmatic events: Stop, Pause, Resume.
    /// Use this event to clean up when a program is stopping, pausing, and resuming.
    /// This event only applies to this SIMPL#Pro program, so this is a
    /// good place to disconnect from equipment or shut down external
    /// communications sockets, etc.
    /// </summary>
    private void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
    {
        switch (programStatusEventType)
        {
            case eProgramStatusEventType.Paused:
                break;
            case eProgramStatusEventType.Resumed:
                break;
            case eProgramStatusEventType.Stopping:
                break;
        }
    }
}
