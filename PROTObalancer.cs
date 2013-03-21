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

/* Main Class */

public class PROTObalancer : PRoConPluginAPI, IPRoConPluginInterface
{
    /* Enums */

    public enum MessageType { Warning, Error, Exception, Normal };
    
    public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, BalanceOnly, UnstackOnly, None };

    public enum Speed { Click_Here_For_Speed_Names, Stop, Slow, Adaptive, Fast };

    public enum DefineStrong { RoundScore, RoundKDR, RoundKills };
    
    public enum PluginState { Disabled, JustEnabled, WaitingForPlayers, Active, Error };
    
    public enum GameState { RoundEnding, RoundStarting, Playing, Warmup, Unknown };


    /* Classes */

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
                    DefinitionOfEarlyPhase = 160;
                    DefinitionOfLatePhase = 40;
                    break;
                case "Conquest Large":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhase = 240;
                    DefinitionOfLatePhase = 60;
                    break;
                case "CTF":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhase = 5; // minutes
                    DefinitionOfLatePhase = 15; // minutes
                    break;
                case "Rush":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhase = 60;
                    DefinitionOfLatePhase = 15;
                    break;
                case "Squad Deathmatch":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    DefinitionOfHighPopulationForPlayers = 14;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhase = 80;
                    DefinitionOfLatePhase = 20;
                    break;
                case "Superiority":
                    MaxPlayers = 24;
                    CheckTeamStackingAfterFirstMinutes = 15;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhase = 160;
                    DefinitionOfLatePhase = 40;
                    break;
                case "Team Deathmatch":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhase = 80;
                    DefinitionOfLatePhase = 20;
                    break;
                case "Squad Rush":
                    MaxPlayers = 8;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    DefinitionOfHighPopulationForPlayers = 6;
                    DefinitionOfLowPopulationForPlayers = 4;
                    DefinitionOfEarlyPhase = 18;
                    DefinitionOfLatePhase = 2;
                    break;
                case "Gun Master":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    DefinitionOfHighPopulationForPlayers = 12;
                    DefinitionOfLowPopulationForPlayers = 6;
                    DefinitionOfEarlyPhase = 0;
                    DefinitionOfLatePhase = 0;
                    break;
                case "Unknown or New Mode":
                default:
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    DefinitionOfHighPopulationForPlayers = 28;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhase = 160;
                    DefinitionOfLatePhase = 40;
                    break;
            }
        }
        
        public int MaxPlayers = 64; // will be corrected later
        public double CheckTeamStackingAfterFirstMinutes = 10;
        public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
        public double DefinitionOfHighPopulationForPlayers = 48;
        public double DefinitionOfLowPopulationForPlayers = 16;
        public double DefinitionOfEarlyPhase = 80;
        public double DefinitionOfLatePhase = 20;
        
        //public double MinTicketsPercentage = 10.0; // TBD
        public int GoAggressive = 0; // TBD
    } // end PerModeSettings


    public class PlayerModel {
        // Permanent
        public String Name;
        public String EAGUID;
        
        // Updated on events
        public int Team;
        public int Squad;
        public DateTime FirstSpawnTimestamp;
        public DateTime LastSeenTimestamp;
        public double ScoreRound;
        public double KillsRound;
        public double DeathsRound;
        public int Rounds; // incremented OnRoundOverPlayers
        public int Rank;
        public bool IsDeployed;
        
        // Battlelog
        public String Tag;
        
        // Computed
        public double KDRRound;
        public double SPMRound;
        
        // Accumulated
        public int ScoreTotal; // not including current round
        public int KillsTotal; // not including current round
        public int DeathsTotal; // not including current round
        
        public PlayerModel() {
            Name = null;
            Team = -1;
            Squad = -1;
            EAGUID = String.Empty;
            FirstSpawnTimestamp = DateTime.MinValue;
            LastSeenTimestamp = DateTime.MinValue;
            Tag = String.Empty;
            ScoreRound = -1;
            KillsRound = -1;
            DeathsRound = -1;
            Rounds = -1;
            Rank = -1;
            KDRRound = -1;
            SPMRound = -1;
            ScoreTotal = 0;
            KillsTotal = 0;
            DeathsTotal = 0;
            IsDeployed = false;
        }
        
        public PlayerModel(String name, int team) : this() {
            Name = name;
            Team = team;
        }
    } // end PlayerModel

    class TeamRoster {
        public int Team = 0; 
        public List<PlayerModel> Roster = null;

        public TeamRoster(int team, List<PlayerModel> roster) {
            Team = team;
            Roster = roster;
        }
    } // end TeamList

