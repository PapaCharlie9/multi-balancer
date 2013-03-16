/* PROTObalancer.cs

by PapaCharlie9@gmail.com

Free to use as is in any way you want with no warranty.

*/

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using System.Net;
using System.Web;
using System.Data;
using System.Threading;
using System.Timers;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;


namespace PRoConEvents
{

/* Aliases */

using EventType = PRoCon.Core.Events.EventType;
using CapturableEvent = PRoCon.Core.Events.CapturableEvents;

/* Enums */

public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, BalanceOnly, UnstackOnly, None };

public enum Speed { Click_Here_For_Speed_Names, Stop, Slow, Adaptive, Fast };

public enum DefineStrong { RoundScore, RoundKDR, RoundKills };

/* Classes */

public class PROTObalancer : PRoConPluginAPI, IPRoConPluginInterface
{

/* Inherited:
    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
*/

    public class PerModeSettings {
        public PerModeSettings() {}
        
        public PerModeSettings(String simplifiedModeName) {
            DetermineStrongPlayersBy = DefineStrong.RoundScore;
            
            switch (simplifiedModeName) {
                case "Conq Small, Dom, Scav":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 28;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseForTickets = 160;
                    DefinitionOfLatePhaseForTickets = 40;
                    break;
                case "Conquest Large":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseForTickets = 240;
                    DefinitionOfLatePhaseForTickets = 60;
                    break;
                case "CTF":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseForTickets = 240;
                    DefinitionOfLatePhaseForTickets = 60;
                    break;
                case "Rush":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseForTickets = 60;
                    DefinitionOfLatePhaseForTickets = 15;
                    break;
                case "Squad Deathmatch":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    DefinitionOfHighPopulationForPlayers = 14;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseForTickets = 80;
                    DefinitionOfLatePhaseForTickets = 20;
                    break;
                case "Superiority":
                    MaxPlayers = 24;
                    CheckTeamStackingAfterFirstMinutes = 15;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseForTickets = 160;
                    DefinitionOfLatePhaseForTickets = 40;
                    break;
                case "Team Deathmatch":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseForTickets = 80;
                    DefinitionOfLatePhaseForTickets = 20;
                    break;
                case "Squad Rush":
                    MaxPlayers = 8;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    DefinitionOfHighPopulationForPlayers = 6;
                    DefinitionOfLowPopulationForPlayers = 4;
                    DefinitionOfEarlyPhaseForTickets = 18;
                    DefinitionOfLatePhaseForTickets = 2;
                    break;
                case "Gun Master":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseForTickets = 0;
                    DefinitionOfLatePhaseForTickets = 0;
                    break;
                case "Unknown or New Mode":
                default:
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 28;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseForTickets = 160;
                    DefinitionOfLatePhaseForTickets = 40;
                    break;
            }
        }
        
        public int MaxPlayers = 64; // will be corrected later
        public double CheckTeamStackingAfterFirstMinutes = 10;
        public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
        public double DefinitionOfHighPopulationForPlayers = 48;
        public double DefinitionOfLowPopulationForPlayers = 16;
        public double DefinitionOfEarlyPhaseForTickets = 80;
        public double DefinitionOfLatePhaseForTickets = 20;
        
        //public double MinTicketsPercentage = 10.0; // TBD
        public int GoAggressive = 0; // TBD
    } // end PerModeSettings


    public class PlayerStats {
        public PlayerStats() {}
        
        public DateTime FirstSpawnTimestamp;
        public DateTime RoundStartTimestamp;
        public String Tag;
        public int Score;
        public int Kills;
        public int Deaths;
        public int Rounds; // incremented OnRoundOverPlayers
        
