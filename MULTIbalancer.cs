/* MULTIbalancer.cs

Copyright 2013, by PapaCharlie9
`   
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
using System.Xml;
using System.Windows.Forms;

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

public class MULTIbalancer : PRoConPluginAPI, IPRoConPluginInterface
{
    /* Enums */

    public enum MessageType { Warning, Error, Exception, Normal, Debug };
    
    public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, BalanceOnly, UnstackOnly, None };

    public enum Speed { Click_Here_For_Speed_Names, Stop, Slow, Adaptive, Fast, Unstack };

    public enum DefineStrong { RoundScore, RoundSPM, RoundKills, RoundKDR, PlayerRank, RoundKPM, BattlelogSPM, BattlelogKDR, BattlelogKPM };
    
    public enum PluginState { Disabled, JustEnabled, Active, Error, Reconnected };
    
    public enum GameState { RoundEnding, RoundStarting, Playing, Warmup, Unknown };

    public enum MoveType { Balance, Unstack, Unswitch };

    public enum ForbidBecause { None, MovedByBalancer, ToWinning, ToBiggest, DisperseByRank, DisperseByList };

    public enum Phase {Early, Mid, Late};

    public enum Population {Low, Medium, High};

    public enum UnstackState {Off, SwappedStrong, SwappedWeak};

    public enum FetchState {New, InQueue, Requesting, Aborted, Succeeded, Failed};

    public enum Scope {SameTeam, SameSquad};

    public enum UnswitchChoice {Always, Never, LatePhaseOnly};

    public enum BattlelogStats {ClanTagOnly, AllTime, Reset};

    public enum DivideByChoices {None, ClanTag, DispersalGroup};

    /* Constants & Statics */

    public const double SWAP_TIMEOUT = 600; // in seconds

    public const double MODEL_TIMEOUT = 24*60; // in minutes

    public const int CRASH_COUNT_HEURISTIC = 24; // player count difference signifies a crash

    public const int MIN_UPDATE_USAGE_COUNT = 20; // minimum number of plugin updates in use

    public const double CHECK_FOR_UPDATES_MINS = 12*60; // 12 hours

    public const double MIN_ADAPT_FAST = 120.0;

    public const String INVALID_NAME_TAG_GUID = "////////";

    public static String[] TEAM_NAMES = new String[] { "None", "US", "RU" };

    public static String[] RUSH_NAMES = new String[] { "None", "Attacking", "Defending" };

    public static String[] SQUAD_NAMES = new String[] { "None",
      "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
      "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa",
      "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray",
      "Yankee", "Zulu", "Haggard", "Sweetwater", "Preston", "Redford", "Faith", "Celeste"
    };

    public const String DEFAULT_LIST_ITEM = "[-- name, tag, or EA_GUID --]";

    /* Classes */
#region Classes
    public class PerModeSettings {
        public PerModeSettings() {}
        
        public PerModeSettings(String simplifiedModeName) {
            DetermineStrongPlayersBy = DefineStrong.RoundScore;
            PercentOfTopOfTeamIsStrong = 50;
            DisperseEvenlyByRank = 0;
            EnableDisperseEvenlyList = false;
            EnableScrambler = false;
            isDefault = false;
            // Rush only
            Stage1TicketPercentageToUnstackAdjustment = 0;
            Stage2TicketPercentageToUnstackAdjustment = 0;
            Stage3TicketPercentageToUnstackAdjustment = 0;
            Stage4And5TicketPercentageToUnstackAdjustment = 0;
            
            switch (simplifiedModeName) {
                case "Conq Small, Dom, Scav":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    MaxUnstackingSwapsPerRound = 2;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 50; // assuming 200 tickets typical
                    DefinitionOfLatePhaseFromEnd = 50; // assuming 200 tickets typical
                    MetroAdjustedDefinitionOfLatePhase = 100;
                    EnableMetroAdjustments = false;
                    break;
                case "Conquest Large":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    MaxUnstackingSwapsPerRound = 4;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 100; // assuming 300 tickets typical
                    DefinitionOfLatePhaseFromEnd = 100; // assuming 300 tickets typical
                    EnableMetroAdjustments = false;
                    MetroAdjustedDefinitionOfLatePhase = 200;
                    break;
                case "CTF":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 4;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 5; // minutes
                    DefinitionOfLatePhaseFromEnd = 5; // minutes
                    break;
                case "Rush":
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 2;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 25; // assuming 75 tickets typical
                    DefinitionOfLatePhaseFromEnd = 25; // assuming 75 tickets typical
                    // Rush only
                    Stage1TicketPercentageToUnstackAdjustment = 5;
                    Stage2TicketPercentageToUnstackAdjustment = 30;
                    Stage3TicketPercentageToUnstackAdjustment = 80;
                    Stage4And5TicketPercentageToUnstackAdjustment = -120;
                    SecondsToCheckForNewStage = 10;
                    break;
                case "Squad Deathmatch":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 0;
                    MaxUnstackingSwapsPerRound = 0;
                    NumberOfSwapsPerGroup = 0;
                    DelaySecondsBetweenSwapGroups = 60;
                    DefinitionOfHighPopulationForPlayers = 14;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 10; // assuming 50 tickets typical
                    DefinitionOfLatePhaseFromEnd = 10; // assuming 50 tickets typical
                    break;
                case "Superiority":
                    MaxPlayers = 24;
                    CheckTeamStackingAfterFirstMinutes = 15;
                    MaxUnstackingSwapsPerRound = 2;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 16;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 50; // assuming 250 tickets typical
                    DefinitionOfLatePhaseFromEnd = 50; // assuming 250 tickets typical
                    break;
                case "Team Deathmatch":
                    MaxPlayers = 64;
                    CheckTeamStackingAfterFirstMinutes = 5;
                    MaxUnstackingSwapsPerRound = 4;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 48;
                    DefinitionOfLowPopulationForPlayers = 16;
                    DefinitionOfEarlyPhaseFromStart = 20; // assuming 100 tickets typical
                    DefinitionOfLatePhaseFromEnd = 20; // assuming 100 tickets typical
                    break;
                case "Squad Rush":
                    MaxPlayers = 8;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    MaxUnstackingSwapsPerRound = 1;
                    NumberOfSwapsPerGroup = 1;
                    DelaySecondsBetweenSwapGroups = 60;
                    DefinitionOfHighPopulationForPlayers = 6;
                    DefinitionOfLowPopulationForPlayers = 4;
                    DefinitionOfEarlyPhaseFromStart = 5; // assuming 20 tickets typical
                    DefinitionOfLatePhaseFromEnd = 5; // assuming 20 tickets typical
                    // Rush only
                    Stage1TicketPercentageToUnstackAdjustment = 5;
                    Stage2TicketPercentageToUnstackAdjustment = 30;
                    Stage3TicketPercentageToUnstackAdjustment = 80;
                    Stage4And5TicketPercentageToUnstackAdjustment = -120;
                    SecondsToCheckForNewStage = 10;
                    break;
                case "Gun Master":
                    MaxPlayers = 16;
                    CheckTeamStackingAfterFirstMinutes = 2;
                    MaxUnstackingSwapsPerRound = 2;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 12;
                    DefinitionOfLowPopulationForPlayers = 6;
                    DefinitionOfEarlyPhaseFromStart = 0;
                    DefinitionOfLatePhaseFromEnd = 0;
                    break;
                case "Unknown or New Mode":
                default:
                    MaxPlayers = 32;
                    CheckTeamStackingAfterFirstMinutes = 10;
                    MaxUnstackingSwapsPerRound = 2;
                    NumberOfSwapsPerGroup = 2;
                    DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                    DefinitionOfHighPopulationForPlayers = 24;
                    DefinitionOfLowPopulationForPlayers = 8;
                    DefinitionOfEarlyPhaseFromStart = 50;
                    DefinitionOfLatePhaseFromEnd = 50;
                    break;
            }
        }
        
        public int MaxPlayers = 64; // will be corrected later
        public double CheckTeamStackingAfterFirstMinutes = 10;
        public int MaxUnstackingSwapsPerRound = 4;
        public double DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
        public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
        public int DefinitionOfHighPopulationForPlayers = 48;
        public int DefinitionOfLowPopulationForPlayers = 16;
        public int DefinitionOfEarlyPhaseFromStart = 50;
        public int DefinitionOfLatePhaseFromEnd = 50;
        public int DisperseEvenlyByRank = 145;
        public bool EnableDisperseEvenlyList = false;
        public double PercentOfTopOfTeamIsStrong = 50;
        public int NumberOfSwapsPerGroup = 2;
        public bool EnableScrambler = false;
        public bool EnableMetroAdjustments = false;
        public int MetroAdjustedDefinitionOfLatePhase = 50;

        // Rush only
        public double Stage1TicketPercentageToUnstackAdjustment = 0;
        public double Stage2TicketPercentageToUnstackAdjustment = 0;
        public double Stage3TicketPercentageToUnstackAdjustment = 0;
        public double Stage4And5TicketPercentageToUnstackAdjustment = 0;
        public double SecondsToCheckForNewStage = 10;
        
        public bool isDefault = true; // not a setting
    } // end PerModeSettings

    public class FetchInfo {
        public FetchState State;
        public DateTime Since;
        public String RequestType;

        public FetchInfo() {
            State = FetchState.New;
            Since = DateTime.Now;
            RequestType = String.Empty;
        }

        public FetchInfo(FetchInfo rhs) {
            this.State = rhs.State;
            this.Since = rhs.Since;
            this.RequestType = rhs.RequestType;
        }
    }

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
        public String SpawnChatMessage;
        public String SpawnYellMessage;
        public bool QuietMessage;
        public MoveInfo DelayedMove;
        public int LastMoveTo;
        public int LastMoveFrom;
        public int ScrambledSquad;
        
        // Battlelog
        public String PersonaId;
        public String Tag;
        public bool TagVerified;
        public String FullName { get {return (String.IsNullOrEmpty(Tag) ? Name : "[" + Tag + "]" + Name);}}
        public FetchInfo TagFetchStatus;
        public double KDR;
        public double SPM;
        public double KPM;
        public bool StatsVerified;
        public FetchInfo StatsFetchStatus;
        
        // Computed
        public double KDRRound;
        public double SPMRound;
        public double KPMRound;
        
        // Accumulated
        public double ScoreTotal; // not including current round
        public double KillsTotal; // not including current round
        public double DeathsTotal; // not including current round
        public int MovesTotal; // not including current round
        public int MovesByMBTotal; // not include current round

        //  Per-round state
        public int MovesRound; // moves NOT made by this plugin
        public int MovesByMBRound; // moves made by this plugin
        public DateTime MovedTimestamp;
        public DateTime MovedByMBTimestamp;

        // Based on settings
        public int DispersalGroup;
        public int Friendex; // index (key) to friend list
        
        public PlayerModel() {
            Name = null;
            Team = -1;
            Squad = -1;
            EAGUID = String.Empty;
            FirstSeenTimestamp = DateTime.Now;
            FirstSpawnTimestamp = DateTime.MinValue;
            LastSeenTimestamp = DateTime.MinValue;
            Tag = String.Empty;
            TagVerified = false;
            ScoreRound = -1;
            KillsRound = -1;
            DeathsRound = -1;
            Rounds = -1;
            Rank = -1;
            KDRRound = -1;
            SPMRound = -1;
            KPMRound = -1;
            ScoreTotal = 0;
            KillsTotal = 0;
            DeathsTotal = 0;
            MovesTotal = 0;
            MovesByMBTotal = 0;
            IsDeployed = false;
            MovesRound = 0;
            MovesByMBRound = 0;
            MovedTimestamp = DateTime.MinValue;
            MovedByMBTimestamp = DateTime.MinValue;
            SpawnChatMessage = String.Empty;
            SpawnYellMessage = String.Empty;
            QuietMessage = false;
            DelayedMove = null;
            LastMoveTo = 0;
            LastMoveFrom = 0;
            TagFetchStatus = new FetchInfo();
            ScrambledSquad = -1;
            DispersalGroup = 0;
            Friendex = -1;
            KDR = -1;
            SPM = -1;
            KPM = -1;
            StatsVerified = false;
            PersonaId = String.Empty;
            StatsFetchStatus = new FetchInfo();
        }
        
        public PlayerModel(String name, int team) : this() {
            Name = name;
            Team = team;
        }

        public void ResetRound() {
            ScoreTotal = ScoreTotal + ScoreRound;
            KillsTotal = KillsTotal + KillsRound;
            DeathsTotal = DeathsTotal + DeathsRound;
            MovesTotal = MovesTotal + MovesRound;
            MovesByMBTotal = MovesByMBTotal + MovesByMBRound;
            Rounds = (Rounds > 0) ? Rounds + 1 : 1;

            ScoreRound = -1;
            KillsRound = -1;
            DeathsRound = -1;
            KDRRound = -1;
            SPMRound = -1;
            KPMRound = -1;
            IsDeployed = false;
            SpawnChatMessage = String.Empty;
            SpawnYellMessage = String.Empty;
            QuietMessage = false;
            DelayedMove = null;
            LastMoveTo = 0;
            LastMoveFrom = 0;

            MovesRound = 0;
            MovesByMBRound = 0;
            DispersalGroup = 0;
            MovedTimestamp = DateTime.MinValue;
            // MovedByMBTimestamp reset when minutes exceeds MinutesAfterBeingMoved
        }

        public PlayerModel ClonePlayer() {
            PlayerModel lhs = new PlayerModel();
            lhs.Name = this.Name;
            lhs.Team = this.Team;
            lhs.Squad = this.Squad;
            lhs.EAGUID = this.EAGUID;
            lhs.FirstSeenTimestamp = this.FirstSeenTimestamp;
            lhs.FirstSpawnTimestamp = this.FirstSpawnTimestamp;
            lhs.LastSeenTimestamp = this.LastSeenTimestamp;
            lhs.Tag = this.Tag;
            lhs.TagVerified = this.TagVerified;
            lhs.ScoreRound = this.ScoreRound;
            lhs.KillsRound = this.KillsRound;
            lhs.DeathsRound = this.DeathsRound;
            lhs.Rounds = this.Rounds;
            lhs.Rank = this.Rank;
            lhs.KDRRound = this.KDRRound;
            lhs.SPMRound = this.SPMRound;
            lhs.KPMRound = this.KPMRound;
            lhs.ScoreTotal = this.ScoreTotal;
            lhs.KillsTotal = this.KillsTotal;
            lhs.DeathsTotal = this.DeathsTotal;
            lhs.MovesTotal = this.MovesTotal;
            lhs.MovesByMBTotal = this.MovesByMBTotal;
            lhs.IsDeployed = this.IsDeployed;
            lhs.MovesRound = this.MovesRound;
            lhs.MovesByMBRound = this.MovesByMBRound;
            lhs.MovedTimestamp = this.MovedTimestamp;
            lhs.MovedByMBTimestamp = this.MovedByMBTimestamp;
            lhs.SpawnChatMessage = this.SpawnChatMessage;
            lhs.SpawnYellMessage = this.SpawnYellMessage;
            lhs.QuietMessage = this.QuietMessage;
            lhs.DelayedMove = this.DelayedMove;
            lhs.LastMoveTo = this.LastMoveTo;
            lhs.LastMoveFrom = this.LastMoveFrom;
            lhs.TagFetchStatus = new FetchInfo(this.TagFetchStatus);
            lhs.ScrambledSquad = this.ScrambledSquad;
            lhs.Friendex = this.Friendex;
            lhs.KDR = this.KDR;
            lhs.SPM = this.SPM;
            lhs.KPM = this.KPM;
            lhs.StatsVerified = this.StatsVerified;
            lhs.PersonaId = this.PersonaId;
            lhs.StatsFetchStatus = new FetchInfo(this.StatsFetchStatus);
            return lhs;
        }
    } // end PlayerModel

    class TeamRoster {
        public int Team = 0; 
        public List<PlayerModel> Roster = null;

        public TeamRoster(int team, List<PlayerModel> roster) {
            Team = team;
            Roster = roster;
        }
    } // end TeamRoster

    public class SquadRoster {
        public int Squad = 0; 
        public double Metric = 0;
        public List<PlayerModel> Roster = null;
        public int ClanTagCount = 0;
        public int DispersalGroup = 0;

        public SquadRoster(int squad) {
            Squad = squad;
            Metric = 0;
            Roster = new List<PlayerModel>();
            ClanTagCount = 0;
            DispersalGroup = 0;
        }

        public SquadRoster(int squad, List<PlayerModel> roster) {
            Squad = squad;
            Roster = roster;
            ClanTagCount = 0;
            DispersalGroup = 0;
        }
    } // end SquadRoster

    public class MoveInfo {
        public MoveType For = MoveType.Balance;
        public ForbidBecause Because = ForbidBecause.None;
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
        public bool aborted = false;

        public MoveInfo() {}

        public MoveInfo(String name, String tag, int fromTeam, String fromName, int toTeam, String toName) : this() {
            Name = name;
            Tag = tag;
            Source = fromTeam;
            SourceName = (String.IsNullOrEmpty(fromName)) ? fromTeam.ToString() : fromName;
            Destination = toTeam;
            DestinationName = (String.IsNullOrEmpty(toName)) ? toTeam.ToString() : toName;
        }

        public void Format(MULTIbalancer plugin, String fmt, bool isYell, bool isBefore) {
            String expanded = fmt;

            if (String.IsNullOrEmpty(expanded)) return;

            String reason = String.Empty;

            if (For == MoveType.Unswitch) {
                switch (this.Because) {
                    case ForbidBecause.MovedByBalancer:
                        reason = plugin.BadBecauseMovedByBalancer;
                        break;
                    case ForbidBecause.DisperseByList:
                        reason = plugin.BadBecauseDispersalList;
                        break;
                    case ForbidBecause.DisperseByRank:
                        reason = plugin.BadBecauseRank;
                        break;
                    case ForbidBecause.ToBiggest:
                        reason = plugin.BadBecauseBiggestTeam;
                        break;
                    case ForbidBecause.ToWinning:
                        reason = plugin.BadBecauseWinningTeam;
                        break;
                    case ForbidBecause.None:
                    default:
                        reason = "(no reason)";
                        break;
                }

                if (expanded.Contains("%reason%")) expanded = expanded.Replace("%reason%", reason);
            }

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
            s += For + ",";
            s += Source + "(" + SourceName + "),";
            s += Destination + "(" + DestinationName + "),";
            s += "CB'" + ChatBefore + "',";
            s += "YB'" + YellBefore + "',";
            s += "CA'" + ChatAfter + "',";
            s += "YA'" + YellAfter + "')";
            return s;
        }
    } // end MoveInfo

    public class DelayedRequest {
        public double MaxDelay; // in seconds
        public DateTime LastUpdate;

        public DelayedRequest() {
            MaxDelay = 0;
            LastUpdate = DateTime.MinValue;
        }

        public DelayedRequest(double delay, DateTime last) {
            MaxDelay = delay;
            LastUpdate = last;
        }
    } // end ListPlayersRequest

    public class PriorityQueue {
        /*
        This class models a prioritized single queue.
        Tag requests are given priority over stats requests.
        When the type of request doesn't matter, such as for Count, the unified value is used.
        When it does matter, such as distinguishing one request from another for Dequeue,
        direct access to the member variable is used.
        */
        public Queue<String> TagQueue; // of player names
        public Queue<String> StatsQueue; // of player names
        private MULTIbalancer fPlugin;

        public PriorityQueue() {
            TagQueue = new Queue<String>();
            StatsQueue = new Queue<String>();
            fPlugin = null;
        }

        public PriorityQueue(MULTIbalancer plugin) : this() {
            fPlugin = plugin;
        }

        public int Count {
            get {return (TagQueue.Count + StatsQueue.Count);}
        }

        public bool Contains(String name) {
            return (TagQueue.Contains(name) || StatsQueue.Contains(name));
        }

        public void Enqueue(String name) {
            if (!TagQueue.Contains(name)) TagQueue.Enqueue(name);
            if (fPlugin.WhichBattlelogStats != BattlelogStats.ClanTagOnly) {
                if (!StatsQueue.Contains(name)) StatsQueue.Enqueue(name);
            }
        }

        public void Clear() {
            TagQueue.Clear();
            StatsQueue.Clear();
        }
    }

#endregion

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
private Thread fFetchThread = null;
private Thread fListPlayersThread = null;
private Thread fScramblerThread = null;
private List<String> fReservedSlots = null;
private bool fGotVersion = false;
private int fServerUptime = -1;
private bool fServerCrashed = false; // because fServerUptime >  fServerInfo.ServerUptime
private DateTime fLastBalancedTimestamp = DateTime.MinValue;
private DateTime fEnabledTimestamp = DateTime.MinValue;
private String fLastMsg = null;
private DateTime fLastVersionCheckTimestamp = DateTime.MinValue;
private double fTimeOutOfJoint = 0;
private List<PlayerModel>[] fDebugScramblerBefore = null;
private List<PlayerModel>[] fDebugScramblerAfter = null;
private DelayedRequest fUpdateThreadLock;
private DateTime fLastServerInfoTimestamp;
private String fHost;
private String fPort;
private List<String> fRushMap3Stages = null;
private List<String> fRushMap5Stages = null;
private int[] fGroupAssignments = null; // index is group number, value is team id
private List<String>[] fDispersalGroups;
private bool fNeedGroupAssignment = false;

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
private Queue<DelayedRequest> fListPlayersQ = null;
private Dictionary<String,String> fFriendlyMaps = null;
private Dictionary<String,String> fFriendlyModes = null;
private double fMaxTickets = -1;
private double fRushMaxTickets = -1; // not normalized
private List<TeamScore> fFinalStatus = null;
private bool fIsFullRound = false;
private UnstackState fUnstackState = UnstackState.Off;
private DateTime fFullUnstackSwapTimestamp;
private int fRushStage = 0;
private double fRushPrevAttackerTickets = 0;
private double fRushAttackerStageLoss = 0;
private double fRushAttackerStageSamples = 0;
private List<MoveInfo> fMoveStash = null;
private int fUnstackGroupCount = 0;
private PriorityQueue fPriorityFetchQ = null;
private bool fIsCacheEnabled = false;
private DelayedRequest fScramblerLock = null;
private int fWinner = 0;
private bool fStageInProgress = false;
private Dictionary<int,List<String>> fFriends;
private List<String> fAllFriends;

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
private int fRoundsEnabled = 0;
private int fGrandTotalQuits = 0;
private int fGrandRageQuits = 0;
private int fTotalQuits = 0;
private int fRageQuits = 0;

// Settings support
private Dictionary<int, Type> fEasyTypeDict = null;
private Dictionary<int, Type> fBoolDict = null;
private Dictionary<int, Type> fListStrDict = null;
private Dictionary<String,PerModeSettings> fPerMode = null;

// Settings
public int SettingsVersion;
public PresetItems Preset;
public bool EnableUnstacking;
public bool EnableSettingsWizard;
public String WhichMode;
public bool MetroIsInMapRotation;
public int MaximumPlayersForMode;
public int LowestMaximumTicketsForMode;
public int HighestMaximumTicketsForMode;
public PresetItems PreferredStyleOfBalancing;
public bool ApplySettingsChanges;

public int DebugLevel;
public int MaximumServerSize;
public bool EnableBattlelogRequests;
public int MaximumRequestRate;
public double WaitTimeout;
public BattlelogStats WhichBattlelogStats;
public int MaxTeamSwitchesByStrongPlayers; // disabled
public int MaxTeamSwitchesByWeakPlayers; // disabled
public double UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
public bool Enable2SlotReserve; // disabled
public bool EnablerecruitCommand; // disabled
public bool EnableWhitelistingOfReservedSlotsList;
public String[] Whitelist;
public List<String> fSettingWhitelist;
public String[] DisperseEvenlyList;
public List<String> fSettingDisperseEvenlyList;
public String[] FriendsList;
public List<String> fSettingFriendsList;
public double SecondsUntilAdaptiveSpeedBecomesFast;

public bool OnWhitelist;
public bool OnFriendsList;
public bool TopScorers;
public bool SameClanTagsInSquad;
public bool SameClanTagsForRankDispersal;
public double MinutesAfterJoining;
public double MinutesAfterBeingMoved;
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

public bool OnlyOnNewMaps; // false means scramble every round
public double OnlyOnFinalTicketPercentage; // 0 means scramble regardless of final score
public DefineStrong ScrambleBy;
public bool KeepSquadsTogether;
public bool KeepClanTagsInSameSquad;
public DivideByChoices DivideBy;
public String ClanTagToDivideBy;
public double DelaySeconds;

public bool QuietMode;
public double YellDurationSeconds;
public String BadBecauseMovedByBalancer;
public String BadBecauseWinningTeam;
public String BadBecauseBiggestTeam;
public String BadBecauseRank;
public String BadBecauseDispersalList;
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
public String ShowInLog; // legacy variable, if defined as String.Empty, settings are pre-v1
public String ShowCommandInLog; // command line to show info in plugin.log
public bool LogChat;
public bool EnableLoggingOnlyMode;
public bool EnableExternalLogging;
public String ExternalLogSuffix; 

public bool EnableImmediateUnswitch;
public bool ForbidSwitchAfterAutobalance; // legacy pre-v1
public bool ForbidSwitchToWinningTeam; // legacy pre-v1
public bool ForbidSwitchToBiggestTeam; // legacy pre-v1
public bool ForbidSwitchAfterDispersal; // legacy pre-v1
public UnswitchChoice ForbidSwitchingAfterAutobalance;
public UnswitchChoice ForbidSwitchingToWinningTeam;
public UnswitchChoice ForbidSwitchingToBiggestTeam;
public UnswitchChoice ForbidSwitchingAfterDispersal;

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

public MULTIbalancer() {
    /* Private members */
    fIsEnabled = false;
    fFinalizerActive = false;
    fPluginState = PluginState.Disabled;
    fGameState = GameState.Unknown;
    fServerInfo = null;
    fGotVersion = false;
    fServerUptime = 0;
    fServerCrashed = false;
    fDebugScramblerBefore = new List<PlayerModel>[2]{new List<PlayerModel>(), new List<PlayerModel>()};
    fDebugScramblerAfter = new List<PlayerModel>[2]{new List<PlayerModel>(), new List<PlayerModel>()};

    fBalancedRound = 0;
    fUnstackedRound = 0;
    fUnswitchedRound = 0;
    fExcludedRound = 0;
    fExemptRound = 0;
    fFailedRound = 0;
    fTotalRound = 0;
    fBalanceIsActive = false;
    fRoundsEnabled = 0;
    fGrandTotalQuits = 0;
    fGrandRageQuits = 0;
    fTotalQuits = 0;
    fRageQuits = 0;

    fMoveThread = null;
    fFetchThread = null;
    fListPlayersThread = null;
    fScramblerThread = null;
    
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
    fListStrDict.Add(0, typeof(String[]));
    
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
    fListPlayersQ = new Queue<DelayedRequest>();

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
    fRushPrevAttackerTickets = 0;
    fRushAttackerStageLoss = 0;
    fRushAttackerStageSamples = 0;
    fMoveStash = new List<MoveInfo>();
    fLastVersionCheckTimestamp = DateTime.MinValue;
    fTimeOutOfJoint = 0;
    fUnstackGroupCount = 0;
    fPriorityFetchQ = new PriorityQueue(this);
    fIsCacheEnabled = false;
    fScramblerLock = new DelayedRequest();
    fWinner = 0;
    fUpdateThreadLock = new DelayedRequest();
    fLastServerInfoTimestamp = DateTime.Now;
    fStageInProgress = false;
    fHost = String.Empty;
    fPort = String.Empty;
    fRushMap3Stages = new List<String>(new String[5]{"MP_007", "XP4_Quake", "XP5_002", "MP_012", "XP4_Rubble"});
    fRushMap5Stages = new List<String>(new String[4]{"MP_013", "XP3_Valley", "MP_017", "XP5_001"});
    fGroupAssignments = new int[5]{0,0,0,0,0};
    fDispersalGroups = new List<String>[5]{null, new List<String>(), new List<String>(), new List<String>(), new List<String>()};
    fNeedGroupAssignment = false;
    fFriends = new Dictionary<int, List<String>>();
    fAllFriends = new List<String>();
    
    /* Settings */

    /* ===== SECTION 0 - Presets ===== */

    SettingsVersion = 1;
    Preset = PresetItems.Standard;
    EnableUnstacking = false;
    EnableSettingsWizard = false;
    WhichMode = "Conquest Large";
    MetroIsInMapRotation = false;
    MaximumPlayersForMode = 64;
    LowestMaximumTicketsForMode = 300;
    HighestMaximumTicketsForMode = 400;
    PreferredStyleOfBalancing = PresetItems.Standard;
    ApplySettingsChanges = false;

    /* ===== SECTION 1 - Settings ===== */

    DebugLevel = 2;
    MaximumServerSize = 64;
    EnableBattlelogRequests = true;
    MaximumRequestRate = 10; // in 20 seconds
    WaitTimeout = 30; // seconds
    WhichBattlelogStats = BattlelogStats.ClanTagOnly;
    MaxTeamSwitchesByStrongPlayers = 1;
    MaxTeamSwitchesByWeakPlayers = 2;
    UnlimitedTeamSwitchingDuringFirstMinutesOfRound = 5.0;
    Enable2SlotReserve = false;
    EnablerecruitCommand = false;
    EnableWhitelistingOfReservedSlotsList = true;
    Whitelist = new String[] {DEFAULT_LIST_ITEM};
    fSettingWhitelist = new List<String>(Whitelist);
    DisperseEvenlyList = new String[] {DEFAULT_LIST_ITEM};
    fSettingDisperseEvenlyList = new List<String>(DisperseEvenlyList);
    FriendsList = new String[] {DEFAULT_LIST_ITEM};
    fSettingFriendsList = new List<String>();
    SecondsUntilAdaptiveSpeedBecomesFast = 3*60; // 3 minutes default
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = true;
    OnFriendsList = false;
    TopScorers = true;
    SameClanTagsInSquad = true;
    SameClanTagsForRankDispersal = false;
    MinutesAfterJoining = 5;
    MinutesAfterBeingMoved = 90; // 1.5 hours
    JoinedEarlyPhase = true;
    JoinedMidPhase = true;
    JoinedLatePhase = false;


    /* ===== SECTION 3 - Round Phase & Population Settings ===== */

    EarlyPhaseTicketPercentageToUnstack = new double[3]         {  0,120,120};
    MidPhaseTicketPercentageToUnstack = new double[3]           {  0,120,120};
    LatePhaseTicketPercentageToUnstack = new double[3]          {  0,  0,  0};
    
    SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;

    EarlyPhaseBalanceSpeed = new Speed[3]           {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
    MidPhaseBalanceSpeed = new Speed[3]             {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
    LatePhaseBalanceSpeed = new Speed[3]            {     Speed.Stop,     Speed.Stop,     Speed.Stop};
    
    /* ===== SECTION 4 - Scrambler ===== */

    OnlyOnNewMaps = true; // false means scramble every round
    OnlyOnFinalTicketPercentage = 120; // 0 means scramble regardless of final score
    ScrambleBy = DefineStrong.RoundScore;
    KeepSquadsTogether = true;
    KeepClanTagsInSameSquad = true;
    DivideBy = DivideByChoices.None;
    ClanTagToDivideBy = String.Empty;
    DelaySeconds = 30;

    /* ===== SECTION 5 - Messages ===== */
    
    QuietMode = false; // false: chat is global, true: chat is private. Yells are always private
    YellDurationSeconds = 10;
    BadBecauseMovedByBalancer = "autobalance moved you to the %toTeam% team";
    BadBecauseWinningTeam = "switching to the winning team is not allowed";
    BadBecauseBiggestTeam = "switching to the biggest team is not allowed";
    BadBecauseRank = "this server splits Colonel 100's between teams";
    BadBecauseDispersalList = "you're on the list of players to split between teams";
    ChatMovedForBalance = "*** MOVED %name% for balance ...";
    YellMovedForBalance = "Moved %name% for balance ...";
    ChatMovedToUnstack = "*** MOVED %name% to unstack teams ...";
    YellMovedToUnstack = "Moved %name% to unstack teams ...";
    ChatDetectedBadTeamSwitch = "%name%, you can't switch to team %fromTeam%: %reason%, sending you back ...";
    YellDetectedBadTeamSwitch = "You can't switch to the %fromTeam% team: %reason%, sending you back!";
    ChatDetectedGoodTeamSwitch = "%name%, thanks for helping out the %toTeam% team!";
    YellDetectedGoodTeamSwitch = "Thanks for helping out the %toTeam% team!";
    ChatAfterUnswitching = "%name%, please stay on the %toTeam% team for the rest of this round";
    YellAfterUnswitching = "Please stay on the %toTeam% team for the rest of this round";
    
    /* ===== SECTION 6 - Unswitcher ===== */

    EnableImmediateUnswitch = true;
    ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
    ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
    ForbidSwitchingAfterDispersal = UnswitchChoice.Always;

    /* ===== SECTION 7 - TBD ===== */

    /* ===== SECTION 8 - Per-Mode Settings ===== */

    /* ===== SECTION 9 - Debug Settings ===== */

    ShowInLog = INVALID_NAME_TAG_GUID;
    ShowCommandInLog = String.Empty;
    LogChat = true;
    EnableLoggingOnlyMode = false;
    EnableExternalLogging = false;
    ExternalLogSuffix = "_mb.log";
}

public MULTIbalancer(PresetItems preset) : this() {
    switch (preset) {
        case PresetItems.Standard:
         // EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,120,120};
         // MidPhaseTicketPercentageToUnstack = new double[3]       {  0,120,120};
         // LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};
         // EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
         // MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
         // LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
         // ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
         // ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
         // ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
         // ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
         // EnableImmediateUnswitch = true;
         // 
         // foreach (String mode in fPerMode.Keys) {
         //      fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
         // }
            break;

        case PresetItems.Aggressive:

            OnWhitelist = true;
            OnFriendsList = false;
            TopScorers = false;
            SameClanTagsInSquad = false;
            SameClanTagsForRankDispersal = false;
            MinutesAfterJoining = 0;
            MinutesAfterBeingMoved = 0;
            JoinedEarlyPhase = false;
            JoinedMidPhase = false;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {110,110,110};
            MidPhaseTicketPercentageToUnstack = new double[3]       {110,110,110};
            LatePhaseTicketPercentageToUnstack = new double[3]      {110,110,110};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Fast,     Speed.Fast,     Speed.Fast};

            ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
            EnableImmediateUnswitch = true;

            // Does not count for automatic detection of preset
            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
            }
            
            break;

        case PresetItems.Passive:

            OnWhitelist = true;
            OnFriendsList = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            SameClanTagsForRankDispersal = true;
            MinutesAfterJoining = 15;
            MinutesAfterBeingMoved = 12*60; // 12 hours
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = true;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,200};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,200,200};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow,     Speed.Slow,     Speed.Slow};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow,     Speed.Slow,     Speed.Slow};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};

            ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
            EnableImmediateUnswitch = false;

            // Does not count for automatic detection of preset
            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
            }
            
            break;

        case PresetItems.Intensify:

            OnWhitelist = true;
            OnFriendsList = false;
            TopScorers = true;
            SameClanTagsInSquad = false;
            SameClanTagsForRankDispersal = false;
            MinutesAfterJoining = 0;
            MinutesAfterBeingMoved = 0;
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

            ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
            EnableImmediateUnswitch = true;

            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 25;
            }
            
            break;

        case PresetItems.Retain:

            OnWhitelist = true;
            OnFriendsList = true;
            TopScorers = true;
            SameClanTagsInSquad = true;
            SameClanTagsForRankDispersal = true;
            MinutesAfterJoining = 15;
            MinutesAfterBeingMoved = 2*60; // 2 hours
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = true;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,150};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,150,200};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};

            ForbidSwitchingAfterAutobalance = UnswitchChoice.Never;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
            EnableImmediateUnswitch = true;

            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 5;
            }
            
            break;

        case PresetItems.BalanceOnly:

            OnWhitelist = true;
            OnFriendsList = false;
            TopScorers = true;
            SameClanTagsInSquad = true;
            SameClanTagsForRankDispersal = true;
            MinutesAfterJoining = 5;
            MinutesAfterBeingMoved = 90;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,  0,  0};
            MidPhaseTicketPercentageToUnstack = new double[3]       {  0,  0,  0};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};

            ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Never;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
            EnableImmediateUnswitch = true;

            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 0;
            }
            
            break;

        case PresetItems.UnstackOnly:

            OnWhitelist = true;
            OnFriendsList = false;
            TopScorers = true;
            SameClanTagsInSquad = true;
            SameClanTagsForRankDispersal = true;
            MinutesAfterJoining = 5;
            MinutesAfterBeingMoved = 90;
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = false;

            EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,120,120};
            MidPhaseTicketPercentageToUnstack = new double[3]       {120,120,120};
            LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};

            EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};
            MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};
            LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};


            ForbidSwitchingAfterAutobalance = UnswitchChoice.Never;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
            EnableImmediateUnswitch = true;

            // Does not count for automatic detection of preset
            foreach (String mode in fPerMode.Keys) {
                fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
            }
            
            break;

        case PresetItems.None:
            break;
        default:
            break;
    }
}


public String GetPluginName() {
    return "MULTIbalancer";
}

public String GetPluginVersion() {
    return "1.0.2.8";
}

public String GetPluginAuthor() {
    return "PapaCharlie9";
}

public String GetPluginWebsite() {
    return "https://github.com/PapaCharlie9/multi-balancer";
}

public String GetPluginDescription() {
    return MULTIbalancerUtils.HTML_DOC;
}









/* ======================== SETTINGS ============================= */