/* Inherited:
    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
*/

// General
private bool fIsEnabled;
private Dictionary<String,String> fModeToSimple = null;
private Dictionary<String,int> fPendingTeamChange = null;

// Data model
private List<String> fAllPlayers = null;
private Dictionary<String, PlayerModel> fKnownPlayers = null;
private PluginState fPluginState;
private GameState fGameState;
private CServerInfo fServerInfo;
private List<PlayerModel> fTeam1 = null;
private List<PlayerModel> fTeam2 = null;
private List<PlayerModel> fTeam3 = null;
private List<PlayerModel> fTeam4 = null;
private List<String> fUnassigned = null;
public DateTime fRoundStartTimestamp;
public bool fModelIsInSync = false; // false immediately after a move, true after listPlayers update

// Settings support
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

// Settings
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
    fPluginState = PluginState.Disabled;
    fGameState = GameState.Unknown;
    fServerInfo = null;
    
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
    
    fAllPlayers = new List<String>();
    fKnownPlayers = new Dictionary<String, PlayerModel>();
    fTeam1 = new List<PlayerModel>();
    fTeam2 = new List<PlayerModel>();
    fTeam3 = new List<PlayerModel>();
    fTeam4 = new List<PlayerModel>();
    fUnassigned = new List<String>();
    fRoundStartTimestamp = DateTime.MinValue;

    fPendingTeamChange = new Dictionary<String,int>();
    fModelIsInSync = false;
    
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









/* ======================== SETTINGS ============================= */









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
            
            bool isCTF = (sm == "CTF");
            bool isGM = (sm == "Gun Master");
            String earlyEq = (sm.Contains("Deathmatch")) ? "<=" :  ">=";
            String lateEq = (sm.Contains("Deathmatch")) ? ">=" : "<=";

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

            if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Check Team Stacking After First Minutes", oneSet.CheckTeamStackingAfterFirstMinutes.GetType(), oneSet.CheckTeamStackingAfterFirstMinutes));

                var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));
            }

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population For Players >=", oneSet.DefinitionOfHighPopulationForPlayers.GetType(), oneSet.DefinitionOfHighPopulationForPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population For Players <=", oneSet.DefinitionOfLowPopulationForPlayers.GetType(), oneSet.DefinitionOfLowPopulationForPlayers));

            if (isCTF) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase For Minutes <=", oneSet.DefinitionOfEarlyPhase.GetType(), oneSet.DefinitionOfEarlyPhase));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase For Minutes >=", oneSet.DefinitionOfLatePhase.GetType(), oneSet.DefinitionOfLatePhase));
            } else if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase For Tickets " + earlyEq, oneSet.DefinitionOfEarlyPhase.GetType(), oneSet.DefinitionOfEarlyPhase));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase For Tickets " + lateEq, oneSet.DefinitionOfLatePhase.GetType(), oneSet.DefinitionOfLatePhase));
            }

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

    DebugWrite(strVariable + " <- " + strValue, 6);

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
                DebugWrite(propertyName + " strValue = " + strValue, 6);
                if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success) {
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
                String perModeSetting = Regex.Replace(fieldPart, @"[^a-zA-Z_0-9]", String.Empty);

                perModeSetting = Regex.Replace(perModeSetting, @"(?:ForTickets|ForMinutes)", String.Empty);
                
                if (!fPerMode.ContainsKey(mode)) {
                    fPerMode[mode] = new PerModeSettings(mode);
                }
                PerModeSettings pms = fPerMode[mode];
                
                field = pms.GetType().GetField(perModeSetting, flags);
                
                DebugWrite("Mode: " + mode + ", Field: " + perModeSetting + ", Value: " + strValue, 6);
                
                if (field != null) {
                    fieldType = field.GetValue(pms).GetType();
                    if (fEasyTypeDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                    } else if (fListStrDict.ContainsValue(fieldType)) {
                        field.SetValue(pms, new List<string>(CPluginVariable.DecodeStringArray(strValue)));
                    } else if (fBoolDict.ContainsValue(fieldType)) {
                        if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success) {
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
                    DebugWrite("field is null", 6);
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
                DebugWrite("modes(" + modeList.Count + "):", 6);
                foreach (String m in modeList) {
                    DebugWrite(m, 6);
                }
            }
            
            ShowInLog = String.Empty;
        }

                
        /*
        switch (perModeSetting) {
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








/* ======================== OVERRIDES ============================= */










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
        "OnLevelLoaded",
        "OnPlayerKilledByAdmin",
        "OnPlayerMovedByAdmin",
        "OnPlayerIsAlive"
    );
}