        public int ScoreTotal; // not including current round
        public int KillsTotal; // not including current round
        public int DeathsTotal; // not including current round
    } // end PlayerStats


private bool fIsEnabled;
private Dictionary<String,String> fModeToSimple = null;
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

/* Settings */

public int DebugLevel;
public int MaximumServerSize;
public int MaxTeamSwitchesByStrongPlayers;
public int MaxTeamSwitchesByWeakPlayers;
public double UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
public bool Enable2SlotReserve;
public bool EnablerecruitCommand;
public bool EnableWhitelistingOfReservedSlotsList;
public String[] Whitelist;
public String[] DisperseEvenlyList;
public PresetItems Preset;

public bool OnWhitelist;
public bool TopScorers;
public bool SameClanTagsInSquad;
public double MinutesAfterJoining;
public bool JoinedEarlyPhase;
public bool JoinedMidPhase;
public bool JoinedLatePhase;

public double[] EarlyPhaseTicketPercentageToUnstack;
public double[] MidPhaseTicketPercentageToUnstack;
public double[] LatePhaseTicketPercentageToUnstack;
public Speed SpellingOfSpeedNamesReminder;
public Speed[] EarlyPhaseBalanceSpeed;
public Speed[] MidPhaseBalanceSpeed;
public Speed[] LatePhaseBalanceSpeed;

public bool QuietMode;
public double YellDurationSeconds;
public String ChatMovedForBalance;
public String YellMovedForBalance;
public String ChatMovedToUnstack;
public String YellMovedToUnstack;
public String ChatDetectedSwitchToWinningTeam;
public String YellDetectedSwitchToWinningTeam;
public String ChatDetectedSwitchToLosingTeam;
public String YellDetectedSwitchToLosingTeam;
public String ChatAfterUnswitching;
public String YellAfterUnswitching;
public String ChatDetectedSwitchByDispersalPlayer;
public String YellDetectedSwitchByDispersalPlayer;
public String ChatAfterUnswitchingDispersalPlayer;
public String YellAfterUnswitchingDispersalPlayer;

public String ShowInLog; // command line to show info in plugin.log
public bool LogChat;

/* Constructor */

public PROTObalancer() {
    /* Private members */
    fIsEnabled = false;
    fModeToSimple = new Dictionary<String,String>();

    fEasyTypeDict = new Dictionary<int, Type>();
    fEasyTypeDict.Add(0, typeof(int));
    fEasyTypeDict.Add(1, typeof(Int16));
    fEasyTypeDict.Add(2, typeof(Int32));
    fEasyTypeDict.Add(3, typeof(Int64));
    fEasyTypeDict.Add(4, typeof(float));
    fEasyTypeDict.Add(5, typeof(long));
    fEasyTypeDict.Add(6, typeof(String));
    fEasyTypeDict.Add(7, typeof(string));
    fEasyTypeDict.Add(8, typeof(double));

    fBoolDict = new Dictionary<int, Type>();
    fBoolDict.Add(0, typeof(Boolean));
    fBoolDict.Add(1, typeof(bool));

    fListStrDict = new Dictionary<int, Type>();
    fListStrDict.Add(0, typeof(List<String>));
    fListStrDict.Add(1, typeof(List<string>));
    
    fPerMode = new Dictionary<String,PerModeSettings>();
    
    /* Settings */

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = 2;
    MaximumServerSize = 64;
    MaxTeamSwitchesByStrongPlayers = 1;
    MaxTeamSwitchesByWeakPlayers = 2;
    UnlimitedTeamSwitchingDuringFirstMinutesOfRound = 5.0;
    Enable2SlotReserve = false;
    EnablerecruitCommand = false;
    EnableWhitelistingOfReservedSlotsList = true;
    Whitelist = new String[] {"[-- name, tag, or EA_GUID --]"};
    DisperseEvenlyList = new String[] {"[-- name, tag, or EA_GUID --]"};
    Preset = PresetItems.Standard;
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = true;
    TopScorers = true;
    SameClanTagsInSquad = true;
    MinutesAfterJoining = 15;
    JoinedEarlyPhase = true;
    JoinedMidPhase = true;
    JoinedLatePhase = false;


    /* ===== SECTION 3 - Round Phase & Population Settings ===== */

    EarlyPhaseTicketPercentageToUnstack = new double[3]         {  0,120,120};
    MidPhaseTicketPercentageToUnstack = new double[3]           {  0,120,120};
    LatePhaseTicketPercentageToUnstack = new double[3]          {  0,  0,  0};
    
    SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;

    EarlyPhaseBalanceSpeed = new Speed[3]           { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
    MidPhaseBalanceSpeed = new Speed[3]             { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
    LatePhaseBalanceSpeed = new Speed[3]            {     Speed.Stop,     Speed.Stop,     Speed.Stop};

    /* ===== SECTION 5 - Messages ===== */
    
    QuietMode = false; // false: chat is global, true: chat is private. Yells are always private
    YellDurationSeconds = 10;
    ChatMovedForBalance = "*** MOVED %name% for balance ...";
    YellMovedForBalance = "Moved %name% for balance ...";
    ChatMovedToUnstack = "*** MOVED %name% to unstack teams ...";
    YellMovedToUnstack = "Moved %name% to unstack teams ...";
    ChatDetectedSwitchToWinningTeam = "%name%, the %fromTeam% needs your help, sending you back ...";
    YellDetectedSwitchToWinningTeam = "The %fromTeam% needs your help, sending you back!";
    ChatDetectedSwitchToLosingTeam = "%name%, thanks for helping out the %toTeam%!";
    YellDetectedSwitchToLosingTeam = "Thanks for helping out the %toTeam%!";
    ChatAfterUnswitching = "%name%, please stay on the %toTeam% for the rest of this round";
    YellAfterUnswitching = "Please stay on the %toTeam% for the rest of this round";
    ChatDetectedSwitchByDispersalPlayer = "%name% is on the list of players to split between teams";
    YellDetectedSwitchByDispersalPlayer = "You're on the list of players to split between teams";
    ChatAfterUnswitchingDispersalPlayer = "%name%, stay on the team you are assigned to";
    YellAfterUnswitchingDispersalPlayer = "Stay on the team you are assigned to";
    
    /* ===== SECTION 6 - TBD ===== */

    /* ===== SECTION 7 - TBD ===== */

    /* ===== SECTION 8 - Per-Mode Settings ===== */

    /* ===== SECTION 9 - Debug Settings ===== */

    ShowInLog = String.Empty;
    LogChat = true;
}

public PROTObalancer(PresetItems preset) : this() {
    switch (preset) {
        case PresetItems.Standard:
         // EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,120,120};
         // MidPhaseTicketPercentageToUnstack = new double[3]       {  0,120,120};
         // LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};
         // EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
         // MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
         // LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            break;

        case PresetItems.Aggressive:

            OnWhitelist = true;
            TopScorers = false;
            SameClanTagsInSquad = false;
            MinutesAfterJoining = 0;
            JoinedEarlyPhase = false;
            JoinedMidPhase = false;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {110,110,110};
            MidPhaseTicketPercentageToUnstack = new double[3]       {110,110,110};
            LatePhaseTicketPercentageToUnstack = new double[3]      {110,110,110};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            
            break;

        case PresetItems.Passive:

            OnWhitelist = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            MinutesAfterJoining = 30;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = true;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,200};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,200,200};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow,     Speed.Slow,     Speed.Slow};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow,     Speed.Slow,     Speed.Slow};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
            break;