public List<CPluginVariable> GetDisplayPluginVariables() {


    List<CPluginVariable> lstReturn = new List<CPluginVariable>();

    try {
        List<String> simpleModes = GetSimplifiedModes();

        /* ===== SECTION 0 - Presets ===== */
        
        UpdatePresetValue();

        String var_name = "0 - Presets|Use Round Phase, Population and Exclusions preset ";
        String var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), Preset)));

        lstReturn.Add(new CPluginVariable("0 - Presets|Enable Unstacking", EnableUnstacking.GetType(), EnableUnstacking));
        
        lstReturn.Add(new CPluginVariable("0 - Presets|Enable Settings Wizard", EnableSettingsWizard.GetType(), EnableSettingsWizard));

        if (EnableSettingsWizard) {
            List<String> enumModes = new List<String>();
            enumModes.Add("Conq Small or Dom or Scav");
            foreach (String sm in simpleModes) {
                if (!sm.Contains("Conq Small")) {
                    enumModes.Add(sm);
                }
            }
            var_name = "0 - Presets|Which Mode";
            var_type = "enum." + var_name + "(" + String.Join("|", enumModes.ToArray()) + ")";

            lstReturn.Add(new CPluginVariable(var_name, var_type, WhichMode));

            lstReturn.Add(new CPluginVariable("0 - Presets|Metro Is In Map Rotation", MetroIsInMapRotation.GetType(), MetroIsInMapRotation));

            lstReturn.Add(new CPluginVariable("0 - Presets|Maximum Players For Mode", MaximumPlayersForMode.GetType(), MaximumPlayersForMode));

            lstReturn.Add(new CPluginVariable("0 - Presets|Lowest Maximum Tickets For Mode", LowestMaximumTicketsForMode.GetType(), LowestMaximumTicketsForMode));

            lstReturn.Add(new CPluginVariable("0 - Presets|Highest Maximum Tickets For Mode", HighestMaximumTicketsForMode.GetType(), HighestMaximumTicketsForMode));

            var_name = "0 - Presets|Preferred Style Of Balancing";
            var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";
        
            lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), PreferredStyleOfBalancing)));

            lstReturn.Add(new CPluginVariable("0 - Presets|Apply Settings Changes", ApplySettingsChanges.GetType(), ApplySettingsChanges));
        }
        
        /* ===== SECTION 1 - Settings ===== */
        
        lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));

        lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Server Size", MaximumServerSize.GetType(), MaximumServerSize));

        lstReturn.Add(new CPluginVariable("1 - Settings|Enable Battlelog Requests", EnableBattlelogRequests.GetType(), EnableBattlelogRequests));

        if (EnableBattlelogRequests) {
            var_name = "1 - Settings|Which Battlelog Stats";
            var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BattlelogStats))) + ")";
        
            lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BattlelogStats), WhichBattlelogStats)));

            lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Request Rate", MaximumRequestRate.GetType(), MaximumRequestRate));

            lstReturn.Add(new CPluginVariable("1 - Settings|Wait Timeout", WaitTimeout.GetType(), WaitTimeout));
        }

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

        lstReturn.Add(new CPluginVariable("1 - Settings|Friends List", FriendsList.GetType(), FriendsList));

        lstReturn.Add(new CPluginVariable("1 - Settings|Disperse Evenly List", DisperseEvenlyList.GetType(), DisperseEvenlyList));
        
        /* ===== SECTION 2 - Exclusions ===== */

        lstReturn.Add(new CPluginVariable("2 - Exclusions|On Whitelist", OnWhitelist.GetType(), OnWhitelist));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|On Friends List", OnFriendsList.GetType(), OnFriendsList));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Top Scorers", TopScorers.GetType(), TopScorers));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Squad", SameClanTagsInSquad.GetType(), SameClanTagsInSquad));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags For Rank Dispersal", SameClanTagsForRankDispersal.GetType(), SameClanTagsForRankDispersal)); 

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Joining", MinutesAfterJoining.GetType(), MinutesAfterJoining));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Being Moved", MinutesAfterBeingMoved.GetType(), MinutesAfterBeingMoved));

        /*
        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Early Phase", JoinedEarlyPhase.GetType(), JoinedEarlyPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Mid Phase", JoinedMidPhase.GetType(), JoinedMidPhase));

        lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Late Phase", JoinedLatePhase.GetType(), JoinedLatePhase));
        */

        /* ===== SECTION 3 - Round Phase & Population Setttings ===== */
        
        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(EarlyPhaseTicketPercentageToUnstack)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(MidPhaseTicketPercentageToUnstack)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(LatePhaseTicketPercentageToUnstack)));
        
        var_name = "3 - Round Phase and Population Settings|Spelling Of Speed Names Reminder";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(Speed))) + ")";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(Speed), SpellingOfSpeedNamesReminder)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(EarlyPhaseBalanceSpeed)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(MidPhaseBalanceSpeed)));

        lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(LatePhaseBalanceSpeed)));

        /* ===== SECTION 4 - Scrambler ===== */

        lstReturn.Add(new CPluginVariable("4 - Scrambler|Only On New Maps", OnlyOnNewMaps.GetType(), OnlyOnNewMaps));

        lstReturn.Add(new CPluginVariable("4 - Scrambler|Only On Final Ticket Percentage >=", OnlyOnFinalTicketPercentage.GetType(), OnlyOnFinalTicketPercentage));

        var_name = "4 - Scrambler|Scramble By";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), ScrambleBy)));

        lstReturn.Add(new CPluginVariable("4 - Scrambler|Keep Squads Together", KeepSquadsTogether.GetType(), KeepSquadsTogether));

        if (!KeepSquadsTogether) {
            lstReturn.Add(new CPluginVariable("4 - Scrambler|Keep Clan Tags In Same Squad", KeepClanTagsInSameSquad.GetType(), KeepClanTagsInSameSquad));
        }

        var_name = "4 - Scrambler|Divide By";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DivideByChoices))) + ")";
        
        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DivideByChoices), DivideBy)));

        if (DivideBy == DivideByChoices.ClanTag) {
            lstReturn.Add(new CPluginVariable("4 - Scrambler|Clan Tag To Divide By", ClanTagToDivideBy.GetType(), ClanTagToDivideBy));
        }

        lstReturn.Add(new CPluginVariable("4 - Scrambler|Delay Seconds", DelaySeconds.GetType(), DelaySeconds));

        /* ===== SECTION 5 - Messages ===== */
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Quiet Mode", QuietMode.GetType(), QuietMode));

        lstReturn.Add(new CPluginVariable("5 - Messages|Yell Duration Seconds", YellDurationSeconds.GetType(), YellDurationSeconds));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved For Balance", ChatMovedForBalance.GetType(), ChatMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved For Balance", YellMovedForBalance.GetType(), YellMovedForBalance));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved To Unstack", ChatMovedToUnstack.GetType(), ChatMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved To Unstack", YellMovedToUnstack.GetType(), YellMovedToUnstack));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Bad Team Switch", ChatDetectedBadTeamSwitch.GetType(), ChatDetectedBadTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Bad Team Switch", YellDetectedBadTeamSwitch.GetType(), YellDetectedBadTeamSwitch));

        lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Moved By Balancer", BadBecauseMovedByBalancer.GetType(), BadBecauseMovedByBalancer));

        lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Winning Team", BadBecauseWinningTeam.GetType(), BadBecauseWinningTeam));

        lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Biggest Team", BadBecauseBiggestTeam.GetType(), BadBecauseBiggestTeam));

        lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Rank", BadBecauseRank.GetType(), BadBecauseRank));

        lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Dispersal List", BadBecauseDispersalList.GetType(), BadBecauseDispersalList));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Good Team Switch", ChatDetectedGoodTeamSwitch.GetType(), ChatDetectedGoodTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Good Team Switch", YellDetectedGoodTeamSwitch.GetType(), YellDetectedGoodTeamSwitch));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Chat: After Unswitching", ChatAfterUnswitching.GetType(), ChatAfterUnswitching));
        
        lstReturn.Add(new CPluginVariable("5 - Messages|Yell: After Unswitching", YellAfterUnswitching.GetType(), YellAfterUnswitching));


        /* ===== SECTION 6 - Unswitcher ===== */

        var_name = "6 - Unswitcher|Forbid Switching After Autobalance";
        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(UnswitchChoice))) + ")";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingAfterAutobalance)));

        var_name = "6 - Unswitcher|Forbid Switching To Winning Team";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingToWinningTeam))); 

        var_name = "6 - Unswitcher|Forbid Switching To Biggest Team";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingToBiggestTeam))); 

        var_name = "6 - Unswitcher|Forbid Switching After Dispersal";

        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingAfterDispersal))); 

        lstReturn.Add(new CPluginVariable("6 - Unswitcher|Enable Immediate Unswitch", EnableImmediateUnswitch.GetType(), EnableImmediateUnswitch)); 

        /* ===== SECTION 7 - TBD ===== */

        /* ===== SECTION 8 - Per-Mode Settings ===== */

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
            bool isRush = (sm.Contains("Rush"));
            bool isSQDM = (sm == "Squad Deathmatch");
            bool isConquest = (sm.Contains("Conq"));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

            if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Check Team Stacking After First Minutes", oneSet.CheckTeamStackingAfterFirstMinutes.GetType(), oneSet.CheckTeamStackingAfterFirstMinutes));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Unstacking Swaps Per Round", oneSet.MaxUnstackingSwapsPerRound.GetType(), oneSet.MaxUnstackingSwapsPerRound));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Number Of Swaps Per Group", oneSet.NumberOfSwapsPerGroup.GetType(), oneSet.NumberOfSwapsPerGroup));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Delay Seconds Between Swap Groups", oneSet.DelaySecondsBetweenSwapGroups.GetType(), oneSet.DelaySecondsBetweenSwapGroups));

                var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Percent Of Top Of Team Is Strong", oneSet.PercentOfTopOfTeamIsStrong.GetType(), oneSet.PercentOfTopOfTeamIsStrong));

            }

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Disperse Evenly By Rank >=", oneSet.DisperseEvenlyByRank.GetType(), oneSet.DisperseEvenlyByRank));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Disperse Evenly List", oneSet.EnableDisperseEvenlyList.GetType(), oneSet.EnableDisperseEvenlyList));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population For Players >=", oneSet.DefinitionOfHighPopulationForPlayers.GetType(), oneSet.DefinitionOfHighPopulationForPlayers));

            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population For Players <=", oneSet.DefinitionOfLowPopulationForPlayers.GetType(), oneSet.DefinitionOfLowPopulationForPlayers));

            if (isCTF) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Minutes From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Minutes From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
            } else if (!isGM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Tickets From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Tickets From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
            }

            if (!isSQDM) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Scrambler", oneSet.EnableScrambler.GetType(), oneSet.EnableScrambler));
            }

            if (isRush) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 1 Ticket Percentage To Unstack Adjustment", oneSet.Stage1TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage1TicketPercentageToUnstackAdjustment));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 2 Ticket Percentage To Unstack Adjustment", oneSet.Stage2TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage2TicketPercentageToUnstackAdjustment));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 3 Ticket Percentage To Unstack Adjustment", oneSet.Stage3TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage3TicketPercentageToUnstackAdjustment));

                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 4 And 5 Ticket Percentage To Unstack Adjustment", oneSet.Stage4And5TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage4And5TicketPercentageToUnstackAdjustment));
                
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Seconds To Check For New Stage", oneSet.SecondsToCheckForNewStage.GetType(), oneSet.SecondsToCheckForNewStage));
            }

            if (isConquest) {
                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Metro Adjustments", oneSet.EnableMetroAdjustments.GetType(), oneSet.EnableMetroAdjustments));

                if (oneSet.EnableMetroAdjustments) {
                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Metro Adjusted Definition Of Late Phase", oneSet.MetroAdjustedDefinitionOfLatePhase.GetType(), oneSet.MetroAdjustedDefinitionOfLatePhase));
                }
            }

        }

        /* ===== SECTION 9 - Debug Settings ===== */

        lstReturn.Add(new CPluginVariable("9 - Debugging|Show Command In Log", ShowCommandInLog.GetType(), ShowCommandInLog));

        lstReturn.Add(new CPluginVariable("9 - Debugging|Log Chat", LogChat.GetType(), LogChat));

        lstReturn.Add(new CPluginVariable("9 - Debugging|Enable Logging Only Mode", EnableLoggingOnlyMode.GetType(), EnableLoggingOnlyMode));

        lstReturn.Add(new CPluginVariable("9 - Debugging|Enable External Logging", EnableExternalLogging.GetType(), EnableExternalLogging));

        if (EnableExternalLogging) {

            lstReturn.Add(new CPluginVariable("9 - Debugging|External Log Suffix", ExternalLogSuffix.GetType(), ExternalLogSuffix));

        }


    } catch (Exception e) {
        ConsoleException(e);
    }

    return lstReturn;
}

public List<CPluginVariable> GetPluginVariables() {
    List<CPluginVariable> lstReturn = GetDisplayPluginVariables();
    // pre-v1 legacy settings
    lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch After Autobalance", ForbidSwitchAfterAutobalance.GetType(), ForbidSwitchAfterAutobalance));
    lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch To Winning Team", ForbidSwitchToWinningTeam.GetType(), ForbidSwitchToWinningTeam));
    lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch To Biggest Team", ForbidSwitchToBiggestTeam.GetType(), ForbidSwitchToBiggestTeam));
    lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch After Dispersal", ForbidSwitchAfterDispersal.GetType(), ForbidSwitchAfterDispersal)); 
    lstReturn.Add(new CPluginVariable("9 - Debugging|Show In Log", ShowInLog.GetType(), ShowInLog));
    // hidden setting
    lstReturn.Add(new CPluginVariable("0 - Presets|Settings Version", SettingsVersion.GetType(), SettingsVersion));
    return lstReturn;
}