public void OnPluginEnable() {
    fIsEnabled = true;
    fPluginState = PluginState.JustEnabled;
    fGameState = GameState.Unknown;
    ConsoleWrite("^bEnabled!^n Version = " + GetPluginVersion());
    DebugWrite("^b^3State = " + fPluginState, 6);
    DebugWrite("^b^3Game state = " + fGameState, 6);
    ServerCommand("serverInfo");
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

    Reset();
    
    ConsoleWrite("^bDisabled!^n");
    fPluginState = PluginState.Disabled;
    fGameState = GameState.Unknown;
    DebugWrite("^b^3State = " + fPluginState, 6);
    DebugWrite("^b^3Game state = " + fGameState, 6);
}


public override void OnVersion(String type, String ver) {
    DebugWrite("Got ^bOnVersion^n: " + type + " " + ver, 7);
}

public override void OnPlayerJoin(String soldierName) { }

public override void OnPlayerLeft(CPlayerInfo playerInfo) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerLeft^n", 7);
    
    if (IsKnownPlayer(playerInfo.SoldierName)) {
        RemovePlayer(playerInfo.SoldierName);
    }
    
    DebugWrite("Disconnected: ^b" + playerInfo.SoldierName, 3);
}

public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerTeamChange^n", 7);
    
    // Only teamId is valid for BF3, squad change is sent on separate event

    if (!IsKnownPlayer(soldierName)) {
        AddNewPlayer(soldierName, teamId);
        DebugWrite("^4New player^0: ^b" + soldierName + "^n, assigned to team " + teamId + " by game server", 3);
    } else {
        /*
         * We need to determine if this team change was instigated by a player or by an admin (plugin).
         * We want to ignore moves by admin. This is tricky due to the events possibly being 
         * in reverse order (team change first, then moved by admin). Use player.isAlive
         * to force a round trip with the game server, to insure that we get the admin move
         * event, if it exists.
         */
        if (fPendingTeamChange.ContainsKey(soldierName)) {
            // This is an admin move in correct order, ignore it
            fPendingTeamChange.Remove(soldierName);
            DebugWrite("Moved by admin: ^b" + soldierName + "^n to team " + teamId, 6);
            return;
        }

        // Remember the pending move in a table
        fPendingTeamChange[soldierName] = teamId;

        // Admin move event may still be on its way, so do a round-trip to check
        ServerCommand("player.isAlive", soldierName);
    }
}