        case PresetItems.Intensify:

            OnWhitelist = true;
            TopScorers = true;
            SameClanTagsInSquad = false;
            MinutesAfterJoining = 60;
            JoinedEarlyPhase = false;
            JoinedMidPhase = false;
            JoinedLatePhase = true;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {110,120,120};
            MidPhaseTicketPercentageToUnstack = new double[3]       {120,120,120};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            // TBD: Needs Speed.OverBalance (similar to Fast, but puts more players on losing team)
            EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
            break;

        case PresetItems.Retain:

            OnWhitelist = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            MinutesAfterJoining = 30;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = true;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,150};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,150,200};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
            break;

        case PresetItems.BalanceOnly:

            OnWhitelist = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            MinutesAfterJoining = 15;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,  0};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,  0,  0};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
            break;

        case PresetItems.UnstackOnly:

            OnWhitelist = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            MinutesAfterJoining = 15;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {120,120,120};
            MidPhaseTicketPercentageToUnstack = new double[3]       {120,120,120};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
            break;

        case PresetItems.None:
            break;
        default:
            break;
    }
}

/* Types */

public enum MessageType { Warning, Error, Exception, Normal };

/* Properties */

public String FormatMessage(String msg, MessageType type) {
    String prefix = "[^b" + GetPluginName() + "^n] ";

    if (type.Equals(MessageType.Warning))
        prefix += "^1^bWARNING^0^n: ";
    else if (type.Equals(MessageType.Error))
        prefix += "^1^bERROR^0^n: ";
    else if (type.Equals(MessageType.Exception))
        prefix += "^1^bEXCEPTION^0^n: ";

    return prefix + msg;
}


public void LogWrite(String msg)
{
    this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
}

public void ConsoleWrite(String msg, MessageType type)
{
    LogWrite(FormatMessage(msg, type));
}

public void ConsoleWrite(String msg)
{
    ConsoleWrite(msg, MessageType.Normal);
}

public void ConsoleWarn(String msg)
{
    ConsoleWrite(msg, MessageType.Warning);
}

public void ConsoleError(String msg)
{
    ConsoleWrite(msg, MessageType.Error);
}

public void ConsoleException(String msg)
{
    ConsoleWrite(msg, MessageType.Exception);
}

public void DebugWrite(String msg, int level)
{
    if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
}


public void ServerCommand(params String[] args)
{
    List<String> list = new List<String>();
    list.Add("procon.protected.send");
    list.AddRange(args);
    this.ExecuteCommand(list.ToArray());
}


public String GetPluginName() {
    return "PROTObalancer";
}

public String GetPluginVersion() {
    return "0.0.0.5";
}

public String GetPluginAuthor() {
    return "PapaCharlie9";
}

public String GetPluginWebsite() {
    return "TBD";
}

public String GetPluginDescription() {
    return PROTObalancerUtils.HTML_DOC;
}




public List<CPluginVariable> GetDisplayPluginVariables() {


    List<CPluginVariable> lstReturn = new List<CPluginVariable>();

    try {
        /* ===== SECTION 0 - Presets ===== */
        
        UpdatePresetValue();

        String var_name = "0 - Presets|Use Round Phase, Population and Exclusions preset ";
        String var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), Preset)));
        
        /* ===== SECTION 1 - Settings ===== */
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));

        lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Server Size", MaximumServerSize.GetType(), MaximumServerSize));