public void SetPluginVariable(String strVariable, String strValue) {
    bool isPresetVar = false;
    bool isReminderVar = false;

    if (fIsEnabled) DebugWrite(strVariable + " <- " + strValue, 6);

    try {
        if (strVariable.Contains("Show In Log") && String.IsNullOrEmpty(strValue)) {
            DebugWrite("^8Detected pre-v1 settings, upgrading ...", 3);
            UpgradePreV1Settings();
            strValue = INVALID_NAME_TAG_GUID; // mark as upgraded
        } else if (strVariable.Contains("Settings Version")) {
            DebugWrite("^1Settings Version = " + strValue, 3);
        }
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
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Spelling Of Speed Names Reminder")) {
                fieldType = typeof(Speed);
                try {
                    field.SetValue(this, (Speed)Enum.Parse(fieldType, strValue));
                    isReminderVar = true;
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Balance Speed")) {
                fieldType = typeof(Speed[]);
                try {
                    // Parse the list into an array of enum vals
                    Speed[] items = MULTIbalancerUtils.ParseSpeedArray(this, strValue); // also validates
                    field.SetValue(this, items);
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Ticket Percentage To Unstack")) {
                fieldType = typeof(double[]);
                try {
                    // Parse the list into an array of numbers
                    double[] nums = MULTIbalancerUtils.ParseNumArray(strValue); // also validates
                    field.SetValue(this, nums);
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Scramble By")) {
                fieldType = typeof(DefineStrong);
                try {
                    field.SetValue(this, (DefineStrong)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp == "Divide By") {
                fieldType = typeof(DivideByChoices);
                try {
                    field.SetValue(this, (DivideByChoices)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Preferred Style Of Balancing")) {
                fieldType = typeof(PresetItems);
                try {
                    field.SetValue(this, (PresetItems)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Forbid Switching")) {
                fieldType = typeof(UnswitchChoice);
                try {
                    field.SetValue(this, (UnswitchChoice)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (tmp.Contains("Which Battlelog Stats")) {
                fieldType = typeof(BattlelogStats);
                try {
                    field.SetValue(this, (BattlelogStats)Enum.Parse(fieldType, strValue));
                } catch (Exception e) {
                    ConsoleException(e);
                }
            } else if (fEasyTypeDict.ContainsValue(fieldType)) {
                field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
            } else if (fListStrDict.ContainsValue(fieldType)) {
                if (DebugLevel >= 8) ConsoleDebug("String array " + propertyName + " <- " + strValue);
                field.SetValue(this, CPluginVariable.DecodeStringArray(strValue));
                if (propertyName == "Whitelist") {
                    MergeWithFile(Whitelist, fSettingWhitelist);
                    if (DebugLevel >= 8) {
                        String l = "Whitelist: ";
                        l = l + String.Join(", ", fSettingWhitelist.ToArray());
                        ConsoleDebug(l);
                    }
                } else if (propertyName == "DisperseEvenlyList") {
                    MergeWithFile(DisperseEvenlyList, fSettingDisperseEvenlyList);
                    SetDispersalListGroups();
                    AssignGroups();
                } else if (propertyName == "FriendsList") {
                    MergeWithFile(FriendsList, fSettingFriendsList);
                    SetFriends();
                }
            } else if (fBoolDict.ContainsValue(fieldType)) {
                if (fIsEnabled) DebugWrite(propertyName + " strValue = " + strValue, 6);
                if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success) {
                    field.SetValue(this, true);
                } else {
                    field.SetValue(this, false);
                }
            } else {
                if (DebugLevel >= 8) ConsoleDebug("Unknown var " + propertyName + " with type " + fieldType);
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
                
                if (fIsEnabled) DebugWrite("Mode: " + mode + ", Field: " + perModeSetting + ", Value: " + strValue, 6);
                
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
                    } else if (strVariable.Contains("Determine Strong")) {
                        fieldType = typeof(DefineStrong);
                        try {
                            field.SetValue(pms, (DefineStrong)Enum.Parse(fieldType, strValue));
                        } catch (Exception e) {
                            ConsoleException(e);
                        }
                    }
                } else {
                    if (fIsEnabled) DebugWrite("field is null", 6);
                }
            }
        }
    } catch (System.Exception e) {
        ConsoleException(e);
    } finally {
        
        if (!isReminderVar) {
            // Reset to show hint
            SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;
        }
        
        if (isPresetVar) {
            // Update other settings based on new preset value
            MULTIbalancerUtils.UpdateSettingsForPreset(this, Preset);
        } else {
            // Update Preset value based on current settings
            UpdatePresetValue();
        }

        if (strVariable.Contains("Apply Settings Changes") && ApplySettingsChanges) {
            ApplySettingsChanges = false;
            EnableSettingsWizard = false;
            ApplyWizardSettings();
        }
        
        // Validate all values and correct if needed
        ValidateSettings(strVariable,  strValue);

        // Handle show in log commands
        if (!String.IsNullOrEmpty(ShowCommandInLog)) {
            CommandToLog(ShowCommandInLog);
            ShowCommandInLog = String.Empty;
        }
    }
}



private bool ValidateSettings(String strVariable, String strValue) {
    try {
                
        /* ===== SECTION 1 - Settings ===== */

        if (strVariable.Contains("Debug Level")) {ValidateIntRange(ref DebugLevel, "Debug Level", 0, 9, 2, false);}
        else if (strVariable.Contains("Maximum Server Size")) {ValidateIntRange(ref MaximumServerSize, "Maximum Server Size", 8, 64, 64, false);}
        else if (strVariable.Contains("Maximum Request Rate")) {ValidateIntRange(ref MaximumRequestRate, "Maximum Request Rate", 1, 15, 10, true);} // in 20 seconds
        else if (strVariable.Contains("Wait Timeout")) {ValidateDoubleRange(ref WaitTimeout, "Wait Timeout", 15, 90, 30, false);}
        else if (strVariable.Contains("Unlimited Team Switching During First Minutes Of Round")) {ValidateDouble(ref UnlimitedTeamSwitchingDuringFirstMinutesOfRound, "Unlimited Team Switching During First Minutes Of Round", 5.0);}
        else if (strVariable.Contains("Seconds Until Adaptive Speed Becomes Fast")) {ValidateDoubleRange(ref SecondsUntilAdaptiveSpeedBecomesFast, "Seconds Until Adaptive Speed Becomes Fast", MIN_ADAPT_FAST, 999999, 3*60, true);} // 3 minutes default
    
        /* ===== SECTION 2 - Exclusions ===== */
    
        else if (strVariable.Contains("Minutes After Joining")) {ValidateDouble(ref MinutesAfterJoining, "Minutes After Joining", 5);}
        else if (strVariable.Contains("Minutes After Being Moved")) {ValidateDouble(ref MinutesAfterBeingMoved, "Minutes After Being Moved", 5);}

        /* ===== SECTION 3 - Round Phase & Population Settings ===== */
    
        for (int i = 0; i < EarlyPhaseTicketPercentageToUnstack.Length; ++i) {
            if (strVariable.Contains("Early Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref EarlyPhaseTicketPercentageToUnstack[i], "Early Phase Ticket Percentage To Unstack", 100.0, 1000.0, 120.0, true);
        }
        for (int i = 0; i < MidPhaseTicketPercentageToUnstack.Length; ++i) {
            if (strVariable.Contains("Mid Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref MidPhaseTicketPercentageToUnstack[i], "Mid Phase Ticket Percentage To Unstack", 100.0, 1000.0, 120.0, true);
        }
        for (int i = 0; i < LatePhaseTicketPercentageToUnstack.Length; ++i) {
            if (strVariable.Contains("Late Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref LatePhaseTicketPercentageToUnstack[i], "Late Phase Ticket Percentage To Unstack", 100.0, 1000.0, 120.0, true);
        }

        /* ===== SECTION 4 - Scrambler ===== */

        if (strVariable.Contains("Only On Final Ticket Percentage")) {ValidateDoubleRange(ref OnlyOnFinalTicketPercentage, "Only On Final Ticket Percentage", 100.0, 1000.0, 120.0, true);}

        else if (strVariable.Contains("Delay Seconds")) {ValidateDoubleRange(ref DelaySeconds, "Delay Seconds", 0, 50, 30, false);}

        /* ===== SECTION 5 - Messages ===== */
    
        else if (strVariable.Contains("Yell Duration Seconds")) {ValidateDoubleRange(ref YellDurationSeconds, "Yell Duration Seconds", 1, 20, 10, true);}

        else if (strVariable.Contains("Chat: Moved For Balance") && ChatMovedForBalance.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
        else if (strVariable.Contains("Yell: Moved For Balance") && YellMovedForBalance.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
        else if (strVariable.Contains("Chat: Moved To Unstack") && ChatMovedToUnstack.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
        else if (strVariable.Contains("Yell: Moved To Unstack") && YellMovedToUnstack.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
        else if (strVariable.Contains("Chat: Detected Good Team Switch") && ChatDetectedGoodTeamSwitch.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
        else if (strVariable.Contains("Yell: Detected Good Team Switch") && YellDetectedGoodTeamSwitch.Contains("%reason%")) {
            ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
        }
    
        /* ===== SECTION 6 ===== */

        /* ===== SECTION 7 - TBD ===== */

        /* ===== SECTION 8 - Per-Mode Settings ===== */

        foreach (String mode in fPerMode.Keys) {
            PerModeSettings perMode = fPerMode[mode];
            PerModeSettings def = new PerModeSettings(mode); // defaults for this mode

            def.MaxPlayers = Math.Min(def.MaxPlayers, MaximumServerSize);
            def.NumberOfSwapsPerGroup = Math.Min(def.NumberOfSwapsPerGroup, perMode.MaxUnstackingSwapsPerRound);
            def.DefinitionOfHighPopulationForPlayers = Math.Min(def.DefinitionOfHighPopulationForPlayers, perMode.MaxPlayers);
            def.DefinitionOfLowPopulationForPlayers = Math.Min(def.DefinitionOfLowPopulationForPlayers, perMode.MaxPlayers);
            if (strVariable.Contains("Max Players")) ValidateIntRange(ref perMode.MaxPlayers, mode + ":" + "Max Players", 8, MaximumServerSize, def.MaxPlayers, false);
            else if (strVariable.Contains("Check Team Stacking After First Minutes")) ValidateDouble(ref perMode.CheckTeamStackingAfterFirstMinutes, mode + ":" + "Check Team Stacking After First Minutes", def.CheckTeamStackingAfterFirstMinutes);
            else if (strVariable.Contains("Max Unstacking Swaps Per Round")) ValidateInt(ref perMode.MaxUnstackingSwapsPerRound, mode + ":" + "Max Unstacking Swaps Per Round", def.MaxUnstackingSwapsPerRound);
            else if (strVariable.Contains("Number Of Swaps Per Group")) ValidateIntRange(ref perMode.NumberOfSwapsPerGroup, mode + ":" + "Number Of Swaps Per Group", 0, perMode.MaxUnstackingSwapsPerRound, def.NumberOfSwapsPerGroup, false);
            else if (strVariable.Contains("Delay Seconds Between Swap Groups")) ValidateDoubleRange(ref perMode.DelaySecondsBetweenSwapGroups, mode + ":" + "Delay Seconds Between Swap Groups", 60, 24*60*60, def.DelaySecondsBetweenSwapGroups, false);
            else if (strVariable.Contains("Percent Of Top Of Team Is Strong")) ValidateDoubleRange(ref perMode.PercentOfTopOfTeamIsStrong, mode + ":" + "Percent Of Top Of Team Is Strong", 5, 50, def.PercentOfTopOfTeamIsStrong, false);
            else if (strVariable.Contains("Disperse Evenly By Rank")) ValidateIntRange(ref perMode.DisperseEvenlyByRank, mode + ":" + "Disperse Evenly By Rank", 0, 145, def.DisperseEvenlyByRank, true);
            else if (strVariable.Contains("Definition Of High Population For Players")) ValidateIntRange(ref perMode.DefinitionOfHighPopulationForPlayers, mode + ":" + "Definition Of High Population For Players", 0, perMode.MaxPlayers, def.DefinitionOfHighPopulationForPlayers, false); 
            else if (strVariable.Contains("Definition Of Low Population For Players")) ValidateIntRange(ref perMode.DefinitionOfLowPopulationForPlayers, mode + ":" + "Definition Of Low Population For Players", 0, perMode.MaxPlayers, def.DefinitionOfLowPopulationForPlayers, false);
            else if (strVariable.Contains("Definition Of Early Phase")) ValidateInt(ref perMode.DefinitionOfEarlyPhaseFromStart, mode + ":" + "Definition Of Early Phase From Start", def.DefinitionOfEarlyPhaseFromStart);
            else if (strVariable.Contains("Metro Adjusted Definition Of Late Phase")) ValidateInt(ref perMode.MetroAdjustedDefinitionOfLatePhase, mode + ":" + "Metro Adjusted Definition Of Late Phase", def.MetroAdjustedDefinitionOfLatePhase);
            else if (strVariable.Contains("Definition Of Late Phase")) ValidateInt(ref perMode.DefinitionOfLatePhaseFromEnd, mode + ":" + "Definition Of Late Phase From End", def.DefinitionOfLatePhaseFromEnd);
            if (mode == "CTF") {
                int maxMinutes = 20; // TBD, might need to factor in gameModeCounter
                if (strVariable.Contains("Definition Of Late Phase") && perMode.DefinitionOfLatePhaseFromEnd > maxMinutes) {
                    ConsoleError("^b" + "Definition Of Late Phase" + "^n must be less than or equal to " + maxMinutes + " minutes, corrected to " + maxMinutes);
                    perMode.DefinitionOfEarlyPhaseFromStart = 0;
                } else if (strVariable.Contains("Definition Of Early Phase") && perMode.DefinitionOfEarlyPhaseFromStart > (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd)) {
                    ConsoleError("^b" + "Definition Of Early Phase" + "^n must be less than or equal to " + (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd) + " minutes, corrected to " + (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd));
                    perMode.DefinitionOfEarlyPhaseFromStart = maxMinutes - perMode.DefinitionOfLatePhaseFromEnd;
                }
            } else if (mode == "Rush" || mode == "Squad Rush") {
                if (strVariable.Contains("Seconds To Check For New Stage")) ValidateDoubleRange(ref perMode.SecondsToCheckForNewStage, mode + ":" + "Seconds To Check For New Stage", 5, 30, def.SecondsToCheckForNewStage, false);
            }
        }

        /* ===== SECTION 9 - Debug Settings ===== */


    } catch (Exception e) {
        ConsoleException(e);
    }
    return true;
}

private void ResetSettings() {
    MULTIbalancer rhs = new MULTIbalancer();

    /* ===== SECTION 0 - Presets ===== */

    Preset = rhs.Preset;
    // EnableUnstacking = rhs.EnableUnstacking; // don't reset EnableUnstacking

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
    // Whitelist = rhs.Whitelist; // don't reset the whitelist
    // DisperseEvenlyList = rhs.DisperseEvenlyList; // don't reset the dispersal list
    
    /* ===== SECTION 2 - Exclusions ===== */
    
    OnWhitelist = rhs.OnWhitelist;
    OnFriendsList = rhs.OnFriendsList;
    TopScorers = rhs.TopScorers;
    SameClanTagsInSquad = rhs.SameClanTagsInSquad;
    SameClanTagsForRankDispersal = rhs.SameClanTagsForRankDispersal;
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

    /* ===== SECTION 4 - Scrambler ===== */

    OnlyOnNewMaps = rhs.OnlyOnNewMaps;
    OnlyOnFinalTicketPercentage = rhs.OnlyOnFinalTicketPercentage;
    ScrambleBy = rhs.ScrambleBy;
    KeepClanTagsInSameSquad = rhs.KeepClanTagsInSameSquad;
    DivideBy = rhs.DivideBy;
    ClanTagToDivideBy = rhs.ClanTagToDivideBy;
    DelaySeconds = rhs.DelaySeconds;

    /* ===== SECTION 5 - Messages ===== */
    
    QuietMode =  rhs.QuietMode;
    YellDurationSeconds = rhs.YellDurationSeconds;
    BadBecauseMovedByBalancer = rhs.BadBecauseMovedByBalancer;
    BadBecauseWinningTeam = rhs.BadBecauseWinningTeam;
    BadBecauseBiggestTeam = rhs.BadBecauseBiggestTeam;
    BadBecauseRank = rhs.BadBecauseRank;
    BadBecauseDispersalList = rhs.BadBecauseDispersalList;
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
    
    /* ===== SECTION 6 - Unswitcher ===== */

    ForbidSwitchingAfterAutobalance = rhs.ForbidSwitchingAfterAutobalance;
    ForbidSwitchingToWinningTeam = rhs.ForbidSwitchingToWinningTeam;
    ForbidSwitchingToBiggestTeam = rhs.ForbidSwitchingToBiggestTeam;
    ForbidSwitchingAfterDispersal = rhs.ForbidSwitchingAfterDispersal;
    EnableImmediateUnswitch = rhs.EnableImmediateUnswitch;

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

    ShowCommandInLog = rhs.ShowCommandInLog;
    LogChat = rhs.LogChat;
    EnableLoggingOnlyMode = rhs.EnableLoggingOnlyMode;
}

private void CommandToLog(string cmd) {
    try {
        Match m = null;
        String msg = String.Empty;
        ConsoleDump("Command: " + cmd);

        m = Regex.Match(cmd, @"^gen\s+((?:cs|cl|ctf|gm|r|sqdm|sr|s|tdm|u)|[1234569])", RegexOptions.IgnoreCase);
        if (m.Success) {
            String what = m.Groups[1].Value;
            int section = 8;
            if (!Int32.TryParse(what, out section)) section = 8;

            List<CPluginVariable> vars = GetDisplayPluginVariables();

            String sm = section.ToString() + " -";
            if (section == 8) {
                switch (what) {
                    case "cs": sm = "for Conq Small, Dom, Scav"; break;
                    case "cl": sm = "for Conquest Large"; break;
                    case "ctf": sm = "for CTF"; break;
                    case "gm": sm = "for Gun Master"; break;
                    case "r": sm = "for Rush"; break;
                    case "sqdm": sm = "for Squad Deathmatch"; break;
                    case "sr": sm = "for Squad Rush"; break;
                    case "s": sm = "for Superiority"; break;
                    case "tdm": sm = "for Team Deathmatch"; break;
                    case "u": sm = "for Unknown or New Mode"; break;
                    default: ConsoleDump("Unknown mode: " + what); return;
                }
            }

            foreach (CPluginVariable var in vars) {
                if (section == 8) {
                    if (var.Name.Contains(sm)) {
                        ConsoleDump(var.Name + ": " + var.Value);
                    }
                } else {
                    if (var.Name.Contains(sm)) {
                        ConsoleDump(var.Name + ": " + var.Value);
                    }
                }
            }
            return;
        }

        if (Regex.Match(cmd, @"lists", RegexOptions.IgnoreCase).Success) {
            ConsoleDump("Whitelist(" + fSettingWhitelist.Count + "):");
            foreach (String item in fSettingWhitelist) {
                ConsoleDump(item);
            }
            ConsoleDump(" ");
            ConsoleDump("Friends List(" + fFriends.Keys.Count +"):");
            foreach (int k in fFriends.Keys) {
                ConsoleDump(k.ToString() + ": " + String.Join(", ", fFriends[k].ToArray()));
            }
            ConsoleDump(" ");
            ConsoleDump("Disperse Evenly List(" + fSettingDisperseEvenlyList.Count + "):");
            foreach (String item in fSettingDisperseEvenlyList) {
                ConsoleDump(item);
            }
            ConsoleDump(" ");
            for (int i = 1; i <= 4; ++i) {
                if (fDispersalGroups[i].Count > 0) {
                    msg = "Dispersal Group " + i + " (" + fDispersalGroups[i].Count + "): " + String.Join(", ", fDispersalGroups[i].ToArray());
                    ConsoleDump(msg);
                }
            }
            ConsoleDump(" ");
            msg = "Group assignments: ";
            for (int i = 1; i <= 4; ++i) {
                msg = msg + fGroupAssignments[i];
                if (i < 4) msg = msg + "/";
            }
            ConsoleDump(msg);
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

        if (Regex.Match(cmd, @"moved", RegexOptions.IgnoreCase).Success) {
            lock (fKnownPlayers) {
                foreach (String name in fKnownPlayers.Keys) {
                    PlayerModel p = fKnownPlayers[name];
                    if (p.MovedByMBTimestamp != DateTime.MinValue) {
                        ConsoleDump("^b" + p.FullName + "^n was moved " + p.MovesByMBRound + " times this round, " + (p.MovesByMBTotal + p.MovesByMBRound) + " total, the last was " + DateTime.Now.Subtract(p.MovedByMBTimestamp).TotalMinutes.ToString("F0") + " minutes ago");
                    }
                }
            }
            return;
        }

        if (Regex.Match(cmd, @"rage", RegexOptions.IgnoreCase).Success) {
            ConsoleDump("Rage stats: " + fGrandRageQuits + " rage of " + fGrandTotalQuits + " total, this round " + fRageQuits + " rage of " + fTotalQuits + " total"); 
            return;
        }
        
        if (Regex.Match(cmd, @"reset settings", RegexOptions.IgnoreCase).Success) {
            ConsoleDump("^8^bRESETTING ALL PLUGIN SETTINGS (except Whitelist and Dispersal list) TO DEFAULT!");
            ResetSettings();
            return;
        }

        if (Regex.Match(cmd, @"scramble[d]?", RegexOptions.IgnoreCase).Success) {
            if (fDebugScramblerBefore[0].Count == 0
              || fDebugScramblerBefore[1].Count == 0
              || fDebugScramblerAfter[0].Count == 0
              || fDebugScramblerAfter[1].Count == 0) {
                ConsoleDump("No scrambler data available");
                return;
            }
            ConsoleDump("===== BEFORE =====");
            ListSideBySide(fDebugScramblerBefore[0], fDebugScramblerBefore[1]);
            ConsoleDump("===== AFTER =====");
            ListSideBySide(fDebugScramblerAfter[0], fDebugScramblerAfter[1]);
            ConsoleDump("===== END =====");
            return;
        }

        if (Regex.Match(cmd, @"size[s]?", RegexOptions.IgnoreCase).Success) {
            int kp = fKnownPlayers.Count;
            int ap = fAllPlayers.Count;
            int old = 0;
            int validTags = 0;
            lock (fKnownPlayers) {
                // count player records more than 12 hours old
                foreach (String name in fKnownPlayers.Keys) {
                    PlayerModel p = fKnownPlayers[name];
                    if (DateTime.Now.Subtract(p.LastSeenTimestamp).TotalMinutes > 12*60) {
                        if (!IsKnownPlayer(name)) {
                            ++old;
                        } 
                    }
                    if (p.TagVerified) ++validTags;
                }
            }
            ConsoleDump("Plugin has been enabled for " + fRoundsEnabled + " rounds");
            ConsoleDump("fKnownPlayers.Count = " + kp + ", not playing = " + (kp-ap) + ", more than 12 hours old = " + old);
            ConsoleDump("fPriorityFetchQ.Count = " + fPriorityFetchQ.Count + ", verified tags = " + validTags);
            ConsoleDump("MULTIbalancerUtils.HTML_DOC.Length = " + MULTIbalancerUtils.HTML_DOC.Length);
            return;
        }

        m = Regex.Match(cmd, @"^sort\s+([1-4])\s+(score|spm|kills|kdr|rank|kpm|bspm|bkdr|bkpm)", RegexOptions.IgnoreCase);
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
                case "spm":
                    fromList.Sort(DescendingRoundSPM);
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
                case "kpm":
                    fromList.Sort(DescendingRoundKPM);
                    break;
                case "bspm":
                    fromList.Sort(DescendingSPM);
                    break;
                case "bkdr":
                    fromList.Sort(DescendingKDR);
                    break;
                case "bkpm":
                    fromList.Sort(DescendingKPM);
                    break;
                default:
                    fromList.Sort(DescendingRoundScore);
                    break;
            }
            int n = 1;
            foreach (PlayerModel p in fromList) {
                switch (propID.ToLower()) {
                    case "score":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6:F0}", p.ScoreRound) + ", ^b" + p.FullName);
                        break;
                    case "spm":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") SPM: " + String.Format("{0,6:F0}", p.SPMRound) + ", ^b" + p.FullName);
                        break;
                    case "kills":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Kills: " + String.Format("{0,6:F0}", p.KillsRound) + ", ^b" + p.FullName);
                       break;
                    case "kdr":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") KDR: " + String.Format("{0,6:F1}", p.KDRRound) + ", ^b" + p.FullName);
                        break;
                    case "rank":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Rank: " + String.Format("{0,6:F0}", p.Rank) + ", ^b" + p.FullName);
                        break;
                    case "kpm":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") KPM: " + String.Format("{0,6:F1}", p.KPMRound) + ", ^b" + p.FullName);
                        break;
                    case "bspm":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") bSPM: " + String.Format("{0,6:F0}", ((p.StatsVerified) ? p.SPM : p.SPMRound)) + ", ^b" + p.FullName);
                        break;
                    case "bkdr":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") bKDR: " + String.Format("{0,6:F1}", ((p.StatsVerified) ? p.KDR : p.KDRRound)) + ", ^b" + p.FullName);
                        break;
                    case "bkpm":
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") bKPM: " + String.Format("{0,6:F0}", ((p.StatsVerified) ? p.KPM : p.KPMRound)) + ", ^b" + p.FullName);
                        break;
                    default:
                        ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6:F0}", p.ScoreRound) + ", ^b" + p.FullName);
                        break;
                }
                n = n + 1;
            }
            return;
        }

        if (Regex.Match(cmd, @"tags?", RegexOptions.IgnoreCase).Success) {
            Dictionary<String,List<PlayerModel>> byTag = new Dictionary<String,List<PlayerModel>>();

            lock (fAllPlayers) {
                foreach (String name in fAllPlayers) {
                    PlayerModel player = GetPlayer(name);
                    if (player == null || player.Team < 1 || player.Team > 2) continue;
                    String tag = ExtractTag(player);
                    if (String.IsNullOrEmpty(tag)) continue;
                    if (!byTag.ContainsKey(tag)) {
                        byTag[tag] = new List<PlayerModel>();
                    }
                    byTag[tag].Add(player);
                }
            }

            List<String> tags = new List<String>();
            foreach (String t in byTag.Keys) {
                tags.Add(t);
                byTag[t].Sort(delegate(PlayerModel lhs, PlayerModel rhs) { // ascending by team/squad
                    if (lhs == null && rhs == null) return 0;
                    if (lhs == null) return -1;
                    if (rhs == null) return 1;

                    // by team, then by squad
                    if (lhs.Team < rhs.Team) return -1;
                    if (lhs.Team > rhs.Team) return 1;
                    if (lhs.Team == rhs.Team) {
                        if (lhs.Squad < 1 || rhs.Squad < 1) return 0;
                        if (lhs.Squad < rhs.Squad) return -1;
                        if (lhs.Squad > rhs.Squad) return 1;
                    }
                    return 0;
                });
            }
            tags.Sort();

            foreach (String t in tags) {
                ConsoleDump("Tag [" + t + "]:");
                List<PlayerModel> clan = byTag[t];
                foreach (PlayerModel p in clan) {
                    ConsoleDump(String.Format("        {0}, {1}, {2}",
                        p.Name,
                        GetTeamName(p.Team),
                        (p.Squad < 0 || p.Squad > SQUAD_NAMES.Length) ? "None" : SQUAD_NAMES[p.Squad]
                    ));
                }
            }
            ConsoleDump(" === END OF TAGS === ");
            return;
        }

        if (Regex.Match(cmd, @"help", RegexOptions.IgnoreCase).Success || !String.IsNullOrEmpty(cmd)) {
            ConsoleDump("^1^bgen^n ^imode^n^0: Generate settings listing for ^imode^n (one of: cs, cl, ctf, gm, r, sqdm, sr, s, tdm, u)");
            ConsoleDump("^1^bgen^n ^isection^n^0: Generate settings listing for ^isection^n (1-6,9)");
            ConsoleDump("^1^blists^n^0: Examine all settings that are lists");
            ConsoleDump("^1^bmodes^n^0: Examine the known game modes");
            ConsoleDump("^1^bmoved^n^0: Examine which players were moved, how many times total and how long ago");
            ConsoleDump("^1^brage^n^0: Examine rage quit statistics");
            ConsoleDump("^1^breset settings^n^0: Reset all plugin settings to default, except for ^bWhitelist^n and ^bDisperse Evenly List^n");
            ConsoleDump("^1^bscrambled^n^0: Examine list of players before and after last successful scramble");
            ConsoleDump("^1^bsizes^n^0: Examine the sizes of various data structures");
            ConsoleDump("^1^bsort^n ^iteam^n ^itype^n^0: Examine sorted ^iteam^n (1-4) by ^itype^n (one of: score, spm, kills, kdr, rank, kpm, bspm, bkdr, bkpm)");
            ConsoleDump("^1^btags^n^0: Examine list of players sorted by clan tags");
            return;
        }

            

    } catch (Exception e) {
        ConsoleException(e);
    }
}









/* ======================== OVERRIDES ============================= */










public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion) {
    fHost = strHostName;
    fPort = strPort;

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
        "OnPlayerSquadChange",
        //"OnGlobalChat",
        //"OnTeamChat",
        //"OnSquadChat",
        "OnRoundOverPlayers",
        "OnRoundOver",
        "OnRoundOverTeamScores",
        "OnLevelLoaded",
        "OnPlayerKilledByAdmin",
        "OnPlayerMovedByAdmin",
        "OnPlayerIsAlive",
        "OnReservedSlotsList",
        "OnEndRound",
        "OnRunNextLevel"
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

    ConsoleWrite("^b^2Enabled!^0^n Version = " + GetPluginVersion());
    DebugWrite("^b^3State = " + fPluginState, 6);
    DebugWrite("^b^3Game state = " + fGameState, 6);

    GatherProconGoodies();

    StartThreads();

    ServerCommand("reservedSlotsList.list");
    ServerCommand("serverInfo");
    ServerCommand("admin.listPlayers", "all");

    LaunchCheckForPluginUpdate();

    fIsCacheEnabled = IsCacheEnabled(true);
}


public void OnPluginDisable() {
    fIsEnabled = false;

    try {
        LaunchCheckForPluginUpdate();

        fEnabledTimestamp = DateTime.MinValue;

        StopThreads();

        Reset();
    
        ConsoleWrite("^bDisabling, stopping threads ...^n");
        fPluginState = PluginState.Disabled;
        fGameState = GameState.Unknown;
        DebugWrite("^b^3State = " + fPluginState, 6);
        DebugWrite("^b^3Game state = " + fGameState, 6);
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnVersion(String type, String ver) {
    if (!fIsEnabled) return;
    
    DebugWrite("Got ^bOnVersion^n: " + type + " " + ver, 7);
    try {
        fGotVersion = true;
    } catch (Exception e) {
        ConsoleException(e);
    }
}

//public override void OnPlayerJoin(String soldierName) { }

public override void OnPlayerLeft(CPlayerInfo playerInfo) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerLeft:^n " + playerInfo.SoldierName, 8);

    try {
        if (IsKnownPlayer(playerInfo.SoldierName)) {
            CheckRageQuit(playerInfo.SoldierName);
            ValidateMove(playerInfo.SoldierName);
            RemovePlayer(playerInfo.SoldierName);
        }
    
        DebugWrite("Player left: ^b" + playerInfo.SoldierName, 4);
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public override void OnPlayerSquadChange(String soldierName, int teamId, int squadId) {
    if (!fIsEnabled) return;

    if (squadId == 0) return;
    
    DebugWrite("^9^bGot OnPlayerSquadChange^n: " + soldierName + " " + teamId + " " + squadId, 7);
}


public override void OnPlayerTeamChange(String soldierName, int teamId, int squadId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerTeamChange^n: " + soldierName + " " + teamId + " " + squadId, 7);

    if (fPluginState == PluginState.Disabled || fPluginState == PluginState.Error) return;

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
            DebugWrite("^4New player^0: ^b" + soldierName + "^n, reassigned to " + GetTeamName(teamId) + " team by " + GetPluginName(), 4);
       } else if (!IsKnownPlayer(soldierName)) {
            int diff = 0;
            bool mustMove = false; // don't have a player model yet, can't determine if must move
            int reassignTo = ToTeam(soldierName, teamId, true, out diff, ref mustMove);
            if (reassignTo == 0 || reassignTo == teamId) {
                // New player was going to the right team anyway
                IncrementTotal(); // no matching stat, reflects non-reassigment joins
                AddNewPlayer(soldierName, teamId);
                UpdateTeams();
                DebugWrite("^4New player^0: ^b" + soldierName + "^n, assigned to " + GetTeamName(teamId) + " team by game server", 4);
            } else {
                Reassign(soldierName, teamId, reassignTo, diff);
            }
        } else if (fGameState == GameState.Playing) {

            // If this was an MB move, finish it
            bool wasPluginMove = FinishMove(soldierName, teamId);

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
                if (!wasPluginMove) {
                    // Some other admin.movePlayer, so update to account for it
                    ConditionalIncrementMoves(soldierName);
                    UpdatePlayerTeam(soldierName, teamId);
                    UpdateTeams();
                } // MB moves incremented by FinishMove, so nothing to do here
                return;
            }

            // Remember the pending move in a table
            fPendingTeamChange[soldierName] = teamId;

            // Admin move event may still be on its way, so do a round-trip to check
            ServerCommand("player.isAlive", soldierName);
        } else if (fGameState == GameState.RoundStarting || fGameState == GameState.RoundEnding) {
            UpdatePlayerTeam(soldierName, teamId);
            UpdateTeams();
        }
    } catch (Exception e) {
        ConsoleException(e);
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

            // Check if player is allowed to switch teams
            // Unswitch is handled in CheckTeamSwitch
            if (CheckTeamSwitch(soldierName, team)) {
                UpdatePlayerTeam(soldierName, team);
                UpdateTeams();        
                IncrementTotal(); // No matching stat, reflects allowed team switches
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public override void OnPlayerMovedByAdmin(string soldierName, int destinationTeamId, int destinationSquadId, bool forceKilled) {
    if (!fIsEnabled) return;

    DebugWrite("^9^bGot OnPlayerMovedByAdmin^n: " + soldierName + " " + destinationTeamId + " " + destinationSquadId + " " + forceKilled, 7);

    try {
        if (fPluginState == PluginState.Active && fGameState == GameState.Playing) {
            if (fPendingTeamChange.ContainsKey(soldierName)) {
                // this is an admin move in reversed order, clear from pending table and ignore the move
                fPendingTeamChange.Remove(soldierName);
                DebugWrite("(REVERSED) Moved by admin: ^b" + soldierName + "^n to team " + destinationTeamId, 6);
                // Already handled updates in preceding team change event
                /*
                ConditionalIncrementMoves(soldierName);
                // Some other admin.movePlayer, so update to account for it
                UpdatePlayerTeam(soldierName, destinationTeamId);
                UpdateTeams();
                */
            } else if (!fUnassigned.Contains(soldierName)) {
                // this is an admin move in correct order, add to pending table and let OnPlayerTeamChange handle it
                fPendingTeamChange[soldierName] = destinationTeamId;
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
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
        ConsoleException(e);
    }
}
*/


public override void OnPlayerKilled(Kill kKillerVictimDetails) {
    if (!fIsEnabled) return;

    String killer = kKillerVictimDetails.Killer.SoldierName;
    String victim = kKillerVictimDetails.Victim.SoldierName;
    String weapon = kKillerVictimDetails.DamageType;
    
    bool isAdminKill = false;
    if (String.IsNullOrEmpty(killer)) {
        killer = victim;
        isAdminKill = (weapon == "Death");
    }
    
    DebugWrite("^9^bGot OnPlayerKilled^n: " + killer  + " -> " + victim + " (" + weapon + ")", 8);
    if (isAdminKill) DebugWrite("^9OnPlayerKilled: admin kill: ^b" + victim + "^n (" + weapon + ")", 7);

    try {
    
        if (fGameState == GameState.Unknown || fGameState == GameState.Warmup) {
            bool wasUnknown = (fGameState == GameState.Unknown);
            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            if (wasUnknown || fGameState == GameState.Playing) DebugWrite("OnPlayerKilled: ^b^3Game state = " + fGameState, 6);  
            fNeedGroupAssignment = (fGameState == GameState.Playing);
        }
    
        if (!isAdminKill) {
            KillUpdate(killer, victim);
    
            if (fPluginState == PluginState.Active && fGameState == GameState.Playing) {
                if (!IsModelInSync()) {
                    if (fTimeOutOfJoint == 0) {
                        // If a move or reassign takes too long, abort it, checked in OnListPlayers
                        fTimeOutOfJoint = GetTimeInRoundMinutes();
                    }
                } else {
                    fTimeOutOfJoint = 0;
                    BalanceAndUnstack(victim);
                }
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
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
        Check if server crashed or Blaze dumped players or model invalid for too long.
        Detected by: last recorded server uptime is greater than zero and less than new uptime,
        or a player model timed out while still being on the all players list,
        or got a version command response, which is used in connection initialization for Procon,
        or the current list of players is more than CRASH_COUNT_HEURISTIC players less than the last
        recorded count, or the last known player count is greater than the maximum server size,
        or more than 3 minutes have elapsed since a move/reassign was started.
        Since these detections are not completely reliable, do a minimal  amount of recovery,
        don't do a full reset
        */
        if (fServerCrashed 
        || fGotVersion 
        || (TotalPlayerCount >= 16 
            && TotalPlayerCount > players.Count 
            && (TotalPlayerCount - players.Count) >= Math.Min(CRASH_COUNT_HEURISTIC, TotalPlayerCount)) 
        || TotalPlayerCount > MaximumServerSize
        || (fTimeOutOfJoint > 0 && GetTimeInRoundMinutes() - fTimeOutOfJoint > 3.0))  {
            fServerCrashed = false;
            fGotVersion = false;
            fTimeOutOfJoint = 0;
            ValidateModel(players);
        } else {
            fUnassigned.Clear();
    
            foreach (CPlayerInfo p in players) {
                try {
                    UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank);
                } catch (Exception e) {
                    ConsoleException(e);
                    continue;
                }
            }
        }

        GarbageCollectKnownPlayers(); // also resets LastMoveTo
 
        UpdateTeams();

        LogStatus(false);
    
        /* Special handling for JustEnabled state */
        if (fPluginState == PluginState.JustEnabled) {
            fPluginState = PluginState.Active;
            fRoundStartTimestamp = DateTime.Now;
            DebugWrite("^b^3State = " + fPluginState, 6);  
        }

        // Use updated player list
        if (fNeedGroupAssignment) {
            AssignGroups();
            fNeedGroupAssignment = false;
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnServerInfo(CServerInfo serverInfo) {
    if (!fIsEnabled || serverInfo == null) return;

    DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 8);
    
    try {
        fLastServerInfoTimestamp = DateTime.Now;

        // Update game state if just enabled (as of R38, CTF TeamScores may be null, does not mean round end)
        if (fGameState == GameState.Unknown && serverInfo.GameMode != "CaptureTheFlag0") {
            if (serverInfo.TeamScores == null || serverInfo.TeamScores.Count < 2) {
                fGameState = GameState.RoundEnding;
                DebugWrite("OnServerInfo: ^b^3Game state = " + fGameState, 6);  
            }
        }

        // Show final status 
        if (fFinalStatus != null) {
            try {
                DebugWrite("^bFINAL STATUS FOR PREVIOUS ROUND:^n", 2);
                foreach (TeamScore ts in fFinalStatus) {
                    if (ts.TeamID >= fTickets.Length) break;
                    fTickets[ts.TeamID] = (ts.Score == 1) ? 0 : ts.Score; // fix rounding
                }
                LogStatus(true);
                DebugWrite("+------------------------------------------------+", 2);
            } catch (Exception) {}
            fFinalStatus = null;
        }

        if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map) {
            ConsoleDebug("ServerInfo update: " + serverInfo.Map + "/" + serverInfo.GameMode);
        }
    
        // Check for server crash
        if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime + 2) { // +2 secs for rounding error in server!
            fServerCrashed = true;
        }
        fServerInfo = serverInfo;
        fServerUptime = serverInfo.ServerUptime;

        // Update max tickets
        PerModeSettings perMode = GetPerModeSettings();
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
            if (fStageInProgress) {
                if (attacker < fRushPrevAttackerTickets && attacker > 0) {
                    fRushAttackerStageLoss = fRushAttackerStageLoss + (fRushPrevAttackerTickets - attacker);
                    ++fRushAttackerStageSamples;
                }
            }
            String avl = String.Empty;
            if (fStageInProgress) avl = ", avg loss = " + RushAttackerAvgLoss().ToString("F1") + "/" + perMode.SecondsToCheckForNewStage.ToString("F0") + " secs";
            if (TotalPlayerCount > 3) DebugWrite("^7serverInfo: Rush attacker = " + attacker + ", was = " + fMaxTickets + avl + ", defender = " + defender, 7); 
        }

        if (fMaxTickets == -1) {
            if (!isRush) {
                fMaxTickets = maxTickets;
                ConsoleDebug("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0"));
            } else if (fServerInfo.TeamScores.Count == 2) {
                fRushMaxTickets = defender;
                fMaxTickets = attacker;
                fRushStage = 1;
                fRushPrevAttackerTickets = attacker;
                fRushAttackerStageSamples = 0;
                fRushAttackerStageLoss = 0;
                fStageInProgress = false;
                ConsoleDebug("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0") + ", fRushMaxTickets = " + fRushMaxTickets + ", fRushStage = " + fRushStage);
            }
        }

        // Rush heuristic: if attacker tickets are higher than last check, new stage started
        if (isRush && fServerInfo != null && !String.IsNullOrEmpty(fServerInfo.Map)) {
            int maxStages = 4;
            if (fRushMap3Stages.Contains(fServerInfo.Map)) {
                maxStages = 3;
            } else if (fRushMap5Stages.Contains(fServerInfo.Map)) {
                maxStages = 5;
            }
            if (fRushStage == 0) {
                fRushMaxTickets = defender;
                fMaxTickets = attacker;
                fRushStage = 1;
                fRushPrevAttackerTickets = attacker;
                fRushAttackerStageSamples = 0;
                fRushAttackerStageLoss = 0;
            }
            if (!fStageInProgress) {
                // hysteresis, wait for attacker tickets to go below threshold before stage is in progress for sure
                fStageInProgress = ((attacker + (2 * perMode.SecondsToCheckForNewStage / 5)) < fMaxTickets);
                if (fStageInProgress) {
                    DebugWrite("^7serverInfo: stage " + fRushStage + " in progress!", 7);
                }
            } else if (attacker > fRushPrevAttackerTickets
            && (attacker - fRushPrevAttackerTickets) >= Math.Min(12, 2 * perMode.SecondsToCheckForNewStage / 5)
            && AttackerTicketsWithinRangeOfMax(attacker) 
            && fRushStage < 5) {
                fStageInProgress = false;
                fRushMaxTickets = defender;
                fMaxTickets = attacker;
                fRushPrevAttackerTickets = attacker;
                fRushStage = fRushStage + 1;
                fRushAttackerStageSamples = 0;
                fRushAttackerStageLoss = 0;
                DebugWrite(".................................... ^b^1New rush stage detected^0^n ....................................", 3);
                DebugBalance("Rush Stage " + fRushStage + " of " + maxStages);
            }
            // update last known attacker ticket value
            fRushPrevAttackerTickets = attacker;
        }

        // Check for plugin updates periodically
        if (fLastVersionCheckTimestamp != DateTime.MinValue 
        && DateTime.Now.Subtract(fLastVersionCheckTimestamp).TotalMinutes > CHECK_FOR_UPDATES_MINS) {
            LaunchCheckForPluginUpdate();
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}


//public override void OnResponseError(List<String> requestWords, String error) { }

//public override void OnGlobalChat(String speaker, String message) { }

//public override void OnTeamChat(String speaker, String message, int teamId) { }

//public override void OnSquadChat(String speaker, String message, int teamId, int squadId) { }

public override void OnRoundOverPlayers(List<CPlayerInfo> players) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOverPlayers^n", 7);

    try {
        // TBD
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public override void OnRoundOverTeamScores(List<TeamScore> teamScores) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOverTeamScores^n", 7);

    try {
        fFinalStatus = teamScores;
        ServerCommand("serverInfo"); // get info for final status report
        Scrambler(teamScores);
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public override void OnRoundOver(int winningTeamId) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRoundOver^n: winner " + winningTeamId, 7);
    fWinner = winningTeamId;

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Round over detected^0^n ::::::::::::::::::::::::::::::::::::", 3);
    
        if (fGameState == GameState.Playing || fGameState == GameState.Unknown) {
            fGameState = GameState.RoundEnding;
            DebugWrite("OnRoundOver: ^b^3Game state = " + fGameState, 6);
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public override void OnLevelLoaded(String mapFileName, String Gamemode, int roundsPlayed, int roundsTotal) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnLevelLoaded^n: " + mapFileName + " " + Gamemode + " " + roundsPlayed + "/" + roundsTotal, 7);

    try {
        DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Level loaded detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

        if (fGameState == GameState.RoundEnding || (fGameState == GameState.Warmup && TotalPlayerCount >= 4) || fGameState == GameState.Unknown) {
            fGameState = GameState.RoundStarting;
            DebugWrite("OnLevelLoaded: ^b^3Game state = " + fGameState, 6);  
        }

        fMaxTickets = -1; // flag to pay attention to next serverInfo
        ServerCommand("serverInfo");
    } catch (Exception e) {
        ConsoleException(e);
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
            fNeedGroupAssignment = (fGameState == GameState.Playing);
        } else if (fGameState == GameState.RoundStarting) {
            // First spawn after Level Loaded is the official start of a round
            DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1First spawn detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

            fGameState = (TotalPlayerCount < 4) ? GameState.Warmup : GameState.Playing;
            DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);

            ResetRound();
            fIsFullRound = true;
            ServerCommand("serverInfo");
            ScheduleListPlayers(2);
            fNeedGroupAssignment = (fGameState == GameState.Playing);
        }
    
        if (fPluginState == PluginState.Active) {
            ValidateMove(soldierName);
            if (fGameState != GameState.RoundEnding) {
                SpawnUpdate(soldierName);
                FireMessages(soldierName);
                CheckDelayedMove(soldierName);
                if (IsRush()) CheckServerInfoUpdate();
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public override void OnPlayerKilledByAdmin(string soldierName) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnPlayerKilledByAdmin^n: " + soldierName, 7);
}

public override void OnReservedSlotsList(List<String> lstSoldierNames) {
    //if (!fIsEnabled) return; // do this always
    
    DebugWrite("^9^bGot OnReservedSlotsList^n", 7);
    fReservedSlots = lstSoldierNames;
}

public override void OnEndRound(int iWinningTeamID) {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnEndRound^n: " + iWinningTeamID, 7);
}

public override void OnRunNextLevel() {
    if (!fIsEnabled) return;
    
    DebugWrite("^9^bGot OnRunNextLevel^n", 7);
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
    String log = String.Empty;

    /* Sanity checks */

    if (fServerInfo == null) {
        return;
    }

    int totalPlayerCount = TotalPlayerCount;

    if (totalPlayerCount >= (MaximumServerSize-1)) {
        if (DebugLevel >= 6) DebugBalance("Server is full, no balancing or unstacking will be attempted!");
        IncrementTotal(); // no matching stat, reflect total deaths handled
        CheckDeativateBalancer("Full");
        return;
    }

    int floorPlayers = 6;
    if (totalPlayerCount < floorPlayers) {
        if (DebugLevel >= 6) DebugBalance("Not enough players in server, minimum is " + floorPlayers);
        CheckDeativateBalancer("Not enough players");
        return;
    }

    if (totalPlayerCount > 0) {
        AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);
    } else {
        CheckDeativateBalancer("Empty");
        return;
    }


    /* Pre-conditions */

    player = GetPlayer(name);
    if (player == null) {
        CheckDeativateBalancer("Unknown player " + name);
        return;
    }

    if (!fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode)) {
        DebugBalance("Unknown game mode: " + fServerInfo.GameMode);
        simpleMode = fServerInfo.GameMode;
    }
    if (String.IsNullOrEmpty(simpleMode)) {
        DebugBalance("Simple mode is null: " + fServerInfo.GameMode);
        CheckDeativateBalancer("Unknown mode");
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

    /* Per-mode and player info */

    String extractedTag = ExtractTag(player);
    Speed balanceSpeed = GetBalanceSpeed(perMode);
    double unstackTicketRatio = GetUnstackTicketRatio(perMode);
    int lastMoveFrom = player.LastMoveFrom;

    if (totalPlayerCount >= (perMode.MaxPlayers-1)) {
        if (DebugLevel >= 6) DebugBalance("Server is full by per-mode Max Players, no balancing or unstacking will be attempted!");
        IncrementTotal(); // no matching stat, reflect total deaths handled
        CheckDeativateBalancer("Full per-mode");
        return;
    }

    /* Check dispersals */
    
    bool mustMove = false;
    bool isDisperseByRank = IsRankDispersal(player);
    bool isDisperseByList = IsDispersal(player);
    if (isDisperseByRank) {
        DebugBalance("ON MUST MOVE LIST ^b" + name + "^n T:" + player.Team + ", Rank " + player.Rank + " >= " + perMode.DisperseEvenlyByRank);
        mustMove = true;
    } else if (isDisperseByList) {
        DebugBalance("ON MUST MOVE LIST ^b" + player.FullName + "^n T:" + player.Team + ", disperse evenly enabled");
        mustMove = true;
    }

    /* Check if balancing is needed */

    if (diff > MaxDiff()) {
        needsBalancing = true; // needs balancing set to true, unless speed is Unstack only
        if (balanceSpeed == Speed.Unstack) {
            DebugBalance("Needs balancing, but balance speed is set to Unstack, so no balancing will be done");
            needsBalancing = false;
        }
    }

    /* Per-mode settings */

    // Adjust for duration of balance active
    if (needsBalancing && fBalanceIsActive && balanceSpeed == Speed.Adaptive && fLastBalancedTimestamp != DateTime.MinValue) {
        double secs = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (secs > SecondsUntilAdaptiveSpeedBecomesFast) {
            DebugBalance("^8^bBalancing taking too long (" + secs.ToString("F0") + " secs)!^n^0 Forcing to Fast balance speed.");
            balanceSpeed = Speed.Fast;
        }
    }
    String andSlow = (balanceSpeed == Speed.Slow) ? " and speed is Slow" : String.Empty;

    // Do not disperse mustMove players if speed is Stop or Phase is Late
    if (mustMove && balanceSpeed == Speed.Stop) {
        DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Balance Speed = Stop");
        mustMove = false;
    } else if (GetPhase(perMode, false) == Phase.Late) {
        DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Phase = Late");
        mustMove = false;
    }

    // Wait for unassigned
    if (!mustMove && balanceSpeed != Speed.Fast && (diff > MaxDiff()) && fUnassigned.Count >= (diff - MaxDiff())) {
        DebugBalance("Wait for " + fUnassigned.Count + " unassigned players to be assigned before activating autobalance");
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    }

    /* Activation check */

    if (balanceSpeed != Speed.Stop && needsBalancing) {
        if (!fBalanceIsActive) {
            DebugBalance("^2^bActivating autobalance!");
            fLastBalancedTimestamp = now;
        }
        fBalanceIsActive = true;
    } else {
        CheckDeativateBalancer("Deactiving autobalance");
    }

    /* Exclusions */

    // Exclude if on Whitelist or Reserved Slots if enabled
    if (OnWhitelist || (needsBalancing && balanceSpeed == Speed.Slow)) {
        List<String> vip = new List<String>(fSettingWhitelist);
        if (EnableWhitelistingOfReservedSlotsList) vip.AddRange(fReservedSlots);
        List <String> tmp = new List<String>();
        // clean up the list
        foreach (String v in vip) {
            if (String.IsNullOrEmpty(v)) continue;
            if (v == INVALID_NAME_TAG_GUID) continue;
            tmp.Add(v);
        }
        vip = tmp;
        String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
        String xt = extractedTag;
        if (String.IsNullOrEmpty(xt)) xt = INVALID_NAME_TAG_GUID;
        if (vip.Contains(name) || vip.Contains(xt) || vip.Contains(guid)) {
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
        case DefineStrong.RoundSPM:
            fromList.Sort(DescendingRoundSPM);
            strongMsg = "Determing strong by: Round SPM";
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
        case DefineStrong.RoundKPM:
            fromList.Sort(DescendingRoundKPM);
            strongMsg = "Determing strong by: Round KPM";
            break;
        case DefineStrong.BattlelogSPM:
            fromList.Sort(DescendingSPM);
            strongMsg = "Determing strong by: Battlelog SPM";
            break;
        case DefineStrong.BattlelogKDR:
            fromList.Sort(DescendingKDR);
            strongMsg = "Determing strong by: Battlelog KDR";
            break;
        case DefineStrong.BattlelogKPM:
            fromList.Sort(DescendingKPM);
            strongMsg = "Determing strong by: Battlelog KPM";
            break;
        default:
            fromList.Sort(DescendingRoundScore);
            strongMsg = "Determing strong by: Round Score";
            break;
    }

    double above = ((fromList.Count * perMode.PercentOfTopOfTeamIsStrong) / 100.0) + 0.5;
    int strongest = Math.Max(0, Convert.ToInt32(above));
    int playerIndex = 0;
    int minPlayers = (isSQDM) ? 5 : fromList.Count; // for SQDM, apply top/strong/weak only if team has 5 or more players

    // Exclude if TopScorers enabled and a top scorer on the team
    int topPlayersPerTeam = 0;
    if (balanceSpeed != Speed.Fast && (TopScorers || balanceSpeed == Speed.Slow)) {
        if (isSQDM) {
            int maxCount = fromList.Count;
            if (maxCount < 5) {
                topPlayersPerTeam = 0;
            } else if (maxCount < 8) {
                topPlayersPerTeam = 1;
            } else if (totalPlayerCount < 16) {
                topPlayersPerTeam = 2;
            } else {
                topPlayersPerTeam = 3;
            }
        } else {
            if (totalPlayerCount < 22) {
                topPlayersPerTeam = 1;
            } else if (totalPlayerCount > 42) {
                topPlayersPerTeam = 3;
            } else {
                topPlayersPerTeam = 2;
            }
        }
    }
    for (int i = 0; i < fromList.Count; ++i) {
        if (fromList[i].Name == player.Name) {
            if (!mustMove 
            && needsBalancing 
            && balanceSpeed != Speed.Fast 
            && fromList.Count >= minPlayers 
            && topPlayersPerTeam != 0 
            && i < topPlayersPerTeam) {
                String why = (balanceSpeed == Speed.Slow) ? "Speed is slow, excluding top scorers" : "Top Scorers enabled";
                if (!loggedStats) {
                    DebugBalance(GetPlayerStatsString(name));
                    loggedStats = true;
                }
                DebugBalance("Excluding ^b" + player.FullName + "^n: " + why + " and this player is #" + (i+1) + " on team " + GetTeamName(player.Team));
                fExcludedRound = fExcludedRound + 1;
                IncrementTotal();
                return;
            } else {
                playerIndex = i;
                break;
            }
        }
    }
    isStrong = (playerIndex < strongest);

    // Exclude if too soon since last move
    if (!mustMove && player.MovedByMBTimestamp != DateTime.MinValue) {
        double mins = DateTime.Now.Subtract(player.MovedByMBTimestamp).TotalMinutes;
        if (mins < MinutesAfterBeingMoved) {
            DebugBalance("Excluding ^b" + player.Name + "^n: last move was " + mins.ToString("F0") + " minutes ago, less than required " + MinutesAfterBeingMoved.ToString("F0") + " minutes");
            fExcludedRound = fExcludedRound + 1;
            IncrementTotal();
            return;
        } else {
            // reset
            player.MovedByMBTimestamp = DateTime.MinValue;
        }
    }

    // Exclude if player joined less than MinutesAfterJoining
    double joinedMinutesAgo = GetPlayerJoinedTimeSpan(player).TotalMinutes;
    double enabledForMinutes = now.Subtract(fEnabledTimestamp).TotalMinutes;
    if (!mustMove 
    && needsBalancing 
    && (enabledForMinutes > MinutesAfterJoining) 
    && balanceSpeed != Speed.Fast 
    && (joinedMinutesAgo < MinutesAfterJoining)) {
        if (!loggedStats) {
            DebugBalance(GetPlayerStatsString(name));
            loggedStats = true;
        }
        DebugBalance("Excluding ^b" + player.FullName + "^n: joined less than " + MinutesAfterJoining.ToString("F1") + " minutes ago (" + joinedMinutesAgo.ToString("F1") + ")");
        fExcludedRound = fExcludedRound + 1;
        IncrementTotal();
        return;   
    }

    // Exclude if in squad with same tags
    if (!mustMove && SameClanTagsInSquad) {
        int cmt =  CountMatchingTags(player, Scope.SameSquad);
        if (cmt >= 2) {
            String et = ExtractTag(player);
            DebugBalance("Excluding ^b" + name + "^n, " + cmt + " players in squad with tag [" + et + "]");
            fExcludedRound = fExcludedRound + 1;
            IncrementTotal();
            return;
        }
    }

    // Exclude if on friends list
    if (OnFriendsList) {
        int cmf = CountMatchingFriends(player);
        if (cmf >= 2) {
            DebugBalance("Excluding ^b" + player.FullName + "^n, " + cmf + " players in squad are friends (friendex = " + player.Friendex + ")");
            fExcludedRound = fExcludedRound + 1;
            IncrementTotal();
            return;
        }
    }

    // Exempt if this player already been moved for balance or unstacking
    if ((!mustMove && GetMoves(player) >= 1) || (mustMove && GetMoves(player) >= 2)) {
        DebugBalance("Exempting ^b" + name + "^n, already moved this round");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    /* Balance */

    int toTeamDiff = 0;
    int toTeam = ToTeam(name, player.Team, false, out toTeamDiff, ref mustMove); // take into account dispersal by Rank, etc.

    if (toTeam == 0 || toTeam == player.Team) {
        if (needsBalancing || mustMove) {
            if (DebugLevel >= 8) DebugBalance("Exempting ^b" + name + "^n, target team selected is same or zero");
            fExemptRound = fExemptRound + 1;
            IncrementTotal();
            return;
        }
    }

    int numTeams = 2; //(isSQDM) ? 4 : 2; // TBD, what is max squad size for SQDM?
    int maxTeamSlots = (MaximumServerSize/numTeams);
    int maxTeamPerMode = (perMode.MaxPlayers/numTeams);
    List<PlayerModel> lt = GetTeam(toTeam);
    int toTeamSize = (lt == null) ? 0 : lt.Count;

    if (toTeamSize == maxTeamSlots || toTeamSize == maxTeamPerMode) {
        if (DebugLevel >= 8) DebugBalance("Exempting ^b" + name + "^n, target team is full " + toTeamSize);
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    if (mustMove) DebugBalance("^4MUST MOVE^0 ^b" + name + "^n from " + GetTeamName(player.Team) + " to " + GetTeamName(toTeam));

    if (!mustMove && needsBalancing && toTeamDiff <= MaxDiff()) {
        DebugBalance("Exempting ^b" + name + "^n, difference between " + GetTeamName(player.Team) + " team and " + GetTeamName(toTeam) + " team is only " + toTeamDiff);
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }


    if ((fBalanceIsActive || mustMove) && toTeam != 0) {
        String ts = null;
        if (isSQDM) {
            ts = fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D)";
        } else {
            ts = fTeam1.Count + "(US) vs " + fTeam2.Count + "(RU)";
        }
        if (mustMove) {
            DebugBalance("Autobalancing because ^b" + name + "^n must be moved");
        } else {
            DebugBalance("Autobalancing because difference of " + diff + " is greater than " + MaxDiff() + ", [" + ts + "]");
        }
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

        // SQDM, not on the biggest team
        if (isSQDM && !mustMove && balanceSpeed != Speed.Fast && player.Team != biggestTeam) {
            // Make sure player's team isn't the same size as biggest
            List<PlayerModel> aTeam = GetTeam(player.Team);
            List<PlayerModel> bigTeam = GetTeam(biggestTeam);
            if (aTeam == null || bigTeam == null || (aTeam != null && bigTeam != null && aTeam.Count < bigTeam.Count)) {
                DebugBalance("Exempting ^b" + name + "^n, not on the biggest team");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }
        }

        // Strong/Weak exemptions and clan tag
        if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count >= minPlayers) {
            if (DebugLevel > 5) DebugBalance(strongMsg);
            // don't move weak player to losing team
            if (!isStrong  && toTeam == losingTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex+1) + " of " + fromList.Count + ", top " + (strongest) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // don't move strong player to winning team
            if (isStrong && toTeam == winningTeam) {
                DebugBalance("Exempting ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (strongest) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // Don't move to same team
            if (player.Team == toTeam) {
                if (DebugLevel >= 7) DebugBalance("Exempting ^b" + name + "^n, don't move player to his own team!");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }
        }

        /* Move for balance */

        int origTeam = player.Team;
        String origName = GetTeamName(player.Team);

        if (lastMoveFrom != 0) {
            origTeam = lastMoveFrom;
            origName = GetTeamName(origTeam);
        }
        
        MoveInfo move = new MoveInfo(name, player.Tag, origTeam, origName, toTeam, GetTeamName(toTeam));
        move.For = MoveType.Balance;
        move.Format(this, ChatMovedForBalance, false, false);
        move.Format(this, YellMovedForBalance, true, false);
        String why = (mustMove) ? "to disperse evenly" : ("because difference is " + diff);
        log = "^4^bBALANCE^n^0 moving ^b" + player.FullName + "^n from " + move.SourceName + " team to " + move.DestinationName + " team " + why;
        log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
        DebugWrite(log, 3);

        DebugWrite("^9" + move, 8);

        player.LastMoveFrom = player.Team;
        StartMoveImmediate(move, false);

        if (EnableLoggingOnlyMode) {
            // Simulate completion of move
            OnPlayerTeamChange(name, toTeam, 0);
            OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
        }
        // no increment total, handled later when move is processed
        return;
    }

    if (!fBalanceIsActive) {
        fLastBalancedTimestamp = now;
        if (DebugLevel >= 8) ConsoleDebug("fLastBalancedTimestamp = " + fLastBalancedTimestamp.ToString("HH:mm:ss"));
    }

    /* Unstack */

    if (!EnableUnstacking) {
        if (DebugLevel >= 8) DebugBalance("Unstack is disabled, Enable Unstacking is set to False");
        IncrementTotal();
        return;
    } else if (!fIsFullRound) {
        if (DebugLevel >= 7) DebugBalance("Unstack is disabled, not a full round");
        IncrementTotal();
        return;
    }

    if (winningTeam <= 0 || winningTeam >= fTickets.Length || losingTeam <= 0 || losingTeam >= fTickets.Length || balanceSpeed == Speed.Stop) {
        if (DebugLevel >= 5) DebugBalance("Skipping unstack for player that was killed ^b" + name +"^n: winning = " + winningTeam + ", losingTeam = " + losingTeam + ", speed = " + balanceSpeed);
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    }

    if (totalPlayerCount > (MaximumServerSize-2) || totalPlayerCount > (perMode.MaxPlayers-2)) {
        // TBD - kick idle players?
        if (DebugLevel >= 7) DebugBalance("No room to swap players for unstacking");
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    }

    if (perMode.CheckTeamStackingAfterFirstMinutes == 0) {
        if (DebugLevel >= 5) DebugBalance("Unstacking has been disabled, Check Team Stacking After First Minutes set to zero");
        IncrementTotal(); // no matching stat, reflect total deaths handled
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
        if (DebugLevel >= 6) DebugBalance("Maximum swaps have already occurred this round (" + (fUnstackedRound/2) + ")");
        fUnstackState = UnstackState.Off;
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    }

    double ratio = 1;
    double usPoints = 0;
    double ruPoints = 0;
    if (IsCTF()) {
        // Use team points, not tickets
        usPoints = GetTeamPoints(1);
        ruPoints = GetTeamPoints(2);
        if (usPoints <= 0) usPoints = 1;
        if (ruPoints <= 0) ruPoints = 1;
        ratio = (usPoints > ruPoints) ? (usPoints/ruPoints) : (ruPoints/usPoints);
    } else {
        // Otherwise use ticket ratio
        if (fTickets[losingTeam] >= 1) {
            if (IsRush()) {
                // normalize Rush ticket ratio
                double attackers = fTickets[1];
                double defenders = fMaxTickets - (fRushMaxTickets - fTickets[2]);
                defenders = Math.Max(defenders, attackers/2);
                ratio = (attackers > defenders) ? (attackers/defenders) : (defenders/attackers);
            } else {
                ratio = Convert.ToDouble(fTickets[winningTeam]) / Convert.ToDouble(fTickets[losingTeam]);
            }
        }
    }

    String um = "Ticket ratio " + (ratio*100.0).ToString("F0") + " vs. unstack ratio of " + (unstackTicketRatio*100.0).ToString("F0");

    if (unstackTicketRatio == 0 || ratio < unstackTicketRatio) {
        if (DebugLevel >= 6) DebugBalance("No unstacking needed: " + um);
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    }

    /*
    Cases:
    1) Never unstacked before, timer is 0 and group count is 0
    2) Within a group, timer is 0 and group count is > 0 but < max
    3) Between groups, timer is > 0 and group count is 0
    */

    double nsis = NextSwapGroupInSeconds(perMode); // returns 0 for case 1 and case 2

    if (nsis > 0) {
        if (DebugLevel >= 6) DebugBalance("Too soon to do another unstack swap group, wait another " + nsis.ToString("F1") + " seconds!");
        IncrementTotal(); // no matching stat, reflect total deaths handled
        return;
    } else {
        fFullUnstackSwapTimestamp = DateTime.MinValue; // turn off timer
    }

    // Are the minimum number of players present to decide strong vs weak?
    if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count < minPlayers) {
        DebugBalance("Not enough players in team to determine strong vs weak, skipping ^b" + name + "^n, ");
        fExemptRound = fExemptRound + 1;
        IncrementTotal();
        return;
    }

    // Otherwise, unstack!
    DebugBalance("^6Unstacking!^0 " + um);

    if (DebugLevel >= 6) {
        if (isStrong) {
            DebugBalance("Player ^b" + player.Name + "^n is strong: #" + (playerIndex+1) + " of " + fromList.Count + ", above #" + strongest + " at " + perMode.PercentOfTopOfTeamIsStrong.ToString("F0") + "%");
        } else {
            DebugBalance("Player ^b" + player.Name + "^n is weak: #" + (playerIndex+1) + " of " + fromList.Count + ", equal or below #" + strongest + " at " + perMode.PercentOfTopOfTeamIsStrong.ToString("F0") + "%");
        }
    }

    if (!loggedStats) {
        DebugBalance(GetPlayerStatsString(name));
        loggedStats = true;
    }

    MoveInfo moveUnstack = null;

    
    int origUnTeam = player.Team;
    String origUnName = GetTeamName(player.Team);
    String strength = "strong";

    if (lastMoveFrom != 0) {
        origUnTeam = lastMoveFrom;
        origUnName = GetTeamName(origUnTeam);
    }

    if (fUnstackState == UnstackState.Off) {
        // First swap
        DebugBalance("For ^b" + name + "^n, first swap of " + perMode.NumberOfSwapsPerGroup);
        fUnstackState = UnstackState.SwappedWeak;
    }

    switch (fUnstackState) {
        case UnstackState.SwappedWeak:
            // Swap strong to losing team
            if (isStrong) {
                // Don't move to same team
                if (player.Team == losingTeam) {
                    if (DebugLevel >= 6) DebugBalance("Skipping strong ^b" + name + "^n, don't move player to his own team!");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }
                DebugBalance("Sending strong player ^0^b" + player.FullName + "^n^9 to losing team " + GetTeamName(losingTeam));
                moveUnstack = new MoveInfo(name, player.Tag, origUnTeam, origUnName, losingTeam, GetTeamName(losingTeam));
                toTeam = losingTeam;
                fUnstackState = UnstackState.SwappedStrong;
            } else {
                DebugBalance("Skipping ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (strongest) + ")");
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
                    if (DebugLevel >= 6) DebugBalance("Skipping weak ^b" + name + "^n, don't move player to his own team!");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }
                DebugBalance("Sending weak player ^0^b" + player.FullName + "^n^9 to winning team " + GetTeamName(winningTeam));
                moveUnstack = new MoveInfo(name, player.Tag, origUnTeam, origUnName, winningTeam, GetTeamName(winningTeam));
                toTeam = winningTeam;
                fUnstackState = UnstackState.SwappedWeak;
                strength = "weak";
                FinishedFullSwap(name, perMode); // updates group count
            } else {
                DebugBalance("Skipping ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex+1) + " of " + fromList.Count + ", median " + (strongest) + ")");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }
            break;
        case UnstackState.Off:
            // fall thru
        default: return;
    }

    /* Move for unstacking */
    
    log = "^4^bUNSTACK^n^0 moving " + strength + " ^b" + player.FullName + "^n from " + moveUnstack.SourceName + " to " + moveUnstack.DestinationName + " because: " + um;
    log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
    DebugWrite(log, 3);
    moveUnstack.For = MoveType.Unstack;
    moveUnstack.Format(this, ChatMovedToUnstack, false, false);
    moveUnstack.Format(this, YellMovedToUnstack, true, false);

    DebugWrite("^9" + moveUnstack, 8);

    if (player.LastMoveFrom == 0) player.LastMoveFrom = player.Team;
    StartMoveImmediate(moveUnstack, false);

    if (EnableLoggingOnlyMode) {
        // Simulate completion of move
        OnPlayerTeamChange(name, toTeam, 0);
        OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
    }
    // no increment total, handled by unstacking move
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
    bool needsFetch = false;
    lock (fKnownPlayers) {
        PlayerModel player = null;
        if (!fKnownPlayers.ContainsKey(name)) {
            player = new PlayerModel(name, team);
            fKnownPlayers[name] = player;
            needsFetch = true;
        } else {
            player = fKnownPlayers[name];
            player.Team = team;
            known = true;
            needsFetch = !(player.TagVerified && player.StatsVerified);
        }
        if (player != null) player.LastSeenTimestamp = DateTime.Now;
    }
    lock (fAllPlayers) {
        if (!fAllPlayers.Contains(name)) fAllPlayers.Add(name);
    }
    if (needsFetch) {
        AddPlayerFetch(name);
    }
    return known;
}

private void RemovePlayer(String name) {
    bool gameChange = false;
    bool removeFetch = false;
    lock (fKnownPlayers) {
        if (fKnownPlayers.ContainsKey(name)) {
            // Keep around for MODEL_TIMEOUT minutes, in case player rejoins
            PlayerModel m = fKnownPlayers[name];
            m.ResetRound();
            m.LastSeenTimestamp = DateTime.Now;
            removeFetch = true;
        }
    }
    if (removeFetch) RemovePlayerFetch(name);
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
                    DebugWrite(state + " state, " + verb + " new player: ^b" + name, 4);
                } else {
                    DebugWrite(state + " state, unassigned player: ^b" + name, 4);
                    if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                    return;
                }
                break;
            case PluginState.Active:
                DebugWrite("Update waiting for ^b" + name + "^n to be assigned a team", 4);
                if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                return;
            case PluginState.Error:
                DebugWrite("Error state, adding new player: ^b" + name, 4);
                AddNewPlayer(name, team);
                break;
            default:
                return;
        }          
    }
   
    int unTeam = -2;
    PlayerModel m = null;

    lock (fKnownPlayers) {
        if (!fKnownPlayers.ContainsKey(name)) {
            ConsoleDebug("UpdatePlayerModel: player ^b" + name + "^n not in master table!");
            return;
        }
        m = fKnownPlayers[name];
    }

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
    m.KPMRound = m.KillsRound / mins;

    // Accumulated
    // TBD

    // Friends
    UpdatePlayerFriends(m);

    if (!EnableLoggingOnlyMode && unTeam != -2 && !fPendingTeamChange.ContainsKey(name)) {
        ConsoleDebug("UpdatePlayerModel:^b" + name + "^n has team " + unTeam + " but update says " + team + "!");
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
    
    PlayerModel m = GetPlayer(name);
    if (m == null) return;
    
    m.LastMoveFrom = 0; // reset

    if (m.Team != team) {
        if (m.Team == 0) {
            DebugWrite("Assigning ^b" + name + "^n to " + team, 4);
        } else {
            DebugWrite("^9Update player ^b" + name + "^n team from " + m.Team + " to " + team, 7);
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
        // cancel moves
        // rebuild the data model
        fPluginState = PluginState.Reconnected;
        DebugWrite("ValidateModel: ^b^3State = " + fPluginState, 6);  
        foreach (CPlayerInfo p in players) {
            try {
                UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank);
                CheckAbortMove(p.SoldierName);
            } catch (Exception e) {
                ConsoleException(e);
            }
        }
        fMoving.Clear();
        fReassigned.Clear();
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
    PlayerModel player = GetPlayer(name);
    if (player == null) return false;
    bool bogusMove = false;
    int lastMoveTo = 0;
    int lastMoveFrom = player.LastMoveFrom;

    // Same team?
    if (toTeam == player.Team) {
        /*
        This could happen with the following sequence of actions:
        + Player died and was moved from 1 to 2 for balance immediately, spawn messages set
        + While still dead, player switches himself back to 1 before respawning
        + All of this happens before a listPlayers refresh, so the model still thinks he is in team 1
        We have to detect that the switch is not to the intended team and fix everything up.
        */
        if (player.LastMoveTo != 0 && player.LastMoveTo != toTeam) {
            DebugUnswitch("Player team switch: ^b" + name + "^n trying to switch to " + GetTeamName(toTeam) + " during a plugin move to " + GetTeamName(player.LastMoveTo));
            bogusMove = true;
            lastMoveTo = player.LastMoveTo;
            player.LastMoveTo = 0;
            DebugUnswitch("Ovewriting previous chat message for ^b" + name + "^n: " + player.SpawnChatMessage);
            player.SpawnChatMessage = String.Empty;
            player.SpawnYellMessage = String.Empty;
        } else {
            DebugUnswitch("Player team switch: ^b" + name + "^n, player model already updated to " + GetTeamName(toTeam) + " team");
            return true;
        }
    } else {
        DebugUnswitch("Player team switch: ^b" + name + "^n from " + GetTeamName(player.Team) + " team to " + GetTeamName(toTeam) + " team");
    }

    // Check if move already in progress for this player and abort it
    bool sendAbortMessage = false;
    lock (fMoveStash) {
        if (fMoveStash.Count > 0) {
             // list only ever has one item
            if (fMoveStash[0].Name == name) {
                fMoveStash.Clear();
            }
        }
    }
    if (sendAbortMessage) {
        DebugUnswitch("ABORTED (by move stash): abort previous move by ^b" + name);
        sendAbortMessage = false;
    }
    
    // Whitelisted?
    if (OnWhitelist) {
        List<String> vip = new List<String>(fSettingWhitelist);
        if (EnableWhitelistingOfReservedSlotsList) vip.AddRange(fReservedSlots);
        List <String> tmp = new List<String>();
        // clean up the list
        foreach (String v in vip) {
            if (String.IsNullOrEmpty(v)) continue;
            if (v == INVALID_NAME_TAG_GUID) continue;
            tmp.Add(v);
        }
        vip = tmp;
        String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
        String xt = ExtractTag(player);
        if (String.IsNullOrEmpty(xt)) xt = INVALID_NAME_TAG_GUID;
        if (vip.Contains(name) || vip.Contains(xt) || vip.Contains(guid)) {
            DebugUnswitch("ALLOWED: On whitelist: ^b" + name);
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    }

    // Check forbidden cases
    PerModeSettings perMode = GetPerModeSettings();
    bool isSQDM = IsSQDM();
    bool isDispersal = IsDispersal(player);
    bool isRank = IsRankDispersal(player);
    bool forbidden = (((isDispersal || isRank) && Forbid(perMode, ForbidSwitchingAfterDispersal)) || (player.MovesByMBRound > 0 && !isSQDM && Forbid(perMode, ForbidSwitchingAfterAutobalance)));

    // Unlimited time?
    if (!forbidden && UnlimitedTeamSwitchingDuringFirstMinutesOfRound > 0 && GetTimeInRoundMinutes() < UnlimitedTeamSwitchingDuringFirstMinutesOfRound) {
        DebugUnswitch("ALLOWED: Time in round " + GetTimeInRoundMinutes().ToString("F0") + " < " + UnlimitedTeamSwitchingDuringFirstMinutesOfRound.ToString("F0"));
        SetSpawnMessages(name, String.Empty, String.Empty, false);
        CheckAbortMove(name);
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

    /*
    A player that was previously moved by the plugin is forbidden from moving to any
    other team by their own initiative for the rest of the round, unless this is
    SQDM mode. In SQDM, if a player is moved from A to B and then later decides
    to move to C, the losing team, that is allowed. Even in SQDM, though, no player
    is allowed to move to the winning team.
    
    All dispersal players are forbidden from moving themselves.
    */

    AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    int iFrom = 0;
    int iTo = 0;

    if (isSQDM) {
        // Moving to any team with fewer tickets is encouraged
        for (int i = 0; i < descendingTickets.Length; ++i) {
            if (fromTeam == descendingTickets[i]) iFrom = i;
            if (toTeam == descendingTickets[i]) iTo = i;
        }
        toLosing = (iTo > iFrom);
    } else {
        toLosing = (toTeam == losingTeam);
    }

    // Trying to switch to losing team?
    if (!forbidden && toLosing && toTeam != biggestTeam) {
        move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam));
        move.Format(this, ChatDetectedGoodTeamSwitch, false, true);
        move.Format(this, YellDetectedGoodTeamSwitch, true, true);
        DebugUnswitch("ALLOWED: Team switch to losing team ^b: " + name);
        SetSpawnMessages(name, move.ChatBefore, move.YellBefore, false);
        CheckAbortMove(name);
        return true;
    }

    if (isSQDM) {
        // Moving to any team with fewer players is encouraged
        for (int i = 0; i < ascendingSize.Length; ++i) {
            if (fromTeam == ascendingSize[i]) iFrom = i;
            if (toTeam == ascendingSize[i]) iTo = i;
        }
        toSmallest = (iTo < iFrom);
    } else {
        toSmallest = (toTeam == smallestTeam);
    }

    // Trying to switch to smallest team?
    if (!forbidden && toSmallest && toTeam != winningTeam) {
        move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam));
        move.Format(this, ChatDetectedGoodTeamSwitch, false, true);
        move.Format(this, YellDetectedGoodTeamSwitch, true, true);
        DebugUnswitch("ALLOWED: Team switch to smallest team ^b: " + name);
        SetSpawnMessages(name, move.ChatBefore, move.YellBefore, false);
        CheckAbortMove(name);
        return true;
    }

    // Adjust for SQDM
    if (isSQDM && fServerInfo != null) {
        if (GetPopulation(perMode, true) == Population.Low) {
            // Allow team switch to any team except biggest and winning
            if (!forbidden && toTeam != biggestTeam && toTeam != winningTeam) {
                DebugUnswitch("ALLOWED: SQDM Low population and not switching to biggest or winning team: ^b" + name);
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }
        }
    }

    // Allow if ticket/point difference is less than allowed margin
    double win = 0;
    double lose = 0;
    double margin = 100;
    if (IsCTF()) {
        win = GetTeamPoints(winningTeam);
        if (win == 0) win = 1;
        lose = GetTeamPoints(losingTeam);
        if (lose == 0) lose = 1;
        margin = ((win > lose) ? win/lose : lose/win);
        // margin is 110%
        if (!forbidden && (margin * 100) <= 110) {
            DebugUnswitch("ALLOWED: CTF move by ^b" + name + "^n because margin is only " + (margin*100).ToString("F0") + "%");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    } else {
        win = fTickets[winningTeam];
        if (win == 0) win = 1;
        lose = fTickets[losingTeam];
        if (lose == 0) lose = 1;
        margin = ((win > lose) ? win/lose : lose/win);
        // margin is 105%
        if (!forbidden && (margin * 100) <= 105) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because margin is only " + (margin*100).ToString("F0") + "%");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    }
    

    // Otherwise, do not allow the team switch
    int origTeam = player.Team;
    String origName = GetTeamName(player.Team);

    if (lastMoveFrom != 0 && toTeam != lastMoveFrom) {
        DebugUnswitch("Setting toTeam from " + GetTeamName(toTeam) + " to original LastMoveFrom = " + GetTeamName(lastMoveFrom));
        toTeam = lastMoveFrom;
    }

    if (bogusMove) {
        origTeam = lastMoveTo;
        origName = GetTeamName(lastMoveTo);
    }

    // select forbidden message from: moved by autobalance, moved to unstack, dispersal, ...
    String badChat = ChatDetectedBadTeamSwitch;
    String badYell = YellDetectedBadTeamSwitch;

    ForbidBecause why = ForbidBecause.None;

    if (player.MovesByMBRound > 0 && !isSQDM) {
        why = ForbidBecause.MovedByBalancer;
        if (!Forbid(perMode, ForbidSwitchingAfterAutobalance)) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Autobalance^n is False");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    } else if (toTeam == winningTeam) {
        why = ForbidBecause.ToWinning;
        if (!Forbid(perMode, ForbidSwitchingToWinningTeam)) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch To Winning Team^n is False");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    } else if (toTeam == biggestTeam) {
        why = ForbidBecause.ToBiggest;
        if (!Forbid(perMode, ForbidSwitchingToBiggestTeam)) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch To Biggest Team^n is False");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    } else if (isDispersal) {
        why = ForbidBecause.DisperseByList;
        if (!Forbid(perMode, ForbidSwitchingAfterDispersal)) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Dispersal^n is False");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    } else if (isRank) {
        why = ForbidBecause.DisperseByRank;
        if (!Forbid(perMode, ForbidSwitchingAfterDispersal)) {
            DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Dispersal^n is False");
            SetSpawnMessages(name, String.Empty, String.Empty, false);
            CheckAbortMove(name);
            return true;
        }
    }

    if (toTeam == origTeam) {
        ConsoleDebug("CheckTeamSwitch: ^b" + name + "^n, can't forbid unswitch to same team " + GetTeamName(toTeam) + "?");
        return true;
    }

    // Tried to switch toTeam from origTeam, so moving from toTeam back to origTeam
    move = new MoveInfo(name, player.Tag, toTeam, GetTeamName(toTeam), origTeam, origName);
    move.For = MoveType.Unswitch;
    move.Because = why;
    move.Format(this, badChat, false, true);
    move.Format(this, badYell, true, true);
    move.Format(this, ChatAfterUnswitching, false, false);
    move.Format(this, YellAfterUnswitching, true, false);
    player.LastMoveFrom = 0;

    if (DebugLevel >= 8) DebugUnswitch(move.ToString());

    if (isSQDM || !EnableImmediateUnswitch) {
        // Delay action until after the player spawns
        DebugUnswitch("FORBIDDEN: delaying unswitch action until spawn of ^b" + name + "^n from " + move.SourceName + " back to " + move.DestinationName);

        if (player.DelayedMove != null) {
            CheckAbortMove(name);
        }
        player.DelayedMove = move;

        if (!String.IsNullOrEmpty(player.SpawnChatMessage)) {
            DebugUnswitch("IGNORED: previously delayed spawn message for ^b" + name + "^n: " + player.SpawnChatMessage);
            SetSpawnMessages(name, String.Empty, String.Empty, false);
        }
    } else {
        // Do the move immediately
        DebugUnswitch("FORBIDDEN: immediately unswitch ^b" + name + "^n from " + move.SourceName + " back to " + move.DestinationName);
        String log = "^4^bUNSWITCHING^n^0 ^b" + player.FullName + "^n from " + move.SourceName + " back to " + move.DestinationName;
        log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
        DebugWrite(log, 3);
        StartMoveImmediate(move, true);
    }

    return false;
}

private void CheckAbortMove(String name) {
    lock (fMoveQ) {
        if (fMoveQ.Count > 0) {
            bool foundAbort = false;
            foreach (MoveInfo mi in fMoveQ) {
                if (mi.Name == name) {
                    mi.aborted = true;
                    foundAbort = true;
                }
            }
            if (foundAbort) Monitor.Pulse(fMoveQ);
        }
    }
    
    PlayerModel player = GetPlayer(name);
    if (player == null) return;

    if (player.DelayedMove != null) {
        DebugUnswitch("IGNORED: abort delayed move of ^b" + name + "^n to " + player.DelayedMove.DestinationName);
        player.DelayedMove = null;
    }
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
    }
}


private void StartMoveImmediate(MoveInfo move, bool sendMessages) {
    // Do an immediate move, also used by the move thread
    if (!fIsEnabled || fPluginState != PluginState.Active) {
        ConsoleDebug("StartMoveImmediate called while fIsEnabled is " + fIsEnabled + " or fPluginState is "  + fPluginState);
        return;
    }

    // Send before messages?
    if (sendMessages) {
        Yell(move.Name, move.YellBefore);
        Chat(move.Name, move.ChatBefore, (move.For == MoveType.Unswitch || QuietMode)); // player only if unswitch or Quiet
    }

    lock (fMoving) {
        if (!fMoving.ContainsKey(move.Name)) fMoving[move.Name] = move;
    }
    // Do the move
    if (!EnableLoggingOnlyMode) {
        int toSquad = ToSquad(move.Name, move.Destination);
        ServerCommand("admin.movePlayer", move.Name, move.Destination.ToString(), toSquad.ToString(), "false");
        ScheduleListPlayers(10);
    }

    // Remember move
    PlayerModel player = GetPlayer(move.Name);
    if (player != null) {
        if (player.LastMoveTo != 0) ConsoleDebug("StartMoveImmediate: ^b" + move.Name + "^n player.LastMoveTo != 0, " + player.LastMoveTo);
        player.LastMoveTo = move.Destination;
    }

    // Log move
    String r = null;
    switch (move.For) {
        case MoveType.Balance: r = " for balance"; break;
        case MoveType.Unstack: r = " to unstack teams"; break;
        case MoveType.Unswitch: r = " to unswitch player"; break;
        default: r = " for ???"; break;
    }
    String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1MOVING^0 " : "^b^1MOVING^0 ";
    DebugWrite(doing + move.Name + "^n from " + move.SourceName + " to " + move.DestinationName + r, 4);
}

private bool FinishMove(String name, int team) {
    // If this is an MB move, handle it
    MoveInfo move = null;
    lock (fMoving) {
        if (fMoving.ContainsKey(name)) {
            move = fMoving[name];
            fMoving.Remove(name);
            try {
                UpdatePlayerTeam(name, team);
                UpdateTeams();
                if (move.For == MoveType.Balance) {++fBalancedRound; IncrementMoves(name); IncrementTotal();}
                else if (move.For == MoveType.Unstack) {++fUnstackedRound; IncrementMoves(name); IncrementTotal();}
                else if (move.For == MoveType.Unswitch) {++fUnswitchedRound; UpdateMoveTime(name); IncrementTotal();}
            } catch (Exception e) {
                ConsoleException(e);
            }
        }
    }
    if (move != null) {
        // MB move for balance/unstacking/unswitching
        SetSpawnMessages(move.Name, move.ChatAfter, move.YellAfter, (move.For == MoveType.Unswitch || QuietMode));
    }
    return (move != null);
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

            // Check abort flag
            if (move.aborted) {
                DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", newer move in progress");
                continue;
            }

            // Sending before messages
            Yell(move.Name, move.YellBefore);
            Chat(move.Name, move.ChatBefore, (move.For == MoveType.Unswitch || QuietMode)); // player only if unswitching or Quiet

            // Stash for check later
            lock (fMoveStash) {
                fMoveStash.Clear();
                fMoveStash.Add(move);
            }

            // Pause
            Thread.Sleep(Convert.ToInt32(YellDurationSeconds*1000));
            if (!fIsEnabled) return;

            // Player may have started another move during the delay, check and abort
            lock (fMoveStash) {
                if (fMoveStash.Count == 0) {
                    DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", new move pending");
                    continue;
                }
                fMoveStash.Clear();
            }
            lock (fMoveQ) {
                foreach (MoveInfo mi in fMoveQ) {
                    if (mi.Name == move.Name) {
                        DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", now moving to " + mi.DestinationName);
                        continue;
                    }
                }
            }

            // Make sure player is dead
            if (!EnableLoggingOnlyMode) {
                ServerCommand("admin.killPlayer", move.Name);
                DebugWrite("^b^1ADMIN KILL^0 " + move.Name, 4);
            } else {
                DebugWrite("^9(SIMULATING) ^b^1ADMIN KILL^0 " + move.Name, 4);
            }

            // Pause
            Thread.Sleep(1*1000);
            if (!fIsEnabled) return;

            // Move player
            StartMoveImmediate(move, false);
        }
    } catch (Exception e) {
        ConsoleException(e);
    } finally {
        ConsoleWrite("^bMoveLoop^n thread stopped");
    }
}


private void Reassign(String name, int fromTeam, int toTeam, int diff) {
    // This is not a known player yet, so not PlayerModel to use
    // Just do a raw move as quickly as possible, not messages, just logging
    String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^4REASSIGNING^0^n new player ^b" : "^b^4REASSIGNING^0^n new player ^b";
    DebugWrite(doing + name + "^n from " + GetTeamName(fromTeam) + " team to " + GetTeamName(toTeam) + " team because difference is " + diff, 3);
    int toSquad = ToSquad(name, toTeam);
    if (!EnableLoggingOnlyMode) {
        fReassigned.Add(name);
        ServerCommand("admin.movePlayer", name, toTeam.ToString(), toSquad.ToString(), "false");
        ScheduleListPlayers(1);
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
    Chat(who, what, QuietMode);
}

private void Chat(String who, String what, bool quiet) {
    String doing = null;
    if (quiet) {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.say", what, "player", who); // chat player only
        }
        ProconChatPlayer(who, what);
        doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to ^b" : "^b^1CHAT^0^n to ^b";
        DebugWrite(doing + who + "^n: " + what, 4);
    } else {
        if (!EnableLoggingOnlyMode) {
            ServerCommand("admin.say", what, "all"); // chat all
        }
        ProconChat(what);
        doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to all: " : "^b^1CHAT^0^n to all: ";
        DebugWrite(doing + what, 4);
    }
}

private void Yell(String who, String what) {
    String doing = null;
    if (!EnableLoggingOnlyMode) {
        ServerCommand("admin.yell", what, YellDurationSeconds.ToString("F0"), "player", who); // yell to player
    }
    doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1YELL^0^n to ^b" : "^b^1YELL^0^n to ^b";
    DebugWrite(doing + who + "^n: " + what, 4);
}

private void ProconChat(String what) {
    if (EnableLoggingOnlyMode) what = "(SIMULATING) " + what;
    if (LogChat) ExecuteCommand("procon.protected.chat.write", "MB > All: " + what);
}

private void ProconChatPlayer(String who, String what) {
    if (EnableLoggingOnlyMode) what = "(SIMULATING) " + what;
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
            m.LastMoveTo = 0; // reset this value while we are here
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
    if (perMode == null) return Phase.Mid;
    // earlyTickets relative to max for count down, 0 for count up
    // lateTickets relative to 0 for count down, max for count up
    double earlyTickets = perMode.DefinitionOfEarlyPhaseFromStart;
    double lateTickets = perMode.DefinitionOfLatePhaseFromEnd;
    Phase phase = Phase.Mid;

    if (fServerInfo == null) return phase;

    if (AdjustForMetro(perMode)) {
        lateTickets = perMode.MetroAdjustedDefinitionOfLatePhase;
    }

    // Special handling for CTF mode
    if (IsCTF()) {
        if (fRoundStartTimestamp == DateTime.MinValue) return Phase.Early;

        double earlyMinutes = earlyTickets;
        double lateMinutes = lateTickets;

        // TBD - assume max round time is 20 minutes
        double maxMinutes = 20;
        //double totalRoundMins = DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes;
        double totalRoundMins = GetTimeInRoundMinutes();

        /* moved to ValidateSettings, keep here for reference
        // Late is higher priority than early
        if (lateMinutes > maxMinutes) {earlyMinutes = 0; lateMinutes = maxMinutes;}
        if (earlyMinutes > (maxMinutes - lateMinutes)) {earlyMinutes = maxMinutes - lateMinutes;}
        */

        if (totalRoundMins <= earlyMinutes) {
            phase = Phase.Early;
        } else if (totalRoundMins >= (maxMinutes - lateMinutes)) {
            phase = Phase.Late;
        } else {
            phase = Phase.Mid;
        }

        if (verbose && DebugLevel >= 8) ConsoleDebug("Phase: " + phase + " (" + totalRoundMins.ToString("F0") + " mins [" + earlyMinutes.ToString("F0") + " - " + (maxMinutes - lateMinutes).ToString("F0") + "])");
        return phase;
    }
    
    if (fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count == 0) return Phase.Mid;

    double tickets = -1;
    double goal = 0;
    bool countDown = true;

    if (fMaxTickets == -1) return Phase.Early;

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
        // Late takes priority over early
        if (lateTickets > fMaxTickets) {earlyTickets = 0; lateTickets = fMaxTickets;}
        if (lateTickets > (fMaxTickets - earlyTickets)) {earlyTickets = fMaxTickets - lateTickets;}

        if (tickets <= lateTickets) {
            phase = Phase.Late;
        } else if (fIsFullRound && (earlyTickets < fMaxTickets) && tickets >= (fMaxTickets - earlyTickets)) {
            phase = Phase.Early;
        } else {
            phase = Phase.Mid;
        }
    } else {
        // count up
        // Late takes priority over early
        if (lateTickets > goal) {earlyTickets = 0; lateTickets = goal;}
        if (earlyTickets > (goal - lateTickets)) {earlyTickets = goal - lateTickets;}

        if (lateTickets < goal && tickets >= (goal - lateTickets)) {
            phase = Phase.Late;
        } else if (tickets <= earlyTickets) {
            phase = Phase.Early;
        } else {
            phase = Phase.Mid;
        }
    }

    if (verbose && DebugLevel >= 8) ConsoleDebug("Phase: " + phase + " (" + tickets + " of " + fMaxTickets + " to " + goal + ", " + RemainingTicketPercent(tickets, goal).ToString("F0") + "%)");

    return phase;
}

private Population GetPopulation(PerModeSettings perMode, bool verbose) {
    if (fServerInfo == null) return Population.Medium;

    int highPop = perMode.DefinitionOfHighPopulationForPlayers;
    int lowPop = perMode.DefinitionOfLowPopulationForPlayers;
    Population pop = Population.Low;

    int totalPop = TotalPlayerCount;

    if (totalPop <= lowPop) {
        pop = Population.Low;
    } else if (totalPop >= highPop) {
        pop = Population.High;
    } else {
        pop = Population.Medium;
    }

    if (verbose && DebugLevel >= 8) ConsoleDebug("Population: " + pop + " (" + totalPop + " [" + lowPop + " - " + highPop + "])");

    return pop;
}

private double GetUnstackTicketRatio(PerModeSettings perMode) {
    Phase phase = GetPhase(perMode, false);
    Population pop = GetPopulation(perMode, false);
    double unstackTicketRatio = 0;

    if (perMode.CheckTeamStackingAfterFirstMinutes == 0) return 0;

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

    // apply rush adjustment
    if (IsRush() && fRushStage > 0 && fRushStage <= 5 && unstackTicketRatio > 100) {
        double adj = 0;
        switch (fRushStage) {
            case 1: adj = perMode.Stage1TicketPercentageToUnstackAdjustment; break;
            case 2: adj = perMode.Stage2TicketPercentageToUnstackAdjustment; break;
            case 3: adj = perMode.Stage3TicketPercentageToUnstackAdjustment; break;
            case 4: adj = perMode.Stage4And5TicketPercentageToUnstackAdjustment; break;
            case 5: adj = perMode.Stage4And5TicketPercentageToUnstackAdjustment; break;
            default: break;
        }
        if (adj != 0) unstackTicketRatio = unstackTicketRatio + adj;
    }
    
    if (unstackTicketRatio <= 100) unstackTicketRatio = 0;

    if (AdjustForMetro(perMode)) {
        double old = unstackTicketRatio;
        switch (phase) {
            case Phase.Early: unstackTicketRatio = 0; break;
            // case Phase.Mid: speed = Speed.Slow; break; // use whatever is specified
            case Phase.Late: unstackTicketRatio = 0; break;
        }
        if (old != unstackTicketRatio) ConsoleDebug("GetUnstackTicketRatio: Adjusted for Metro from " + old + " to " + unstackTicketRatio);
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
    if (AdjustForMetro(perMode)) {
        Speed old = speed;
        switch (phase) {
            case Phase.Early: speed = Speed.Stop; break;
            case Phase.Mid: speed = Speed.Slow; break;
            case Phase.Late: speed = Speed.Stop; break;
        }
        if (old != speed) ConsoleDebug("GetBalanceSpeed: Adjusted for Metro from " + old + " to " + speed);
    }
    return speed;
}

private void SetTag(PlayerModel player, Hashtable data) {
    if (data == null) {
        player.TagFetchStatus.State = FetchState.Failed;
        ConsoleDebug("SetTag ^b" + player.Name + "^n data = null");
        return;
    }
    player.TagFetchStatus.State = FetchState.Succeeded;
    player.TagVerified = true;

    if (!data.ContainsKey("clanTag") || ((String)data["clanTag"] == null)) {
        DebugFetch("Request clanTag(^b" + player.Name + "^n), no clanTag key in data");
        return;
    }

    player.Tag = (String)data["clanTag"];
}

private void SetStats(PlayerModel player, Hashtable stats) {
    player.StatsFetchStatus.State = FetchState.Failed;
    if (stats == null) {
        ConsoleDebug("SetStats ^b" + player.Name + "^n stats = null");
        return;
    }

    Dictionary<String,double> propValues = new Dictionary<String,double>();
    propValues["kdRatio"] = -1;
    propValues["timePlayed"] = -1;
    propValues["kills"] = -1;
    propValues["scorePerMinute"] = -1;
    propValues["rsDeaths"] = -1;
    propValues["rsKills"] = -1;
    propValues["rsScore"] = -1;
    propValues["rsTimePlayed"] = -1;
    
    foreach (DictionaryEntry entry in stats) {
        try {
            String entryKey = (String)(entry.Key.ToString());

            // skip entries we are not interested in 
            if (!propValues.ContainsKey(entryKey)) continue;

            String entryValue = (String)(entry.Value.ToString());

            double dValue = -1;
            Double.TryParse(entryValue, out dValue);
            propValues[entryKey] = (Double.IsNaN(dValue)) ? -1 : dValue;
        } catch (Exception) {}
    }

    // Now set the player values, starting with AllTime
    double allTimeMinutes = Math.Max(1, propValues["timePlayed"] / 60);
    player.KDR = propValues["kdRatio"];
    player.SPM = propValues["scorePerMinute"];
    player.KPM = propValues["kills"] / allTimeMinutes;

    // Using Reset?
    String type = "All-Time";
    if (WhichBattlelogStats == BattlelogStats.Reset && propValues["rsTimePlayed"] > 0) {
        type = "Reset";
        double resetMinutes = Math.Max(1, propValues["rsTimePlayed"] / 60);
        double resetKDR = propValues["rsKills"] / Math.Max(1, propValues["rsDeaths"]);
        if (resetKDR > 0) player.KDR = resetKDR;
        double resetSPM = propValues["rsScore"] / resetMinutes;
        if (resetSPM > 0) player.SPM = resetSPM;
        double resetKPM = propValues["rsKills"] / resetMinutes; 
        if (resetKPM > 0) player.KPM = resetKPM;
    }
    player.StatsFetchStatus.State = FetchState.Succeeded;
    player.StatsVerified = true;
    String msg = type + " [bKDR:" + player.KDR.ToString("F2") + ", bSPM:" + player.SPM.ToString("F0") + ", bKPM:" + player.KPM.ToString("F1") + "]";
    DebugFetch("Player stats updated ^0^b" + player.Name + "^n, " + msg);
}


private void Scrambler(List<TeamScore> teamScores) {
    // Check all the reasons not to scramble
    if (fServerInfo == null) return;

    PerModeSettings perMode = GetPerModeSettings();

    if (!perMode.EnableScrambler) return;

    int current = fServerInfo.CurrentRound + 1; // zero based index
    if (OnlyOnNewMaps && current < fServerInfo.TotalRounds) {
        DebugScrambler("Only scrambling new maps and this is only round " + current + " of " + fServerInfo.TotalRounds);
        return;
    }

    if (IsSQDM()) {
        DebugScrambler("SQDM can't be scrambled");
        return;
    }

    if (TotalPlayerCount < 16) {
        DebugScrambler("Not enough players to scramble, at least 16 required: " + TotalPlayerCount);
        return;
    }

    if (!IsCTF() && OnlyOnFinalTicketPercentage > 100) {
        if (teamScores == null || teamScores.Count != 2) {
            DebugScrambler("DEBUG: no final team scores");
            return;
        }
        bool countDown = true;

        if (fMaxTickets == -1) return;

        double goal = fMaxTickets;
        double a = teamScores[0].Score;
        double b = teamScores[1].Score;

        
        if (IsRush()) {
            // normalize Rush ticket ratio
            b = fMaxTickets - (fRushMaxTickets - b);
            b = Math.Max(b, 1);
        }

        if (Regex.Match(fServerInfo.GameMode, @"(?:TeamDeathMatch|SquadDeathMatch)").Success) {
            countDown = false;
            goal = teamScores[0].WinningScore;
        }

        double ratio = 0;
        if (countDown) {
            // ratio of difference from max
            if (a < b) {
                ratio = (goal - a) / Math.Max(1, (goal - b)); 
                DebugScrambler("Ratio us/ru: " + a + " vs " + b + " <- [" + goal + "]: " + (goal-a) + "/" + Math.Max(1, (goal-b)) + " = " + ratio.ToString("F2"));
            } else {
                ratio = (goal - b) / Math.Max(1, (goal - a));
                DebugScrambler("Ratio ru/us: " + a + " vs " + b + " <- [" + goal + "]: " + (goal-b) + "/" + Math.Max(1, (goal-a)) + " = " + ratio.ToString("F2"));
            }
        } else {
            // direct ratio
            if (a > b) {
                ratio = a / Math.Max(1, b);
                DebugScrambler("Ratio us/ru: " + a + " vs " + b + " -> [" + goal + "]: " + a + "/" + Math.Max(1, b) + " = " + ratio.ToString("F2"));
            } else {
                ratio = b / Math.Max(1, a);
                DebugScrambler("Ratio ru/us: " + a + " vs " + b + " -> [" + goal + "]: " + b + "/" + Math.Max(1, a) + " = " + ratio.ToString("F2"));
            }
        } 
        if ((ratio * 100) < OnlyOnFinalTicketPercentage) {
            DebugScrambler("Only On Final Ticket Percentage >= " + OnlyOnFinalTicketPercentage.ToString("F0") + "%, but ratio is only " + (ratio * 100).ToString("F0") + "%");
            return;
        } else {
            DebugScrambler("Only On Final Ticket Percentage >= " + OnlyOnFinalTicketPercentage.ToString("F0") + "% and ratio is " + (ratio * 100).ToString("F0") + "%");
        }
    }

    DebugScrambler("Scrambling teams by " + ScrambleBy + " in " + DelaySeconds.ToString("F0") + " seconds");

    try {
        fDebugScramblerBefore[0].Clear();
        fDebugScramblerBefore[1].Clear();
        fDebugScramblerAfter[0].Clear();
        fDebugScramblerAfter[1].Clear();
    } catch (Exception e) {
        ConsoleException(e);
    }

    // Activate the scrambler thread
    lock (fScramblerLock) {
        fScramblerLock.MaxDelay = DelaySeconds;
        fScramblerLock.LastUpdate = DateTime.Now;
        Monitor.Pulse(fScramblerLock);
    }
}

private void ScramblerLoop () {
    /*
    Strategy: Scan each team and build filtered team and optionally squad lists.
    The ScrambleBy metric of each item in the pool is calculated. The pool is
    sorted according to the ScrambleBy setting. The best player/squad is assigned
    to the losing team, then the team total is calculated. More strong players/squads
    are added to the losing team until its metric sum is greater than the winning team,
    then players/squads are added to the winning team until it is greater, and so on.
    If at any time a team is full, the remainder of the players/squads are added to 
    the other team.
    
    Finally, each member of the new team is checked and if they need to be moved,
    a move command is issued.  Since this is between rounds, a special move command
    that bypasses all move tracking is used.
    */
    try {
        DateTime last = DateTime.MinValue;
        while (fIsEnabled) {
            double delay = 0;
            DateTime since = DateTime.MinValue;
            lock (fScramblerLock) {
                while (fScramblerLock.MaxDelay == 0) {
                    Monitor.Wait(fScramblerLock);
                    if (!fIsEnabled) return;
                }
                delay = fScramblerLock.MaxDelay;
                since = fScramblerLock.LastUpdate;
                fScramblerLock.MaxDelay = 0;
                fScramblerLock.LastUpdate = DateTime.MinValue;
            }

            if (since == DateTime.MinValue) continue;

            if (last != DateTime.MinValue && DateTime.Now.Subtract(last).TotalMinutes < 5) {
                DebugScrambler("^0Last scramble was less than 5 minutes ago, skipping!");
                continue;
            }

            try {

                PerModeSettings perMode = GetPerModeSettings();

                // wait specified number of seconds
                if (delay > 0) {
                    bool listUpdated = false;
                    while (DateTime.Now.Subtract(since).TotalSeconds < delay) {
                        try {
                            if (!listUpdated && delay - DateTime.Now.Subtract(since).TotalSeconds <= 5) {
                                // update the player list within 5 seconds of the delay expiring
                                listUpdated = true;
                                DebugScrambler("Last chance player list update, account for players who have left");
                                ServerCommand("admin.listPlayers", "all");
                            }
                        } catch (Exception) {}
                        Thread.Sleep(1000); // 1 second
                        if (!fIsEnabled) return;
                    }
                }

                String extra = String.Empty;
                if (DivideBy == DivideByChoices.ClanTag) extra = " [" + ClanTagToDivideBy + "]";
                String kst = String.Empty;
                if (KeepSquadsTogether) kst = ", KeepSquadsTogether";
                String kctiss = String.Empty;
                if (KeepClanTagsInSameSquad) kctiss = ", KeepClansTagsInSameSquad";
                DebugScrambler("Starting scramble of " + TotalPlayerCount + " players, winner was " + GetTeamName(fWinner));
                DebugScrambler("Using (" + ScrambleBy + kst + kctiss + ", DivideBy = " + DivideBy + extra + ")");
                last = DateTime.Now;

                // Build a filtered list
                List<String> toScramble = new List<String>();
                //List<String> exempt = new List<String>();
                PlayerModel player = null;

                lock (fAllPlayers) {
                    foreach (String egg in fAllPlayers) {
                        try {
                            player = GetPlayer(egg);
                            if (player == null) continue;

                            // For debugging
                            if (IsKnownPlayer(player.Name) && player.Team > 0 && player.Team <= 2) {
                                fDebugScramblerBefore[player.Team-1].Add(player.ClonePlayer());
                            } else continue; // skip joining players

                            // Add this player to list of scramblers
                            toScramble.Add(egg);
                        } catch (Exception e) {
                            if (DebugLevel >= 8) ConsoleException(e);
                        }
                    }
                }

                if (toScramble.Count == 0) continue;

                // Build squad table and overall list
                List<SquadRoster> all = new List<SquadRoster>();
                Dictionary<int,SquadRoster> squads = new Dictionary<int,SquadRoster>(); // key int is (team * 1000) + squad
                List<PlayerModel> loneWolves = new List<PlayerModel>();
                int key = 0;
                String debugMsg = String.Empty;

                foreach (String egg in toScramble) {
                    try {
                        if (!IsKnownPlayer(egg)) continue; // might have left while we were working
                        player = GetPlayer(egg);
                        if (player == null) continue;
                        if (player.Team < 1) continue; // skip players that are still joining
                        if (player.Squad < 1) continue; // skip players not in a squad
                        key = 9000; // free pool
                        int squadId = player.Squad;
                        if (KeepSquadsTogether) {
                            key = (Math.Max(0, player.Team) * 1000) + Math.Max(0, player.Squad);
                            if (key < 1000) {
                                loneWolves.Add(player);
                                continue;
                            } else {
                                DebugScrambler("Keeping ^b" + player.FullName + "^n together with squad, using key " + key);
                            }
                            AddPlayerToSquadRoster(squads, player, key, squadId, true);
                        } else if (KeepClanTagsInSameSquad) {
                            int nt = CountMatchingTags(player, Scope.SameSquad);
                            String tt = ExtractTag(player);
                            if (tt == null) tt = String.Empty;
                            if (nt >= 2) {
                                key = (Math.Max(0, player.Team) * 1000) + Math.Max(0, player.Squad); // 0 is okay, makes lone-wolf pool
                                if (key < 1000) {
                                    loneWolves.Add(player);
                                    continue;
                                } else {
                                    DebugScrambler("Keeping ^b" + player.Name + "^n together with " + nt + " tags [" + tt + "] with squad, using key " + key);
                                }
                            } else {
                                loneWolves.Add(player);
                                continue;
                            }
                            AddPlayerToSquadRoster(squads, player, key, squadId, true);
                        } else {
                            loneWolves.Add(player);
                        }
                    } catch (Exception e) {
                        if (DebugLevel >= 8) ConsoleException(e);
                    }
                }

                // Add lone wolves to empty squads
                int emptyId = 1;
                SquadRoster home = null;
                bool filling = false;
                foreach (PlayerModel wolf in loneWolves) {
                    bool goback = true;
                    while (goback) {
                        if (!filling) {
                            // Need to find an empty squad
                            key = (wolf.Team * 1000) + emptyId;
                            while (squads.ContainsKey(key)) {
                                emptyId = emptyId + 1;
                                if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                                key = (wolf.Team * 1000) + emptyId;
                            }
                            filling = true;
                            DebugScrambler("For both teams, using empty squad " + emptyId);
                        }
                        if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                        if (filling) {
                            // Add wolf to the squad we are filling until full
                            key = (wolf.Team * 1000) + emptyId;
                            home = AddPlayerToSquadRoster(squads, wolf, key, emptyId, false);
                            if (home == null || !home.Roster.Contains(wolf)) {
                                // Full
                                filling = false;
                                continue;
                            } else {
                                // Next wolf
                                DebugScrambler("Lone wolf ^b" + wolf.Name + "^n filled in empty squad " + wolf.Team + "/" + emptyId);
                                goback = false;
                                continue;
                            }
                        }
                    }
                }

                // Sum up the metric for each squad
                foreach (int k in squads.Keys) {
                    SquadRoster sr = squads[k];
                    switch (ScrambleBy) {
                        case DefineStrong.RoundScore:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.ScoreRound;
                            }
                            break;
                        case DefineStrong.RoundSPM:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.SPMRound;
                            }
                            break;
                        case DefineStrong.RoundKills:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.KillsRound;
                            }
                            break;
                        case DefineStrong.RoundKDR:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.KDRRound;
                            }
                            break;
                        case DefineStrong.PlayerRank:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.Rank;
                            }
                            break;
                        case DefineStrong.RoundKPM:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.KPMRound;
                            }
                            break;
                        case DefineStrong.BattlelogSPM:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
                            }
                            break;
                        case DefineStrong.BattlelogKDR:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
                            }
                            break;
                        case DefineStrong.BattlelogKPM:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
                            }
                            break;
                        default:
                            foreach (PlayerModel p in sr.Roster) {
                                sr.Metric = sr.Metric + p.ScoreRound;
                            }
                            break;
                    }

                    String ot = (sr.Roster[0].Team == 1) ? "US" : "RU";
                    DebugScrambler(ot + "/" + SQUAD_NAMES[sr.Squad] + " " + ScrambleBy + ":" + sr.Metric.ToString("F1"));

                    switch (DivideBy) {
                        case DivideByChoices.ClanTag:
                            foreach (PlayerModel p in sr.Roster) {
                                if (!String.IsNullOrEmpty(ClanTagToDivideBy) && ExtractTag(p) == ClanTagToDivideBy) sr.ClanTagCount  = sr.ClanTagCount + 1;
                            }
                            debugMsg = " ClanTag[" + ClanTagToDivideBy + "] " + sr.ClanTagCount;
                            break;
                        case DivideByChoices.DispersalGroup: {
                            int[] gCount = new int[3]{0,0,0};
                            foreach (PlayerModel p in sr.Roster) {
                                if (IsDispersal(p)) {
                                    if (p.DispersalGroup == 1 || p.DispersalGroup == 2) {
                                        gCount[p.DispersalGroup] = gCount[p.DispersalGroup] + 1;
                                    }
                                }
                            }
                            if (gCount[1] != 0 || gCount[2] != 0) {
                                sr.DispersalGroup = (gCount[1] > gCount[2]) ? 1 : 2;
                            }
                            debugMsg = " Dispersal Group = " + sr.DispersalGroup;
                            break;
                        }
                        case DivideByChoices.None:
                        default:
                            break;
                    }

                    DebugScrambler(ot + "/" + SQUAD_NAMES[sr.Squad] + " " + DivideBy + ": " + debugMsg);

                    all.Add(sr);
                }
                squads.Clear();

                if (all.Count == 0) continue;

                // Sort squads
                all.Sort(DescendingMetricSquad);

                DebugScrambler("After sorting:");
                foreach (SquadRoster ds in all) {
                    String oldt = (ds.Roster[0].Team == 1) ? "US" : "RU";
                    DebugScrambler("    " + ScrambleBy + ":" + ds.Metric.ToString("F1") + " " + oldt + "/" + SQUAD_NAMES[ds.Squad]);
                }
            
                // Prepare the new team lists
                List<PlayerModel> usScrambled = new List<PlayerModel>();
                Dictionary<int,SquadRoster> usSquads = new Dictionary<int,SquadRoster>();
                double usMetric = 0;
                List<PlayerModel> ruScrambled = new List<PlayerModel>();
                Dictionary<int,SquadRoster> ruSquads = new Dictionary<int,SquadRoster>();
                double ruMetric = 0;


                // Dole out squads, keeping metric in balance, starting with the losing team
                List<PlayerModel> target = (fWinner == 0 || fWinner == 1) ? ruScrambled : usScrambled;
                Dictionary<int,SquadRoster> squadTable = (fWinner == 0 || fWinner == 1) ? ruSquads : usSquads;
                int teamMax = MaximumServerSize/2;
                debugMsg = String.Empty;

                // Pre-process DivideBy setting
                if (DivideBy == DivideByChoices.DispersalGroup) {
                    // Skim the dispersal squads off the top
                    List<PlayerModel> localTarget = null;
                    List<SquadRoster> copy = new List<SquadRoster>(all);
                    foreach (SquadRoster disp in copy) {
                        if (disp.DispersalGroup == 1 && usScrambled.Count < teamMax) {
                            localTarget = usScrambled;
                            debugMsg = "US";
                        } else if (disp.DispersalGroup == 2 && ruScrambled.Count < teamMax) {
                            localTarget = ruScrambled;
                            debugMsg = "RU";
                        } else {
                            continue;
                        }
                        DebugScrambler("Squad " + SQUAD_NAMES[disp.Squad] + ", Dispersal Group " + disp.DispersalGroup + " to " + debugMsg);
                        AssignSquadToTeam(disp, squadTable, usScrambled, ruScrambled, localTarget);
                        all.Remove(disp);
                    }
                    if (usScrambled == target && target.Count >= teamMax) target = ruScrambled;
                    if (ruScrambled == target && target.Count >= teamMax) target = usScrambled;
                }

                foreach (SquadRoster squad in all) { // strongest to weakest

                    AssignSquadToTeam(squad, squadTable, usScrambled, ruScrambled, target);
                    /*
                    if (usScrambled.Count >= teamMax && ruScrambled.Count >= teamMax) {
                        DebugScrambler("BOTH teams full! Skipping remaining free pool!");
                        break;
                    }
                    int wasSquad = squad.Roster[0].Squad;

                    // Remap if there is a squad collision
                    if (squadTable.ContainsKey(squad.Squad)) {
                        RemapSquad(squadTable, squad);
                    }
                    squadTable[squad.Squad] = squad;
                    int wasTeam = squad.Roster[0].Team;
                    String st = (wasTeam == 1) ? "US" : "RU";
                    String gt = (target == usScrambled) ? "US" : "RU";
                    DebugScrambler(st + "/" + SQUAD_NAMES[wasSquad] + " scrambled to " + gt + "/" + SQUAD_NAMES[squad.Squad]);

                    // Assign the squad to the target team
                    foreach (PlayerModel p in squad.Roster) {
                        p.ScrambledSquad = squad.Squad;
                        if (target.Count < teamMax && IsKnownPlayer(p.Name)) target.Add(p);
                    }
                    */

                    // Recalc team metrics
                    SumMetricByTeam(usScrambled, ruScrambled, out usMetric, out ruMetric);
                    if (DebugLevel >= 6) ConsoleDebug("Updated scrambler metrics " + ScrambleBy + ": US(" + usScrambled.Count + ") = " + usMetric.ToString("F1") + ", RU(" + ruScrambled.Count + ") = " + ruMetric.ToString("F1"));
                    if (usScrambled.Count >= teamMax && ruScrambled.Count < teamMax) {
                        target = ruScrambled;
                        squadTable = ruSquads;
                        debugMsg = "Original target = RU";
                    } else if (ruScrambled.Count >= teamMax && usScrambled.Count < teamMax) {
                        target = usScrambled;
                        squadTable = usSquads;
                        debugMsg = "Original target = US";
                    } else if (usMetric < ruMetric) {
                        target = usScrambled;
                        squadTable = usSquads;
                        debugMsg = "Original target = US";
                    } else {
                        target = ruScrambled;
                        squadTable = ruSquads;
                        debugMsg = "Original target = RU";
                    }
                }

                if (!fIsEnabled) return;

                // Now run through each list and move any players that need moving
                ScrambleMove(usScrambled, 1);
                ScrambleMove(ruScrambled, 2);

                ScheduleListPlayers(1); // refresh

                // For debugging
                foreach (PlayerModel dude in usScrambled) {
                    if (!IsKnownPlayer(dude.Name)) continue;
                    player = dude.ClonePlayer();
                    player.Squad = player.ScrambledSquad;
                    fDebugScramblerAfter[0].Add(player);
                }
                foreach (PlayerModel dude in ruScrambled) {
                    if (!IsKnownPlayer(dude.Name)) continue;
                    player = dude.ClonePlayer();
                    player.Squad = player.ScrambledSquad;
                    fDebugScramblerAfter[1].Add(player);
                }

                DebugScrambler("DONE!");
                if (DebugLevel >= 6) CommandToLog("scrambled");
            } catch (Exception e) {
                ConsoleException(e);
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
    } finally {
        ConsoleWrite("^bScramblerLoop^n thread stopped");
    }
}

private void AssignSquadToTeam(SquadRoster squad, Dictionary<int,SquadRoster> squadTable, List<PlayerModel> usScrambled, List<PlayerModel> ruScrambled, List<PlayerModel> target) {
        int teamMax = MaximumServerSize/2;

        if (usScrambled.Count >= teamMax && ruScrambled.Count >= teamMax) {
            DebugScrambler("BOTH teams full! Skipping remaining free pool!");
            return;
        }
        int wasSquad = squad.Roster[0].Squad;

        // Remap if there is a squad collision
        if (squadTable.ContainsKey(squad.Squad)) {
            RemapSquad(squadTable, squad);
        }
        squadTable[squad.Squad] = squad;
        int wasTeam = squad.Roster[0].Team;
        String st = (wasTeam == 1) ? "US" : "RU";
        String gt = (target == usScrambled) ? "US" : "RU";
        DebugScrambler(st + "/" + SQUAD_NAMES[wasSquad] + " scrambled to " + gt + "/" + SQUAD_NAMES[squad.Squad]);

        // Assign the squad to the target team
        foreach (PlayerModel p in squad.Roster) {
            p.ScrambledSquad = squad.Squad;
            if (target.Count < teamMax && IsKnownPlayer(p.Name)) target.Add(p);
        }
}


private void ScrambleMove(List<PlayerModel> scrambled, int where) {
    int toSquad = 0;
    int toTeam = where;

    // Move to available squad
    foreach (PlayerModel dude in scrambled) {
        if (!IsKnownPlayer(dude.Name)) continue; // might have left
        String xt = ExtractTag(dude);
        String name = dude.Name;
        if (!String.IsNullOrEmpty(xt)) name = "[" + xt + "]" + name;
        toSquad = dude.ScrambledSquad;
        if (toSquad < 0 || toSquad > (SQUAD_NAMES.Length - 1)) {
            ConsoleDebug("ScrambleMove: why is ^b" + name + "^n scrambled to squad " + toSquad + "?");
            continue;
        }
        if (toSquad == 0) {
            ConsoleDebug("ScrambleMove: why is ^b" + name + "^n scrambled to squad 0?");
            continue;
        }
        if (dude.Team == toTeam && dude.Squad == toSquad) continue;

        // Do the move
        if (!EnableLoggingOnlyMode) {
            DebugScrambler("^1^bMOVE^n^0 ^b" + name + "^n to " + GetTeamName(toTeam) + " team, squad " + SQUAD_NAMES[toSquad]);
            ServerCommand("admin.movePlayer", dude.Name, toTeam.ToString(), toSquad.ToString(), "false");
            Thread.Sleep(20);
        } else {
            DebugScrambler("^9(SIMULATED) ^1^bMOVE^n^0 ^b" + name + "^n to " + GetTeamName(toTeam) + " team, squad " + SQUAD_NAMES[toSquad]);
        }
    }
}

private SquadRoster AddPlayerToSquadRoster(Dictionary<int,SquadRoster> squads, PlayerModel player, int key, int squadId, bool ignoreSize) {
    SquadRoster squad = null;
    if (squads.TryGetValue(key, out squad)) {
        if (ignoreSize || squad.Roster.Count < 4) {
            squad.Roster.Add(player);
        }
    } else {
        squad = new SquadRoster(squadId);
        squad.Roster.Add(player);
        squads[key] = squad;
    }
    return squad;
}

private void SumMetricByTeam(List<PlayerModel> usScrambled, List<PlayerModel> ruScrambled, out double usMetric, out double ruMetric) {
    usMetric = 0;
    ruMetric = 0;
    // sum up the metric by team
    switch (ScrambleBy) {
        case DefineStrong.RoundScore:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.ScoreRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.ScoreRound;
            }
            break;
        case DefineStrong.RoundSPM:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.SPMRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.SPMRound;
            }
            break;
        case DefineStrong.RoundKills:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.KillsRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.KillsRound;
            }
            break;
        case DefineStrong.RoundKDR:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.KDRRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.KDRRound;
            }
            break;
        case DefineStrong.PlayerRank:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.Rank;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.Rank;
            }
            break;
        case DefineStrong.RoundKPM:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.KPMRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.KPMRound;
            }
            break;
        case DefineStrong.BattlelogSPM:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
            }
            break;
        case DefineStrong.BattlelogKDR:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
            }
            break;
        case DefineStrong.BattlelogKPM:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
            }
            break;
        default:
            foreach (PlayerModel p in usScrambled) {
                usMetric = usMetric + p.ScoreRound;
            }
            foreach (PlayerModel p in ruScrambled) {
                ruMetric = ruMetric + p.ScoreRound;
            }
            break;
    }
}