public override void OnPlayerIsAlive(string soldierName, bool isAlive) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnPlayerIsAlive^n: ^b" + soldierName + "^n " + isAlive, 7);

    /*
     * This may be the return leg of the round-trip to insure that
     * an admin move event, if any, has been processed. If the player's
     * name is still in fPendingTeamChange, it's a real player instigated move
     */

    if (fPendingTeamChange.ContainsKey(soldierName)) {
        int team = fPendingTeamChange[soldierName];
        fPendingTeamChange.Remove(soldierName);
        
        DebugWrite("Player team switch: ^b" + soldierName + "^n to team " + team, 6);

        // Check if player is allowed to switch teams
        // Unswitch is handled in CheckTeamSwitch
        if (CheckTeamSwitch(soldierName, team)) {
            UpdatePlayerTeam(soldierName, team);
        }
    }
}

public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnPlayerMovedByAdmin^n: ^b" + soldierName + "^n " + destinationTeamId + "/" + destinationSquadId + " " + forceKilled, 7);

    if (fPendingTeamChange.ContainsKey(soldierName)) {
        // this is an admin move in reversed order, clear from pending table and ignore the move
        fPendingTeamChange.Remove(soldierName);
        DebugWrite("(REVERSED) Moved by admin: ^b" + soldierName + " to team " + destinationTeamId, 6);
    } else if (!fUnassigned.Contains(soldierName)) {
        // this is an admin move in correct order, add to pending table and let OnPlayerTeamChange handle it
        fPendingTeamChange[soldierName] = destinationTeamId;
    }
}


public override void OnPlayerKilled(Kill kKillerVictimDetails) {
    if (!fIsEnabled) return;
    
    String killer = kKillerVictimDetails.Killer.SoldierName;
    String victim = kKillerVictimDetails.Victim.SoldierName;
    String weapon = kKillerVictimDetails.DamageType;
    
    if (String.IsNullOrEmpty(killer)) killer = victim;
    
    DebugWrite("^9^bGot OnPlayerKilled^n: " + killer  + " -> " + victim + " (" + weapon + ")", 7);
    
    if (fGameState == GameState.Unknown) {
        fGameState = (TotalPlayerCount() < 4) ? GameState.Warmup : GameState.Playing;
        DebugWrite("^b^3Game state = " + fGameState, 6);  
    }
    
    KillUpdate(killer, victim);
    
    if (fModelIsInSync) BalanceAndUnstack(victim);
}

public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnListPlayers^n", 7);
    
    fUnassigned.Clear();
    
    foreach (CPlayerInfo p in players) {
        try {
            UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank);
        } catch (Exception e) {
            ConsoleException(e.ToString());
            continue;
        }
    }
    fModelIsInSync = true;
    
    DebugStatus();
    
    /* Special handling for JustEnabled state */
    if (fPluginState == PluginState.JustEnabled) {
        fPluginState = (TotalPlayerCount() < 4) ? PluginState.WaitingForPlayers : PluginState.Active;
        DebugWrite("^b^3State = " + fPluginState, 6);  
    }
}

public override void OnServerInfo(CServerInfo serverInfo) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 7);
    
    if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map) {
        DebugWrite("ServerInfo update: " + serverInfo.Map + "/" + serverInfo.GameMode, 3);
    }
    
    fServerInfo = serverInfo;
}

public override void OnResponseError(List<String> requestWords, String error) { }



public override void OnGlobalChat(String speaker, String message) { }

public override void OnTeamChat(String speaker, String message, int teamId) { }

public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

public override void OnRoundOverPlayers(List<CPlayerInfo> players) { }

public override void OnRoundOverTeamScores(List<TeamScore> teamScores) { }

public override void OnRoundOver(int winningTeamId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOver^n", 7);

    DebugWrite(":::::::::::: ^b^1Round over detected^0^n ::::::::::::", 2);
    
    if (fGameState == GameState.Playing || fGameState == GameState.Unknown) {
        fGameState = GameState.RoundEnding;
        DebugWrite("^b^3Game state = " + fGameState, 6);  
    }
}

public override void OnLoadingLevel(String mapFileName, int roundsPlayed, int roundsTotal) { }