/*
        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Strong Players", MaxTeamSwitchesByStrongPlayers.GetType(), MaxTeamSwitchesByStrongPlayers));

        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Weak Players", MaxTeamSwitchesByWeakPlayers.GetType(), MaxTeamSwitchesByWeakPlayers));
*/

        lstReturn.Add(new CPluginVariable("1 - Settings|Unlimited Team Switching During First Minutes Of Round", UnlimitedTeamSwitchingDuringFirstMinutesOfRound.GetType(), UnlimitedTeamSwitchingDuringFirstMinutesOfRound));

/*
        lstReturn.Add(new CPluginVariable("1 - Settings|Enable 2 Slot Reserve", Enable2SlotReserve.GetType(), Enable2SlotReserve));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable @#!recruit Command", EnablerecruitCommand.GetType(), EnablerecruitCommand));
*/

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Whitelisting Of Reserved Slots List", EnableWhitelistingOfReservedSlotsList.GetType(), EnableWhitelistingOfReservedSlotsList));

        lstReturn.Add(new CPluginVariable("1 - Settings|Whitelist", Whitelist.GetType(), Whitelist));

        lstReturn.Add(new CPluginVariable("1 - Settings|Disperse Evenly List", DisperseEvenlyList.GetType(), DisperseEvenlyList));
        
        /* ===== SECTION 2 - Exclusions ===== */

        lstReturn.Add(new CPluginVariable("2 - Exclusions|On Whitelist", OnWhitelist.GetType(), OnWhitelist));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Top Scorers", TopScorers.GetType(), TopScorers));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Squad", SameClanTagsInSquad.GetType(), SameClanTagsInSquad));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Joining", MinutesAfterJoining.GetType(), MinutesAfterJoining));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Early Phase", JoinedEarlyPhase.GetType(), JoinedEarlyPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Mid Phase", JoinedMidPhase.GetType(), JoinedMidPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Late Phase", JoinedLatePhase.GetType(), JoinedLatePhase));

        /* ===== SECTION 3 - Round Phase & Population Setttings ===== */
        
        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(EarlyPhaseTicketPercentageToUnstack)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(MidPhaseTicketPercentageToUnstack)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(LatePhaseTicketPercentageToUnstack)));
        
        var_name = "3 - Round Phase and Population Settings|Spelling Of Speed Names Reminder";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(Speed))) + ")";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(Speed), SpellingOfSpeedNamesReminder)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Balance Speed (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(EarlyPhaseBalanceSpeed)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Balance Speed (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(MidPhaseBalanceSpeed)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Balance Speed (Low, Med, High population)", typeof(String), PROTObalancerUtils.ArrayToString(LatePhaseBalanceSpeed)));

        /* ===== SECTION 5 - Messages ===== */
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Quiet Mode", QuietMode.GetType(), QuietMode));

        lstReturn.Add(new CPluginVariable("5 - Messages|Yell Duration Seconds", YellDurationSeconds.GetType(), YellDurationSeconds));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved For Balance", ChatMovedForBalance.GetType(), ChatMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved For Balance", YellMovedForBalance.GetType(), YellMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved To Unstack", ChatMovedToUnstack.GetType(), ChatMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved To Unstack", YellMovedToUnstack.GetType(), YellMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Switch To Winning Team", ChatDetectedSwitchToWinningTeam.GetType(), ChatDetectedSwitchToWinningTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Switch To Winning Team", YellDetectedSwitchToWinningTeam.GetType(), YellDetectedSwitchToWinningTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Switch To Losing Team", ChatDetectedSwitchToLosingTeam.GetType(), ChatDetectedSwitchToLosingTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Switch To Losing Team", YellDetectedSwitchToLosingTeam.GetType(), YellDetectedSwitchToLosingTeam));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: After Unswitching", ChatAfterUnswitching.GetType(), ChatAfterUnswitching));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: After Unswitching", YellAfterUnswitching.GetType(), YellAfterUnswitching));

        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Switch By Dispersal Player", ChatDetectedSwitchByDispersalPlayer.GetType(), ChatDetectedSwitchByDispersalPlayer));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Switch By Dispersal Player", YellDetectedSwitchByDispersalPlayer.GetType(), YellDetectedSwitchByDispersalPlayer));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: After Unswitching Dispersal Player", ChatAfterUnswitchingDispersalPlayer.GetType(), ChatAfterUnswitchingDispersalPlayer));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: After Unswitching Dispersal Player", YellAfterUnswitchingDispersalPlayer.GetType(), YellAfterUnswitchingDispersalPlayer));

        /* ===== SECTION 6 - TBD ===== */

        /* ===== SECTION 7 - TBD ===== */

        /* ===== SECTION 8 - Per-Mode Settings ===== */

        List<String> simpleModes = GetSimplifiedModes();

        foreach (String sm in simpleModes) {
            PerModeSettings oneSet = null;
            if (!fPerMode.ContainsKey(sm)) {
                oneSet = new PerModeSettings(sm);
                fPerMode[sm] = oneSet;
            } else {
                oneSet = fPerMode[sm];
            }

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Check Team Stacking After First Minutes", oneSet.CheckTeamStackingAfterFirstMinutes.GetType(), oneSet.CheckTeamStackingAfterFirstMinutes));

            var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
            var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

            lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population For Players >=", oneSet.DefinitionOfHighPopulationForPlayers.GetType(), oneSet.DefinitionOfHighPopulationForPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population For Players <=", oneSet.DefinitionOfLowPopulationForPlayers.GetType(), oneSet.DefinitionOfLowPopulationForPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase For Tickets >=", oneSet.DefinitionOfEarlyPhaseForTickets.GetType(), oneSet.DefinitionOfEarlyPhaseForTickets));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase For Tickets <=", oneSet.DefinitionOfLatePhaseForTickets.GetType(), oneSet.DefinitionOfLatePhaseForTickets));

            /*
            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Min Tickets Percentage", oneSet.MinTicketsPercentage.GetType(), oneSet.MinTicketsPercentage));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Go Aggressive", oneSet.GoAggressive.GetType(), oneSet.GoAggressive));
            */
        }

        /* ===== SECTION 9 - Debug Settings ===== */

        lstReturn.Add(new CPluginVariable("9 - Debugging|Show In Log", ShowInLog.GetType(), ShowInLog));

        lstReturn.Add(new CPluginVariable("9 - Debugging|Log Chat", LogChat.GetType(), LogChat));


    } catch (Exception e) {
        ConsoleException(e.ToString());
    }

    return lstReturn;
}