private void RemapSquad(Dictionary<int,SquadRoster> squadTable, SquadRoster squad) {
    int emptyId = 1;
    while (squadTable.ContainsKey(emptyId)) {
        emptyId = emptyId + 1;
        if (emptyId > (SQUAD_NAMES.Length - 1)) {
            if (DebugLevel >= 8) ConsoleDebug("RemapSquad: ran out of empty squads!");
            return;
        }
    }
    squad.Squad = emptyId;
}















/* ======================== BATTLELOG ============================= */














private void AddPlayerFetch(String name) {
    if (!EnableBattlelogRequests) return;
    if (String.IsNullOrEmpty(name)) return;
    PlayerModel player = GetPlayer(name);
    if (player == null) return;
    if (player.TagFetchStatus.State != FetchState.New && player.TagFetchStatus.State != FetchState.InQueue) {
        DebugFetch("Cannot refetch tag for player ^b" + player.Name + "^n, previous result was " + player.TagFetchStatus.State);
        if (WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
    }
    if (player.StatsFetchStatus.State != FetchState.New && player.TagFetchStatus.State != FetchState.InQueue) {
        DebugFetch("Cannot refetch stats for player ^b" + player.Name + "^n, previous result was " + player.StatsFetchStatus.State);
        return;
    }
    player.TagFetchStatus.State = FetchState.InQueue;
    player.StatsFetchStatus.State = FetchState.InQueue;
    lock (fPriorityFetchQ) {
        if (!fPriorityFetchQ.Contains(name)) {
            fPriorityFetchQ.Enqueue(name);
            Monitor.Pulse(fPriorityFetchQ);
        } 
    }
}

private void RemovePlayerFetch(String name) {
    if (String.IsNullOrEmpty(name)) return;
    PlayerModel player = GetPlayer(name);
    if (player == null) return;
    lock (fPriorityFetchQ) {
        if (fPriorityFetchQ.Contains(name)) {
            player.TagFetchStatus.State = FetchState.Aborted;
            player.StatsFetchStatus.State = FetchState.Aborted;
        }
    }
}

public void FetchLoop() {
    try {
        DateTime since = DateTime.MinValue;
        int requests = 1;

        while (fIsEnabled) {
            String name = null;
            bool isTagRequest = true;
            int n = 0;
            lock (fPriorityFetchQ) {
                while (fPriorityFetchQ.Count == 0) {
                    Monitor.Wait(fPriorityFetchQ);
                    if (!fIsEnabled) return;
                }
                /*
                Tag requests have priority over stats requests.
                Exhaust the tag queue before taking from the stats queue.
                */
                if (fPriorityFetchQ.TagQueue.Count > 0) {
                    name = fPriorityFetchQ.TagQueue.Dequeue();
                } else if (fPriorityFetchQ.StatsQueue.Count > 0) {
                    name = fPriorityFetchQ.StatsQueue.Dequeue();
                    isTagRequest = false;
                }
                n = fPriorityFetchQ.Count;
            }

            if (since == DateTime.MinValue) since = DateTime.Now;

            String msg = n.ToString() + " request" + ((n > 1) ? "s" : "") + " in Battlelog request queue";
            if (n == 0) msg = "no more requests in Battlelog request queue";
            DebugWrite("^5(FETCH)^0 " + msg, 3);

            PlayerModel player = GetPlayer(name);
            if (player == null) continue;
            if (!EnableBattlelogRequests) {
                player.TagFetchStatus.State = FetchState.Aborted; // drain the fetch queue
                player.StatsFetchStatus.State = FetchState.Aborted; // drain the fetch queue
            }
            if (player.TagFetchStatus.State == FetchState.Aborted || player.StatsFetchStatus.State == FetchState.Aborted) {
                if (DebugLevel >= 8) ConsoleDebug("FetchLoop: fetch for ^b" + name + "^n was aborted!");
                continue;
            }

            if (++requests > MaximumRequestRate) {
                // Wait remainder of 20 seconds before continuing
                int delay = 20 - Convert.ToInt32(DateTime.Now.Subtract(since).TotalSeconds);
                if (delay > 0) {
                    DebugFetch("Sleeping remaining " + delay + " seconds before sending next request");
                    while (delay > 0) {
                        Thread.Sleep(1000);
                        if (!fIsEnabled) return;
                        if (!EnableBattlelogRequests) break;
                        --delay;
                    }
                }
                requests = 1; // reset
                since = DateTime.Now;
            }

            String requestType = (isTagRequest) ? "clanTag" : "overview";
            if (fIsCacheEnabled) {
                SendCacheRequest(name, requestType);
            } else {
                SendBattlelogRequest(name, requestType);
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
    } finally {
        ConsoleWrite("^bFetchLoop^n thread stopped");
    }
}

private void SendBattlelogRequest(String name, String requestType) {
    try {
        String result = String.Empty;
        String err = String.Empty;

        PlayerModel player = GetPlayer(name);
        if (player == null) return;
        FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
        status.State = FetchState.Requesting;
        status.Since = DateTime.Now;
        status.RequestType = requestType;
        DebugFetch("Fetching from Battlelog " + requestType + "(^b" + name + "^n)");

        if (String.IsNullOrEmpty(player.PersonaId)) {
            // Get the main page
            bool ok = false;
            try {
                if (!fIsEnabled) return;
                ok = FetchWebPage(ref result, "http://battlelog.battlefield.com/bf3/user/" + name);
                if (!fIsEnabled) return;
            } catch (Exception e) {
                ConsoleException(e);
            } finally {
                status.State = FetchState.Failed;
            }

            if (!ok) {
                status.State = FetchState.Failed;
                return;
            }

            // Extract the personaId
            MatchCollection pid = Regex.Matches(result, @"bf3/soldier/" + name + @"/stats/(\d+)(['""]|/\s*['""]|/[^/'""]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in pid) {
                if (match.Success && !Regex.Match(match.Groups[2].Value.Trim(), @"(ps3|xbox)", RegexOptions.IgnoreCase).Success) {
                    player.PersonaId = match.Groups[1].Value.Trim();
                    break;
                }
            }

            if (String.IsNullOrEmpty(player.PersonaId)) {
                DebugFetch("Request for ^b" + name +"^n failed, could not find persona-id!");
                status.State = FetchState.Failed;
                return;
            }
        }

        if (requestType == "clanTag") {
            // Extract the player tag
            Match tag = Regex.Match(result, player.PersonaId + @"[/'"">\s]+\[\s*([a-zA-Z0-9]+)\s*\]\s*" + name, RegexOptions.IgnoreCase | RegexOptions.Singleline); // Fixed #9
            //Match tag = Regex.Match(result, @"\[\s*([a-zA-Z0-9]+)\s*\]\s*" + name, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (tag.Success) {
                Hashtable data = new Hashtable();
                data["clanTag"] = tag.Groups[1].Value;
                SetTag(player, data);
                DebugFetch("Player tag updated: ^b" + player.FullName);
            } else {
                // No tag
                player.TagVerified = true;
                status.State = FetchState.Succeeded;
                DebugFetch("Battlelog says ^b" + player.Name + "^n has no tag");
            }
        } else if (requestType == "overview") {
            if (!fIsEnabled || WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
            String furl = "http://battlelog.battlefield.com/bf3/overviewPopulateStats/" + player.PersonaId + "/bf3-us-assault/1/";
            if (FetchWebPage(ref result, furl)) {
                if (!fIsEnabled) {
                    status.State = FetchState.Failed;
                    return;
                }

                Hashtable json = (Hashtable)JSON.JsonDecode(result);

                // verify we got a success message
                if (!CheckSuccess(json, out err)) {
                    status.State = FetchState.Failed;
                    DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): " + err);
                    return;
                }

                // verify there is data structure
                Hashtable data = null;
                if (!json.ContainsKey("data") || (data = (Hashtable)json["data"]) == null) {
                    status.State = FetchState.Failed;
                    DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response does not contain a data field (^4" + furl + "^0)");
                    return;
                }

                // verify there is stats structure
                Hashtable stats = null;
                if (!data.ContainsKey("overviewStats") || (stats = (Hashtable)data["overviewStats"]) == null) {
                    status.State = FetchState.Failed;
                    DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response data does not contain overviewStats (^4" + furl + "^0)");
                    return;
                }

                // extract the fields from the stats
                SetStats(player, stats);
            } else {
                status.State = FetchState.Failed;
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public bool IsCacheEnabled(bool verbose) {
    List<MatchCommand> registered = this.GetRegisteredCommands();
    foreach (MatchCommand command in registered) {
        if (command.RegisteredClassname.CompareTo("CBattlelogCache") == 0 && command.RegisteredMethodName.CompareTo("PlayerLookup") == 0) {
            if (verbose) DebugFetch("^bBattlelog Cache^n plugin will be used for stats fetching!");
            return true;
        } else {
            DebugFetch("Registered P: " + command.RegisteredClassname + ", M: " + command.RegisteredMethodName);
        }
    }
    if (verbose) DebugWrite("^1^bBattlelog Cache^n plugin is disabled; installing/updating and enabling the plugin is recommended for use with " + GetPluginName() + "!", 3);
    return false;
}

private void SendCacheRequest(String name, String requestType) {
    try {
        /* 
        Called in the FetchLoop thread
        */
        Hashtable request = new Hashtable();
        request["playerName"] = name;
        request["pluginName"] = GetPluginName();
        request["pluginMethod"] = "CacheResponse";
        request["requestType"] = requestType;

        // Set up response entry
        PlayerModel player = GetPlayer(name);
        if (player == null) return;
        FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
        status.State = FetchState.Requesting;
        status.Since = DateTime.Now;
        status.RequestType = requestType;
        DebugFetch("Sending cache request " + requestType + "(^b" + name + "^n)");

        // Send request
        if (!fIsEnabled) return;
        this.ExecuteCommand("procon.protected.plugins.call", "CBattlelogCache", "PlayerLookup", JSON.JsonEncode(request));
    } catch (Exception e) {
        ConsoleException(e);
    }
}

public void CacheResponse(params String[] response) {
    try {
        /*
        Called from the Battlelog Cache plugin Response thread
        */
        String val = null;
        if (DebugLevel >= 8) {
            DebugFetch("CacheResponse called with " + response.Length + " parameters");
            for (int i = 0; i < response.Length; ++i) {
                DebugFetch("#" + i + ") Length: " + response[i].Length);
                val = response[i];
                if (val.Length > 100) val = val.Substring(0, 500) + " ... ";
                if (val.Contains("{")) val = val.Replace('{', '<').Replace('}', '>'); // ConsoleWrite doesn't like messages with "{" in it
                DebugFetch("#" + i + ") Value: " + val);
            }
        }

        String name = response[0]; // Player's name
        val = response[1]; // JSON string

        Hashtable header = (Hashtable)JSON.JsonDecode(val);

        if (header == null) {
            DebugFetch("Request for ^b" + name +"^n failed!");
            return;
        }

        String result = (String)header["type"];
        double fetchTime = -1;
        Double.TryParse((String)header["fetchTime"], out fetchTime);
        double age = -1;
        Double.TryParse((String)header["age"], out age);

        PlayerModel player = GetPlayer(name);
        String err = String.Empty;
        String requestType = String.Empty;
        DateTime since = DateTime.Now;
        
        FetchInfo status = null;
        if (player.TagFetchStatus.State == FetchState.Requesting) {
            status = player.TagFetchStatus;
            since = status.Since;
        } else if (player.StatsFetchStatus.State == FetchState.Requesting) {
            status = player.StatsFetchStatus;
            since = status.Since;
        } else return;

        if (CheckSuccess(header, out err)) {
            // verify there is data structure
            Hashtable d = null;
            if (!header.ContainsKey("data") || (d = (Hashtable)header["data"]) == null) {
                ConsoleDebug("CacheResponse header does not contain data field!");
                status.State = FetchState.Failed;
                return;
            }
            if (d.ContainsKey("clanTag")) {
                requestType = "clanTag";
            } else if (d.ContainsKey("overviewStats")) {
                requestType = "overview";
            }

            status.RequestType = requestType;
            if (fetchTime > 0) {
                DebugFetch("Request " + status.RequestType + "(^b" + name + "^n) succeeded, cache refreshed from Battlelog, took ^2" + fetchTime.ToString("F1") + " seconds");
            } else if (age > 0) {
                TimeSpan a = TimeSpan.FromSeconds(age);
                DebugFetch("Request " + status.RequestType + "(^b" + name + "^n) succeeded, cached stats used, age is " + a.ToString().Substring(0, 8));
            }

            // Apply the result to the player
            switch (requestType) {
                case "clanTag":
                    SetTag(player, d);
                    if (String.IsNullOrEmpty(player.Tag)) {
                        DebugFetch("Battlelog Cache says ^b" + player.Name + "^n has no tag");
                    } else {
                        DebugFetch("Player tag updated: ^b" + player.FullName);
                    }
                    break;
                case "overview": {
                    // verify there is stats structure
                    Hashtable stats = null;
                    if ((stats = (Hashtable)d["overviewStats"]) == null) {
                        status.State = FetchState.Failed;
                        DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): Battlelog Cache response data does not contain overviewStats");
                        return;
                    }
                    SetStats(player, stats);
                    break;
                }
                default:
                    break;
            }
        } else {
            if (player.TagFetchStatus.State == FetchState.Requesting) {
                player.TagFetchStatus.State = FetchState.Failed;
                requestType = "clanTag";
            } else if (player.StatsFetchStatus.State == FetchState.Requesting) {
                player.StatsFetchStatus.State = FetchState.Failed;
                requestType = "overview";
            }
            DebugFetch("Request " + requestType + "(^b" + name + "^n): " + err);
        }
        DebugFetch("^2^bTIME^n took " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F2") + " secs, cache lookup for ^b" + name);
    } catch (Exception e) {
        ConsoleException(e);
    }
}


private bool CheckSuccess(Hashtable json, out String err) {

    if (json == null) {
        err = "JSON response is null!";
        return false;
    }

    if (!json.ContainsKey("type")) {
        err = "JSON response malformed: does not contain 'type'!";
        return false;
    }

    String type = (String)json["type"];

    if (type == null) {
        err = "JSON response malformed: 'type' is null!";
        return false;
    }

    if (Regex.Match(type, @"success", RegexOptions.IgnoreCase).Success) {
        err = null;
        return true;
    }

    if (!json.ContainsKey("message")) {
        err = "JSON response malformed: does not contain 'message'!";
        return false;
    }

    String message = (String)json["message"];

    if (message == null) {
        err = "JSON response malformed: 'message' is null!";
        return false;
    }

    err = "Cache fetch failed (type: " + type + ", message: " + message + ")!";
    return false;
}

private bool FetchWebPage(ref String result, String url) {
    bool ret = false;
    try {

        WebClient client = new WebClient();
        String ua = "Mozilla/5.0 (compatible; PRoCon 1; MULTIbalancer)";
        // XXX String ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0; .NET CLR 3.5.30729)";
        if (DebugLevel >= 8) DebugFetch("Using user-agent: " + ua);
        client.Headers.Add("user-agent", ua);

        DateTime since = DateTime.Now;

        result = client.DownloadString(url);

        /* TESTS
        String testUrl = "http://status.savanttools.com/?code=";
        html_data = client.DownloadString(testUrl + "429%20Too%20Many%20Requests");
        //html_data = client.DownloadString(testUrl + "509%20Bandwidth%20Limit%20Exceeded");
        //html_data = client.DownloadString(testUrl + "408%20Request%20Timeout");
        //html_data = client.DownloadString(testUrl + "404%20Not%20Found");
        */

        DebugFetch("^2^bTIME^n took " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F2") + " secs, url: " + url);

        if (Regex.Match(result, @"that\s+page\s+doesn't\s+exist", RegexOptions.IgnoreCase | RegexOptions.Singleline).Success) {
            DebugFetch("^b" + url + "^n does not exist");
            result = String.Empty;
            return false;
        }

        ret = true;
    } catch (WebException e) {
        if (e.Status.Equals(WebExceptionStatus.Timeout)) {
            if (DebugLevel >= 3) DebugFetch("EXCEPTION: HTTP request timed-out");
        } else {
            if (DebugLevel >= 3) DebugFetch("EXCEPTION: " + e.Message);
        }
        ret = false;
    } catch (Exception ae) {
        if (DebugLevel >= 3) DebugFetch("EXCEPTION: " + ae.Message);
        ret = false;
    }
    return ret;
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
    if (EnableExternalLogging) {
        LogExternal(msg);
    }
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

public void ConsoleException(Exception e)
{
    if (DebugLevel >= 3) ConsoleWrite(e.ToString(), MessageType.Exception);
}

public void DebugWrite(String msg, int level)
{
    if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal);
}

public void ConsoleDebug(String msg)
{
    if (DebugLevel >= 6) ConsoleWrite(msg, MessageType.Debug);
}

public void ConsoleDump(String msg)
{
    ConsoleWrite("^b[Show In Log]^n ^1" + msg);
}


private void ServerCommand(params String[] args)
{
    List<String> list = new List<String>();
    list.Add("procon.protected.send");
    list.AddRange(args);
    this.ExecuteCommand(list.ToArray());
}

private void TaskbarNotify(String title, String msg) {
    this.ExecuteCommand("procon.protected.notification.write", title, msg);
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

public bool CheckForEquality(MULTIbalancer rhs) {
    return (this.OnWhitelist == rhs.OnWhitelist
     && this.OnFriendsList == rhs.OnFriendsList
     && this.TopScorers == rhs.TopScorers
     && this.SameClanTagsInSquad == rhs.SameClanTagsInSquad
     && this.SameClanTagsForRankDispersal == rhs.SameClanTagsForRankDispersal
     && this.MinutesAfterJoining == rhs.MinutesAfterJoining
     && this.JoinedEarlyPhase == rhs.JoinedEarlyPhase
     && this.JoinedMidPhase == rhs.JoinedMidPhase
     && this.JoinedLatePhase == rhs.JoinedLatePhase
     && MULTIbalancerUtils.EqualArrays(this.EarlyPhaseTicketPercentageToUnstack, rhs.EarlyPhaseTicketPercentageToUnstack)
     && MULTIbalancerUtils.EqualArrays(this.MidPhaseTicketPercentageToUnstack, rhs.MidPhaseTicketPercentageToUnstack)
     && MULTIbalancerUtils.EqualArrays(this.LatePhaseTicketPercentageToUnstack, rhs.LatePhaseTicketPercentageToUnstack)
     && MULTIbalancerUtils.EqualArrays(this.EarlyPhaseBalanceSpeed, rhs.EarlyPhaseBalanceSpeed)
     && MULTIbalancerUtils.EqualArrays(this.MidPhaseBalanceSpeed, rhs.MidPhaseBalanceSpeed)
     && MULTIbalancerUtils.EqualArrays(this.LatePhaseBalanceSpeed, rhs.LatePhaseBalanceSpeed)
     && this.ForbidSwitchingAfterAutobalance == rhs.ForbidSwitchingAfterAutobalance 
     && this.ForbidSwitchingToWinningTeam == rhs.ForbidSwitchingToWinningTeam 
     && this.ForbidSwitchingToBiggestTeam == rhs.ForbidSwitchingToBiggestTeam
     && this.ForbidSwitchingAfterDispersal == rhs.ForbidSwitchingAfterDispersal
     && this.EnableImmediateUnswitch == rhs.EnableImmediateUnswitch
    );
}


private void UpdatePresetValue() {
    Preset = PresetItems.None;  // backstop value

    try {

        // Check for Standard
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.Standard)) {
            Preset = PresetItems.Standard;
            return;
        }
    
        // Check for Aggressive
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.Aggressive)) {
            Preset = PresetItems.Aggressive;
            return;
        }
    
        // Check for Passive
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.Passive)) {
            Preset = PresetItems.Passive;
            return;
        }
    
        // Check for Intensify
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.Intensify)) {
            Preset = PresetItems.Intensify;
            return;
        }
    
        // Check for Retain
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.Retain)) {
            Preset = PresetItems.Retain;
            return;
        }
    
        // Check for BalanceOnly
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.BalanceOnly)) {
            Preset = PresetItems.BalanceOnly;
            return;
        }
    
        // Check for UnstackOnly
        if (MULTIbalancerUtils.IsEqual(this, PresetItems.UnstackOnly)) {
            Preset = PresetItems.UnstackOnly;
            return;
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}