public override void OnLevelStarted() { }

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnLevelLoaded^n", 7);

    DebugWrite(":::::::::::: ^b^1Level loaded detected^0^n ::::::::::::", 2);

    if (fGameState == GameState.RoundEnding || fGameState == GameState.Unknown) {
        fGameState = GameState.RoundStarting;
        DebugWrite("^b^3Game state = " + fGameState, 6);  
    }
}

public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerSpawned^n", 7);
    
    if (fGameState == GameState.Unknown) {
        fGameState = (TotalPlayerCount() < 4) ? GameState.Warmup : GameState.Playing;
        DebugWrite("^b^3Game state = " + fGameState, 6);  
    }

    if (fGameState == GameState.RoundStarting) {
        DebugWrite(":::::::::::: ^b^1First spawn detected^0^n ::::::::::::", 2);

        fGameState = (TotalPlayerCount() < 4) ? GameState.Warmup : GameState.Playing;
        DebugWrite("^b^3Game state = " + fGameState, 6);
        
        fRoundStartTimestamp = DateTime.Now;
    }
    
    SpawnUpdate(soldierName);
    
}


public override void OnPlayerKilledByAdmin(string soldierName) {
    // TBD for m.IsDeployed
}









/* ======================== CORE ENGINE ============================= */










public void BalanceAndUnstack(String name) {
}

public bool IsKnownPlayer(String name) {
    bool check = false;
    lock (fAllPlayers) {
        check = fAllPlayers.Contains(name);
    }
    return check;
}

public bool AddNewPlayer(String name, int team) {
    bool stateChange = false;
    bool gameChange = false;
    bool known = false;
    lock (fKnownPlayers) {
        if (!fKnownPlayers.ContainsKey(name)) {
            fKnownPlayers[name] = new PlayerModel(name, team);
        } else {
            fKnownPlayers[name].Team = team;
            known = true;
        }
        fKnownPlayers[name].LastSeenTimestamp = DateTime.Now;
    }
    lock (fAllPlayers) {
        if (!fAllPlayers.Contains(name)) fAllPlayers.Add(name);
        if (fAllPlayers.Count < 4) {
            if (fGameState == GameState.Unknown) {
                fGameState = GameState.Warmup;
                gameChange = true;
            }
        } else {
            if (fPluginState == PluginState.WaitingForPlayers) {
                fPluginState = PluginState.Active;
                stateChange = true;
            }
            if (fGameState == GameState.Warmup || fGameState == GameState.Unknown) {
                fGameState = GameState.Playing;
                gameChange = true;
            }
        }
    }
    if (stateChange) {
        DebugWrite("^b^3State = " + fPluginState, 6);
    }
    if (gameChange) {
        DebugWrite("^b^3Game state = " + fGameState, 6);
    }
    return known;
}

public void RemovePlayer(String name) {
    bool stateChange = false;
    bool gameChange = false;
    lock (fKnownPlayers) {
        if (fKnownPlayers.ContainsKey(name)) {
            fKnownPlayers[name].LastSeenTimestamp = DateTime.Now;
        }
    }
    lock (fAllPlayers) {
        if (fAllPlayers.Contains(name)) fAllPlayers.Remove(name);
    
        if (fAllPlayers.Count < 4) {
            if (fPluginState != PluginState.WaitingForPlayers) {
                fPluginState = PluginState.WaitingForPlayers;
                stateChange = true;
            }
            if (fGameState != GameState.Warmup) {
                fGameState = GameState.Warmup;
                gameChange = true;
            }
        }
    }
    if (stateChange) {
        DebugWrite("^b^3State = " + fPluginState, 6);
    }
    if (gameChange) {
        DebugWrite("^b^3Game state = " + fGameState, 6);
    }
}

public int TotalPlayerCount() {
    lock (fAllPlayers) {
        return fAllPlayers.Count;
    }
}