public List<CPluginVariable> GetPluginVariables() {
    return GetDisplayPluginVariables();
}

public void SetPluginVariable(String strVariable, String strValue) {
    bool isPresetVar = false;
    bool isReminderVar = false;

    DebugWrite(strVariable + " <- " + strValue, 3);

    try {
        String tmp = strVariable;
        int pipeIndex = strVariable.IndexOf('|');
        if (pipeIndex >= 0) {
            pipeIndex++;
            tmp = strVariable.Substring(pipeIndex, strVariable.Length - pipeIndex);
        }
        if (tmp.Contains("(Low, Med, High population)")) {
            tmp = tmp.Replace("(Low, Med, High population)", String.Empty);
        }

        BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        String propertyName = Regex.Replace(tmp, @"[^a-zA-Z_0-9]", String.Empty);
        
        if (strVariable.Contains("preset")) propertyName = "Preset";

        FieldInfo field = this.GetType().GetField(propertyName, flags);
        
        Type fieldType = null;


        if (!strVariable.Contains("Settings for") && field != null) {
            fieldType = field.GetValue(this).GetType();
            if (strVariable.Contains("preset")) {
                fieldType = typeof(PresetItems);
                try {
                    Preset = (PresetItems)Enum.Parse(fieldType, strValue);
                    isPresetVar = true;
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (tmp.Contains("Spelling Of Speed Names Reminder")) {
                fieldType = typeof(Speed);
                try {
                    field.SetValue(this, (Speed)Enum.Parse(fieldType, strValue));
                    isReminderVar = true;
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (tmp.Contains("Balance Speed")) {
                fieldType = typeof(Speed[]);
                try {
                    // Parse the list into an array of enum vals
                    Speed[] items = PROTObalancerUtils.ParseSpeedArray(strValue); // also validates
                    field.SetValue(this, items);
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (tmp.Contains("Ticket Percentage To Unstack")) {
                fieldType = typeof(double[]);
                try {
                    // Parse the list into an array of numbers
                    double[] nums = PROTObalancerUtils.ParseNumArray(strValue); // also validates
                    field.SetValue(this, nums);
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                }
            } else if (fEasyTypeDict.ContainsValue(fieldType)) {
                field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
            } else if (fListStrDict.ContainsValue(fieldType)) {
                field.SetValue(this, new List<string>(CPluginVariable.DecodeStringArray(strValue)));
            } else if (fBoolDict.ContainsValue(fieldType)) {
                DebugWrite(propertyName + " strValue = " + strValue, 3);
                if (Regex.Match(strValue, "True", RegexOptions.IgnoreCase).Success) {
                    field.SetValue(this, true);
                } else {
                    field.SetValue(this, false);
                }
            }
        } else {
            Match m = Regex.Match(tmp, @"([^:]+):\s([^:]+)$");
            
            if (m.Success) {
                String mode = m.Groups[1].Value;
                String fieldPart = m.Groups[2].Value.Replace(" ","");
                String perModeName = Regex.Replace(fieldPart, @"[^a-zA-Z_0-9]", String.Empty);
                
                if (!fPerMode.ContainsKey(mode)) {
                    fPerMode[mode] = new PerModeSettings(mode);
                }
                PerModeSettings pms = fPerMode[mode];
                
                field = pms.GetType().GetField(perModeName, flags);
                
                DebugWrite("Mode: " + mode + ", Field: " + perModeName + ", Value: " + strValue, 3);
                
                if (field != null) {
                    fieldType = field.GetValue(pms).GetType();
                    if (fEasyTypeDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                    } else if (fListStrDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, new List<string>(CPluginVariable.DecodeStringArray(strValue)));
                    } else if (fBoolDict.ContainsValue(fieldType)) {
                        if (Regex.Match(strValue, "True", RegexOptions.IgnoreCase).Success) {
                            field.SetValue(pms, true);
                        } else {
                            field.SetValue(pms, false);
                        }
                    } else if (strVariable.Contains("Strong")) {
                        fieldType = typeof(DefineStrong);
                        try {
                            field.SetValue(pms, (DefineStrong)Enum.Parse(fieldType, strValue));
                        } catch (Exception e) {
                            ConsoleException(e.ToString());
                        }
                    }
                } else {
                    DebugWrite("field is null", 3);
                }
            }
        }
    } catch (System.Exception e) {
        ConsoleException(e.ToString());
    } finally {
        // TBD: Validate() function needed here!
        // Validate all values and correct if needed
        
        if (!String.IsNullOrEmpty(ShowInLog)) {
            if (Regex.Match(ShowInLog, @"modes", RegexOptions.IgnoreCase).Success) {
                List<String> modeList = GetSimplifiedModes();
                DebugWrite("modes(" + modeList.Count + "):", 2);
                foreach (String m in modeList) {
                    DebugWrite(m, 2);
                }
            }
            
            ShowInLog = String.Empty;
        }

                
        /*
        switch (perModeName) {
            case "Min Tickets":
                if (!Double.TryParse(strValue, out pms.MinTicketsPercentage)) {
                    ConsoleError("Bogus setting for " + strVariable + " ? " + strValue);
                }
                break;
            case "Go Aggressive":
                if (!Int32.TryParse(strValue, out pms.GoAggressive)) {
                    ConsoleError("Bogus setting for " + strVariable + " ? " + strValue);
                }
                break;
        }
        */
        
        if (!isReminderVar) {
            // Reset to show hint
            SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;
        }
        
        if (isPresetVar) {
            // Update other settings based on new preset value
            PROTObalancerUtils.UpdateSettingsForPreset(this, Preset);
        } else {
            // Update Preset value based on current settings
            UpdatePresetValue();
        }
    }
}


public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
    this.RegisterEvents(this.GetType().Name, 
    "OnVersion",
    "OnServerInfo",
    "OnResponseError",
    "OnListPlayers",
    "OnPlayerJoin",
    "OnPlayerLeft",
    "OnPlayerKilled",
    "OnPlayerSpawned",
    "OnPlayerTeamChange",
    "OnGlobalChat",
    "OnTeamChat",
    "OnSquadChat",
    "OnRoundOverPlayers",
    "OnRoundOver",
    "OnRoundOverTeamScores",
    "OnLoadingLevel",
    "OnLevelStarted",
    "OnLevelLoaded"
    );
}

public void OnPluginEnable() {
    fIsEnabled = true;
    ConsoleWrite("Enabled! Version = " + GetPluginVersion());
}


private void JoinWith(Thread thread, int secs)
{
    if (thread == null || !thread.IsAlive)
        return;

    ConsoleWrite("Waiting for ^b" + thread.Name + "^n to finish");
    thread.Join(secs*1000);
}


public void OnPluginDisable() {
    fIsEnabled = false;
    
    ConsoleWrite("Disabled!");
}


public override void OnVersion(String type, String ver) {
    DebugWrite("OnVersion " + type + " " + ver, 9);
}

private CServerInfo fSI = null;

public override void OnServerInfo(CServerInfo serverInfo) {
    DebugWrite("Debug level = " + DebugLevel, 9);
    
    fSI = serverInfo;
}

public override void OnResponseError(List<String> requestWords, String error) { }

public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
}

public override void OnPlayerJoin(String soldierName) {
}

public override void OnPlayerLeft(CPlayerInfo playerInfo) {
}

public override void OnPlayerKilled(Kill kKillerVictimDetails) { }

public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) { }

public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) { }

public override void OnGlobalChat(String speaker, String message) { }

public override void OnTeamChat(String speaker, String message, int teamId) { }

public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

public override void OnRoundOverPlayers(List<CPlayerInfo> players) { }

public override void OnRoundOverTeamScores(List<TeamScore> teamScores) { }

public override void OnRoundOver(int winningTeamId) { }

public override void OnLoadingLevel(String mapFileName, int roundsPlayed, int roundsTotal) { }

public override void OnLevelStarted() { }

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) { } // BF3


