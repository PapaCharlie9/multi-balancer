/* PROTObalancer.cs

by PapaCharlie9

Permission is hereby granted, free of charge, to any person or organization
obtaining a copy of the software and accompanying documentation covered by
this license (the "Software") to use, reproduce, display, distribute,
execute, and transmit the Software, and to prepare derivative works of the
Software, and to permit third-parties to whom the Software is furnished to
do so, without restriction.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.

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

    public enum UnstackState {Off, SwappedStrong, SwappedWeak};

    /* Constants & Statics */

    public const double SWAP_TIMEOUT = 60; // in seconds

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
                    MaxUnstackingSwapsPerRound = 6;
                    DefinitionOfHighPopulationForPlayers = 28;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 50; // assuming 200 tickets typical
                    DefinitionOfLatePhaseFromEnd = 50; // assuming 200 tickets typical
                    break;
                case "Conquest Large":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    MaxUnstackingSwapsPerRound = 12;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 100; // assuming 300 tickets typical
                    DefinitionOfLatePhaseFromEnd = 100; // assuming 300 tickets typical
                    break;
                case "CTF":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 12;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 5; // minutes
                    DefinitionOfLatePhaseFromEnd = 5; // minutes
                    break;
                case "Rush":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 6;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 25; // assuming 75 tickets typical
                    DefinitionOfLatePhaseFromEnd = 25; // assuming 75 tickets typical
                    break;
                case "Squad Deathmatch":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 0;
                    MaxUnstackingSwapsPerRound = 0;
                    DefinitionOfHighPopulationForPlayers = 14;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 10; // assuming 50 tickets typical
                    DefinitionOfLatePhaseFromEnd = 10; // assuming 50 tickets typical
                    break;
                case "Superiority":
                    MaxPlayers = 24;
                    CheckTeamStackingAfterFirstMinutes = 15;
                    MaxUnstackingSwapsPerRound = 6;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 50; // assuming 250 tickets typical
                    DefinitionOfLatePhaseFromEnd = 50; // assuming 250 tickets typical
                    break;
                case "Team Deathmatch":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 12;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 20; // assuming 100 tickets typical
                    DefinitionOfLatePhaseFromEnd = 20; // assuming 100 tickets typical
                    break;
                case "Squad Rush":
                    MaxPlayers = 8;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    MaxUnstackingSwapsPerRound = 3;
                    DefinitionOfHighPopulationForPlayers = 6;
                    DefinitionOfLowPopulationForPlayers = 4;
                    DefinitionOfEarlyPhaseFromStart = 5; // assuming 20 tickets typical
                    DefinitionOfLatePhaseFromEnd = 5; // assuming 20 tickets typical
                    break;
                case "Gun Master":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    MaxUnstackingSwapsPerRound = 6;
                    DefinitionOfHighPopulationForPlayers = 12;
                    DefinitionOfLowPopulationForPlayers = 6;
                    DefinitionOfEarlyPhaseFromStart = 0;
                    DefinitionOfLatePhaseFromEnd = 0;
                    break;
                case "Unknown or New Mode":
                default:
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    MaxUnstackingSwapsPerRound = 6;
                    DefinitionOfHighPopulationForPlayers = 28;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 50;
                    DefinitionOfLatePhaseFromEnd = 50;
                    break;
            }
        }
        
        public int MaxPlayers = 64; // will be corrected later
        public double CheckTeamStackingAfterFirstMinutes = 10;
        public int MaxUnstackingSwapsPerRound = 6;
        public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
        public double DefinitionOfHighPopulationForPlayers = 48;
        public double DefinitionOfLowPopulationForPlayers = 16;
        public double DefinitionOfEarlyPhaseFromStart = 50;
        public double DefinitionOfLatePhaseFromEnd = 50;
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
            if (expanded.Contains("%fromTeam%")) expanded = expanded.Replace("%fromTeam%", SourceName);
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
            s += "CB'" + ChatBefore + "',";
            s += "YB'" + YellBefore + "',";
            s += "CA'" + ChatAfter + "',";
            s += "YA'" + YellAfter + "')";
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
private DateTime fEnabledTimestamp = DateTime.MinValue;
private String fLastMsg = null;

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
private double fRushMaxTickets = -1; // not normalized
private List<TeamScore> fFinalStatus = null;
private bool fIsFullRound = false;
private UnstackState fUnstackState = UnstackState.Off;
private DateTime fFullUnstackSwapTimestamp;
private int fRushStage = 0;
private double fRushAttackerTickets = 0;

// Operational statistics
private int fReassignedRound = 0;
private int fBalancedRound = 0;
private int fUnstackedRound = 0;
private int fUnswitchedRound = 0;
private int fExcludedRound = 0;
private int fExemptRound = 0;
private int fFailedRound = 0;
private int fTotalRound = 0;
private bool fBalanceIsActive = false;

// Settings support
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

// Settings
public int DebugLevel;
public int MaximumServerSize;
public bool EnableBattlelogRequests; // TBD
public int MaximumRequestRate; // TBD
public int MaxTeamSwitchesByStrongPlayers; // disabled
public int MaxTeamSwitchesByWeakPlayers; // disabled
public double UnlimitedTeamSwitchingDuringFirstMinutesOfRound; // TBD
public bool Enable2SlotReserve; // disabled
public bool EnablerecruitCommand; // disabled
public bool EnableWhitelistingOfReservedSlotsList;
public String[] Whitelist;
public String[] DisperseEvenlyList; // TBD
public PresetItems Preset;
public double SecondsUntilAdaptiveSpeedBecomesFast;

public bool OnWhitelist;
public bool TopScorers;
public bool SameClanTagsInSquad; // TBD
public double MinutesAfterJoining;
public bool JoinedEarlyPhase; // disabled
public bool JoinedMidPhase; // disabled
public bool JoinedLatePhase; // disabled

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
public String ChatDetectedBadTeamSwitch;
public String YellDetectedBadTeamSwitch;
public String ChatDetectedGoodTeamSwitch;
public String YellDetectedGoodTeamSwitch;
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
    fBalanceIsActive = false;

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
    fFullUnstackSwapTimestamp = DateTime.MinValue;
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
    fRushMaxTickets = -1;
    fLastBalancedTimestamp = DateTime.MinValue;
    fEnabledTimestamp = DateTime.MinValue;
    fFinalStatus = null;
    fIsFullRound = false;
    fUnstackState = UnstackState.Off;
    fLastMsg = null;
    fRushStage = 0;
    fRushAttackerTickets = 0;
    
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
    SecondsUntilAdaptiveSpeedBecomesFast = 3*60; // 3 minutes default
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = true;
    TopScorers = true;
    SameClanTagsInSquad = true;
    MinutesAfterJoining = 5;
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
    ChatDetectedBadTeamSwitch = "%name%, the %fromTeam% team needs your help, sending you back ...";
    YellDetectedBadTeamSwitch = "The %fromTeam% team needs your help, sending you back!";
    ChatDetectedGoodTeamSwitch = "%name%, thanks for helping out the %toTeam% team!";
    YellDetectedGoodTeamSwitch = "Thanks for helping out the %toTeam% team!";
    ChatAfterUnswitching = "%name%, please stay on the %toTeam% team for the rest of this round";
    YellAfterUnswitching = "Please stay on the %toTeam% team for the rest of this round";
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
            MinutesAfterJoining = 15;
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
            MinutesAfterJoining = 0;
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
            MinutesAfterJoining = 15;
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
            MinutesAfterJoining = 5;
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
            MinutesAfterJoining = 5;
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
    return "0.0.0.13";
}

public String GetPluginAuthor() {
    return "PapaCharlie9";
}

public String GetPluginWebsite() {
    return "TBD";
}