public void UpdatePlayerModel(String name, int team, int squad, String eaGUID, int score, int kills, int deaths, int rank) {
    bool known = false;
    if (!IsKnownPlayer(name)) {
        switch (fPluginState) {
            case PluginState.JustEnabled:
                if (team != 0) {
                    known = AddNewPlayer(name, team);
                    String verb = (known) ? "^6renewing^0" : "^4adding^0";
                    DebugWrite("JustEnabled state, " + verb + " new player: ^b" + name, 3);
                } else {
                    DebugWrite("JustEnabled state, unassigned player: ^b" + name, 3);
                    if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                    return;
                }
                break;
            case PluginState.Active:
            case PluginState.WaitingForPlayers:
                DebugWrite("Update waiting for ^b" + name + "^n to be assigned a team", 3);
                if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                return;
                break;
            case PluginState.Error:
                DebugWrite("Error state, adding new player: ^b" + name, 3);
                AddNewPlayer(name, team);
                break;
            default:
                return;
        }          
    }
    
    if (!fKnownPlayers.ContainsKey(name)) {
        DebugWrite("^b^1ERROR^0^n: player ^b" + name + "^n not in master table!", 1);
        fPluginState = PluginState.Error;
        return;
    }
    
    bool unex = false;
    int unTeam = -1;
    
    lock (fKnownPlayers) {
    
        PlayerModel m = fKnownPlayers[name];
    
        if (m.Team != team) {
            unTeam = m.Team;
            m.Team = team;
        }
        m.Squad = squad;
        m.EAGUID = eaGUID;
        m.ScoreRound = score;
        m.KillsRound = kills;
        m.DeathsRound = deaths;
        m.Rank = rank;

        m.LastSeenTimestamp = DateTime.Now;

        // Computed
        m.KDRRound = m.KillsRound / Math.Max(1, m.DeathsRound);
        double mins = (m.FirstSpawnTimestamp == DateTime.MinValue) ? 1 : Math.Max(1, DateTime.Now.Subtract(m.FirstSpawnTimestamp).TotalMinutes);
        m.SPMRound = m.ScoreRound / mins;

        // Accumulated
        // TBD


        fKnownPlayers[name] = m;
    }

    if (unex) {
        DebugWrite("^b^1UNEXPECTED^0^n: player model for ^b" + name + "^n has team " + unTeam + " but update says " + team + "!", 3);
    }
}


public void UpdatePlayerTeam(String name, int team) {
    if (!IsKnownPlayer(name) || !fKnownPlayers.ContainsKey(name)) {
        DebugWrite("^b^1UNEXPECTED^0^n: UpdatePlayerTeam(" + name + ", " + team + ")", 3);
        return;
    }
    
    PlayerModel m = fKnownPlayers[name];
    
    if (m.Team != team) {
        if (m.Team == 0) {
            DebugWrite("Assigning ^b" + name + "^n to " + team, 3);
        } else {
            DebugWrite("Team switch: ^b" + name + "^n from " + m.Team + " to " + team, 3);
            m.Team = team;
        }
        m.LastSeenTimestamp = DateTime.Now;
        fKnownPlayers[name] = m;
    }
}



public bool CheckTeamSwitch(String name, int team) {
    // Team
    return true; // means switch is okay
}



public void SpawnUpdate(String name) {
    if (fPluginState != PluginState.Active) return;
    bool ok = false;
    bool updated = false;
    DateTime now = DateTime.Now;
    lock (fKnownPlayers) {
        PlayerModel m = null;
        if (fKnownPlayers.TryGetValue(name, out m)) {
            ok = true;
            // If first spawn timestamp is earlier than round start, update it
            if (m.FirstSpawnTimestamp == DateTime.MinValue || DateTime.Compare(m.FirstSpawnTimestamp, fRoundStartTimestamp) < 0) {
                m.FirstSpawnTimestamp = now;
                updated = true;
            } else {
                m.LastSeenTimestamp = now;
            }
            m.IsDeployed = true;
        }
    }    

    if (!ok) {
        DebugWrite("^b^1UNEXPECTED^0^n: player " + name + " spawned, but not a known player!", 3);
    }

    if (updated) {
        DebugWrite("^9Spawn: ^b" + name + "^n @ " + now.ToString("HH:mm:ss"), 6);
    }
}