private void Reset() {
    ResetRound();
    
    lock (fPriorityFetchQ) {
        fPriorityFetchQ.Clear();
        Monitor.Pulse(fPriorityFetchQ);
    }

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

    lock (fMoveStash) {
        fMoveStash.Clear();
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
    fRoundsEnabled = 0;
    fGrandTotalQuits = 0;
    fGrandRageQuits = 0;

    fDebugScramblerBefore[0].Clear();
    fDebugScramblerBefore[1].Clear();
    fDebugScramblerAfter[0].Clear();
    fDebugScramblerAfter[1].Clear();
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
            try {
                if (!fKnownPlayers.ContainsKey(name)) {
                    ConsoleDebug("ResetRound: " + name + " not in fKnownPlayers");
                    continue;
                }
                PlayerModel m = null;
                lock (fKnownPlayers) {
                    m = fKnownPlayers[name];
                }

                m.ResetRound();
            } catch (Exception e) {
                ConsoleException(e);
            }
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
    fRushPrevAttackerTickets = 0;
    fTimeOutOfJoint = 0;
    fRoundsEnabled = fRoundsEnabled + 1;
    fGrandTotalQuits = fGrandTotalQuits + fTotalQuits;
    fTotalQuits = 0;
    fGrandRageQuits = fGrandRageQuits + fRageQuits;
    fRageQuits = 0;

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

private bool IsCTF() {
    if (fServerInfo == null) return false;
    return (fServerInfo.GameMode == "CaptureTheFlag0");
}

private int MaxDiff() {
    if (fServerInfo == null) return 2;
    PerModeSettings perMode = null;
    String simpleMode = String.Empty;
    if (IsSQDM()) {
        return ((TotalPlayerCount <= 32) ? 1 : 2);
    }
    perMode = GetPerModeSettings();

    if (!perMode.isDefault) return ((GetPopulation(perMode, false) == Population.High) ? 2 : 1);

    return 2;
}

private void UpdateTeams() {
    ClearTeams();

    List<String> names = new List<String>();

    lock (fAllPlayers) {
        foreach (String name in fAllPlayers) {
            if (!fKnownPlayers.ContainsKey(name)) {
                ConsoleDebug("UpdateTeams: " + name + " not in fKnownPlayers");
                continue;
            }
            names.Add(name);
        }
    }
    lock (fKnownPlayers) {
        foreach (String dude in names) {
            PlayerModel player = null;
            if (fKnownPlayers.TryGetValue(dude, out player) && player != null) {
                List<PlayerModel> t = GetTeam(player.Team);
                if (t != null) t.Add(player);
                // Also update move timer
                if (player.MovedByMBTimestamp != DateTime.MinValue && DateTime.Now.Subtract(player.MovedByMBTimestamp).TotalMinutes >= MinutesAfterBeingMoved) {
                    player.MovedByMBTimestamp = DateTime.MinValue;
                }
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
        if (i < teams.Count) {
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
    if (fServerInfo.TeamScores == null) return;
    bool isCTF = IsCTF();
    if (!isCTF && fServerInfo.TeamScores.Count == 0) return;
    if (IsRush()) {
        // Normalize scores
        TeamScore attackers = fServerInfo.TeamScores[0];
        TeamScore defenders = fServerInfo.TeamScores[1];
        double normalized = fMaxTickets - (fRushMaxTickets - defenders.Score);
        normalized = Math.Max(normalized, Convert.ToDouble(attackers.Score)/2);
        byScore.Add(attackers); // attackers
        byScore.Add(new TeamScore(defenders.TeamID, Convert.ToInt32(normalized), defenders.WinningScore));
    } else if (isCTF) {
        // Base sort on team points rather than tickets
        int usPoints = Convert.ToInt32(GetTeamPoints(1));
        int ruPoints = Convert.ToInt32(GetTeamPoints(2));
        DebugWrite("^9CTF analysis: US/RU points = " + usPoints + "/" + ruPoints, 8);
        byScore.Add(new TeamScore(1, usPoints, 0));
        byScore.Add(new TeamScore(2, ruPoints, 0));
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
    DebugWrite("^9AnalyzeTeams: biggest/smallest/winning/losing = " + biggestTeam + "/" + smallestTeam + "/" + winningTeam + "/" + losingTeam, 8);
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


private int ToTeam(String name, int fromTeam, bool isReassign, out int diff, ref bool mustMove) {
    diff = 0;
    if (fromTeam < 1 || fromTeam > 4) return 0;

    List<PlayerModel>[] byId = new List<PlayerModel>[5]{null, fTeam1, fTeam2, fTeam3, fTeam4};
    
    int biggestTeam = 0;
    int smallestTeam = 0;
    int winningTeam = 0;
    int losingTeam = 0;
    int[] ascendingSize = null;
    int[] descendingTickets = null;

    AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

    // diff already set by AnalyzeTeams
    if (mustMove) {
        int disTeam = ToTeamByDispersal(name, fromTeam, byId);

        if (disTeam == -1) {
            // this player moved more than other dispersals, skip
            DebugBalance("Exempting dispersal player ^b" + name + "^n, moved more than others");
            // leave mustMove set to true so that caller does the right thing
            return 0;
        }

        if (disTeam != 0) {
            DebugWrite("^9ToTeam for ^b" + name + "^n: dispersal returned team " + disTeam, 6);
            return disTeam;
        }
        // fall thru if dispersal doesn't find a suitable team
        mustMove = false;
    }

    DebugWrite("^9ToTeam for ^b" + name + "^n: winning/losing = " + winningTeam + "/" + losingTeam, 8);
    if (DebugLevel >= 8 && descendingTickets != null) {
        String ds = "^9ToTeam for ^b" + name + "^n: descendingTickets = [";
        for (int k = 0; k < descendingTickets.Length; ++k) {
            ds = ds + descendingTickets[k] + " ";
        }
        ds = ds + "]";
        DebugWrite(ds, 8);
    }

    // diff is maximum difference between any two teams
    if (!isReassign && diff <= MaxDiff()) return 0;
    int superDiff = diff;

    int targetTeam = smallestTeam;

    // if teams are same size, send to losing team
    if (biggestTeam != smallestTeam && byId[biggestTeam].Count == byId[smallestTeam].Count && losingTeam != fromTeam) {
        targetTeam = losingTeam;
    }
    
    if (targetTeam == fromTeam) return 0;

    // Special handling for SQDM
    bool isSQDM = IsSQDM();
    if (isSQDM) {
        int orig = targetTeam;
        int i = 0;

        // Don't send to the winning team, even if it is the smallest, unless reassigning
        if (!isReassign && targetTeam == winningTeam) {
            while (i < ascendingSize.Length) {
                int aTeam = ascendingSize[i];
                ++i;
                if (aTeam == orig || aTeam == winningTeam || aTeam == fromTeam) continue;
                targetTeam = aTeam;
                break;
            }
        }

        if (targetTeam != orig) {
            String szs = "(";
            for (i = 1; i < byId.Length; ++i) {
                szs = szs + byId[i].Count.ToString();
                if (i == 4) {
                    szs = szs + ")";
                } else {
                    szs = szs + ", ";
                }
            }
            DebugBalance("ToTeam  for ^b" + name + "^n: SQDM adjusted target from " + GetTeamName(orig) + " team to " + GetTeamName(targetTeam) + " team: " + szs);
        }
    }

    // recompute diff to be difference between fromTeam and target team
    diff = GetTeamDifference(ref fromTeam, ref targetTeam);
    if (diff < 0) {
        ConsoleDebug("ToTeam for ^b" + name + "^n: GetTeamDifference returned negative diff = " + diff);
        diff = Math.Abs(diff);
    }

    // Fake out difference due to adjustment
    if (isSQDM && diff < MaxDiff() && diff != 0) {
        DebugBalance("ToTeam  for ^b" + name + "^n: SQDM fake out diff due to adjustment, was " + diff + ", will be reported as " + superDiff);
        diff = superDiff;
    }

    String tm = "(";
    for (int j = 1; j <= 4; ++j) {
        if (j == winningTeam) tm = tm + "+";
        if (j == losingTeam) tm = tm + "-";
        tm = tm + byId[j].Count;
        if (j != 4) tm = tm + "/";
    }
    tm = tm + ")";
    DebugWrite("ToTeam for ^b" + name + "^n: analyze returned " + tm + ", " + fromTeam + " ==> " + targetTeam, 5);

    return targetTeam;
}

private int ToTeamByDispersal(String name, int fromTeam, List<PlayerModel>[] teamListsById) {
    int targetTeam = 0;
    bool allEqual = false;
    int grandTotal = 0;

    if (teamListsById == null) return 0;

    /*
    Select a team that would disperse this player evenly with similar players,
    regardless of balance or stacking. Dispersal list takes priority over
    other dispersal types.
    */

    PlayerModel player = GetPlayer(name);
    if (player == null) return 0;

    PerModeSettings perMode = GetPerModeSettings();
    if (perMode.isDefault) return 0;

    bool isSQDM = IsSQDM();
    bool mostMoves = true;

    bool isDispersalByRank = IsRankDispersal(player);
    bool isDispersalByList = IsDispersal(player);

    /* By Dispersal List */

    if (isDispersalByList) {
        int[] usualSuspects = new int[5]{0,0,0,0,0};

        if (player.DispersalGroup >= 1 && player.DispersalGroup <= 4) {
            // Disperse by group
            if (!isSQDM && player.DispersalGroup > 2) {
                if (DebugLevel >= 7) ConsoleDebug("ToTeamByDispersal ignoring Group " + player.DispersalGroup + " for ^b" + player.FullName + "^n, not SQDM");
                // fall thru
            } else {
                return fGroupAssignments[player.DispersalGroup];
            }
        }

        // Otherwise normal list dispersal
        mostMoves = true;

        for (int teamId = 1; teamId < teamListsById.Length; ++teamId) {
            foreach (PlayerModel p in teamListsById[teamId]) {
                if (p.Name == player.Name) continue; // don't count this player
                
                if (IsDispersal(p)) {
                    usualSuspects[teamId] = usualSuspects[teamId] + 1;
                    grandTotal = grandTotal + 1;

                    // Make sure this player hasn't been moved more than any other dispersal player
                    if (GetMoves(p) > GetMoves(player)) {
                        mostMoves = false;
                    }
                }
            }
        }

        if (mostMoves && GetMoves(player) > 0) {
            DebugWrite("ToTeamByDispersal List: ^b" + player.Name + "^n moved more than other dispersals (" + GetMoves(player) + " times), skipping!", 5);
            return -1;
        }

        String an = usualSuspects[1] + "/" + usualSuspects[2];
        if (isSQDM) an = an + "/" + usualSuspects[3] + "/" + usualSuspects[4];
        DebugWrite("ToTeamByDispersal: analysis of ^b" + player.FullName + "^n dispersal by list: " + an, 5);

        // Pick smallest one
        targetTeam = 0;
        allEqual = true;
        int minSuspects = 64;
        for (int i = 1; i < usualSuspects.Length; ++i) {
            if (!isSQDM && i > 2) continue;
            if (allEqual && usualSuspects[i] == minSuspects) {
                allEqual = true;
            } else if (usualSuspects[i] < minSuspects) {
                minSuspects = usualSuspects[i];
                targetTeam = i;
                if (i != 1) allEqual = false;
            } else {
                if (i != 1) allEqual = false;
            }
        }

        if (grandTotal > 1 && !allEqual && targetTeam != 0 && targetTeam != fromTeam) return targetTeam;

        if (allEqual) DebugWrite("^9ToTeamByDispersal: all equal list, skipping", 5);
        // otherwise fall through and try rank
    }

    /* By Rank? */

    if (isDispersalByRank) {
        int[] rankers = new int[5]{0,0,0,0,0};
        grandTotal = 0;
        mostMoves = true;

        for (int i = 1; i < teamListsById.Length; ++i) {
            foreach (PlayerModel p in teamListsById[i]) {
                if (p.Name == player.Name) continue; // don't count this player
                if (p.Rank >= perMode.DisperseEvenlyByRank) {
                    rankers[i] = rankers[i] + 1;
                    grandTotal = grandTotal + 1;

                    // Make sure this player hasn't been moved more than any other dispersal player
                    if (GetMoves(p) > GetMoves(player)) {
                        mostMoves = false;
                    }
                }
            }
        }

        if (mostMoves && GetMoves(player) > 0) {
            DebugWrite("ToTeamByDispersal Rank: ^b" + player.Name + "^n moved more than other dispersals (" + GetMoves(player) + " times), skipping!", 5);
            return -1;
        }

        String a = rankers[1] + "/" + rankers[2];
        if (isSQDM) a = a + "/" + rankers[3] + "/" + rankers[4];
        DebugWrite("ToTeamByDispersal: analysis of ^b" + name + "^n dispersal of rank >= " + perMode.DisperseEvenlyByRank + ": " + a, 5);

        // Pick smallest one
        targetTeam = 0;
        allEqual = true;
        int minRanks = 64;
        for (int i = 1; i < rankers.Length; ++i) {
            if (!isSQDM && i > 2) continue;
            if (allEqual && rankers[i] == minRanks) {
                allEqual = true;
            } else if (rankers[i] < minRanks) {
                minRanks = rankers[i];
                targetTeam = i;
                if (i != 1) allEqual = false;
            } else {
                if (i != 1) allEqual = false;
            }
        }

        if (allEqual || grandTotal < 2) {
            DebugWrite("^9ToTeamByDispersal: all equal by rank, skipping", 5);
            return 0; // don't disperse
        }
        // fall through
    }

    return targetTeam; // ok if 0 or same as fromTeam, caller checks
}

private int ToSquad(String name, int team) {
    int ret = 0;
    try {
        List<PlayerModel> teamList = null;

        if (IsSQDM()) return 1; // SQDM, squad is always 1

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

        // Find the biggest squad less than 4 (that isn't locked -- TBD)
        int squad = 0;
        int best = 0;
        int atZero = 0;
        for (int squadNum = 1; squadNum < squads.Length; ++squadNum) {
            int n = squads[squadNum];
            if (n == 0) {
                if (atZero == 0) atZero = squadNum;
                continue;
            }
            if (n >= 4) continue;
            if (n > best) {
                squad = squadNum;
                best = n;
            }
        }
        // if no best squad, use empty squad with lowest slot number
        if (squad == 0 && atZero != 0) {
            ret = atZero;
        } else {
            // otherwise return the best squad
            ret = squad;
        }
        if (DebugLevel >= 6) {
            String ss = "selected " + ret + " out of ";
            for (int k = 1; k < squads.Length; ++k) {
                if (squads[k] == 0) continue;
                ss = ss + k + ":" + squads[k] + "/";
            }
            ss = ss + "-";
            ConsoleDebug("ToSquad ^b" + name + "^n: " + ss);
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
    return ret;
}

private void StartThreads() {
    fMoveThread = new Thread(new ThreadStart(MoveLoop));
    fMoveThread.IsBackground = true;
    fMoveThread.Name = "unswitcher";
    fMoveThread.Start();

    fListPlayersThread = new Thread(new ThreadStart(ListPlayersLoop));
    fListPlayersThread.IsBackground = true;
    fListPlayersThread.Name = "lister";
    fListPlayersThread.Start();

    fFetchThread = new Thread(new ThreadStart(FetchLoop));
    fFetchThread.IsBackground = true;
    fFetchThread.Name = "fetcher";
    fFetchThread.Start();

    fScramblerThread = new Thread(new ThreadStart(ScramblerLoop));
    fScramblerThread.IsBackground = true;
    fScramblerThread.Name = "scrambler";
    fScramblerThread.Start();
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
                    lock (fPriorityFetchQ) {
                        Monitor.Pulse(fPriorityFetchQ);
                    }
                    JoinWith(fFetchThread, 3);
                    fFetchThread = null;
                    lock (fScramblerLock) {
                        fScramblerLock.MaxDelay = 0;
                        Monitor.Pulse(fScramblerLock);
                    }
                    JoinWith(fScramblerThread, 3);
                    fScramblerThread = null;
                }
                catch (Exception e)
                {
                    ConsoleException(e);
                }

                fFinalizerActive = false;
                ConsoleWrite("Finished disabling threads, ready to be enabled again!");
            }));

        stopper.Name = "stopper";
        stopper.Start();

    }
    catch (Exception e)
    {
        ConsoleException(e);
    }
}

private void UpdateMoveTime(String name) {
    PlayerModel player = GetPlayer(name);
    if (player == null) return;
    player.MovedTimestamp = DateTime.Now;
}

private void IncrementMoves(String name) {
    if (!IsKnownPlayer(name)) return;
    lock (fKnownPlayers) {
        PlayerModel m = fKnownPlayers[name];
        m.MovesByMBRound = m.MovesByMBRound + 1;
        m.MovedByMBTimestamp = DateTime.Now;
    }
    UpdateMoveTime(name);
}

private void ConditionalIncrementMoves(String name) {
    /*
    If some other plugin did an admin move on this player, increment
    the non-MB move counter so that this player will be exempted from balancing and unstacking
    for the rest of this round, but don't set the flag or the timer, since MB didn't move this player.
    */
    if (!IsKnownPlayer(name)) return;
    lock (fKnownPlayers) {
        PlayerModel m = fKnownPlayers[name];
        m.MovesRound = m.MovesRound + 1;
    }
    IncrementTotal(); // no matching stat, reflects handling of non-MB admin move
}


private int GetMoves(PlayerModel player) {
    if (player == null) return 0;
    return (player.MovesRound + player.MovesByMBRound);
}


private void IncrementTotal()
{
    if (fPluginState == PluginState.Active) fTotalRound = fTotalRound + 1;
}

private String GetTeamName(int team) {
    if (team < 0) return "None";

    if (IsSQDM() && team < SQUAD_NAMES.Length) {
        return SQUAD_NAMES[team];
    } else if (IsRush() && team < RUSH_NAMES.Length) {
        return RUSH_NAMES[team];
    }
    if (team >= TEAM_NAMES.Length) return "None";
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
            DelayedRequest request = null;
            lock (fListPlayersQ) {
                while (fListPlayersQ.Count == 0) {
                    Monitor.Wait(fListPlayersQ);
                    if (!fIsEnabled) return;
                }

                request = fListPlayersQ.Dequeue();

                // Wait until event handler updates fListPlayersTimestamp or MaxDelay has elapsed
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
        ConsoleException(e);
    } finally {
        ConsoleWrite("^bListPlayersLoop^n thread stopped");
    }
}

private void ScheduleListPlayers(double delay) {
    DelayedRequest r = new DelayedRequest(delay, fListPlayersTimestamp);
    DebugWrite("^9Scheduling listPlayers no sooner than " + r.MaxDelay  + " seconds from " + r.LastUpdate.ToString("HH:mm:ss"), 7);
    lock (fListPlayersQ) {
        fListPlayersQ.Enqueue(r);
        Monitor.Pulse(fListPlayersQ);
    }
}

private String ExtractTag(PlayerModel m) {
    if (m == null) return String.Empty;

    String tag = m.Tag;
    if (m.TagVerified && String.IsNullOrEmpty(tag)) {
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
public static int DescendingRoundSPM(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }
    if (lhs.SPMRound < rhs.SPMRound) return 1;
    if (lhs.SPMRound > rhs.SPMRound) return -1;
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

// Sort delegate
public static int DescendingRoundKPM(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }
    if (lhs.KPMRound < rhs.KPMRound) return 1;
    if (lhs.KPMRound > rhs.KPMRound) return -1;
    return 0;
}

// Sort delegate
public static int DescendingMetricSquad(SquadRoster lhs, SquadRoster rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }

    // Dividing by Clan Tag takes precedence, only when both are zero is the metric used
    if (lhs.ClanTagCount > 0 || rhs.ClanTagCount > 0) {
        if (lhs.ClanTagCount < rhs.ClanTagCount) { return 1; }
        if (lhs.ClanTagCount > rhs.ClanTagCount) { return -1; }
        return 0;
    }

    if (lhs.Metric < rhs.Metric) return 1;
    if (lhs.Metric > rhs.Metric) return -1;
    return 0;
}


// Sort delegate
public static int DescendingSPM(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }
    double lSPM = (lhs.StatsVerified) ? lhs.SPM : lhs.SPMRound;
    double rSPM = (rhs.StatsVerified) ? rhs.SPM : rhs.SPMRound;
    if (lSPM < rSPM) return 1;
    if (lSPM > rSPM) return -1;
    return 0;
}


// Sort delegate
public static int DescendingKDR(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }
    double lKDR = (lhs.StatsVerified) ? lhs.KDR : lhs.KDRRound;
    double rKDR = (rhs.StatsVerified) ? rhs.KDR : rhs.KDRRound;
    if (lKDR < rKDR) return 1;
    if (lKDR > rKDR) return -1;
    return 0;
}


// Sort delegate
public static int DescendingKPM(PlayerModel lhs, PlayerModel rhs) {
    if (lhs == null) {
        return ((rhs == null) ? 0 : -1);
    } else if (rhs == null) {
        return ((lhs == null) ? 0 : 1);
    }
    double lKPM = (lhs.StatsVerified) ? lhs.KPM : lhs.KPMRound;
    double rKPM = (rhs.StatsVerified) ? rhs.KPM : rhs.KPMRound;
    if (lKPM < rKPM) return 1;
    if (lKPM > rKPM) return -1;
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
            p = null;
        }
    }
    if (p == null) ConsoleDebug("GetPlayer unknown player ^b" + name);
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
    int level = 5;
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
    DebugWrite("^5(SWITCH)^9 " + msg, 5);
}


private void DebugFetch(String msg) {
    DebugWrite("^5(FETCH)^9 " + msg, 7);
}

private void DebugScrambler(String msg) {
    DebugWrite("^5(SCRAMBLER)^9 " + msg, 6);
}


private double NextSwapGroupInSeconds(PerModeSettings perMode) {
    if (fFullUnstackSwapTimestamp == DateTime.MinValue) return 0;
    if (fUnstackGroupCount > 0 && fUnstackGroupCount <= perMode.NumberOfSwapsPerGroup) return 0;
    double since = DateTime.Now.Subtract(fFullUnstackSwapTimestamp).TotalSeconds;
    if (since > perMode.DelaySecondsBetweenSwapGroups) return 0;
    return (perMode.DelaySecondsBetweenSwapGroups - since);
}


private String GetPlayerStatsString(String name) {
    DateTime now = DateTime.Now;
    double score = -1;
    double kills = -1;
    double deaths = -1;
    double kdr = -1;
    double spm = -1;
    double kpm = -1;
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
            kpm = m.KPMRound;
            tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
            team = m.Team;
        }
    }

    if (!ok) return("NO STATS FOR: " + name);

    String type = "ROUND";
    if (WhichBattlelogStats != BattlelogStats.ClanTagOnly && m.StatsVerified) {
        type = (WhichBattlelogStats == BattlelogStats.AllTime) ? "ALL-TIME" : "RESET";
        kdr = m.KDR;
        spm = m.SPM;
        kpm = m.KPM;
    }

    Match rm = Regex.Match(tir.ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
    String sTIR = (rm.Success) ? rm.Groups[1].Value : "?";
    String vn = m.FullName;

    return("^0" + type + " STATS: ^b" + vn + "^n [T:" + team + ", S:" + score + ", K:" + kills + ", D:" + deaths + ", KDR:" + kdr.ToString("F2") + ", SPM:" + spm.ToString("F0") + ", KPM:" + kpm.ToString("F1") + ", TIR: " + sTIR + "]");
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

private void SetSpawnMessages(String name, String chat, String yell, bool quiet) {
    PlayerModel player = null;

    player = GetPlayer(name);
    if (player == null) return;

    if (!String.IsNullOrEmpty(player.SpawnChatMessage)) {
        DebugWrite("^9Overwriting previous chat message for ^b" + name + "^n: " + player.SpawnChatMessage, 7);
    }
    player.SpawnChatMessage = chat;
    player.SpawnYellMessage = yell;
    player.QuietMessage = quiet;
}

private void FireMessages(String name) {
    PlayerModel player = GetPlayer(name);
    if (player == null) return;

    if (!String.IsNullOrEmpty(player.SpawnChatMessage) || !String.IsNullOrEmpty(player.SpawnYellMessage)) {
        DebugWrite("^5(SPAWN)^9 firing messages delayed until spawn for ^b" + name, 5);
    }
    if (!String.IsNullOrEmpty(player.SpawnChatMessage)) Chat(name, player.SpawnChatMessage, player.QuietMessage);
    if (!String.IsNullOrEmpty(player.SpawnYellMessage)) Yell(name, player.SpawnYellMessage);
    player.SpawnChatMessage = String.Empty;
    player.SpawnYellMessage = String.Empty;
    player.QuietMessage = false;
}

private void CheckDelayedMove(String name) {
    PlayerModel player = GetPlayer(name);
    if (player == null) return;
    
    if (player.DelayedMove != null) {
        MoveInfo dm = player.DelayedMove;
        player.DelayedMove = null;

        DebugWrite("^5(SPAWN)^9 executing delayed move of ^b" + name, 5);
        DebugUnswitch("FORBIDDEN: Detected bad team switch, scheduling admin kill and move for ^b: " + name);
        String log = "^4^bUNSWITCHING^n^0 ^b" + player.FullName + "^n from " + dm.SourceName + " back to " + dm.DestinationName;
        log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
        DebugWrite(log, 3);
        KillAndMoveAsync(dm);
    }
}

private double GetTeamPoints(int team) {
    double total = 0;
    List<String> dup = new List<String>();
    // copy player name list
    lock (fAllPlayers) {
        dup.AddRange(fAllPlayers);
    }
    // sum up player points for specified team
    foreach (String name in dup) {
        PlayerModel player = GetPlayer(name);
        if (player.Team != team) continue;
        total = total + player.ScoreRound;
    }
    return total;
}


private PerModeSettings GetPerModeSettings() {
    PerModeSettings perMode = null;
    if (fModeToSimple == null || fServerInfo == null) return new PerModeSettings();
    String simpleMode = String.Empty;
    if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) 
    && !String.IsNullOrEmpty(simpleMode)
    && fPerMode.TryGetValue(simpleMode, out perMode)
    && perMode != null) {
        return perMode;
    }
    return new PerModeSettings();
}