/* ========================================== */

public List<String> GetSimplifiedModes() {
    List<String> r = new List<String>();
    
    if (fModeToSimple.Count < 1) {
        List<CMap> raw = this.GetMapDefines();
        foreach (CMap m in raw) {
            String simple = null;
            switch (m.GameMode) {
                case "Conquest Large":
                case "Assault64":
                    simple = "Conquest Large";
                    break;
                case "Conquest":
                case "Assault":
                case "Assault #2":
                case "Conquest Domination":
                case "Scavenger":
                    simple = "Conq Small, Dom, Scav";
                    break;
                case "TDM":
                case "TDM Close Quarters":
                    simple = "Team Deathmatch";
                    break;
                case "Tank Superiority":
                case "Air Superiority":
                    simple = "Superiority";
                    break;
                case "Rush":
                case "CTF":
                case "Squad Deathmatch":
                case "Gun Master":
                case "Squad Rush":
                    simple = m.GameMode;
                    break;
                default:
                    simple = "Unknown or New Mode";
                    break;
                
            }
            if (fModeToSimple.ContainsKey(m.PlayList)) {
                if (fModeToSimple[m.PlayList] != simple) {
                    ConsoleWarn("For mode " + m.PlayList + " old value " + fModeToSimple[m.PlayList] + " != new value " + simple);
                }
            } else {
                fModeToSimple[m.PlayList] = simple;
            }
        }
    }
    
    bool last = false;
    foreach (KeyValuePair<String,String> p in fModeToSimple) {
        if (r.Contains(p.Value)) continue;
        if (p.Value == "Unknown or New Mode") { last = true; continue; }
        r.Add(p.Value); // collect up all the simple GameMode names
    }
    if (last) r.Add("Unknown or New Mode"); // make sure this is last

    return r;
}