public void KillUpdate(String killer, String victim) {
    if (fPluginState != PluginState.Active) return;
    bool okVictim = false;
    bool okKiller = false;
    DateTime now = DateTime.Now;
    double score = -1;
    double kills = -1;
    double deaths = -1;
    double kdr = -1;
    double spm = -1;
    TimeSpan tir = TimeSpan.FromSeconds(0); // Time In Round
    lock (fKnownPlayers) {
        PlayerModel m = null;
        if (killer != victim && fKnownPlayers.TryGetValue(killer, out m)) {
            okKiller = true;
            m.LastSeenTimestamp = now;
            m.IsDeployed = true;
        } else if (killer == victim) {
            okKiller = true;
        }
        if (fKnownPlayers.TryGetValue(victim, out m)) {
            okVictim = true;
            m.LastSeenTimestamp = now;
            m.IsDeployed = false;
            score = m.ScoreRound;
            kills = m.KillsRound;
            deaths = m.DeathsRound;
            kdr = m.KDRRound;
            spm = m.SPMRound;
            tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
        }
    }

    if (!okKiller) {
        DebugWrite("^b^1UNEXPECTED^0^n: player ^b" + killer + "^n is a killer, but not a known player!", 3);
    }
    
    if (!okVictim) {
        DebugWrite("^b^1UNEXPECTED^0^n: player ^b" + victim + "^n is a victim, but not a known player!", 3);
    } else {
        Match rm = Regex.Match(tir.ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
        String sTIR = (rm.Success) ? rm.Groups[1].Value : "?";
        DebugWrite("^9STATS: ^b" + victim + "^n [S:" + score + ", K:" + kills + ", D:" + deaths + ", KDR: " + kdr.ToString("F2") + ", SPM: " + spm.ToString("F0") + ", TIR: " + sTIR + "]", 6);
    }
}







/* ======================== SUPPORT FUNCTIONS ============================= */










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
    if (DebugLevel >= 3) ConsoleWrite(msg, MessageType.Exception);
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

public void Reset() {
    fAllPlayers.Clear();
    
    fTeam1.Clear();
    fTeam2.Clear();
    fTeam3.Clear();
    fTeam4.Clear();

    /*
     * fKnownPlayers is not cleared, since we want to retain stats from previous plugin sessions
     */
}

public bool IsSQDM() {
    if (fServerInfo == null) return false;
    return (fServerInfo.GameMode == "SquadDeathMatch0");
}

public int MaxDiff() {
    // TBD - based on per mode population settings
    return 1;
}

public void ListTeams() {
    fTeam1.Clear();
    fTeam2.Clear();
    fTeam3.Clear();
    fTeam4.Clear();

    foreach (String name in fAllPlayers) {
        if (!fKnownPlayers.ContainsKey(name)) {
            throw new Exception("ListTeams: " + name + " not in fKnownPlayers");
        }
        switch (fKnownPlayers[name].Team) {
            case 1: fTeam1.Add(fKnownPlayers[name]); break;
            case 2: fTeam2.Add(fKnownPlayers[name]); break;
            case 3: fTeam3.Add(fKnownPlayers[name]); break;
            case 4: fTeam4.Add(fKnownPlayers[name]); break;
            default: break;
        }
    }
}

public int ToTeam(int fromTeam) {
    if (fromTeam < 1 || fromTeam > 4) return 0;

    ListTeams();

    List<TeamRoster> teams = new List<TeamRoster>();
    List<PlayerModel> from = null;
    
    if (fromTeam == 1) {
        from = fTeam1;
    } else {
        teams.Add(new TeamRoster(1, fTeam1));
    }
    if (fromTeam == 2) {
        from = fTeam2;
    } else {
        teams.Add(new TeamRoster(2, fTeam2));
    }
    if (IsSQDM()) {
        if (fromTeam == 3) {
            from = fTeam3;
        } else {
            teams.Add(new TeamRoster(3, fTeam3));
        }
        if (fromTeam == 4) {
            from = fTeam4;
        } else {
            teams.Add(new TeamRoster(4, fTeam4));
        }

        teams.Sort(delegate(TeamRoster lhs, TeamRoster rhs) {
            // Sort ascending order by count
            if (lhs == null || rhs == null) return 0;
            if (lhs.Roster.Count < rhs.Roster.Count) return -1;
            if (lhs.Roster.Count > rhs.Roster.Count) return 1;
            return 0;
        });
    }

    if (from.Count == 0) return 0;

    TeamRoster to = teams[0];

    if (to.Roster.Count >= from.Count) return 0;

    if ((from.Count - to.Roster.Count) <= MaxDiff()) return 0;

    return to.Team;
}

public void DebugStatus() {
    
    ListTeams();
    
    DebugWrite("^bStatus^n: Plugin state = " + fPluginState + ", game state = " + fGameState + ", mode = " + fServerInfo.GameMode, 3);
    
    if (IsSQDM()) {
        DebugWrite("^bStatus^n: Team counts = " + fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D), with " + fUnassigned.Count + " unassigned", 3);
    } else {
        DebugWrite("^bStatus^n: Team counts = " + fTeam1.Count + "(US) vs " + fTeam2.Count + "(RU), with " + fUnassigned.Count + " unassigned", 3);
    }
    
    List<int> counts = new List<int>();
    counts.Add(fTeam1.Count);
    counts.Add(fTeam2.Count);
    if (IsSQDM()) {
        counts.Add(fTeam3.Count);
        counts.Add(fTeam4.Count);
    }
    
    counts.Sort();
    int diff = Math.Abs(counts[0] - counts[counts.Count-1]);
    
    DebugWrite("^bStatus^n: Max difference = " + ((diff > 2) ? "^8^b" : "^b") + diff, 3);
}


} // end PROTObalancer