private void CheckDeativateBalancer(String reason) {
    if (fBalanceIsActive) {
        fBalanceIsActive = false;
        double dur = DateTime.Now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (fLastBalancedTimestamp == DateTime.MinValue) dur = 0;
        if (dur > 0) {
            if (DebugLevel >= 6) DebugBalance("^2^b" + reason + "^n: Was active for " + dur.ToString("F0") + " seconds!");
        }
    }
}

private bool IsDispersal(PlayerModel player) {
    player.DispersalGroup = 0;
    PerModeSettings perMode = GetPerModeSettings();
    bool isDispersalByList = false;
    if (!perMode.EnableDisperseEvenlyList) return false;
    String extractedTag = ExtractTag(player);
    if (String.IsNullOrEmpty(extractedTag)) extractedTag = INVALID_NAME_TAG_GUID;
    String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
    if (fSettingDisperseEvenlyList.Count > 0) {
        if (fSettingDisperseEvenlyList.Contains(player.Name) 
        || fSettingDisperseEvenlyList.Contains(guid) 
        || fSettingDisperseEvenlyList.Contains(extractedTag)) {
            isDispersalByList = true;
        }
    }
    for (int i = 1; i <= 4; ++i) {
        if (!isDispersalByList && fDispersalGroups[i].Count > 0) {
            fDispersalGroups[i] = new List<String>(fDispersalGroups[i]);
            if (fDispersalGroups[i].Contains(player.Name) 
            || fDispersalGroups[i].Contains(guid) 
            || fDispersalGroups[i].Contains(extractedTag)) {
                isDispersalByList = true;
                player.DispersalGroup = i;
                break;
            }
        }
    }
    return (isDispersalByList);
}

private bool IsRankDispersal(PlayerModel player) {
    PerModeSettings perMode = GetPerModeSettings();
    if (perMode.DisperseEvenlyByRank == 0) return false;
    if (SameClanTagsForRankDispersal && CountMatchingTags(player, Scope.SameTeam) >= 2) {
        if (player.Rank >= perMode.DisperseEvenlyByRank) DebugWrite("^9Exempting player from rank dispersal, due to SameClanTagsForRankDispersal: ^b" + "^b" + player.FullName + "^n", 6);
        return false;
    }
    return (player.Rank >= perMode.DisperseEvenlyByRank);
}

private void FinishedFullSwap(String name, PerModeSettings perMode) {
    fUnstackGroupCount = fUnstackGroupCount + 1;
    if (fUnstackGroupCount >= perMode.NumberOfSwapsPerGroup) {
        fFullUnstackSwapTimestamp = DateTime.Now; // start the timer
        DebugBalance("For ^b" + name + "^n, finished group of " + perMode.NumberOfSwapsPerGroup + ", delay timer set");
        fUnstackGroupCount = 0;
    } else {
        DebugBalance("For ^b" + name + "^n, did swap " + fUnstackGroupCount + " of " + perMode.NumberOfSwapsPerGroup);
        fFullUnstackSwapTimestamp = DateTime.MinValue;
    }
}