public String GetPluginDescription() {
    //ConsoleWrite("length = " + PROTObalancerUtils.HTML_DOC.Length);
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

        lstReturn.Add(new CPluginVariable("1 - Settings|Seconds Until Adaptive Speed Becomes Fast", SecondsUntilAdaptiveSpeedBecomesFast.GetType(), SecondsUntilAdaptiveSpeedBecomesFast));
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Whitelisting Of Reserved Slots List", EnableWhitelistingOfReservedSlotsList.GetType(), EnableWhitelistingOfReservedSlotsList));

        lstReturn.Add(new CPluginVariable("1 - Settings|Whitelist", Whitelist.GetType(), Whitelist));

        lstReturn.Add(new CPluginVariable("1 - Settings|Disperse Evenly List", DisperseEvenlyList.GetType(), DisperseEvenlyList));
        
        /* ===== SECTION 2 - Exclusions ===== */

        lstReturn.Add(new CPluginVariable("2 - Exclusions|On Whitelist", OnWhitelist.GetType(), OnWhitelist));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Top Scorers", TopScorers.GetType(), TopScorers));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Squad", SameClanTagsInSquad.GetType(), SameClanTagsInSquad));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Joining", MinutesAfterJoining.GetType(), MinutesAfterJoining));

        /*
        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Early Phase", JoinedEarlyPhase.GetType(), JoinedEarlyPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Mid Phase", JoinedMidPhase.GetType(), JoinedMidPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Late Phase", JoinedLatePhase.GetType(), JoinedLatePhase));
        */

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
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Bad Team Switch", ChatDetectedBadTeamSwitch.GetType(), ChatDetectedBadTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Bad Team Switch", YellDetectedBadTeamSwitch.GetType(), YellDetectedBadTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Good Team Switch", ChatDetectedGoodTeamSwitch.GetType(), ChatDetectedGoodTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Good Team Switch", YellDetectedGoodTeamSwitch.GetType(), YellDetectedGoodTeamSwitch));
        
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

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

            if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Check Team Stacking After First Minutes", oneSet.CheckTeamStackingAfterFirstMinutes.GetType(), oneSet.CheckTeamStackingAfterFirstMinutes));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Unstacking Swaps Per Round", oneSet.MaxUnstackingSwapsPerRound.GetType(), oneSet.MaxUnstackingSwapsPerRound));

                var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));
            }

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Disperse Evenly For Rank >=", oneSet.DisperseEvenlyForRank.GetType(), oneSet.DisperseEvenlyForRank));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population For Players >=", oneSet.DefinitionOfHighPopulationForPlayers.GetType(), oneSet.DefinitionOfHighPopulationForPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population For Players <=", oneSet.DefinitionOfLowPopulationForPlayers.GetType(), oneSet.DefinitionOfLowPopulationForPlayers));

            if (isCTF) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Minutes From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Minutes From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
            } else if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Tickets From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Tickets From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
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

                perModeSetting = Regex.Replace(perModeSetting, @"(?:AsTickets|AsMinutes)", String.Empty);
                
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

        if (SecondsUntilAdaptiveSpeedBecomesFast < 120) {
            ConsoleWarn("Seconds Until Adaptive Speed Becomes Fast must be 120 or greater!");
            SecondsUntilAdaptiveSpeedBecomesFast = 120;
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
    ChatDetectedBadTeamSwitch = rhs.ChatDetectedBadTeamSwitch;
    YellDetectedBadTeamSwitch = rhs.YellDetectedBadTeamSwitch;
    ChatDetectedGoodTeamSwitch = rhs.ChatDetectedGoodTeamSwitch;
    YellDetectedGoodTeamSwitch = rhs.YellDetectedGoodTeamSwitch;
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
    fEnabledTimestamp = DateTime.Now;
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
        fEnabledTimestamp = DateTime.MinValue;

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
            FinishMove(soldierName, teamId);

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
                ConditionalIncrementMoves(soldierName);
                // Some other admin.movePlayer, so update to account for it
                UpdatePlayerTeam(soldierName, teamId);
                UpdateTeams();
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
                ConditionalIncrementMoves(soldierName);
                // Some other admin.movePlayer, so update to account for it
                UpdatePlayerTeam(soldierName, destinationTeamId);
                UpdateTeams();
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

        if (fFinalStatus != null) {
            try {
                DebugWrite("^bFINAL STATUS FOR PREVIOUS ROUND:^n", 3);
                foreach (TeamScore ts in fFinalStatus) {
                    if (ts.TeamID >= fTickets.Length) break;
                    fTickets[ts.TeamID] = (ts.Score == 1) ? 0 : ts.Score; // fix rounding
                }
                LogStatus();
            } catch (Exception) {}
            fFinalStatus = null;
        }

        if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map) {
            DebugWrite("ServerInfo update: " + serverInfo.Map + "/" + serverInfo.GameMode, 3);
        }
    
        if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime) {
            fServerCrashed = true;
        }
        fServerInfo = serverInfo;
        fServerUptime = serverInfo.ServerUptime;

        bool isRush = IsRush();
        double minTickets = Double.MaxValue;
        double maxTickets = 0;
        double attacker = 0;
        double defender = 0;
        if (fServerInfo.TeamScores == null)  return;
        foreach (TeamScore ts in fServerInfo.TeamScores) {
            if (ts.TeamID >= fTickets.Length) break;
            fTickets[ts.TeamID] = ts.Score;
            if (ts.Score > maxTickets) maxTickets = ts.Score;
            if (ts.Score < minTickets) minTickets = ts.Score;
        }

        if (isRush) {
            attacker = fServerInfo.TeamScores[0].Score;
            defender = fServerInfo.TeamScores[1].Score;
            DebugWrite("^7serverInfo: Rush attacker = " + attacker + ", was = " + fRushAttackerTickets + ", defender = " + defender, 7); 
        }

        if (fMaxTickets == -1) {
            if (!isRush) {
                fMaxTickets = maxTickets;
                DebugWrite("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0"), 3);
            } else if (fServerInfo.TeamScores.Count == 2) {
                fRushMaxTickets = defender;
                fMaxTickets = attacker;
                fRushStage = 1;
                fRushAttackerTickets = attacker;
                DebugWrite("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0") + ", fRushMaxTickets = " + fRushMaxTickets + ", fRushStage = " + fRushStage, 4);
            }
        }

        // Rush heuristic: if attacker tickets are higher than last check, new stage started
        if (isRush) {
            if (attacker > fRushAttackerTickets) {
                fRushMaxTickets = defender;
                fMaxTickets = attacker;
                fRushStage = fRushStage + 1;
                DebugWrite(".................................... ^b^1New rush stage detected^0^n ....................................", 3);
                DebugBalance("Rush Stage " + fRushStage + " of 4");
            }
            // update last known attacker ticket value
            fRushAttackerTickets = attacker;
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
        fFinalStatus = teamScores;
        ServerCommand("serverInfo"); // get info for final status report
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnRoundOver(int winningTeamId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOver^n: winner " + winningTeamId, 7);

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Round over detected^0^n ::::::::::::::::::::::::::::::::::::", 3);
    
        if (fGameState == GameState.Playing || fGameState == GameState.Unknown) {
            fGameState = GameState.RoundEnding;
            DebugWrite("OnRoundOver: ^b^3Game state = " + fGameState, 6);
        }
    } catch (Exception e) {
        ConsoleException(e.ToString());
    }
}

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnLevelLoaded^n: " + mapFileName + " " + Gamemode + " " + roundsPlayed + "/" + roundsTotal, 7);

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Level loaded detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

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
            DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1First spawn detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);

            ResetRound();
            fIsFullRound = true;
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
    int[] ascendingSize = null;
    int[] descendingTickets = null;
    String strongMsg = String.Empty;
    int diff = 0;
    DateTime now = DateTime.Now;
    bool needsBalancing = false;
    bool loggedStats = false;
    bool isSQDM = IsSQDM();

    /* Sanity checks */

    if (fServerInfo == null) {
        return;
    }

    int totalPlayerCount = TotalPlayerCount;

    if (totalPlayerCount > 0) {
        AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);
    } else {
        return;
    }

    if (diff > MaxDiff()) {
        if (totalPlayerCount < 6) {
            if (DebugLevel >= 7) DebugBalance("Not enough players in server, minimum is " + 6);
            return;
        }

        if (totalPlayerCount >= (MaximumServerSize-1)) {
            if (DebugLevel >= 7) DebugBalance("Server is full, no balancing or unstacking will be attempted!");
            return;
        }

        needsBalancing = true;
    }

    /* Pre-conditions */

    lock (fKnownPlayers) {
        if (!fKnownPlayers.TryGetValue(name, out player)) {
            DebugBalance("Unknown player: ^b" + name);
            return;
        }
    }
    if (player == null) {
        DebugBalance("No model for player: ^b" + name);
        return;
    }
    if (!fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode)) {
        DebugBalance("Unknown game mode: " + fServerInfo.GameMode);
        simpleMode = fServerInfo.GameMode;
    }
    if (String.IsNullOrEmpty(simpleMode)) {
        DebugBalance("Simple mode is null: " + fServerInfo.GameMode);
        return;
    }
    if (!fPerMode.TryGetValue(simpleMode, out perMode)) {
        DebugBalance("No per-mode settings for " + simpleMode + ", using defaults");
        perMode = new PerModeSettings();
    }
    if (perMode == null) {
        DebugBalance("Per-mode settings null for " + simpleMode + ", using defaults");
        perMode = new PerModeSettings();
    }

    /* Per-mode settings */

    Speed balanceSpeed = GetBalanceSpeed(perMode);
    double unstackTicketRatio = GetUnstackTicketRatio(perMode);

    // Adjust for duration of balance active
    if (needsBalancing && fBalanceIsActive && balanceSpeed == Speed.Adaptive && fLastBalancedTimestamp != DateTime.MinValue) {
        double secs = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (secs > SecondsUntilAdaptiveSpeedBecomesFast) {
            DebugBalance("^8^bBalancing taking too long (" + secs.ToString("F0") + " secs)!^n^0 Forcing to Fast balance speed.");
            balanceSpeed = Speed.Fast;
        }
    }
    String andSlow = (balanceSpeed == Speed.Slow) ? " and speed is Slow" : String.Empty;

    /* Activation check */

    if (balanceSpeed != Speed.Stop && needsBalancing) {
        if (!fBalanceIsActive) {
            DebugBalance("^2^bActivating autobalance!");
            fLastBalancedTimestamp = now;
        }
        fBalanceIsActive = true;
    } else if (fBalanceIsActive) {
        fBalanceIsActive = false;
        double dur = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (fLastBalancedTimestamp == DateTime.MinValue) dur = 0;
        if (dur > 0) {
            DebugBalance("^2^bDeactiving autobalance! Was active for " + dur.ToString("F0") + " seconds!");
        }
    }

    /* Exclusions */

    // Exclude if on Whitelist or Reserved Slots if enabled
    if (needsBalancing && balanceSpeed != Speed.Fast && (OnWhitelist || balanceSpeed == Speed.Slow)) {
        List<String> vip = new List<String>(Whitelist);
        if (EnableWhitelistingOfReservedSlotsList) vip.AddRange(fReservedSlots);
        /*
        while (vip.Contains(String.Empty)) {
            vip.Remove(String.Empty);
        }
        */
        if (vip.Contains(name) || vip.Contains(ExtractTag(player)) || vip.Contains(player.EAGUID)) {
            DebugBalance("Excluding ^b" + player.FullName + "^n: whitelisted" + andSlow);
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
    int minPlayers = (isSQDM) ? 5 : fromList.Count; // for SQDM, apply top/strong/weak only if team has 5 or more players

    // Exclude if TopScorers enabled and a top scorer on the team
    int topPlayersPerTeam = 0;
    if (balanceSpeed != Speed.Fast && (TopScorers || balanceSpeed == Speed.Slow)) {
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
            if (needsBalancing && balanceSpeed != Speed.Fast && fromList.Count >= minPlayers && topPlayersPerTeam != 0 && i < topPlayersPerTeam) {
                String why = (balanceSpeed == Speed.Slow) ? "Speed is slow, excluding top scorers" : "Top Scorers enabled";
                if (!loggedStats) {
                    DebugBalance(GetPlayerStatsString(name));
                    loggedStats = true;
                }
                DebugBalance("Excluding ^b" + player.FullName + "^n: " + why + " and this player is #" + (i+1) + " on team " + player.Team);
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

    // Exclude if player joined less than MinutesAfterJoining
    double joinedMinutesAgo = GetPlayerJoinedTimeSpan(player).TotalMinutes;
    double enabledForMinutes = now.Subtract(fEnabledTimestamp).TotalMinutes;
    if (needsBalancing && (enabledForMinutes > MinutesAfterJoining) && balanceSpeed != Speed.Fast && (joinedMinutesAgo < MinutesAfterJoining)) {
        if (!loggedStats) {
            DebugBalance(GetPlayerStatsString(name));
            loggedStats = true;
        }
        DebugBalance("Excluding ^b" + player.FullName + "^n: joined less than " + MinutesAfterJoining.ToString("F1") + " minutes ago (" + joinedMinutesAgo.ToString("F1") + ")");
        fExcludedRound = fExcludedRound + 1;
        IncrementTotal();
        return;   
    }

    /* Balance */

    bool mustMove = false;

    int toTeamDiff = 0;
    int toTeam = ToTeam(name, player.Team, out toTeamDiff, out mustMove); // take into account dispersal by Rank, etc.

    if (needsBalancing && (toTeam == 0 || toTeam == player.Team)) {
        if (DebugLevel >= 8) DebugBalance("Exempting ^b" + name + "^n, target team selected is same or zero");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    if (needsBalancing && toTeamDiff <= MaxDiff()) {
        DebugBalance("Exempting ^b" + name + "^n, difference between team " + player.Team + " and " + toTeam + " is only " + toTeamDiff);
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    if (fBalanceIsActive && toTeam != 0) {
        String ts = null;
        if (isSQDM) {
            ts = fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D)";
        } else {
            ts = fTeam1.Count + "(US) vs " + fTeam2.Count + "(RU)";
        }
        DebugBalance("Autobalancing because difference of " + diff + " is greater than " + MaxDiff() + ", [" + ts + "]");
        double abTime = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (abTime > 0) {
            DebugBalance("^2^bAutobalance has been active for " + abTime.ToString("F1") + " seconds!");
        }

        if (!loggedStats) {
            DebugBalance(GetPlayerStatsString(name));
            loggedStats = true;
        }

        /* Exemptions */

        // Already on the smallest team
        if (!mustMove && player.Team == smallestTeam) {
            DebugBalance("Exempting ^b" + name + "^n, already on the smallest team");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            return;
        }

        // Has this player already been moved for balance or unstacking?
        if (!mustMove && player.MovesRound >= 1) {
            DebugBalance("Exempting ^b" + name + "^n, already moved this round");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            return;
        }

        // SQDM, not on the biggest team nor the next biggest team
        if (isSQDM && !mustMove && balanceSpeed != Speed.Fast && player.Team != biggestTeam && player.Team != ascendingSize[2]) {
            DebugBalance("Exempting ^b" + name + "^n, not on the biggest team nor next biggest team");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            return;
        }

        if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count >= minPlayers) { // TBD
            if (DebugLevel > 5) DebugBalance(strongMsg);
            // don't move weak player to losing team
            if (!isStrong  && toTeam == losingTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // don't move strong player to winning team
            if (isStrong && toTeam == winningTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // Don't move to same team
            if (player.Team == toTeam) {
                if (DebugLevel >= 7) DebugBalance("Exempting ^b" + name + "^n, don't move player to his own team!");
                return;
            }
        }


        // TBD

        /* Move for balance */

        MoveInfo move = new MoveInfo(name, player.Tag, player.Team, GetTeamName(player.Team), toTeam, GetTeamName(toTeam));
        move.Reason = ReasonFor.Balance;
        move.Format(ChatMovedForBalance, false, false);
        move.Format(YellMovedForBalance, true, false);

        DebugWrite("^9" + move, 7);

        StartMoveImmediate(move, false);

        if (EnableLoggingOnlyMode) {
            // Simulate completion of move
            OnPlayerTeamChange(name, toTeam, 0);
            OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
        }
        return;
    }

    if (!fBalanceIsActive) {
        fLastBalancedTimestamp = now;
        if (DebugLevel >= 8) DebugBalance("fLastBalancedTimestamp = " + fLastBalancedTimestamp.ToString("HH:mm:ss"));
    }

    /* Unstack */

    if (winningTeam <= 0 || winningTeam >= fTickets.Length || losingTeam <= 0 || losingTeam >= fTickets.Length || balanceSpeed == Speed.Stop) {
        return;
    }

    int tpc = TotalPlayerCount;
    if (tpc > (MaximumServerSize-2) || tpc > (perMode.MaxPlayers-2)) {
        // TBD - kick idle players?
        if (DebugLevel >= 7) DebugBalance("No room to swap players for unstacking");
        return;
    }

    if (perMode.CheckTeamStackingAfterFirstMinutes == 0) {
        if (DebugLevel >= 5) DebugBalance("Unstacking has been disabled, Check Team Stacking After First Minutes set to zero)");
        return;
    }

    double tirMins = GetTimeInRoundMinutes();

    if (tirMins < perMode.CheckTeamStackingAfterFirstMinutes) {
        DebugBalance("Too early to check for unstacking, skipping ^b" + name + "^n");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    if ((fUnstackedRound/2) >= perMode.MaxUnstackingSwapsPerRound) {
        if (DebugLevel >= 7) DebugBalance("Maximum swaps have already occurred this round (" + (fUnstackedRound/2) + ")");
        return;
    }

    double ratio = 1;
    if (fTickets[losingTeam] >= 1) {
        if (IsRush()) {
            double attackers = fTickets[1];
            double defenders = fMaxTickets - (fRushMaxTickets - fTickets[2]);
            defenders = Math.Max(defenders, attackers/2);
            ratio = (attackers > defenders) ? (attackers/defenders) : (defenders/attackers);
        } else {
            ratio = Convert.ToDouble(fTickets[winningTeam]) / Convert.ToDouble(fTickets[losingTeam]);
        }
    }

    String um = "Ticket ratio " + (ratio*100.0).ToString("F0") + " vs. unstack ratio of " + (unstackTicketRatio*100.0).ToString("F0");

    if (unstackTicketRatio == 0 || ratio < unstackTicketRatio) {
        if (DebugLevel >= 7) DebugBalance("No unstacking needed: " + um);
        return;
    }

    double nsis = NextSwapInSeconds();
    if (nsis > 0 && fUnstackState == UnstackState.SwappedWeak) {
        if (DebugLevel >= 7) DebugBalance("Too soon to do another unstack swap, wait another " + nsis.ToString("F1") + " seconds!");
        return;
    }

    // Are the minimum number of players present to decide strong vs weak?
    if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count < minPlayers) {
        DebugBalance("Not enough players in team to determine strong vs weak, skipping ^b" + name + "^n, ");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    // Has this player already been moved for balance or unstacking?
    if (!mustMove && player.MovesRound >= 1) {
        DebugBalance("Already moved this round, skipping ^b" + name + "^n, ");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    // Otherwise, unstack!
    DebugBalance("^6Unstacking!^0 " + um);

    if (!loggedStats) {
        DebugBalance(GetPlayerStatsString(name));
        loggedStats = true;
    }

    MoveInfo moveUnstack = null;

    switch (fUnstackState) {
        case UnstackState.Off:
            // First swap
        case UnstackState.SwappedWeak:
            // Swap strong to losing team
            if (isStrong) {
                // Don't move to same team
                if (player.Team == losingTeam) {
                    if (DebugLevel >= 7) DebugBalance("Skipping strong ^b" + name + "^n, don't move player to his own team!");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }
                DebugBalance("Sending strong player ^0^b" + player.FullName + "^n^9 to losing team " + GetTeamName(losingTeam));
                moveUnstack = new MoveInfo(name, player.Tag, player.Team, GetTeamName(player.Team), losingTeam, GetTeamName(losingTeam));
                toTeam = losingTeam;
                fUnstackState = UnstackState.SwappedStrong;
            } else {
                DebugBalance("Skipping ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }
            break;
        case UnstackState.SwappedStrong:
            // Swap weak to winning team
            if (!isStrong) {
                // Don't move to same team
                if (player.Team == winningTeam) {
                    if (DebugLevel >= 7) DebugBalance("Skipping weak ^b" + name + "^n, don't move player to his own team!");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }
                DebugBalance("Sending weak player ^0^b" + player.FullName + "^n^9 to winning team " + GetTeamName(winningTeam));
                moveUnstack = new MoveInfo(name, player.Tag, player.Team, GetTeamName(player.Team), winningTeam, GetTeamName(winningTeam));
                toTeam = winningTeam;
                fUnstackState = UnstackState.SwappedWeak;
                fFullUnstackSwapTimestamp = now;
                if ((fUnstackedRound/2) >= perMode.MaxUnstackingSwapsPerRound) {
                    DebugBalance("^1Maximum unstacking swaps has been reached for this round, no more unstacking will occur!");
                    fUnstackState = UnstackState.Off;
                }
            } else {
                DebugBalance("Skipping ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (median+1) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }
            break;
        default: return;
    }

    /* Move for unstacking */

    moveUnstack.Reason = ReasonFor.Unstack;
    moveUnstack.Format(ChatMovedToUnstack, false, false);
    moveUnstack.Format(YellMovedToUnstack, true, false);

    DebugWrite("^9" + moveUnstack, 7);

    StartMoveImmediate(moveUnstack, false);

    if (EnableLoggingOnlyMode) {
        // Simulate completion of move
        OnPlayerTeamChange(name, toTeam, 0);
        OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
    }
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
    
    int unTeam = -2;
    
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

    if (!EnableLoggingOnlyMode && unTeam != -2) {
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


private bool CheckTeamSwitch(String name, int toTeam) {

    if (fPluginState != PluginState.Active || fGameState != GameState.Playing) return false;

    // Get model
    PlayerModel player = null;
    lock (fKnownPlayers) {
        if (!fKnownPlayers.TryGetValue(name, out player) || player == null) {
            DebugUnswitch("IGNORED: Unknown player: ^b" + name);
            return false;
        }
    }

    // Same team?
    if (toTeam == player.Team) {
        ConsoleDebug("CheckTeamSwitch: name = " + name + ", changing to same team " + toTeam + "?");
        return false;
    }
    
    // Whitelisted?
    if (OnWhitelist) {
        List<String> vip = new List<String>(Whitelist);
        if (EnableWhitelistingOfReservedSlotsList) vip.AddRange(fReservedSlots);
        /*
        while (vip.Contains(String.Empty)) {
            vip.Remove(String.Empty);
        }
        */
        if (vip.Contains(name) || vip.Contains(ExtractTag(player)) || vip.Contains(player.EAGUID)) {
            DebugUnswitch("ALLOWED: On whitelist: ^b" + name);
            return true;
        }
    }

    // Unlimited time?
    if (UnlimitedTeamSwitchingDuringFirstMinutesOfRound > 0 && GetTimeInRoundMinutes() < UnlimitedTeamSwitchingDuringFirstMinutesOfRound) {
        DebugUnswitch("ALLOWED: Time in round " + GetTimeInRoundMinutes().ToString("F0") + " < " + UnlimitedTeamSwitchingDuringFirstMinutesOfRound.ToString("F0"));
        return true;
    }

    // Helps?
    int diff = 0;
    int biggestTeam = 0;
    int smallestTeam = 0;
    int winningTeam = 0;
    int losingTeam = 0;
    int[] ascendingSize = null;
    int[] descendingTickets = null;
    int fromTeam = player.Team;
    MoveInfo move = null;
    bool toLosing = false;
    bool toSmallest = false;

    AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    int iFrom = 0;
    int iTo = 0;

    if (IsSQDM()) {
        // Score before size
        for (int i = 0; i < descendingTickets.Length; ++i) {
            if (fromTeam == descendingTickets[i]) iFrom = i;
            if (toTeam == descendingTickets[i]) iTo = i;
        }
        toLosing = (iTo > iFrom);
    } else {
        toLosing = (toTeam == losingTeam);
    }

    // Trying to switch to losing team?
    if (toLosing) {
        move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam));
        move.Format(ChatDetectedGoodTeamSwitch, false, true);
        move.Format(YellDetectedGoodTeamSwitch, true, true);
        DebugUnswitch("ALLOWED: Team switch to losing team ^b: " + name);
        Chat(name, move.ChatBefore);
        Yell(name, move.YellBefore);
        return true;
    }

    if (IsSQDM()) {
        for (int i = 0; i < ascendingSize.Length; ++i) {
            if (fromTeam == ascendingSize[i]) iFrom = i;
            if (toTeam == ascendingSize[i]) iTo = i;
        }
        toSmallest = (iTo < iFrom);
    } else {
        toSmallest = (toTeam == smallestTeam);
    }

    // Trying to switch to smallest team?
    if (toSmallest) {
        move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam));
        move.Format(ChatDetectedGoodTeamSwitch, false, true);
        move.Format(YellDetectedGoodTeamSwitch, true, true);
        DebugUnswitch("ALLOWED: Team switch to smallest team ^b: " + name);
        Chat(name, move.ChatBefore);
        Yell(name, move.YellBefore);
        return true;
    }

    // Otherwise, do not allow the team switch

    // TBD: select forbidden message from: moved by autobalance, moved to unstack, dispersal, ...

    // Tried to switch "toTeam" from "player.Team", so moving from "toTeam" back to original team (player.Team)
    DebugUnswitch("FORBIDDEN: Detected bad team switch, scheduling admin kill and move for ^b: " + name);
    move = new MoveInfo(name, player.Tag, toTeam, GetTeamName(toTeam), player.Team, GetTeamName(player.Team));
    move.Reason = ReasonFor.Unswitch;
    move.Format(ChatDetectedBadTeamSwitch, false, true);
    move.Format(YellDetectedBadTeamSwitch, true, true);
    move.Format(ChatAfterUnswitching, false, false);
    move.Format(YellAfterUnswitching, true, false);

    if (DebugLevel >= 7) DebugUnswitch(move.ToString());

    KillAndMoveAsync(move);

    return false;
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
            tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
        }
    }

    if (!okKiller) {
        ConsoleDebug("player ^b" + killer + "^n is a killer, but not a known player!");
    }
    
    if (!okVictim) {
        ConsoleDebug("player ^b" + victim + "^n is a victim, but not a known player!");
    } else {
        int toTeam = 0;
        int fromTeam = 0;
        int level = (GetTeamDifference(ref fromTeam, ref toTeam) > MaxDiff()) ? 4 : 8;

        // XXX DebugWrite("^9" + GetPlayerStatsString(victim), level);
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
        ServerCommand("admin.movePlayer", move.Name, move.Destination.ToString(), "0", "false"); // TBD, assign to squad also
        ScheduleListPlayers(10);
    }
    String r = null;
    switch (move.Reason) {
        case ReasonFor.Balance: r = " for balance"; break;
        case ReasonFor.Unstack: r = " to unstack teams"; break;
        case ReasonFor.Unswitch: r = " to unswitch player"; break;
        default: r = " for ???"; break;
    }
    String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1MOVING^0 " : "^b^1MOVING^0 ";
    DebugWrite(doing + move.Name + "^n from " + move.SourceName + " to " + move.DestinationName + r, 2);
}

private void FinishMove(String name, int team) {
    // If this is an MB move, handle it
    MoveInfo move = null;
    lock (fMoving) {
        if (fMoving.ContainsKey(name)) {
            move = fMoving[name];
            fMoving.Remove(name);
            try {
                UpdatePlayerTeam(name, team);
                UpdateTeams();
                if (move.Reason == ReasonFor.Balance) {fBalancedRound = fBalancedRound + 1; IncrementMoves(name);}
                else if (move.Reason == ReasonFor.Unstack) {fUnstackedRound = fUnstackedRound + 1; IncrementMoves(name);}
                else if (move.Reason == ReasonFor.Unswitch) {fUnswitchedRound = fUnswitchedRound + 1; IncrementTotal();}
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
        /* Shouldn't we let caller decide if this is an admin move?
        // Some other admin.movePlayer, so update to account for it
        UpdatePlayerTeam(name, team);
        UpdateTeams();
        */
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
            Thread.Sleep(Convert.ToInt32(YellDurationSeconds*1000));
            if (!fIsEnabled) return;

            // Make sure player is dead
            if (!EnableLoggingOnlyMode) {
                ServerCommand("admin.killPlayer", move.Name);
                DebugWrite("^b^1ADMIN KILL^0 " + move.Name, 2);
            } else {
                DebugWrite("^9(SIMULATING) ^b^1ADMIN KILL^0 " + move.Name, 2);
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
    String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1REASSIGNING^0^n new player ^b" : "^b^1REASSIGNING^0^n new player ^b";
    DebugWrite(doing + name + "^n from team " + fromTeam + " to team " + toTeam + " because difference is " + diff, 2);
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
        doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to ^b" : "^b^1CHAT^0^n to ^b";
        DebugWrite(doing + who + "^n: " + what, 2);
    } else {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.say", what, "all"); // chat all
        }
        ProconChat(what);
        doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to all: " : "^b^1CHAT^0^n to all: ";
        DebugWrite(doing + what, 2);
    }
}

private void Yell(String who, String what) {
    String doing = null;
    if (!EnableLoggingOnlyMode) {
        ServerCommand("admin.yell", what, YellDurationSeconds.ToString("F0"), "player", who); // yell to player
    }
    doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1YELL^0^n to ^b" : "^b^1YELL^0^n to ^b";
    DebugWrite(doing + who + "^n: " + what, 2);
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
    bool revalidate = false;
    lock (fKnownPlayers) {
        List<String> garbage = new List<String>();

        // collect up garbage
        foreach (String name in fKnownPlayers.Keys) {
            PlayerModel m = fKnownPlayers[name];
            if (DateTime.Now.Subtract(m.LastSeenTimestamp).TotalMinutes > MODEL_TIMEOUT) {
                if (IsKnownPlayer(name)) {
                    ConsoleDebug("^b" + name + "^n has timed out and is still on active players list, idling?");
                    // Revalidate the data model
                    revalidate = true;
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

    if (revalidate) {
        lock (fAllPlayers) {
            fAllPlayers.Clear();
            ScheduleListPlayers(1);
        }
    }

    if (n > 0) {
        DebugWrite("^9Garbage collected " + n + " old players from known players table", 6);
    }
}

private Phase GetPhase(PerModeSettings perMode, bool verbose) {
    if (fServerInfo == null || fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return Phase.Mid;

    // earlyTickets relative to max for count down, 0 for count up
    // lateTickets relative to 0 for count down, max for count up
    double earlyTickets = perMode.DefinitionOfEarlyPhaseFromStart;
    double lateTickets = perMode.DefinitionOfLatePhaseFromEnd;
    Phase phase = Phase.Early;

    double tickets = -1;
    double goal = 0;
    bool countDown = true;

    if (Regex.Match(fServerInfo.GameMode, @"(?:TeamDeathMatch|SquadDeathMatch)").Success) {
        countDown = false;
        goal = fServerInfo.TeamScores[0].WinningScore;
    }

    // Find ticket count closest to end
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
        if (tickets <= lateTickets) {
            phase = Phase.Late;
        } else if (fIsFullRound && (earlyTickets < fMaxTickets) && tickets >= (fMaxTickets - earlyTickets)) {
            phase = Phase.Early;
        } else {
            phase = Phase.Mid;
        }
    } else {
        // count up
        if (lateTickets < goal && tickets >= (goal - lateTickets)) {
            phase = Phase.Late;
        } else if (tickets <= earlyTickets) {
            phase = Phase.Early;
        } else {
            phase = Phase.Mid;
        }
    }

    if (verbose && DebugLevel >= 8) DebugBalance("Phase: " + phase + " (" + tickets + " of " + fMaxTickets + " to " + goal + ", " + RemainingTicketPercent(tickets, goal).ToString("F0") + "%)");

    return phase;
}

private Population GetPopulation(PerModeSettings perMode, bool verbose) {
    if (fServerInfo == null) return Population.Medium;

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
    fFinalStatus = null;
    fMaxTickets = -1;
    fBalanceIsActive = false;
    fIsFullRound = false;
    fLastMsg = null;
}

private void ResetRound() {
    ClearTeams();

    for (int i = 0; i < fTickets.Length; i++) {
        fTickets[i] = 0;
    }
            
    fRoundStartTimestamp = DateTime.Now;
    fFullUnstackSwapTimestamp = DateTime.MinValue;

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
    fUnstackState = UnstackState.Off;
    fRushStage = 0;
    fRushAttackerTickets = 0;

    fLastBalancedTimestamp = DateTime.MinValue;
}

private bool IsSQDM() {
    if (fServerInfo == null) return false;
    return (fServerInfo.GameMode == "SquadDeathMatch0");
}

private bool IsRush() {
    if (fServerInfo == null) return false;
    return (fServerInfo.GameMode == "RushLarge0" || fServerInfo.GameMode == "SquadRush0");
}

private int MaxDiff() {
    if (fServerInfo == null || IsSQDM()) return 2;
    PerModeSettings perMode = null;
    String simpleMode = String.Empty;
    if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) && fPerMode.TryGetValue(simpleMode, out perMode) && perMode != null) {
        return ((GetPopulation(perMode, false) == Population.High) ? 2 : 1);
    }
    return 2;
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


private void AnalyzeTeams(out int maxDiff, out int[] ascendingSize, out int[] descendingTickets, out int biggestTeam, out int smallestTeam, out int winningTeam, out int losingTeam) {
    biggestTeam = 0;
    smallestTeam = 0;
    winningTeam = 0;
    losingTeam = 0;
    maxDiff = 0;
    bool isSQDM = IsSQDM();

    ascendingSize = new int[4]{0,0,0,0};
    descendingTickets = new int[4]{0,0,0,0};

    if (fServerInfo == null) return;

    List<TeamRoster> teams = new List<TeamRoster>();

    teams.Add(new TeamRoster(1, fTeam1));
    teams.Add(new TeamRoster(2, fTeam2));
    if (isSQDM) {
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

    for (int i = 0; i < ascendingSize.Length; ++i) {
        if (isSQDM || i < 2) {
            ascendingSize[i] = teams[i].Team;
        } else {
            ascendingSize[i] = 0;
        }
    }

    TeamRoster small = teams[0];
    TeamRoster big = teams[teams.Count-1];
    smallestTeam = small.Team;
    biggestTeam = big.Team;
    maxDiff = big.Roster.Count - small.Roster.Count;

    List<TeamScore> byScore = new List<TeamScore>();
    if (fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return;
    if (IsRush() && fServerInfo.TeamScores.Count == 2) {
        // Normalize scores
        TeamScore attackers = fServerInfo.TeamScores[0];
        TeamScore defenders = fServerInfo.TeamScores[1];
        double normalized = fMaxTickets - (fRushMaxTickets - defenders.Score);
        normalized = Math.Max(normalized, Convert.ToDouble(attackers.Score)/2);
        byScore.Add(attackers); // attackers
        byScore.Add(new TeamScore(defenders.TeamID, Convert.ToInt32(normalized), defenders.WinningScore));
    } else {
        byScore.AddRange(fServerInfo.TeamScores);
    }

    byScore.Sort(delegate(TeamScore lhs, TeamScore rhs) {
        // Sort descending order by score
        if (lhs == null || rhs == null) return 0;
        if (lhs.Score < rhs.Score) return 1;
        if (lhs.Score > rhs.Score) return -1;
        return 0;
    });

    for (int i = 0; i < descendingTickets.Length; ++i) {
        if (isSQDM || i < 2) {
            descendingTickets[i] = byScore[i].TeamID;
        } else {
            descendingTickets[i] = 0;
        }
    }

    winningTeam = byScore[0].TeamID;
    losingTeam = byScore[byScore.Count-1].TeamID;
}

private int DifferenceFromSmallest(int fromTeam) {
    int biggestTeam = 0;
    int smallestTeam = 0;
    int winningTeam = 0;
    int losingTeam = 0;
    int diff = 0;
    int[] ascendingSize = null;
    int[] descendingTickets = null;

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

    AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

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
    int[] ascendingSize = null;
    int[] descendingTickets = null;

    AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    // diff is maximum difference between any two teams

    if (diff <= MaxDiff()) return 0;

    int targetTeam = smallestTeam;
    
    // Special handling for SQDM, when small teams are equal, pick the lowest numbered ID
    if (IsSQDM()) {
        if (targetTeam == 4 && fTeam3.Count == fTeam4.Count && fromTeam != 3) targetTeam = 3;
        if (targetTeam == 3 && fTeam2.Count == fTeam3.Count && fromTeam != 2) targetTeam = 2;
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

private void ConditionalIncrementMoves(String name) {
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
        if (IsRush() && tickets > fMaxTickets && tickets < fRushMaxTickets) {
            double normalized = Math.Max(0, fMaxTickets - (fRushMaxTickets - tickets));
            return ((normalized / fMaxTickets) * 100.0);
        }
        return ((tickets / fMaxTickets) * 100.0);
    }
    return (((goal - tickets) / goal) * 100.0);
}

private TimeSpan GetPlayerJoinedTimeSpan(PlayerModel player) {
    if (player != null) {
        return(DateTime.Now.Subtract(player.FirstSeenTimestamp));
    }
    return TimeSpan.FromMinutes(0);
}

private void DebugBalance(String msg) {
    // Filter out repeat messages
    int level = 4;
    if (fLastMsg != null) {
        if (msg.Equals(fLastMsg)) {
            level = 8;
        } else {
            String[] mWords = msg.Split(new Char[] {' '});
            String[] lWords = fLastMsg.Split(new Char[] {' '});

            int n = Math.Min(mWords.Length, lWords.Length);
            int i = 0;
            for (i = 0; i < n; ++i) {
                if (!mWords[i].Equals(lWords[i])) break;
            }
            if ((i+1) >= 5) level = 8;
        }
    }
    DebugWrite("^5(AUTO)^9 " + msg, level);
    fLastMsg = msg;
}

private void DebugUnswitch(String msg) {
    DebugWrite("^5(SWITCH)^9 " + msg, 4);
}

private double NextSwapInSeconds() {
    if (fFullUnstackSwapTimestamp == DateTime.MinValue) return 0;
    double since = DateTime.Now.Subtract(fFullUnstackSwapTimestamp).TotalSeconds;
    if (since > SWAP_TIMEOUT) return 0;
    return (SWAP_TIMEOUT - since);
}


private String GetPlayerStatsString(String name) {
    DateTime now = DateTime.Now;
    double score = -1;
    double kills = -1;
    double deaths = -1;
    double kdr = -1;
    double spm = -1;
    int team = -1;
    bool ok = false;
    TimeSpan tir = TimeSpan.FromSeconds(0); // Time In Round
    PlayerModel m = null;

    lock (fKnownPlayers) {
        if (fKnownPlayers.TryGetValue(name, out m)) {
            ok = true;
            m.LastSeenTimestamp = now;
            m.IsDeployed = false;
            score = m.ScoreRound;
            kills = m.KillsRound;
            deaths = m.DeathsRound;
            kdr = m.KDRRound;
            spm = m.SPMRound;
            tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
            team = m.Team;
        }
    }

    if (!ok) return("NO STATS FOR: " + name);

    Match rm = Regex.Match(tir.ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
    String sTIR = (rm.Success) ? rm.Groups[1].Value : "?";
    String vn = m.FullName;

    return("STATS: ^b" + vn + "^n [T:" + team + ", S:" + score + ", K:" + kills + ", D:" + deaths + ", KDR:" + kdr.ToString("F2") + ", SPM:" + spm.ToString("F0") + ", TIR: " + sTIR + "]");
}


private double GetTimeInRoundMinutes() {
    DateTime rst = (fRoundStartTimestamp == DateTime.MinValue) ? DateTime.Now : fRoundStartTimestamp;
    return (DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes);
}

private String GetTimeInRoundString() {
    DateTime rst = (fRoundStartTimestamp == DateTime.MinValue) ? DateTime.Now : fRoundStartTimestamp;
    Match rm = Regex.Match(DateTime.Now.Subtract(fRoundStartTimestamp).ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
    return ( (rm.Success) ? rm.Groups[1].Value : "?" );
}


private void LogStatus() {
    // If server is empty, log status only every 20 minutes
    if (TotalPlayerCount == 0) {
        if (fRoundStartTimestamp != DateTime.MinValue && DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes <= 20) {
            return;
        } else {
            fRoundStartTimestamp = DateTime.Now;
        }
    }

    Speed balanceSpeed = Speed.Adaptive;

    String tm = fTickets[1] + "/" + fTickets[2];
    if (IsSQDM()) tm = tm + "/" + fTickets[3] + "/" + fTickets[4];

    String rt = GetTimeInRoundString();

    DebugWrite("^bStatus^n: Plugin state = " + fPluginState + ", game state = " + fGameState + ", Enable Logging Only Mode = " + EnableLoggingOnlyMode, 4);
    if (IsRush()) {
        DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", stage = " + fRushStage + ", time in round = " + rt + ", tickets = " + tm + "(" + Math.Max(fTickets[1]/2, fMaxTickets - (fRushMaxTickets - fTickets[2])) + ")", 4);
    } else {
        DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", time in round = " + rt + ", tickets = " + tm, 4);
    }
    if (fPluginState == PluginState.Active) {
        double secs = DateTime.Now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (!fBalanceIsActive || fLastBalancedTimestamp == DateTime.MinValue) secs = 0;
        PerModeSettings perMode = null;
        String simpleMode = String.Empty;
        if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) 
          && fPerMode.TryGetValue(simpleMode, out perMode) && perMode != null) {
            balanceSpeed = GetBalanceSpeed(perMode);
            double unstackRatio = GetUnstackTicketRatio(perMode);
            String activeTime = (secs > 0) ? "^1active (" + secs.ToString("F0") + " secs)^0" : "not active";
            DebugWrite("^bStatus^n: Autobalance is " + activeTime + ", phase = " + GetPhase(perMode, false) + ", population = " + GetPopulation(perMode, false) + ", speed = " + balanceSpeed + ", unstack when ticket ratio >= " + (unstackRatio * 100).ToString("F0") + "%", 3);
        }
    }
    if (!IsModelInSync()) DebugWrite("^bStatus^n: fMoving = " + fMoving.Count + ", fReassigned = " + fReassigned.Count, 5);

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
    String next = (diff > MaxDiff() && fGameState == GameState.Playing && balanceSpeed != Speed.Stop && !fBalanceIsActive) ? "^n^0 ... autobalance will activate on next death!" : "^n";
    
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
<p>This version of the plugin is capable of doing balancing and reassignment by moving players. <font color=#FF0000>Set PROTObalancer's <b>Enable Logging Only Mode</b> to true</font>, which insures that actions are logged only, not actually applied to your server. If you do not do this, this prototype will conflict with other balancing or player switching plugins and a player might be moved back and forth between teams repeatedly.</p>

<p><font color=#0000FF>The purpose of this prototype is to get feedback about usage and behavior.</font></p>

<p>Since logging to plugin.log is an important part of this phase of testing, it would be best if you could reduce or eliminate logging of other plugins so that most or all of the log is from PROTObalancer. If you are planning to leave the plugin unattended to collect logs, set <b>Debug Level</b> to 5. If you are going to watch the log interactively for a while, you can experiment with higher or lower <b>Debug Level</b> settings. It would also be useful to set the console.log to enable Event logging, but be advised that the console.log might get quite large. Do <b>not</b> enable Debug on your console.log.</p>

<h3>KNOWN ISSUES</h3>
<p>Only balancing and unstacking (excluding SQDM) is working. Team unswitching/balance guarding is not working.</p>

<p>The following settings are not hooked up, they don't do anything:
<ul>
<li>Enable Battlelog Requests</li>
<li>Maximum Request Rate</li>
<li>Unlimited Team Switching During First Minutes Of Round</li>
<li>Disperse Evenly List</li>
<li>Same Clan Tags In Squad</li>
<li>Per-mode: Disperse Evenly For Rank</li>
<li>Substitution %tag% for chat or yell message</li>
</ul>
</p>

<p><b>Sections 4, 6 and 7 are intentionally not defined.</b></p>

<h2>Description</h2>
<p>This plugin performs several automated operations:
<ul>
<li>Team balancing for all modes</li>
<li>Unstacking a stacked team</li>
<li>Unswitching players who team switch</li>
</ul></p>

<p>This plugin only moves players when they die. No players are killed by admin to be moved, with the single exception of players who attempt to switch teams when team switching is not allowed -- those players are admin killed before being moved back to their original team. This plugin also monitors new player joins and if the game server would assign a new player to the wrong team (a team with 30 players when another team only has 27 players), the plugin will <i>reassign</i> the player to the team that needs players for balance. This all happens before a player spawns, so they will not be aware that they were reassigned.</p>

<h3>Quick Start</h3>
<p>Don't want to spend a lot of time learning all of the settings for this plugin? Just follow these quick start steps:

<p>1) Select a <b>Preset</b> at the top of the plugin settings:
<table>
<tr><td><b>Standard</b></td><td>Autobalance and unstack teams, good for most server configurations</td></tr>
<tr><td><b>Aggressive</b></td><td>Autobalance and unstack teams quickly, moving lots of players in a short amount of time</td></tr>
<tr><td><b>Passive</b></td><td>Autobalance and unstack teams slowly, moving few players over a long period of time</td></tr>
<tr><td><b>Intensify</b></td><td>Focus on keeping teams evenly matched for a level playing field and an intense game</td></tr>
<tr><td><b>Retain</b></td><td>Focus on reducing rage quitting by keeping teams balanced, but refrain from too many player moves</td></tr>
<tr><td><b>BalanceOnly</b></td><td>Disable team unstacking, only move for autobalance</td></tr>
<tr><td><b>UnstackOnly</b></td><td>Disable autobalancing, only move to unstack teams</td></tr>
<tr><td><b>None</b></td><td>Custom plugin settings (this is automatically selected if you change settings controlled by <b>Presets</b>)</td></tr>
</table></p>

<p>2) Review plugin section <b>5. Messages</b> and change any messages you don't like.</p>

<p>3) That's it! You are good to go.</p>

<h3>Details</h3>
<p>This plugin provides a rich set of features for a wide variety of team management styles. Some (but not all) of the styles this plugin is designed for are listed below, and you can mix and max these styles depending on the game mode, number of players on the server and whether it is early or late in the round:</p>

<h4>Fair play</h4>
<p>This style aims for each round to be as evenly balanced in skills as possible. Every round should end as a &quot;nail-biter&quot;. If you want to see Conquest rounds end with ticket differences less than 20 or Team Deathmatch or Squad Deathmatch rounds end with kill differences less than 5 or Rush matches that get down to 1 ticket before the last MCOM is blown, the settings provided by this plugin give you the best chance to have that experience on your server.</p>

<h4>Cutthroat</h4>
<p>This is pretty much the exact opposite of Fair Play. Every player for himself and damn the consequences. If one team gets stacked with good players, that's just too bad for the other team. The newest players to join are the ones moved to keep teams balanced. This plugin supports cutthroat style by turning most of the features off, except new player reassignment and new player autobalancing.</p>

<h4>Retain players</h4>
<p>This style aims to retain players on your server. Players are left alone to do what they want, but aspects of team balance and team switching that cause players to leave, like too much autobalancing, team stacking, too many Colonel 100's on one team, too many players from one clan on one team, etc., are dealt with. Only things that are related to team balance are managed, however. This plugin doesn't do anything about, for example, base raping.</p>

<h4>Keep friends together</h4>
<p>This style recognizes that friends like to play together. To the extent that friends wear the same clan tag, the balancer and unstacker can be configured to keep players with the same tags together.</p>

<h4>Split problem clans apart</h4>
<p>This style recognizes that some &quot;pro&quot; clans can spoil everyone's fun if they play together, so the balancer and unstacker can be configured to split players with the same clan tag apart and spread them out evenly between teams.</p>

<h2>Concepts</h2>
<p>This plugin recognizes that a game round has a natural pattern and flow that depends on several factors. Play during the very beginning of a round is different from the very end. Play when the server is nearly empty is different from when the server is nearly full. The natural flow of a round of Conquest is very different from the flow of a game of Rush. Strong (good) players are not interchangeable with weak (bad) players. So with all these differences, how can one set of settings cover all of those different situations? They can't. So this plugin allows you to configure different settings for each combination of factors. The primary factors and concepts are described in the sections that follow.</p>

<h3>Round Phase</h3>
<p>To configure the factor of time, each round is divided into three time phases: <b>Early</b>, <b>Mid</b> (for Mid-phase), and <b>Late</b>. You define the phase based on ticket counts (or in the case of CTF, time in minutes) from the start of the round and the end of the round. You may define different settings for different modes, e.g., for <i>Conquest Large</i> you might define the early phase to be the first 200 tickets after the round starts, but for <i>Team Deathmatch</i> you might set early phase to be after the first 25 kills.</p>

<h3>Population</h3>
<p>To configure the factor of number of players, each round is divivded into three population levels: <b>Low</b>, <b>Medium</b>, and <b>High</b>. You define the population level based on total number of players in the server.</p>

<h3>Game Mode</h3>
<p>To configure the factor of game mode, each game mode is grouped into similar per-mode settings. For example, Conquest Large and Conquest Assault Large are grouped together as <b>Conquest Large</b>.</p>

<h3>Exclusions</h3>
<p>There are certain types of players that should never be moved for autobalance. You define those players with exclusions. For example, you can arrange for everyone on your reserved slots lists to be whitelisted so that they are ignored by this plugin.</p>

<h3>Balance Speed</h3>
<p>The aggressiveness with which the balancer selects players to move is controled by the speed names:
<ul>
<li>Stop: no balancing, no players are selected to move</li>
<li>Slow: few players are selected to move, all exclusions are applied, whether they are enabled by you or not</li>
<li>Fast: many players are selected to move, no exclusions are applied, whether they are enabled by you or not</li>
<li>Adaptive: starts out slow; if teams remain unbalanced, gradually selects more players to move; if teams are still unbalanced after <b>Seconds Until Adaptive Speed Becomes Fast</b>, many players are selected, etc.</li>
</ul></p>

<h3>Definition of Strong</h3>
<p>To configure the selection of strong players and weak players, you choose a definition for strong determined from:
<ul>
<li>Round Score</li>
<li>Round KDR</li>
<li>Round Kills</li>
<li>Player Rank</li>
</ul></p>

<h3>Ticket Percentage (Ratio)</h3>
<p>The ticket percentage ratio is calculated by taking the tickets of the winning team and dividing them by the tickets of the losing team, expressed as a percentage. For example, if the winning team has 550 tickets and the losing team has 500 tickets, the ticket percentage ratio is 110. This ratio is used to determine when teams are stacked. If the ticket percentage ratio exceeds the level that you set, unstacking swaps will begin.</p>

<h3>Unstacking</h3>
<p>To unstack teams, a strong player is selected from the winning team and is moved to the losing team. Then, a weak player is selected from the losing team and moved to the winning team. This is repeated until the round ends, or teams become unbalanced, or <b>Max&nbsp;Unstacking&nbsp;Swaps&nbsp;Per&nbsp;Round</b> is reached, whichever comes first.</p>

<h2>Settings</h2>
<p>Each setting is defined below. Settings are grouped into sections.</p>

<h3>0 - Presets</h3>
<p>See the <b>Quick Start</b> section above.</p>

<h3>1 - Settings</h3>
<p>These are general settings.</p>

<p><b>Debug Level</b>: Number from 0 to 9, default 2. Sets the amount of debug messages sent to plugin.log. Status messages for the state of the plugin may be seen at level 4 or higher. Complete details for operation of the plugin may be seen at level 7 or higher. When a problem with the plugin needs to be diagnosed, level 7 will often be required. Setting the level to 0 turns off all logging messages.</p>

<p><b>Maximum Server Size</b>: Number from 8 to 64, default 64. Maximum number of slots on your game server, regardless of game mode.</p>

<p><b>Enable Battlelog Requests</b>: True or False, default True. Enables making requests to Battlelog (uses BattlelogCache if available). Used to obtain clan tag for players.</p>

<p><b>Maximum Request Rate</b>: Number from 1 to 15, default 2. If <b>Enable Battlelog Requests</b> is set to True, defines the maximum number of Battlelog requests that are sent every 20 seconds.</p>

<p><b>Unlimited Team Switching During First Minutes Of Round</b>: Number greater than or equal to 0, default 5. Starting from the beginning of the round, this is the number of minutes that players are allowed to switch teams without restriction. After this time is expired, the plugin will prevent team switching that unbalances or stacks teams. The idea is to enable friends who were split up during the previous round due to autobalancing or unstacking to regroup so that they can play together this round. However, players who switch teams during this period are not excluded from being moved for autobalance or unstacking later in the round, unless some other exclusion applies them.</p>

<p><b>Seconds Until Adaptive Speed Becomes Fast</b>: Number of seconds greater than or equal to 120, default 180. If the autobalance speed is Adaptive and the autobalancer has been active for more than the specified number of seconds, the speed will be forced to Fast. This insures that teams don't remain unbalanced too long if Adaptive speed is not sufficient to move players.</p>

<p><b>Enable Whitelisting Of Reserved Slots List</b>: True or False, default True. Treats the reserved slots list as if it were added to the specified <b>Whitelist</b>.</p>

<p><b>Whitelist</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line, in any combination. If <b>On&nbsp;Whitelist</b> is enabled or the balance speed is <i>Slow</i>, any players on the whitelist are completely excluded from being moved by the plugin. Example list with the name of one player, tag of a clan, and GUID of another player:
<pre>
  PapaCharlie9
  LGN
  EA_20D5B089E734F589B1517C8069A37E28
</pre></p>

<p><b>Disperse Evenly List</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line, in any combination. Groups of players found on this list will be split up and moved so that they are evenly dispersed across teams.</p>

<h3>2 - Exclusions</h3>
<p>These settings define which players should be excluded from being moved for balance or unstacking. Changing a preset may overwrite the value of one or more of these settings. Changing one of these settings may change the value of the Preset, usually to None, to indicate a custom setting.</p>

<p><b>On Whitelist</b>: True or False, default True. If True, the Whitelist is used to exclude players. If False, the Whitelist is ignored.</p>

<p><b>Top Scorers</b>: True or False, default True. If True, the top 1, 2, or 3 players (depending on server population and mode) on each team are excluded from moves for balancing or unstacking. This is to reduce the whining and QQing when a team loses their top players to autobalancing.</p>

<p><b>Same Clan Tags In Squad</b>: True or False, default True. If True, a player will be excluded from being moved for balancing or unstacking if they are a member of a squad (or team, in the case of SQDM) that has at least one other player in it with the same clan tag.</p>

<p><b>Minutes After Joining</b>: Number greater than or equal to 0, default 5. After joining the server, a player is excluded from being moved for balance or unstacking for this number of minutes. Set to 0 to disable. Keep in mind that most joining players were already assigned to the team with the least players. They have already 'paid their dues'.</p>

<h3>3 - Round Phase and Population Settings</h3>
<p>These settings control balancing and unstacking, depending on the round phase and server population.
For each phase, there are three unstacking settings for server population: Low, Medium and High, by number of players. Each number is the ticket percentage ratio that triggers unstacking for each combination of phase and population. Setting the value to 0 disables team unstacking for that combination. If the number is not 0, if the ratio of the winning team's tickets to the losing teams tickets is equal to or greater than the ticket percentage ratio specified, unstacking will be activated.</p>

<p><i>Example</i>: for the <b>Ticket Percentage To Unstack</b> setting, there are three phases, Early, Mid and Late. For each phase, the value is a list of 3 number, either 0 or greater than or equal to 100, one for each of the population levels of Low, Medium, and High, respectively:
<pre>
    Early Phase: Ticket Percentage To Unstack        0, 120, 120
    Mid Phase: Ticket Percentage To Unstack          0, 120, 120
    Late Phase: Ticket Percentage To Unstack         0, 0, 0
</pre></p>

<p>This means that in the Early or Mid phases when the population is Low, there will be no unstacking (0 always means disable). Also, in the Late Phase for any population level, there will be no unstacking. For any other combination, such as Mid Phase with High population, teams will be unstacked when the ratio of winning tickets to losing tickets is 120% or more.</p>

<p>For each phase, there are also three balance speed names for server population: Low, Medium and High, by number of players. These speeds control how aggressively players are selected for moving by the autobalancer. Enter them as speed names separated by commas.</p>

<p><i>Example</i>: for the <b>Balance Speed</b> setting, there are three phases, Early, Mid and Late. For each phase, the value is a list of 3 speed names, one for each of the population levels of Low, Medium, and High, respectively: 
<pre>
    Early Phase: Balance Speed        Slow, Adaptive, Adaptive
    Mid Phase: Balance Speed          Slow, Adaptive, Adaptive
    Late Phase: Balance speed         Stop, Stop, Stop
</pre></p>

<p>This means that in the Early or Mid phases when the population is Low, the balance speed will be Slow. In the Late Phase for any population level, balancing will be disabled. For any other combination, such as Mid Phase with High population, balancing will use an Adaptive speed.</p>

<p>If you forget the names of the balance speeds, click on the <b>Spelling Of Speed Names Reminder</b> setting. This will display all of the balance speed names for you.</p>

<h3>4 - TBD</h3>
<p>There is no section 4. This section is reserved for future use.

<h3>5 - Messages</h3>
<p>These settings define all of the chat and yell messages that are sent to players when various actions are taken by the plugin. All of the messages are in pairs, one for chat, one for yell. If both the chat and the yell messages are defined and <b>Quiet&nbsp;Mode</b> is not set to True, both will be sent at the same time. The message setting descriptions apply to both chat and yell. To disable a chat message for a specific actcion, delete the message and leave it empty. To disable theyell message for a specific action, delete the message and leave it empty.</p>

<p>Several substitution macros are defined. You may use them in either chat or yell messages:
<table border='0'>
<tr><td>%name%</td><td>player name</td></tr>
<tr><td>%tag%</td><td>player clan tag</td></tr>
<tr><td>%fromTeam%</td><td>original team a player came from, as 'US' or 'RU', or 'Alpha', 'Bravo', 'Charlie', or 'Delta' for SQDM</td></tr>
<tr><td>%toTeam%</td><td>new team a player is/was moved to, ditto</td></tr>
</table></p>

<p><b>Quiet Mode</b>: True or False, default False. If False, chat messages are sent to all players and yells are sent to the player being moved. If True, chat and yell messages are only sent to the player being moved.</p>

<p><b>Yell Duration Seconds</b>: A number greater than 0 and less than or equal to 20, or 0. If set to 0, all yells are disabled, even if they have non-empty messages. All yells have the same duration.</p>

<p><b>Moved For Balance</b>: Message sent after a player is moved for balance.</p>

<p><b>Moved To Unstack</b>: Message sent after a player is moved to unstack teams.</p>

<p><b>Detected Bad Team Switch</b>: Message sent after a player switches from the losing team to the winning team, or from the smallest team to the biggest team, or after being moved by the plugin for balance or unstacking. The message is sent before player is sent back to his original team.</p>

<p><b>Detected Good Team Switch</b>: Message sent after a player switches from the winning team to the losing team, or from the biggest team to the smallest team. There is no follow-up message, this is the only one sent.</p>

<p><b>After Unswitching</b>: Message sent after a player is killed by admin and moved back to the team he was assigned to. This message is sent after the <b>Detected Bad Team Switch</b> message.</p>

<p><b>Detected Switch By Dispersal Player</b>: Message sent after a player on the <b>Disperse Evenly List</b> switches teams from the one he was sent to by the plugin. The message is sent before the player is sent back to his original team.</p>

<p><b>After Unswitching Dispersal Player</b>: Message sent after a player on the <b>Disperse Evenly List</b> is killed by admin and moved back to the team he was assigned to. This message is sent after the <b>Detected Switch By Dispersal Player</b> message.</p>

<h3>6 - TBD</h3>
<p>There is no section 6. This section is reserved for future use.</p>

<h3>7 - TBD</h3>
<p>There is no section 7. This section is reserved for future use.</p>

<h3>8 - Settings for ... (each game mode)</h3>
<p>These are the per-mode settings, used to define population and phase levels for a round and other settings specific to a game mode. Some modes have settings that no other modes have, other modes have fewer settings than most other modes. Each section is structured similarly. One common section is described in detail below and applies to several modes. Modes that have unique settings are then listed separately. The game modes are grouped as follows:
<table border='0'>
<tr><td>Conq Small, Dom, Scav</td><td>Conquest Small, Conquest Assault Small #1 and #2, Conquest Domination, and Scavenger</td></tr>
<tr><td>Conquest Large</td><td>Conquest Large and Conquest Assault Large</td></tr>
<tr><td>CTF</td><td>Capture The Flag, uses minutes to define phase instead of tickets</td></tr>
<tr><td>Gun Master</td><td>Only has a few settings</td></tr>
<tr><td>Rush</td><td>Has unique settings shared with Squad Rush and no other modes</td></tr>
<tr><td>Squad Deathmatch</td><td>Standard settings, similar to Conquest</td></tr>
<tr><td>Squad Rush</td><td>Has unique settings shared with Rush and no other modes</td></tr>
<tr><td>Superiority</td><td>Air and Tank Superiority</td></tr>
<tr><td>Team Deathmatch</td><td>Standard settings, similar to Conquest</td></tr>
<tr><td>Unknown or New Mode</td><td>Generic settings for any new mode that gets introduced before this plugin gets updated</td></tr>
</table></p>

<p>These are the settings that are common to most modes:</p>

<p><b>Max Players</b>: Number greater than or equal to 8 and less than or equal to <b>Maximum Server Size</b>. Some modes might be set up in UMM or Adaptive Server Size or other plugins with a lower maximum than the server maximum. If you set a lower value in your server settings or in a plugin, set the same setting here. This is important for calculating population size correctly.</p>

<p><b>Check Team Stacking After First Minutes</b>: Number greater than or equal to 0. From the start of the round, this setting is the number of minutes to wait before activating unstacking. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Max Unstacking Swaps Per Round</b>: Number greater than or equal to 0. To prevent the plugin from swapping every player on every team for unstacking, a maximum per round is set here. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Determine Strong Players By</b>: The setting defines how strong players are determined. Any player that is not a strong player is a weak player. See the <b>Definition of Strong</b> section above for the list of settings. All players in a single team are sorted by the specified definition. Any player above the median position after sorting is considered strong. For example, suppose there are 31 players on a team and this setting is set to <i>RoundScore</i> and after sorting, the median is position #16. If this player is position #7, he is considered strong. If his position is #16 or #17, he is considered weak.</p>

<p><b>Disperse Evenly For Rank >=</b>: Number greater than or equal to 0 and less than or equal to 145, default 145. Any players with this absolute rank (Colonel 100 is 145) or higher will be dispersed evenly across teams. This is useful to insure that Colonel 100 ranked players don't all stack on one team. Set to 0 to disable.</p>

<p><b>Definition Of High Population For Players >=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the High population level. If the total number of players in the server is greater than or equal to this number, population is High.</p>

<p><b>Definition Of Low Population For Players <=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the Low population level. If the total number of players in the server is less than or equal to this number, population is Low. If the total number is between the definition of High and Low, it is Medium.</p>

<p><b>Definition Of Early Phase As Tickets From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as tickets from the start of the round. For example, if your round starts with 1500 tickets and you set this to 300, as long as the ticket level for all teams is greater than or equal to 1500-300=1200, the phase is Early. Set to 0 to disable Early phase.</p>

<p><b>Definition Of Late Phase As Tickets From End</b>: Number greater than or equal to 0. This is where you define the Late phase, as tickets from the end of the round. For example, if you set this to 300 and at least one team in Conquest has less than 300 tickets less, the phase is Late. If the ticket level of both teams is between the Early and Late settings, the phase is Mid. Set to 0 to disable Late phase.</p>

<p>These settings are unique to CTF.</p>

<p><b>Definition Of Early Phase As Minutes From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as minutes from the start of the round. For example, if your round starts with 20 minutes on the clock and you set this to 5, the phase is Early until 20-5=15 minutes are left on the clock.</p>

<p><b>Definition Of Late Phase As Minutes From End</b>: This is where you define the Late phase, as minutes from the end of the round. For example, if your round starts with 20 minutes on the clock and you set this to 8, the phase is Late for when there 8 minutes or less left on the clock.</p>

<p>These settings are unique to Rush and Squad Rush.</p>

<p><b>TBD</b>: TBD</p>

<h3>9 - Debugging</h3>
<p>These settings are used for debugging problems with the plugin.</p>

<p><b>Show In Log</b>: Special commands may be typed in this text area to display information in plugin.log.</p>

<p><b>Log Chat</b>: True or False, default True. All chat and yell messages sent by the plugin will be logged in chat.log.</p>

<p><b>Enable Logging Only Mode</b>: True or False, default False. If set to True, the plugin will only log messages. No move, chat or yell commands will be sent to the game server. If set to False, the plugin will operate normally.</p>

<h2>Development</h2>
<p>TBD</p>
";

} // end PROTObalancerUtils


} // end namespace PRoConEvents