/* ======================== UTILITIES ============================= */









static class PROTObalancerUtils {
    public static bool IsEqual(PROTObalancer lhs, PROTObalancer.PresetItems preset) {
        PROTObalancer rhs = new PROTObalancer(preset);
        return (lhs.CheckForEquality(rhs));
    }
    
    public static void UpdateSettingsForPreset(PROTObalancer lhs, PROTObalancer.PresetItems preset) {
        PROTObalancer rhs = new PROTObalancer(preset);
        
        lhs.DebugWrite("UpdateSettingsForPreset to " + preset, 6);

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

    public static bool EqualArrays(PROTObalancer.Speed[] lhs, PROTObalancer.Speed[] rhs) {
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

    public static String ArrayToString(PROTObalancer.Speed[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = Enum.GetName(typeof(PROTObalancer.Speed), a[i]);
                first = false;
            } else {
                ret = ret + ", " + Enum.GetName(typeof(PROTObalancer.Speed), a[i]);
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

    public static PROTObalancer.Speed[] ParseSpeedArray(String s) {
        PROTObalancer.Speed[] speeds = new PROTObalancer.Speed[3] {
            PROTObalancer.Speed.Click_Here_For_Speed_Names,
            PROTObalancer.Speed.Click_Here_For_Speed_Names,
            PROTObalancer.Speed.Click_Here_For_Speed_Names
        };
        if (String.IsNullOrEmpty(s)) return speeds;
        if (!s.Contains(",")) return speeds;
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) return speeds;
        for (int i = 0; i < speeds.Length; ++i) {
            try {
                speeds[i] = (PROTObalancer.Speed)Enum.Parse(typeof(PROTObalancer.Speed), strs[i]);
            } catch (Exception e) {
                // TBD log an error about a bogus value?
                speeds[i] = PROTObalancer.Speed.Click_Here_For_Speed_Names;
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

