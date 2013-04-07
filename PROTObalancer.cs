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

    public enum MessageType { Warning, Error, Exception, Normal, Debug };
    
    public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, BalanceOnly, UnstackOnly, None };

    public enum Speed { Click_Here_For_Speed_Names, Stop, Slow, Adaptive, Fast };

    public enum DefineStrong { RoundScore, RoundKDR, RoundKills, PlayerRank };
    
    public enum PluginState { Disabled, JustEnabled, Active, Error, Reconnected };
    
    public enum GameState { RoundEnding, RoundStarting, Playing, Warmup, Unknown };

    public enum ReasonFor { Balance, Unstack, Unswitch };

    public enum Phase {Early, Mid, Late};

    public enum Population {Low, Medium, High};

    /* Constants & Statics */

    public const int DUMMY = 2;

    public const double MODEL_TIMEOUT = 24*60; // in minutes

    public const int CRASH_COUNT_HEURISTIC = 6; // player count difference signifies a crash

    public static String[] TEAM_NAMES = new String[] { "None", "US", "RU" };

    public static String[] SQUAD_NAMES = new String[] { "None",
      "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
      "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa",
      "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray",
      "Yankee", "Zulu", "Haggard", "Sweetwater", "Preston", "Redford", "Faith", "Celeste"
    };

    /* Classes */

    public class PerModeSettings {
        public PerModeSettings() {}
        
        public PerModeSettings(String simplifiedModeName) {
            DetermineStrongPlayersBy = DefineStrong.RoundScore;
            DisperseEvenlyForRank = 145;
            
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
                    DefinitionOfEarlyPhase = 10;
                    DefinitionOfLatePhase = 40;
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
                    DefinitionOfEarlyPhase = 20;
                    DefinitionOfLatePhase = 80;
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
        public int DisperseEvenlyForRank = 145;
        
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
        public DateTime FirstSeenTimestamp; // on player join or plugin enable
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
        public String FullName { get {return (String.IsNullOrEmpty(Tag) ? Name : "[" + Tag + "]" + Name);}}
        
        // Computed
        public double KDRRound;
        public double SPMRound;
        
        // Accumulated
        public double ScoreTotal; // not including current round
        public double KillsTotal; // not including current round
        public double DeathsTotal; // not including current round

        //  Per-round state
        public int MovesRound;
        public bool MovedByMB;
        
        public PlayerModel() {
            Name = null;
            Team = -1;
            Squad = -1;
            EAGUID = String.Empty;
            FirstSeenTimestamp = DateTime.Now;
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
            MovesRound = 0;
            MovedByMB = false;
        }
        
        public PlayerModel(String name, int team) : this() {
            Name = name;
            Team = team;
        }

        public void ResetRound() {
            ScoreTotal = ScoreTotal + ScoreRound;
            KillsTotal = KillsTotal + KillsRound;
            DeathsTotal = DeathsTotal + DeathsRound;
            Rounds = (Rounds > 0) ? Rounds + 1 : 1;

            ScoreRound = -1;
            KillsRound = -1;
            DeathsRound = -1;
            KDRRound = -1;
            SPMRound = -1;
            IsDeployed = false;

            MovesRound = 0;
            MovedByMB = false;
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

    public class MoveInfo {
        public ReasonFor Reason = ReasonFor.Balance;
        public String Name = String.Empty;
        public String Tag = String.Empty;
        public int Source = -1;
        public String SourceName = String.Empty;
        public int Destination = -1;
        public String DestinationName = String.Empty;
        public String ChatBefore = String.Empty;
        public String YellBefore = String.Empty;
        public String ChatAfter = String.Empty;
        public String YellAfter = String.Empty;

        public MoveInfo() {}

        public MoveInfo(String name, String tag, int fromTeam, String fromName, int toTeam, String toName) : this() {
            Name = name;
            Tag = tag;
            Source = fromTeam;
            SourceName = (String.IsNullOrEmpty(fromName)) ? fromTeam.ToString() : fromName;
            Destination = toTeam;
            DestinationName = (String.IsNullOrEmpty(toName)) ? toTeam.ToString() : toName;
        }

        public void Format(String fmt, bool isYell, bool isBefore) {
            String expanded = fmt;

            if (String.IsNullOrEmpty(expanded)) return;

            if (expanded.Contains("%name%")) expanded = expanded.Replace("%name%", Name);
            if (expanded.Contains("%tag%")) expanded = expanded.Replace("%tag%", Tag);
            if (expanded.Contains("%fromTeam%")) expanded = expanded.Replace("%fromTeam", SourceName);
            if (expanded.Contains("%toTeam%")) expanded = expanded.Replace("%toTeam%", DestinationName);

            if (isYell) {
                if (isBefore) {
                    YellBefore = expanded;
                } else {
                    YellAfter = expanded;
                }
            } else {
                if (isBefore) {
                    ChatBefore = expanded;
                } else {
                    ChatAfter = expanded;
                }
            }
        }

        public override String ToString() {
            String s = "Move(";
            s += "[" + Tag + "]" + Name + ",";
            s += Reason + ",";
            s += Source + "(" + SourceName + "),";
            s += Destination + "(" + DestinationName + "),";
            s += "C'" + ChatBefore + "',";
            s += "Y'" + YellBefore + "',";
            s += "C'" + ChatAfter + "',";
            s += "Y'" + YellAfter + "')";
            return s;
        }
    } // end MoveInfo

    public class ListPlayersRequest {
        public double MaxDelay; // in seconds
        public DateTime LastUpdate;

        public ListPlayersRequest() {
            MaxDelay = 0;
            LastUpdate = DateTime.MinValue;
        }

        public ListPlayersRequest(double delay, DateTime last) {
            MaxDelay = delay;
            LastUpdate = last;
        }
    } // end ListPlayersRequest

/* Inherited:
    this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
    this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
*/

// General
private bool fIsEnabled;
private bool fFinalizerActive = false;
private Dictionary<String,String> fModeToSimple = null;
private Dictionary<String,int> fPendingTeamChange = null;
private Thread fMoveThread = null;
private Thread fListPlayersThread = null;
private List<String> fReservedSlots = null;
private bool fGotVersion = false;
private int fServerUptime = -1;
private bool fServerCrashed = false; // because fServerUptime >  fServerInfo.ServerUptime
private DateTime fLastBalancedTimestamp = DateTime.MinValue;
private DateTime fLastUnbalancedTimestamp = DateTime.MinValue;

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
private DateTime fRoundStartTimestamp;
private Dictionary<String, MoveInfo> fMoving = null;
private Queue<MoveInfo> fMoveQ = null;
private List<String> fReassigned = null;
private int[] fTickets = null;
private DateTime fListPlayersTimestamp;
private Queue<ListPlayersRequest> fListPlayersQ = null;
private Dictionary<String,String> fFriendlyMaps = null;
private Dictionary<String,String> fFriendlyModes = null;
private double fMaxTickets = -1;
private bool fFinalStatus = false;

// Operational statistics
private int fReassignedRound = 0;
private int fBalancedRound = 0;
private int fUnstackedRound = 0;
private int fUnswitchedRound = 0;
private int fExcludedRound = 0;
private int fExemptRound = 0;
private int fFailedRound = 0;
private int fTotalRound = 0;

// Settings support
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

// Settings
public int DebugLevel;
public int MaximumServerSize;
public bool EnableBattlelogRequests;
public int MaximumRequestRate;
public int MaxTeamSwitchesByStrongPlayers; // disabled
public int MaxTeamSwitchesByWeakPlayers; // disabled
public double UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
public bool Enable2SlotReserve; // disabled
public bool EnablerecruitCommand; // disabled
public bool EnableWhitelistingOfReservedSlotsList;
public String[] Whitelist;
public String[] DisperseEvenlyList;
public PresetItems Preset;
public double MaximumPassiveBalanceSeconds;

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
public bool EnableLoggingOnlyMode;

// Properties
public int TotalPlayerCount {get{lock (fAllPlayers) {return fAllPlayers.Count;}}}
public String FriendlyMap { 
    get {
        if (fServerInfo == null) return "???";
        String r = null;
        return (fFriendlyMaps.TryGetValue(fServerInfo.Map, out r)) ? r : fServerInfo.Map;
    }
}
public String FriendlyMode { 
    get {
        if (fServerInfo == null) return "???";
        String r = null;
        return (fFriendlyModes.TryGetValue(fServerInfo.GameMode, out r)) ? r : fServerInfo.GameMode;
    }
}



/* Constructor */

public PROTObalancer() {
    /* Private members */
    fIsEnabled = false;
    fFinalizerActive = false;
    fPluginState = PluginState.Disabled;
    fGameState = GameState.Unknown;
    fServerInfo = null;
    fGotVersion = false;
    fServerUptime = 0;
    fServerCrashed = false;

    fBalancedRound = 0;
    fUnstackedRound = 0;
    fUnswitchedRound = 0;
    fExcludedRound = 0;
    fExemptRound = 0;
    fFailedRound = 0;
    fTotalRound = 0;

    fMoveThread = null;
    fListPlayersThread = null;
    
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
    fListPlayersTimestamp = DateTime.MinValue;
    fListPlayersQ = new Queue<ListPlayersRequest>();

    fPendingTeamChange = new Dictionary<String,int>();
    fMoving = new Dictionary<String, MoveInfo>();
    fMoveQ = new Queue<MoveInfo>();
    fReassigned = new List<String>();
    fReservedSlots = new List<String>();
    fTickets = new int[5]{0,0,0,0,0};
    fFriendlyMaps = new Dictionary<String,String>();
    fFriendlyModes = new Dictionary<String,String>();
    fMaxTickets = -1;
    fLastBalancedTimestamp = DateTime.MinValue;
    fLastUnbalancedTimestamp = DateTime.MinValue;
    fFinalStatus = false;
    
    /* Settings */

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = 2;
    MaximumServerSize = 64;
    EnableBattlelogRequests = true;
    MaximumRequestRate = 2; // in 20 seconds
    MaxTeamSwitchesByStrongPlayers = 1;
    MaxTeamSwitchesByWeakPlayers = 2;
    UnlimitedTeamSwitchingDuringFirstMinutesOfRound = 5.0;
    Enable2SlotReserve = false;
    EnablerecruitCommand = false;
    EnableWhitelistingOfReservedSlotsList = true;
    Whitelist = new String[] {"[-- name, tag, or EA_GUID --]"};
    DisperseEvenlyList = new String[] {"[-- name, tag, or EA_GUID --]"};
    Preset = PresetItems.Standard;
    MaximumPassiveBalanceSeconds = 3*60;
    
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
    EnableLoggingOnlyMode = true; // TBD false
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
    return "0.0.0.9";
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

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Battlelog Requests", EnableBattlelogRequests.GetType(), EnableBattlelogRequests));

        if (EnableBattlelogRequests) lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Request Rate", MaximumRequestRate.GetType(), MaximumRequestRate));

/*
        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Strong Players", MaxTeamSwitchesByStrongPlayers.GetType(), MaxTeamSwitchesByStrongPlayers));

        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Weak Players", MaxTeamSwitchesByWeakPlayers.GetType(), MaxTeamSwitchesByWeakPlayers));
*/

        lstReturn.Add(new CPluginVariable("1 - Settings|Unlimited Team Switching During First Minutes Of Round", UnlimitedTeamSwitchingDuringFirstMinutesOfRound.GetType(), UnlimitedTeamSwitchingDuringFirstMinutesOfRound));

/*
        lstReturn.Add(new CPluginVariable("1 - Settings|Enable 2 Slot Reserve", Enable2SlotReserve.GetType(), Enable2SlotReserve));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable @#!recruit Command", EnablerecruitCommand.GetType(), EnablerecruitCommand));
*/

        lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Passive Balance Seconds", MaximumPassiveBalanceSeconds.GetType(), MaximumPassiveBalanceSeconds));
        
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

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Disperse Evenly For Rank >=", oneSet.DisperseEvenlyForRank.GetType(), oneSet.DisperseEvenlyForRank));

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

        lstReturn.Add(new CPluginVariable("9 - Debugging|Enable Logging Only Mode", EnableLoggingOnlyMode.GetType(), EnableLoggingOnlyMode));


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
                    Speed[] items = PROTObalancerUtils.ParseSpeedArray(this, strValue); // also validates
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
            CommandToLog(ShowInLog);
            ShowInLog = String.Empty;
        }

        if (MaximumPassiveBalanceSeconds < 120) {
            ConsoleWarn("Maximum Passive Balance Seconds must be 120 or greater!");
            MaximumPassiveBalanceSeconds = 120;
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

private void ResetSettings() {
    PROTObalancer rhs = new PROTObalancer();

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = rhs.DebugLevel;
    MaximumServerSize = rhs.MaximumServerSize;
    EnableBattlelogRequests = rhs.EnableBattlelogRequests;
    MaximumRequestRate =  rhs.MaximumRequestRate;
    MaxTeamSwitchesByStrongPlayers = rhs.MaxTeamSwitchesByStrongPlayers;
    MaxTeamSwitchesByWeakPlayers = rhs.MaxTeamSwitchesByWeakPlayers;
    UnlimitedTeamSwitchingDuringFirstMinutesOfRound = rhs.UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
    Enable2SlotReserve = rhs.Enable2SlotReserve;
    EnablerecruitCommand = rhs.EnablerecruitCommand;
    EnableWhitelistingOfReservedSlotsList = rhs.EnableWhitelistingOfReservedSlotsList;
    Whitelist = rhs.Whitelist;
    DisperseEvenlyList = rhs.DisperseEvenlyList;
    Preset = rhs.Preset;
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = rhs.OnWhitelist;
    TopScorers = rhs.TopScorers;
    SameClanTagsInSquad = rhs.SameClanTagsInSquad;
    MinutesAfterJoining = rhs.MinutesAfterJoining;
    JoinedEarlyPhase = rhs.JoinedEarlyPhase;
    JoinedMidPhase = rhs.JoinedMidPhase;
    JoinedLatePhase = rhs.JoinedLatePhase;


    /* ===== SECTION 3 - Round Phase & Population Settings ===== */

    EarlyPhaseTicketPercentageToUnstack = rhs.EarlyPhaseTicketPercentageToUnstack;
    MidPhaseTicketPercentageToUnstack = rhs.MidPhaseTicketPercentageToUnstack;
    LatePhaseTicketPercentageToUnstack = rhs.LatePhaseTicketPercentageToUnstack;
    
    SpellingOfSpeedNamesReminder = rhs.SpellingOfSpeedNamesReminder;

    EarlyPhaseBalanceSpeed = rhs.EarlyPhaseBalanceSpeed;
    MidPhaseBalanceSpeed = rhs.MidPhaseBalanceSpeed;
    LatePhaseBalanceSpeed = rhs.LatePhaseBalanceSpeed;

    /* ===== SECTION 5 - Messages ===== */
    
    QuietMode =  rhs.QuietMode;
    YellDurationSeconds = rhs.YellDurationSeconds;
    ChatMovedForBalance = rhs.ChatMovedForBalance;
    YellMovedForBalance = rhs.YellMovedForBalance;
    ChatMovedToUnstack = rhs.ChatMovedToUnstack;
    YellMovedToUnstack = rhs.YellMovedToUnstack;
    ChatDetectedSwitchToWinningTeam = rhs.ChatDetectedSwitchToWinningTeam;
    YellDetectedSwitchToWinningTeam = rhs.YellDetectedSwitchToWinningTeam;
    ChatDetectedSwitchToLosingTeam = rhs.ChatDetectedSwitchToLosingTeam;
    YellDetectedSwitchToLosingTeam = rhs.YellDetectedSwitchToLosingTeam;
    ChatAfterUnswitching = rhs.ChatAfterUnswitching;
    YellAfterUnswitching = rhs.YellAfterUnswitching;
    ChatDetectedSwitchByDispersalPlayer = rhs.ChatDetectedSwitchByDispersalPlayer;
    YellDetectedSwitchByDispersalPlayer = rhs.YellDetectedSwitchByDispersalPlayer;
    ChatAfterUnswitchingDispersalPlayer = rhs.ChatAfterUnswitchingDispersalPlayer;
    YellAfterUnswitchingDispersalPlayer = rhs.YellAfterUnswitchingDispersalPlayer;
    
    /* ===== SECTION 6 - TBD ===== */

    /* ===== SECTION 7 - TBD ===== */

    /* ===== SECTION 8 - Per-Mode Settings ===== */

    List<String> simpleModes = GetSimplifiedModes();

    fPerMode.Clear();

    foreach (String sm in simpleModes) {
        PerModeSettings oneSet = null;
        if (!fPerMode.ContainsKey(sm)) {
            oneSet = new PerModeSettings(sm);
            fPerMode[sm] = oneSet;
        }
    }

    /* ===== SECTION 9 - Debug Settings ===== */

    ShowInLog = rhs.ShowInLog;
    LogChat = rhs.LogChat;
    EnableLoggingOnlyMode = rhs.EnableLoggingOnlyMode;
}

private void CommandToLog(string cmd) {
    try {
        Match m = null;
        ConsoleDump("Command: " + cmd);

        if (Regex.Match(cmd, @"help", RegexOptions.IgnoreCase).Success) {
            ConsoleDump("^1^bmodes^n^0: List the known game modes");
            ConsoleDump("^1^breset settings^n^0: Reset all plugin settings to default");
            ConsoleDump("^1^bsort^n ^iteam^n ^itype^n^0: Sort ^iteam^n (1-4) by ^itype^n (one of: score, kills, kdr, rank)");
            return;
        }

        if (Regex.Match(cmd, @"modes", RegexOptions.IgnoreCase).Success) {
            List<String> modeList = GetSimplifiedModes();
            ConsoleDump("modes(" + modeList.Count + "):");
            foreach (String mode in modeList) {
                ConsoleDump(mode);
            }
            return;
        }
        
        if (Regex.Match(cmd, @"reset settings", RegexOptions.IgnoreCase).Success) {
            ConsoleDump("^8^bRESETTING ALL PLUGIN SETTINGS TO DEFAULT!");
            ResetSettings();
            return;
        }

        m = Regex.Match(cmd, @"^sort\s+([1-4])\s+(score|kills|kdr|rank)", RegexOptions.IgnoreCase);
        if (m.Success) {
            String teamID = m.Groups[1].Value;
            String propID = m.Groups[2].Value;

            int team = 0;
            if (!Int32.TryParse(teamID, out team) || team < 1 || team > 4) {
                ConsoleDump("Invalid team: " + teamID);
                return;
            }
            List<PlayerModel> fromList = GetTeam(team);
            if (fromList == null || fromList.Count < 3) {
                ConsoleDump("Invalid team or not enough players in team: " + team);
                return;
            }
            switch (propID.ToLower()) {
                case "score":
                    fromList.Sort(DescendingRoundScore);
                    break;
                case "kills":
                    fromList.Sort(DescendingRoundKills);
                   break;
                case "kdr":
                    fromList.Sort(DescendingRoundKDR);
                    break;
                case "rank":
                    fromList.Sort(DescendingPlayerRank);
                    break;
                default:
                    fromList.Sort(DescendingRoundScore);
                    break;
            }
            int n = 1;
            foreach (PlayerModel p in fromList) {
                switch (propID.ToLower()) {
                    case "score":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6}", p.ScoreRound) + ", ^b" + p.FullName);
                        break;
                    case "kills":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Kills: " + String.Format("{0,6}", p.KillsRound) + ", ^b" + p.FullName);
                       break;
                    case "kdr":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") KDR: " + String.Format("{0,6}", p.KDRRound) + ", ^b" + p.FullName);
                        break;
                    case "rank":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Rank: " + String.Format("{0,6}", p.Rank) + ", ^b" + p.FullName);
                        break;
                    default:
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6}", p.ScoreRound) + ", ^b" + p.FullName);
                        break;
                }
                n = n + 1;
            }
        }

            

    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}









/* ======================== OVERRIDES ============================= */










public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
    this.RegisterEvents(this.GetType().Name, 
        "OnVersion",
        "OnServerInfo",
        //"OnResponseError",
        "OnListPlayers",
        //"OnPlayerJoin",
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
        "OnPlayerIsAlive",
        "OnReservedSlotsList"
    );
}

public void OnPluginEnable() {
    if (fFinalizerActive) {
        ConsoleWarn("Not done disabling, try again in 10 seconds!");
        return;
    }
    fIsEnabled = true;
    fPluginState = PluginState.JustEnabled;
    fGameState = GameState.Unknown;
    fRoundStartTimestamp = DateTime.Now;

    ConsoleWrite("^bEnabled!^n Version = " + GetPluginVersion());
    DebugWrite("^b^3State = " + fPluginState, 6);
    DebugWrite("^b^3Game state = " + fGameState, 6);

    GatherProconGoodies();

    StartThreads();

    ServerCommand("reservedSlotsList.list");
    ServerCommand("serverInfo");
    ServerCommand("admin.listPlayers", "all");
}


public void OnPluginDisable() {
    fIsEnabled = false;

    try {
        StopThreads();

        Reset();
    
        ConsoleWrite("^bDisabling, stopping threads ...^n");
        fPluginState = PluginState.Disabled;
        fGameState = GameState.Unknown;
        DebugWrite("^b^3State = " + fPluginState, 6);
        DebugWrite("^b^3Game state = " + fGameState, 6);
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}


public override void OnVersion(String type, String ver) {
    if (!fIsEnabled) return;
    
    DebugWrite("Got ^bOnVersion^n: " + type + " " + ver, 7);
    try {
        fGotVersion = true;
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnPlayerJoin(String soldierName) { }

public override void OnPlayerLeft(CPlayerInfo playerInfo) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerLeft:^n " + playerInfo.SoldierName, 8);

    try {
        if (IsKnownPlayer(playerInfo.SoldierName)) {
            RemovePlayer(playerInfo.SoldierName);
        }
    
        DebugWrite("Player left: ^b" + playerInfo.SoldierName, 3);
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerTeamChange^n: " + soldierName + " " + teamId + " " + squadId, 7);

    try {
        // Only teamId is valid for BF3, squad change is sent on separate event

        // Handle team change event
        if (fReassigned.Contains(soldierName)) {
            // We reassigned this new player
            fReassigned.Remove(soldierName);
            IncrementTotal();
            fReassignedRound = fReassignedRound + 1;
            AddNewPlayer(soldierName, teamId);
            UpdateTeams();
            DebugWrite("^4New player^0: ^b" + soldierName + "^n, ^b^1REASSIGNED^0^n to team " + teamId + " by " + GetPluginName(), 3);
       } else if (!IsKnownPlayer(soldierName)) {
            int diff = 0;
            bool mustMove = false;
            int reassignTo = ToTeam(soldierName, teamId, out diff, out mustMove);
            if (reassignTo == 0 || reassignTo == teamId) {
                // New player was going to the right team anyway
                IncrementTotal();
                AddNewPlayer(soldierName, teamId);
                UpdateTeams();
                DebugWrite("^4New player^0: ^b" + soldierName + "^n, assigned to team " + teamId + " by game server", 3);
            } else {
                Reassign(soldierName, teamId, reassignTo, diff);
            }
        } else if (fPluginState == PluginState.Active && fGameState == GameState.Playing) {

            // If this was an MB move, finish it
            FinishMoveImmediate(soldierName, teamId);

            /*
             * We need to determine if this team change was instigated by a player or by an admin (plugin).
             * We want to ignore moves by admin. This is tricky due to the events possibly being 
             * in reverse order (team change first, then moved by admin). Use player.isAlive
             * to force a round trip with the game server, to insure that we get the admin move
             * event, if it exists.
             */
            if (fPendingTeamChange.ContainsKey(soldierName)) {
                // This is an admin move in correct order, do not treat it as a team switch
                fPendingTeamChange.Remove(soldierName);
                DebugWrite("Moved by admin: ^b" + soldierName + "^n to team " + teamId, 6);
                ProvisionalIncrementMoves(soldierName);
                return;
            }

            // Remember the pending move in a table
            fPendingTeamChange[soldierName] = teamId;

            // Admin move event may still be on its way, so do a round-trip to check
            ServerCommand("player.isAlive", soldierName);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnPlayerIsAlive(string soldierName, bool isAlive) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnPlayerIsAlive^n: " + soldierName + " " + isAlive, 7);

    try {
        if (fPluginState != PluginState.Active) return;
        /*
        This may be the return leg of the round-trip to insure that
        an admin move event, if any, has been processed. If the player's
        name is still in fPendingTeamChange, it's a real player instigated move
        */
        if (fPendingTeamChange.ContainsKey(soldierName)) {
            int team = fPendingTeamChange[soldierName];
            fPendingTeamChange.Remove(soldierName);
        
            DebugWrite("Player team switch: ^b" + soldierName + "^n to team " + team, 6);
            IncrementTotal();

            // Check if player is allowed to switch teams
            // Unswitch is handled in CheckTeamSwitch
            if (CheckTeamSwitch(soldierName, team)) {
                UpdatePlayerTeam(soldierName, team);
                UpdateTeams();
            }
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnPlayerMovedByAdmin^n: " + soldierName + " " + destinationTeamId + "/" + destinationSquadId + " " + forceKilled, 7);

    try {
        if (fPluginState == PluginState.Active && fGameState == GameState.Playing) {
            if (fPendingTeamChange.ContainsKey(soldierName)) {
                // this is an admin move in reversed order, clear from pending table and ignore the move
                // (unless MB initiated it)
                fPendingTeamChange.Remove(soldierName);
                DebugWrite("(REVERSED) Moved by admin: ^b" + soldierName + " to team " + destinationTeamId, 6);
                ProvisionalIncrementMoves(soldierName);
            } else if (!fUnassigned.Contains(soldierName)) {
                // this is an admin move in correct order, add to pending table and let OnPlayerTeamChange handle it
                fPendingTeamChange[soldierName] = destinationTeamId;
            }
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

/*
public override void OnSquadListPlayers(int teamId, int squadId, int playerCount, List<string> playersInSquad) {
    if (!fIsEnabled) return;
    
    DebugWrite("Got ^bOnSquadListPlayers^n: " + teamId + "/" + squadId + " has " + playerCount, 7);

    try {
        if (playersInSquad == null || playersInSquad.Count == 0) return;

        // Logging
        if (DebugLevel >= 6) {
            String ss = "Squad (";
            int t = Math.Max(0, Math.Min(teamId, TEAM_NAMES.Length-1));
            int s = Math.Max(0, Math.Min(squadId, SQUAD_NAMES.Length-1));

            ss = ss + TEAM_NAMES[t] + "/" + SQUAD_NAMES[s] + "): ";

            bool first = true;
            foreach (String grunt in playersInSquad) {
                if (first) {
                    ss = ss + grunt;
                    first = false;
                } else {
                    ss = ss + ", " + grunt;
                }
            }

            ConsoleWrite("^9" + ss);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}
*/


public override void OnPlayerKilled(Kill kKillerVictimDetails) {
    if (!fIsEnabled) return;

    String killer = kKillerVictimDetails.Killer.SoldierName;
    String victim = kKillerVictimDetails.Victim.SoldierName;
    String weapon = kKillerVictimDetails.DamageType;
    
    if (String.IsNullOrEmpty(killer)) killer = victim;
    
    DebugWrite("^9^bGot OnPlayerKilled^n: " + killer  + " -> " + victim + " (" + weapon + ")", 8);

    try {
    
        if (fGameState == GameState.Unknown || fGameState == GameState.Warmup) {
            bool wasUnknown = (fGameState == GameState.Unknown);
            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            if (wasUnknown || fGameState == GameState.Playing) DebugWrite("OnPlayerKilled: ^b^3Game state = " + fGameState, 6);  
        }
    
        KillUpdate(killer, victim);

        IncrementTotal();
    
        if (fPluginState == PluginState.Active && fGameState == GameState.Playing && IsModelInSync()) BalanceAndUnstack(victim);
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnListPlayers^n", 8);

    try {
        if (subset.Subset != CPlayerSubset.PlayerSubsetType.All) return;

        lock (fListPlayersQ) {
            fListPlayersTimestamp = DateTime.Now;
            Monitor.Pulse(fListPlayersQ);
        }

        /*
        Check if server crashed or Blaze dumped players.
        Detected by: last recorded server uptime is greater than zero and less than new uptime,
        or a player model timed out while still being on the all players list,
        or got a version command response, which is used in connection initialization for Procon,
        or the current list of players is more than CRASH_COUNT_HEURISTIC players less than the last
        recorded count,
        or the last known player count is greater than the maximum server size.
        Since these detections are not completely reliable, do a minimal  amount of recovery,
        don't do a full reset
        */
        if (fServerCrashed || fGotVersion || (players.Count + CRASH_COUNT_HEURISTIC) <  TotalPlayerCount || TotalPlayerCount > MaximumServerSize)  {
            fServerCrashed = false;
            fGotVersion = false;
            ValidateModel(players);
        } else {
            fUnassigned.Clear();
    
            foreach (CPlayerInfo p in players) {
                try {
                    UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank);
                } catch (Exception e) {
                    ConsoleException(e.ToString());
                    continue;
                }
            }
        }

        GarbageCollectKnownPlayers();
 
        UpdateTeams();

        LogStatus();
    
        /* Special handling for JustEnabled state */
        if (fPluginState == PluginState.JustEnabled) {
            fPluginState = PluginState.Active;
            fRoundStartTimestamp = DateTime.Now;
            DebugWrite("^b^3State = " + fPluginState, 6);  
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}


public override void OnServerInfo(CServerInfo serverInfo) {
    if (!fIsEnabled || serverInfo == null) return;

    DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 8);
    
    try {
        // Update game state if just enabled
        if (fGameState == GameState.Unknown) {
            if (serverInfo.TeamScores == null || serverInfo.TeamScores.Count < 2) {
                fGameState = GameState.RoundEnding;
                DebugWrite("OnServerInfo: ^b^3Game state = " + fGameState, 6);  
            }
        }

        if (fFinalStatus) {
            try {
                DebugWrite("^bFINAL STATUS FOR PREVIOUS ROUND:^n", 3);
                LogStatus();
            } catch (Exception) {}
            fFinalStatus = false;
        }

        if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map) {
            DebugWrite("ServerInfo update: " + serverInfo.Map + "/" + serverInfo.GameMode, 3);
        }
    
        if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime) {
            fServerCrashed = true;
        }
        fServerInfo = serverInfo;
        fServerUptime = serverInfo.ServerUptime;

        int i = 1;
        double maxTickets = 0;
        if (fServerInfo.TeamScores != null) foreach (TeamScore ts in fServerInfo.TeamScores) {
            fTickets[i] = ts.Score;
            if (ts.Score > maxTickets) maxTickets = ts.Score;
            i = i + 1;
            if (i >= fTickets.Length) break;
        }

        if (fMaxTickets == -1) {
            fMaxTickets = maxTickets;
            DebugWrite("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0"), 3);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnResponseError(List<String> requestWords, String error) { }



public override void OnGlobalChat(String speaker, String message) { }

public override void OnTeamChat(String speaker, String message, int teamId) { }

public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

public override void OnRoundOverPlayers(List<CPlayerInfo> players) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOverPlayers^n", 7);

    try {
        // TBD
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnRoundOverTeamScores(List<TeamScore> teamScores) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOverTeamScores^n", 7);

    try {
        // TBD
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnRoundOver(int winningTeamId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOver^n: winner " + winningTeamId, 7);

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Round over detected^0^n ::::::::::::::::::::::::::::::::::::", 2);
    
        if (fGameState == GameState.Playing || fGameState == GameState.Unknown) {
            fGameState = GameState.RoundEnding;
            DebugWrite("OnRoundOver: ^b^3Game state = " + fGameState, 6);
        }

        fFinalStatus = true;
        ServerCommand("serverInfo"); // get info for final status report
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnLevelLoaded^n: " + mapFileName + " " + Gamemode + " " + roundsPlayed + "/" + roundsTotal, 7);

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Level loaded detected^0^n ::::::::::::::::::::::::::::::::::::", 2);

        if (fGameState == GameState.RoundEnding || fGameState == GameState.Unknown) {
            fGameState = GameState.RoundStarting;
            DebugWrite("OnLevelLoaded: ^b^3Game state = " + fGameState, 6);  
        }

        fMaxTickets = -1; // flag to pay attention to next serverInfo
        ServerCommand("serverInfo");
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerSpawned: ^n" + soldierName, 8);
    
    try {
        if (fGameState == GameState.Unknown || fGameState == GameState.Warmup) {
            bool wasUnknown = (fGameState == GameState.Unknown);
            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            if (wasUnknown || fGameState == GameState.Playing) DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);  
        } else if (fGameState == GameState.RoundStarting) {
            // First spawn after Level Loaded is the official start of a round
            DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1First spawn detected^0^n ::::::::::::::::::::::::::::::::::::", 2);

            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);

            ResetRound();
        }
    
        if (fPluginState == PluginState.Active && fGameState != GameState.RoundEnding) {
            ValidateMove(soldierName);
            SpawnUpdate(soldierName);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}


public override void OnPlayerKilledByAdmin(string soldierName) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerKilledByAdmin^n: " + soldierName, 7);
    // TBD for m.IsDeployed
}

public override void OnReservedSlotsList(List<String> lstSoldierNames) {
    //if (!fIsEnabled) return; // do this always
    
    DebugWrite("^9^bGot OnReservedSlotsList^n", 7);
    fReservedSlots = lstSoldierNames;
}








/* ======================== CORE ENGINE ============================= */









private void BalanceAndUnstack(String name) {

    /* Useful variables */

    PlayerModel player = null;
    String simpleMode = String.Empty;
    PerModeSettings perMode = null;
    bool isStrong = false; // this player
    int winningTeam = 0;
    int losingTeam = 0;
    int biggestTeam = 0;
    int smallestTeam = 0;
    String strongMsg = String.Empty;
    int diff = 0;
    DateTime now = DateTime.Now;

    /* Sanity checks */

    if (fServerInfo == null) {
        fLastBalancedTimestamp = now;
        fLastUnbalancedTimestamp = now;
        return;
    }

    int totalPlayerCount = TotalPlayerCount;

    if (totalPlayerCount > 0) AnalyzeTeams(out diff, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    if (diff > MaxDiff()) {
        if (totalPlayerCount < ((IsSQDM()) ? 8 : 6)) {
            if (DebugLevel >= 7) DebugBalance("Not enough players in server, minimum is " + ((IsSQDM()) ? 8 : 6));
            fLastBalancedTimestamp = now;
            fLastUnbalancedTimestamp = now;
            return;
        }

        if (totalPlayerCount >= (MaximumServerSize-1)) {
            if (DebugLevel >= 7) DebugBalance("Server is full, no balancing or unstacking will be attempted!");
            fLastBalancedTimestamp = now;
            fLastUnbalancedTimestamp = now;
            return;
        }
    }

    /* Pre-conditions */

    lock (fKnownPlayers) {
        if (!fKnownPlayers.ContainsKey(name)) {
            DebugBalance("Unknown player: ^b" + name);
            return;
        }
        player = fKnownPlayers[name];
    }
    if (player == null) {
        DebugBalance("No model for player: ^b" + name);
        return;
    }
    if (!fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode)) {
        DebugBalance("Unknown game mode: " + fServerInfo.GameMode);
        return;
    }
    if (String.IsNullOrEmpty(simpleMode)) {
        DebugBalance("Simple mode is null for: " + fServerInfo.GameMode);
        return;
    }
    if (!fPerMode.TryGetValue(simpleMode, out perMode)) {
        DebugBalance("No per-mode settings for: " + simpleMode);
        return;
    }
    if (perMode == null) {
        DebugBalance("Per-mode settings null for: " + simpleMode);
        return;
    }

    /* Per-mode settings */
    Speed balanceSpeed = GetBalanceSpeed(perMode);
    double unstackTicketRatio = GetUnstackTicketRatio(perMode);

    // Adjust for duration of balance active
    if (diff > MaxDiff()
      && balanceSpeed == Speed.Adaptive
      && fLastUnbalancedTimestamp != DateTime.MinValue && fLastBalancedTimestamp != DateTime.MinValue 
      && fLastUnbalancedTimestamp.CompareTo(fLastBalancedTimestamp) > 0) {
        double secs = fLastUnbalancedTimestamp.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (secs > MaximumPassiveBalanceSeconds) {
            DebugBalance("^8^bBalancing taking too long (" + secs + " secs)!^n^0 Forcing to Fast balance speed.");
            balanceSpeed = Speed.Fast;
        }
    }

    /* Exclusions */

    // Exclude if on Whitelist or Reserved Slots if enabled
    if (diff > MaxDiff() && balanceSpeed != Speed.Fast && (OnWhitelist || balanceSpeed == Speed.Slow)) {
        List<String> vip = new List<String>(Whitelist);
        if (EnableWhitelistingOfReservedSlotsList) vip.AddRange(fReservedSlots);
        /*
        while (vip.Contains(String.Empty)) {
            vip.Remove(String.Empty);
        }
        */
        if (vip.Contains(name) || vip.Contains(ExtractTag(player)) || vip.Contains(player.EAGUID)) {
            DebugBalance("Excluding ^b" + player.FullName + "^n: whitelisted (or Slow)");
            fExcludedRound = fExcludedRound + 1;
            IncrementTotal();
            return;
        }
    }

    // Sort player's team by the strong method
    List<PlayerModel> fromList = GetTeam(player.Team);
    if (fromList == null) {
        DebugBalance("Unknown team " + player.Team + " for player ^b" + player.Name);
        return;
    }
    switch (perMode.DetermineStrongPlayersBy) {
        case DefineStrong.RoundScore:
            fromList.Sort(DescendingRoundScore);
            strongMsg = "Determing strong by: Round Score";
            break;
        case DefineStrong.RoundKills:
            fromList.Sort(DescendingRoundKills);
            strongMsg = "Determing strong by: Round Kills";
            break;
        case DefineStrong.RoundKDR:
            fromList.Sort(DescendingRoundKDR);
            strongMsg = "Determing strong by: Round KDR";
            break;
        case DefineStrong.PlayerRank:
            fromList.Sort(DescendingPlayerRank);
            strongMsg = "Determing strong by: Player Rank";
            break;
        default:
            fromList.Sort(DescendingRoundScore);
            strongMsg = "Determing strong by: Round Score";
            break;
    }
    int median = Math.Max(0, (fromList.Count / 2) - 1);
    int playerIndex = 0;

    // Exclude if TopScorers enabled and a top scorer on the team
    int topPlayersPerTeam = 0;
    if (balanceSpeed != Speed.Fast && TopScorers) {
        if (totalPlayerCount < 22) {
            topPlayersPerTeam = 1;
        } else if (totalPlayerCount > 42) {
            topPlayersPerTeam = Math.Min(3, fromList.Count);
        } else {
            topPlayersPerTeam = Math.Min(2, fromList.Count);
        } 
    }
    for (int i = 0; i < fromList.Count; ++i) {
        if (fromList[i].Name == player.Name) {
            if (diff > MaxDiff() && balanceSpeed != Speed.Fast && topPlayersPerTeam != 0 && i < topPlayersPerTeam) {
                DebugBalance("Excluding ^b" + player.FullName + "^n: Top Scorers enabled and this player is #" + (i+1) + " on team " + player.Team);
                fExcludedRound = fExcludedRound + 1;
                IncrementTotal();
                return;
            } else {
                playerIndex = i;
                break;
            }
        }
    }
    isStrong = (playerIndex < median);

    /* Balance */

    bool mustMove = false;

    int toTeam = ToTeam(name, player.Team, out diff, out mustMove); // take into account dispersal by Rank, etc.

    bool balanceActive = false;

    while (balanceSpeed != Speed.Stop && diff > MaxDiff() && toTeam != 0) {
        DebugBalance("Autobalancing because difference of " + diff + " is greater than " + MaxDiff());
        balanceActive = true;
        double abTime = fLastUnbalancedTimestamp.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (abTime > 0) {
            DebugBalance("^2^bAutobalance has been active for " + abTime.ToString("F0") + " seconds!");
        }
        fLastUnbalancedTimestamp = now;
        if (DebugLevel >= 8) DebugBalance("fLastUnbalancedTimestamp = " + fLastUnbalancedTimestamp.ToString("HH:mm:ss"));

        /* Exemptions */

        // Already on the smallest team
        if (!mustMove && player.Team == smallestTeam) {
            DebugBalance("Exempting ^b" + name + "^n, already on the smallest team");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            break;
        }

        // Has this player already been moved for balance or unstacking?
        if (!mustMove && player.MovesRound > 1) {
            DebugBalance("Exempting ^b" + name + "^n, already moved this round");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            break;
        }

        if (!mustMove && balanceSpeed != Speed.Fast) { // TBD
            if (DebugLevel > 5) DebugBalance(strongMsg);
            // don't move weak player to losing team
            if (!isStrong && toTeam == losingTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                break;
            }

            // don't move strong player to winning team
            if (isStrong && toTeam == winningTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                break;
           }
        }


        // TBD

        /* Move for balance */

        MoveInfo move = new MoveInfo(name, player.Tag, player.Team, GetTeamName(player.Team), toTeam, GetTeamName(toTeam));
        move.Reason = ReasonFor.Balance;
        move.Format(ChatMovedForBalance, false, false);
        move.Format(YellMovedForBalance, true, false);

        DebugWrite("^9" + move, 6);

        StartMoveImmediate(move, false);

        if (EnableLoggingOnlyMode || DUMMY == 2) {
            // Simulate completion of move
            OnPlayerTeamChange(name, toTeam, 0);
            OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
        }
        break;
    }

    if (!balanceActive) {
        double dur = fLastUnbalancedTimestamp.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (dur > 0) {
            DebugBalance("^2^bAutobalance was active for " + dur.ToString("F0") + " seconds!");
        }
        fLastBalancedTimestamp = now;
        if (DebugLevel >= 8) DebugBalance("fLastBalancedTimestamp = " + fLastBalancedTimestamp.ToString("HH:mm:ss"));
    }

    /* Unstack */

    // TBD

}

private void DebugBalance(String msg) {
    DebugWrite("^5(AUTO)^9 " + msg, 4);
}


private bool IsKnownPlayer(String name) {
    bool check = false;
    lock (fAllPlayers) {
        check = fAllPlayers.Contains(name);
    }
    return check;
}

private bool AddNewPlayer(String name, int team) {
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
    }
    return known;
}

private void RemovePlayer(String name) {
    bool gameChange = false;
    lock (fKnownPlayers) {
        if (fKnownPlayers.ContainsKey(name)) {
            // Keep around for MODEL_TIMEOUT minutes, in case player rejoins
            PlayerModel m = fKnownPlayers[name];
            m.ResetRound();
            m.LastSeenTimestamp = DateTime.Now;
        }
    }
    lock (fAllPlayers) {
        if (fAllPlayers.Contains(name)) fAllPlayers.Remove(name);
    
        if (fAllPlayers.Count < 4) {
            if (fGameState != GameState.Warmup) {
                fGameState = GameState.Warmup;
                gameChange = true;
            }
        }
    }
    if (gameChange) {
        DebugWrite("RemovePlayer: ^b^3Game state = " + fGameState, 6);
    }
}


private void UpdatePlayerModel(String name, int team, int squad, String eaGUID, int score, int kills, int deaths, int rank) {
    bool known = false;
    if (!IsKnownPlayer(name)) {
        switch (fPluginState) {
            case PluginState.JustEnabled:
            case PluginState.Reconnected:
                String state = (fPluginState == PluginState.JustEnabled) ? "JustEnabled" : "Reconnected";
                if (team != 0) {
                    known = AddNewPlayer(name, team);
                    String verb = (known) ? "^6renewing^0" : "^4adding^0";
                    DebugWrite(state + " state, " + verb + " new player: ^b" + name, 3);
                } else {
                    DebugWrite(state + " state, unassigned player: ^b" + name, 3);
                    if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                    return;
                }
                break;
            case PluginState.Active:
                DebugWrite("Update waiting for ^b" + name + "^n to be assigned a team", 3);
                if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                return;
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
    }

    if (!EnableLoggingOnlyMode && DUMMY != 2) {
        ConsoleDebug("player model for ^b" + name + "^n has team " + unTeam + " but update says " + team + "!");
    }
}


private void UpdatePlayerTeam(String name, int team) {
    bool isKnown = IsKnownPlayer(name);
    if (!isKnown) {
        lock (fKnownPlayers) {
            isKnown = fKnownPlayers.ContainsKey(name);
        }
        if (!isKnown) {
            ConsoleDebug("UpdatePlayerTeam(" + name + ", " + team + ") not known!");
            return;
        }
        lock (fAllPlayers) {
            fAllPlayers.Add(name);
        }
    }
    
    PlayerModel m = null;
    lock (fKnownPlayers) {
        if (!fKnownPlayers.TryGetValue(name, out m)) {
            ConsoleDebug("UpdatePlayerTeam(" + name + ", " + team + ") no model for player!");
            return;            
        }
    }
    
    if (m.Team != team) {
        if (m.Team == 0) {
            DebugWrite("Assigning ^b" + name + "^n to " + team, 3);
        } else {
            DebugWrite("Team switch: ^b" + name + "^n from " + m.Team + " to " + team, 7);
            m.Team = team;
        }
        m.LastSeenTimestamp = DateTime.Now;
    }
}

private void ValidateModel(List<CPlayerInfo> players) {
    // forget the active list, might be incorrect
    lock (fAllPlayers) {
        fAllPlayers.Clear();
    }
    fUnassigned.Clear();

    if (players.Count == 0) {
        // no players, so waiting state
        fGameState = GameState.Warmup;
    } else {
        // rebuild the data model
        fPluginState = PluginState.Reconnected;
        DebugWrite("ValidateModel: ^b^3State = " + fPluginState, 6);  
        foreach (CPlayerInfo p in players) {
            try {
                UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank);
            } catch (Exception e) {
                ConsoleException(e.ToString());
            }
        }
        /* Special handling for Reconnected state */
        fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Unknown;
        fRoundStartTimestamp = DateTime.Now;
        UpdateTeams();
    }
    fPluginState = PluginState.Active;
    DebugWrite("ValidateModel: ^b^3State = " + fPluginState, 6);  
    DebugWrite("ValidateModel: ^b^3Game state = " + fGameState, 6);
}

private void RevalidateModel() {
    // set flag
    // schedule listPlayers
}


private bool CheckTeamSwitch(String name, int team) {
    // Team
    return true; // means switch is okay
}

private void SpawnUpdate(String name) {
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
            }
            m.LastSeenTimestamp = now;
            m.IsDeployed = true;
        }
    }    

    if (!ok) {
        ConsoleDebug("player " + name + " spawned, but not a known player!");
    }

    if (updated) {
        DebugWrite("^9Spawn: ^b" + name + "^n @ " + now.ToString("HH:mm:ss"), 6);
    }
}


private void KillUpdate(String killer, String victim) {
    if (fPluginState != PluginState.Active) return;
    bool okVictim = false;
    bool okKiller = false;
    DateTime now = DateTime.Now;
    double score = -1;
    double kills = -1;
    double deaths = -1;
    double kdr = -1;
    double spm = -1;
    int team = -1;
    String tag = String.Empty;
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
            team = m.Team;
            tag = ExtractTag(m);
        }
    }

    if (!okKiller) {
        ConsoleDebug("player ^b" + killer + "^n is a killer, but not a known player!");
    }
    
    if (!okVictim) {
        ConsoleDebug("player ^b" + victim + "^n is a victim, but not a known player!");
    } else {
        Match rm = Regex.Match(tir.ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
        String sTIR = (rm.Success) ? rm.Groups[1].Value : "?";
        String vn = (!String.IsNullOrEmpty(tag)) ? "[" + tag + "]" + victim : victim;
        int toTeam = 0;
        int fromTeam = 0;
        int level = (GetTeamDifference(ref fromTeam, ref toTeam) > MaxDiff()) ? 4 : 8;

        DebugWrite("^9STATS: ^b" + vn + "^n [T:" + team + ", S:" + score + ", K:" + kills + ", D:" + deaths + ", KDR:" + kdr.ToString("F2") + ", SPM:" + spm.ToString("F0") + ", TIR: " + sTIR + "]", level);
    }
}

private void StartMoveImmediate(MoveInfo move, bool sendMessages) {
    // Do an immediate move, also used by the move thread
    if (!fIsEnabled || fPluginState != PluginState.Active) {
        ConsoleDebug("MoveImmediate called while fIsEnabled is " + fIsEnabled + " or fPluginState is "  + fPluginState);
        return;
    }

    // Send before messages?
    if (sendMessages) {
        Yell(move.Name, move.YellBefore);
        Chat(move.Name, move.ChatBefore);
    }

    lock (fMoving) {
        if (!fMoving.ContainsKey(move.Name)) fMoving[move.Name] = move;
    }
    // Do the move
    if (!EnableLoggingOnlyMode) {
        ServerCommand("admin.movePlayer", move.Destination.ToString(), "0", "false"); // TBD, assign to squad also
        ScheduleListPlayers(10);
    }
    String r = null;
    switch (move.Reason) {
        case ReasonFor.Balance: r = " for balance"; break;
        case ReasonFor.Unstack: r = " to unstack teams"; break;
        case ReasonFor.Unswitch: r = " to unswitch player"; break;
        default: r = " for ???"; break;
    }
    String doing = (EnableLoggingOnlyMode || DUMMY == 2) ? "^9(SIMULATING) ^b^1MOVING^0 " : "^b^1MOVING^0 ";
    DebugWrite(doing + move.Name + "^n from " + move.SourceName + " to " + move.DestinationName + r, DUMMY);
}

private void FinishMoveImmediate(String name, int team) {
    // If this is an MB move, handle it
    MoveInfo move = null;
    lock (fMoving) {
        if (fMoving.ContainsKey(name)) {
            move = fMoving[name];
            fMoving.Remove(name);
            try {
                UpdatePlayerTeam(name, team);
                UpdateTeams();
                if (move.Reason == ReasonFor.Balance || move.Reason == ReasonFor.Unstack) IncrementMoves(name);
                if (move.Reason == ReasonFor.Balance) fBalancedRound = fBalancedRound + 1;
                else if (move.Reason == ReasonFor.Unstack) fUnstackedRound = fUnstackedRound + 1;
                else if (move.Reason == ReasonFor.Unswitch) fUnswitchedRound = fUnswitchedRound + 1;
            } catch (Exception e) {
                ConsoleException(e.ToString());
            }
        }
    }
    if (move != null) {
        // MB move for balance/unstacking/unswitching
        Yell(move.Name, move.YellAfter);
        Chat(move.Name, move.ChatAfter);
        IncrementTotal();
    } else {
        // Some other admin.movePlayer, so update to account for it
        UpdatePlayerTeam(name, team);
        UpdateTeams();
    }
}

private void KillAndMoveAsync(MoveInfo move) {
    lock (fMoveQ) {
        fMoveQ.Enqueue(move);
        Monitor.Pulse(fMoveQ);
    }
}

public void MoveLoop() {
    try {
        while (fIsEnabled) {
            MoveInfo move = null;
            lock (fMoveQ) {
                while (fMoveQ.Count == 0) {
                    Monitor.Wait(fMoveQ);
                    if (!fIsEnabled) return;
                }
                move = fMoveQ.Dequeue();
            }

            // Sending before messages
            Yell(move.Name, move.YellBefore);
            Chat(move.Name, move.ChatBefore);

            // Pause
            Thread.Sleep(5*1000);
            if (!fIsEnabled) return;

            // Make sure player is dead
            if (!EnableLoggingOnlyMode) {
                ServerCommand("admin.killPlayer", move.Name);
                DebugWrite("^b^1ADMIN KILL^0 " + move.Name, DUMMY);
            } else {
                DebugWrite("^9(SIMULATING) ^b^1ADMIN KILL^0 " + move.Name, DUMMY);
            }

            // Pause
            Thread.Sleep(1*1000);
            if (!fIsEnabled) return;

            // Move player
            StartMoveImmediate(move, false);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    } finally {
        ConsoleWrite("^bMoveLoop^n thread stopped");
    }
}


private void Reassign(String name, int fromTeam, int toTeam, int diff) {
    // This is not a known player yet, so not PlayerModel to use
    // Just do a raw move as quickly as possible, not messages, just logging
    String doing = (EnableLoggingOnlyMode || DUMMY == 2) ? "^9(SIMULATING) ^b^1REASSIGNING^0^n new player ^b" : "^b^1REASSIGNING^0^n new player ^b";
    DebugWrite(doing + name + "^n from team " + fromTeam + " to team " + toTeam + " because difference is " + diff, DUMMY);
    int toSquad = ToSquad(name, toTeam);
    if (!EnableLoggingOnlyMode) {
        fReassigned.Add(name);
        ServerCommand("admin.movePlayer", name, toTeam.ToString(), toSquad.ToString(), "false");
        ScheduleListPlayers(10);
    } else {
        // Simulate reassignment
        fReassigned.Add(name);
        ScheduleListPlayers(1);
        OnPlayerTeamChange(name, toTeam, toSquad);
    }
}

private bool IsModelInSync() {
    lock (fMoving) {
        return (fMoving.Count == 0 && fReassigned.Count == 0);
    }
}

private void ValidateMove(String name) {
    /*
    This may be the return leg of the round-trip to insure that
    a move for balance MB has completed. If fMoving still
    contains the player's name, the move failed.
    */
    bool completedMove = true;
    lock (fMoving) {
        if (fMoving.ContainsKey(name)) {
            completedMove = false;
            fMoving.Remove(name);
        }
    }
    if (!completedMove) {
        ConsoleDebug("Move of ^b" + name + "^n failed!");
        IncrementTotal();
        fFailedRound = fFailedRound + 1;
        return;
    }
    /*
    This may be the return leg of the round-trip to insure that
    a reassignment of a player by MB has completed. If fReassigned still
    contains the player's name, the move failed.
    */
    bool completedReassign = true;
    lock (fReassigned) {
        if (fReassigned.Contains(name)) {
            completedReassign = false;
            fReassigned.Remove(name);
        }
    }
    if (!completedReassign) {
        ConsoleDebug("Reassign of ^b" + name + "^n failed!");
        fFailedRound = fFailedRound + 1;
        IncrementTotal();
        AddNewPlayer(name, 0);
        UpdateTeams();
        return;
    }
}

private void Chat(String who, String what) {
    String doing = null;
    if (QuietMode) {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.say", what, "player", who); // chat player only
        }
        ProconChatPlayer(who, what);
        doing = (EnableLoggingOnlyMode || DUMMY == 2) ? "^9(SIMULATING) ^b^1CHAT^0^n to ^b" : "^b^1CHAT^0^n to ^b";
        DebugWrite(doing + who + "^n: " + what, DUMMY);
    } else {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.say", what); // chat all
        }
        ProconChat(what);
        doing = (EnableLoggingOnlyMode || DUMMY == 2) ? "^9(SIMULATING) ^b^1CHAT^0^n to all: " : "^b^1CHAT^0^n to all: ";
        DebugWrite(doing + what, DUMMY);
    }
}

private void Yell(String who, String what) {
    String doing = null;
    if (!QuietMode) {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.yell", what, "player", who); // yell to player
        }
        doing = (EnableLoggingOnlyMode || DUMMY == 2) ? "^9(SIMULATING) ^b^1YELL^0^n to ^b" : "^b^1YELL^0^n to ^b";
        DebugWrite(doing + who + "^n: " + what, DUMMY);
    }
}

private void ProconChat(String what) {
    if (LogChat) ExecuteCommand("procon.protected.chat.write", "MB > All: " + what);
}

private void ProconChatPlayer(String who, String what) {
    if (LogChat) ExecuteCommand("procon.protected.chat.write", "MB > " + who + ": " + what);
}

private void GarbageCollectKnownPlayers()
{
    int n = 0;
    lock (fKnownPlayers) {
        List<String> garbage = new List<String>();

        // collect up garbage
        foreach (String name in fKnownPlayers.Keys) {
            PlayerModel m = fKnownPlayers[name];
            if (DateTime.Now.Subtract(m.LastSeenTimestamp).TotalMinutes > MODEL_TIMEOUT) {
                if (IsKnownPlayer(name)) {
                    ConsoleDebug("^b" + name + "^n has timed out and is still on active players list, idling?");
                    // Revalidate the data model
                    RevalidateModel();
                } else {
                    garbage.Add(name);
                }
            }
        }

        // remove garbage
        if (garbage.Count > 0) foreach (String name in garbage) {
            fKnownPlayers.Remove(name);
            n = n + 1;
        }
    }

    if (n > 0) {
        DebugWrite("^9Garbage collected " + n + " old players from known players table", 6);
    }
}

private Phase GetPhase(PerModeSettings perMode, bool verbose) {
    if (fServerInfo == null | fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return Phase.Mid;

    double earlyTickets = perMode.DefinitionOfEarlyPhase;
    double lateTickets = perMode.DefinitionOfLatePhase;
    Phase phase = Phase.Early;

    double tickets = -1;
    double goal = 0;
    bool countDown = true;

    if (Regex.Match(fServerInfo.GameMode, @"(?:TeamDeathMatch|SquadDeathMatch)").Success) {
        countDown = false;
        goal = fServerInfo.TeamScores[0].WinningScore;
    }

    foreach (TeamScore ts in fServerInfo.TeamScores) {
        if (tickets == -1) {
            tickets = ts.Score;
        } else {
            if (countDown) {
                if (ts.Score < tickets) {
                    tickets = ts.Score;
                }
            } else {
                if (ts.Score > tickets) {
                    tickets = ts.Score;
                }
            }
        }
    }

    if (countDown) {
        if (tickets >= earlyTickets) {
            phase = Phase.Early;
        } else if (tickets <= lateTickets) {
            phase = Phase.Late;
        } else {
            phase = Phase.Mid;
        }
    } else {
        // count up
        if (tickets <= earlyTickets) {
            phase = Phase.Early;
        } else if (tickets >= lateTickets) {
            phase = Phase.Late;
        } else {
            phase = Phase.Mid;
        }
    }

    if (verbose && DebugLevel >= 8) DebugBalance("Phase: " + phase + " (" + tickets + " of " + fMaxTickets + " to " + goal + ", " + RemainingTicketPercent(tickets, goal).ToString("F0") + "%)");

    return phase;
}

private Population GetPopulation(PerModeSettings perMode, bool verbose) {
    if (fServerInfo == null | fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return Population.Medium;

    double highPop = perMode.DefinitionOfHighPopulationForPlayers;
    double lowPop = perMode.DefinitionOfLowPopulationForPlayers;
    Population pop = Population.Low;

    int totalPop = TotalPlayerCount;

    if (totalPop <= lowPop) {
        pop = Population.Low;
    } else if (totalPop >= highPop) {
        pop = Population.High;
    } else {
        pop = Population.Medium;
    }

    if (verbose && DebugLevel >= 8) DebugBalance("Population: " + pop + " (" + totalPop + " [" + lowPop + " - " + highPop + "])");

    return pop;
}

private double GetUnstackTicketRatio(PerModeSettings perMode) {
    Phase phase = GetPhase(perMode, false);
    Population pop = GetPopulation(perMode, false);
    double unstackTicketRatio = 0;

    switch (phase) {
        case Phase.Early:
            switch (pop) {
                case Population.Low: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[0]; break;
                case Population.Medium: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[1]; break;
                case Population.High: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[2]; break;
                default: break;
            }
            break;
        case Phase.Mid:
            switch (pop) {
                case Population.Low: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[0]; break;
                case Population.Medium: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[1]; break;
                case Population.High: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[2]; break;
                default: break;
            }
            break;
        case Phase.Late:
            switch (pop) {
                case Population.Low: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[0]; break;
                case Population.Medium: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[1]; break;
                case Population.High: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[2]; break;
                default: break;
            }
            break;
        default: break;
    }
    return (unstackTicketRatio/100.0);
}

private Speed GetBalanceSpeed(PerModeSettings perMode) {
    Phase phase = GetPhase(perMode, true);
    Population pop = GetPopulation(perMode, true);
    Speed speed = Speed.Adaptive;

    switch (phase) {
        case Phase.Early:
            switch (pop) {
                case Population.Low: speed = EarlyPhaseBalanceSpeed[0]; break;
                case Population.Medium: speed = EarlyPhaseBalanceSpeed[1]; break;
                case Population.High: speed = EarlyPhaseBalanceSpeed[2]; break;
                default: break;
            }
            break;
        case Phase.Mid:
            switch (pop) {
                case Population.Low: speed = MidPhaseBalanceSpeed[0]; break;
                case Population.Medium: speed = MidPhaseBalanceSpeed[1]; break;
                case Population.High: speed = MidPhaseBalanceSpeed[2]; break;
                default: break;
            }
            break;
        case Phase.Late:
            switch (pop) {
                case Population.Low: speed = LatePhaseBalanceSpeed[0]; break;
                case Population.Medium: speed = LatePhaseBalanceSpeed[1]; break;
                case Population.High: speed = LatePhaseBalanceSpeed[2]; break;
                default: break;
            }
            break;
        default: break;
    }
    return speed;
}














/* ======================== SUPPORT FUNCTIONS ============================= */












private String FormatMessage(String msg, MessageType type) {
    String prefix = "[^b" + GetPluginName() + "^n] ";

    if (Thread.CurrentThread.Name != null) prefix += "Thread(^b" + Thread.CurrentThread.Name + "^n): ";

    if (type.Equals(MessageType.Warning))
        prefix += "^1^bWARNING^0^n: ";
    else if (type.Equals(MessageType.Error))
        prefix += "^1^bERROR^0^n: ";
    else if (type.Equals(MessageType.Exception))
        prefix += "^1^bEXCEPTION^0^n: ";
    else if (type.Equals(MessageType.Debug))
        prefix += "^9^bDEBUG^n: ";

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

public void ConsoleDebug(String msg)
{
    if (DebugLevel >= 3) ConsoleWrite(msg, MessageType.Debug);
}

public void ConsoleDump(String msg)
{
    ConsoleWrite("^b[Show In Log]^n ^5" + msg);
}


private void ServerCommand(params String[] args)
{
    List<String> list = new List<String>();
    list.Add("procon.protected.send");
    list.AddRange(args);
    this.ExecuteCommand(list.ToArray());
}



private List<String> GetSimplifiedModes() {
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


private void UpdatePresetValue() {
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

private void Reset() {
    ResetRound();

    lock (fMoveQ) {
        fMoveQ.Clear();
        Monitor.Pulse(fMoveQ);
    }

    lock (fListPlayersQ) {
        fListPlayersQ.Clear();
        Monitor.Pulse(fListPlayersQ);
    }

    lock (fAllPlayers) {
        fAllPlayers.Clear();
    }

    lock (fMoving) {
        fMoving.Clear();
    }

    fReassigned.Clear();
    fPendingTeamChange.Clear();
    fUnassigned.Clear();
    
    /*
    fKnownPlayers is not cleared right away, since we want to retain stats from previous plugin sessions.
    It will be garbage collected after MODEL_MINUTES.
    */

    fServerInfo = null; // release Procon reference
    fListPlayersTimestamp = DateTime.MinValue;
    fGotVersion = false;
    fServerUptime = 0;
    fServerCrashed  = false;
    fFinalStatus = false;
    fMaxTickets = -1;
}

private void ResetRound() {
    ClearTeams();

    for (int i = 0; i < fTickets.Length; i++) {
        fTickets[i] = 0;
    }
            
    fRoundStartTimestamp = DateTime.Now;

    lock (fAllPlayers) {
        foreach (String name in fAllPlayers) {
            if (!fKnownPlayers.ContainsKey(name)) {
                throw new Exception("ResetRound: " + name + " not in fKnownPlayers");
            }
            PlayerModel m = null;
            lock (fKnownPlayers) {
                m = fKnownPlayers[name];
            }

            m.ResetRound();
        }
    }

    fBalancedRound = 0;
    fUnstackedRound = 0;
    fUnswitchedRound = 0;
    fExcludedRound = 0;
    fExemptRound = 0;
    fFailedRound = 0;
    fTotalRound = 0;
    fReassignedRound = 0;

    fLastBalancedTimestamp = DateTime.MinValue;
    fLastUnbalancedTimestamp = DateTime.MinValue;
}

private bool IsSQDM() {
    if (fServerInfo == null) return false;
    return (fServerInfo.GameMode == "SquadDeathMatch0");
}

private int MaxDiff() {
    // TBD - based on per mode population settings
    return 1;
}

private void UpdateTeams() {
    ClearTeams();

    lock (fAllPlayers) {
        foreach (String name in fAllPlayers) {
            if (!fKnownPlayers.ContainsKey(name)) {
                throw new Exception("UpdateTeams: " + name + " not in fKnownPlayers");
            }
            lock (fKnownPlayers) {
                List<PlayerModel> t = GetTeam(fKnownPlayers[name].Team);
                if (t != null) t.Add(fKnownPlayers[name]);
            }
        }
    }
}

private List<PlayerModel> GetTeam(int team) {
    switch (team) {
        case 1: return(fTeam1);
        case 2: return(fTeam2);
        case 3: if (IsSQDM()) return(fTeam3); break;
        case 4: if (IsSQDM()) return(fTeam4); break;
        default: break;
    }
    return null;
}

private void ClearTeams() {
    fTeam1.Clear();
    fTeam2.Clear();
    fTeam3.Clear();
    fTeam4.Clear();
}

// Negative return value means toTeam is larger than fromTeam
private int GetTeamDifference(ref int fromTeam, ref int toTeam) {
    // 0 vs 0 means assign the max team to fromTeam and min team to toTeam and return the difference
    if (fromTeam < 0 || fromTeam > 4) return 0;
    if (toTeam < 0 || toTeam > 4) return 0;
    if (fromTeam != 0 && toTeam != 0 && fromTeam == toTeam) return 0;

    if (fromTeam != 0 && toTeam != 0) {
        List<PlayerModel> from = null;
        List<PlayerModel> to = null;

        from = GetTeam(fromTeam);
        if (from == null) return 0;

        to = GetTeam(toTeam);
        if (to == null) return 0;

        return (from.Count - to.Count);
    }

    // otherwise find min and max

    List<TeamRoster> teams = new List<TeamRoster>();
    int big = 1;

    teams.Add(new TeamRoster(1, fTeam1));
    teams.Add(new TeamRoster(2, fTeam2));
    if (IsSQDM()) {
        teams.Add(new TeamRoster(3, fTeam3));
        teams.Add(new TeamRoster(4, fTeam4));
        big = 3;
    }

    teams.Sort(delegate(TeamRoster lhs, TeamRoster rhs) {
        // Sort ascending order by count
        if (lhs == null || rhs == null) return 0;
        if (lhs.Roster.Count < rhs.Roster.Count) return -1;
        if (lhs.Roster.Count > rhs.Roster.Count) return 1;
        return 0;
    });

    TeamRoster minTeam = teams[0];
    TeamRoster maxTeam = teams[big];

    // assert(fromTeam == 0 && toTeam == 0)
    toTeam = minTeam.Team;
    fromTeam = maxTeam.Team;
    return (maxTeam.Roster.Count - minTeam.Roster.Count);
}


private void AnalyzeTeams(out int maxDiff, out int biggestTeam, out int smallestTeam, out int winningTeam, out int losingTeam) {
    biggestTeam = 0;
    smallestTeam = 0;
    winningTeam = 0;
    losingTeam = 0;
    maxDiff = 0;

    if (fServerInfo == null) return;

    List<TeamRoster> teams = new List<TeamRoster>();

    teams.Add(new TeamRoster(1, fTeam1));
    teams.Add(new TeamRoster(2, fTeam2));
    if (IsSQDM()) {
        teams.Add(new TeamRoster(3, fTeam3));
        teams.Add(new TeamRoster(4, fTeam4));
    }

    teams.Sort(delegate(TeamRoster lhs, TeamRoster rhs) {
        // Sort ascending order by count
        if (lhs == null || rhs == null) return 0;
        if (lhs.Roster.Count < rhs.Roster.Count) return -1;
        if (lhs.Roster.Count > rhs.Roster.Count) return 1;
        return 0;
    });

    TeamRoster small = teams[0];
    TeamRoster big = teams[teams.Count-1];
    smallestTeam = small.Team;
    biggestTeam = big.Team;
    maxDiff = big.Roster.Count - small.Roster.Count;

    List<TeamScore> byScore = new List<TeamScore>();
    if (fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return;
    byScore.AddRange(fServerInfo.TeamScores);

    byScore.Sort(delegate(TeamScore lhs, TeamScore rhs) {
        // Sort descending order by score
        if (lhs == null || rhs == null) return 0;
        if (lhs.Score < rhs.Score) return 1;
        if (lhs.Score > rhs.Score) return -1;
        return 0;
    });

    winningTeam = byScore[0].TeamID;
    losingTeam = byScore[byScore.Count-1].TeamID;
}

private int DifferenceFromSmallest(int fromTeam) {
    int biggestTeam = 0;
    int smallestTeam = 0;
    int winningTeam = 0;
    int losingTeam = 0;
    int diff = 0;

    List<TeamRoster> teams = new List<TeamRoster>();

    teams.Add(new TeamRoster(1, fTeam1));
    teams.Add(new TeamRoster(2, fTeam2));
    if (IsSQDM()) {
        teams.Add(new TeamRoster(3, fTeam3));
        teams.Add(new TeamRoster(4, fTeam4));
    }

    if (!IsSQDM()) {
        if (fromTeam < 1 || fromTeam > 2) return 0;
    } else {
        if (fromTeam < 1 || fromTeam > 4) return 0;
    }

    AnalyzeTeams(out diff, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    if (fromTeam == smallestTeam || smallestTeam < 1 || smallestTeam > teams.Count) return 0;

    return(teams[fromTeam-1].Roster.Count - teams[smallestTeam-1].Roster.Count);
}


private int ToTeam(String name, int fromTeam, out int diff, out bool mustMove) {
    diff = 0;
    mustMove = false;
    if (fromTeam < 1 || fromTeam > 4) return 0;

    List<PlayerModel> from = null;
    
    if (fromTeam == 1) {
        from = fTeam1;
    }

    if (fromTeam == 2) {
        from = fTeam2;
    } 

    if (IsSQDM()) {
        if (fromTeam == 3) {
            from = fTeam3;
        }

        if (fromTeam == 4) {
            from = fTeam4;
        }
    }

    if (from != null && from.Count == 0) return 0;

    int biggestTeam = 0;
    int smallestTeam = 0;
    int winningTeam = 0;
    int losingTeam = 0;

    AnalyzeTeams(out diff, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    // diff is maximum difference between any two teams

    if (diff <= MaxDiff()) return 0;

    int targetTeam = smallestTeam;
    
    // Special handling for SQDM, when small teams are equal, pick the lowest numbered ID
    if (IsSQDM()) {
        if (targetTeam == 4 && fTeam3.Count == fTeam4.Count) targetTeam = 3;
        if (targetTeam == 3 && fTeam2.Count == fTeam3.Count) targetTeam = 2;
    }

    // recompute diff to be difference between fromTeam and target team
    diff = GetTeamDifference(ref fromTeam, ref targetTeam);

    // TBD, for SQDM, based on name, might need to take into account dispersal by Rank, etc.
    // mustMove set to True if dispersal policy (etc) must override other policies
    return targetTeam;
}

private int ToSquad(String name, int team) {
    String tag = String.Empty;
    if (IsKnownPlayer(name)) {
        lock (fKnownPlayers) {
            tag = fKnownPlayers[name].Tag;
        }
    }

    List<PlayerModel> teamList = null;

    teamList = GetTeam(team);
    if (teamList == null) return 0;

    int[] squads = new int[SQUAD_NAMES.Length];

    // Build table of squad counts
    int i = 0;
    for (i = 0; i < squads.Length; ++i) {
        squads[i] = 0;
    }
    foreach (PlayerModel p in teamList) {
        i = p.Squad;
        if (i < 0 || i >= squads.Length) continue;
        squads[i] = squads[i] + 1;
    }

    // Find the smallest squad above zero (that isn't locked -- TBD)
    int squad = 0;
    int least = 4;
    int atZero = 0;
    for (int squadNum = 1; squadNum < squads.Length; ++squadNum) {
        int n = squads[squadNum];
        if (n == 0) {
            if (atZero == 0) atZero = squadNum;
            continue;
        }
        if (n < least) {
            squad = squadNum;
            least = n;
        }
    }
    // if there is an empty squad at a lower slot than the smallest squad, return that
    if (atZero < squad) return atZero;
    // otherwise return the smallest squad
    return squad;
}

private void StartThreads() {
    fMoveThread = new Thread(new ThreadStart(MoveLoop));
    fMoveThread.IsBackground = true;
    fMoveThread.Name = "MoveLoop";
    fMoveThread.Start();

    fListPlayersThread = new Thread(new ThreadStart(ListPlayersLoop));
    fListPlayersThread.IsBackground = true;
    fListPlayersThread.Name = "ListPlayersLoops";
    fListPlayersThread.Start();
}

private void JoinWith(Thread thread, int secs)
{
    if (thread == null || !thread.IsAlive)
        return;

    ConsoleWrite("Waiting for ^b" + thread.Name + "^n to finish");
    thread.Join(secs*1000);
}

private void StopThreads() {
    try
    {
        Thread stopper = new Thread(new ThreadStart(delegate()
            {
                fFinalizerActive = true;

                Thread.Sleep(100);

                try
                {
                    lock (fMoveQ) {
                        Monitor.Pulse(fMoveQ);
                    }
                    JoinWith(fMoveThread, 3);
                    fMoveThread = null;
                    JoinWith(fListPlayersThread, 3);
                    fListPlayersThread = null;

                    //this.blog.CleanUp();
                    /*
                    lock (this.cacheResponseTable) {
                        this.cacheResponseTable.Clear();
                    }
                    */
                }
                catch (Exception e)
                {
                    ConsoleException(e.ToString());
                }

                fFinalizerActive = false;
                ConsoleWrite("Finished disabling threads, ready to be enabled again!");
            }));

        stopper.Name = "stopper";
        stopper.Start();

    }
    catch (Exception e)
    {
        ConsoleException(e.ToString());
    }
}

private void IncrementMoves(String name) {
    if (!IsKnownPlayer(name)) return;
    lock (fKnownPlayers) {
        PlayerModel m = fKnownPlayers[name];
        m.MovesRound = m.MovesRound + 1;
        m.MovedByMB = true;
    }
}

private void ProvisionalIncrementMoves(String name) {
    /*
    If some other plugin did an admin move on this player, increment
    the move counter so that this player will be exempted from balancing and unstacking
    for the rest of this round, but don't set the flag, since MB didn't move this player.
    */
    if (!IsKnownPlayer(name)) return;
    lock (fKnownPlayers) {
        PlayerModel m = fKnownPlayers[name];
        if (!m.MovedByMB) {
            m.MovesRound = m.MovesRound + 1;
        }
    }
}

/*
private int GetMoves(String name) {
    if (!IsKnownPlayer(name)) return 99;
    lock (fKnownPlayers) {
        return fKnownPlayers[name].MovesRound;
    }
}
*/

private void IncrementTotal()
{
    if (fPluginState == PluginState.Active) fTotalRound = fTotalRound + 1;
}

private String GetTeamName(int team) {
    if (team < 0 || team >= SQUAD_NAMES.Length) team = 0;
    if (team == 0) return "None";

    if (IsSQDM()) {
        return SQUAD_NAMES[team];
    }
    if (team > 2) return "None";
    return TEAM_NAMES[team];
}

private void ListPlayersLoop() {
    /*
    Strategy: Control the rate of listPlayers commands by keeping track of the
    timestamp of the last event. Only issue a new command if no new event occurs within
    the required time.
    */
    try {
        while (fIsEnabled) {
            ListPlayersRequest request = null;
            lock (fListPlayersQ) {
                while (fListPlayersQ.Count == 0) {
                    Monitor.Wait(fListPlayersQ);
                    if (!fIsEnabled) return;
                }

                request = fListPlayersQ.Dequeue();

                while (request.LastUpdate == fListPlayersTimestamp 
                  && DateTime.Now.Subtract(request.LastUpdate).TotalSeconds < request.MaxDelay) {
                    Monitor.Wait(fListPlayersQ, 1000);
                    if (!fIsEnabled) return;
                }
            }

            // If there has been no event, ask for one
            if (request.LastUpdate == fListPlayersTimestamp) ServerCommand("admin.listPlayers", "all");
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    } finally {
        ConsoleWrite("^bListPlayersLoop^n thread stopped");
    }
}

private void ScheduleListPlayers(double delay) {
    ListPlayersRequest r = new ListPlayersRequest(delay, fListPlayersTimestamp);
    DebugWrite("^9Scheduling listPlayers no sooner than " + r.MaxDelay  + " seconds from " + r.LastUpdate.ToString("HH:mm:ss"), 5);
    lock (fListPlayersQ) {
        fListPlayersQ.Enqueue(r);
        Monitor.Pulse(fListPlayersQ);
    }
}

private String ExtractTag(PlayerModel m) {
    if (m == null) return String.Empty;

    String tag = m.Tag;
    if (String.IsNullOrEmpty(tag)) {
        // Maybe they are using [_-=]XXX[=-_]PlayerName[_-=]XXX[=-_] format
        Match tm = Regex.Match(m.Name, @"^[=_\-]*([^=_\-]{2,4})[=_\-]");
        if (tm.Success) {
            tag = tm.Groups[1].Value;
        } else {
            tm = Regex.Match(m.Name, @"[^=_\-][=_\-]([^=_\-]{2,4})[=_\-]*$");
            if (tm.Success) { 
                tag = tm.Groups[1].Value;
            } else {
                tag = String.Empty;
            }
        }
    }
    return tag;
}

// Sort delegate
public static int DescendingRoundScore(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }

    if (lhs.ScoreRound < rhs.ScoreRound) return 1;
    if (lhs.ScoreRound > rhs.ScoreRound) return -1;
    return 0;
}


// Sort delegate
public static int DescendingRoundKills(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }

    if (lhs.KillsRound < rhs.KillsRound) return 1;
    if (lhs.KillsRound > rhs.KillsRound) return -1;
    return 0;
}


// Sort delegate
public static int DescendingRoundKDR(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }

    if (lhs.KDRRound < rhs.KDRRound) return 1;
    if (lhs.KDRRound > rhs.KDRRound) return -1;
    return 0;
}


// Sort delegate
public static int DescendingPlayerRank(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }

    if (lhs.Rank < rhs.Rank) return 1;
    if (lhs.Rank > rhs.Rank) return -1;
    return 0;
}


private void GatherProconGoodies() {
    fFriendlyMaps.Clear();
    fFriendlyModes.Clear();
    List<CMap> bf3_defs = this.GetMapDefines();
    foreach (CMap m in bf3_defs) {
        if (!fFriendlyMaps.ContainsKey(m.FileName)) fFriendlyMaps[m.FileName] = m.PublicLevelName;
        if (!fFriendlyModes.ContainsKey(m.PlayList)) fFriendlyModes[m.PlayList] = m.GameMode;
    }
    if (DebugLevel >= 8) {
        foreach (KeyValuePair<String,String> pair in fFriendlyMaps) {
            DebugWrite("friendlyMaps[" + pair.Key + "] = " + pair.Value, 8);
        }
        foreach (KeyValuePair<String,String> pair in fFriendlyModes) {
            DebugWrite("friendlyModes[" + pair.Key + "] = " + pair.Value, 8);
        }
    }
    DebugWrite("Friendly names loaded", 6);
}


private PlayerModel GetPlayer(String name) {
    if (String.IsNullOrEmpty(name)) return null;
    PlayerModel p = null;
    lock (fKnownPlayers) {
        if (!fKnownPlayers.TryGetValue(name, out p)) {
            return null;
        }
    }
    return p;
}

private double RemainingTicketPercent(double tickets, double goal) {
    if (goal == 0) {
        return ((tickets / fMaxTickets) * 100.0);
    }
    return (((goal - tickets) / goal) * 100.0);
}




private void LogStatus() {
    
    String tm = fTickets[1] + "/" + fTickets[2];
    if (IsSQDM()) tm = tm + "/" + fTickets[3] + "/" + fTickets[4];

    DateTime rst = (fRoundStartTimestamp == DateTime.MinValue) ? DateTime.Now : fRoundStartTimestamp;
    Match rm = Regex.Match(DateTime.Now.Subtract(fRoundStartTimestamp).ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
    String rt = (rm.Success) ? rm.Groups[1].Value : "?";

    DebugWrite("^bStatus^n: Plugin state = " + fPluginState + ", game state = " + fGameState + ", Enable Logging Only Mode = " + EnableLoggingOnlyMode, 4);
    DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", time in round = " + rt + ", tickets = " + tm, 4);
    if (fPluginState == PluginState.Active) {
        double secs = fLastUnbalancedTimestamp.Subtract(fLastBalancedTimestamp).TotalSeconds;
        PerModeSettings perMode = null;
        String simpleMode = String.Empty;
        if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) 
          && fPerMode.TryGetValue(simpleMode, out perMode) && perMode != null) {
            Speed balanceSpeed = GetBalanceSpeed(perMode);
            double unstackRatio = GetUnstackTicketRatio(perMode);
            String activeTime = (secs > 0) ? "^1active (" + secs.ToString("F0") + " secs)^0" : "not active";
            DebugWrite("^bStatus^n: Autobalance is " + activeTime + ", phase = " + GetPhase(perMode, false) + ", population = " + GetPopulation(perMode, false) + ", speed = " + balanceSpeed + ", unstack when ticket ratio >= " + (unstackRatio * 100).ToString("F0") + "%", 3);
        }
    }
    if (!IsModelInSync()) DebugWrite("^bStatus^n: fMoving = " + fMoving.Count + ", fReassigned = " + fReassigned.Count, 3);

    DebugWrite("^bStatus^n: " + fReassignedRound + " reassigned, " + fBalancedRound + " balanced, " + fUnstackedRound + " unstacked, " + fUnswitchedRound + " unswitched, " + fExcludedRound + " excluded, " + fExemptRound + " exempted, " + fFailedRound + " failed; of " + fTotalRound + " TOTAL", 5);
    
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
    String next = (diff > MaxDiff() && fGameState == GameState.Playing) ? "^n^0 ... autobalance will activate on next death!" : "^n";
    
    DebugWrite("^bStatus^n: Team difference = " + ((diff > MaxDiff()) ? "^8^b" : "^b") + diff + next, 3);
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

    public static PROTObalancer.Speed[] ParseSpeedArray(PROTObalancer plugin, String s) {
        PROTObalancer.Speed[] speeds = new PROTObalancer.Speed[3] {
            PROTObalancer.Speed.Adaptive,
            PROTObalancer.Speed.Adaptive,
            PROTObalancer.Speed.Adaptive
        };
        if (String.IsNullOrEmpty(s)) return speeds;
        if (!s.Contains(",")) return speeds;
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) return speeds;
        for (int i = 0; i < speeds.Length; ++i) {
            try {
                speeds[i] = (PROTObalancer.Speed)Enum.Parse(typeof(PROTObalancer.Speed), strs[i]);
            } catch (Exception) {
                plugin.ConsoleWarn("Bad balance speed value: " + strs[i]);
                speeds[i] = PROTObalancer.Speed.Adaptive;
            }
        }
        return speeds;
    }

    public const String HTML_DOC = @"
<h1>Multi-Balancer &amp; Unstacker, including SQDM</h1>
<p>For BF3, this plugin does live round team balancing and unstacking for all game modes, including Squad Deathmatch (SQDM).</p>

<h2>THIS IS JUST A PROTOTYPE FOR FEEDBACK!</h2>
<p>Only new player reassignment is enabled. All other balancing and unstacking features do logging only. If you plan to test this on a live server, <FONT color='#FF0000'><b>DISABLE</b> TrueBalancer Balance Guard or Insane Limits limits that evaluate OnTeamChange or ProconRulz that evaluate on team changes, such as player team unswitching or unstacking.</FONT>. Alternatively, set PROTObalancer's <b>Enable Logging Only Mode</b> to true, which will make new player reassignment do logging only. If you do not do this, the two plugins will conflict with each other and a player might be moved back and forth between teams repeatedly.</p>

<p><font color=#0000FF>The purpose of this prototype is to get feedback about usage and behavior.</font></p>

<p>Since logging to plugin.log is an important part of this phase of testing, it would be best if you could reduce or eliminate logging of other plugins so that most or all of the log is from PROTObalancer. If you are planning to leave the plugin unattended to collect logs, set <b>Debug Level</b> to 5. If you are going to watch the log interactively for a while, you can experiment with higher or lower <b>Debug Level</b> settings. It would also be useful to set the console.log to enable Event logging, but be advised that the console.log might get quite large. Do <b>not</b> enable Debug on your console.log.</p>

<p>The only settings that do anything are:
<ul>
<li>Debug Level</li>
<li>Enable Whitelisting Of Reserved Slot List</li>
<li>Whitelist</li>
<li>On Whitelist</li>
<li>Quiet Mode</li>
<li>Chat: Moved For Balance (only for logging purposes, no chat is actually sent)</li>
<li>Yell: Moved For Balance (only for logging purposes, no yell is actually sent)</li>
<li>Log Chat</li>
<li>Enable Logging Only Mode</li>
</ul>
</p>

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