private void ValidateInt(ref int val, String propName, int def) {
    if (val < 0) {
        ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}


private void ValidateIntRange(ref int val, String propName, int min, int max, int def, bool zeroOK) {
    if (zeroOK && val == 0) return;
    if (val < min || val > max) {
        String zero = (zeroOK) ? " or equal to 0" : String.Empty;
        ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
        val = def;
    }
}


private void ValidateDouble(ref double val, String propName, double def) {
    if (val < 0) {
        ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}


private void ValidateDoubleRange(ref double val, String propName, double min, double max, double def, bool zeroOK) {
    if (zeroOK && val == 0.0) return;
    if (val < min || val > max) {
        String zero = (zeroOK) ? " or equal to 0" : String.Empty;
        ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
        val = def;
        return;
    }
}


private void CheckRageQuit(String name) {
    /*
    Heuristic: if player leaves server within 1 minute of being moved, treat as a rage quit
    due to actions of this plugin.
    */
    PlayerModel player = GetPlayer(name);
    if (player == null) return;

    ++fTotalQuits;

    if (player.MovedTimestamp != DateTime.MinValue && DateTime.Now.Subtract(player.MovedTimestamp).TotalSeconds <= 60) {
        ++fRageQuits;
        DebugWrite("Looks like ^b" + name + "^n rage quit: " + fRageQuits + " so far this round, out of " + fTotalQuits, 4);
    }
}


private int CountMatchingTags(PlayerModel player, Scope scope) {
    if (player == null) return 0;
    if (player.Team == 0 || player.Squad == 0) return 0;
    int team = player.Team;
    int squad = player.Squad;

    List<PlayerModel> teamList = GetTeam(team);
    if (teamList == null) return 0;

    String tag = ExtractTag(player);
    if (String.IsNullOrEmpty(tag)) return 0;
    int same = 0;
    int verified = 0;
    int total = 0;

    foreach (PlayerModel mate in teamList) {
        if (scope == Scope.SameSquad && mate.Squad != squad) continue;
        ++total;
        if (mate.TagVerified) ++verified;
        if (ExtractTag(mate) == tag) ++same;
    }

    String sname = squad.ToString();
    if (squad > 0 && squad <= SQUAD_NAMES.Length) {
        sname = SQUAD_NAMES[squad];
    }

    String loc = (scope == Scope.SameSquad) ? sname : GetTeamName(team);

    if (verified < 2) {
        if (DebugLevel >= 6) DebugBalance("Count for matching tags for player ^b" + player.Name + "^n in " + loc + ", not enough verified tags to find matches");
        return 0;
    } else {
        if (DebugLevel >= 6 && same > 1) DebugBalance("Count for matching tags for player ^b" + player.Name + "^n in " + loc + ", found " + same + " matching tags [" + tag + "]");
    }
    return same;
}


private void ListSideBySide(List<PlayerModel> us, List<PlayerModel> ru) {
    int max = Math.Max(us.Count, ru.Count);

    // Sort lists by specified metric, which might have changed by now, oh well
    List<PlayerModel> all = new List<PlayerModel>();
    PlayerModel player = null;
    double usTotal = 0;
    foreach (PlayerModel u in us) {
        String en = u.Name;
        player = u;
        if (player == null) continue;
        all.Add(player);
        double stat = 0;
        switch (ScrambleBy) {
            case DefineStrong.RoundScore:
                stat = player.ScoreRound;
                break;
            case DefineStrong.RoundSPM:
                stat = player.SPMRound;
                break;
            case DefineStrong.RoundKills:
                stat = player.KillsRound;
                break;
            case DefineStrong.RoundKDR:
                stat = player.KDRRound;
                break;
            case DefineStrong.PlayerRank:
                stat = player.Rank;
                break;
            case DefineStrong.RoundKPM:
                stat = player.KPMRound;
                break;
            case DefineStrong.BattlelogSPM:
                stat = ((player.StatsVerified) ? player.SPM :player.SPMRound);
                break;
            case DefineStrong.BattlelogKDR:
                stat = ((player.StatsVerified) ? player.KDR :player.KDRRound);
                break;
            case DefineStrong.BattlelogKPM:
                stat = ((player.StatsVerified) ? player.KPM :player.KPMRound);
                break;
            default:
                break;
        }
        usTotal = usTotal + stat;
    }
    double usAvg = usTotal / Math.Max(1, us.Count);

    double ruTotal = 0;
    foreach (PlayerModel r in ru) {
        String en = r.Name;
        player = r;
        if (player == null) continue;
        all.Add(player);
        double stat = 0;
        switch (ScrambleBy) {
            case DefineStrong.RoundScore:
                stat = player.ScoreRound;
                break;
            case DefineStrong.RoundSPM:
                stat = player.SPMRound;
                break;
            case DefineStrong.RoundKills:
                stat = player.KillsRound;
                break;
            case DefineStrong.RoundKDR:
                stat = player.KDRRound;
                break;
            case DefineStrong.PlayerRank:
                stat = player.Rank;
                break;
            case DefineStrong.RoundKPM:
                stat = player.KPMRound;
                break;
            case DefineStrong.BattlelogSPM:
                stat = ((player.StatsVerified) ? player.SPM :player.SPMRound);
                break;
            case DefineStrong.BattlelogKDR:
                stat = ((player.StatsVerified) ? player.KDR :player.KDRRound);
                break;
            case DefineStrong.BattlelogKPM:
                stat = ((player.StatsVerified) ? player.KPM :player.KPMRound);
                break;
            default:
                break;
        }
        ruTotal = ruTotal + stat;
    }
    double ruAvg = ruTotal / Math.Max(1, ru.Count);

    String kstat = "?";
    switch (ScrambleBy) {
        case DefineStrong.RoundScore:
            all.Sort(DescendingRoundScore);
            kstat = "S";
            break;
        case DefineStrong.RoundSPM:
            all.Sort(DescendingRoundSPM);
            kstat = "SPM";
            break;
        case DefineStrong.RoundKills:
            all.Sort(DescendingRoundKills);
            kstat = "K";
            break;
        case DefineStrong.RoundKDR:
            all.Sort(DescendingRoundKDR);
            kstat = "KDR";
            break;
        case DefineStrong.PlayerRank:
            all.Sort(DescendingPlayerRank);
            kstat = "R";
            break;
        case DefineStrong.RoundKPM:
            all.Sort(DescendingRoundKPM);
            kstat = "KPM";
            break;
        case DefineStrong.BattlelogSPM:
            all.Sort(DescendingSPM);
            kstat = "bSPM";
            break;
        case DefineStrong.BattlelogKDR:
            all.Sort(DescendingKDR);
            kstat = "bKDR";
            break;
        case DefineStrong.BattlelogKPM:
            all.Sort(DescendingKPM);
            kstat = "bKPM";
            break;
        default:
            all.Sort(DescendingRoundScore);
            break;
    }
    List<String> allNames = new List<String>();
    foreach (PlayerModel p in all) {
        allNames.Add(p.Name); // sorted name list
    }

    // Sort teams
    
    us.Sort(delegate(PlayerModel lhs, PlayerModel rhs) { // descending position in allNames
        if (lhs == null && rhs == null) return 0;
        if (lhs == null) return -1;
        if (rhs == null) return 1;

        int l = allNames.IndexOf(lhs.Name)+1;
        int r = allNames.IndexOf(rhs.Name)+1;
        if (l == 0 && r == 0) return 0;
        if (l == 0) return 1; // 0 sorts to end
        if (r == 0) return 1;
        if (l < r) return -1;
        if (l > r) return 1;
        return 0;
    });
    ru.Sort(delegate(PlayerModel lhs, PlayerModel rhs) { // descending position in allNames
        if (lhs == null && rhs == null) return 0;
        if (lhs == null) return -1;
        if (rhs == null) return 1;

        int l = allNames.IndexOf(lhs.Name)+1;
        int r = allNames.IndexOf(rhs.Name)+1;
        if (l == 0 && r == 0) return 0;
        if (l == 0) return 1; // 0 sorts to end
        if (r == 0) return 1;
        if (l < r) return -1;
        if (l > r) return 1;
        return 0;
    });

    for (int i = 0; i < max; ++i) {
        String u = " ";
        String r = " ";
        String xt = "";
        int sq = 0;
        if (i < us.Count) {
            try {
                player = us[i];
                xt = ExtractTag(player);
                if (!String.IsNullOrEmpty(xt)) {
                    xt = "[" + xt + "]" + player.Name;
                } else {
                    xt = player.Name;
                }
                sq = Math.Max(0, Math.Min(player.Squad, SQUAD_NAMES.Length - 1));
            } catch (Exception e) {}
            u = xt + " (" + SQUAD_NAMES[sq] + ", " + kstat + ":#" + (allNames.IndexOf(player.Name)+1) + ")";
        }
        if (i < ru.Count) {
            try {
                player = ru[i];
                xt = ExtractTag(player);
                if (!String.IsNullOrEmpty(xt)) {
                    xt = "[" + xt + "]" + player.Name;
                } else {
                    xt = player.Name;
                }
                sq = Math.Max(0, Math.Min(player.Squad, SQUAD_NAMES.Length - 1));
            } catch (Exception e) {}
            r = xt + " (" + SQUAD_NAMES[sq] + ", " + kstat + ":#" + (allNames.IndexOf(player.Name)+1) + ")";
        }
        ConsoleDump(String.Format("{0,-32} - {1,32}", u, r));
    }
    ConsoleDump(String.Format("{0,-32} - {1,32}", 
        "US AVG " + kstat + ":" + usAvg.ToString("F2"),
        "RU AVG " + kstat + ":" + ruAvg.ToString("F2")
    ));

}

private String ExtractName(String fullName) {
    String ret = fullName;
    Match m = Regex.Match(fullName, @"\[\w+\](\w+)");
    if (m.Success) {
        ret = m.Groups[1].Value;
    }
    return ret;
}

private void CheckServerInfoUpdate() {
    // Already checked IsRush
    PerModeSettings perMode = GetPerModeSettings();
    if (DateTime.Now.Subtract(fLastServerInfoTimestamp).TotalSeconds >= perMode.SecondsToCheckForNewStage) {
        ServerCommand("serverInfo");
        fLastServerInfoTimestamp = DateTime.Now;
    }
}

private bool AttackerTicketsWithinRangeOfMax(double attacker) {
    if (attacker >= fMaxTickets) return true;
    PerModeSettings perMode = GetPerModeSettings();
    return (attacker + Math.Min(12, 2 * perMode.SecondsToCheckForNewStage / 5) >= fMaxTickets);
}


private double RushAttackerAvgLoss() {
    if (fRushAttackerStageSamples == 0) return fRushAttackerStageLoss;
    return (fRushAttackerStageLoss/fRushAttackerStageSamples);
}

private bool AdjustForMetro(PerModeSettings perMode) {
    if (perMode == null) return false;
    if (!perMode.EnableMetroAdjustments) return false;
    if (fServerInfo == null) return false;
    return (fServerInfo.Map == "MP_Subway");
}

private void LogExternal(String msg) {
    if (msg == null || ExternalLogSuffix == null) return;
    String entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
    entry = entry + msg;
    entry = Regex.Replace(entry, @"\^[bni\d]", String.Empty);
    entry = entry.Replace(" [MULTIbalancer]", String.Empty);
    String date = DateTime.Now.ToString("yyyyMMdd");
    String suffix = (String.IsNullOrEmpty(ExternalLogSuffix)) ? "_mb.log" : ExternalLogSuffix;
    String path = Path.Combine(Path.Combine("Logs", fHost + "_" + fPort), date + suffix);

    try {
        if (!Path.IsPathRooted(path)) path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);

        // Add newline
        entry = entry + "\n";

        lock (ExternalLogSuffix) { // mutex access to log file
            using (FileStream fs = File.Open(path, FileMode.Append)) {
                Byte[] info = new UTF8Encoding(true).GetBytes(entry);
                fs.Write(info, 0, info.Length);
            }
        }
    } catch (Exception ex) {
        ConsoleError("Unable to append to log file: " + path);
        ConsoleError(ex.ToString());
    }
}

void ApplyWizardSettings() {
    ConsoleWrite("Applying Wizard settings ...");

    // Validate the numbers
    ValidateIntRange(ref MaximumPlayersForMode, "Maximum Players For Mode", 8, 64, 64, false);
    ValidateIntRange(ref LowestMaximumTicketsForMode, "Lowest Maximum Tickets For Mode", 20, 10000, 300, false);
    ValidateIntRange(ref HighestMaximumTicketsForMode, "Highest Maximum Tickets For Mode", 20, 10000, 400, false);
    if (HighestMaximumTicketsForMode < LowestMaximumTicketsForMode) {
        ConsoleError("^b" + "Highest Maximum Tickets For Mode" + "^n must be greater than ^b" + "Lowest Maximum Tickets For Mode" + "^n, corrected");
        int tmp = HighestMaximumTicketsForMode;
        HighestMaximumTicketsForMode = LowestMaximumTicketsForMode;
        LowestMaximumTicketsForMode = tmp;
    }

    try {
        String modeName = WhichMode;
        if (modeName == "Conq Small or Dom or Scav") modeName = "Conq Small, Dom, Scav"; // settings don't like commas in enum
        ConsoleWrite("For mode: ^b" + modeName);
        PerModeSettings perMode = null;
        if (fPerMode == null) {
            ConsoleWarn("Settings Wizard failed due to being disabled, please enable the plugin!");
            return;
        }
        if (fPerMode.TryGetValue(modeName, out perMode) && perMode != null) {
            bool isCTF = (modeName == "CTF");

            // Set the per mode Max Players
            perMode.MaxPlayers = MaximumPlayersForMode;
            ConsoleWrite("Set ^bMax Players^n to " + perMode.MaxPlayers);

            // Set the Population ranges
            if (MaximumPlayersForMode == 64) {
                perMode.DefinitionOfHighPopulationForPlayers = 48;
                perMode.DefinitionOfLowPopulationForPlayers = 16;
            } else if (MaximumPlayersForMode >= 56) {
                perMode.DefinitionOfHighPopulationForPlayers = 40;
                perMode.DefinitionOfLowPopulationForPlayers = 16;
            } else if (MaximumPlayersForMode >= 48) {
                perMode.DefinitionOfHighPopulationForPlayers = 32;
                perMode.DefinitionOfLowPopulationForPlayers = 16;
            } else if (MaximumPlayersForMode >= 40) {
                perMode.DefinitionOfHighPopulationForPlayers = 28;
                perMode.DefinitionOfLowPopulationForPlayers = 12;
            } else if (MaximumPlayersForMode >= 32) {
                perMode.DefinitionOfHighPopulationForPlayers = 24;
                perMode.DefinitionOfLowPopulationForPlayers = 8;
            } else if (MaximumPlayersForMode >= 24) {
                perMode.DefinitionOfHighPopulationForPlayers = 16;
                perMode.DefinitionOfLowPopulationForPlayers = 8;
            } else if (MaximumPlayersForMode >= 16) {
                perMode.DefinitionOfHighPopulationForPlayers = 12;
                perMode.DefinitionOfLowPopulationForPlayers = 4;
            } else {
                perMode.DefinitionOfHighPopulationForPlayers = 6;
                perMode.DefinitionOfLowPopulationForPlayers = 4;
            }
            ConsoleWrite("Set ^bDefinition Of High Population For Players^n to " + perMode.DefinitionOfHighPopulationForPlayers);
            ConsoleWrite("Set ^bDefinition Of Low Population For Players^n to " + perMode.DefinitionOfLowPopulationForPlayers);

            // Set the Phase ranges
            if (!isCTF) {
                double high = HighestMaximumTicketsForMode;
                double low = LowestMaximumTicketsForMode;
                double late = low/4.0; // late always 25% of low
                // Try 33% of high first
                double delta = high / 3.0;
                if ((low - delta - late) < Math.Min(50.0, low/2)) {
                    // Try 25% of high
                    delta = high / 4.0;
                    if ((low - delta - late) < Math.Min(50.0, low/2)) {
                        // Use 33% of low
                        delta = low / 3.0;
                    }
                }
                perMode.DefinitionOfEarlyPhaseFromStart = Math.Min(300, Convert.ToInt32(delta)); // adaptive early
                perMode.DefinitionOfLatePhaseFromEnd = Math.Min(300, Convert.ToInt32(late));
                ConsoleWrite("Set ^bDefinition Of Early Phase As Tickets From Start^n to " + perMode.DefinitionOfEarlyPhaseFromStart);
                ConsoleWrite("Set ^bDefinition Of Late Phase As Tickets From End^n to " + perMode.DefinitionOfLatePhaseFromEnd);
            } else {
                ConsoleWrite("CTF Phase definitions cannot be set with the wizard, skipping.");
            }

            if (MetroIsInMapRotation && modeName.Contains("Conq")) {
                // Use half of low
                perMode.MetroAdjustedDefinitionOfLatePhase = LowestMaximumTicketsForMode / 2;
                ConsoleWrite("Set ^bMetro Adjusted Defintion Of Late Phase^n to " + perMode.MetroAdjustedDefinitionOfLatePhase);
            }

            switch (PreferredStyleOfBalancing) {
                case PresetItems.Standard:

                    EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
                    MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
                    break;

                case PresetItems.Aggressive:

                    EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast,     Speed.Fast,     Speed.Fast};
                    MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast,     Speed.Fast,     Speed.Fast};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Fast,     Speed.Fast,     Speed.Fast};
            
                    break;

                case PresetItems.Passive:

                    EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow,     Speed.Slow,     Speed.Slow};
                    MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow,     Speed.Slow,     Speed.Slow};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
                    break;

                case PresetItems.Intensify:

                    // TBD: Needs Speed.OverBalance (similar to Fast, but puts more players on losing team)
                    EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
                    MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
                    break;

                case PresetItems.Retain:

                    EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
                    MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Slow, Speed.Adaptive,     Speed.Slow};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
                    break;

                case PresetItems.BalanceOnly:

                    EarlyPhaseBalanceSpeed = new Speed[3]   { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
                    MidPhaseBalanceSpeed = new Speed[3]     { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
            
                    break;

                case PresetItems.UnstackOnly:

                    EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};
                    MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};
                    LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Unstack,     Speed.Unstack,     Speed.Unstack};
            
                    break;

                case PresetItems.None:
                    break;
                default:
                    break;
            }

            if (MetroIsInMapRotation && modeName.Contains("Conq")) {
                // In sure that Metro adjustment results in a Stop speed
                LatePhaseBalanceSpeed = new Speed[3] {Speed.Stop,  Speed.Stop, Speed.Stop};
            }

            ConsoleWrite("Please review your Section 3 Early, Mid and Late Balance Speeds set to style " + PreferredStyleOfBalancing);

            ConsoleWrite("COMPLETED application of Wizard settings! Please review your Section 8 settings for ^b" + modeName);
        }
    } catch (Exception e) {
        ConsoleException(e);
    }
}

private void UpgradePreV1Settings() {
     /* ===== SECTION 6 - Unswitcher ===== */
     ForbidSwitchingAfterAutobalance = (ForbidSwitchAfterAutobalance) ? UnswitchChoice.Always : UnswitchChoice.Never;
     ForbidSwitchingAfterDispersal = (ForbidSwitchAfterDispersal) ? UnswitchChoice.Always : UnswitchChoice.Never;
     ForbidSwitchingToBiggestTeam = (ForbidSwitchToBiggestTeam) ? UnswitchChoice.Always : UnswitchChoice.Never;
     ForbidSwitchingToWinningTeam = (ForbidSwitchToWinningTeam) ? UnswitchChoice.Always : UnswitchChoice.Never;

    if (!EnableUnstacking) { // Assume settings were customized and should be left unchanged if True
        /* ===== SECTION 8 - Per-Mode Settings ===== */
        List<String> simpleModes = GetSimplifiedModes();

        foreach (String sm in simpleModes) {
            PerModeSettings oneSet = null;
            if (fPerMode.TryGetValue(sm, out oneSet) && oneSet != null) {
                PerModeSettings def = new PerModeSettings(sm);
                oneSet.DelaySecondsBetweenSwapGroups = def.DelaySecondsBetweenSwapGroups;
                oneSet.MaxUnstackingSwapsPerRound = def.MaxUnstackingSwapsPerRound;
                oneSet.NumberOfSwapsPerGroup = def.NumberOfSwapsPerGroup;
            }
        }
    }
}

private bool Forbid(PerModeSettings perMode, UnswitchChoice choice) {
    if (choice == UnswitchChoice.Always) return true;
    if (choice == UnswitchChoice.Never) return false;
    
    bool ret = false;
    if (choice == UnswitchChoice.LatePhaseOnly) {
        if (perMode == null) return false;
        ret = (GetPhase(perMode, false) == Phase.Late);
    }
    return ret;
}

private void MergeWithFile(String[] var, List<String> list) {
    if (var == null || list == null) return;
    list.Clear();
    int n = 0;
    foreach (String s in var) {
        if (n == 0 && Regex.Match(s, @"^\s*<").Success) {
            String fileName = s.Replace("<", String.Empty);
            String path = Path.Combine("Configs", fileName);

            try {
                if (!Path.IsPathRooted(path)) path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);
                Byte[] buffer = new Byte[128]; // 64k buffer
                int got = 0;
                UTF8Encoding utf = new UTF8Encoding(false, true);
                StringBuilder sb = new StringBuilder();

                using (FileStream fs = File.Open(path, FileMode.Open)) {
                    while ((got = fs.Read(buffer, 0, buffer.Length-1)) > 0) {
                        String tmp = utf.GetString(buffer, 0, got);
                        foreach (Char c in tmp) {
                            if (c == '\n') {
                                list.Add(sb.ToString());
                                sb = new StringBuilder();
                            } else if (c == '\r') {
                                continue;
                            } else {
                                sb.Append(c);
                            }
                        }
                    }
                    if (sb.Length > 0) {
                        list.Add(sb.ToString());
                    }
                }
            } catch (Exception ex) {
                ConsoleError("Unable to merge file: " + fileName);
                ConsoleError(ex.GetType().ToString() + ": " + ex.Message);
            }
            if (list.Count > 0) {
                ConsoleDebug("MergeWithFile ^b" + fileName + "^n contained:");
                foreach (String mf in list) {
                    ConsoleDebug(mf);
                }
                ConsoleDebug("MergeWithFile, end of ^b" + fileName + "^n");
            }
        } else {
            list.Add(s);
        }
        n = n + 1;
    }
}


private void SetDispersalListGroups() {
    /*
    This function scans the Disperse Evenly List for lines that specify
    a group. A group starts with a single digit, 1 thru 4, followed by
    whitespace, followed by a whitespace separated list of name/tag/guid items
    that should be assigned to the specified group list.
    */
    if (fSettingDisperseEvenlyList.Count == 0) return;
    if (fSettingDisperseEvenlyList.Count == 1 && fSettingDisperseEvenlyList[0] == DEFAULT_LIST_ITEM) return;
    foreach (List<String> gl in fDispersalGroups) {
        if (gl == null) continue;
        gl.Clear();
    }
    List<String> copy = new List<String>(fSettingDisperseEvenlyList);
    foreach (String line in copy) {
        try {
            if (String.IsNullOrEmpty(line)) continue;
            String[] tokens = Regex.Split(line, @"\s+");
            if (tokens != null && tokens.Length == 1 && !String.IsNullOrEmpty(tokens[0])) {
                // Not a group, so retain
                continue;
            }
            // Otherwise, check for a group specifier
            bool first = true;
            bool remove = false;
            List<String> group = null;
            bool bad = false;
            int groupId = 0;
            // Scan one line
            foreach (String token in tokens) {
                if (String.IsNullOrEmpty(token)) continue;
                // First token might be group specifier, if so move remaining tokens to group list
                if (first) {
                    if (Regex.Match(token, @"^[1234]").Success) {
                        // It's a group
                        if (Int32.TryParse(token, out groupId)) {
                            if (groupId >= 1 && groupId <= 4) {
                                group = fDispersalGroups[groupId];
                                remove = true;
                            } else bad = true;
                        }
                    }
                    first = false;
                    if (group != null) continue; // skip group id
                }
                if (group == null) {
                    break; // not a group specification, get out of this token parsing loop
                } else if (group.Contains(token)) {
                    ConsoleWarn("In Disperse Evenly List in Group " + groupId + ", ^b" + token + "^n is duplicated, please remove all duplicates");
                } else {
                    // Add the rest of the tokens to the group
                    group.Add(token);
                }
            }
            if (bad) {
                // Warn, leave line in original as is
                ConsoleWarn("In Disperse Evenly List, unrecognized grouping, possible typo? " + line);
            } else if (remove) {
                // Remove lines that define groups from the normal list
                fSettingDisperseEvenlyList.Remove(line);
            }
        } catch (Exception e) {
            ConsoleWarn("In Disperse Evenly List, skipping bad line: " + line);
            ConsoleWarn(e.Message);
        }
    }
    // Check for uniqueness
    List<String> uniq = new List<String>();
    for (int i = 1; i <= 4; ++i) {
        copy = new List<String>(fDispersalGroups[i]);
        foreach (String s in copy) {
            if (uniq.Contains(s)) {
                ConsoleWarn("In Disperse Evenly List in Group " + i + ", ^b" + s + "^n is duplicated, please remove all duplicates");
                fDispersalGroups[i].Remove(s);
            } else {
                uniq.Add(s);
            }
        }
    }
    copy = new List<String>(fSettingDisperseEvenlyList);
    foreach (String s in copy) {
        if (uniq.Contains(s)) {
            ConsoleWarn("In Disperse Evenly List, ^b" + s + "^n is duplicated, please remove all duplicates");
            fSettingDisperseEvenlyList.Remove(s);
        } else {
            uniq.Add(s);
        }
    }
    // debugging
    if (DebugLevel >= 6) {
        String g1 = "Group 1: ";
        String g2 = "Group 2: ";
        String g3 = "Group 3: ";
        String g4 = "Group 4: ";
        if (fDispersalGroups[1].Count > 0) {
            g1 = g1 + String.Join(", ", fDispersalGroups[1].ToArray());
            ConsoleDebug("SetDispersalListGroups " + g1);
        }
        if (fDispersalGroups[2].Count > 0) {
            g2 = g2 + String.Join(", ", fDispersalGroups[2].ToArray());
            ConsoleDebug("SetDispersalListGroups " + g2);
        }
        if (fDispersalGroups[3].Count > 0) {
            g3 = g3 + String.Join(", ", fDispersalGroups[3].ToArray());
            ConsoleDebug("SetDispersalListGroups " + g3);
        }
        if (fDispersalGroups[4].Count > 0) {
            g4 = g4 + String.Join(", ", fDispersalGroups[4].ToArray());
            ConsoleDebug("SetDispersalListGroups " + g4);
        }
        ConsoleDebug("SetDispersalListGroups remaining list: " + String.Join(", ", fSettingDisperseEvenlyList.ToArray()));
    }
}

private void AssignGroups() {
    int grandTotal = 0;
    List<int> availableTeamIds = new List<int>(new int[4]{1, 2, 3, 4});

    try {
        // Insure that dispersal groups have been assigned
        List<PlayerModel> all = new List<PlayerModel>();
        all.AddRange(fTeam1);
        all.AddRange(fTeam2);
        all.AddRange(fTeam3);
        all.AddRange(fTeam4);
        foreach (PlayerModel p in all) {
            if (IsDispersal(p)) {
                if (DebugLevel >= 6) ConsoleDebug("AssignGroups assigned ^b" + p.FullName + "^n to Group " + p.DispersalGroup);
            }
        }

        // Clear
        for (int groupId = 1; groupId <= 4; ++groupId) {
            fGroupAssignments[groupId] = 0;
        }

        // Compute distribution of groups
        int[,] count = new int[5,5]{ // group,team
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0}
        };

        foreach (PlayerModel p in all) {
            if (p.DispersalGroup == 0) continue;
            ++count[p.DispersalGroup,p.Team];
            ++grandTotal;
        }

        if (grandTotal == 0) {
            ConsoleDebug("AssignGroups: No players or no groups, defaulting to 1,2,3,4");
            for (int i = 1; i <= 4; ++i) {
                fGroupAssignments[i] = i;
            }
            return;
        }

        // Assign team to group that has the most players in that team
        for (int groupId = 1; groupId <= 4; ++groupId) {
            // Find the max team count for this group
            int most = 0;
            int num = 0;
            foreach (int teamId in availableTeamIds) {
                if (count[groupId,teamId] > num) {
                    most = teamId;
                    num = count[groupId,teamId];
                }
            }
            if (most != 0) {
                if (!availableTeamIds.Contains(most)) {
                    throw new Exception("team " + most + " already allocated!");
                }
                fGroupAssignments[groupId] = most;
                availableTeamIds.Remove(most);
            }
        }

        // Assign unallocated teams
        for (int groupId = 1; groupId <= 4; ++groupId) {
            if (fGroupAssignments[groupId] == 0) {
                if (availableTeamIds.Count == 0) {
                    throw new Exception("Ran out of team IDs!");
                }
                int ti = availableTeamIds[0];
                fGroupAssignments[groupId] = ti;
                availableTeamIds.Remove(ti);
            }
        }

        // Sanity check
        availableTeamIds.Clear();
        for (int groupId = 1; groupId <= 4; ++groupId) {
            if (availableTeamIds.Contains(fGroupAssignments[groupId])) {
                throw new Exception("Duplicate assignment!");
            } else {
                availableTeamIds.Add(fGroupAssignments[groupId]);
            }
        }
    } catch (Exception e) {
        ConsoleException(e);
        ConsoleDebug("AssignGroups: Defaulting to 1,2,3,4");
        for (int i = 1; i <= 4; ++i) {
            fGroupAssignments[i] = i;
        }
    } finally {
        if (DebugLevel >= 6) {
            String msg = "Group assignments: ";
            for (int i = 1; i <= 4; ++i) {
                msg = msg + fGroupAssignments[i];
                if (i < 4) msg = msg + "/";
            }
            ConsoleWrite(msg);
        }
    }
}


private void SetFriends() {

    if (fSettingFriendsList.Count == 0) return;
    if (fSettingFriendsList.Count == 1 && fSettingFriendsList[0] == DEFAULT_LIST_ITEM) return;
    
    fFriends.Clear();
    int key = 1;

    foreach (String line in fSettingFriendsList) {
        try {
            if (String.IsNullOrEmpty(line)) continue;
            if (line == DEFAULT_LIST_ITEM) continue;
            String[] tokens = Regex.Split(line, @"\s+");
            if (tokens != null && tokens.Length == 1 && !String.IsNullOrEmpty(tokens[0])) {
                throw new Exception("Line contains only one name");
            }
            // Otherwise, store the sub-list of friends
            List<String> subList = new List<String>();
            foreach (String token in tokens) {
                if (String.IsNullOrEmpty(token)) continue;
                subList.Add(token);
            }
            fFriends[key] = subList;
            ++key;
        } catch (Exception e) {
            ConsoleWarn("In Friends List, skipping bad line: " + line);
            ConsoleWarn(e.Message);
        }
    }
    // Check uniqueness
    fAllFriends.Clear();
    foreach (int k in fFriends.Keys) {
        List<String> copy = new List<String>(fFriends[k]);
        foreach (String name in copy) {
            if (fAllFriends.Contains(name)) {
                ConsoleWarn("In Friends List, ^b" + name + "^n is duplicated, please remove all duplicates");
                fFriends[k].Remove(name);
            } else {
                fAllFriends.Add(name);
            }
        }
    }
    // Update player model
    UpdateFriends();
    // debugging
    if (DebugLevel >= 6) {
        ConsoleDebug("SetFriends list of friends: ");
        foreach (int k in fFriends.Keys) {
            ConsoleDebug(k.ToString() + ": " + String.Join(", ", fFriends[k].ToArray()));
        }
    }
}

private void UpdateFriends() {
    // short-circuit
    if (fSettingFriendsList.Count == 0) return;
    if (fSettingFriendsList.Count == 1 && fSettingFriendsList[0] == DEFAULT_LIST_ITEM) return;

    lock (fAllPlayers) {
        foreach (String name in fAllPlayers) {
            PlayerModel friend = null;
            lock (fKnownPlayers) {
                if (!fKnownPlayers.TryGetValue(name, out friend) || friend == null) continue;
            }
            UpdatePlayerFriends(friend);
        }
    }
}

private void UpdatePlayerFriends(PlayerModel friend) {
    if (friend == null) return;
    friend.Friendex = -1; 
    if (!fAllFriends.Contains(friend.Name)) return; // optmization, avoid search if not a friend

    String guid = (String.IsNullOrEmpty(friend.EAGUID)) ? INVALID_NAME_TAG_GUID : friend.EAGUID;
    String tag = ExtractTag(friend);
    if (String.IsNullOrEmpty(tag)) tag = INVALID_NAME_TAG_GUID;

    foreach (int key in fFriends.Keys) {
        try {
            List<String> subList = fFriends[key];   
            if (subList.Contains(friend.Name)
            || subList.Contains(tag)
            || subList.Contains(guid)) {
                friend.Friendex = key;
                break;
            }
        } catch (Exception e) {
            if (DebugLevel >= 7) ConsoleException(e);
        }
    }
}


private int CountMatchingFriends(PlayerModel player) {
    if (player == null) return 0;
    if (player.Friendex == -1) return 0;
    if (player.Team == 0 || player.Squad == 0) return 0;
    int team = player.Team;
    int squad = player.Squad;

    List<PlayerModel> teamList = GetTeam(team);
    if (teamList == null) return 0;

    int same = 0;

    foreach (PlayerModel mate in teamList) {
        if (mate.Squad != squad) continue;
        if (mate.Friendex == player.Friendex) ++same;
    }

    String sname = squad.ToString();
    if (squad > 0 && squad <= SQUAD_NAMES.Length) {
        sname = SQUAD_NAMES[squad];
    }
    
    if (DebugLevel >= 6 && same > 1) DebugBalance("Count of matching friends for player ^b" + player.Name + "^n in " + sname + ", found " + same + " matching friends (friendex = " + player.Friendex + ")");

    return same;
}

/* === NEW_NEW_NEW === */



public void LaunchCheckForPluginUpdate() {
    try {
        double alive = 0;
        DateTime since = DateTime.MinValue;
        lock (fUpdateThreadLock) {
            alive = fUpdateThreadLock.MaxDelay; // repurpose MaxDelay to be a thread counter
            since = fUpdateThreadLock.LastUpdate;
        }
        if (alive > 0) {
            DebugWrite("Unable to check for updates, " + alive + " threads active for " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F1") + " seconds!", 3);
            return;
        }

        Thread t = new Thread(new ThreadStart(CheckForPluginUpdate));
        t.IsBackground = true;
        t.Name = "updater";
        DebugWrite("Starting updater thread ...", 3);
        t.Start();
        Thread.Sleep(2);
    } catch (Exception e) {
        ConsoleException(e);
    }
}


public void CheckForPluginUpdate() { // runs in one-shot thread
	try {
        lock (fUpdateThreadLock) {
            fUpdateThreadLock.MaxDelay = fUpdateThreadLock.MaxDelay + 1;
            fUpdateThreadLock.LastUpdate = DateTime.Now;
        }
		XmlDocument xml = new XmlDocument();
        try {
            xml.Load("https://myrcon.com/procon/plugins/report/format/xml/plugin/MULTIbalancer");
        } catch (System.Security.SecurityException e) {
            if (DebugLevel >= 8) ConsoleException(e);
            ConsoleWrite(" ");
            ConsoleWrite("^8^bNOTICE! Unable to check for plugin update!");
            ConsoleWrite("Tools => Options... => Plugins tab: ^bPlugin security^n is set to ^bRun plugins in a sandbox^n.");
            //ConsoleWrite("Please add ^bmyrcon.com^n to your trusted ^bOutgoing connections^n");
            ConsoleWrite("Consider changing to ^bRun plugins with no restrictions.^n");
            ConsoleWrite("Alternatively, check the ^bPlugins^n forum for an update to this plugin.");
            ConsoleWrite(" ");
            return;
        } 
        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Got " + xml.BaseURI);

        /*
        Example:
        <report>
            <id>5132671</id>
            <plugin>
                <id>478344</id>
                <uid>MultiBalancer</uid>
                <name>MULTI-balancer</name>
            </plugin>
            <version>
                <id>965536</id>
                <major>1</major>
                <minor>0</minor>
                <maintenance>0</maintenance>
                <build>1</build>
            </version>
            <sum_in_use>22</sum_in_use>
            <avg_in_use>22.0000</avg_in_use>
            <max_in_use>22</max_in_use>
            <min_in_use>22</min_in_use>
            <stamp>2013-05-10 10:00:04</stamp>
        </report>
        */

        XmlNodeList rows = xml.SelectNodes("//report");
        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: # rows = " + rows.Count);
        if (rows.Count == 0) return;
        Dictionary<String,int> versions = new Dictionary<String,int>();
		foreach (XmlNode tr in rows) {
            XmlNode ver = tr.SelectSingleNode("version");
            XmlNode count = tr.SelectSingleNode("sum_in_use");
            if (ver != null && count != null) {
                int test = 0;
                XmlNode major = ver.SelectSingleNode("major");
                if (major != null && !Int32.TryParse(major.InnerText, out test)) continue;
                XmlNode minor = ver.SelectSingleNode("minor");
                if (minor != null && !Int32.TryParse(minor.InnerText, out test)) continue;
                XmlNode maint = ver.SelectSingleNode("maintenance");
                if (maint != null && !Int32.TryParse(maint.InnerText, out test)) continue;
                XmlNode build = ver.SelectSingleNode("build");
                if (build != null && !Int32.TryParse(build.InnerText, out test)) continue;
                String vt = major.InnerText + "." + minor.InnerText + "." + maint.InnerText + "." + build.InnerText;
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Version: " + vt + ", Count: " + count.InnerText);
                int n = 0;
                if (!Int32.TryParse(count.InnerText, out n)) continue; 
                versions[vt] = n;
            }
        }

        // Select current version and any "later" versions
        int usage = 0;
        String myVersion = GetPluginVersion();
        if (!versions.TryGetValue(myVersion, out usage)) {
            DebugWrite("CheckForPluginUpdate: " + myVersion + " not found!", 8);
            return;
        }

        // numeric sort
        List<String> byNumeric = new List<String>();
        byNumeric.AddRange(versions.Keys);
        // Sort numerically descending
        byNumeric.Sort(delegate(String lhs, String rhs) {
            if (lhs == rhs) return 0;
            if (String.IsNullOrEmpty(lhs)) return 1;
            if (String.IsNullOrEmpty(rhs)) return -1;
            uint l = VersionToNumeric(lhs);
            uint r = VersionToNumeric(rhs);
            if (l < r) return 1;
            if (l > r) return -1;
            return 0;
        });
        DebugWrite("CheckForPluginUpdate: sorted version list:", 7);
        foreach (String u in byNumeric) {
            DebugWrite(u + " (" + String.Format("{0:X8}", VersionToNumeric(u)) + "), count = " + versions[u], 7);
        }

        int position = byNumeric.IndexOf(myVersion);

        DebugWrite("CheckForPluginUpdate: found " + position + " newer versions", 5);

        if (position != 0) {
            // Newer versions found
            // Find the newest version with the largest number of usages
            int hasMost = -1;
            int most = 0;
            for (int i = position-1; i >= 0; --i) {
                int newerVersionCount = versions[byNumeric[i]];
                if (hasMost == -1 || most < newerVersionCount) {
                    // Skip newer versions that don't have enough usage yet
                    if (most > 0 && newerVersionCount < MIN_UPDATE_USAGE_COUNT) continue;
                    hasMost = i;
                    most = versions[byNumeric[i]];
                }
            }

            if (hasMost != -1 && hasMost < byNumeric.Count && most >= MIN_UPDATE_USAGE_COUNT) {
                String newVersion = byNumeric[hasMost];
                ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                ConsoleWrite(" ");
                ConsoleWrite("^8^bA NEW VERSION OF THIS PLUGIN IS AVAILABLE!");
                ConsoleWrite(" ");
                ConsoleWrite("^8^bPLEASE UPDATE TO VERSION: ^0" + newVersion);
                ConsoleWrite(" ");
                ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

                TaskbarNotify(GetPluginName() + ": new version available!", "Please download and install " + newVersion); 
            }
        }
	} catch (Exception e) {
		if (DebugLevel >= 8) ConsoleException(e);
	} finally {
        // Update check time
        fLastVersionCheckTimestamp = DateTime.Now;
        // Update traffic control
        lock (fUpdateThreadLock) {
            fUpdateThreadLock.MaxDelay = fUpdateThreadLock.MaxDelay - 1;
            fUpdateThreadLock.LastUpdate = DateTime.MinValue;
        }
        DebugWrite("Updater thread finished!", 3);
    }
}

private uint VersionToNumeric(String ver) {
    uint numeric = 0;
    byte part = 0;
    Match m = Regex.Match(ver, @"^\s*([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)(\w*)\s*$");
    if (m.Success) {
        for (int i = 1; i < 5; ++i) {
            if (!Byte.TryParse(m.Groups[i].Value, out part)) {
                part = 0;
            }
            numeric = (numeric << 8) | part;
        }
    }
    return numeric;
}


private void LogStatus(bool isFinal) {
  try {
    // If server is empty, log status only every 60 minutes
    if (!isFinal && DebugLevel < 9 && TotalPlayerCount == 0) {
        if (fRoundStartTimestamp != DateTime.MinValue && DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes <= 60) {
            return;
        } else {
            fRoundStartTimestamp = DateTime.Now;
        }
    }

    if (!isFinal && (DebugLevel == 4)) ConsoleWrite("+------------------------------------------------+");

    Speed balanceSpeed = Speed.Adaptive;

    String tm = fTickets[1] + "/" + fTickets[2];
    if (IsSQDM()) tm = tm + "/" + fTickets[3] + "/" + fTickets[4];
    if (IsRush()) tm = tm  + "(" + Math.Max(fTickets[1]/2, fMaxTickets - (fRushMaxTickets - fTickets[2])) + ")";
    if (IsCTF()) tm = GetTeamPoints(1) + "/" + GetTeamPoints(2);

    double goal = 0;
    if (fServerInfo != null && Regex.Match(fServerInfo.GameMode, @"(?:TeamDeathMatch|SquadDeathMatch)").Success) {
        if (fServerInfo.TeamScores != null && fServerInfo.TeamScores.Count > 0) {
            goal = fServerInfo.TeamScores[0].WinningScore;
        }
    }

    if (goal == 0) {
        if (fMaxTickets != -1) tm = tm + " <- [" + fMaxTickets.ToString("F0") + "]";
    } else {
        tm = tm + " -> [" + goal.ToString("F0") + "]";
    }

    String rt = GetTimeInRoundString();

    PerModeSettings perMode = GetPerModeSettings();

    String metroAdj = (perMode.EnableMetroAdjustments) ? ", Metro Adjustments Enabled" : String.Empty;
    String unstackDisabled = (!EnableUnstacking) ? ", Unstacking Disabled" : String.Empty;
    String logOnly = (EnableLoggingOnlyMode) ? ", Logging Only Mode Enabled" : String.Empty;

    DebugWrite("^bStatus^n: Plugin state = " + fPluginState + ", game state = " + fGameState + metroAdj + unstackDisabled + logOnly, 6);
    int useLevel = (isFinal) ? 2 : 4;
    if (IsRush()) {
        DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", stage = " + fRushStage + ", time in round = " + rt + ", tickets = " + tm, useLevel);
    } else if (IsCTF()) {
        DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", time in round = " + rt + ", points = " + tm, useLevel);
    } else {
        DebugWrite("^bStatus^n: Map = " + FriendlyMap + ", mode = " + FriendlyMode + ", time in round = " + rt + ", tickets = " + tm, useLevel);
    }
    if (fPluginState == PluginState.Active) {
        double secs = DateTime.Now.Subtract(fLastBalancedTimestamp).TotalSeconds;
        if (!fBalanceIsActive || fLastBalancedTimestamp == DateTime.MinValue) secs = 0;
        /*
        PerModeSettings perMode = null;
        String simpleMode = String.Empty;
        if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) 
          && fPerMode.TryGetValue(simpleMode, out perMode) && perMode != null) {
        */
        if (perMode != null) {
            balanceSpeed = GetBalanceSpeed(perMode);
            double unstackRatio = GetUnstackTicketRatio(perMode);
            String activeTime = (secs > 0) ? "^1active (" + secs.ToString("F0") + " secs)^0" : "not active";
            DebugWrite("^bStatus^n: Autobalance is " + activeTime + ", phase = " + GetPhase(perMode, false) + ", population = " + GetPopulation(perMode, false) + ", speed = " + balanceSpeed + ", unstack when ticket ratio >= " + (unstackRatio * 100).ToString("F0") + "%", 4);
        }
    }
    if (!IsModelInSync()) {
        double toj = (fTimeOutOfJoint == 0) ? 0 : GetTimeInRoundMinutes() - fTimeOutOfJoint;
        DebugWrite("^bStatus^n: Model not in sync for " + toj.ToString("F1") + " mins: fMoving = " + fMoving.Count + ", fReassigned = " + fReassigned.Count, 6);
    }

    String raged = fRageQuits.ToString() + "/" + fTotalQuits + " raged, ";
    useLevel = (isFinal) ? 2 : 5;
    DebugWrite("^bStatus^n: " + raged + fReassignedRound + " reassigned, " + fBalancedRound + " balanced, " + fUnstackedRound + " unstacked, " + fUnswitchedRound + " unswitched, " + fExcludedRound + " excluded, " + fExemptRound + " exempted, " + fFailedRound + " failed; of " + fTotalRound + " TOTAL", useLevel);
    
    useLevel = (isFinal) ? 2 : 4;
    if (IsSQDM()) {
        DebugWrite("^bStatus^n: Team counts [" + TotalPlayerCount + "] = " + fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D), with " + fUnassigned.Count + " unassigned", useLevel);
    } else {
        DebugWrite("^bStatus^n: Team counts [" + TotalPlayerCount + "] = " + fTeam1.Count + "(US) vs " + fTeam2.Count + "(RU), with " + fUnassigned.Count + " unassigned", useLevel);
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
    String next = (diff > MaxDiff() && fGameState == GameState.Playing && balanceSpeed != Speed.Stop && !fBalanceIsActive) ? "^n^0 ... autobalance will activate as soon as possible!" : "^n";
    
    DebugWrite("^bStatus^n: Team difference = " + ((diff > MaxDiff()) ? "^8^b" : "^b") + diff + next, 4);

  } catch (Exception e) {
    ConsoleException(e);
  }
}

} // end MULTIbalancer