public bool CheckForEquality(PROTObalancer rhs) {
    return (this.OnWhitelist == rhs.OnWhitelist
     && this.TopScorers == rhs.TopScorers
     && this.SameClanTagsInSquad == rhs.SameClanTagsInSquad
     && this.MinutesAfterJoining == rhs.MinutesAfterJoining
     && this.JoinedEarlyPhase == rhs.JoinedEarlyPhase
     && this.JoinedMidPhase == rhs.JoinedMidPhase
     && this.JoinedLatePhase == rhs.JoinedLatePhase
     && PROTObalancerUtils.EqualArrays(this.EarlyPhaseTicketPercentageToUnstack, rhs.EarlyPhaseTicketPercentageToUnstack)
     && PROTObalancerUtils.EqualArrays(this.MidPhaseTicketPercentageToUnstack, rhs.MidPhaseTicketPercentageToUnstack)
     && PROTObalancerUtils.EqualArrays(this.LatePhaseTicketPercentageToUnstack, rhs.LatePhaseTicketPercentageToUnstack)
     && PROTObalancerUtils.EqualArrays(this.EarlyPhaseBalanceSpeed, rhs.EarlyPhaseBalanceSpeed)
     && PROTObalancerUtils.EqualArrays(this.MidPhaseBalanceSpeed, rhs.MidPhaseBalanceSpeed)
     && PROTObalancerUtils.EqualArrays(this.LatePhaseBalanceSpeed, rhs.LatePhaseBalanceSpeed)
    );
}


public void UpdatePresetValue() {
    Preset = PresetItems.None;  // backstop value

    // Check for Standard
    if (PROTObalancerUtils.IsEqual(this, PresetItems.Standard)) {
        Preset = PresetItems.Standard;
        return;
    }
    
    // Check for Aggressive
    if (PROTObalancerUtils.IsEqual(this, PresetItems.Aggressive)) {
        Preset = PresetItems.Aggressive;
        return;
    }
    
    // Check for Passive
    if (PROTObalancerUtils.IsEqual(this, PresetItems.Passive)) {
        Preset = PresetItems.Passive;
        return;
    }
    
    // Check for Intensify
    if (PROTObalancerUtils.IsEqual(this, PresetItems.Intensify)) {
        Preset = PresetItems.Intensify;
        return;
    }
    
    // Check for Retain
    if (PROTObalancerUtils.IsEqual(this, PresetItems.Retain)) {
        Preset = PresetItems.Retain;
        return;
    }
    
    // Check for BalanceOnly
    if (PROTObalancerUtils.IsEqual(this, PresetItems.BalanceOnly)) {
        Preset = PresetItems.BalanceOnly;
        return;
    }
    
    // Check for UnstackOnly
    if (PROTObalancerUtils.IsEqual(this, PresetItems.UnstackOnly)) {
        Preset = PresetItems.UnstackOnly;
        return;
    }
}


} // end PROTObalancer









/* Utilities */


static class PROTObalancerUtils {
    public static bool IsEqual(PROTObalancer lhs, PresetItems preset) {
        PROTObalancer rhs = new PROTObalancer(preset);
        return (lhs.CheckForEquality(rhs));
    }
    