/* ======================== UTILITIES ============================= */

#region UTILITIES







static class MULTIbalancerUtils {
    public static bool IsEqual(MULTIbalancer lhs, MULTIbalancer.PresetItems preset) {
        MULTIbalancer rhs = new MULTIbalancer(preset);
        return (lhs.CheckForEquality(rhs));
    }
    
    public static void UpdateSettingsForPreset(MULTIbalancer lhs, MULTIbalancer.PresetItems preset) {
        try {
            MULTIbalancer rhs = new MULTIbalancer(preset);
        
            lhs.DebugWrite("UpdateSettingsForPreset to " + preset, 6);

            lhs.OnWhitelist = rhs.OnWhitelist;
            lhs.OnFriendsList = rhs.OnFriendsList;
            lhs.TopScorers = rhs.TopScorers;
            lhs.SameClanTagsInSquad = rhs.SameClanTagsInSquad;
            lhs.SameClanTagsForRankDispersal = rhs.SameClanTagsForRankDispersal;
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

            lhs.ForbidSwitchingAfterAutobalance = rhs.ForbidSwitchingAfterAutobalance;
            lhs.ForbidSwitchingToWinningTeam = rhs.ForbidSwitchingToWinningTeam;
            lhs.ForbidSwitchingToBiggestTeam = rhs.ForbidSwitchingToBiggestTeam;
            lhs.ForbidSwitchingAfterDispersal = rhs.ForbidSwitchingAfterDispersal;
            lhs.EnableImmediateUnswitch = rhs.EnableImmediateUnswitch;

        } catch (Exception) { }
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

    public static bool EqualArrays(MULTIbalancer.Speed[] lhs, MULTIbalancer.Speed[] rhs) {
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

    public static String ArrayToString(MULTIbalancer.Speed[] a) {
        String ret = String.Empty;
        bool first = true;
        if (a == null || a.Length == 0) return ret;
        for (int i = 0; i < a.Length; ++i) {
            if (first) {
                ret = Enum.GetName(typeof(MULTIbalancer.Speed), a[i]);
                first = false;
            } else {
                ret = ret + ", " + Enum.GetName(typeof(MULTIbalancer.Speed), a[i]);
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

    public static MULTIbalancer.Speed[] ParseSpeedArray(MULTIbalancer plugin, String s) {
        MULTIbalancer.Speed[] speeds = new MULTIbalancer.Speed[3] {
            MULTIbalancer.Speed.Adaptive,
            MULTIbalancer.Speed.Adaptive,
            MULTIbalancer.Speed.Adaptive
        };
        if (String.IsNullOrEmpty(s) || !s.Contains(",")) {
            if (s == null) s = "(null)";
            plugin.ConsoleWarn("Bad balance speed setting: " + s);
            return speeds;
        }
        String[] strs = s.Split(new Char[] {','});
        if (strs.Length != 3) {
            plugin.ConsoleWarn("Wrong number of speeds, should be 3, separated by commas: " + s);
            return speeds;
        }
        for (int i = 0; i < speeds.Length; ++i) {
            try {
                speeds[i] = (MULTIbalancer.Speed)Enum.Parse(typeof(MULTIbalancer.Speed), strs[i]);
            } catch (Exception) {
                plugin.ConsoleWarn("Bad balance speed value: " + strs[i]);
                speeds[i] = MULTIbalancer.Speed.Adaptive;
            }
        }
        return speeds;
    }

#region HTML_DOC
    public const String HTML_DOC = @"
<h1>Multi-Balancer &amp; Unstacker, including SQDM</h1>
<h3>Acknowledgments</h3>
<p>This plugin would not have been possible without the help and support of these individuals and communities:<br></br>
<small>myrcon.com staff, [C2C]Blitz, [FTB]guapoloko, [Xtra]HexaCanon, [11]EBassie, Firejack, [IAF]SDS, dyn, Jaythegreat1, ADKGamers, AgentHawk, TreeSaint, Taxez, PatPgtips, Hutchew, LumpyNutz, popbndr, tarreltje, 24Flat, [Oaks]kcuestag ... and many others</small></p>

<p>For BF3, this plugin does live round team balancing and unstacking for all game modes, including Squad Deathmatch (SQDM).</p>

<h2>NOTICE</h2>
<p>This plugin is free to use, forever. Support is provided on a voluntary basic, when time is available, by the author and the user community. Use at your own risk, no guarantees are made or implied (complete notice text is in the source code). Some of the code in this plugin (Battlelog and BattlelogCache code, plugin framework, other odds & ends) was directly derived from Insane Limits by micovery. Inspiration for the plugin settings came from TrueBalancer by Panther and all of the members of the design discussion group, some of whom are listed above in the acknowledgments.</p>

<p><b>Section 7 of settings is intentionally not defined.</b></p>

<h2>Description</h2>
<p>This plugin performs several automated operations:
<ul>
<li>Team balancing for all modes</li>
<li>Unstacking a stacked team</li>
<li>Unswitching players who team switch</li>
</ul></p>

<p>This plugin only moves players when they die. No players are killed by admin to be moved, with the single exception of players who attempt to switch teams when team switching is not allowed -- those players may be admin killed before being moved back to their original team. This plugin also monitors new player joins and if the game server would assign a new player to the wrong team (a team with 30 players when another team only has 27 players), the plugin will <i>reassign</i> the player to the team that needs players for balance. This all happens before a player spawns, so they will not be aware that they were reassigned.</p>

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
</table>
<b><font color=#FF0000>Standard, Retain, and BalanceOnly are recommended to admins new to this plugin.</font></b> Aggressive and Intensify are not recommended for admins new to this plugin.</p>

<p>2) Review plugin section <b>5. Messages</b> and change any messages you don't like.</p>

<p>3) Find your game mode in Section 8 and review the settings. Adjust the <b>Max Players</b> and <b>Definition Of ...</b> settings as needed. Or, <b>Enable Settings Wizard</b> in Section 0, <i>fill in the form that is displayed</i>, and then change <b>Apply Settings Changes</b> to True, to have the plugin set up your per-mode settings automatically.</p>

<p>4) That's it! You are good to go.</p>

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
<table border='0'>
<tr><td>Stop</td><td>No balancing, no players are selected to move</td></tr>
<tr><td>Slow</td><td>Few players are selected to move, all exclusions are applied, whether they are enabled by you or not</td></tr>
<tr><td>Fast</td><td>Many players are selected to move, no exclusions are applied, whether they are enabled by you or not</td></tr>
<tr><td>Adaptive</td><td>Starts out slow; if teams remain unbalanced, gradually selects more players to move; if teams are still unbalanced after <b>Seconds Until Adaptive Speed Becomes Fast</b>, many players are selected, etc.</td></tr>
<tr><td>Unstack</td><td>Do unstacking only, no balancing. May swap players when teams are not in balance.</td></tr>
</table></p>

<h3>Definition of Strong</h3>
<p>To configure the selection of strong players and weak players, you choose a definition for strong determined from:
<table border='0'>
<tr><td>Round Score</td><td> </td></tr>
<tr><td>Round SPM</td><td>Battlelog SPM</td></tr>
<tr><td>Round Kills</td><td> </td></tr>
<tr><td>Round KDR</td><td>Battlelog KDR</td></tr>
<tr><td>Player Rank</td><td> </td></tr>
<tr><td>Round KPM</td><td>Battlelog KPM</td></tr>
</table></p>

<h3>Ticket Percentage (Ratio)</h3>
<p>The ticket percentage ratio is calculated by taking the tickets of the winning team and dividing them by the tickets of the losing team, expressed as a percentage. For example, if the winning team has 550 tickets and the losing team has 500 tickets, the ticket percentage ratio is 110. This ratio is used to determine when teams are stacked. If the ticket percentage ratio exceeds the level that you set, unstacking swaps will begin.</p>

<h3>Unstacking</h3>
<p>Stacking refers to one team having more strong players than the other team. The result of stacked teams is lopsided wins and usually rage quitting from the losing team or attempts to switch to the winning team. If unstacking is enabled and the <b>Ticket Percentage (Ratio)</b> is exceeded, the plugin will attempt to unstack teams. To unstack teams, a strong player is selected from the winning team and is moved to the losing team. Then, a weak player is selected from the losing team and moved to the winning team. This is repeated until the round ends, or teams become unbalanced, or <b>Max&nbsp;Unstacking&nbsp;Swaps&nbsp;Per&nbsp;Round</b> is reached, whichever comes first.</p>

<h3>Merge Files</h3>
<p>A merge file is an external file that you can use to specify a list setting, such as <b>Whitelist</b>. An external file is convenient if you have long lists or if you share the same list across multiple game servers. The file is specified as <b>&lt;</b><i>filename.ext</i> on the first line of the list, with no whitespace. The contents of the file should be UTF-8 text, using the same contents and syntax as the list it will be merged with. The file should be stored in the procon/Configs folder. You can store as many differently named files there as you want, but each list can only use one merge file at a time.</p>

<h2>Settings</h2>
<p>Each setting is defined below. Settings are grouped into sections.</p>

<h3>0 - Presets</h3>
<p>See the <b>Quick Start</b> section above.</p>

<p><b>Enable Unstacking</b>: True or False, default False. Enables the per-mode unstacking features described in sections 3 and 8. Setting to False will not reset individual unstacking settings, it just disables the unstacking-related settings from operating. Setting to True enables all of your untacking-related settings.</p>

<p><b>Enable Settings Wizard</b>: True or False, default False. If set to True, the plugin will automatically change your per-mode settings based on some basic information that you provide. Several additional settings are displayed. The first is <b>Which Mode</b>. Select the mode you want to apply changes to. Fill in the rest of the settings below <b>Which Mode</b>; they are self-explanatory. When you are done, change the <b>Apply Settings Changes</b> from False to True. The changes will be applied, information for review will be displayed in the plugin.log window, and the wizard will set itself to False and hide itself again.</p>

<h3>1 - Settings</h3>
<p>These are general settings.</p>

<p><b>Debug Level</b>: Number from 0 to 9, default 2. Sets the amount of debug messages sent to plugin.log. Status messages for the state of the plugin may be seen at level 4 or higher. Complete details for operation of the plugin may be seen at level 7 or higher. When a problem with the plugin needs to be diagnosed, level 7 will often be required. Setting the level to 0 turns off all logging messages.</p>

<p><b>Maximum Server Size</b>: Number from 8 to 64, default 64. Maximum number of slots on your game server, regardless of game mode.</p>

<p><b>Enable Battlelog Requests</b>: True or False, default True. Enables making requests to Battlelog and uses BattlelogCache if available. Used to obtain clan tag for players and optionally, overview stats SPM, KDR, and KPM.</p>

<p><b>Which Battlelog Stats</b>: ClanTagOnly, AllTime, or Reset. Selects the type of Battlelog stats you want to use, clan tag only, All-Time or Reset stats.</p>

<p><b>Maximum Request Rate</b>: Number from 1 to 15, default 10. If <b>Enable Battlelog Requests</b> is set to True, defines the maximum number of Battlelog requests that are sent every 20 seconds.</p>

<p><b>Wait Timeout</b>: Number from 15 to 90, default 30. If <b>Enable Battlelog Requests</b> is set to True, defines the maximum number of seconds to wait for a reply from Battlelog or BattlelogCache before giving up.</p>

<p><b>Unlimited Team Switching During First Minutes Of Round</b>: Number greater than or equal to 0, default 5. Starting from the beginning of the round, this is the number of minutes that players are allowed to switch teams without restriction. After this time is expired, the plugin will prevent team switching that unbalances or stacks teams. The idea is to enable friends who were split up during the previous round due to autobalancing or unstacking to regroup so that they can play together this round. However, players who switch teams during this period are not excluded from being moved for autobalance or unstacking later in the round, unless some other exclusion applies them.</p>

<p><b>Seconds Until Adaptive Speed Becomes Fast</b>: Number of seconds greater than or equal to 120, default 180. If the autobalance speed is Adaptive and the autobalancer has been active for more than the specified number of seconds, the speed will be forced to Fast. This insures that teams don't remain unbalanced too long if Adaptive speed is not sufficient to move players.</p>

<p><b>Enable Whitelisting Of Reserved Slots List</b>: True or False, default True. Treats the reserved slots list as if it were added to the specified <b>Whitelist</b>.</p>

<p><b>Whitelist</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line, in any combination. The first item may also specify a file to merge into the list, e.g., <i>&lt;whitelist.txt</i>. See <b>Merge Files</b> above. If <b>On&nbsp;Whitelist</b> is enabled or the balance speed is <i>Slow</i>, any players on the whitelist are completely excluded from being moved by the plugin (except for between-round scrambling). Example list with the name of one player, tag of a clan, and GUID of another player:
<pre>
  PapaCharlie9
  LGN
  EA_20D5B089E734F589B1517C8069A37E28
</pre></p>

<p><b>Friends List</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, <b>two or more per line</b> separated by spaces, in any combination. The first item may also specify a file to merge into the list, e.g., <i>&lt;friends.txt</i>. See <b>Merge Files</b> above. If <b>On&nbsp;Friends&nbsp;List</b> is enabled, a player is excluded if at least one other player in his squad is also on the same friends sub-list. A sub-list is a single line of the Friends List with two or more names, tags or guids. No literal item may be duplicated anywhere in the list, but a player's clan tag may be on one sub-list and his name on another and his guid on a third. Example of two separate friends sub-lists:
<pre>
  PapaCharlie9 FTB C2C
  Tom Dick Harry EA_20D5B089E734F589B1517C8069A37E28 
</pre></p>

<p><b>Disperse Evenly List</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line (except for groups, see below), in any combination. Players found on this list will be split up and moved so that they are evenly dispersed across teams. The first item may also specify a file to merge into the list, e.g., <i>&lt;disperse.txt</i>. See <b>Merge Files</b> above. Groups of players, tags and guids may be specified to insure that they are always balanced to the opposite team from other specified groups. For example, if clan tag ABC is in group 1 and clan tag XYZ in is group 2, all players with clan tag ABC will eventually be balanced to one team and all players with clan tag XYZ will eventually be balanced to the other team. Groups 3 and 4 are used only for SQDM mode. A group is specified by starting an item in the list with a single digit, from 1 to 4, followed by a space, followed by a space separated list of names, tags or guids. Individual items and groups may be specified in any combination and any order in the list, though duplicating any item is an error. Here is an example list with individual players 'Joe' and 'Mary' and groups 1 and 2:
<pre>
  1 ABC LGN PapaCharlie9
  Joe
  2 XYZ EA_20D5B089E734F589B1517C8069A37E28
  Mary
</pre></p>

<h3>2 - Exclusions</h3>
<p>These settings define which players should be excluded from being moved for balance or unstacking. Changing a preset may overwrite the value of one or more of these settings. Changing one of these settings may change the value of the Preset, usually to None, to indicate a custom setting.</p>

<p><b>On Whitelist</b>: True or False, default True. If True, the <b>Whitelist</b> is used to exclude players. If False, the Whitelist is ignored.</p>

<p><b>On Friends List</b>: True or False, default False. If True, the Friend List is used to exclude players. If False, the <b>Friends List</b> is ignored.</p>

<p><b>Top Scorers</b>: True or False, default True. If True, the top 1, 2, or 3 players (depending on server population and mode) on each team are excluded from moves for balancing or unstacking. This is to reduce the whining and QQing when a team loses their top players to autobalancing.</p>

<p><b>Same Clan Tags In Squad</b>: True or False, default True. If True, a player will be excluded from being moved for balancing or unstacking if they are a member of a squad (or team, in the case of SQDM) that has at least one other player in it with the same clan tag.</p>

<p><b>Same Clan Tags For Rank Dispersal</b>: True or False, default False. If True, dispersal by per-mode <b>Disperse Evenly By Rank &gt;=</b> will not be applied if the player has a clan tag that at least one other player on the same team has.</p>

<p><b>Minutes After Joining</b>: Number greater than or equal to 0, default 5. After joining the server, a player is excluded from being moved for balance or unstacking for this number of minutes. Set to 0 to disable. Keep in mind that most joining players were already assigned to the team with the least players. They have already 'paid their dues'.</p>

<p><b>Minutes After Being Moved</b>: Number greater than or equal to 0, default 90. After being moved for balance or unstacking, a player is excluded from being moved again for the specified number of minutes. Set to 0 to disable.</p>

<h3>3 - Round Phase and Population Settings</h3>
<p>These settings control balancing and unstacking, depending on the round phase and server population.
For each phase, there are three unstacking settings for server population: Low, Medium and High, by number of players. Each number is the ticket percentage ratio that triggers unstacking for each combination of phase and population. Setting the value to 0 disables team unstacking for that combination. If the number is not 0, if the ratio of the winning team's tickets to the losing teams tickets is equal to or greater than the ticket percentage ratio specified, unstacking will be activated.</p>

<p><i>Example</i>: for the <b>Ticket Percentage To Unstack</b> setting, there are three phases, Early, Mid and Late. For each phase, the value is a list of 3 number, either 0 or greater than 100, one for each of the population levels of Low, Medium, and High, respectively:
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

<h3>4 - Scrambler</h3>
<p>These settings define options for between-round scrambling of teams. The setting <b>Enable Scrambler</b> is a per-mode setting, which allows you to decide on a mode-by-mode basis whether to use scrambling between rounds or not. See the per-mode settings in Section 8 below for more details. Note that whitelisted players are <b>not</b> excluded from scrambling and that scrambling is not possible with SQDM.</p>

<p><b>Only On New Maps</b>: True or False, default True. If True, scrambles will happen only after the last round of a map. For example, if a map has 2 rounds, there will be no scramble after round 1, only after round 2. If False, scrambling will be attempted at the end of every round.</p>

<p><b>Only On Final Ticket Percentage &gt;=</b>: Number greater than 100 or equal to 0, default 120. This is the ratio between the winning and losing teams final ticket counts at the end of the round. In count-down modes like Conquest, this is the ratio of the difference between the maximum starting tickets and the final ticket count. For example, on a 1000 ticket server, if the final ticket counts are 0/250, the ratio is (1000-0)/(1000-250)=1000/750=133%. Since that is greater than 120, scrambling would be done. For count-up modes like TDM, the ratio is final ticket values. If this value is set to 0, scrambling will occur regardless of final ticket counts.</p>

<p><b>Scramble By</b>: One of the values defined in <b>Definition Of Strong</b> above. Determines how strong vs. weak players are chosen for scrambling.</p>

<p><b>Keep Squads Together</b>: True or False, default True. If True, during scrambling, an attempt is made to keep players in a squad together so that they are moved as a squad. This is not always possible and sometimes squads may be split up even when this setting is True. The squad ID may change, e.g., if the players were originally in Alpha, they may end up in Echo on the other team.</p>

<p><b>Keep Clan Tags In Same Squad</b>: True or False, default True. Only visible if <b>Keep Squads Together</b> is set to False. If True, players in a squad with other players with the same clan tag will be kept together, if possible. Players in the same squad that do not have the same tag may get moved to another squad. The squad ID may change, e.g., if the players were originally in Hotel, they may end up in Charlie on the other team.</p>

<p><b>Divide By</b>: None, ClanTag, or DispersalGroup. Specifies how players should be divided into teams during scrambling. ClanTag divides all players evenly between the two teams if they have the clan tag specified in <b>Clan Tag To Divide By</b>. Only one tag be specified. DispersalGroup divides players to their assigned dispersal group, if they are in one of the two groups defined in the <b>Disperse Evenly List</b>, if any.

<p><b>Delay Seconds</b>: Number of seconds greater than or equal to 0 and less than or equal to 50, default 30. Number of seconds to wait after the round ends before doing the scramble. If done too soon, many players may leave after the scramble, resulting in wildly unequal teams. If done too late, the next level may load and the game server will swap players to opposite teams, interfering with the scramble in progress, which may result in wildly unequal teams.</p>

<h3>5 - Messages</h3>
<p>These settings define all of the chat and yell messages that are sent to players when various actions are taken by the plugin. All of the messages are in pairs, one for chat, one for yell. If both the chat and the yell messages are defined and <b>Quiet&nbsp;Mode</b> is not set to True, both will be sent at the same time. The message setting descriptions apply to both chat and yell. To disable a chat message for a specific actcion, delete the message and leave it empty. To disable theyell message for a specific action, delete the message and leave it empty.</p>

<p>Several substitution macros are defined. You may use them in either chat or yell messages:
<table border='0'>
<tr><td>%name%</td><td>player name</td></tr>
<tr><td>%tag%</td><td>player clan tag</td></tr>
<tr><td>%fromTeam%</td><td>team the player is currently on, as 'US' or 'RU', or 'Alpha', 'Bravo', 'Charlie', or 'Delta' for SQDM</td></tr>
<tr><td>%toTeam%</td><td>team the plugin will move the player to, same team name substitutions as for %fromTeam%</td></tr>
<tr><td>%reason%</td><td>ONLY APPLIES TO BAD TEAM SWITCH: reason for switching the player back, may contain other replacements</td></tr>
</table></p>

<p><b>Quiet Mode</b>: True or False, default False. If False, chat messages are sent to all players and yells are sent to the player being moved. If True, chat and yell messages are only sent to the player being moved.</p>

<p><b>Yell Duration Seconds</b>: A number greater than 0 and less than or equal to 20, or 0. If set to 0, all yells are disabled, even if they have non-empty messages. All yells have the same duration. This duration also controls the delay between when a player is warned and when the are unswitched (see Section 6).</p>

<p><b>Moved For Balance</b>: Message sent after a player is moved for balance.</p>

<p><b>Moved To Unstack</b>: Message sent after a player is moved to unstack teams.</p>

<p><b>Detected Bad Team Switch</b>: Message sent after a player tries to make a forbidden team switch if [b]Enable Immediate Unswitch[/b] is set to False (see Section 6 below) or mode is Squad Deathmatch. The message is sent before the player is admin killed and sent back to his original team.</p>

<p><b>Bad Because: Moved By Balancer</b>: Replacement for %reason% if the player tried to move to a different team from the one the plugin have moved them to for balance or unstacking.</p>

<p><b>Bad Because: Winning Team</b>: Replacement for %reason% if the player tried to move to the winning team.</p>

<p><b>Bad Because: Biggest Team</b>: Replacement for %reason% if the player tried to move to the biggest team.</p>

<p><b>Bad Because: Rank</b>: Replacement for %reason% if the player has Rank greater than or equal to the per-mode <b>Disperse Evenly By Rank</b> setting.</p>

<p><b>Bad Because: Dispersal List</b>: Replacement for %reason% if the player is a member of the <b>Disperse Evenly List</b>.</p>

<p><b>Detected Good Team Switch</b>: Message sent after a player switches from the winning team to the losing team, or from the biggest team to the smallest team. There is no follow-up message, this is the only one sent.</p>

<p><b>After Unswitching</b>: Message sent after a player is killed by admin and moved back to the team he was assigned to. This message is sent after the <b>Detected Bad Team Switch</b> message.</p>

<h3>6 - Unswitcher</h3>
<p>This section controls the unswitcher. Every time a player tries to switch to a different team, the unswitcher checks if the switch is allowed or forbidden. If forbidden, he will be moved back by the plugin (see <b>Enable Immediate Unswitch</b> for details about how). The possible values are <i>Always</i>, which means do not allow (always forbid) this type of team switching, <i>Never</i>, which means allow team switching of this type, and <i>LatePhaseOnly</i>, which means allow team switching of this type until Late Phase, then no longer allow it (forbid it). Note that setting any of the <b>Forbid ...</b> settings to <i>Never</i> will reduce the effectiveness of the balancer and unstacker.</p>

<p><b>Forbid Switching After Autobalance</b>: Always, Never, or LatePhaseOnly, default Always. Controls team switching after being moved to a different team for balance or unstacking. This setting forbids moved players from moving back to their original team.</p>

<p><b>Forbid Switching To Winning Team</b>: Always, Never, or LatePhaseOnly, default Always. Controls switching to the winning team.</p>

<p><b>Forbid Switch To Biggest Team</b>: Always, Never, or LatePhaseOnly, default Always. Contorls switching to the biggest team.</p>

<p><b>Forbid Switch After Dispersal</b>: Always, Never, or LatePhaseOnly, default Always. Controls team switching after being moved to a different team due to <b>Disperse Evenly By Rank</b> or the <b>Disperse Evenly List</b>. This setting forbids them from moving back to their original team.</p>

<p><b>Enable Immediate Unswitch</b>: True or False, default True. If True, if a player tries to make a forbidden team switch, the plugin will immediately move them back without any warning. They will only see the <b>After Unswitching</b> message(s). If False, the plugin will wait until the player spawns, it will then post the <b>Detected Bad Team Switch</b> message(s), it will wait <b>Yell Duration Seconds</b> seconds, then it will admin kill the player and move him back. <b>NOTE: Does not apply to SQDM. SQDM is always treated as this were set to False.</b></p>

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
<tr><td>Squad Deathmatch</td><td>Standard settings, similar to Conquest, except that unstacking is disabled (default 0)</td></tr>
<tr><td>Squad Rush</td><td>Has unique settings shared with Rush and no other modes</td></tr>
<tr><td>Superiority</td><td>Air and Tank Superiority</td></tr>
<tr><td>Team Deathmatch</td><td>TDM and TDM Close Quarters, standard settings, similar to Conquest</td></tr>
<tr><td>Unknown or New Mode</td><td>Generic settings for any new mode that gets introduced before this plugin gets updated</td></tr>
</table></p>

<p>These are the settings that are common to most modes:</p>

<p><b>Max Players</b>: Number greater than or equal to 8 and less than or equal to <b>Maximum Server Size</b>. Some modes might be set up in UMM or Adaptive Server Size or other plugins with a lower maximum than the server maximum. If you set a lower value in your server settings or in a plugin, set the same setting here. This is important for calculating population size correctly.</p>

<p><b>Check Team Stacking After First Minutes</b>: Number greater than or equal to 0. From the start of the round, this setting is the number of minutes to wait before activating unstacking. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Max Unstacking Swaps Per Round</b>: Number greater than or equal to 0. To prevent the plugin from swapping every player on every team for unstacking, a maximum per round is set here. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Number Of Swaps Per Group</b>: Number greater than or equal to 0 and less than or equal to <b>Max Unstacking Swaps Per Round</b>, ideally an integral factor, e.g., if <b>Max Unstacking Swaps Per Round</b> is 12, <b>Number Of Swaps Per Group</b> may be 1, 2, 3, 4, 6 or 12. During unstacking, swaps are done as quickly as possible, up to this number. Once this number of swaps is reached, the <b>Delay Seconds Between Swap Groups</b> delay is applied before further swaps are attempted.</p>

<p><b>Delay Seconds Between Swap Groups</b>: Number greater than or equal to 60. After a group of unstacking swaps, wait this number of seconds before doing another group of unstacking swaps.</p>

<p><b>Determine Strong Players By</b>: Choice based on method. The setting defines how strong players are determined. Any player that is not a strong player is a weak player. See the <b>Definition of Strong</b> section above for the list of settings. All players in a single team are sorted by the specified definition. Any player above the median position after sorting is considered strong. For example, suppose there are 31 players on a team and this setting is set to <i>RoundScore</i> and after sorting, the median is position #16. If this player is position #7, he is considered strong. If his position is #16 or #17, he is considered weak.</p>

<p><b>Percent Of Top Of Team Is Strong</b>: Number greater than or equal to 5 and less than or equal to 50, or 0. After sorting a team with the <b>Determine Strong Players By</b> choice, this percentage determines the portion of the top players to define as strong. Default is 50 so that any player above the median counts as strong. CAUTION: This setting is changed when the <b>Preset</b> is changed, previous values are overwritten for all modes.</p>

<p><b>Disperse Evenly By Rank &gt;=</b>: Number greater than or equal to 0 and less than or equal to 145, default 0. Any players with this absolute rank (Colonel 100 is 145) or higher will be dispersed evenly across teams. This is useful to insure that Colonel 100 ranked players don't all stack on one team. Set to 0 to disable.</p>

<p><b>Enable Disperse Evenly List</b>:  True or False, default False. If set to true, the players are matched against the <b>Disperse Evenly List</b> and any that match will be dispersed evenly across teams. This is useful to insure that certain clans or groups of players don't always dominate whatever team they are not on.</p>

<p><b>Definition Of High Population For Players &gt;=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the High population level. If the total number of players in the server is greater than or equal to this number, population is High.</p>

<p><b>Definition Of Low Population For Players &lt;=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the Low population level. If the total number of players in the server is less than or equal to this number, population is Low. If the total number is between the definition of High and Low, it is Medium.</p>

<p><b>Definition Of Early Phase As Tickets From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as tickets from the start of the round. For example, if your round starts with 1500 tickets and you set this to 300, as long as the ticket level for all teams is greater than or equal to 1500-300=1200, the phase is Early. Set to 0 to disable Early phase.</p>

<p><b>Definition Of Late Phase As Tickets From End</b>: Number greater than or equal to 0. This is where you define the Late phase, as tickets from the end of the round. For example, if you set this to 300 and at least one team in Conquest has less than 300 tickets less, the phase is Late. If the ticket level of both teams is between the Early and Late settings, the phase is Mid. Set to 0 to disable Late phase.</p>

<p><b>Enable Scrambler</b>: True or False, default False. If set to True, between-round scrambling of teams will be attempted for rounds played in this mode, depending on the settings in Section 5.</p>

<p>These settings are unique to Conquest.</p>

<p><b>Enable Metro Adjustments</b>: True or False, default False. This setting should be set to True when Metro is one of several maps in a Conquest Large or Conquest Small rotation. This setting insures that no players are moved to the losing team, which is usually futile. The actual effect is that when the map is Metro, during Early and Late phase, the Balance Speed is forced to be Stop and the Unstack Percentage Ratio is forced to be 0%. During Mid Phase, the Balance Speed is forced to be Slow. The Unstack Percentage Ratio is left unchanged for Mid Phase. If Metro is the only Conquest map in the rotation or if Metro is not in the rotation at all, set this setting to False. See also <b>Metro Adjusted Definition Of Late Phase</b>.</p>

<p><b>Metro Adjusted Definition Of Late Phase</b>: Number greater than or equal to 0. This setting is visible only when <b>Enable Metro Adjustments</b> is set to True. When the map is Metro, the value specified here is used instead of <b>Definition Of Late Phase As Tickets From End</b>. This allows you to specify a much longer Late phase than for the other Conquest maps in your rotation. You generally want Metro Late phase to be the second half of your tickets, for example, if you have 1000 tickets, set this setting to 500.</p>

<p>These settings are unique to CTF.</p>

<p><b>Definition Of Early Phase As Minutes From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as minutes from the start of the round. For example, if your round starts with 20 minutes on the clock and you set this to 5, the phase is Early until 20-5=15 minutes are left on the clock.</p>

<p><b>Definition Of Late Phase As Minutes From End</b>: Number greater than or equal to 0. This is where you define the Late phase, as minutes from the end of the round. For example, if your round starts with 20 minutes on the clock and you set this to 8, the phase is Late for when there 8 minutes or less left on the clock.</p>

<p>These settings are unique to Rush and Squad Rush.</p>

<p>Rush and Squad Rush require adjustments to the ticket percentage to unstack values specified in section 3 above. For example, if you have a mixed mode server with TDM and Rush, you may set ticket percentage to unstack to 120 for certain combinations of phase and population. This works great for TDM with 200 tickets. It does not work well for Rush with 150 tickets. The ticket ratio may easily exceed 120% without the teams being stacked. It's just the nature of the stages. Rather than have completely different settings for Rush and Squad Rush for section 3, instead, the per-mode settings define adjustments to the section 3 settings. For example, if you specify 30 for <b>Stage 1 Ticket Percentage To Unstack Adjustment</b>, 30 is added to 120 to yield 150% as the ratio for stage 1. You may also use negative numbers to reduce the value, for example, if the normal setting is 120 and you want stage 4 to have no unstacking, you may set the adjustment to -120. If the adjustment results in a value less than or equal to 100, it is set to 0. If you use 0 for the adjustment value, no change is made. <b>If the normal value is 0, no adjustment is applied.</b> Otherwise, the adjustment is applied to all phase and population combinations for that stage. Rush maps range from 3 to 5 stages. Most are 4. To account for maps with up to 5 stages, there is one setting for stage 4 and stage 5. Treat this setting as the 'last' stage.</p>

<p><b>Stage 1 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. If the defending team is stacked, the game will be unlikely to get past stage 1, so ratios in the range 125 to 150 after adjustment are good for stage 1. For example, if your normal ratio is 120, set the adjustment to 5 to get 125 for Rush.</p>

<p><b>Stage 2 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. If the attacking team is stacked, the game will get to stage 2 quickly, so ratios in the range 125 to 150 are good for stage 2. For example, if  your normal ratio is 120, set the adjustment to 30 to get 150 for Rush</p>

<p><b>Stage 3 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. Evenly matched teams will often get to stage 3, so set the ratio high to catch unsual situations only, ratios in the range 200 or more are good for stage 3. For example, if your normal ratio is 120, set the adjustment to 80 to get 200 for Rush.</p>

<p><b>Stage 4 And 5 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. This is tricky, since a team that is stacked for attackers or evenly matched teams will both get to the last stage. To give the benefit of the doubt, aim for a ratio of 0. For example, if your normal ratio is 120, set the adjustment to -120 to get 0 for Rush.</p>

<p><b>Seconds To Check For New Stage </b>: Number greater than or equal to 5 and less than or equal to 30, default is 10. Number of seconds between each check to see if a new stage has started. The check is a guess since BF3 does not report stage changes, so it is possible for the plugin to guess incorrectly.</p>


<h3>9 - Debugging</h3>
<p>These settings are used for debugging problems with the plugin.</p>

<p><b>Show Command In Log</b>: Special commands may be typed in this text area to display information in plugin.log. Type <i>help></i> into the text field and press Enter (type a return). A list of commands will be written to plugin.log.</p>

<p><b>Log Chat</b>: True or False, default True. If set to True, all chat messages sent by the plugin will be logged in chat.log.</p>

<p><b>Enable Logging Only Mode</b>: True or False, default False. If set to True, the plugin will only log messages. No move, chat or yell commands will be sent to the game server. If set to False, the plugin will operate normally.</p>

<p><b>Enable External Logging</b>: True or False, default False. If set to True, plugin.log messages will also be sent to an external log file in Procon's Log folder, by game server connection. See <b>External Log Suffix</b>. </p>

<p><b>External Log Suffix</b>: Suffix for file name used for the external log file, default is <i>_mb.log</i>. The path to procon/Logs/<i>ip_port</i> is used to write a log file with the current date in YYYYMMDD format prepended to the suffix you supply, for example, 20130515_mb.log.</p>

<h2>Development</h2>
<p>This plugin is an open source project hosted on GitHub.com. The repo is located at
<a href='https://github.com/PapaCharlie9/multi-balancer'>https://github.com/PapaCharlie9/multi-balancer</a> and
the master branch is used for public distributions. See the <a href='https://github.com/PapaCharlie9/multi-balancer/tags'>Tags</a> tab for the latest ZIP distribution. If you would like to offer bug fixes or new features, feel free to fork the repo and submit pull requests. Post questions and problem reports in the forum Plugin thread.</p>
";
#endregion

} // end MULTIbalancerUtils
#endregion

} // end namespace PRoConEvents