    public static void UpdateSettingsForPreset(PROTObalancer lhs, PresetItems preset) {
        PROTObalancer rhs = new PROTObalancer(preset);
        
        lhs.DebugWrite("UpdateSettingsForPreset to " + preset, 3);

        lhs.OnWhitelist = rhs.OnWhitelist;
        lhs.TopScorers = rhs.TopScorers;
        lhs.SameClanTagsInSquad = rhs.SameClanTagsInSquad;
        lhs.MinutesAfterJoining = rhs.MinutesAfterJoining;
        lhs.JoinedEarlyPhase = rhs.JoinedEarlyPhase;
        lhs.JoinedMidPhase = rhs.JoinedMidPhase;
        lhs.JoinedLatePhase = rhs.JoinedLatePhase;

        lhs.EarlyPhaseTicketPercentageToUnstack = rhs.EarlyPhaseTicketPercentageToUnstack;
        lhs.MidPhaseTicketPercentageToUnstack = rhs.MidPhaseTicketPercentageToUnstack;
        lhs.LatePhaseTicketPercentageToUnstack = rhs.LatePhaseTicketPercentageToUnstack;

        lhs.EarlyPhaseBalanceSpeed = rhs.EarlyPhaseBalanceSpeed;
        lhs.MidPhaseBalanceSpeed = rhs.MidPhaseBalanceSpeed;
        lhs.LatePhaseBalanceSpeed = rhs.LatePhaseBalanceSpeed;
    }
    
    public static bool EqualArrays(double[] lhs, double[] rhs) {
        if (lhs == null && rhs == null) return true;
        if (lhs == null || rhs == null) return false;
        if (lhs.Length != rhs.Length) return false;
        
        for (int i = 0; i < lhs.Length; ++i) {
            if (lhs[i] != rhs[i]) return false;
        }
        return true;
    }

    public static bool EqualArrays(Speed[] lhs, Speed[] rhs) {
        if (lhs == null && rhs == null) return true;
        if (lhs == null || rhs == null) return false;
        if (lhs.Length != rhs.Length) return false;
        
        for (int i = 0; i < lhs.Length; ++i) {
            if (lhs[i] != rhs[i]) return false;
        }
        return true;
    }
    
    public static String ArrayToString(double[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = a[i].ToString("F0");
                first = false;
            } else {
                ret = ret + ", " + a[i].ToString("F0");
            }
        }
        return ret;
    }

    public static String ArrayToString(Speed[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = Enum.GetName(typeof(Speed), a[i]);
                first = false;
            } else {
                ret = ret + ", " + Enum.GetName(typeof(Speed), a[i]);
            }
        }
        return ret;
    }

    public static double[] ParseNumArray(String s) {
        double[] nums = new double[3] {-1,-1,-1}; // -1 indicates a syntax error
        if (String.IsNullOrEmpty(s)) return nums;
        if (!s.Contains(",")) return nums;
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) return nums;
        for (int i = 0; i < nums.Length; ++i) {
            bool parsedOk = Double.TryParse(strs[i], out nums[i]);
            if (!parsedOk) {
                nums[i] = -1;
                return nums;
            }
        }
        return nums;
    }

    public static Speed[] ParseSpeedArray(String s) {
        Speed[] speeds = new Speed[3] {Speed.Click_Here_For_Speed_Names, Speed.Click_Here_For_Speed_Names, Speed.Click_Here_For_Speed_Names};
        if (String.IsNullOrEmpty(s)) return speeds;
        if (!s.Contains(",")) return speeds;
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) return speeds;
        for (int i = 0; i < speeds.Length; ++i) {
            try {
                speeds[i] = (Speed)Enum.Parse(typeof(Speed), strs[i]);
            } catch (Exception e) {
                // TBD log an error about a bogus value?
                speeds[i] = Speed.Click_Here_For_Speed_Names;
            }
        }
        return speeds;
    }

    public const String HTML_DOC = @"
<h1>Multi-Balancer &amp; Unstacker, including SQDM</h1>
<p>For BF3, this plugin does live round team balancing and unstacking for all game modes, including Squad Deathmatch (SQDM).</p>

<h2>THIS IS JUST A PROTOTYPE FOR FEEDBACK!</h2>
<p>It doesn't do any balancing or unstacking, no matter what you change the settings to. It is completely safe to run on an active server. It sends no commands to the server.</p>

<p><font color=#0000FF>The purpose of this prototype is to get feedback from users about the arrangement and usage of plugin settings. I'm interested in answers to these questions:</font></p>

<ul>
<li>What is your overall impression with the number and complexity of settings?</li>
<li>What's missing?</li>
<li>What should be added?</li>
<li>Which names or values are confusing and need additional documentation to clarify?</li>
<li>What should be changed to improve clarity?</li>
</ul>

<h2>Description</h2>
<p>TBD</p>

<h2>Commands</h2>
<p>TBD</p>

<h2>Settings</h2>
<p>TBD</p>

<h2>Development</h2>
<p>TBD</p>
<h3>Changelog</h3>
<blockquote><h4>0.0.0.1 (10-JAN-2013)</h4>
    - initial version<br/>
</blockquote>
";

} // end PROTObalancerUtils


} // end namespace PRoConEvents



